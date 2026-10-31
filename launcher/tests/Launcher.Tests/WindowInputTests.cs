using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

/// <summary>What a pad press or Esc does on each page, and when the pad hints show.</summary>
public sealed class WindowInputTests
{
    [Theory]
    [InlineData(PadAction.Up, InputCommand.FocusUp)]
    [InlineData(PadAction.Down, InputCommand.FocusDown)]
    [InlineData(PadAction.Left, InputCommand.FocusLeft)]
    [InlineData(PadAction.Right, InputCommand.FocusRight)]
    public void Pad_directions_move_focus_in_that_direction_on_every_page(PadAction pad, InputCommand expected)
    {
        foreach (var page in Enum.GetValues<LauncherPage>())
            Assert.Equal(expected, LauncherInput.Route(pad, page, setupRunning: false));
    }

    [Fact]
    public void A_presses_the_focused_control_and_no_press_does_nothing()
    {
        foreach (var page in Enum.GetValues<LauncherPage>())
        {
            Assert.Equal(InputCommand.Press, LauncherInput.Route(PadAction.Accept, page, setupRunning: false));
            Assert.Equal(InputCommand.None, LauncherInput.Route(PadAction.None, page, setupRunning: false));
        }
    }

    [Fact]
    public void B_and_Esc_go_back_from_Settings_and_Achievements()
    {
        Assert.Equal(InputCommand.GoBack, LauncherInput.Route(PadAction.Back, LauncherPage.Settings, setupRunning: false));
        Assert.Equal(InputCommand.GoBack, LauncherInput.Route(PadAction.Back, LauncherPage.Achievements, setupRunning: false));
    }

    [Fact]
    public void B_stops_a_running_setup_and_does_nothing_on_the_setup_rows()
    {
        Assert.Equal(InputCommand.StopSetup, LauncherInput.Route(PadAction.Back, LauncherPage.Setup, setupRunning: true));
        Assert.Equal(InputCommand.None, LauncherInput.Route(PadAction.Back, LauncherPage.Setup, setupRunning: false));
    }

    [Fact]
    public void B_on_the_home_screen_does_nothing()
    {
        Assert.Equal(InputCommand.None, LauncherInput.Route(PadAction.Back, LauncherPage.Home, setupRunning: false));
    }

    [Fact]
    public void Pad_hints_are_hidden_until_the_pad_is_used_and_hide_again_on_mouse_or_keyboard()
    {
        var hints = new InputHints();
        Assert.False(hints.Visible);

        hints.Used(InputSource.Pad);
        Assert.True(hints.Visible);

        hints.Used(InputSource.Mouse);
        Assert.False(hints.Visible);

        hints.Used(InputSource.Pad);
        hints.Used(InputSource.Keyboard);
        Assert.False(hints.Visible);
    }

    [Fact]
    public void Hints_name_only_the_buttons_that_do_something_on_the_page()
    {
        Assert.DoesNotContain("B:", InputHints.Text(LauncherPage.Home, setupRunning: false));
        Assert.Contains("B: back", InputHints.Text(LauncherPage.Settings, setupRunning: false));
        Assert.Contains("B: back", InputHints.Text(LauncherPage.Achievements, setupRunning: false));
        Assert.DoesNotContain("B:", InputHints.Text(LauncherPage.Setup, setupRunning: false));
        Assert.Contains("B: stop setup", InputHints.Text(LauncherPage.Setup, setupRunning: true));
        Assert.All(Enum.GetValues<LauncherPage>(), page => Assert.Contains("A: select", InputHints.Text(page, setupRunning: false)));
    }

    [Fact]
    public void Changing_the_hint_state_raises_Changed_once_per_change()
    {
        var hints = new InputHints();
        var raised = 0;
        hints.Changed += () => raised++;

        hints.Used(InputSource.Mouse);
        hints.Used(InputSource.Pad);
        hints.Used(InputSource.Pad);

        Assert.Equal(1, raised);
    }
}
