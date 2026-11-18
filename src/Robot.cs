using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DeskKeeper;

/// <summary>
/// Дубина von oben , fährt wie der Saugroboter: dreht sich in Fahrtrichtung, fährt weich an und bremst.
/// Der Arm sitzt auf einem Drehteller; seine Haltung ist (Gier relativ zum Fahrwerk, Reichweite, Höhe).
/// Der Anhänger folgt als echter Deichselwagen (Schleppkurve) und schneidet Kurven wie ein Wagen hinter der Lok.
/// Szene „Иерихон“: jedes Symbol einzeln aus dem Anhänger holen und absetzen. Wiederherstellen: Symbol greifen, hintragen.
/// </summary>
sealed class Robot : IHelper
{
    enum Kind { Drive, Reach, Grip, Swing, Release, Lift, Carry }
    readonly record struct Arm(double Yaw, double Reach, double Height);   // Yaw relativ zum Fahrwerk (Bogenmaß), Reach in r, Height 0…1
    record Step(Kind Kind, double From, double To, Point BaseA, Point BaseB, double HeadA, double HeadB, Arm ArmA, Arm ArmB, string? Icon, Point Drop);

    readonly List<Step> steps = [];
    readonly double r, start;
    readonly bool trailer;
    readonly Rect[] screens;
    readonly Rect primary;
    // Anhänger-Simulation (120 Hz)
    readonly List<(Point Center, double Heading)> cart = [];
    const double CartRate = 120;
    static readonly Arm Rest = new(0, 0.42, 0.85);

    public double End { get; }
    public int Carried => steps.Count(s => s.Kind == Kind.Release);
    /// <summary>Anzahl echter Fahrten und Summe aller Lenkwinkel (Grad) – weniger ist ruhiger.</summary>
    public int Drives => steps.Count(s => s.Kind is Kind.Drive or Kind.Carry && (s.BaseB - s.BaseA).Length > 1);
    public double SteeringDegrees
    {
        get
        {
            double sum = 0, last = double.NaN;
            for (double t = start; t < End; t += 0.05) { double h = BodyAt(t).Head; if (!double.IsNaN(last)) sum += Math.Abs(Wrap(h - last)); last = h; }
            return sum * 180 / Math.PI;
        }
    }

