using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DeskKeeper;

/// <summary>
/// Weitere Helfer neben DUM-E : Gabelstapler, Kranwagen und ein Schwarm Lieferdrohnen (eigene Gestaltung, ohne Marke).
/// Alle bringen die Symbole zurück – nach einer Szene aus ihrem Lager (Palette, Ladefläche, Landeplatz), beim Wiederherstellen von dort,
/// wo Windows sie hingelegt hat.
/// </summary>
static class Helpers
{
    public const string Dume = "dume", Forklift = "stapler", Crane = "kran", Drones = "drohnen", Chinook = "chinook", Mi26 = "mi26";
    public static readonly string[] All = [Dume, Forklift, Crane, Drones, Chinook, Mi26];
    /// <summary>Gewählter Helfer (Auswahl im Modul, gespeichert in auswahl.json).</summary>
    public static string Current = Dume;

    public static IHelper Create(string kind, IReadOnlyList<IconInfo> icons, Func<string, Point> pickOf, IReadOnlyDictionary<string, Point> home, bool fromCart,
        double start, double size, Rect[] screens, Rect primary, IEnumerable<string> order) => kind switch
    {
        Forklift => new ForkliftHelper(pickOf, home, fromCart, start, size, primary, order),
        Crane => new CraneHelper(pickOf, home, fromCart, start, size, primary, order),
        Drones => new DroneHelper(pickOf, home, fromCart, start, size, primary, order),
        Chinook => new LiftHelper(LiftHelper.Type.Chinook, pickOf, home, fromCart, start, size, primary, order),
        Mi26 => new LiftHelper(LiftHelper.Type.Mi26, pickOf, home, fromCart, start, size, primary, order),
        _ => new Robot(icons, pickOf, home, fromCart, start, size, screens, primary, order),
    };

    /// <summary>Vorschaubild eines Helfers (Auswahlkachel).</summary>
    public static void DrawPortrait(DrawingContext dc, string kind, Point at, double size)
    {
        switch (kind)
        {
            case Forklift: ForkliftHelper.DrawBody(dc, at, -0.35, size, 0.6, true, 0.4); break;
            case Crane: CraneHelper.DrawTruck(dc, at, -0.2, size, 1, -0.9, size * 3.2, 0.3); break;
            case Drones:
                DroneHelper.DrawDrone(dc, at + new Vector(-size * 0.9, -size * 0.25), 0.3, size * 0.8, 1, 0.3, true);
                DroneHelper.DrawDrone(dc, at + new Vector(size * 0.9, size * 0.35), -0.4, size * 0.8, 0.8, 0.9, false);
                break;
            case Chinook: LiftHelper.DrawChinook(dc, at, -0.5, size, 0.6, 0.4); break;
            case Mi26: LiftHelper.DrawMi26(dc, at, -0.5, size, 0.6, 0.4); break;
        }
    }
}

/// <summary>Gemeinsamer Ablauf: Fahrzeug-Stellungen als Stützpunkte, je Symbol Abschnitte (Zeit, Lage) bis zum Absetzen.</summary>
abstract class PathHelper : IHelper
{
    protected readonly double r, begin;
    protected readonly List<(double T, Point P, double H)> keys = [];
    protected readonly Dictionary<string, (List<(double From, double To, Func<double, Point> At)> Legs, Point Home)> trips = new(StringComparer.OrdinalIgnoreCase);
    public double End { get; protected set; }
    public int Carried => trips.Count;

    protected PathHelper(double size, double start) { r = size; begin = start; }

    public (bool Handled, Point? At) IconAt(string id, double t)
    {
        if (!trips.TryGetValue(id, out var trip) || trip.Legs.Count == 0 || t < trip.Legs[0].From) return (false, null);
        foreach (var (from, to, at) in trip.Legs) if (t <= to) return (true, at(Math.Max(t, from)));
        return (true, trip.Home);
    }

    protected void Leg(string id, Point home, double from, double to, Func<double, Point> at)
    {
        if (!trips.TryGetValue(id, out var trip)) trips[id] = trip = ([], home);
        trip.Legs.Add((from, to, at));
    }
    /// <summary>Symbol gleitet in settle Sekunden von dort, wo es abgesetzt wurde, an seinen Platz.</summary>
    protected void Settle(string id, Point home, double from, double settle, Point dropped) =>
        Leg(id, home, from, from + settle, q => dropped + (home - dropped) * Fly.Smooth((q - from) / settle));

