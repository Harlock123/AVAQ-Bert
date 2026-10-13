using AVAQBert.Engine;
using AVAQBert.Models;

namespace AVAQBert.Tests;

public sealed class GameplayTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "qbert-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    /// <summary>A round under way with nothing arriving.</summary>
    private GameEngine Calm(int level = 1, int round = 1, int seed = 1)
    {
        var g = new GameEngine(seed, new HighScoreTable(_dir));
        g.StartGame();
        g.JumpTo(level, round);
        g.Calm();
        return g;
    }

    private static void Steps(GameEngine g, int n, Controls c = default)
    {
        for (int i = 0; i < n; i++) g.Step(c);
    }

    /// <summary>One hop, and let it land.</summary>
    private static void HopOnce(GameEngine g, Hop hop)
    {
        g.Step(new Controls(hop));
        Steps(g, (int)(60 * GameEngine.HopSeconds) + 2);
    }

    [Fact]
    public void The_pyramid_has_28_cubes_and_hops_go_diagonally()
    {
        Assert.Equal(28, Field.Cubes().Count());
        var c = new Cell(3, 1);
        Assert.Equal(new Cell(2, 0), c.Step(Hop.UpLeft));
        Assert.Equal(new Cell(2, 1), c.Step(Hop.UpRight));
        Assert.Equal(new Cell(4, 1), c.Step(Hop.DownLeft));
        Assert.Equal(new Cell(4, 2), c.Step(Hop.DownRight));
        Assert.False(new Cell(0, 0).Step(Hop.UpLeft).OnPyramid);
        Assert.False(new Cell(6, 3).Step(Hop.DownLeft).OnPyramid);
        // Neighbours sit half a cube across and a row down.
        Assert.Equal(16, Field.Top(c.Step(Hop.DownRight)).X - Field.Top(c).X);
        Assert.Equal(24, Field.Top(c.Step(Hop.DownRight)).Y - Field.Top(c).Y);
    }

    [Theory]
    [InlineData(1, 1, false)]
    [InlineData(2, 2, false)]
    [InlineData(3, 1, true)]
    [InlineData(4, 2, true)]
    public void Each_level_has_its_own_colour_rule(int level, int goal, bool reverts)
    {
        var p = new Pyramid { Rule = Pyramid.RuleFor(level) };
        Assert.Equal(goal, p.Goal);
        Assert.Equal(reverts, p.Reverts);
        var c = new Cell(2, 1);
        for (int i = 0; i < goal; i++) Assert.Equal(1, p.Land(c));
        Assert.True(p.Finished(c));
        Assert.Equal(reverts ? -1 : 0, p.Land(c));
        Assert.Equal(!reverts, p.Finished(c));
        Assert.Equal(4, Pyramid.RuleFor(9));
    }

    [Fact]
    public void Hopping_onto_a_cube_turns_it_for_25()
    {
        var g = Calm();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        HopOnce(g, Hop.DownRight);
        Assert.Equal(new Cell(1, 1), g.Hero.Cell);
        Assert.True(g.Pyramid.Finished(new Cell(1, 1)));
        Assert.Equal(25, g.Score);
        Assert.Contains(Sfx.Turn, heard);
        HopOnce(g, Hop.UpLeft);
        Assert.Equal(50, g.Score); // the top cube wasn't changed by starting on it
        HopOnce(g, Hop.DownRight);
        Assert.Equal(50, g.Score); // nothing for a cube already done
    }

    [Fact]
    public void Hopping_off_the_edge_with_no_disc_costs_a_life()
    {
        var g = Calm();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.PutHero(new Cell(6, 3));
        HopOnce(g, Hop.DownLeft);
        Assert.Equal(Phase.Fell, g.Phase);
        Assert.Contains(Sfx.Fall, heard);
        Steps(g, (int)(60 * (GameEngine.FellSeconds + GameEngine.ReadySeconds)) + 4);
        Assert.Equal(3, g.Lives);
        Assert.Equal(new Cell(0, 0), g.Hero.Cell);
    }

    [Fact]
    public void A_disc_carries_the_hero_to_the_top_and_the_viper_follows_off_the_edge()
    {
        var g = Calm();
        var disc = g.Discs.First(d => d.Spot.Col < 0);
        var from = new Cell(disc.Spot.Row + 1, 0);
        g.PutHero(from);
        var viper = g.PutFoe(FoeKind.Viper, from.Step(Hop.DownRight));
        HopOnce(g, Hop.UpLeft);
        Assert.Same(disc, g.Hero.Riding);
        for (int i = 0; i < 60 * 4 && g.Hero.Riding is not null; i++) g.Step(default);
        Assert.Null(g.Hero.Riding);
        Assert.True(disc.Used);
        Steps(g, 30);
        Assert.Equal(new Cell(0, 0), g.Hero.Cell);
        Assert.Equal(Phase.Playing, g.Phase);
        Assert.True(viper.Falling || viper.Dead, "lured off after the hero");
        Assert.True(g.Score >= 500);
    }

    [Fact]
    public void A_red_ball_bounces_down_and_off_and_kills_on_the_way()
    {
        var g = Calm();
        g.PutHero(new Cell(0, 0));
        var ball = g.PutFoe(FoeKind.RedBall, new Cell(4, 2));
        var g2 = Calm();
        for (int i = 0; i < 60 * 10 && !ball.Dead; i++) { g.Step(default); Assert.True(ball.Cell.Row >= 4); }
        Assert.True(ball.Dead, "fell off the bottom");
        Assert.Equal(Phase.Playing, g.Phase);

        g2.PutHero(new Cell(5, 2));
        g2.PutFoe(FoeKind.RedBall, new Cell(5, 2));
        g2.Step(default);
        Assert.Equal(Phase.Caught, g2.Phase);
        Assert.Contains(g2.Effects, e => e.Kind == EffectKind.Curse && e.Text == "@!#?@!");
        Steps(g2, (int)(60 * (GameEngine.CaughtSeconds + GameEngine.ReadySeconds)) + 4);
        Assert.Equal(3, g2.Lives);
        Assert.Empty(g2.Foes);
        Assert.Equal(new Cell(5, 2), g2.Hero.Cell); // carries on from where it was
    }

    [Fact]
    public void The_purple_ball_hatches_into_a_viper_that_hunts()
    {
        var g = Calm();
        var ball = g.PutFoe(FoeKind.PurpleBall, new Cell(5, 2));
        g.PutHero(new Cell(1, 0));
        for (int i = 0; i < 60 * 6 && ball.Kind != FoeKind.Viper; i++) { g.Hero.Cell = new Cell(1, 0); g.Step(default); }
        Assert.Equal(FoeKind.Viper, ball.Kind);
        Assert.Equal(Field.Rows - 1, ball.Cell.Row);
        int row = ball.Cell.Row;
        for (int i = 0; i < 60 * 3; i++) { g.Hero.Cell = new Cell(1, 0); g.Step(default); }
        Assert.True(ball.Cell.Row < row, "climbing towards the hero");
    }

    [Fact]
    public void The_green_ball_freezes_everything_and_the_gremlin_is_worth_catching()
    {
        var g = Calm();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        var viper = g.PutFoe(FoeKind.Viper, new Cell(6, 0));
        g.PutFoe(FoeKind.GreenBall, new Cell(1, 1));
        HopOnce(g, Hop.DownRight);
        Assert.True(g.FreezeLeft > 2);
        Assert.Equal(25 + 100, g.Score);
        Assert.Contains(Sfx.Freeze, heard);
        var at = viper.Cell;
        Steps(g, 60 * 2);
        Assert.Equal(at, viper.Cell);

        g = Calm();
        g.Paint(new Cell(3, 1), 1);
        g.Hold();
        var gremlin = g.PutFoe(FoeKind.Gremlin, new Cell(2, 0));
        g.PutHero(new Cell(1, 0));
        HopOnce(g, Hop.DownLeft);
        Assert.True(gremlin.Dead);
        Assert.Equal(25 + 300, g.Score);
    }

    [Fact]
    public void The_gremlin_turns_cubes_back()
    {
        var g = new GameEngine(1, new HighScoreTable(_dir));
        g.StartGame();
        g.JumpTo(1, 2);
        foreach (var c in Field.Cubes()) g.Paint(c, 1);
        g.PutHero(new Cell(0, 0));
        var gremlin = g.PutFoe(FoeKind.Gremlin, new Cell(1, 0));
        int before = g.Pyramid.FinishedCount;
        for (int i = 0; i < 60 * 3 && !gremlin.Dead; i++) { g.Hero.Cell = new Cell(0, 0); g.Step(default); }
        Assert.True(g.Pyramid.FinishedCount < before);
    }

    [Fact]
    public void Finishing_the_pyramid_pays_a_bonus_and_four_rounds_make_a_level()
    {
        var g = Calm();
        foreach (var c in Field.Cubes()) g.Paint(c, 1);
        g.Paint(new Cell(1, 1), 0);
        HopOnce(g, Hop.DownRight);
        Assert.Equal(Phase.RoundClear, g.Phase);
        Assert.Equal(25 + 1000 + 2 * 50, g.Score);
        Steps(g, (int)(60 * GameEngine.RoundClearSeconds) + 2);
        Assert.Equal(2, g.Round);
        Assert.Equal(0, g.Pyramid.FinishedCount);

        g = Calm(level: 1, round: 4);
        foreach (var c in Field.Cubes()) g.Paint(c, 1);
        g.Paint(new Cell(1, 1), 0);
        HopOnce(g, Hop.DownRight);
        Steps(g, (int)(60 * GameEngine.RoundClearSeconds) + 2);
        Assert.Equal(2, g.Level);
        Assert.Equal(1, g.Round);
        Assert.Equal(2, g.Pyramid.Goal);
    }

    [Fact]
    public void Bonus_lives_at_8000_and_every_14000_after()
    {
        var g = Calm();
        Assert.Equal(4, g.Lives);
        g.AddScore(7999);
        Assert.Equal(4, g.Lives);
        g.AddScore(1);
        Assert.Equal(5, g.Lives);
        g.AddScore(14000);
        Assert.Equal(6, g.Lives);
    }

    [Fact]
    public void The_demonstration_pilot_clears_rounds_and_the_game_ends_in_the_table()
    {
        var g = new GameEngine(1, new HighScoreTable(_dir));
        g.StartGame();
        var pilot = new AttractPilot(1);
        for (int i = 0; i < 60 * 60 * 3 && g.State == GameState.Playing && g.Level * 10 + g.Round < 13; i++) g.Step(pilot.Decide(g));
        Assert.True(g.Level * 10 + g.Round >= 13, $"reached {g.Level}-{g.Round}; lost: {string.Join("; ", g.LossLog)}");
        for (int i = 0; i < 60 * 60 * 10 && g.State == GameState.Playing; i++) g.Step(new Controls(Hop.UpLeft));
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(60 * 4);
        Assert.Equal(GameState.EnterInitials, g.State);
        g.Initials!.Finish();
        g.Idle(2);
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal(g.Score, HighScoreTable.Load(_dir).TopScore);
    }

    [Fact]
    public void The_same_seed_plays_the_same_game()
    {
        string Run()
        {
            var g = new GameEngine(21, new HighScoreTable(_dir));
            g.StartGame();
            var pilot = new AttractPilot(5);
            for (int i = 0; i < 60 * 60; i++) g.Step(pilot.Decide(g));
            return $"{g.Level} {g.Round} {g.Score} {g.Lives} {g.Hero.Cell} {g.Pyramid.FinishedCount} {g.Foes.Count}";
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void The_title_plays_its_tune_and_the_demonstration_has_sound()
    {
        var g = new GameEngine(5, new HighScoreTable(_dir));
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Idle(1);
        Assert.Equal([Sfx.TitleTune], heard);
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + 10)));
        Assert.Equal(GameState.Attract, g.State);
        Assert.Contains(Sfx.StartTune, heard);
        Assert.Contains(Sfx.Hop, heard);
        Assert.Contains(Sfx.Turn, heard);
        g.Idle((int)(60 * (GameEngine.AttractSeconds - 8)));
        Assert.Equal(GameState.HighScores, g.State);
        g.Idle((int)(60 * (GameEngine.HighScoreSeconds + 1)));
        Assert.Equal(GameState.Title, g.State);
    }

    [Fact]
    public void Pausing_holds_everything()
    {
        var g = Calm();
        g.Pause();
        Steps(g, 30, new Controls(Hop.DownRight));
        Assert.Equal(new Cell(0, 0), g.Hero.Cell);
        g.Resume();
        HopOnce(g, Hop.DownRight);
        Assert.Equal(new Cell(1, 1), g.Hero.Cell);
    }

    [Fact]
    public void Every_sound_is_there()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        var files = Enum.GetValues<Sfx>().Select(s => s.ToString());
        Assert.All(files, f => Assert.True(File.Exists(Path.Combine(folder, f + ".wav")), f));
    }

    [Fact]
    public void Every_sprite_is_well_formed()
    {
        foreach (var s in new[] { Sprites.HeroStand, Sprites.HeroHop, Sprites.HeroBack, Sprites.RedBall, Sprites.Viper[0], Sprites.Gremlin, Sprites.Disc[0] })
            Assert.All(s.Rows.SelectMany(r => r).Where(ch => ch != '.'), ch => Assert.True(Sprites.Colours.ContainsKey(ch), $"colour {ch}"));
        Assert.Equal(16, Sprites.HeroStand.Width);
        Assert.Equal(5, Colours.Rounds.Length);
    }
}