    public Robot(IReadOnlyList<IconInfo> icons, Func<string, Point> pickOf, IReadOnlyDictionary<string, Point> home, bool fromCart,
        double robotStart, double size, Rect[] screens, Rect primary, IEnumerable<string> order)
    {
        r = size; start = robotStart; trailer = fromCart; this.screens = screens; this.primary = primary;
        var basePoint = new Point(primary.Left - r * 2.6, primary.Bottom - r * 1.6);
        double head = 0;
        var armPose = Rest;
        double t = robotStart;
        var ids = order.ToList();
        double pace = Math.Clamp(18.0 / Math.Max(1, ids.Count), 0.62, 1.2);
        // 19.11.2026: Armbewegungen beim Greifen und Hinstellen ruhiger – auch bei vielen Symbolen nie schneller als Grundtempo
        double arm = Math.Max(1.0, pace) * 1.35;

        // 1. Durchgang: Fahrwege und Zeiten (Armziele für den Anhänger kommen nach der Simulation)
        for (int n = 0; n < ids.Count; n++)
        {
            var id = ids[n];
            Point drop = home[id];
            Point? following = n + 1 < ids.Count ? (fromCart ? home[ids[n + 1]] : pickOf(ids[n + 1])) : null;
            if (fromCart)
            {
                var stand = InReach(drop, basePoint) ? basePoint : Stand(drop, basePoint, head, following);
                Drive(ref t, ref basePoint, ref head, ref armPose, stand, pace);
                steps.Add(new Step(Kind.Reach, t, t += 0.85 * arm, stand, stand, head, head, armPose, default, id, drop));   // zum Anhänger ausholen: weiter Schwenk, ruhig
                steps.Add(new Step(Kind.Grip, t, t += 0.24 * arm, stand, stand, head, head, default, default, id, drop));
                var place = Aim(stand, head, drop, 0);
                steps.Add(new Step(Kind.Swing, t, t += 0.85 * arm, stand, stand, head, head, default, place, id, drop));
                steps.Add(new Step(Kind.Release, t, t += 0.34 * arm, stand, stand, head, head, place, place, id, drop));
                armPose = place;
            }
            else
            {
                Point pick = pickOf(id);
                var stand = InReach(pick, basePoint) ? basePoint : Stand(pick, basePoint, head);
                Drive(ref t, ref basePoint, ref head, ref armPose, stand, pace);
                var grab = Aim(stand, head, pick, 0);
                steps.Add(new Step(Kind.Reach, t, t += 0.85 * arm, stand, stand, head, head, armPose, grab, id, pick));
                steps.Add(new Step(Kind.Grip, t, t += 0.26 * arm, stand, stand, head, head, grab, grab, id, pick));
                var hold = new Arm(0, 0.75, 0.8);
                steps.Add(new Step(Kind.Lift, t, t += 0.5 * arm, stand, stand, head, head, grab, hold, id, pick));
                var standDrop = InReach(drop, stand) ? stand : Stand(drop, stand, head);
                var (c1, c2, turn) = Curve(stand, head, standDrop);
                double carry = Math.Clamp(CurveLength(stand, c1, c2, standDrop) / (r * 1.9), 0.9, 4.2) * pace;
                steps.Add(new Step(Kind.Carry, t, t += carry, stand, standDrop, head, turn, hold, hold, id, pick));
                curves[steps.Count - 1] = (c1, c2);
                head = turn;
                var place = Aim(standDrop, head, drop, 0);
                steps.Add(new Step(Kind.Swing, t, t += 0.75 * arm, standDrop, standDrop, head, head, hold, place, id, drop));
                steps.Add(new Step(Kind.Release, t, t += 0.36 * arm, standDrop, standDrop, head, head, place, place, id, drop));
                armPose = place;
                basePoint = standDrop;
            }
        }
        // Wegfahren über den nächsten Rand
        var screen = ScreenOf(basePoint);
        var exit = new Point(basePoint.X < screen.Left + screen.Width / 2 ? screen.Left - r * 3.5 : screen.Right + r * 3.5, basePoint.Y);
        Drive(ref t, ref basePoint, ref head, ref armPose, exit, pace);
        End = t + 0.1;

        // 2. Anhänger simulieren, dann Greifhaltungen in den Laderaum eintragen
        Simulate();
        for (int i = 0; i < steps.Count; i++)
        {
            var s = steps[i];
            if (s.Kind == Kind.Reach && fromCart)
            {
                var cargo = CartAt(s.To).Center;
                var into = Aim(s.BaseA, s.HeadA, cargo, 0.45);
                steps[i] = s with { ArmB = into };
                steps[i + 1] = steps[i + 1] with { ArmA = into, ArmB = into };
                steps[i + 2] = steps[i + 2] with { ArmA = into };
            }
        }
    }

    /// <summary>Fahrt auf einer weichen Kurve wie ein Fahrzeug mit Wenderadius (kein Drehen auf der Stelle): Start in der
    /// aktuellen Fahrtrichtung, Ende in Anfahrtrichtung. Der Anhänger folgt dadurch wie ein echter Anhänger.</summary>
    void Drive(ref double t, ref Point from, ref double head, ref Arm arm, Point to, double pace)
    {
        double dist = (to - from).Length;
        if (dist < 1) return;   // steht schon richtig: Arm bleibt, die nächste Bewegung beginnt aus der jetzigen Haltung
        var (c1, c2, endHead) = Curve(from, head, to);
        double length = dist < 1 ? 0 : CurveLength(from, c1, c2, to);
        double drive = dist < 1 ? 0 : Math.Clamp(length / (r * 2.1), 0.7, 4.5) * pace;   // langsam wie ein schwerer Manipulator
        steps.Add(new Step(Kind.Drive, t, t += drive, from, to, head, endHead, arm, Rest, null, default));
        curves[steps.Count - 1] = (c1, c2);
        from = to; head = endHead; arm = Rest;
    }
    readonly Dictionary<int, (Point C1, Point C2)> curves = [];

    (Point C1, Point C2, double EndHead) Curve(Point from, double head, Point to)
    {
        double dist = (to - from).Length;
        if (dist < r * 0.3) return (from, to, head);
        double k = Math.Clamp(dist * 0.45, r * 0.6, r * 2.4);
        // Bei starkem Richtungswechsel kurze Tangente: enger, sauberer Bogen statt Schleife
        double change = Math.Abs(Wrap(Math.Atan2(to.Y - from.Y, to.X - from.X) - head));
        var c1 = from + new Vector(Math.Cos(head), Math.Sin(head)) * k * Math.Max(0.2, Math.Cos(change / 2));
        var end = to - c1; end.Normalize();
        var c2 = to - end * k * 0.6;
        return (c1, c2, Math.Atan2(end.Y, end.X));
    }