    protected (Point P, double H) Pose(double t)
    {
        if (keys.Count == 0) return (default, 0);
        if (t <= keys[0].T) return (keys[0].P, keys[0].H);
        for (int i = 1; i < keys.Count; i++)
        {
            if (t > keys[i].T) continue;
            var (t0, p0, h0) = keys[i - 1]; var (t1, p1, h1) = keys[i];
            double k = Fly.Smooth((t - t0) / Math.Max(1e-6, t1 - t0));
            return (p0 + (p1 - p0) * k, Fly.Turn(h0, h1, k));
        }
        return (keys[^1].P, keys[^1].H);
    }

    /// <summary>Erst auf der Stelle zum Ziel drehen, dann weich anfahren und bremsen.</summary>
    protected void Move(ref double t, ref Point p, ref double h, Point to, double speed)
    {
        var d = to - p;
        if (d.Length < 1) return;
        double head = Math.Atan2(d.Y, d.X);
        double turn = Math.Abs(Wrap(head - h));
        if (turn > 0.05) { t += 0.1 + turn / 3.2; keys.Add((t, p, head)); }
        h = head;
        t += Math.Max(0.3, d.Length / speed);
        keys.Add((t, to, head));
        p = to;
    }
    protected void Wait(ref double t, Point p, double h, double dt) { t += dt; keys.Add((t, p, h)); }
    protected static double Wrap(double a) { while (a > Math.PI) a -= Math.Tau; while (a < -Math.PI) a += Math.Tau; return a; }
    protected Point Attached(double q, Vector offset) { var (p, h) = Pose(q); return p + Fly.Rot(offset, h); }

    /// <summary>Symbole zu Fuhren bündeln: das nächste offene und bis zu max−1 weitere mit Ziel in der Nähe.</summary>
    protected static List<List<string>> Batches(IEnumerable<string> order, IReadOnlyDictionary<string, Point> home, int max, double near)
    {
        var open = order.ToList(); var result = new List<List<string>>();
        while (open.Count > 0)
        {
            var first = open[0]; open.RemoveAt(0);
            var batch = new List<string> { first };
            foreach (var id in open.Where(id => (home[id] - home[first]).Length <= near).Take(max - 1).ToList()) { batch.Add(id); open.Remove(id); }
            result.Add(batch);
        }
        return result;
    }

    public abstract void Draw(DrawingContext dc, double t);
}

/// <summary>Gabelstapler von oben: holt bis zu vier Symbole auf die Palette, fährt sie zum Ziel und setzt sie dort ab.</summary>
sealed class ForkliftHelper : PathHelper
{
    readonly Vector[] slots;
    readonly List<(double From, double To)> loaded = [];
    readonly List<(double T, double Lift)> lift = [];
    readonly Point park;

    public ForkliftHelper(Func<string, Point> pickOf, IReadOnlyDictionary<string, Point> home, bool fromCart, double start, double size, Rect primary, IEnumerable<string> order)
        : base(size, start)
    {
        slots = [new(r * 1.0, -r * 0.3), new(r * 1.0, r * 0.3), new(r * 1.45, -r * 0.3), new(r * 1.45, r * 0.3)];
        park = new Point(primary.Left + r * 1.5, primary.Bottom - r * 1.4);
        var p = new Point(primary.Left - r * 2.2, park.Y); double h = 0, t = start;
        keys.Add((t, p, h)); lift.Add((t, 0));
        double speed = r * 3.6;
        Move(ref t, ref p, ref h, park, speed);
        foreach (var batch in Batches(order, home, 4, r * 3.2))
        {
            double loadFrom = t;
            for (int k = 0; k < batch.Count; k++)
            {
                var id = batch[k]; var slot = slots[k];
                if (fromCart)
                {   // Palette am Lager: Symbole kommen aus dem Stapel auf die Gabel
                    if (k == 0) Move(ref t, ref p, ref h, park, speed);
                    lift.Add((t, 0)); Wait(ref t, p, h, 0.24); lift.Add((t, 0.15));
                }
                else
                {
                    var pick = pickOf(id);
                    var d = pick - p; double head = d.LengthSquared > 1 ? Math.Atan2(d.Y, d.X) : h;
                    var stand = pick - Fly.Rot(slot, head);
                    Move(ref t, ref p, ref h, stand, speed);
                    lift.Add((t, 0)); Wait(ref t, p, h, 0.32); lift.Add((t, 0.6));
                }
                double carriedFrom = t;
                trips[id] = ([], home[id]);
                Leg(id, home[id], carriedFrom, double.MaxValue, q => Attached(q, slot));   // Ende wird beim Absetzen gesetzt
            }
            // zum Ziel: die Palettenmitte über die Mitte der Zielplätze
            var center = new Point(batch.Average(id => home[id].X), batch.Average(id => home[id].Y));
            var middle = new Vector(batch.Select((_, k) => slots[k].X).Average(), batch.Select((_, k) => slots[k].Y).Average());
            var way = center - p; double travel = way.LengthSquared > 1 ? Math.Atan2(way.Y, way.X) : h;
            var standAt = center - Fly.Rot(middle, travel);
            Move(ref t, ref p, ref h, standAt, speed);
            lift.Add((t, 0.6)); Wait(ref t, p, h, 0.28); lift.Add((t, 0));
            for (int k = 0; k < batch.Count; k++)
            {
                var id = batch[k]; var slot = slots[k];
                var legs = trips[id].Legs;
                var dropped = Attached(t, slot);
                legs[^1] = (legs[^1].From, t, legs[^1].At);
                Settle(id, home[id], t, 0.36, dropped);
                t += 0.3; keys.Add((t, p, h));
            }
            loaded.Add((loadFrom, t));
            // zurücksetzen, damit die Gabel frei ist
            var back = p - new Vector(Math.Cos(h), Math.Sin(h)) * r * 0.9;
            t += 0.45; keys.Add((t, back, h)); p = back;
        }
        Move(ref t, ref p, ref h, park, speed);
        Move(ref t, ref p, ref h, new Point(primary.Left - r * 2.2, park.Y), speed);
        End = t + 0.2;
    }

