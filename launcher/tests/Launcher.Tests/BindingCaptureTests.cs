using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

/// <summary>
/// Waiting for the next key or pad button after a binding row is chosen: what is captured, what is
/// ignored, and how it ends (Esc or the timeout cancels). Names are the game's (src/pad_bindings.h
/// input names; rex::ui key names, as the in-game page's PressedKeyBinding offers them).
/// </summary>
public sealed class BindingCaptureTests
{
    private static SettingRow BindingRow(string device) =>
        new("binding", SettingRowKind.Setting, device == "pad" ? "Pad: A" : "Keys: A", "", "a", "A", [],
            Available: true, Editable: false, Remappable: true, Note: null, new BindingDescription(device, "A"));

    private static PadState Pad(ushort buttons = 0, byte left = 0, byte right = 0) => new(buttons, 0, 0, left, right);

    [Theory]
    [InlineData(PadState.A, "a")]
    [InlineData(PadState.B, "b")]
    [InlineData(0x4000, "x")]
    [InlineData(0x8000, "y")]
    [InlineData(0x0100, "lb")]
    [InlineData(0x0200, "rb")]
    [InlineData(0x0040, "ls")]
    [InlineData(0x0080, "rs")]
    [InlineData(0x0020, "back")]
    [InlineData(0x0010, "start")]
    [InlineData(PadState.DPadUp, "up")]
    [InlineData(PadState.DPadDown, "down")]
    [InlineData(PadState.DPadLeft, "left")]
    [InlineData(PadState.DPadRight, "right")]
    public void A_pad_button_is_named_as_the_game_names_its_inputs(int buttons, string name) =>
        Assert.Equal(name, BindingNames.PadInput(Pad((ushort)buttons)));

    [Fact]
    public void A_trigger_counts_from_the_games_threshold_and_a_stick_is_not_an_input()
    {
        Assert.Equal("lt", BindingNames.PadInput(Pad(left: 30)));
        Assert.Equal("rt", BindingNames.PadInput(Pad(right: 200)));
        Assert.Null(BindingNames.PadInput(Pad(left: 29, right: 29)));
        Assert.Null(BindingNames.PadInput(new PadState(0, 30000, -30000)));
    }

    [Theory]
    [InlineData("W", "W")]
    [InlineData("D1", "1")]
    [InlineData("D0", "0")]
    [InlineData("F5", "F5")]
    [InlineData("Space", "Space")]
    [InlineData("Return", "Return")]
    [InlineData("Enter", "Return")]
    [InlineData("Tab", "Tab")]
    [InlineData("Back", "Backspace")]
    [InlineData("Delete", "Delete")]
    [InlineData("Left", "Left")]
    [InlineData("PageUp", "PageUp")]
    [InlineData("Prior", "PageUp")]
    [InlineData("PageDown", "PageDown")]
    [InlineData("Next", "PageDown")]
    [InlineData("OemSemicolon", "Semicolon")]
    [InlineData("Oem1", "Semicolon")]
    [InlineData("OemQuotes", "Quote")]
    [InlineData("OemComma", "Comma")]
    [InlineData("OemPeriod", "Period")]
    [InlineData("OemMinus", "Minus")]
    [InlineData("OemPlus", "Plus")]
    [InlineData("OemQuestion", "Slash")]
    [InlineData("OemPipe", "Backslash")]
    [InlineData("OemOpenBrackets", "LBracket")]
    [InlineData("OemCloseBrackets", "RBracket")]
    [InlineData("OemTilde", "Backtick")]
    public void A_key_is_named_as_the_game_reads_it_back(string key, string name) =>
        Assert.Equal(name, BindingNames.Key(key, ctrl: false, alt: false, shift: false));

    [Fact]
    public void Modifiers_held_come_first_in_the_games_order()
    {
        Assert.Equal("Shift+W", BindingNames.Key("W", false, false, true));
        Assert.Equal("Ctrl+Alt+Shift+Up", BindingNames.Key("Up", true, true, true));
    }

    [Theory]
    [InlineData("F4")]            // opens the in-game page, and the game does not offer it
    [InlineData("Escape")]        // cancels
    [InlineData("LeftShift")]
    [InlineData("RightCtrl")]
    [InlineData("LWin")]
    [InlineData("NumPad1")]
    [InlineData("Oem8")]
    [InlineData("MediaPlayPause")]
    public void A_key_the_game_does_not_offer_has_no_name(string key) =>
        Assert.Null(BindingNames.Key(key, false, false, false));

    [Fact]
    public void Nothing_is_waiting_until_a_binding_row_is_chosen_and_other_rows_do_not_start_a_capture()
    {
        var capture = new BindingCapture();
        Assert.False(capture.Waiting);

        capture.Begin(BindingRow("keyboard") with { Binding = null }, nowMs: 0);
        Assert.False(capture.Waiting);

        capture.Begin(BindingRow("keyboard") with { Remappable = false }, nowMs: 0);
        Assert.False(capture.Waiting);
    }