    static double CurveLength(Point a, Point b, Point c, Point d)
    {
        double total = 0; var last = a;
        for (int i = 1; i <= 16; i++) { var p = Bezier(a, b, c, d, i / 16.0); total += (p - last).Length; last = p; }
        return total;
    }
    static Point Bezier(Point a, Point b, Point c, Point d, double t)
    {
        double w = 1 - t;
        return new Point(w * w * w * a.X + 3 * w * w * t * b.X + 3 * w * t * t * c.X + t * t * t * d.X, w * w * w * a.Y + 3 * w * w * t * b.Y + 3 * w * t * t * c.Y + t * t * t * d.Y);
    }

    static double Heading(Point from, Point to, double current) => (to - from).Length < 1 ? current : Math.Atan2(to.Y - from.Y, to.X - from.X);

    /// <summary>
    /// Standplatz: in Reichweite neben dem Ziel, auf der Seite mit Platz für Дубина samt Anhänger
    /// (am Rand also zur Bildschirmmitte hin), bevorzugt die Seite, aus der Дубина kommt.
    /// </summary>
    /// <summary>Ziel liegt schon in Armreichweite: nicht fahren, nur den Arm drehen (spart unnötige Manöver).</summary>
    bool InReach(Point target, Point basePoint)
    {
        double d = (target - basePoint).Length;
        return d >= r * 0.5 && d <= r * 1.55 && ScreenOf(target).Contains(basePoint);
    }

    Point Stand(Point target, Point comingFrom, double heading, Point? following = null)
    {
        var screen = ScreenOf(target);
        var come = comingFrom - target; if (come.LengthSquared > 1) come.Normalize();
        Point best = target; double bestScore = double.MinValue;
        for (int k = 0; k < 16; k++)
        {
            double a = k * Math.PI / 8;
            var dir = new Vector(Math.Cos(a), Math.Sin(a));
            var p = target + dir * (r * 1.1);
            double room = Math.Min(Math.Min(p.X - screen.Left, screen.Right - p.X), Math.Min(p.Y - screen.Top, screen.Bottom - p.Y));
            // Platz, kurze Fahrt und wenig Lenken bevorzugen (keine Schleifen)
            var path = p - comingFrom;
            double turn = path.LengthSquared > 1 ? Math.Abs(Wrap(Math.Atan2(path.Y, path.X) - heading)) / Math.PI : 0;
            double score = Math.Min(room, r * 2.4) / r + 0.5 * Vector.Multiply(dir, come) - 1.1 * turn - 0.12 * path.Length / r
                + (following is { } next && InReach(next, p) ? 1.6 : 0);   // von hier erreicht der Arm auch das nächste Symbol
            if (score > bestScore) { bestScore = score; best = p; }
        }
        double m = r * 0.7;
        return new Point(Math.Clamp(best.X, screen.Left + m, screen.Right - m), Math.Clamp(best.Y, screen.Top + m, screen.Bottom - m));
    }

    Arm Aim(Point basePoint, double head, Point world, double height)
    {
        var d = world - basePoint;
        return new Arm(Wrap(Math.Atan2(d.Y, d.X) - head), Math.Clamp(d.Length / r, 0.35, 1.6), height);
    }

    // ------------------------------------------------------------ Zustand zur Zeit t

    Step? StepAt(double t)
    {
        Step? last = null;
        foreach (var s in steps) { if (t < s.From) break; last = s; if (t < s.To) return s; }
        return last;
    }
    int IndexOf(Step s) => steps.IndexOf(s);

    (Point Base, double Head) BodyAt(double t)
    {
        var s = StepAt(t);
        if (s is null) return (steps.Count > 0 ? steps[0].BaseA : default, 0);
        double k = Math.Clamp((t - s.From) / Math.Max(1e-6, s.To - s.From), 0, 1);
        if ((s.Kind is Kind.Drive or Kind.Carry) && curves.TryGetValue(IndexOf(s), out var c))
        {
            double q = Drive5(k);
            var pos = Bezier(s.BaseA, c.C1, c.C2, s.BaseB, q);
            var ahead = Bezier(s.BaseA, c.C1, c.C2, s.BaseB, Math.Min(1, q + 0.01)) - Bezier(s.BaseA, c.C1, c.C2, s.BaseB, Math.Max(0, q - 0.01));
            double heading = ahead.LengthSquared > 1e-4 ? Math.Atan2(ahead.Y, ahead.X) : s.HeadA + Wrap(s.HeadB - s.HeadA) * q;
            return (pos, heading);
        }
        return (s.BaseA, s.HeadA);
    }