    double LiftAt(double t)
    {
        for (int i = 1; i < lift.Count; i++) if (t <= lift[i].T) return lift[i - 1].Lift + (lift[i].Lift - lift[i - 1].Lift) * Fly.Smooth((t - lift[i - 1].T) / Math.Max(1e-6, lift[i].T - lift[i - 1].T));
        return lift.Count > 0 ? lift[^1].Lift : 0;
    }

    public override void Draw(DrawingContext dc, double t)
    {
        if (t < begin || t > End) return;
        var (p, h) = Pose(t);
        bool carrying = loaded.Any(l => t >= l.From && t <= l.To);
        DrawBody(dc, p, h, r, LiftAt(t), carrying, t);
    }

    static readonly Brush Paint = Fly.B("#E8A317"), PaintDark = Fly.B("#C2860F"), Weight = Fly.B("#3A3D42"), Tyre = Fly.B("#1C1D20"), Seat = Fly.B("#2A2C30"),
        Steel = Fly.B("#8A9096"), Fork = Fly.B("#2E3135"), Wood = Fly.B("#90B07A48"), Lamp = Art.Glow("#E0FFA020");
    static readonly Pen Edge = Fly.P("#5A4410", 0.03), Guard = Fly.P("#202226", 0.045), WoodEdge = Fly.P("#B0603A20", 0.02);

    /// <summary>Stapler von oben, Länge etwa 1,4 size ohne Gabel; lift 0…1 hebt die Gabel (Schatten), carrying zeigt die Palette.</summary>
    internal static void DrawBody(DrawingContext dc, Point at, double heading, double size, double lift, bool carrying, double t)
    {
        dc.PushTransform(new TranslateTransform(size * 0.08, size * 0.12));
        dc.PushTransform(Fly.Frame(at, heading, size));
        dc.DrawRoundedRectangle(Fly.Shadow, null, new Rect(-0.8, -0.45, 1.35, 0.9), 0.12, 0.12);
        dc.Pop(); dc.Pop();
        dc.PushTransform(Fly.Frame(at, heading, size));
        // Gabel und Palette vorn (die Palette nur als Rahmen, damit die Symbole darauf sichtbar bleiben)
        double rise = 0.05 * lift;
        if (carrying) { dc.DrawRoundedRectangle(Wood, WoodEdge, new Rect(0.62 - rise, -0.5, 1.1, 1.0), 0.03, 0.03); for (int i = 0; i < 4; i++) dc.DrawLine(WoodEdge, new Point(0.66 + i * 0.34, -0.5), new Point(0.66 + i * 0.34, 0.5)); }
        foreach (double s in new[] { -1.0, 1.0 }) dc.DrawRectangle(Fork, null, new Rect(0.58, s * 0.22 - 0.045, 1.05, 0.09));
        dc.DrawRectangle(Steel, Guard, new Rect(0.5, -0.4, 0.1, 0.8));   // Hubmast
        foreach (double s in new[] { -1.0, 1.0 }) { dc.DrawRoundedRectangle(Tyre, null, new Rect(0.2, s * 0.42 - 0.08, 0.3, 0.16), 0.05, 0.05); dc.DrawRoundedRectangle(Tyre, null, new Rect(-0.62, s * 0.38 - 0.07, 0.24, 0.14), 0.05, 0.05); }
        dc.DrawRoundedRectangle(Paint, Edge, new Rect(-0.68, -0.38, 1.18, 0.76), 0.12, 0.12);
        dc.DrawRoundedRectangle(Weight, null, new Rect(-0.76, -0.36, 0.3, 0.72), 0.14, 0.14);   // Gegengewicht
        dc.DrawRoundedRectangle(Seat, null, new Rect(-0.32, -0.16, 0.3, 0.32), 0.06, 0.06);
        dc.DrawEllipse(null, Guard, new Point(0.1, 0), 0.12, 0.12);                             // Lenkrad
        dc.DrawRectangle(null, Guard, new Rect(-0.46, -0.34, 0.82, 0.68));                       // Fahrerschutzdach
        dc.DrawLine(Guard, new Point(-0.46, -0.34), new Point(0.36, 0.34)); dc.DrawLine(Guard, new Point(-0.46, 0.34), new Point(0.36, -0.34));
        dc.DrawRectangle(PaintDark, null, new Rect(-0.46, -0.04, 0.82, 0.08));
        Soft.Draw(dc, Lamp, new Point(-0.4, 0.28), (t % 0.8) < 0.4 ? 0.14 : 0.06, (t % 0.8) < 0.4 ? 0.14 : 0.06, 1);   // Rundumleuchte
        dc.Pop();
    }
}

