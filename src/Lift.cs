using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DeskKeeper;

/// <summary>
/// Transporthubschrauber als Helfer : CH-47F Chinook mit Tandemrotoren und Mi-26, der größte Transporthubschrauber.
/// Beide fliegen ein, schweben über den Symbolen, nehmen sie mit dem Lastnetz am Haken auf (Chinook bis zu sechs, Mi-26 bis zu zehn)
/// und setzen sie an ihrem Platz ab. Kurze Wege im Schwebeflug seitlich, ohne zu drehen; im Schwebeflug wirbelt der Abwind Staub auf.
/// </summary>
sealed class LiftHelper : PathHelper
{
    public enum Type { Chinook, Mi26 }
    readonly Type type;
    readonly List<(double T, double Alt)> altitude = [];
    readonly List<(double From, double To, int Count)> load = [];
    readonly List<(double T, double Down)> hook = [];
    readonly Vector net;
    readonly Vector[] slots;

    readonly double pm;

    /// <param name="iconSize">Symbolgröße: daraus der gemeinsame Maßstab (ein Symbol = 6-m-Container)</param>
    public LiftHelper(Type type, Func<string, Point> pickOf, IReadOnlyDictionary<string, Point> home, bool fromCart, double start, double size, Rect primary, IEnumerable<string> order, double iconSize)
        : base(size, start)
    {
        this.type = type;
        pm = Scale.PerMeter(iconSize);
        int capacity = type == Type.Chinook ? 4 : 8;   // wie Container am Haken: Chinook bis vier, Mi-26 bis acht
        // Lastnetz hängt an langen Seilen hinter dem Rumpf (von oben sichtbar)
        net = type == Type.Chinook ? new Vector(-pm * 15, 0) : new Vector(-pm * 22, 0);
        slots = Enumerable.Range(0, capacity).Select(i => new Vector(-(i / 2) * iconSize * 1.05, (i % 2 == 0 ? -1 : 1) * iconSize * 0.55)).ToArray();
        var lz = new Point(primary.Left + r * 2.2, primary.Bottom - r * 1.8);
        var p = new Point(primary.Left - r * 5, primary.Top + r * 2); double h = 0, t = start;
        keys.Add((t, p, h)); altitude.Add((t, 1)); hook.Add((t, 0));
        double speed = r * (type == Type.Chinook ? 6.2 : 5.4);

        Point NetAt(Point body, double head) => body + Fly.Rot(net, head);
        void Fly_(Point target, double head)
        {   // Streckenflug in Reiseflughöhe, am Ziel absinken
            var body = target - Fly.Rot(net, head);
            if ((body - p).Length > r * 2.5)
            {
                altitude.Add((t, altitude[^1].Alt)); double up = t + 0.3; altitude.Add((up, 1));
                Move(ref t, ref p, ref h, target - Fly.Rot(net, Math.Atan2(target.Y - p.Y, target.X - p.X)), speed);
                altitude.Add((t - 0.35, 1)); altitude.Add((t, 0.55));
            }
            else Hop(NetAt(p, h), target);
        }
        void Hop(Point from, Point to)
        {   // seitlich versetzen im Schwebeflug, Kurs bleibt
            var d = to - from; if (d.Length < 1) return;
            t += Math.Max(0.22, d.Length / (speed * 0.7));
            p += d; keys.Add((t, p, h)); altitude.Add((t, 0.55));
        }
        void Lower(out double at)
        {   // Haken ab, Last greifen/lösen, Haken hoch
            hook.Add((t, 0)); t += 0.16; hook.Add((t, 1)); at = t; t += 0.14; hook.Add((t, 0));
            keys.Add((t, p, h)); altitude.Add((t, 0.55));
        }

        foreach (var batch in Batches(order, home, capacity, r * 5))
        {
            double loadedFrom;
            var ids = batch.ToList();
            if (fromCart)
            {   // Landezone: das ganze Netz auf einmal
                Fly_(lz, h);
                Lower(out loadedFrom);
                for (int k = 0; k < ids.Count; k++)
                {
                    var id = ids[k]; var slot = slots[k];
                    trips[id] = ([], home[id]);
                    Leg(id, home[id], loadedFrom, double.MaxValue, q => Attached(q, net + slot));
                }
            }
            else
            {
                // Aufnehmen in Nächster-Nachbar-Reihenfolge
                var left = ids.ToList(); ids.Clear();
                loadedFrom = double.NaN;
                while (left.Count > 0)
                {
                    var here = NetAt(p, h);
                    var id = left.OrderBy(i => (pickOf(i) - here).LengthSquared).First(); left.Remove(id); ids.Add(id);
                    var slot = slots[ids.Count - 1];
                    var pick = pickOf(id) - Fly.Rot(slot, h);
                    Fly_(pick, h);
                    Lower(out double grab);
                    if (double.IsNaN(loadedFrom)) loadedFrom = grab;
                    var source = pickOf(id); var s = slot;
                    trips[id] = ([], home[id]);
                    Leg(id, home[id], grab, grab + 0.3, q => source + (Attached(q, net + s) - source) * Fly.Smooth((q - grab) / 0.3));
                    Leg(id, home[id], grab + 0.3, double.MaxValue, q => Attached(q, net + s));
                }
            }
            // Abliefern, ebenfalls nach kürzestem Weg
            var open = ids.ToList();
            while (open.Count > 0)
            {
                var here = NetAt(p, h);
                var id = open.OrderBy(i => (home[i] - here).LengthSquared).First(); open.Remove(id);
                var slot = slots[ids.IndexOf(id)];
                Fly_(home[id] - Fly.Rot(slot, h), h);
                Lower(out double drop);
                var legs = trips[id].Legs;
                legs[^1] = (legs[^1].From, drop, legs[^1].At);
                Settle(id, home[id], drop, 0.3, Attached(drop, net + slot));
            }
            load.Add((loadedFrom, t, ids.Count));
        }
        // Abflug nach links oben
        altitude.Add((t, altitude[^1].Alt)); altitude.Add((t + 0.4, 1));
        Move(ref t, ref p, ref h, new Point(primary.Left - r * 6, primary.Top - r * 3), speed);
        altitude.Add((t, 1));
        End = t + 0.2;
    }

