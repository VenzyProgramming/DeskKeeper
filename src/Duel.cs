using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DeskKeeper;

/// <summary>
/// Duell F-35A gegen Su-57 (19.11.2026: „Duelle zwischen SU und F“, „soll episch aussehen“): beide jagen sich über die
/// Symbolgruppen, die Führung wechselt nach jeder Gruppe (der Hintere zieht seitlich vorbei, der Vordere fällt zurück – rollende Schere).
/// Wer hinten liegt, feuert nach vorn: Luft-Luft-Raketen aus der Nase heraus auf den Vorderen, dazu Kanonenstöße. Der Vordere wirft über
/// der Gruppe Täuschkörperfächer, die Raketen gehen auf die Täuschkörper und schlagen in die Symbole darunter ein.
/// Lange Rauchspuren, Wirbelschleppen an den Flügelspitzen, Nachbrenner.
/// Finale (19.11.2026: „der Su-57 sollte am Ende den F-35 jagen / zerstören“): die Su-57 setzt sich hinter die F-35, die erste Rakete
/// geht auf die Täuschkörper, die zweite trifft; die F-35 explodiert, stürzt trudelnd mit Feuer und Rauch ab und schlägt auf, die Su-57 zieht davon.
/// Nachbesserung 19.11.2026 („schießen blind irgendwo hin oder teleportieren sich random“): Querversatz an einer geglätteten
/// Bahnrichtung (vorher sprang er in engen Kurven bis 10-fache Fluggeschwindigkeit), Raketen starten in Flugrichtung des Schützen
/// (vorher bis 171° daneben) und treffen nur Symbole unter dem Gegner (vorher bis 40 Symbolgrößen entfernt).
/// </summary>
sealed class DuelRaid : IAttack
{
    internal record Shot(bool ByF35, double Fire, double Impact, Point To);
    internal record Burst(bool ByF35, double Start);
    internal record Fan(bool OfF35, double Start);

    readonly List<Point> line = [];
    readonly List<double> cum = [];
    internal readonly List<Shot> Shots = [];
    readonly List<Burst> bursts = [];
    readonly List<Fan> fans = [];
    internal IReadOnlyList<Burst> Bursts => bursts;
    internal IReadOnlyList<Fan> Fans => fans;
    readonly List<double> passes = [];   // Zeitpunkte, zu denen der Vordere über einer Gruppe ist
    readonly List<double> leads = [];    // +1: F-35A vorn, −1: Su-57 vorn
    readonly double speed, spanF, spanS, uF, uS, along, side, t0, length, breakAt, killAt = double.MaxValue / 4, crashAt = double.MaxValue / 4;
    readonly List<(Point P, double T, double Size)> airBursts = [];
    Point killPoint; Vector killVelocity; double killHeading;
    internal double KillAt => killAt;
    internal double CrashAt => crashAt;
    internal IReadOnlyList<(Point P, double T, double Size)> AirBursts => airBursts;
    const double Flight = 1.25, TrailLife = 4.0, Smooth = 150;

    public IReadOnlyList<(Point P, double T)> Blasts { get; }
    public IReadOnlyList<double>? BlastSizes { get; }
    public double LastImpact { get; }
    public double End { get; }
    public bool Explodes => true;
    public double Reach { get; }