/// <summary>
/// Kranwagen: fährt zu den Symbolen, stützt sich ab und hebt sie mit dem Teleskopausleger auf die Ladefläche; am Ziel wieder abstützen und
/// jedes Symbol an seinen Platz schwenken.
/// </summary>
sealed class CraneHelper : PathHelper
{
    readonly List<(double T, double Angle, double Length)> boom = [];
    readonly List<(double T, double Out)> legs = [];
    readonly List<(double T, double Hook)> hook = [];
    readonly Vector[] bed;
    readonly Vector turret;
    readonly double reach;

    public CraneHelper(Func<string, Point> pickOf, IReadOnlyDictionary<string, Point> home, bool fromCart, double start, double size, Rect primary, IEnumerable<string> order)
        : base(size, start)
    {
        turret = new Vector(-r * 0.62, 0);
        bed = [new(r * 0.05, -r * 0.24), new(r * 0.05, r * 0.24), new(r * 0.45, -r * 0.24), new(r * 0.45, r * 0.24)];
        reach = r * 4.2;
        var park = new Point(primary.Left + r * 2.2, primary.Bottom - r * 1.5);
        var p = new Point(primary.Left - r * 3, park.Y); double h = 0, t = start;
        keys.Add((t, p, h)); boom.Add((t, Math.PI, r * 1.1)); legs.Add((t, 0)); hook.Add((t, 0));
        double speed = r * 5;
        double angle = Math.PI, length = r * 1.1;
        void Stow(ref double tt) { Slew(ref tt, h + Math.PI, r * 1.1); legs.Add((tt, 1)); tt += 0.25; legs.Add((tt, 0)); }
        void Deploy(ref double tt) { legs.Add((tt, 0)); tt += 0.3; legs.Add((tt, 1)); keys.Add((tt, keys[^1].P, keys[^1].H)); }
        void Slew(ref double tt, double a, double l)
        {
            double turn = Math.Abs(Wrap(a - angle)), extend = Math.Abs(l - length);
            double dt = Math.Max(0.12, Math.Max(turn / 4.6, extend / (r * 11)));
            boom.Add((tt, angle, length)); tt += dt; angle = angle + Wrap(a - angle); length = l; boom.Add((tt, angle, length));
            keys.Add((tt, keys[^1].P, keys[^1].H)); hook.Add((tt, 0));
        }
        void Dip(ref double tt) { hook.Add((tt, 0)); tt += 0.11; hook.Add((tt, 1)); tt += 0.09; hook.Add((tt, 0)); keys.Add((tt, keys[^1].P, keys[^1].H)); }
        (double A, double L) Aim(Point target, Point body, double head)
        {
            var base0 = body + Fly.Rot(turret, head); var d = target - base0;
            return (Math.Atan2(d.Y, d.X), Math.Clamp(d.Length, r * 0.6, reach));
        }
        Point StandFor(Point target, Point from)
        {   // Drehkranz etwa 2,6 r vom Ziel, auf der Seite, von der der Wagen kommt
            var d = from - target; if (d.LengthSquared < 1) d = new Vector(-1, 0); d.Normalize();
            return target + d * (r * 2.6);
        }
        Move(ref t, ref p, ref h, park, speed);
        foreach (var batch in Batches(order, home, 4, r * 3.4))
        {
            // Aufnehmen in der kürzesten Reihenfolge (nächster Nachbar), Ladeplatz merkt sich jedes Symbol
            var slotOf = new Dictionary<string, int>();
            var picks = new List<string>();
            {
                var left = batch.ToList(); var from = p;
                while (left.Count > 0)
                {
                    var next = fromCart ? left[0] : left.OrderBy(i => (pickOf(i) - from).LengthSquared).First();
                    left.Remove(next); picks.Add(next); slotOf[next] = picks.Count - 1; if (!fromCart) from = pickOf(next);
                }
            }
            if (!fromCart)
            {
                bool deployed = false;
                for (int k = 0; k < picks.Count; k++)
                {
                    var id = picks[k]; var pick = pickOf(id);
                    if ((pick - (p + Fly.Rot(turret, h))).Length > reach * 0.92)
                    {
                        if (deployed) { Stow(ref t); deployed = false; }
                        var stand = StandFor(pick, p);
                        var travel = stand - p; double head = travel.LengthSquared > 1 ? Math.Atan2(travel.Y, travel.X) : h;
                        Move(ref t, ref p, ref h, stand - Fly.Rot(turret, head), speed);
                    }
                    if (!deployed) { Deploy(ref t); deployed = true; }
                    var (a, l) = Aim(pick, p, h); Slew(ref t, a, l);
                    Dip(ref t);
                    double picked = t - 0.09;
                    trips[id] = ([], home[id]);
                    Leg(id, home[id], picked, double.MaxValue, Hook);
                    var slotWorld = p + Fly.Rot(bed[k], h);
                    var (ba, bl) = Aim(slotWorld, p, h); Slew(ref t, ba, bl);
                    Dip(ref t);
                    var legsOf = trips[id].Legs; int kk = k;
                    legsOf[^1] = (legsOf[^1].From, t - 0.09, legsOf[^1].At);
                    Leg(id, home[id], t - 0.09, double.MaxValue, q => Attached(q, bed[kk]));
                }
                if (deployed) Stow(ref t);
            }
            // Abliefern: an jedem Ziel abstützen, vom Wagen nehmen und hinschwenken
            bool set = false;
            for (int k = 0; k < batch.Count; k++)
            {
                var id = batch[k]; var target = home[id];
                if ((target - (p + Fly.Rot(turret, h))).Length > reach * 0.92 || !set)
                {
                    if (set) Stow(ref t);
                    if ((target - (p + Fly.Rot(turret, h))).Length > reach * 0.92)
                    {
                        var stand = StandFor(target, p);
                        var travel = stand - p; double head = travel.LengthSquared > 1 ? Math.Atan2(travel.Y, travel.X) : h;
                        Move(ref t, ref p, ref h, stand - Fly.Rot(turret, head), speed);
                    }
                    Deploy(ref t); set = true;
                }
                var slotWorld = p + Fly.Rot(bed[slotOf[id]], h);
                var (ba, bl) = Aim(slotWorld, p, h); Slew(ref t, ba, bl);
                Dip(ref t);
                if (fromCart) { trips[id] = ([], home[id]); Leg(id, home[id], t - 0.09, double.MaxValue, Hook); }
                else { var legsOf = trips[id].Legs; legsOf[^1] = (legsOf[^1].From, t - 0.09, legsOf[^1].At); Leg(id, home[id], t - 0.09, double.MaxValue, Hook); }
                var (a, l) = Aim(target, p, h); Slew(ref t, a, l);
                Dip(ref t);
                var all = trips[id].Legs;
                var at = Hook(t - 0.09);
                all[^1] = (all[^1].From, t - 0.09, all[^1].At);
                Settle(id, home[id], t - 0.09, 0.2, at);
            }
            if (set) Stow(ref t);
        }
        Move(ref t, ref p, ref h, park, speed);
        Move(ref t, ref p, ref h, new Point(primary.Left - r * 3, park.Y), speed);
        End = t + 0.2;
    }

