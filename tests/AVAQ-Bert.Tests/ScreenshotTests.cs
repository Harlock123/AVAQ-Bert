using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVAQBert.Engine;
using AVAQBert.Models;
using AVAQBert.Views;
using Controls = AVAQBert.Engine.Controls;

namespace AVAQBert.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set QBERT_SHOTS to a
/// folder to save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "qbert-ui-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(seed, HighScoreTable.Load(_dir));
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("QBERT_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVAQ-Bert-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    [AvaloniaFact]
    public void The_title_and_a_round_under_way()
    {
        var (w, c, g) = Open();
        g.Idle(60 * 2);
        Shot(w, c, "title");
        g.StartGame();
        g.Go();
        var pilot = new AttractPilot(2);
        for (int i = 0; i < 60 * 40 && g.State == GameState.Playing; i++)
        {
            g.Step(pilot.Decide(g));
            if (i > 60 * 8 && g.Phase == Phase.Playing && g.Foes.Count(f => !f.Dead) >= 2 && g.Pyramid.FinishedCount > 12) break;
        }
        Shot(w, c, "round");
        w.Close();
    }

    [AvaloniaFact]
    public void The_viper_on_the_trail_and_a_curse()
    {
        var (w, c, g) = Open(seed: 5);
        g.StartGame();
        g.JumpTo(2, 2);
        g.Calm();
        foreach (var cell in Field.Cubes().Where(x => x.Row <= 3)) g.Paint(cell, 1);
        foreach (var cell in Field.Cubes().Where(x => x.Row == 2)) g.Paint(cell, 2);
        g.PutHero(new Cell(4, 1));
        var viper = g.PutFoe(FoeKind.Viper, new Cell(2, 0));
        g.PutFoe(FoeKind.RedBall, new Cell(5, 4));
        g.PutFoe(FoeKind.Gremlin, new Cell(3, 3));
        g.Step(default);
        Shot(w, c, "viper");

        // Caught: the curse.
        g.PutHero(viper.Cell.Step(Hop.DownRight));
        for (int i = 0; i < 60 * 3 && g.Phase == Phase.Playing; i++) g.Step(default);
        Assert.Equal(Phase.Caught, g.Phase);
        g.Idle(10);
        Shot(w, c, "caught");
        w.Close();
    }

    [AvaloniaFact]
    public void Game_over_and_the_high_scores()
    {
        var (w, c, g) = Open(seed: 4);
        g.StartGame();
        // Hop straight off the edge, again and again.
        for (int i = 0; i < 60 * 60 && g.State == GameState.Playing; i++) g.Step(new Controls(Hop.UpLeft));
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(20);
        Shot(w, c, "game-over");
        g.Idle(60 * 4);
        if (g.State == GameState.EnterInitials) { g.Initials!.Finish(); g.Idle(5); }
        Assert.Equal(GameState.HighScores, g.State);
        g.Idle(20);
        Shot(w, c, "high-scores");
        w.Close();
    }
}
