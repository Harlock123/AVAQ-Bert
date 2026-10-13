using AVAQBert.Models;

namespace AVAQBert.Engine;

public sealed partial class GameEngine
{
    public const double HopSeconds = 0.32;
    public const double ReadySeconds = 1.2;
    public const double FirstReadySeconds = 2.4;
    public const double CaughtSeconds = 2.0;
    public const double FellSeconds = 1.6;
    public const double RoundClearSeconds = 2.5;
    public const double FreezeSeconds = 3;
    public const double DiscSeconds = 1.6;
    public const int RoundsPerLevel = 4;

    public Pyramid Pyramid { get; private set; }
    public Hero Hero { get; } = new();
    public List<Foe> Foes { get; } = [];
    public List<Disc> Discs { get; } = [];
    public double FreezeLeft { get; private set; }
    public string? LastDeath { get; private set; }
    internal List<string> LossLog { get; } = [];

    /// <summary>The colours of this round.</summary>
    public Colours Colours => Colours.Rounds[((Level - 1) * RoundsPerLevel + Round - 1) % Colours.Rounds.Length];

    private double _readyFor = ReadySeconds;
    private double _spawn;
    private Hop? _heldPrev;
    private bool _calm, _hold;

    /// <summary>The others hop a little quicker each level and round.</summary>
    public double FoeHopEvery => Math.Max(0.55, 1.05 - 0.07 * (Level - 1) - 0.02 * (Round - 1));

    // ----- rounds -------------------------------------------------------------------------------

    private void StartRound(int round)
    {
        Round = round;
        Pyramid = new Pyramid { Rule = Pyramid.RuleFor(Level) };
        Discs.Clear();
        // Two discs, one each side, at rows that vary from round to round.
        var rng = new Random(Level * 97 + round * 13);
        int left = rng.Next(1, 6), right = rng.Next(1, 6);
        Discs.Add(new Disc { Spot = new Cell(left, -1) });
        Discs.Add(new Disc { Spot = new Cell(right, right + 1) });
        SetUpRound();
        SetPhase(Phase.Ready);
        _readyFor = Level == 1 && round == 1 ? FirstReadySeconds : ReadySeconds;
    }

    private void SetUpRound()
    {
        Foes.Clear();
        PlaceHero(new Cell(0, 0));
        FreezeLeft = 0;
        _spawn = 2;
    }

    private void PlaceHero(Cell at)
    {
        Hero.Cell = at;
        Hero.Hopping = false;
        Hero.Riding = null;
        Hero.Falling = false;
        Hero.FallY = 0;
        Hero.Facing = Hop.DownRight;
    }

    private void Simulate(double dt, Controls c)
    {
        PhaseTime += dt;
        switch (Phase)
        {
            case Phase.Ready:
                if (PhaseTime >= _readyFor) SetPhase(Phase.Playing);
                return;
            case Phase.Caught:
                if (PhaseTime < CaughtSeconds) return;
                LoseLife(Hero.Cell);
                return;
            case Phase.Fell:
                Hero.FallY += 240 * dt;
                if (PhaseTime < FellSeconds) return;
                LoseLife(new Cell(0, 0));
                return;
            case Phase.RoundClear:
                if (PhaseTime < RoundClearSeconds) return;
                if (Round < RoundsPerLevel) StartRound(Round + 1);
                else { Level++; StartRound(1); }
                return;
        }

        MoveHero(dt, c);
        if (Phase != Phase.Playing) return;
        if (FreezeLeft > 0) FreezeLeft = Math.Max(0, FreezeLeft - dt);
        else MoveFoes(dt);
        Touch();
    }

    private void LoseLife(Cell restart)
    {
        Lives--;
        if (Lives <= 0) { GameOver(); return; }
        Foes.Clear();
        PlaceHero(restart);
        _spawn = 2;
        SetPhase(Phase.Ready);
        _readyFor = ReadySeconds;
    }

    // ----- the hero -----------------------------------------------------------------------------

    /// <summary>Where the hero is drawn: hopping, floating up on a disc, or falling away.</summary>
    public Vec HeroPos
    {
        get
        {
            if (Hero.Riding is { } d)
            {
                var from = Field.Top(d.Spot);
                var top = Field.Top(new Cell(0, 0)) with { Y = Field.Top(new Cell(0, 0)).Y - 36 };
                double t = Math.Min(1, Hero.RideT);
                return from + (top - from) * t;
            }
            var p = Hero.Pos;
            return Hero.Falling ? p with { Y = p.Y + Hero.FallY } : p;
        }
    }