    static double Lerp(List<(double T, double V)> list, double t)
    {
        if (list.Count == 0) return 0;
        if (t <= list[0].T) return list[0].V;
        for (int i = 1; i < list.Count; i++)
            if (t <= list[i].T)
            {
                double k = Fly.Smooth((t - list[i - 1].T) / Math.Max(1e-6, list[i].T - list[i - 1].T));
                return list[i - 1].V + (list[i].V - list[i - 1].V) * k;
            }
        return list[^1].V;
    }

    static readonly Brush DustBrush = Art.Glow("#50A89A80"), NetLine = Fly.B("#C8B88A");
    static readonly Pen Sling = Fly.P("#30343A", 1), NetPen = Fly.P("#B0C8B88A", 0.8);

    public override void Draw(DrawingContext dc, double t)
    {
        if (t < begin - 0.5 || t > End + 0.5) return;
        var (p, h) = Pose(t);
        double alt = Lerp(altitude.Select(a => (a.T, a.Alt)).ToList(), t);
        double down = Lerp(hook, t);
        // Abwind: Staubring am Boden, je tiefer, desto deutlicher
        double rotor = pm * (type == Type.Chinook ? 9.14 : 16);
        if (alt < 0.9) Soft.Draw(dc, DustBrush, p, rotor * 1.25, rotor * 1.15, Math.Clamp((0.9 - alt) * 1.6, 0, 0.6));
        // Last am Haken: Seil zum Netz, Netz um die Symbole
        var hookAt = p;
        var netAt = p + Fly.Rot(net, h);
        var current = load.FirstOrDefault(l => t >= l.From && t <= l.To);
        if (current.Count > 0 || down > 0.02)
        {
            dc.DrawLine(Sling, hookAt, netAt);
            if (current.Count > 0)
            {
                dc.PushTransform(Fly.Frame(netAt, h, r));
                int rows = (current.Count + 1) / 2;
                double q = slots.Length > 1 ? -slots[2 % slots.Length].X / r : 0.42, w = slots[1].Y / r;
                var box = new Rect(-(rows - 1) * q - w * 0.7, -w * 1.9, (rows - 1) * q + w * 1.4, w * 3.8);
                dc.DrawRoundedRectangle(null, new Pen(NetPen.Brush, 0.025), box, 0.12, 0.12);
                for (double x = box.Left + 0.15; x < box.Right; x += 0.15) dc.DrawLine(new Pen(NetPen.Brush, 0.012), new Point(x, box.Top), new Point(x, box.Bottom));
                for (double y = box.Top + 0.15; y < box.Bottom; y += 0.15) dc.DrawLine(new Pen(NetPen.Brush, 0.012), new Point(box.Left, y), new Point(box.Right, y));
                dc.Pop();
            }
        }
        double spin = t * 9;
        if (type == Type.Chinook) DrawChinook(dc, p, h, pm, alt, spin);
        else DrawMi26(dc, p, h, pm, alt, spin);
    }

