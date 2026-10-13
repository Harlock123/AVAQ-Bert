namespace AVAQBert.Models;

/// <summary>A 2D point or vector (y grows downwards).</summary>
public readonly record struct Vec(double X, double Y)
{
    public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec operator -(Vec a, Vec b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec operator *(Vec a, double k) => new(a.X * k, a.Y * k);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public override string ToString() => $"({X:0.#}, {Y:0.#})";
}

/// <summary>A cube of the pyramid: row 0 is the top; row r has cubes 0 to r.</summary>
public readonly record struct Cell(int Row, int Col)
{
    public bool OnPyramid => Row is >= 0 and < Field.Rows && Col >= 0 && Col <= Row;
    public Cell Step(Hop h) => h switch
    {
        Hop.UpLeft => new(Row - 1, Col - 1),
        Hop.UpRight => new(Row - 1, Col),
        Hop.DownLeft => new(Row + 1, Col),
        _ => new(Row + 1, Col + 1),
    };
    public override string ToString() => $"[{Row},{Col}]";
}

/// <summary>The four diagonal hops.</summary>
public enum Hop { UpLeft, UpRight, DownLeft, DownRight }

/// <summary>
/// The screen in game pixels: 224 by 256, portrait like the arcade's, with the pyramid of 28 cubes
/// (seven rows) in the middle. A cube's top face is a diamond 32 across and 16 deep; each row sits 24
/// pixels below the one above.
/// </summary>
public static class Field
{
    public const int Width = 224;
    public const int Height = 256;
    public const int Rows = 7;
    public const double Step = 1.0 / 60;
    public const double TopY = 52;

    /// <summary>The middle of a cube's top face: where feet stand.</summary>
    public static Vec Top(Cell c) => new(112 + (c.Col - c.Row / 2.0) * 32, TopY + c.Row * 24);

    public static IEnumerable<Cell> Cubes()
    {
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c <= r; c++) yield return new Cell(r, c);
    }

    public static readonly Hop[] Hops = [Hop.UpLeft, Hop.UpRight, Hop.DownLeft, Hop.DownRight];
}