    public DuelRaid(Rect area, IReadOnlyList<Point> targets, double iconSize)
    {
        spanF = Scale.PerMeter(iconSize) * 10.7; spanS = Scale.PerMeter(iconSize) * 14.1;
        uF = spanF / 68; uS = spanS / 70;
        speed = Math.Max(area.Width / 7.5, spanS * 2.4);
        double turnRate = 70 * Math.PI / 180, radius = speed / turnRate;
        along = side = Math.Min(spanS * 1.1, radius * 0.45);   // gleich groß: der Abstand der Jets bleibt beim Überholen konstant
        Reach = iconSize * 0.5;
        breakAt = double.MaxValue / 4;

        // Gruppen: bis zu sechs benachbarte Symbole, Reihenfolge als Kette vom linken Rand
        var open = targets.ToList();
        var groups = new List<List<Point>>();
        var inner = area; inner.Inflate(-spanS * 2.5, -spanS * 2.5);
        if (inner.IsEmpty) inner = area;
        var mid = new Point(inner.X + inner.Width / 2, inner.Y + inner.Height / 2);
        var start = new Point(area.Left - spanS * 6, open.Count > 0 ? open.OrderBy(p => p.X).First().Y : mid.Y);
        Point prev = start;
        while (open.Count > 0)
        {
            var here = prev;
            var seed = open.OrderBy(p => (p - here).LengthSquared).First();
            var g = open.OrderBy(p => (p - seed).LengthSquared).Take(6).Where(p => (p - seed).Length < iconSize * 4.8).ToList();
            open.RemoveAll(g.Contains); groups.Add(g);
            prev = new Point(g.Average(p => p.X), g.Average(p => p.Y));
        }

        // Flugbahn als Simulation (19.11.2026: „buggt herum, zu schnell“): feste Geschwindigkeit, Drehrate höchstens 70°/s, steuert
        // die Gruppen nacheinander an. Liegt eine Gruppe zu nah, ergibt sich eine saubere Schleife statt einer Haarnadel.
        var index = new List<int>();
        const double dt = 1 / 120.0;
        double capture = Math.Max(iconSize * 2.2, radius * 0.9);
        var pos = start;
        double hd = groups.Count > 0 ? Math.Atan2(groups[0].Average(p => p.Y) - start.Y, groups[0].Average(p => p.X) - start.X) : 0;
        double rate = 0;
        const double rollAccel = 45 * Math.PI / 180, gain = 1.1;   // Drehrate baut sich mit höchstens 45°/s² auf (19.11.2026: „keine ruckartigen Manöver“)
        void Turn(double delta)
        {
            double want = Math.Clamp(delta * gain, -turnRate, turnRate);
            // rechtzeitig ausrollen: höchstens die Rate, die sich bis zum Ziel noch auf null abbauen lässt
            double stop = Math.Sqrt(2 * rollAccel * Math.Abs(delta));
            want = Math.Clamp(want, -stop, stop);
            rate += Math.Clamp(want - rate, -rollAccel * dt, rollAccel * dt);
            hd += rate * dt;
        }
        void Step()
        {
            pos += new Vector(Math.Cos(hd), Math.Sin(hd)) * speed * dt;
            cum.Add(line.Count == 0 ? 0 : cum[^1] + (pos - line[^1]).Length); line.Add(pos);
        }
        line.Add(pos); cum.Add(0);
        const double minGap = 3.5;
        int lastPass = 0;
        void Steer(Point aim)
        {
            double want = Math.Atan2(aim.Y - pos.Y, aim.X - pos.X);
            Turn(Math.IEEERemainder(want - hd, 2 * Math.PI));
            Step();
        }
        for (int gi = 0; gi < groups.Count; gi++)
        {
            var g = groups[gi];
            var c = new Point(g.Average(p => p.X), g.Average(p => p.Y));
            if (gi > 0)
            {   // nach jeder Gruppe erst hinaus in den freien Raum, damit zwischen zwei Überflügen Zeit für den Führungswechsel bleibt
                var away = mid + (mid - c) * 0.35;
                for (double run = 0; run < speed * minGap * 0.3; run += speed * dt) Steer(away);
            }
            double turned = 0, best = double.MaxValue; int bestIndex = line.Count - 1;
            for (int guard = 0; guard < 120 * 30; guard++)
            {
                double dist = (c - pos).Length;
                if (dist < best && (index.Count == 0 || (line.Count - 1 - lastPass) * dt >= minGap)) { best = dist; bestIndex = line.Count - 1; }
                bool rested = index.Count == 0 || (line.Count - 1 - lastPass) * dt >= minGap;   // genug Zeit seit dem letzten Überflug
                if (dist < capture && rested) break;
                if (turned > 2 * Math.PI && dist > best + speed * 0.3) break;   // kreist sonst um eine Gruppe im Wendekreis
                double want = Math.Atan2(c.Y - pos.Y, c.X - pos.X);
                double before = hd;
                Turn(Math.IEEERemainder(want - hd, 2 * Math.PI));
                turned += Math.Abs(hd - before);
                Step();
            }
            index.Add(bestIndex); lastPass = bestIndex;
        }
        int finaleIndex = line.Count - 1;
        for (double run = 0; run < speed * 7.5; run += speed * dt) Steer(mid);   // Finale: weiter Bogen über der Bildschirmmitte
        double diag = Math.Sqrt(area.Width * area.Width + area.Height * area.Height);
        for (double run = 0; run < diag * 1.6 + spanS * 10; run += speed * dt) { Turn(0); Step(); }   // ausrollen, dann geradeaus hinaus
        length = cum[^1];

        // Überflugzeiten: der Vordere (Weg + along) ist über der Gruppe; erste Gruppe frühestens nach 1,6 s
        double firstD = groups.Count > 0 ? cum[index[0]] : 0;
        t0 = Math.Max(0.3, 1.8 - (firstD - along) / speed);   // erste Gruppe frühestens nach 1,8 s, davor kommen die Jets von außen herein
        for (int i = 0; i < groups.Count; i++)
        {
            passes.Add(t0 + (cum[index[i]] - along) / speed);
            leads.Add(i % 2 == 0 ? 1 : -1);
        }

        var blasts = new List<(Point, double)>();
        for (int i = 0; i < groups.Count; i++)
        {
            double tp = passes[i];
            bool byF35 = leads[i] < 0;   // der Hintere schießt
            fans.Add(new Fan(!byF35, tp - 0.4));
            if (i % 2 == 1) bursts.Add(new Burst(byF35, tp - 1.05));
            var g = groups[i].OrderBy(p => (p - PlaneAt(!byF35, tp - 0.4)).Length).ToList();
            for (int k = 0; k < g.Count; k++)
            {
                double impact = tp + 0.1 + k * 0.18, best = impact - Flight, bestAngle = double.MaxValue;
                for (double fire = impact - 1.6; fire <= impact - 0.8; fire += 0.05)
                {
                    double h = Heading(byF35, fire);
                    var to = PlaneAt(!byF35, fire) - PlaneAt(byF35, fire);
                    double off = Math.Abs(Math.IEEERemainder(Math.Atan2(to.Y, to.X) - h, 2 * Math.PI));
                    if (off < bestAngle - 0.02) { bestAngle = off; best = fire; }
                }
                Shots.Add(new Shot(byF35, best, impact, g[k]));
                blasts.Add((g[k], impact));
            }
        }
        Blasts = blasts; BlastSizes = blasts.Select(_ => 1.05).ToList();
        LastImpact = blasts.Count > 0 ? blasts.Max(b => b.Item2) : 2;

        // Finale: F-35A vorn, Su-57 dahinter (Führung +1); erst Täuschkörper, dann der Treffer
        double finalPass = passes.Count > 0 ? passes[^1] : 1.8;
        passes.Add(finalPass + 3.5); leads.Add(1);
        double Aim(double from, double to)
        {   // Zeitpunkt, zu dem die F-35A der Su-57 am genauesten vor der Nase ist
            double best = from, bestAngle = double.MaxValue;
            for (double fire = from; fire <= to; fire += 0.05)
            {
                var v = PlaneAt(true, fire) - PlaneAt(false, fire);
                double off = Math.Abs(Math.IEEERemainder(Math.Atan2(v.Y, v.X) - Heading(false, fire), 2 * Math.PI));
                if (off < bestAngle - 0.02) { bestAngle = off; best = fire; }
            }
            return best;
        }
        double fire1 = Aim(finalPass + 3.6, finalPass + 4.6), hit1 = fire1 + 1.25;
        fans.Add(new Fan(true, fire1 + 0.45));
        {
            double hh = Heading(true, hit1 - 0.5);
            var back = -new Vector(Math.Cos(hh), Math.Sin(hh)); var across = new Vector(-back.Y, back.X);
            var decoy = PlaneAt(true, hit1 - 0.5) + back * spanF * 1.6 + across * spanF * 0.9;
            Shots.Add(new Shot(false, fire1, hit1, decoy));
            airBursts.Add((decoy, hit1, 0.6));
        }
        bursts.Add(new Burst(false, fire1 + 0.25));
        double fire2 = Aim(fire1 + 0.7, fire1 + 1.5);
        killAt = fire2 + 1.25;
        fans.Add(new Fan(true, killAt - 0.7));
        killPoint = PlaneAt(true, killAt);
        killVelocity = (PlaneAt(true, killAt) - PlaneAt(true, killAt - 0.05)) / 0.05;
        killHeading = Heading(true, killAt);
        Shots.Add(new Shot(false, fire2, killAt, killPoint));
        airBursts.Add((killPoint, killAt, 1.4));
        crashAt = killAt + 2.6;
        airBursts.Add((WreckAt(crashAt), crashAt, 1.7));
        breakAt = killAt + 0.9;   // die Su-57 zieht nach dem Treffer davon
        LastImpact = Math.Max(LastImpact, crashAt + 1.0);   // DUM-E wartet, bis die F-35A aufgeschlagen ist
        if (Environment.GetEnvironmentVariable("DK_DUEL_DEBUG") is "1") Console.WriteLine($"gruppen {groups.Count}, ueberfluege {string.Join(" ", passes.Select(p => p.ToString("0.0")))}");
        End = Math.Max(Math.Max(LastImpact + 0.4, breakAt + 3.2), crashAt + 2.6);
    }

