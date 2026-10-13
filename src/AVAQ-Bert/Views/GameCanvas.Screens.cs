using Avalonia;
using Avalonia.Media;
using AVAQBert.Engine;
using AVAQBert.Models;

namespace AVAQBert.Views;

public sealed partial class GameCanvas
{
    private const double RightPanel = FieldLeft + Field.Width * Scale;

    /// <summary>The panels either side: scores, spare lives and the colour to aim for on the left; level, round and keys on the right.</summary>
    private void DrawPanels(DrawingContext dc)
    {
        var g = Engine;
        double lx = FieldLeft / 2, rx = RightPanel + (ScreenWidth - RightPanel) / 2;
        bool playing = g.State is GameState.Playing or GameState.Paused or GameState.GameOver or GameState.EnterInitials or GameState.Attract;

        Text(dc, "PLAYER 1", new Point(lx, 60), 30, Orange, center: true);
        Text(dc, (playing ? g.Score : 0).ToString(), new Point(lx, 92), 34, White, center: true);
        Text(dc, "HI-SCORE", new Point(lx, 160), 30, Orange, center: true);
        Text(dc, Math.Max(g.HighScores.TopScore, g.Score).ToString(), new Point(lx, 192), 34, White, center: true);

        if (playing)
        {
            // The colour to turn every cube.
            Text(dc, "CHANGE TO:", new Point(lx, 270), 28, Dim, center: true);
            var c = g.Colours;
            var centre = new Point(lx, 310);
            void Poly(uint argb, params Point[] pts)
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
            Poly(c.Left, centre + new Point(-36, 0), centre + new Point(0, 18), centre + new Point(0, 70), centre + new Point(-36, 52));
            Poly(c.Right, centre + new Point(0, 18), centre + new Point(36, 0), centre + new Point(36, 52), centre + new Point(0, 70));
            Poly(c.Done, centre + new Point(0, -18), centre + new Point(36, 0), centre + new Point(0, 18), centre + new Point(-36, 0));

            int spare = g.Lives - (g.Phase is Phase.Caught or Phase.Fell ? 0 : 1);
            for (int i = 0; i < Math.Min(spare, 5); i++) DrawPanelSprite(dc, Sprites.HeroStand, lx - 60 + i * 30, 690);
            Text(dc, "LEVEL", new Point(rx, 60), 30, Orange, center: true);
            Text(dc, g.Level.ToString(), new Point(rx, 92), 34, White, center: true);
            Text(dc, "ROUND", new Point(rx, 160), 30, Orange, center: true);
            Text(dc, g.Round.ToString(), new Point(rx, 192), 34, White, center: true);
            Text(dc, "CUBES", new Point(rx, 260), 30, Orange, center: true);
            Text(dc, $"{g.Pyramid.FinishedCount} / 28", new Point(rx, 292), 34, Yellow, center: true);
        }
        Text(dc, "AVAQ-BERT", new Point(rx, 520), 24, Dim, center: true);
        Text(dc, "Arrows: up = up-right,", new Point(rx, 550), 20, Dim, center: true);
        Text(dc, "right = down-right ...", new Point(rx, 570), 20, Dim, center: true);
        Text(dc, "or Q E Z C / 7 9 1 3", new Point(rx, 590), 20, Dim, center: true);
        Text(dc, "Enter or Space starts", new Point(rx, 610), 20, Dim, center: true);
        Text(dc, "M mute  P pause", new Point(rx, 640), 20, Dim, center: true);
        Text(dc, "Ctrl+S screenshot", new Point(rx, 660), 20, Dim, center: true);
    }

    private static void DrawPanelSprite(DrawingContext dc, Sprites.Sprite s, double left, double top)
    {
        const double px = 1.6;
        for (int r = 0; r < s.Height; r++)
            for (int c = 0; c < s.Width; c++)
            {
                char ch = s.Rows[r][c];
                if (ch == '.') continue;
                dc.FillRectangle(Brush(Sprites.Colours.GetValueOrDefault(ch, 0xFFFF00FF)), new Rect(left + c * px, top + r * px, px + 0.2, px + 0.2));
            }
    }

    private static void Panel(DrawingContext dc, double top, double bottom) =>
        dc.FillRectangle(Brush(0xE0000010), new Rect(P(10, top), P(Field.Width - 10, bottom)));