    static double Ease<T>(List<(double T, T A)> list, double t, Func<T, double> pick)
    {
        if (list.Count == 0) return 0;
        for (int i = 1; i < list.Count; i++)
            if (t <= list[i].T) { double a = pick(list[i - 1].A), b = pick(list[i].A); return a + (b - a) * Fly.Smooth((t - list[i - 1].T) / Math.Max(1e-6, list[i].T - list[i - 1].T)); }
        return pick(list[^1].A);
    }
    (double A, double L) BoomAt(double t)
    {
        for (int i = 1; i < boom.Count; i++)
            if (t <= boom[i].T)
            {
                double k = Fly.Smooth((t - boom[i - 1].T) / Math.Max(1e-6, boom[i].T - boom[i - 1].T));
                return (boom[i - 1].Angle + (boom[i].Angle - boom[i - 1].Angle) * k, boom[i - 1].Length + (boom[i].Length - boom[i - 1].Length) * k);
            }
        return (boom[^1].Angle, boom[^1].Length);
    }
    Point Hook(double t)
    {
        var (p, h) = Pose(t); var (a, l) = BoomAt(t);
        return p + Fly.Rot(turret, h) + new Vector(Math.Cos(a), Math.Sin(a)) * l;
    }

    public override void Draw(DrawingContext dc, double t)
    {
        if (t < begin || t > End) return;
        var (p, h) = Pose(t); var (a, l) = BoomAt(t);
        double outriggers = Ease(legs.Select(x => (x.T, x.Out)).ToList(), t, v => v), dip = Ease(hook.Select(x => (x.T, x.Hook)).ToList(), t, v => v);
        DrawTruck(dc, p, h, r, outriggers, a - h, l, dip);
    }

