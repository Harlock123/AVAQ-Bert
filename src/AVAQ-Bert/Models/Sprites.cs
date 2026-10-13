namespace AVAQBert.Models;

/// <summary>
/// The game's pixel art, this game's own drawings. The hero is drawn row by row; the bigger and
/// rounder things are painted from ellipses and boxes into a pixel grid, which keeps them easy to read
/// and to change. Each letter is a colour (see <see cref="Colours"/>); '.' is clear.
/// </summary>
public static class Sprites
{
    public sealed record Sprite(string[] Rows)
    {
        public int Width => Rows[0].Length;
        public int Height => Rows.Length;
    }

    private static Sprite S(params string[] rows)
    {
        if (rows.Any(r => r.Length != rows[0].Length))
            throw new ArgumentException("Sprite rows must be the same width: " + string.Join("|", rows));
        return new Sprite(rows);
    }

    /// <summary>A pixel grid to paint shapes into.</summary>
    private sealed class Canvas(int width, int height)
    {
        private readonly char[,] _px = Fill(width, height);

        private static char[,] Fill(int w, int h)
        {
            var g = new char[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++) g[x, y] = '.';
            return g;
        }

        public Canvas Ellipse(double cx, double cy, double rx, double ry, char c)
        {
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
                    if (dx * dx + dy * dy <= 1) _px[x, y] = c;
                }
            return this;
        }

        public Canvas Box(int x0, int y0, int x1, int y1, char c)
        {
            for (int x = Math.Max(0, x0); x <= Math.Min(width - 1, x1); x++)
                for (int y = Math.Max(0, y0); y <= Math.Min(height - 1, y1); y++) _px[x, y] = c;
            return this;
        }

        public Canvas Dot(int x, int y, char c) => Box(x, y, x, y, c);