    Point OnLine(double d)
    {
        if (d <= 0) { var v0 = line[1] - line[0]; v0.Normalize(); return line[0] + v0 * d; }
        if (d >= length) { var v1 = line[^1] - line[^2]; v1.Normalize(); return line[^1] + v1 * (d - length); }
        int i = cum.BinarySearch(d); if (i < 0) i = ~i - 1;
        i = Math.Clamp(i, 0, line.Count - 2);
        double seg = cum[i + 1] - cum[i], k = seg > 0 ? (d - cum[i]) / seg : 0;
        return line[i] + (line[i + 1] - line[i]) * k;
    }

    internal double BreakAt => breakAt;

    /// <summary>Wrack der F-35A nach dem Treffer: gleitet mit abnehmender Geschwindigkeit weiter, dreht sich immer schneller,
    /// sinkt bis zum Aufschlag (Schatten wandert unter das Wrack).</summary>
    internal Point WreckAt(double t)
    {
        double tau = Math.Clamp(t - killAt, 0, crashAt - killAt);
        return killPoint + killVelocity * (1.1 * (1 - Math.Exp(-tau / 1.1)));
    }

    double WreckHeading(double t) { double tau = Math.Max(0, t - killAt); return killHeading + 1.8 * tau * tau; }

    /// <summary>Führung der F-35A: +1 vorn, −1 hinten. Wechsel in den ersten 80 % zwischen zwei Überflügen (weich),
    /// damit beim Schießen und Täuschen die Rollen klar sind.</summary>
    double Lead(double t)
    {
        if (passes.Count == 0) return 1;
        if (t <= passes[0]) return leads[0];
        for (int i = 0; i < passes.Count - 1; i++)
        {
            if (t > passes[i + 1]) continue;
            double gap = passes[i + 1] - passes[i];
            double k = Math.Clamp((t - passes[i] - 0.3) / (gap * 0.75), 0, 1);
            return leads[i] + (leads[i + 1] - leads[i]) * Fly.Smooth(k);
        }
        return leads[^1];
    }