    private void MoveHero(double dt, Controls c)
    {
        var h = Hero;
        if (h.Riding is { } disc)
        {
            h.RideT += dt / DiscSeconds;
            if (h.RideT < 1.15) return;
            h.Riding = null;
            disc.Used = true;
            // Dropped off on the top cube.
            h.From = new Cell(-1, 0);
            h.Cell = new Cell(0, 0);
            Land();
            return;
        }
        if (h.Hopping)
        {
            h.T += dt / HopSeconds;
            if (h.T < 1) return;
            h.Hopping = false;
            Land();
            return;
        }
        // Hold a direction to keep hopping that way; a fresh press always counts.
        if (c.Hop is not { } hop) { _heldPrev = null; return; }
        _heldPrev = hop;
        h.From = h.Cell;
        h.Cell = h.Cell.Step(hop);
        h.Facing = hop;
        h.Hopping = true;
        h.T = 0;
    }

    private void Land()
    {
        var h = Hero;
        if (!h.Cell.OnPyramid)
        {
            var disc = Discs.FirstOrDefault(d => !d.Used && d.Spot == h.Cell);
            if (disc is not null)
            {
                // Carried off to the top; everything but the viper vanishes, and the viper may follow.
                h.Riding = disc;
                h.RideT = 0;
                Foes.RemoveAll(f => f.Kind is not (FoeKind.Viper or FoeKind.PurpleBall));
                // A viper close behind follows the hero off the edge.
                foreach (var v in Foes.Where(f => f.Kind == FoeKind.Viper && !f.Falling && Steps(f.Cell, disc.Spot) <= 3))
                {
                    v.Lured = disc.Spot;
                    v.Wait = Math.Min(v.Wait, 0.15);
                }
                Play(Sfx.Disc);
                return;
            }
            h.Falling = true;
            Die("jumped off the pyramid", Phase.Fell, Sfx.Fall);
            return;
        }
        Play(Sfx.Hop);
        int change = Pyramid.Land(h.Cell);
        if (change > 0) { AddScore(25); Play(Sfx.Turn); }
        if (Pyramid.AllFinished) ClearRound();
    }

    private void ClearRound()
    {
        int bonus = 1000 + 250 * ((Level - 1) * RoundsPerLevel + Round - 1);
        int unused = Discs.Count(d => !d.Used);
        AddScore(bonus + 50 * unused);
        Popup(new Vec(112, 30), bonus, 2.5);
        if (unused > 0) Play(Sfx.Unused);
        Foes.Clear();
        SetPhase(Phase.RoundClear);
        Play(Sfx.RoundClear);
    }

    private void Die(string why, Phase phase, Sfx sound)
    {
        LastDeath = why;
        LossLog.Add($"L{Level}R{Round}: {why} at {Hero.Cell}");
        SetPhase(phase);
        Play(sound);
        if (phase == Phase.Caught)
            Effects.Add(new Effect { Kind = EffectKind.Curse, Pos = HeroPos, Life = CaughtSeconds, Text = "@!#?@!" });
    }

    // ----- the others ---------------------------------------------------------------------------

    private void MoveFoes(double dt)
    {
        if (!_calm)
        {
            _spawn -= dt;
            if (_spawn <= 0 && Foes.Count(f => !f.Dead) < 2 + Level / 2)
            {
                _spawn = Math.Max(1.4, 3.4 - 0.3 * (Level - 1) - 0.1 * Round) + _rng.NextDouble();
                Spawn();
            }
        }
        foreach (var f in Foes)
        {
            if (f.Dead) continue;
            if (f.Drop > 0) { f.Drop = Math.Max(0, f.Drop - dt * 2.5); continue; }
            if (f.Falling)
            {
                f.FallY += 220 * dt;
                if (f.FallY > 260) f.Dead = true;
                continue;
            }
            if (f.Hatch > 0)
            {
                f.Hatch -= dt;
                if (f.Hatch <= 0) { f.Kind = FoeKind.Viper; f.Wait = FoeHopEvery * 0.6; }
                continue;
            }
            if (f.Hopping)
            {
                f.T += dt / (HopSeconds * 1.1);
                if (f.T < 1) continue;
                f.Hopping = false;
                FoeLanded(f);
                continue;
            }
            if (_hold && f.Kind != FoeKind.Viper) continue;
            f.Wait -= dt;
            if (f.Wait > 0) continue;
            f.Wait = f.Lured is not null ? 0.15 : FoeHopEvery * (f.Kind == FoeKind.Viper ? 0.8 : 1);
            if (f.Kind == FoeKind.PurpleBall && f.Cell.Row == Field.Rows - 1)
            {
                f.Hatch = 0.7;
                Play(Sfx.Hatch);
                continue;
            }
            var hop = f.Kind == FoeKind.Viper ? Hunt(f) : _rng.Next(2) == 0 ? Hop.DownLeft : Hop.DownRight;
            if (hop is not { } chosen) continue;
            f.From = f.Cell;
            f.Cell = f.Cell.Step(chosen);
            f.Facing = chosen;
            f.Hopping = true;
            f.T = 0;
        }
        Foes.RemoveAll(f => f.Dead);
    }

