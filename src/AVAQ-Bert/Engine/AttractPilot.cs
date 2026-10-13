using AVAQBert.Models;

namespace AVAQBert.Engine;

/// <summary>
/// The computer player for attract mode and the tests. Between hops it marks every cube something
/// dangerous is on or could hop to next, then hops towards the nearest cube still to be changed (going
/// round finished ones on the levels where stepping on them turns them back), never onto a marked cube.
/// It goes for the green ball and the gremlin, and takes a disc when the viper gets close.
/// </summary>
public sealed class AttractPilot(int seed)
{
    private readonly Random _rng = new(seed);

    public Controls Decide(GameEngine g)
    {
        var h = g.Hero;
        if (g.Phase != Phase.Playing || h.Hopping || h.Riding is not null || h.Falling) return default;
        var at = h.Cell;
        var danger = Danger(g);
        var viper = g.Foes.FirstOrDefault(f => f.Kind == FoeKind.Viper && !f.Falling && !f.Dead);
        bool viperNear = viper is not null && Steps(viper.Cell, at) <= 2;

        Hop? best = null;
        double bestCost = double.MaxValue;
        foreach (var hop in Field.Hops)
        {
            var to = at.Step(hop);
            if (!to.OnPyramid)
            {
                if (viperNear && g.Discs.Any(d => !d.Used && d.Spot == to)) return new Controls(hop);
                continue;
            }
            if (danger.Contains(to)) continue;
            double cost = Distance(g, to);
            if (g.Pyramid.Reverts && g.Pyramid.Finished(to)) cost += 3;
            if (g.Foes.Any(f => f.Kind is FoeKind.GreenBall or FoeKind.Gremlin && !f.Dead && f.Cell == to)) cost -= 3;
            if (viper is not null) cost -= Math.Min(3, Steps(viper.Cell, to)) * 0.5;
            cost += _rng.NextDouble() * 0.2;
            if (cost < bestCost) { bestCost = cost; best = hop; }
        }
        if (best is null) return default;
        // Stay put rather than hop if here is safe and there's nothing to gain.
        if (!danger.Contains(at) && bestCost > Distance(g, at) + 2) return default;
        return new Controls(best);
    }

    private static int Steps(Cell a, Cell b) => GameEngine.Steps(a, b);

    /// <summary>How far to the nearest cube that still needs a hop (0: this one does).</summary>
    private static double Distance(GameEngine g, Cell from)
    {
        var p = g.Pyramid;
        if (!p.Finished(from)) return 0;
        return Field.Cubes().Where(c => !p.Finished(c)).Select(c => (double)Steps(from, c)).DefaultIfEmpty(0).Min();
    }

    /// <summary>Every cube something dangerous is on, landing on, or could hop to next.</summary>
    private static HashSet<Cell> Danger(GameEngine g)
    {
        var cells = new HashSet<Cell>();
        foreach (var f in g.Foes)
        {
            if (f.Dead || f.Falling || f.Kind is FoeKind.GreenBall or FoeKind.Gremlin) continue;
            var here = f.Cell;
            cells.Add(here);
            if (f.Hopping) cells.Add(f.From);
            bool viper = f.Kind == FoeKind.Viper || f.Hatch > 0;
            var next = viper ? Field.Hops : [Hop.DownLeft, Hop.DownRight];
            foreach (var hop in next)
            {
                var to = here.Step(hop);
                if (to.OnPyramid) cells.Add(to);
            }
        }
        return cells;
    }
}