    bool F35Ahead(double t) => Lead(t) > 0;

    /// <summary>Lage eines Jets: Mitte des Paares auf dem Weg, Längsversatz nach Führung, Querversatz beim Überholen (an der
    /// über ±150 px geglätteten Bahnrichtung). Nach dem Auseinanderbrechen zieht jeder zu seiner Seite weg.</summary>
    internal Point PlaneAt(bool f35, double t)
    {
        double sign = f35 ? 1 : -1, lead = Lead(t);
        double brk = Fly.Smooth((t - breakAt) / 1.6);
        double theta = (1 - lead) / 2 * Math.PI;   // 0: F-35A vorn … π: Su-57 vorn; weich, ohne Ruck an den Enden
        double d = (t - t0) * speed + sign * along * Math.Cos(theta) + brk * sign * speed * 0.3 * Math.Max(0, t - breakAt);
        var p = OnLine(d);
        var (n, _) = Across(d);
        // Überholen auf einem Halbkreis um den anderen: Längs- und Querversatz zusammen immer gleich lang, die Jets überlappen nie
        // (19.11.2026: „ein Jet ist auf einmal unter dem anderen“).
        double lateral = sign * side * Math.Sin(theta) + sign * brk * brk * speed * 0.9 * Math.Max(0, t - breakAt);
        return p + n * lateral;
    }

    /// <summary>Querrichtung, gemittelt über ±0,6 s Flugweg, und wie gerade die Bahn dort ist (1 gerade … 0 Haarnadel).
    /// Ohne Mittelung kippte die Querrichtung in engen Kurven und der Jet sprang seitlich.</summary>
    (Vector N, double Straight) Across(double d)
    {
        var sum = new Vector(); const int n = 12; double reach = speed * 0.7;
        for (int j = -n; j <= n; j++)
        {
            double x = d + reach * j / n;
            var v = OnLine(x + Smooth * 0.5) - OnLine(x - Smooth * 0.5);
            if (v.Length > 0) { v.Normalize(); sum += v; }
        }
        double straight = sum.Length / (2 * n + 1);
        if (sum.Length < 1e-6) return (new Vector(0, 0), 0);
        sum.Normalize();
        return (new Vector(-sum.Y, sum.X), Fly.Smooth(Math.Clamp((straight - 0.6) / 0.35, 0, 1)));
    }

    double Heading(bool f35, double t)
    {
        var a = PlaneAt(f35, t - 0.04); var b = PlaneAt(f35, t + 0.04);
        return Math.Atan2(b.Y - a.Y, b.X - a.X);
    }