    static readonly Brush Chassis = Fly.B("#3B4046"), Cab = Fly.B("#C8302A"), CabRoof = Fly.B("#A62722"), Glass = Fly.B("#2C3846"), Yellow = Fly.B("#F2B41C"), YellowDark = Fly.B("#C9921A"),
        Tyre = Fly.B("#1C1D20"), Deck = Fly.B("#5A5F66"), Pad = Fly.B("#2A2C30"), Weight = Fly.B("#2E3135");
    static readonly Pen Edge = Fly.P("#202226", 0.03), Rope = Fly.P("#202226", 0.025), Stripe = Fly.P("#202226", 0.05);

    /// <summary>Kranwagen von oben (Länge etwa 2,3 size); boom: Winkel relativ zum Wagen, Länge in Pixeln; dip 0…1 senkt den Haken.</summary>
    internal static void DrawTruck(DrawingContext dc, Point at, double heading, double size, double outriggers, double boom, double length, double dip)
    {
        dc.PushTransform(new TranslateTransform(size * 0.1, size * 0.15));
        dc.PushTransform(Fly.Frame(at, heading, size));
        dc.DrawRoundedRectangle(Fly.Shadow, null, new Rect(-1.15, -0.42, 2.35, 0.84), 0.1, 0.1);
        dc.Pop(); dc.Pop();
        dc.PushTransform(Fly.Frame(at, heading, size));
        // Abstützungen: vier Träger, die seitlich ausfahren
        foreach (double x in new[] { -0.95, 0.55 })
            foreach (double s in new[] { -1.0, 1.0 })
            {
                double ext = 0.55 * outriggers;
                dc.DrawRectangle(YellowDark, Edge, new Rect(x - 0.06, s > 0 ? 0.3 : -0.3 - ext, 0.12, ext));
                if (ext > 0.02) dc.DrawRoundedRectangle(Pad, null, new Rect(x - 0.12, s * (0.3 + ext) - 0.1, 0.24, 0.2), 0.04, 0.04);
            }
        foreach (double x in new[] { -0.85, -0.45, 0.85 })
            foreach (double s in new[] { -1.0, 1.0 }) dc.DrawRoundedRectangle(Tyre, null, new Rect(x - 0.15, s * 0.42 - 0.08, 0.3, 0.16), 0.05, 0.05);
        dc.DrawRoundedRectangle(Chassis, Edge, new Rect(-1.15, -0.4, 2.0, 0.8), 0.06, 0.06);
        dc.DrawRectangle(Deck, Edge, new Rect(-0.3, -0.38, 1.0, 0.76));   // Ladefläche
        for (int i = 0; i < 4; i++) dc.DrawLine(Edge, new Point(-0.3 + i * 0.33, -0.38), new Point(-0.3 + i * 0.33, 0.38));
        dc.DrawRoundedRectangle(Cab, Edge, new Rect(0.78, -0.42, 0.45, 0.84), 0.1, 0.1);       // Fahrerhaus
        dc.DrawRoundedRectangle(CabRoof, null, new Rect(0.84, -0.34, 0.3, 0.68), 0.06, 0.06);
        dc.DrawRectangle(Glass, null, new Rect(1.14, -0.36, 0.06, 0.72));
        // Drehkranz mit Gegengewicht und Teleskopausleger
        dc.PushTransform(new TranslateTransform(-0.62, 0));
        dc.PushTransform(new RotateTransform(boom * 180 / Math.PI));
        dc.DrawRoundedRectangle(Weight, Edge, new Rect(-0.55, -0.3, 0.3, 0.6), 0.06, 0.06);
        dc.DrawEllipse(YellowDark, Edge, new Point(0, 0), 0.34, 0.34);
        double len = length / size;
        dc.DrawRoundedRectangle(Yellow, Edge, new Rect(0, -0.11, len, 0.22), 0.04, 0.04);
        for (double s = 0.6; s < len - 0.2; s += 0.9) dc.DrawLine(Stripe, new Point(s, -0.11), new Point(s, 0.11));   // Teleskopstufen
        dc.DrawRoundedRectangle(Glass, null, new Rect(-0.12, 0.14, 0.26, 0.16), 0.04, 0.04);   // Kranführerkabine
        // Haken: beim Absenken etwas kleiner, Seil sichtbar
        double hookSize = 0.12 - 0.03 * dip;
        dc.DrawLine(Rope, new Point(len, 0), new Point(len + 0.05, 0.05 * dip));
        dc.DrawEllipse(Weight, Edge, new Point(len + 0.05, 0.05 * dip), hookSize, hookSize);
        dc.Pop(); dc.Pop();
        dc.Pop();
    }
}

