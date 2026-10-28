namespace TopSpin4.Launcher.Core;

/// <summary>What the window does for one press: the App turns each into a focus move, a press or a page action.</summary>
public enum InputCommand
{
    None,
    FocusUp,
    FocusDown,
    FocusLeft,
    FocusRight,
    /// <summary>Presses the focused control (the pad's A).</summary>
    Press,
    /// <summary>Leaves Settings for Home.</summary>
    GoBack,
    /// <summary>Stops a running setup; it resumes later.</summary>
    StopSetup,
}

/// <summary>
/// The window's input rules, apart from any UI type: a pad direction moves focus the same way on
/// every page (the App hands it to Avalonia's directional focus, so focus follows the layout),
/// A presses, and B or Esc goes back where there is a back: Settings, and a running setup.
/// The home screen and the setup rows have nowhere to go back to, so B does nothing there.
/// </summary>
public static class LauncherInput
{
    public static InputCommand Route(PadAction action, LauncherPage page, bool setupRunning) => action switch
    {
        PadAction.Up => InputCommand.FocusUp,
        PadAction.Down => InputCommand.FocusDown,
        PadAction.Left => InputCommand.FocusLeft,
        PadAction.Right => InputCommand.FocusRight,
        PadAction.Accept => InputCommand.Press,
        PadAction.Back when page == LauncherPage.Settings => InputCommand.GoBack,
        PadAction.Back when page == LauncherPage.Setup && setupRunning => InputCommand.StopSetup,
        _ => InputCommand.None,
    };
}

/// <summary>Where the latest input came from.</summary>
public enum InputSource
{
    Pad,
    Keyboard,
    Mouse,
}

/// <summary>
/// The pad button hints at the foot of the window: shown after pad input, hidden again once the
/// mouse or keyboard is used, so a desktop player never sees them.
/// </summary>
public sealed class InputHints
{
    public bool Visible { get; private set; }

    /// <summary>Raised when <see cref="Visible"/> changes.</summary>
    public event Action? Changed;

    public void Used(InputSource source)
    {
        var visible = source == InputSource.Pad;
        if (visible == Visible) return;
        Visible = visible;
        Changed?.Invoke();
    }

    /// <summary>The hint line for a page: only the buttons that do something there.</summary>
    public static string Text(LauncherPage page, bool setupRunning)
    {
        const string Move = "D-pad or stick: move      A: select";
        return page switch
        {
            LauncherPage.Settings => Move + "      B: back",
            LauncherPage.Setup when setupRunning => Move + "      B: stop setup",
            _ => Move,
        };
    }
}