    /// <summary>Rakete als kubische Kurve: aus der Nase in Flugrichtung des Schützen, auf den Gegner zu, dann auf die Täuschkörper
    /// über dem Symbol.</summary>
    Point MissileAt(Shot s, double k)
    {
        double h = Heading(s.ByF35, s.Fire);
        var dir = new Vector(Math.Cos(h), Math.Sin(h));
        var p0 = PlaneAt(s.ByF35, s.Fire) + dir * (s.ByF35 ? spanF : spanS) * 0.55;
        var p1 = p0 + dir * (speed * 0.9 + spanS * 1.5);
        var target = PlaneAt(!s.ByF35, s.Impact - 0.3);
        var p2 = target + (s.To - target) * 0.45;   // auf den Gegner zu, dann zu den Täuschkörpern über dem Symbol
        var p3 = s.To;
        double m = 1 - k;
        return new Point(m * m * m * p0.X + 3 * m * m * k * p1.X + 3 * m * k * k * p2.X + k * k * k * p3.X,
                         m * m * m * p0.Y + 3 * m * m * k * p1.Y + 3 * m * k * k * p2.Y + k * k * k * p3.Y);
    }

    /// <summary>Unruhe der Fluglage je Bild (60 Bilder/s): größte Drehrate (Grad/s), Anzahl Wechsel der Drehrichtung je Sekunde
    /// (Zittern/Pendeln), größte Beschleunigung (Vielfaches der Fluggeschwindigkeit je Sekunde) und Fluggeschwindigkeit in Bildschirmbreiten je s.</summary>
    internal (double TurnRate, double Wobble, double Accel, double ScreensPerSecond) Unrest(double width)
    {
        double maxTurn = 0, maxAcc = 0; int flips = 0; double seconds = Math.Max(0.1, breakAt - 0.5);
        foreach (bool f in new[] { true, false })
        {
            double lastRate = 0; Vector lastV = new();
            for (double t = 0.5; t < breakAt; t += 1 / 60.0)
            {
                double rate = Math.IEEERemainder(Heading(f, t + 1 / 60.0) - Heading(f, t), 2 * Math.PI) * 60 * 180 / Math.PI;
                maxTurn = Math.Max(maxTurn, Math.Abs(rate));
                if (Math.Abs(rate) > 400 && Environment.GetEnvironmentVariable("DK_DUEL_DEBUG") is "1") { double d0 = (t - t0) * speed; Console.WriteLine($"dreh t={t:0.000} {(f ? "F35" : "Su57")} {rate:0} grad/s lead={Lead(t):0.00} d={d0:0} straight={Across(d0).Straight:0.00} pos={PlaneAt(f, t).X:0},{PlaneAt(f, t).Y:0}"); }
                if (Math.Abs(rate) > 8 && Math.Abs(lastRate) > 8 && Math.Sign(rate) != Math.Sign(lastRate)) flips++;
                if (Math.Abs(rate) > 8) lastRate = rate;
                var v = (PlaneAt(f, t + 1 / 60.0) - PlaneAt(f, t)) * 60;
                if (t > 0.5) maxAcc = Math.Max(maxAcc, (v - lastV).Length * 60 / speed);
                lastV = v;
            }
        }
        return (maxTurn, flips / (2 * seconds), maxAcc, speed / width);
    }

    /// <summary>Ruck der Fluglage: größte Drehbeschleunigung (Grad/s², über 0,1 s gemittelt) je Jet und der Bahn allein, dazu der Zeitpunkt.</summary>
    internal (double Jet, double JetAt, double Path) Jerk()
    {
        double jet = 0, at = 0, path = 0;
        const double h = 0.05;
        double Rate(Func<double, double> heading, double t) => Math.IEEERemainder(heading(t + h) - heading(t - h), 2 * Math.PI) / (2 * h) * 180 / Math.PI;
        double PathHeading(double t) { double d = (t - t0) * speed; var v = OnLine(d + 4) - OnLine(d - 4); return Math.Atan2(v.Y, v.X); }
        for (double t = 0.6; t < breakAt; t += 1 / 60.0)
        {
            foreach (bool f in new[] { true, false })
            {
                double a = Math.Abs(Rate(x => Heading(f, x), t + 0.05) - Rate(x => Heading(f, x), t - 0.05)) / 0.1;
                if (a > jet) { jet = a; at = t; }
            }
            path = Math.Max(path, Math.Abs(Rate(PathHeading, t + 0.05) - Rate(PathHeading, t - 0.05)) / 0.1);
        }
        return (jet, at, path);
    }

    /// <summary>Kleinster Abstand der beiden Jets vor dem Auseinanderbrechen, in Spannweiten der Su-57.</summary>
    internal double ClosestPass()
    {
        double min = double.MaxValue;
        for (double t = 0.5; t < breakAt; t += 1 / 60.0) min = Math.Min(min, (PlaneAt(true, t) - PlaneAt(false, t)).Length / spanS);
        return min;
    }