/// <summary>
/// Lieferdrohnen: mehrere Quadrokopter starten vom Landeplatz, holen je ein Symbol, fliegen es zu seinem Platz, senken sich ab und lassen es los.
/// </summary>
sealed class DroneHelper : PathHelper
{
    readonly List<List<(double T, Point P, double Alt)>> drones = [];
    readonly List<List<(double From, double To)>> carrying = [];
    readonly Point pad;

    public DroneHelper(Func<string, Point> pickOf, IReadOnlyDictionary<string, Point> home, bool fromCart, double start, double size, Rect primary, IEnumerable<string> order)
        : base(size, start)
    {
        pad = new Point(primary.Left + r * 1.6, primary.Bottom - r * 1.6);
        var ids = order.ToList();
        int count = Math.Clamp((ids.Count + 3) / 4, 2, 6);
        double speed = r * 5.2, end = start;
        for (int d = 0; d < count; d++)
        {
            var path = new List<(double, Point, double)>(); var busy = new List<(double, double)>();
            drones.Add(path); carrying.Add(busy);
            var p = pad + new Vector((d % 3 - 1) * r * 0.9, (d / 3) * r * 0.9);
            double t = start + d * 0.3;
            path.Add((t, p, 0)); t += 0.35; path.Add((t, p, 1));   // abheben
            for (int n = d; n < ids.Count; n += count)
            {
                var id = ids[n]; var from = fromCart ? pad : pickOf(id); var to = home[id];
                void Go(Point target, double alt, double extra = 0) { t += Math.Max(0.35, (target - p).Length / speed) + extra; path.Add((t, target, alt)); p = target; }
                Go(from, 1);
                t += 0.22; path.Add((t, p, 0.35));                    // absenken und greifen
                double grabbed = t;
                t += 0.18; path.Add((t, p, 1));
                Go(to, 1);
                t += 0.22; path.Add((t, p, 0.35));                    // absenken, loslassen
                busy.Add((grabbed, t));
                int index = d;
                trips[id] = ([], home[id]);
                Leg(id, home[id], grabbed, t, q => At(index, q) + new Vector(0, r * 0.05));
                t += 0.18; path.Add((t, p, 1));
            }
            t += Math.Max(0.35, (pad + new Vector((d % 3 - 1) * r * 0.9, (d / 3) * r * 0.9) - p).Length / speed);
            var land = pad + new Vector((d % 3 - 1) * r * 0.9, (d / 3) * r * 0.9);
            path.Add((t, land, 1)); t += 0.35; path.Add((t, land, 0));
            end = Math.Max(end, t);
        }
        End = end + 0.3;
    }

    (Point P, double Alt, double Heading) State(int d, double t)
    {
        var path = drones[d];
        if (t <= path[0].T) return (path[0].P, path[0].Alt, 0);
        for (int i = 1; i < path.Count; i++)
            if (t <= path[i].T)
            {
                double k = Fly.Smooth((t - path[i - 1].T) / Math.Max(1e-6, path[i].T - path[i - 1].T));
                var v = path[i].P - path[i - 1].P;
                return (path[i - 1].P + v * k, path[i - 1].Alt + (path[i].Alt - path[i - 1].Alt) * k, v.LengthSquared > 1 ? Math.Atan2(v.Y, v.X) : 0);
            }
        return (path[^1].P, path[^1].Alt, 0);
    }
    Point At(int d, double t) => State(d, t).P;

