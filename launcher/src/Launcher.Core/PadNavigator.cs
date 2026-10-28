namespace TopSpin4.Launcher.Core;

/// <summary>What a pad press does in the Launcher: move the focus, press the focused control, or go back.</summary>
public enum PadAction
{
    None,
    Up,
    Down,
    Left,
    Right,
    Accept,
    Back,
}

/// <summary>One reading of the first pad, in XInput's layout.</summary>
public readonly record struct PadState(ushort Buttons, short LeftX, short LeftY)
{
    public const ushort DPadUp = 0x0001, DPadDown = 0x0002, DPadLeft = 0x0004, DPadRight = 0x0008;
    public const ushort A = 0x1000, B = 0x2000;
}

/// <summary>
/// Turns pad readings into menu actions: D-pad or left stick move, A accepts, B goes back. A held
/// direction repeats after a pause, like a console menu. Avalonia reads no pad itself, so the
/// window polls the pad and feeds this; it has no UI types, so it is tested directly.
/// </summary>
public sealed class PadNavigator
{
    public const int FirstRepeatMs = 400;
    public const int RepeatMs = 120;
    private const int StickThreshold = 16000;

    private PadAction _held;
    private long _repeatAt;

    /// <summary>The action to take for this reading, or None. Call it every poll.</summary>
    public PadAction Update(PadState state, long nowMs)
    {
        var held = Held(state);
        if (held == PadAction.None)
        {
            _held = PadAction.None;
            return PadAction.None;
        }
        if (held != _held)
        {
            _held = held;
            _repeatAt = nowMs + FirstRepeatMs;
            return held;
        }
        if (held is PadAction.Accept or PadAction.Back || nowMs < _repeatAt) return PadAction.None;
        _repeatAt = nowMs + RepeatMs;
        return held;
    }

    private static PadAction Held(PadState state)
    {
        var buttons = state.Buttons;
        if ((buttons & PadState.DPadUp) != 0 || state.LeftY > StickThreshold) return PadAction.Up;
        if ((buttons & PadState.DPadDown) != 0 || state.LeftY < -StickThreshold) return PadAction.Down;
        if ((buttons & PadState.DPadLeft) != 0 || state.LeftX < -StickThreshold) return PadAction.Left;
        if ((buttons & PadState.DPadRight) != 0 || state.LeftX > StickThreshold) return PadAction.Right;
        if ((buttons & PadState.A) != 0) return PadAction.Accept;
        if ((buttons & PadState.B) != 0) return PadAction.Back;
        return PadAction.None;
    }
}
