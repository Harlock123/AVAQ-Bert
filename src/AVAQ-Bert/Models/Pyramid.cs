namespace AVAQBert.Models;

/// <summary>
/// The pyramid's cubes and how their tops change colour. Each cube starts at 0 and is finished at
/// <see cref="Goal"/>; the level sets the rule:
/// <list type="bullet">
/// <item>level 1: one hop turns a cube;</item>
/// <item>level 2: two hops (through a middle colour);</item>
/// <item>level 3: one hop turns it, but another turns it back;</item>
/// <item>level 4 and on: two hops to finish, and another knocks it back a step.</item>
/// </list>
/// </summary>
public sealed class Pyramid
{
    private readonly int[,] _state = new int[Field.Rows, Field.Rows];

    public required int Rule { get; init; }
    public int Goal => Rule is 2 or 4 ? 2 : 1;
    /// <summary>Hopping on a finished cube turns it back.</summary>
    public bool Reverts => Rule >= 3;

    public static int RuleFor(int level) => Math.Min(level, 4);

    public int this[Cell c] => _state[c.Row, c.Col];

    public bool Finished(Cell c) => _state[c.Row, c.Col] >= Goal;
    public bool AllFinished => Field.Cubes().All(Finished);
    public int FinishedCount => Field.Cubes().Count(Finished);

    /// <summary>The hero lands here: on a step, or back a step on a finished cube if the level says so. Returns +1, −1 or 0.</summary>
    public int Land(Cell c)
    {
        ref int s = ref _state[c.Row, c.Col];
        if (s < Goal) { s++; return 1; }
        if (Reverts) { s--; return -1; }
        return 0;
    }

    /// <summary>The gremlin lands here: a step back (but never below the start).</summary>
    public bool Undo(Cell c)
    {
        ref int s = ref _state[c.Row, c.Col];
        if (s == 0) return false;
        s--;
        return true;
    }

    internal void Set(Cell c, int state) => _state[c.Row, c.Col] = state;
}

/// <summary>A spinning disc beside the pyramid: hop onto it and it carries you to the top.</summary>
public sealed class Disc
{
    /// <summary>The off-pyramid spot it floats at: (row, −1) on the left, (row, row + 1) on the right.</summary>
    public required Cell Spot { get; init; }
    public bool Used { get; set; }
}

/// <summary>Each round's colours: the cube tops (start, middle, finished) and the two sides.</summary>
public sealed record Colours(uint Start, uint Middle, uint Done, uint Left, uint Right)
{
    public static readonly Colours[] Rounds =
    [
        new(0xFF3A5AE8, 0xFF3CD84A, 0xFFFFE14A, 0xFF6A6A7A, 0xFF3A3A48),
        new(0xFFE8E8F0, 0xFFB070FF, 0xFF4FD7FF, 0xFF2A6A4A, 0xFF1A4030),
        new(0xFFFF8A1E, 0xFFFF4FD8, 0xFF3CD84A, 0xFF4A3A7A, 0xFF2A2048),
        new(0xFF8A5A2A, 0xFFFFE14A, 0xFFE83A3A, 0xFF3A6A8A, 0xFF1E3A50),
        new(0xFF4FD7FF, 0xFFE8E8F0, 0xFFFF8A1E, 0xFF7A3A3A, 0xFF481E1E),
    ];

    public uint Top(int state, int goal) => state >= goal ? Done : state == 0 ? Start : Middle;
}