    // ---------------------------------------------------------------- CH-47F (Rumpf 15,9 m, zwei Dreiblattrotoren 18,29 m, Nabenabstand 11,9 m)
    static readonly Geometry ChinookBody = Fly.G("M7.95,0 C7.95,-0.9 7.7,-1.3 7.1,-1.45 L6.2,-1.5 L-7.4,-1.5 L-7.95,-1.2 L-7.95,1.2 L-7.4,1.5 L6.2,1.5 L7.1,1.45 C7.7,1.3 7.95,0.9 7.95,0 Z");
    static readonly Geometry ChinookSponsons = Fly.Both("M2.8,1.45 C2.8,1.9 2.4,2 2,2 L-3.6,2 C-4.2,2 -4.4,1.8 -4.4,1.45 Z");
    static readonly Geometry ChinookGlass = Fly.G("M7.85,-0.5 L7.6,-1.25 L7.05,-1.35 L7.15,-0.5 Z M7.85,0.5 L7.6,1.25 L7.05,1.35 L7.15,0.5 Z M7.9,-0.4 L7.9,0.4 L7.2,0.4 L7.2,-0.4 Z");
    static readonly Geometry ChinookSpine = Fly.G("M5,-0.35 L-3.6,-0.35 L-3.6,0.35 L5,0.35 Z");
    static readonly Geometry ChinookPylon = Fly.G("M-3.6,-0.8 C-4.4,-1.05 -7.2,-1.05 -7.9,-0.6 L-7.9,0.6 C-7.2,1.05 -4.4,1.05 -3.6,0.8 Z");
    static readonly Geometry ChinookSeams = Fly.G("M4,-1.5 L4,1.5 M1,-1.5 L1,1.5 M-2,-1.5 L-2,1.5 M-6.3,-1.5 L-6.3,1.5 M6.2,-1.5 L6.2,1.5");
    // Rumpfstöße, Laderampe, Seitentür vorn rechts, Wartungsklappen am Tunnel, Tankstutzen der Sponsons
    static readonly Geometry ChinookDetail = Fly.G("M-6.3,-1.3 L-7.8,-1.1 M-6.3,1.3 L-7.8,1.1 M5.6,1.5 L5.6,1.1 L4.4,1.1 L4.4,1.5 M3,-0.35 L3,0.35 M0,-0.35 L0,0.35 M-2.6,-0.35 L-2.6,0.35 " +
        "M1.6,-1.95 L1.6,-1.5 M1.6,1.95 L1.6,1.5 M-2.4,-1.95 L-2.4,-1.5 M-2.4,1.95 L-2.4,1.5 M7.1,-1.45 L7.1,1.45");
    static readonly Brush Screen = Fly.B("#6C7458"), HeliTip = Fly.B("#B8BCB0");
    static readonly Pen HeliAntenna = Fly.P("#1C1F21", 0.08);
    static readonly Brush Olive = Fly.B("#4C5437"), OliveDark = Fly.B("#3C4330"), OliveLight = Fly.B("#58613F"), HeliGlass = Fly.B("#1F272D"), HeliBlade = Fly.B("#202426"),
        HeliDisc = Fly.B("#1C202428"), EngineDark = Fly.B("#1C1F21");
    static readonly Pen HeliEdge = Fly.P("#2A2F22", 0.06), HeliSeam = Fly.P("#424A33", 0.04);