    Arm ArmAt(double t)
    {
        var s = StepAt(t);
        if (s is null) return Rest;
        double k = Math.Clamp((t - s.From) / Math.Max(1e-6, s.To - s.From), 0, 1);
        if (s.Kind == Kind.Drive) { double span = s.To - s.From, back = Math.Min(span, Math.Max(1.0, span * 0.6)); k = Math.Clamp((t - s.From) / Math.Max(1e-6, back), 0, 1); }
        // Drehteller führt, Ausleger folgt etwas verzögert; beim Schwenken über das Fahrwerk hinweg angehoben
        double yawK = Drive5(k), reachK = Drive5(Math.Clamp((k - 0.08) / 0.92, 0, 1));
        double height = s.ArmA.Height + (s.ArmB.Height - s.ArmA.Height) * Drive5(k);
        if (s.Kind == Kind.Swing) height += 0.75 * Math.Sin(Math.PI * k);
        if (s.Kind == Kind.Reach) height += 0.35 * Math.Sin(Math.PI * k);
        return new Arm(s.ArmA.Yaw + Wrap(s.ArmB.Yaw - s.ArmA.Yaw) * yawK, s.ArmA.Reach + (s.ArmB.Reach - s.ArmA.Reach) * reachK, Math.Clamp(height, 0, 1.3));
    }

    /// <summary>Prüfung: größter Sprung des Arms von Bild zu Bild (Drehung in Grad, Ausleger in Fahrgestellbreiten) bei 60 Bildern/s.</summary>
    internal (double Yaw, double Reach) LargestArmStep()
    {
        double yaw = 0, reach = 0; var last = ArmAt(start);
        for (double t = start + 1 / 60.0; t < End; t += 1 / 60.0)
        {
            var a = ArmAt(t);
            yaw = Math.Max(yaw, Math.Abs(Wrap(a.Yaw - last.Yaw)) * 180 / Math.PI);
            reach = Math.Max(reach, Math.Abs(a.Reach - last.Reach));
            last = a;
        }
        return (yaw, reach);
    }

    double JawAt(double t)
    {
        var s = StepAt(t);
        if (s is null || t >= s.To) return 0.4;
        double k = (t - s.From) / (s.To - s.From);
        return s.Kind switch { Kind.Reach => 0.4 + 0.6 * Math.Min(1, k * 1.6), Kind.Grip => 1 - k, Kind.Lift or Kind.Carry or Kind.Swing => 0, Kind.Release => Math.Min(1, k * 2.5), _ => 0.4 };
    }

    Point Tip(double t)
    {
        var (b, h) = BodyAt(t);
        var a = ArmAt(t);
        return b + new Vector(Math.Cos(h + a.Yaw), Math.Sin(h + a.Yaw)) * a.Reach * r;
    }

    /// <summary>Position eines Symbols, solange Дубина es hält; handled=false: Szene entscheidet.</summary>
    public (bool Handled, Point? At) IconAt(string id, double t)
    {
        foreach (var s in steps)
        {
            if (s.Icon != id) continue;
            if (t < s.From) return (false, null);
            switch (s.Kind)
            {
                case Kind.Reach when trailer:
                    if (t < s.To) return (true, null);   // liegt noch im Anhänger
                    break;
                case Kind.Grip when trailer:
                    if (t < s.To) return (true, t - s.From > (s.To - s.From) * 0.5 ? Tip(t) : null);
                    break;
                case Kind.Grip or Kind.Lift or Kind.Carry or Kind.Swing:
                    if (t < s.To) return (true, Tip(t));
                    break;
                case Kind.Release:
                    return (true, s.Drop);
            }
        }
        return (false, null);
    }

    // ------------------------------------------------------------ Anhänger (Schleppkurve)

    Point HitchOf(Point b, double head) => b - new Vector(Math.Cos(head), Math.Sin(head)) * (r * 0.46);
    double Bar => r * 0.5;   // kurze Deichsel wie bei Auto mit Anhänger