        public Sprite Done()
        {
            var rows = new string[height];
            for (int y = 0; y < height; y++)
            {
                var chars = new char[width];
                for (int x = 0; x < width; x++) chars[x] = _px[x, y];
                rows[y] = new string(chars);
            }
            return new Sprite(rows);
        }
    }

    public static readonly IReadOnlyDictionary<char, uint> Colours = new Dictionary<char, uint>
    {
        ['w'] = 0xFFF4F4F4, // white
        ['k'] = 0xFF14141C, // black
        ['o'] = 0xFFFF8A1E, // orange
        ['O'] = 0xFFC05A10, // dark orange
        ['r'] = 0xFFE83A3A, // red
        ['R'] = 0xFF9A1E1E, // dark red
        ['p'] = 0xFFB050F0, // purple
        ['P'] = 0xFF6A2A9A, // dark purple
        ['g'] = 0xFF3CD84A, // green
        ['G'] = 0xFF1E8A2A, // dark green
        ['y'] = 0xFFFFE14A, // yellow
        ['c'] = 0xFF4FD7FF, // cyan
        ['m'] = 0xFF9A9AAA, // grey
    };

    // ----- the hero: a round, big-eyed hopper with a trunk of a nose (facing down-right) ----------

    private static Canvas Hero(bool tucked) => new Canvas(16, 16)
        .Ellipse(7, 7, 6, 6, 'o').Ellipse(5, 5, 2, 1.5, 'y')                    // body, shine
        .Ellipse(8, 6, 2.2, 2.4, 'w').Ellipse(11, 6, 2.2, 2.4, 'w').Dot(9, 7, 'k').Dot(12, 7, 'k')
        .Box(11, 9, 13, 10, 'O').Box(13, 10, 15, 12, 'O').Dot(15, 12, 'k')       // the trunk, out and down
        .Box(tucked ? 4 : 3, 13, tucked ? 5 : 4, 15, 'O').Box(tucked ? 8 : 9, 13, tucked ? 9 : 10, 15, 'O')
        .Box(tucked ? 3 : 2, 15, tucked ? 6 : 5, 15, 'O').Box(tucked ? 7 : 8, 15, tucked ? 10 : 11, 15, 'O');

    public static readonly Sprite HeroStand = Hero(false).Done();
    public static readonly Sprite HeroHop = Hero(true).Done();

    /// <summary>Facing up the pyramid: the back of the head.</summary>
    public static readonly Sprite HeroBack = new Canvas(16, 16).Ellipse(8, 7, 6, 6, 'o').Ellipse(6, 5, 2, 1.5, 'y')
        .Box(4, 13, 5, 15, 'O').Box(10, 13, 11, 15, 'O').Box(3, 15, 6, 15, 'O').Box(9, 15, 12, 15, 'O').Done();

    // ----- the others -----------------------------------------------------------------------------

    public static Sprite Ball(char main, char dark) => new Canvas(12, 12).Ellipse(6, 6, 5.8, 5.8, main).Ellipse(7, 7.5, 4, 3.5, dark)
        .Ellipse(6, 6, 4.2, 4.2, main).Ellipse(4.5, 4, 1.6, 1.3, 'w').Done();

    public static readonly Sprite RedBall = Ball('r', 'R');
    public static readonly Sprite PurpleBall = Ball('p', 'P');
    public static readonly Sprite GreenBall = Ball('g', 'G');

    /// <summary>The viper: coiled up on its tail, two frames.</summary>
    public static readonly Sprite[] Viper =
    [
        new Canvas(16, 22).Ellipse(8, 18, 6, 3.5, 'p').Ellipse(8, 18, 3.5, 1.8, 'P')
            .Ellipse(7, 13, 4.5, 3, 'p').Ellipse(9, 8, 3.5, 3, 'p').Ellipse(8, 3.5, 4, 3.5, 'p')
            .Dot(6, 3, 'y').Dot(10, 3, 'y').Dot(6, 2, 'k').Dot(10, 2, 'k').Box(7, 6, 9, 6, 'r').Dot(8, 7, 'r').Done(),
        new Canvas(16, 22).Ellipse(8, 19, 6, 3, 'p').Ellipse(8, 19, 3.5, 1.5, 'P')
            .Ellipse(9, 14, 4.5, 3, 'p').Ellipse(7, 9, 3.5, 3, 'p').Ellipse(8, 4.5, 4, 3.5, 'p')
            .Dot(6, 4, 'y').Dot(10, 4, 'y').Dot(6, 3, 'k').Dot(10, 3, 'k').Box(7, 7, 9, 7, 'r').Done(),
    ];

    /// <summary>The gremlin: a little green imp who turns cubes back.</summary>
    public static readonly Sprite Gremlin = new Canvas(14, 14).Ellipse(7, 7, 5, 5, 'g').Dot(3, 2, 'g').Dot(10, 2, 'g').Dot(2, 1, 'G').Dot(11, 1, 'G')
        .Box(4, 5, 5, 6, 'w').Box(8, 5, 9, 6, 'w').Dot(5, 6, 'k').Dot(9, 6, 'k').Box(5, 9, 8, 9, 'k')
        .Box(3, 12, 4, 13, 'G').Box(9, 12, 10, 13, 'G').Done();

    /// <summary>A spinning disc: three frames of its turn.</summary>
    public static readonly Sprite[] Disc =
    [
        new Canvas(16, 8).Ellipse(8, 4, 7.5, 3.5, 'r').Ellipse(8, 4, 5, 2, 'y').Ellipse(8, 4, 2, 1, 'c').Done(),
        new Canvas(16, 8).Ellipse(8, 4, 7.5, 3.5, 'y').Ellipse(8, 4, 5, 2, 'c').Ellipse(8, 4, 2, 1, 'r').Done(),
        new Canvas(16, 8).Ellipse(8, 4, 7.5, 3.5, 'c').Ellipse(8, 4, 5, 2, 'r').Ellipse(8, 4, 2, 1, 'y').Done(),
    ];
}