    /// <param name="k">Pixel je Meter</param>
    internal static void DrawChinook(DrawingContext dc, Point at, double heading, double k, double alt, double spin)
    {
        ShadowOf(dc, at, heading, k, alt, ChinookBody, ChinookSponsons);
        dc.PushTransform(Fly.Frame(at, heading, k));
        dc.DrawGeometry(OliveDark, HeliEdge, ChinookSponsons);
        dc.DrawGeometry(Olive, HeliEdge, ChinookBody);
        dc.DrawGeometry(null, HeliSeam, ChinookSeams);
        dc.DrawGeometry(OliveLight, HeliEdge, ChinookSpine);
        foreach (double s in new[] { -1.0, 1.0 })
        {   // T55-Triebwerke außen am hinteren Pylon
            dc.DrawRoundedRectangle(OliveLight, HeliEdge, new Rect(-7.2, 1.55 * s - 0.45, 3.8, 0.9), 0.45, 0.45);
            dc.DrawEllipse(EngineDark, null, new Point(-3.45, 1.55 * s), 0.12, 0.38);
            dc.DrawRectangle(EngineDark, null, new Rect(-7.3, 1.55 * s - 0.3, 0.2, 0.6));
        }
        dc.DrawGeometry(OliveLight, HeliEdge, ChinookPylon);
        dc.DrawGeometry(HeliGlass, HeliEdge, ChinookGlass);
        dc.DrawEllipse(OliveLight, HeliEdge, new Point(5.95, 0), 0.6, 0.6);   // vorderes Getriebe
        dc.DrawRoundedRectangle(OliveLight, HeliEdge, new Rect(4.6, -0.8, 2.5, 1.6), 0.6, 0.6);   // vorderer Rotorpylon über dem Cockpit
        dc.DrawGeometry(null, HeliSeam, ChinookDetail);
        foreach (double s in new[] { -1.0, 1.0 })
        {
            dc.DrawEllipse(Screen, HeliEdge, new Point(-3.35, 1.55 * s), 0.16, 0.42);   // Einlauf mit Partikelabscheider
            dc.DrawRectangle(Fly.B("#90D84040"), null, new Rect(5.2, 1.5 * s - 0.08, 0.5, 0.16));   // Rettungswinde rechts / Positionslicht
        }
        dc.DrawEllipse(EngineDark, null, new Point(-7.1, 0.45), 0.18, 0.18);   // APU-Abgas
        dc.DrawRectangle(EngineDark, null, new Rect(-5.6, -0.5, 1.4, 1.0));     // Lüftergitter der Getriebekühlung
        for (double x = -5.5; x < -4.2; x += 0.25) dc.DrawLine(HeliSeam, new Point(x, -0.45), new Point(x, 0.45));
        dc.DrawLine(HeliAntenna, new Point(2.2, -0.2), new Point(2.2, 0.2));      // Antennen auf dem Rücken
        dc.DrawLine(HeliAntenna, new Point(-1.2, -0.2), new Point(-1.2, 0.2));
        // Rotoren: vorn gegen, hinten mit dem Uhrzeigersinn, Blattspitzen überlappen in der Mitte
        Rotor(dc, new Point(5.95, 0), 9.14, 3, -spin, 0.81);
        Rotor(dc, new Point(-5.95, 0), 9.14, 3, spin + 0.5, 0.81);
        dc.Pop();
    }