    void Simulate()
    {
        var (b0, h0) = BodyAt(start);
        var hitch = HitchOf(b0, h0);
        var center = hitch - new Vector(Math.Cos(h0), Math.Sin(h0)) * Bar;
        for (double t = start; t <= End + 0.05; t += 1 / CartRate)
        {
            var (b, h) = BodyAt(t);
            hitch = HitchOf(b, h);
            var d = center - hitch;
            if (d.LengthSquared < 1e-6) d = -new Vector(Math.Cos(h), Math.Sin(h));
            d.Normalize();
            center = hitch + d * Bar;
            cart.Add((center, Math.Atan2(-d.Y, -d.X)));
        }
    }

    (Point Center, double Heading) CartAt(double t)
    {
        if (cart.Count == 0) return (default, 0);
        double f = Math.Clamp((t - start) * CartRate, 0, cart.Count - 1);
        int i = (int)f; int j = Math.Min(cart.Count - 1, i + 1); double k = f - i;
        var a = cart[i]; var b = cart[j];
        return (a.Center + (b.Center - a.Center) * k, a.Heading + Wrap(b.Heading - a.Heading) * k);
    }

    // ------------------------------------------------------------ Zeichnen

    public void Draw(DrawingContext dc, double t)
    {
        if (t < start - 0.01 || t > End) return;
        var (b, h) = BodyAt(t);
        var arm = ArmAt(t);
        double u = r / 100;
        double travel = (t - start) * 3;
        if (trailer)
        {
            var (c, ch) = CartAt(t);
            Art.TopTrailer(dc, c, ch, HitchOf(b, h), u);
        }
        Art.TopBase(dc, b, h, travel, u);
        // Arm in der senkrechten Ebene lösen: Schulter auf Höhe 55, Glieder 85/85 (Einheiten)
        double reach = arm.Reach * 100, z = arm.Height * 50;
        const double hs = 55, l1 = 85, l2 = 85;
        double dz = z - hs, dist = Math.Clamp(Math.Sqrt(reach * reach + dz * dz), Math.Abs(l1 - l2) + 1, l1 + l2 - 0.5);
        double a = Math.Atan2(dz, reach), c2 = Math.Acos(Math.Clamp((l1 * l1 + dist * dist - l2 * l2) / (2 * l1 * dist), -1, 1));
        double e = Math.Cos(a + c2) * l1, ez = hs + Math.Sin(a + c2) * l1;   // Ellbogen oben
        Art.TopArm(dc, b, h + arm.Yaw, e, ez, reach, z, JawAt(t), u);
    }

    internal void DrawPortrait(DrawingContext dc, Point b, double head, double yaw, double reach, double height, double cartAngle)
    {
        double u = r / 100;
        var hitch = HitchOf(b, head);
        var cartDir = new Vector(Math.Cos(head + cartAngle), Math.Sin(head + cartAngle));
        Art.TopTrailer(dc, hitch - cartDir * Bar, head + cartAngle, hitch, u);
        Art.TopBase(dc, b, head, 0, u);
        double rr = reach * 100, z = height * 50; const double hs = 55, l1 = 85, l2 = 85;
        double dz = z - hs, dist = Math.Clamp(Math.Sqrt(rr * rr + dz * dz), 1, l1 + l2 - 0.5);
        double a = Math.Atan2(dz, rr), c2 = Math.Acos(Math.Clamp((l1 * l1 + dist * dist - l2 * l2) / (2 * l1 * dist), -1, 1));
        Art.TopArm(dc, b, head + yaw, Math.Cos(a + c2) * l1, hs + Math.Sin(a + c2) * l1, rr, z, 0.6, u);
    }

    Rect ScreenOf(Point p) { foreach (var s in screens) if (s.Contains(p)) return s; return primary; }
    static double Wrap(double a) { while (a > Math.PI) a -= Math.Tau; while (a < -Math.PI) a += Math.Tau; return a; }
    static double Smooth(double v) { v = Math.Clamp(v, 0, 1); return v * v * (3 - 2 * v); }
    static double Drive5(double k) { k = Math.Clamp(k, 0, 1); return k * k * k * (k * (6 * k - 15) + 10); }
    static double Overshoot(double k) { k = Math.Clamp(k, 0, 1); double e = k * k * (3 - 2 * k); return e + 0.05 * Math.Sin(Math.PI * k) * Math.Max(0, k - 0.5) * 2; }
}