    private void DrawTitle(DrawingContext dc)
    {
        double cx = Field.Width / 2.0;
        // A little pyramid behind the title, its cubes changing colour.
        var col = Colours.Rounds[(int)(_clock / 3) % Colours.Rounds.Length];
        for (int r = 0; r < 3; r++)
            for (int c = 0; c <= r; c++)
            {
                var top = new Vec(cx + (c - r / 2.0) * 32, 158 + r * 24);
                int lit = (int)(_clock * 3) % 7;
                DrawCube(dc, top, r * 3 + c < lit ? col.Done : col.Start, col.Left, col.Right);
            }
        DrawSprite(dc, Sprites.HeroStand, cx - 8, 158 - 17);
        Text(dc, "AVA Q-BERT", P(cx, 20), 84, Orange, center: true);
        Text(dc, "AFTER THE 1982 ARCADE CLASSIC", P(cx, 36), 26, Dim, center: true);
        Text(dc, "HOP ON EVERY CUBE TO CHANGE ITS COLOUR", P(cx, 50), 26, White, center: true);
        Row(Sprites.RedBall, 66, "RED BALL: KEEP CLEAR", "");
        Row(Sprites.Viper[0], 85, "VIPER: LURE IT OFF FROM A DISC", "500");
        Row(Sprites.GreenBall, 104, "GREEN BALL: STOPS EVERYTHING", "100");
        Row(Sprites.Gremlin, 120, "GREMLIN: UNDOES YOUR CUBES", "300");
        Row(Sprites.Disc[0], 135, "CUBE CHANGED / DISC LEFT OVER", "25 / 50");
        if (_clock % 1 < 0.65) Text(dc, "PRESS ENTER OR SPACE TO START", P(cx, 242), 30, White, center: true);
        Text(dc, "© 2026 LONNIE WATSON", P(cx, 252), 22, Dim, center: true);

        void Row(Sprites.Sprite s, double y, string name, string points)
        {
            DrawSprite(dc, s, 22 - s.Width / 2.0, y - s.Height / 2.0);
            Text(dc, name, P(36, y), 25, White);
            if (points.Length > 0) Text(dc, points, P(212, y), 25, White, right: true);
        }
    }

    /// <summary>A score's place on the table: level and round.</summary>
    public static string StageName(int stage) => $"{stage / 10}-{stage % 10}";

    private void DrawHighScores(DrawingContext dc)
    {
        double cx = Field.Width / 2.0;
        Panel(dc, 10, 236);
        Text(dc, "TOP HOPPERS", P(cx, 30), 60, Orange, center: true);
        Text(dc, "RANK    SCORE   NAME  REACHED", P(cx, 50), 26, Dim, center: true);
        var table = Engine.HighScores.Entries;
        for (int i = 0; i < table.Count; i++)
        {
            var e = table[i];
            bool fresh = i == Engine.NewEntryIndex;
            if (fresh && _clock % 0.5 > 0.35) continue;
            var brush = fresh ? Yellow : i == 0 ? Orange : White;
            Text(dc, $"{i + 1,2}.  {e.Score,8}   {e.Initials}   {StageName(e.Stage),-4}", P(cx, 66 + i * 14), 32, brush, center: true);
        }
        if (_clock % 1 < 0.65) Text(dc, "PRESS ENTER OR SPACE TO PLAY", P(cx, 220), 30, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var entry = Engine.Initials;
        if (entry is null) return;
        dc.FillRectangle(Brush(0xE8000000), new Rect(P(16, 92), P(208, 170)));
        double cx = Field.Width / 2.0;
        Text(dc, "A NEW HIGH SCORE!", P(cx, 102), 44, Yellow, center: true);
        Text(dc, $"{Engine.Score}", P(cx, 116), 40, White, center: true);
        for (int i = 0; i < HighScoreTable.InitialsLength; i++)
        {
            var at = P(cx - 12 + i * 12, 138);
            bool cursor = i == entry.Cursor;
            Text(dc, entry.Initials[i].ToString(), at, 70, cursor ? Yellow : White, center: true);
            if (cursor && _clock % 0.6 < 0.4)
                dc.FillRectangle(Yellow, new Rect(at.X - 16, at.Y + 32, 32, 5));
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE.  ENTER WHEN DONE", P(cx, 162), 22, Dim, center: true);
    }
}