    /// <summary>Messwerte für die Prüfung: größte Geschwindigkeit eines Jets (Vielfaches der Fluggeschwindigkeit; Sprünge zeigen sich
    /// als großer Wert), größter Winkel zwischen Raketenstart und Flugrichtung des Schützen (Grad), größter Abstand Gegner–Einschlag
    /// (in Symbolgrößen), kleinster Längsabstand Schütze hinter Gegner beim Abschuss (in Spannweiten; negativ = davor).</summary>
    internal (double SpeedRatio, double LaunchAngle, double TargetMiss, double Behind) Metrics(double iconSize)
    {
        double maxV = 0;
        for (double t = 0.5; t < breakAt; t += 1 / 60.0)
            foreach (bool f in new[] { true, false })
            {
                double v = (PlaneAt(f, t + 1 / 60.0) - PlaneAt(f, t)).Length * 60 / speed;
                if (v > 2.2 && Environment.GetEnvironmentVariable("DK_DUEL_DEBUG") is "1") Console.WriteLine($"spitze t={t:0.00} {(f ? "F35" : "Su57")} {v:0.0}x lead={Lead(t):0.00} d={(t - t0) * speed:0}");
                maxV = Math.Max(maxV, v);
            }
        double angle = 0, miss = 0, behind = double.MaxValue;
        foreach (var s in Shots)
        {
            var launch = MissileAt(s, 0.02) - MissileAt(s, 0);
            double h = Heading(s.ByF35, s.Fire);
            angle = Math.Max(angle, Math.Abs(Math.IEEERemainder(Math.Atan2(launch.Y, launch.X) - h, 2 * Math.PI)) * 180 / Math.PI);
            miss = Math.Max(miss, (PlaneAt(!s.ByF35, s.Impact - 0.3) - s.To).Length / iconSize);
            var to = PlaneAt(!s.ByF35, s.Fire) - PlaneAt(s.ByF35, s.Fire);
            behind = Math.Min(behind, 180 - Math.Abs(Math.IEEERemainder(Math.Atan2(to.Y, to.X) - h, 2 * Math.PI)) * 180 / Math.PI);
        }
        return (maxV, angle, miss, behind == double.MaxValue ? 0 : behind);
    }

    static readonly Brush Smoke = Fly.B("#E6EEF0F2"), Vapor = Fly.B("#FFFFFFFF"), Burner = Art.Glow("#F0FF9A3C"), BurnerCore = Art.Glow("#F8FFE8C0"), Muzzle = Art.Glow("#F0FFD27A");

    public void Draw(DrawingContext dc, double t)
    {
        // Rauchspuren aller Raketen: ganzer Weg, jeder Punkt verblasst mit seinem Alter
        foreach (var s in Shots)
        {
            if (t < s.Fire || t > s.Impact + TrailLife) continue;
            double head = Math.Min(1, (t - s.Fire) / (s.Impact - s.Fire));
            var u = s.ByF35 ? uF : uS;
            Point prev = MissileAt(s, 0);
            for (int i = 1; i <= 26; i++)
            {
                double k = head * i / 26;
                var p = MissileAt(s, k);
                double age = t - (s.Fire + k * (s.Impact - s.Fire));
                double fade = 1 - Math.Clamp(age / TrailLife, 0, 1);
                if (fade > 0.02)
                {
                    dc.DrawLine(Soft.FadePen(Smoke, (4 + 9 * (1 - fade)) * u * 1.3, 0.28 * fade * fade), prev, p);
                    dc.DrawLine(Soft.FadePen(Smoke, (1.6 + 2 * (1 - fade)) * u * 1.3, 0.75 * fade * fade), prev, p);
                }
                prev = p;
            }
        }
        // Täuschkörperfächer („Engel“): je Seite drei Glutpunkte, die nach außen und hinten abfallen
        foreach (var f in fans)
        {
            double age = t - f.Start;
            if (age < 0 || age > 1.8) continue;
            var origin = PlaneAt(f.OfF35, f.Start);
            double h = Heading(f.OfF35, f.Start);
            var fwd = new Vector(Math.Cos(h), Math.Sin(h)); var n = new Vector(-fwd.Y, fwd.X);
            double span = f.OfF35 ? spanF : spanS;
            for (int k = 0; k < 6; k++)
            {
                double sgn = k % 2 == 0 ? 1 : -1, lane = 1 + k / 2;
                double a = Math.Max(0, age - k * 0.06);
                Point FlareAt(double x) => origin + n * (sgn * span * (0.9 + 0.55 * lane) * (1 - Math.Exp(-x * 2.2))) - fwd * (span * 1.6 * x + span * 0.5 * x * x * lane);
                Planes.Flare(dc, FlareAt(Math.Max(0, a - 0.22)), FlareAt(a), (f.OfF35 ? uF : uS) * 1.3, a, 1.8);
            }
        }
        // Raketen im Flug
        foreach (var s in Shots)
        {
            if (t < s.Fire || t >= s.Impact) continue;
            double k = (t - s.Fire) / (s.Impact - s.Fire);
            var p = MissileAt(s, k); var q = MissileAt(s, Math.Max(0, k - 0.03));
            var u = s.ByF35 ? uF : uS;
            Planes.Rocket(dc, q, p, u * 1.3, t);
            Planes.Maverick(dc, p, Math.Atan2(p.Y - q.Y, p.X - q.X), u * 0.75);
        }
        // Kanonenstöße: Leuchtspur nach vorn, Mündungsfeuer
        foreach (var b in bursts)
        {
            if (t < b.Start || t > b.Start + 1.1) continue;
            var u = b.ByF35 ? uF : uS;
            for (int i = 0; i < 9; i++)
            {
                double shot = b.Start + i * 0.07, age = t - shot;
                if (age < 0 || age > 0.42) continue;
                var gun = PlaneAt(b.ByF35, shot); double h = Heading(b.ByF35, shot);
                var fwd = new Vector(Math.Cos(h), Math.Sin(h));
                var at = gun + fwd * ((b.ByF35 ? spanF : spanS) * 0.6 + speed * 2.4 * age) + new Vector(-fwd.Y, fwd.X) * ((i % 3 - 1) * u * 2);
                Planes.Tracer(dc, at - fwd * u * 10, at, u * 1.2);
            }
            if (t < b.Start + 0.6 && (int)(t * 30) % 2 == 0)
            {
                var gun = PlaneAt(b.ByF35, t); double h = Heading(b.ByF35, t);
                Soft.Draw(dc, Muzzle, gun + new Vector(Math.Cos(h), Math.Sin(h)) * (b.ByF35 ? spanF : spanS) * 0.55, u * 6, u * 6, 0.9);
            }
        }
        // Jets: wer hinten ist, zuerst
        Wreck(dc, t);
        bool fFirst = F35Ahead(t);
        foreach (bool f in fFirst ? new[] { false, true } : new[] { true, false })
            if (!(f && t >= killAt)) Jet(dc, f, t);
        foreach (var (p, at, size) in airBursts) AirBurst(dc, p, t - at, size);
    }

