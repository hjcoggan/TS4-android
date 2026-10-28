namespace TopSpin4.Launcher.Core;

/// <summary>
/// Names for the keys and pad buttons a player presses to remap, in the form the game's Controls
/// section stores. Pad inputs are the game's input names (<c>kInputs</c> in src/pad_bindings.h);
/// keys are the names the game reads back (<c>kKeyNames</c> in the SDK) and are the same set its
/// in-game page offers (<c>PressedKeyBinding</c> in src/settings/settings_page.cpp). Keys come in
/// as Avalonia's key names, so this has no UI types.
/// </summary>
public static class BindingNames
{
    /// <summary>A trigger counts as a press above this, as the game's and XInput's threshold.</summary>
    public const int TriggerThreshold = 30;

    private static readonly (ushort Button, string Name)[] PadButtons =
    [
        (PadState.A, "a"), (PadState.B, "b"), (0x4000, "x"), (0x8000, "y"),
        (0x0100, "lb"), (0x0200, "rb"), (0x0040, "ls"), (0x0080, "rs"),
        (0x0020, "back"), (0x0010, "start"),
        (PadState.DPadUp, "up"), (PadState.DPadDown, "down"), (PadState.DPadLeft, "left"), (PadState.DPadRight, "right"),
    ];

    /// <summary>The input a pad reading presses (a button, a D-pad direction or a trigger), or null; sticks are not inputs.</summary>
    public static string? PadInput(PadState state)
    {
        foreach (var (button, name) in PadButtons)
            if ((state.Buttons & button) != 0) return name;
        if (state.LeftTrigger >= TriggerThreshold) return "lt";
        if (state.RightTrigger >= TriggerThreshold) return "rt";
        return null;
    }

    // Avalonia key names (an alias of a key has two) to the game's. F4 opens the in-game page and Escape
    // cancels a capture, so neither can be bound; the rest are the keys the game's own page offers.
    private static readonly Dictionary<string, string> KeyNames = BuildKeyNames();

    private static Dictionary<string, string> BuildKeyNames()
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var letter = 'A'; letter <= 'Z'; letter++) names[letter.ToString()] = letter.ToString();
        for (var digit = 0; digit < 10; digit++) names["D" + digit] = digit.ToString();
        for (var function = 1; function <= 12; function++)
            if (function != 4) names["F" + function] = "F" + function;
        foreach (var (key, name) in new[]
        {
            ("Left", "Left"), ("Up", "Up"), ("Right", "Right"), ("Down", "Down"),
            ("Space", "Space"), ("Return", "Return"), ("Enter", "Return"), ("Tab", "Tab"), ("Back", "Backspace"),
            ("Insert", "Insert"), ("Delete", "Delete"), ("Home", "Home"), ("End", "End"),
            ("PageUp", "PageUp"), ("Prior", "PageUp"), ("PageDown", "PageDown"), ("Next", "PageDown"),
            ("OemSemicolon", "Semicolon"), ("Oem1", "Semicolon"), ("OemQuotes", "Quote"), ("Oem7", "Quote"),
            ("OemComma", "Comma"), ("OemPeriod", "Period"), ("OemMinus", "Minus"), ("OemPlus", "Plus"),
            ("OemQuestion", "Slash"), ("Oem2", "Slash"), ("OemPipe", "Backslash"), ("Oem5", "Backslash"),
            ("OemOpenBrackets", "LBracket"), ("Oem4", "LBracket"), ("OemCloseBrackets", "RBracket"), ("Oem6", "RBracket"),
            ("OemTilde", "Backtick"), ("Oem3", "Backtick"),
        })
            names[key] = name;
        return names;
    }

    /// <summary>
    /// The game's name for a key with the modifiers held ("Shift+W"), or null for a key it does not
    /// offer (a modifier alone, F4, Escape, a numpad key).
    /// </summary>
    public static string? Key(string avaloniaKey, bool ctrl, bool alt, bool shift) =>
        KeyNames.TryGetValue(avaloniaKey, out var name) ? (ctrl ? "Ctrl+" : "") + (alt ? "Alt+" : "") + (shift ? "Shift+" : "") + name : null;
}