    // ---------------------------------------------------------------- Ми-26 (Rumpf 33,7 m, Achtblattrotor 32 m, Fünfblatt-Heckrotor 7,6 m)
    static readonly Geometry HaloBody = Fly.G("M16.9,0 C16.9,-1.4 16.4,-2 15.4,-2.1 L12,-2.1 L-3,-2.1 L-5,-1.6 L-14,-0.9 L-16.6,-0.7 L-16.6,0.7 L-14,0.9 L-5,1.6 L-3,2.1 L12,2.1 L15.4,2.1 C16.4,2 16.9,1.4 16.9,0 Z");
    static readonly Geometry HaloSponsons = Fly.Both("M7,2 C7,2.6 6.6,2.75 6,2.75 L-1.2,2.75 C-1.8,2.75 -2.2,2.5 -2.2,2 Z");
    static readonly Geometry HaloEngines = Fly.Both("M9.6,0.2 C9.6,0.1 9.8,0.1 10,0.1 L10,2 C9.8,2.1 9.6,2 9.6,1.9 Z M9.8,0.2 L2.6,0.25 L1.8,0.8 L1.6,2.2 L2.6,2.3 L9.8,2.1 Z");
    static readonly Geometry HaloGearbox = Fly.G("M2,-1.1 C1,-1.4 -2.6,-1.4 -3.6,-0.9 L-3.6,0.9 C-2.6,1.4 1,1.4 2,1.1 Z");
    static readonly Geometry HaloStab = Fly.Both("M-12.4,0.8 L-12.6,2.6 L-13.9,2.6 L-13.9,0.8 Z");
    static readonly Geometry HaloGlass = Fly.G("M16.8,-0.6 L16.4,-1.7 L15.5,-1.9 L15.6,-0.6 Z M16.8,0.6 L16.4,1.7 L15.5,1.9 L15.6,0.6 Z M16.9,-0.5 L16.9,0.5 L15.7,0.5 L15.7,-0.5 Z");
    static readonly Geometry HaloCamo = Fly.G("M13,-2.1 C11,-1 9,-1.6 7,-2.1 Z M6,2.1 C4,1 1,1.4 -1,2.1 Z M-6,-1.5 C-8,-0.6 -10,-0.4 -12,-1 Z M12,0.6 C10,1.8 8,1.6 7,0.4 Z");
    static readonly Geometry HaloSeams = Fly.G("M12,-2.1 L12,2.1 M6,-2.1 L6,2.1 M-3,-2.1 L-3,2.1 M14.6,-2.1 L14.6,2.1");
    // Muscheltüren und Laderampe am Kabinenende, Stöße am Leitwerksträger, Höhenruder, Tankstutzen
    static readonly Geometry HaloDetail = Fly.G("M-3,-2.1 C-3.8,-1.2 -4.4,-0.6 -5,0 C-4.4,0.6 -3.8,1.2 -3,2.1 M-7,-1.4 L-7,1.4 M-10,-1.1 L-10,1.1 M-13.2,-2.4 L-13.2,-0.9 M-13.2,2.4 L-13.2,0.9 " +
        "M4,-2.75 L4,-2.1 M4,2.75 L4,2.1 M8,-2.1 L8,2.1 M10,-0.15 L1.8,-0.15 M10,0.15 L1.8,0.15");
    static readonly Geometry HaloExhaustR = Fly.G("M2.4,1.2 L1.2,1.9 L1.5,2.4 L2.7,1.8 Z"), HaloExhaustL = Fly.G("M2.4,-1.2 L1.2,-1.9 L1.5,-2.4 L2.7,-1.8 Z");
    static readonly Brush MiRed = Art.Glow("#C0FF3030"), MiGreen = Art.Glow("#C040FF80");
    static readonly Brush HaloTop = Fly.B("#6C7254"), HaloShade = Fly.B("#5E6449"), HaloPatch = Fly.B("#8A8160");