    static readonly Brush Soot = Art.Glow("#B0262628"), Fire = Art.Glow("#F0FF8A2A"), FireCore = Art.Glow("#F8FFE6A0"), Heat = Art.Glow("#90FFB060"), Ash = Fly.B("#2A2A2C");

    /// <summary>Brennendes Wrack mit Rauchfahne, Trümmerteile aus dem Treffer, nach dem Aufschlag eine Rauchsäule.</summary>
    void Wreck(DrawingContext dc, double t)
    {
        if (t < killAt) return;
        double tau = t - killAt, fall = crashAt - killAt;
        // Rauchfahne: frühere Lagen des Wracks, wächst und verblasst
        for (int j = 14; j >= 1; j--)
        {
            double tj = t - j * 0.09;
            if (tj < killAt) continue;
            double age = t - tj;
            Soft.Draw(dc, Soot, WreckAt(tj), spanF * (0.3 + age * 0.6), spanF * (0.3 + age * 0.6), 0.9 * (1 - age / 1.35));
        }
        // Trümmerteile
        for (int k = 0; k < 8; k++)
        {
            double a = k * 0.785 + 0.4, v = speed * (0.35 + 0.08 * (k % 4));
            double life = 1.4 + 0.15 * (k % 3);
            if (tau > life) continue;
            var dir = new Vector(Math.Cos(a), Math.Sin(a));
            var p = killPoint + killVelocity * (0.6 * (1 - Math.Exp(-tau / 0.6))) + dir * v * (0.8 * (1 - Math.Exp(-tau / 0.8)));
            double fade = 1 - tau / life, z = spanF * (0.05 + 0.02 * (k % 3));
            Soft.Draw(dc, Fire, p, z * 2.2, z * 2.2, 0.6 * fade);
            dc.PushTransform(new RotateTransform((tau * (200 + 60 * k)) % 360, p.X, p.Y));
            dc.DrawRectangle(Soft.Fade(Ash, fade), null, new Rect(p.X - z, p.Y - z * 0.5, z * 2, z));
            dc.Pop();
        }
        if (tau < fall)
        {   // das Wrack selbst: dreht, sinkt, brennt
            var p = WreckAt(t); double k = tau / fall;
            Soft.Draw(dc, Fire, p, spanF * (0.6 + 0.12 * Math.Sin(t * 31)), spanF * 0.5, 0.95);
            Planes.F35(dc, p, WreckHeading(t), uF * (1 - 0.15 * k), 0.45 * (1 - Fly.Smooth(k)), t);
            Soft.Draw(dc, FireCore, p + new Vector(Math.Cos(WreckHeading(t)), Math.Sin(WreckHeading(t))) * -spanF * 0.25, spanF * 0.26, spanF * 0.2, 0.95);
            Soft.Draw(dc, Fire, p + new Vector(Math.Cos(WreckHeading(t) + 1.6), Math.Sin(WreckHeading(t) + 1.6)) * spanF * 0.3, spanF * 0.22, spanF * 0.18, 0.85);
        }
        else
        {   // nach dem Aufschlag: Rauchsäule von oben gesehen, langsam breiter und heller
            double age = tau - fall;
            var c = WreckAt(crashAt);
            for (int j = 0; j < 4; j++)
                Soft.Draw(dc, Soot, c + new Vector(Math.Cos(j * 1.7) * spanF * 0.3 * age, Math.Sin(j * 1.7) * spanF * 0.3 * age), spanF * (0.6 + age * 0.45), spanF * (0.55 + age * 0.4), Math.Max(0, 0.65 - age * 0.18));
        }
    }