    [Fact]
    public void The_next_key_pressed_is_captured_with_the_modifiers_held()
    {
        var capture = new BindingCapture();
        var row = BindingRow("keyboard");
        capture.Begin(row, nowMs: 0);

        Assert.True(capture.Waiting);
        Assert.Null(capture.KeyDown("LeftShift", false, false, true));   // a modifier alone is not the key
        var captured = capture.KeyDown("W", false, false, true);

        Assert.Equal(new CapturedBinding(row, "Shift+W"), captured);
        Assert.False(capture.Waiting);
    }

    [Fact]
    public void A_pad_button_pressed_after_the_buttons_that_started_the_capture_are_let_go_is_captured()
    {
        var capture = new BindingCapture();
        var row = BindingRow("pad");
        capture.Begin(row, nowMs: 0);

        Assert.Null(capture.PadChanged(Pad(PadState.A)));   // A is still down from choosing the row
        Assert.Null(capture.PadChanged(Pad(PadState.A, right: 255)));
        Assert.Null(capture.PadChanged(Pad()));
        Assert.True(capture.Waiting);
        Assert.Null(capture.PadChanged(new PadState(0, 30000, 0)));   // a stick is not a button
        var captured = capture.PadChanged(Pad(left: 255));

        Assert.Equal(new CapturedBinding(row, "lt"), captured);
        Assert.False(capture.Waiting);
    }

    [Fact]
    public void A_key_row_ignores_the_pad_and_a_pad_row_ignores_keys()
    {
        var capture = new BindingCapture();
        capture.Begin(BindingRow("keyboard"), nowMs: 0);
        Assert.Null(capture.PadChanged(Pad()));
        Assert.Null(capture.PadChanged(Pad(PadState.B)));
        Assert.True(capture.Waiting);
        capture.Cancel();

        capture.Begin(BindingRow("pad"), nowMs: 0);
        Assert.Null(capture.KeyDown("W", false, false, false));
        Assert.True(capture.Waiting);
    }

    [Fact]
    public void Esc_cancels_on_either_device_and_nothing_is_captured()
    {
        foreach (var device in new[] { "keyboard", "pad" })
        {
            var capture = new BindingCapture();
            capture.Begin(BindingRow(device), nowMs: 0);

            Assert.Null(capture.KeyDown("Escape", false, false, false));

            Assert.False(capture.Waiting);
        }
    }

    [Fact]
    public void A_key_the_game_does_not_offer_is_ignored_and_the_capture_keeps_waiting()
    {
        var capture = new BindingCapture();
        capture.Begin(BindingRow("keyboard"), nowMs: 0);

        Assert.Null(capture.KeyDown("F4", false, false, false));
        Assert.Null(capture.KeyDown("LWin", false, false, false));

        Assert.True(capture.Waiting);
    }

    [Fact]
    public void A_key_still_held_from_choosing_the_row_is_not_captured_when_it_repeats_but_is_once_let_go()
    {
        var capture = new BindingCapture();
        Assert.Null(capture.KeyDown("Return", false, false, false));   // Enter chose the row; the capture is not on yet
        capture.Begin(BindingRow("keyboard"), nowMs: 0);

        Assert.Null(capture.KeyDown("Return", false, false, false));   // auto-repeat
        Assert.True(capture.Waiting);
        capture.KeyUp("Return");
        var captured = capture.KeyDown("Return", false, false, false);

        Assert.Equal("Return", captured?.Value);
    }

    [Fact]
    public void The_capture_gives_up_after_the_timeout_and_captures_nothing_late()
    {
        var capture = new BindingCapture();
        capture.Begin(BindingRow("keyboard"), nowMs: 1000);

        Assert.False(capture.Expire(1000 + BindingCapture.TimeoutMs - 1));
        Assert.True(capture.Waiting);
        Assert.True(capture.Expire(1000 + BindingCapture.TimeoutMs));
        Assert.False(capture.Waiting);
        Assert.Null(capture.KeyDown("W", false, false, false));
        Assert.False(capture.Expire(1000 + BindingCapture.TimeoutMs + 1));   // told once
    }

    [Fact]
    public void The_prompt_names_what_to_press_and_how_to_cancel()
    {
        var capture = new BindingCapture();
        Assert.Equal("", capture.Prompt);

        capture.Begin(BindingRow("pad"), nowMs: 0);
        Assert.Contains("pad button", capture.Prompt);
        Assert.Contains("Esc", capture.Prompt);

        capture.Begin(BindingRow("keyboard"), nowMs: 0);
        Assert.Contains("key", capture.Prompt);
        Assert.Contains("Esc", capture.Prompt);
    }
}
