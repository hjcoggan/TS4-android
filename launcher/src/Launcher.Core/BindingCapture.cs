namespace TopSpin4.Launcher.Core;

/// <summary>A binding row and what the player pressed for it: a pad input name ("lt") or a key ("Shift+W").</summary>
public sealed record CapturedBinding(SettingRow Row, string Value);

/// <summary>
/// Waits for the next key or pad button after the player chooses a binding row. A pad row takes a
/// pad button, a key row a key; Esc or the timeout cancels. What was already down when the row was
/// chosen (the A button, the Enter key) is not taken for the answer: a pad waits until it is let go,
/// a key until its key-up. The window feeds every key and pad reading, whether or not it is waiting,
/// so what is held is known. No UI types, so it is tested directly.
/// </summary>
public sealed class BindingCapture
{
    /// <summary>How long a capture waits before it gives up.</summary>
    public const int TimeoutMs = 8000;

    private readonly HashSet<string> _down = [];
    private readonly HashSet<string> _held = [];
    private bool _padReleased;
    private long _deadline;

    /// <summary>The binding row waiting for a press, or null.</summary>
    public SettingRow? Row { get; private set; }

    public bool Waiting => Row is not null;

    /// <summary>What to tell the player while waiting; empty when not.</summary>
    public string Prompt => Row?.Binding?.Device switch
    {
        BindingDevice.Pad => "Press the pad button to use. Esc cancels.",
        BindingDevice.Keyboard => "Press the key to use. Esc cancels.",
        _ => "",
    };

    /// <summary>Starts waiting for a row that can be remapped; any other row does nothing.</summary>
    public void Begin(SettingRow row, long nowMs)
    {
        if (row.Binding is null || !row.Remappable) return;
        Row = row;
        _deadline = nowMs + TimeoutMs;
        _padReleased = false;
        _held.Clear();
        _held.UnionWith(_down);
    }

    public void Cancel() => Row = null;

    /// <summary>Gives up once the timeout has passed; true the one time that happens.</summary>
    public bool Expire(long nowMs)
    {
        if (Row is null || nowMs < _deadline) return false;
        Row = null;
        return true;
    }

    /// <summary>A key went down (Avalonia's key name). The row and key when it is the answer, else null.</summary>
    public CapturedBinding? KeyDown(string key, bool ctrl, bool alt, bool shift)
    {
        _down.Add(key);
        if (Row is not { } row) return null;
        if (key == "Escape")
        {
            Cancel();
            return null;
        }
        if (row.Binding?.Device != BindingDevice.Keyboard || _held.Contains(key)) return null;
        if (BindingNames.Key(key, ctrl, alt, shift) is not { } name) return null;
        Row = null;
        return new CapturedBinding(row, name);
    }

    public void KeyUp(string key)
    {
        _down.Remove(key);
        _held.Remove(key);
    }

    /// <summary>The pad was read. The row and input when a button or trigger is the answer, else null.</summary>
    public CapturedBinding? PadChanged(PadState state)
    {
        if (Row is not { } row || row.Binding?.Device != BindingDevice.Pad) return null;
        var pressed = BindingNames.PadInput(state);
        if (pressed is null)
        {
            _padReleased = true;
            return null;
        }
        if (!_padReleased) return null;
        Row = null;
        return new CapturedBinding(row, pressed);
    }
}