    /// <summary>Explosion in der Luft bzw. beim Aufschlag: Blitz, Feuerball aus der Bildfolge der Szene, Hitzeschein.</summary>
    void AirBurst(DrawingContext dc, Point p, double age, double size)
    {
        const double burst = 1.3;
        if (age < 0 || age > burst) return;
        double s = spanF * 0.55 * size;
        double flash = 1 - Fly.Smooth(age / 0.25);
        if (flash > 0.01) { Soft.Draw(dc, Heat, p, s * 3.4, s * 3.2, flash); Soft.Draw(dc, FireCore, p, s * 1.1, s * 1.1, flash); }
        var frames = Art.Explosion;
        var image = frames[(int)(Math.Clamp(age / burst, 0, 0.999) * frames.Length)];
        double d = s * 6;
        dc.DrawImage(image, new Rect(p.X - d / 2, p.Y - d / 2, d, d));
    }

    void Jet(DrawingContext dc, bool f35, double t)
    {
        var at = PlaneAt(f35, t); double h = Heading(f35, t);
        double span = f35 ? spanF : spanS, u = f35 ? uF : uS;
        var fwd = new Vector(Math.Cos(h), Math.Sin(h)); var n = new Vector(-fwd.Y, fwd.X);
        // Kurvenlast aus der Richtungsänderung: Wirbelschleppen werden dichter
        double turn = Math.Abs(Math.IEEERemainder(Heading(f35, t + 0.1) - Heading(f35, t - 0.1), 2 * Math.PI));
        double g = Math.Clamp(turn * 4, 0.15, 1) + Fly.Smooth((t - breakAt) / 0.6) * 0.6;
        double tip = span * 0.48;
        for (int sgn = -1; sgn <= 1; sgn += 2)
        {
            Point? prev = null;
            for (int i = 0; i <= 18; i++)
            {
                double ti = t - i * 0.045;
                var p = PlaneAt(f35, ti); double hi = Heading(f35, ti);
                var ni = new Vector(-Math.Sin(hi), Math.Cos(hi));
                var w = p + ni * (sgn * tip) - new Vector(Math.Cos(hi), Math.Sin(hi)) * span * 0.3;
                if (prev is { } q && (w - q).Length < span * 0.9) dc.DrawLine(Soft.FadePen(Vapor, u * (1.4 + i * 0.16), 0.32 * g * (1 - i / 19.0)), q, w);
                prev = w;
            }
        }
        // Nachbrenner: flackernde Glut hinter den Düsen, beim Auseinanderbrechen voll
        double ab = 0.55 + 0.25 * Math.Sin(t * 47 + (f35 ? 0 : 1.3)) + Fly.Smooth((t - breakAt) / 0.5) * 0.5;
        double back = span * (f35 ? 0.6 : 0.62);
        var nozzles = f35 ? new[] { 0.0 } : new[] { -0.1, 0.1 };
        foreach (double o in nozzles)
        {
            var c = at - fwd * back + n * (o * span);
            dc.PushTransform(new RotateTransform(h * 180 / Math.PI, c.X, c.Y));
            Soft.Draw(dc, Burner, new Point(c.X - span * 0.12 * ab, c.Y), span * 0.22 * ab, span * 0.09 * ab, 0.85);
            Soft.Draw(dc, BurnerCore, c, span * 0.08 * ab, span * 0.045 * ab, 0.95);
            dc.Pop();
        }
        if (f35) Planes.F35(dc, at, h, u, 0.45, t);
        else Planes.Su57(dc, at, h, u, 0.45, t);
    }
}
