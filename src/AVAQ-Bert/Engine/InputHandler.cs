using Avalonia.Input;
using AVAQBert.Models;

namespace AVAQBert.Engine;

/// <summary>
/// Keyboard and gamepad boiled down to the controls: a diagonal hop. The arrow keys are turned a
/// quarter round, like the arcade's diagonal stick (up hops up-right, right down-right, down down-left,
/// left up-left); Q, E, Z and C (and 7, 9, 1 and 3 on the number pad) give the four corners directly.
/// The latest key held wins.
/// </summary>
public sealed class InputHandler : IDisposable
{
    private readonly List<Key> _held = [];
    private readonly ControllerManager _pad = new(0);

    public void OnKeyDown(Key key)
    {
        _held.Remove(key);
        _held.Add(key);
    }

    public void OnKeyUp(Key key) => _held.Remove(key);
    public bool Has(Key key) => _held.Contains(key);

    public void Update() => _pad.Update();

    private static Hop? HopOf(Key k) => k switch
    {
        Key.Up or Key.W or Key.E or Key.NumPad9 => Hop.UpRight,
        Key.Right or Key.D or Key.C or Key.NumPad3 => Hop.DownRight,
        Key.Down or Key.S or Key.Z or Key.NumPad1 => Hop.DownLeft,
        Key.Left or Key.A or Key.Q or Key.NumPad7 => Hop.UpLeft,
        _ => null,
    };

    public Controls Controls
    {
        get
        {
            for (int i = _held.Count - 1; i >= 0; i--)
                if (HopOf(_held[i]) is { } h) return new Controls(h);
            // A stick pushed into a corner hops that way; the D-pad is turned like the arrow keys.
            float x = _pad.LeftStickX, y = _pad.LeftStickY;
            if (Math.Abs(x) > 0.35f && Math.Abs(y) > 0.35f)
                return new Controls(y < 0 ? (x < 0 ? Hop.UpLeft : Hop.UpRight) : (x < 0 ? Hop.DownLeft : Hop.DownRight));
            if (_pad.DPadUp) return new Controls(Hop.UpRight);
            if (_pad.DPadRight) return new Controls(Hop.DownRight);
            if (_pad.DPadDown) return new Controls(Hop.DownLeft);
            if (_pad.DPadLeft) return new Controls(Hop.UpLeft);
            return default;
        }
    }

    public bool Start => Has(Key.Enter) || Has(Key.Space) || Has(Key.D1) || _pad.ButtonStart || _pad.ButtonA;

    public bool PadUp => _pad.DPadUp || _pad.LeftStickY < -0.5f;
    public bool PadDown => _pad.DPadDown || _pad.LeftStickY > 0.5f;
    public bool PadLeft => _pad.DPadLeft || _pad.LeftStickX < -0.5f;
    public bool PadRight => _pad.DPadRight || _pad.LeftStickX > 0.5f;
    public bool PadAccept => _pad.ButtonA || _pad.ButtonX || _pad.ButtonB;
    public bool PadDone => _pad.ButtonStart;
    public bool PadPause => _pad.ButtonStart;
    public bool PadBack => _pad.ButtonBack;

    public void Dispose() => _pad.Dispose();
}
