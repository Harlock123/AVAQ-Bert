using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVAQBert.Engine;
using AVAQBert.Models;

namespace AVAQBert.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVAQ-Bert/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
        {
            b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
            BrushCache[argb] = b;
        }
        return b;
    }

    private static readonly IBrush White = Brush(0xFFF2F2F2);
    private static readonly IBrush Yellow = Brush(0xFFFFE14A);
    private static readonly IBrush Red = Brush(0xFFFF3D3D);
    private static readonly IBrush Orange = Brush(0xFFFF8A1E);
    private static readonly IBrush Dim = Brush(0xFF8A8AA6);

    /// <summary>Game pixels to the 1024×768 screen.</summary>
    private static Point P(double x, double y) => new(FieldLeft + x * Scale, FieldTop + y * Scale);
    private static Point P(Vec v) => P(v.X, v.Y);

    private static void Fill(DrawingContext dc, uint argb, double x, double y, double w, double h) =>
        dc.FillRectangle(Brush(argb), new Rect(FieldLeft + x * Scale, FieldTop + y * Scale, w * Scale, h * Scale));

    private void DrawScreen(DrawingContext dc)
    {
        var g = Engine;
        using (dc.PushClip(new Rect(P(0, 0), P(Field.Width, Field.Height))))
        {
            switch (g.State)
            {
                case GameState.Title:
                    DrawTitle(dc);
                    break;
                case GameState.HighScores:
                    DrawHighScores(dc);
                    break;
                default:
                    DrawPlay(dc);
                    break;
            }
        }
        DrawPanels(dc);
    }

    // ----- the pyramid --------------------------------------------------------------------------

    private static void Polygon(DrawingContext dc, uint argb, params Point[] pts)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(pts[0], true);
            foreach (var pt in pts.Skip(1)) ctx.LineTo(pt);
            ctx.EndFigure(true);
        }
        dc.DrawGeometry(Brush(argb), null, geo);
    }

    /// <summary>A cube: its top face (the colour that counts) and its two sides.</summary>
    private static void DrawCube(DrawingContext dc, Vec top, uint face, uint left, uint right)
    {
        double x = top.X, y = top.Y;
        Polygon(dc, left, P(x - 16, y), P(x, y + 8), P(x, y + 32), P(x - 16, y + 24));
        Polygon(dc, right, P(x, y + 8), P(x + 16, y), P(x + 16, y + 24), P(x, y + 32));
        Polygon(dc, face, P(x, y - 8), P(x + 16, y), P(x, y + 8), P(x - 16, y));
    }

    private void DrawPyramid(DrawingContext dc)
    {
        var g = Engine;
        var col = g.Colours;
        bool flash = g.Phase == Phase.RoundClear && (int)(g.PhaseTime * 8) % 2 == 0;
        foreach (var c in Field.Cubes())
        {
            uint face = flash ? 0xFFF4F4F4 : col.Top(g.Pyramid[c], g.Pyramid.Goal);
            DrawCube(dc, Field.Top(c), face, col.Left, col.Right);
        }
    }

    // ----- the play -----------------------------------------------------------------------------

    private void DrawPlay(DrawingContext dc)
    {
        var g = Engine;
        // Things falling off the far side drop behind the pyramid.
        if (g.Hero.Falling && g.Hero.From.Row > g.Hero.Cell.Row) DrawHero(dc);
        DrawPyramid(dc);

        int spin = (int)(_clock * 10) % 3;
        foreach (var d in g.Discs)
        {
            if (d.Used) continue;
            var at = g.Hero.Riding == d ? g.HeroPos : Field.Top(d.Spot);
            DrawSprite(dc, Sprites.Disc[spin], at.X - 8, at.Y - 2);
        }

        foreach (var f in g.Foes)
        {
            var p = f.Pos;
            switch (f.Kind)
            {
                case FoeKind.RedBall: DrawSprite(dc, Sprites.RedBall, p.X - 6, p.Y - 12); break;
                case FoeKind.PurpleBall:
                    bool shake = f.Hatch > 0 && (int)(_clock * 20) % 2 == 0;
                    DrawSprite(dc, Sprites.PurpleBall, p.X - 6 + (shake ? 1 : 0), p.Y - 12);
                    break;
                case FoeKind.GreenBall: DrawSprite(dc, Sprites.GreenBall, p.X - 6, p.Y - 12); break;
                case FoeKind.Gremlin: DrawSprite(dc, Sprites.Gremlin, p.X - 7, p.Y - 14, flip: f.Facing is Hop.DownLeft or Hop.UpLeft); break;
                case FoeKind.Viper: DrawSprite(dc, Sprites.Viper[f.Hopping ? 1 : 0], p.X - 8, p.Y - 22, flip: f.Facing is Hop.DownLeft or Hop.UpLeft); break;
            }
        }

        if (!(g.Hero.Falling && g.Hero.From.Row > g.Hero.Cell.Row)) DrawHero(dc);

        foreach (var fx in g.Effects)
        {
            if (fx.Kind == EffectKind.Curse)
            {
                var at = fx.Pos;
                var box = new Rect(P(at.X + 4, at.Y - 34), P(at.X + 60, at.Y - 20));
                dc.DrawRectangle(White, null, box, 10, 10);
                Polygon(dc, 0xFFF2F2F2, P(at.X + 8, at.Y - 21), P(at.X + 16, at.Y - 21), P(at.X + 4, at.Y - 14));
                Text(dc, fx.Text, new Point(box.Center.X, box.Center.Y), 30, Brush(0xFF14141C), center: true);
            }
            else Text(dc, fx.Text, P(fx.Pos.X, fx.Pos.Y - 20 - fx.Age * 8), 28, Yellow, center: true);
        }

        if (g.FreezeLeft > 0) Text(dc, "FROZEN!", P(Field.Width / 2.0, 236), 34, Brush(0xFF3CD84A), center: true);

        switch (g.State)
        {
            case GameState.Paused:
                Banner(dc, "PAUSED", "ESC TO QUIT, ANY OTHER KEY TO RESUME");
                break;
            case GameState.GameOver:
                Banner(dc, "GAME  OVER", null);
                break;
            case GameState.EnterInitials:
                DrawInitials(dc);
                break;
            default:
                if (g.Phase == Phase.Ready && g.PhaseTime < 1.2 && g.Pyramid.FinishedCount == 0)
                    Banner(dc, $"LEVEL {g.Level}  ROUND {g.Round}", g.State == GameState.Attract ? "DEMONSTRATION" : null);
                else if (g.Phase == Phase.RoundClear) Banner(dc, "WELL HOPPED!", null);
                else if (g.State == GameState.Attract) Text(dc, "DEMONSTRATION", P(Field.Width / 2.0, 16), 30, Red, center: true);
                break;
        }
    }

    private void Banner(DrawingContext dc, string title, string? sub)
    {
        Fill(dc, 0xE0000000, 24, 220, 176, sub is null ? 24 : 34);
        Text(dc, title, P(Field.Width / 2.0, 232), 40, Yellow, center: true);
        if (sub is not null) Text(dc, sub, P(Field.Width / 2.0, 246), 26, White, center: true);
    }

    private void DrawHero(DrawingContext dc)
    {
        var g = Engine;
        var h = g.Hero;
        var p = g.HeroPos;
        bool up = h.Facing is Hop.UpLeft or Hop.UpRight;
        var art = up ? Sprites.HeroBack : h.Hopping || h.Riding is not null ? Sprites.HeroHop : Sprites.HeroStand;
        DrawSprite(dc, art, p.X - 8, p.Y - 17, flip: h.Facing is Hop.DownLeft or Hop.UpLeft);
    }

    /// <summary>Draws a sprite with its top-left at a game point, optionally mirrored.</summary>
    private static void DrawSprite(DrawingContext dc, Sprites.Sprite s, double x, double y, bool flip = false, double px = 1)
    {
        for (int r = 0; r < s.Height; r++)
        {
            var row = s.Rows[r];
            for (int c = 0; c < s.Width; c++)
            {
                char ch = row[flip ? s.Width - 1 - c : c];
                if (ch == '.') continue;
                var a = P(x + c * px, y + r * px);
                dc.FillRectangle(Brush(Sprites.Colours.GetValueOrDefault(ch, 0xFFFF00FF)), new Rect(a.X, a.Y, Scale * px + 0.3, Scale * px + 0.3));
            }
        }
    }

    private static void Text(DrawingContext dc, string text, Point at, double size, IBrush brush, bool center = false, bool right = false)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, brush);
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }
}