    /// <param name="k">Pixel je Meter</param>
    internal static void DrawMi26(DrawingContext dc, Point at, double heading, double k, double alt, double spin)
    {
        ShadowOf(dc, at, heading, k, alt, HaloBody, HaloSponsons, HaloStab);
        dc.PushTransform(Fly.Frame(at, heading, k));
        dc.DrawGeometry(HaloShade, HeliEdge, HaloSponsons);
        dc.DrawGeometry(HaloShade, HeliEdge, HaloStab);
        dc.DrawGeometry(HaloTop, HeliEdge, HaloBody);
        dc.DrawGeometry(HaloPatch, null, HaloCamo);
        dc.DrawGeometry(null, HeliSeam, HaloSeams);
        dc.DrawGeometry(HaloShade, HeliEdge, HaloEngines);   // D-136 mit Staubfiltern vorn, Abgas schräg nach außen
        foreach (double s in new[] { -1.0, 1.0 }) dc.DrawEllipse(EngineDark, null, new Point(1.9, 1.5 * s), 0.35, 0.55);
        dc.DrawGeometry(HaloTop, HeliEdge, HaloGearbox);
        dc.DrawGeometry(HeliGlass, HeliEdge, HaloGlass);
        dc.DrawGeometry(null, HeliSeam, HaloDetail);
        foreach (double s in new[] { -1.0, 1.0 })
        {
            dc.DrawEllipse(Screen, HeliEdge, new Point(9.9, 1.05 * s), 0.25, 0.85);   // Staubschutz vor dem Einlauf
            dc.DrawGeometry(EngineDark, null, s > 0 ? HaloExhaustR : HaloExhaustL);  // Abgasrohr schräg nach außen
        }
        dc.DrawRectangle(EngineDark, null, new Rect(-3.4, -0.6, 1.2, 1.2));       // Ölkühlereinlauf hinter dem Getriebe
        for (double y = -0.5; y < 0.6; y += 0.25) dc.DrawLine(HeliSeam, new Point(-3.4, y), new Point(-2.2, y));
        dc.DrawRoundedRectangle(null, HeliSeam, new Rect(13.2, -0.5, 1.0, 1.0), 0.2, 0.2);   // Notausstieg im Cockpitdach
        dc.DrawLine(HeliAntenna, new Point(-7, -0.2), new Point(-7, 0.2));
        dc.DrawLine(HeliAntenna, new Point(-10.5, -0.2), new Point(-10.5, 0.2));
        Soft.Draw(dc, MiRed, new Point(2.5, -2.75), 0.5, 0.5, 1);   // Positionslichter an den Fahrwerksverkleidungen
        Soft.Draw(dc, MiGreen, new Point(2.5, 2.75), 0.5, 0.5, 1);
        // Seitenleitwerk mit Heckrotor rechts: von oben die Kante der Rotorebene
        dc.DrawRoundedRectangle(HaloShade, HeliEdge, new Rect(-17.4, -0.35, 3.4, 0.7), 0.3, 0.3);
        dc.DrawRectangle(HeliDisc, null, new Rect(-19.6, 0.75, 7.6, 0.35));
        for (int b = 0; b < 5; b++)
        {
            double a = spin * 3 + b * Math.Tau / 5;
            dc.DrawLine(new Pen(HeliBlade, 0.22), new Point(-15.8, 0.92), new Point(-15.8 + 3.8 * Math.Cos(a), 0.92));
        }
        Rotor(dc, new Point(-0.6, 0), 16, 8, -spin, 0.8);
        dc.Pop();
    }

    static void Rotor(DrawingContext dc, Point hub, double radius, int blades, double angle, double chord)
    {
        dc.DrawEllipse(HeliDisc, null, hub, radius, radius);
        var pen = new Pen(HeliBlade, chord) { EndLineCap = PenLineCap.Flat };
        var ghost = new Pen(Fly.B("#30202426"), chord * 1.6);
        for (int b = 0; b < blades; b++)
        {
            double a = angle + b * Math.Tau / blades;
            var tip = new Vector(Math.Cos(a), Math.Sin(a)) * radius;
            var lag = new Vector(Math.Cos(a - 0.18), Math.Sin(a - 0.18)) * radius;
            dc.DrawLine(ghost, hub, hub + lag);   // Bewegungsunschärfe
            dc.DrawLine(pen, hub + tip * 0.06, hub + tip);
            dc.DrawLine(new Pen(HeliTip, chord) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat }, hub + tip * 0.96, hub + tip);   // Blattspitze
            dc.DrawLine(new Pen(Olive, chord * 0.7), hub, hub + tip * 0.08);   // Blattanschluss mit Schwenkdämpfer
        }
        dc.DrawEllipse(EngineDark, null, hub, radius * 0.045, radius * 0.045);
        dc.DrawEllipse(Olive, null, hub, radius * 0.02, radius * 0.02);
    }

    static void ShadowOf(DrawingContext dc, Point at, double heading, double k, double alt, params Geometry[] parts)
    {
        double lift = alt * k * 6;
        dc.PushTransform(new TranslateTransform(lift * 0.6, lift));
        dc.PushTransform(Fly.Frame(at, heading, k));
        foreach (var g in parts) dc.DrawGeometry(Fly.Shadow, null, g);
        dc.Pop(); dc.Pop();
    }
}
