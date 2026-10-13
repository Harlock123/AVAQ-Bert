using AVAQBert.Models;

namespace AVAQBert.Engine;

public enum GameState { Title, Attract, Playing, Paused, GameOver, EnterInitials, HighScores }

/// <summary>What is happening within a game.</summary>
public enum Phase
{
    /// <summary>A moment before the hopping starts.</summary>
    Ready,
    Playing,
    /// <summary>Caught by something: the hero curses and everything clears away.</summary>
    Caught,
    /// <summary>Jumped off the pyramid with no disc there.</summary>
    Fell,
    /// <summary>Every cube done: the pyramid flashes.</summary>
    RoundClear,
}

/// <summary>Sounds the engine asks for; the host decides how they sound.</summary>
public enum Sfx
{
    Hop, Turn, FoeHop, Disc, Fall, Curse, ViperFall, Freeze, Catch, Hatch, Unused, RoundClear, ExtraLife,
    TitleTune, StartTune, GameOver,
}

/// <summary>
/// The whole game with no UI. Call <see cref="Update"/> with the elapsed time and the controls; it
/// runs in fixed 60 Hz steps, so it plays the same at any frame rate and a seed always plays the
/// same game.
/// </summary>
public sealed partial class GameEngine
{
    public const int StartingLives = 4;
    public const int FirstExtraLife = 8000;
    public const int ExtraLifeEvery = 14000;
    public const double TitleIdleSeconds = 15;
    public const double AttractSeconds = 45;
    public const double HighScoreSeconds = 9;

    private readonly Random _rng;
    private double _accumulator;
    private bool _startPrev;
    private bool _titleTunePending = true;
    private int _nextExtraLife;

    public GameEngine(int seed = 1982, HighScoreTable? table = null)
    {
        _rng = new Random(seed);
        HighScores = table ?? HighScoreTable.Load();
        Pyramid = new Pyramid { Rule = 1 };
        SetUpRound();
    }

    public event Action<Sfx>? Sound;

    public GameState State { get; private set; } = GameState.Title;
    public Phase Phase { get; private set; } = Phase.Ready;
    public double StateTime { get; private set; }
    public double PhaseTime { get; private set; }
    public HighScoreTable HighScores { get; }
    public InitialsEntry? Initials { get; private set; }
    public int NewEntryIndex { get; private set; } = -1;
    public AttractPilot? Pilot { get; private set; }
    public bool Silent { get; set; }

    public int Score { get; private set; }
    public int Lives { get; private set; }
    public int Level { get; private set; } = 1;
    /// <summary>Four rounds to a level, 1 to 4.</summary>
    public int Round { get; private set; } = 1;

    public List<Effect> Effects { get; } = [];

    private void Play(Sfx s)
    {
        if (!Silent) Sound?.Invoke(s);
    }

    public void Update(double dt, Controls controls, bool start)
    {
        _accumulator += Math.Min(dt, 0.25);
        while (_accumulator >= Field.Step)
        {
            _accumulator -= Field.Step;
            Step(controls, start);
        }
    }

    /// <summary>One 1/60 s step.</summary>
    public void Step(Controls c, bool start = false)
    {
        double dt = Field.Step;
        StateTime += dt;
        bool startPressed = start && !_startPrev;
        _startPrev = start;
        foreach (var e in Effects) e.Age += dt;
        Effects.RemoveAll(e => e.Done);

        switch (State)
        {
            case GameState.Title:
                if (_titleTunePending) { _titleTunePending = false; Play(Sfx.TitleTune); }
                if (startPressed) StartGame();
                else if (StateTime > TitleIdleSeconds) StartAttract();
                break;
            case GameState.Attract:
                Simulate(dt, Pilot!.Decide(this));
                if (startPressed) { StartGame(); break; }
                if (State == GameState.Attract && StateTime > AttractSeconds) ShowHighScores(-1);
                break;
            case GameState.Playing:
                Simulate(dt, c);
                break;
            case GameState.GameOver:
                if (StateTime > 3)
                {
                    if (HighScores.Qualifies(Score))
                    {
                        State = GameState.EnterInitials;
                        StateTime = 0;
                        Initials = new InitialsEntry(HighScores.LastInitials);
                    }
                    else ShowHighScores(-1);
                }
                break;
            case GameState.EnterInitials:
                if (Initials is { IsDone: true } done)
                {
                    int place = HighScores.Insert(done.Initials, Score, Level * 10 + Round);
                    HighScores.Save();
                    ShowHighScores(place);
                }
                break;
            case GameState.HighScores:
                if (startPressed) StartGame();
                else if (StateTime > HighScoreSeconds) ReturnToTitle();
                break;
        }
    }

    public void StartGame()
    {
        Pilot = null;
        Begin(GameState.Playing);
    }

    private void StartAttract()
    {
        Pilot = new AttractPilot(_rng.Next());
        Begin(GameState.Attract);
    }

    private void Begin(GameState state)
    {
        State = state;
        StateTime = 0;
        Score = 0;
        Lives = StartingLives;
        _nextExtraLife = FirstExtraLife;
        NewEntryIndex = -1;
        Effects.Clear();
        Level = 1;
        StartRound(1);
        Play(Sfx.StartTune);
    }

    public void ReturnToTitle()
    {
        State = GameState.Title;
        StateTime = 0;
        Pilot = null;
        _titleTunePending = true;
        Effects.Clear();
        Level = 1;
        Round = 1;
        Pyramid = new Pyramid { Rule = 1 };
        SetUpRound();
    }

    private void ShowHighScores(int place)
    {
        State = GameState.HighScores;
        StateTime = 0;
        NewEntryIndex = place;
        Pilot = null;
    }

    public void Pause()
    {
        if (State != GameState.Playing) return;
        State = GameState.Paused;
    }

    public void Resume()
    {
        if (State != GameState.Paused) return;
        State = GameState.Playing;
    }

    private void GameOver()
    {
        if (State == GameState.Attract)
        {
            ShowHighScores(-1);
            return;
        }
        Play(Sfx.GameOver);
        State = GameState.GameOver;
        StateTime = 0;
    }

    internal void AddScore(int points)
    {
        Score += points;
        while (Score >= _nextExtraLife)
        {
            _nextExtraLife += ExtraLifeEvery;
            Lives++;
            Play(Sfx.ExtraLife);
        }
    }

    private void Popup(Vec at, int points, double life = 1.2) =>
        Effects.Add(new Effect { Kind = EffectKind.Score, Pos = at, Life = life, Text = points.ToString() });

    private void SetPhase(Phase p)
    {
        Phase = p;
        PhaseTime = 0;
    }
}