    static readonly Brush PadFill = Fly.B("#3A3F46"), PadMark = Fly.B("#E8EAEC"), Body = Fly.B("#F2F4F6"), BodyAccent = Fly.B("#2E7DD7"), Arm = Fly.B("#30343A"), Disc = Fly.B("#30202428"),
        Blade = Fly.B("#202428"), Led = Art.Glow("#E040FF80"), LedRed = Art.Glow("#E0FF3030");
    static readonly Pen Edge = Fly.P("#202428", 0.03), PadEdge = Fly.P("#E8EAEC", 0.05);

    public override void Draw(DrawingContext dc, double t)
    {
        if (t < begin - 0.5 || t > End + 0.5) return;
        // Landeplatz
        dc.DrawEllipse(PadFill, PadEdge, pad + new Vector(0, r * 0.4), r * 1.7, r * 1.5);
        dc.PushTransform(Fly.Frame(pad + new Vector(0, r * 0.4), 0, r));
        dc.DrawRectangle(PadMark, null, new Rect(-0.32, -0.4, 0.1, 0.8)); dc.DrawRectangle(PadMark, null, new Rect(0.22, -0.4, 0.1, 0.8)); dc.DrawRectangle(PadMark, null, new Rect(-0.22, -0.05, 0.44, 0.1));
        dc.Pop();
        for (int d = 0; d < drones.Count; d++)
        {
            var (p, alt, heading) = State(d, t);
            bool loaded = carrying[d].Any(c => t >= c.From && t <= c.To);
            DrawDrone(dc, p, heading, r * 0.62, alt, t * 7 + d, loaded);
        }
    }

    /// <summary>Quadrokopter von oben; alt 0…1 hebt ihn (größer, Schatten weiter weg), Rotoren drehen sich.</summary>
    internal static void DrawDrone(DrawingContext dc, Point at, double heading, double size, double alt, double spin, bool loaded)
    {
        double scale = size * (0.85 + 0.2 * alt);
        dc.PushTransform(new TranslateTransform(size * 0.35 * alt, size * 0.55 * alt));
        dc.PushTransform(Fly.Frame(at, heading, scale));
        dc.DrawEllipse(Fly.Shadow, null, new Point(0, 0), 0.9, 0.9);
        dc.Pop(); dc.Pop();
        dc.PushTransform(Fly.Frame(at, heading, scale));
        foreach (double ax in new[] { -1.0, 1.0 })
            foreach (double ay in new[] { -1.0, 1.0 })
            {
                var rotor = new Point(ax * 0.55, ay * 0.55);
                var arm = new StreamGeometry();
                using (var g = arm.Open()) { g.BeginFigure(new Point(ax * 0.08, ay * 0.12), true, true); g.LineTo(new Point(rotor.X + ay * 0.04, rotor.Y - ax * 0.04), true, false); g.LineTo(new Point(rotor.X - ay * 0.04, rotor.Y + ax * 0.04), true, false); g.LineTo(new Point(-ax * 0.04 + ax * 0.08, ay * 0.04), true, false); }
                arm.Freeze();
                dc.DrawGeometry(Arm, null, arm);
                dc.DrawEllipse(Disc, null, rotor, 0.42, 0.42);
                double a = spin * (ax * ay > 0 ? 1 : -1);
                dc.DrawLine(new Pen(Blade, 0.07), rotor + new Vector(Math.Cos(a), Math.Sin(a)) * 0.38, rotor - new Vector(Math.Cos(a), Math.Sin(a)) * 0.38);
                dc.DrawEllipse(Arm, null, rotor, 0.07, 0.07);
            }
        dc.DrawRoundedRectangle(Body, Edge, new Rect(-0.3, -0.22, 0.6, 0.44), 0.14, 0.14);
        dc.DrawRoundedRectangle(BodyAccent, null, new Rect(-0.3, -0.05, 0.6, 0.1), 0.04, 0.04);
        if (loaded) dc.DrawRectangle(null, Edge, new Rect(-0.24, -0.3, 0.48, 0.6));   // Greifer geschlossen
        Soft.Draw(dc, Led, new Point(0.3, 0.14), 0.08, 0.08, 1);
        Soft.Draw(dc, LedRed, new Point(0.3, -0.14), 0.08, 0.08, 1);
        dc.Pop();
    }
}
