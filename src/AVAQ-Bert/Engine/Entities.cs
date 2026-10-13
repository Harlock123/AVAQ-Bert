using AVAQBert.Models;

namespace AVAQBert.Engine;

/// <summary>Something that hops from cube to cube: the hero or one of the others.</summary>
public abstract class Hopper
{
    public Cell Cell { get; set; }
    /// <summary>Mid-hop: from where, and how far through (0 to 1).</summary>
    public bool Hopping { get; set; }
    public Cell From { get; set; }
    public double T { get; set; }
    public Hop Facing { get; set; } = Hop.DownRight;

    /// <summary>Where it's drawn: along an arc from the last cube to the next, feet on the top face.</summary>
    public virtual Vec Pos
    {
        get
        {
            var to = Field.Top(Cell);
            if (!Hopping) return to;
            var from = Field.Top(From);
            double lift = 14 * Math.Sin(Math.PI * T);
            return new Vec(from.X + (to.X - from.X) * T, from.Y + (to.Y - from.Y) * T - lift);
        }
    }
}

public sealed class Hero : Hopper
{
    /// <summary>Riding a disc to the top: the disc, and how far up it's got.</summary>
    public Disc? Riding { get; set; }
    public double RideT { get; set; }
    /// <summary>Jumped off the pyramid with nothing there: falling.</summary>
    public bool Falling { get; set; }
    public double FallY { get; set; }
}

public enum FoeKind
{
    /// <summary>Bounces down and off the bottom.</summary>
    RedBall,
    /// <summary>Bounces down to the bottom and hatches into the viper.</summary>
    PurpleBall,
    /// <summary>Hunts the hero; can be lured off the edge.</summary>
    Viper,
    /// <summary>Catch it and everything stops for a moment.</summary>
    GreenBall,
    /// <summary>Hops down turning cubes back; worth catching.</summary>
    Gremlin,
}

public sealed class Foe : Hopper
{
    public required FoeKind Kind { get; set; }
    /// <summary>Seconds until its next hop.</summary>
    public double Wait { get; set; }
    /// <summary>Dropping in from above at the start.</summary>
    public double Drop { get; set; }
    /// <summary>Gone off the edge (the viper falling, a ball off the bottom).</summary>
    public bool Falling { get; set; }
    public double FallY { get; set; }
    public bool Dead { get; set; }
    public double Hatch { get; set; }
    /// <summary>A viper close behind when the hero took a disc: it goes after them, off the edge.</summary>
    public Cell? Lured { get; set; }

    public override Vec Pos => Drop > 0 ? base.Pos with { Y = base.Pos.Y - Drop * 120 } : Falling ? base.Pos with { Y = base.Pos.Y + FallY } : base.Pos;
}

public enum EffectKind { Score, Curse }

public sealed class Effect
{
    public required EffectKind Kind { get; init; }
    public Vec Pos { get; set; }
    public double Age { get; set; }
    public double Life { get; init; } = 1;
    public string Text { get; init; } = "";
    public bool Done => Age >= Life;
}

/// <summary>The controls for one step: a hop asked for (or none).</summary>
public record struct Controls(Hop? Hop);