    private void Spawn()
    {
        bool viperAbout = Foes.Any(f => f.Kind is FoeKind.Viper or FoeKind.PurpleBall);
        double roll = _rng.NextDouble();
        var kind = !viperAbout && roll < 0.35 ? FoeKind.PurpleBall
            : Round >= 2 && roll < 0.55 ? FoeKind.Gremlin
            : roll < 0.68 ? FoeKind.GreenBall
            : FoeKind.RedBall;
        var cell = new Cell(1, _rng.Next(2));
        Foes.Add(new Foe { Kind = kind, Cell = cell, From = cell, Drop = 1, Wait = FoeHopEvery * 0.5 });
    }

    /// <summary>Hops between two cubes (or a cube and a spot off the edge), ignoring what's in the way.</summary>
    public static int Steps(Cell a, Cell b)
    {
        // A hop moves (row, col) by ±(1, 0) or ±(1, 1): so (dr − dc) of the first and dc of the second.
        int dr = b.Row - a.Row, dc = b.Col - a.Col;
        return Math.Abs(dr - dc) + Math.Abs(dc);
    }

    /// <summary>The viper's hop: towards the hero, or after them off the edge where they took a disc.</summary>
    private Hop? Hunt(Foe f)
    {
        var target = f.Lured ?? (Hero.Riding is not null ? new Cell(0, 0) : Hero.Cell);
        var goal = Field.Top(target);
        Hop? best = null;
        double bestDist = double.MaxValue;
        foreach (var h in Field.Hops)
        {
            var to = f.Cell.Step(h);
            if (!to.OnPyramid && to != f.Lured) continue;
            double d = (Field.Top(to) - goal).Length;
            if (d < bestDist) { bestDist = d; best = h; }
        }
        return best;
    }

    private void FoeLanded(Foe f)
    {
        Play(Sfx.FoeHop);
        if (!f.Cell.OnPyramid)
        {
            f.Falling = true;
            if (f.Kind == FoeKind.Viper)
            {
                AddScore(500);
                Popup(Field.Top(f.Cell), 500, 1.5);
                Play(Sfx.ViperFall);
            }
            return;
        }
        if (f.Kind == FoeKind.Gremlin && Pyramid.Undo(f.Cell)) Play(Sfx.Turn);
    }

    /// <summary>The hero and something else in the same place.</summary>
    private void Touch()
    {
        if (Hero.Riding is not null || Hero.Falling) return;
        var hp = HeroPos;
        foreach (var f in Foes)
        {
            if (f.Dead || f.Falling || f.Drop > 0.25 || (f.Pos - hp).Length > 11) continue;
            switch (f.Kind)
            {
                case FoeKind.GreenBall:
                    f.Dead = true;
                    FreezeLeft = FreezeSeconds;
                    AddScore(100);
                    Popup(f.Pos, 100);
                    Play(Sfx.Freeze);
                    break;
                case FoeKind.Gremlin:
                    f.Dead = true;
                    AddScore(300);
                    Popup(f.Pos, 300);
                    Play(Sfx.Catch);
                    break;
                default:
                    Die($"was caught by the {f.Kind.ToString().ToLowerInvariant()}", Phase.Caught, Sfx.Curse);
                    return;
            }
        }
        Foes.RemoveAll(f => f.Dead);
    }

    // ----- for the tests and screenshots --------------------------------------------------------

    internal void Idle(int steps)
    {
        for (int i = 0; i < steps; i++) Step(default);
    }

    internal void Go()
    {
        if (Phase == Phase.Ready) SetPhase(Phase.Playing);
    }

    internal void JumpTo(int level, int round)
    {
        Level = level;
        StartRound(round);
        Go();
    }

    /// <summary>Nothing new arrives.</summary>
    internal void Calm() => _calm = true;

    /// <summary>Everything but the viper stays where it is.</summary>
    internal void Hold() => _hold = true;

    internal void PutHero(Cell at) => PlaceHero(at);

    internal Foe PutFoe(FoeKind kind, Cell at)
    {
        var f = new Foe { Kind = kind, Cell = at, From = at, Wait = FoeHopEvery };
        Foes.Add(f);
        return f;
    }

    internal void Paint(Cell c, int state) => Pyramid.Set(c, state);
}
