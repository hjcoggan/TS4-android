namespace TopSpin4.Launcher.Core;

/// <summary>The Launcher's pages. Each is drawn from its own model; the window shows one at a time.</summary>
public enum LauncherPage
{
    /// <summary>First run: <see cref="SetupFlow"/>.</summary>
    Setup,
    /// <summary>PLAY, the status line and the labelled actions: <see cref="HomeModel"/>.</summary>
    Home,
    /// <summary>The settings: <see cref="SettingsPageModel"/>.</summary>
    Settings,
    /// <summary>The achievements read from the player's disc (<see cref="DiscArtCollection.Achievements"/>).</summary>
    Achievements,
}

/// <summary>
/// Which page the window shows. Setup gives way to Home when setup finishes; Settings and
/// Achievements open from Home or from each other (the window's side rail), and Back returns
/// to Home. Later tickets add pages and per-page models here rather than growing the window.
/// </summary>
public sealed class LauncherShell
{
    private readonly SetupFlow _flow;
    private LauncherPage _open = LauncherPage.Home;

    public LauncherShell(SetupFlow flow)
    {
        _flow = flow;
        flow.Changed += () => Changed?.Invoke();
    }

    public LauncherPage Page =>
        _flow.Phase != SetupPhase.Ready ? LauncherPage.Setup
        : _open;

    /// <summary>Raised when the page may have changed, on whichever thread changed it; the window marshals it.</summary>
    public event Action? Changed;

    public void OpenSettings() => Open(LauncherPage.Settings);

    public void OpenAchievements() => Open(LauncherPage.Achievements);

    public void OpenHome() => Open(LauncherPage.Home);

    /// <summary>Esc and the pad's B: leaves Settings or Achievements for Home; Home and Setup have nowhere to go back to.</summary>
    public void Back()
    {
        if (Page is LauncherPage.Settings or LauncherPage.Achievements) Open(LauncherPage.Home);
    }

    /// <summary>The pages after setup open from one another; nothing opens before setup is done.</summary>
    private void Open(LauncherPage page)
    {
        if (Page == LauncherPage.Setup || page == LauncherPage.Setup || Page == page) return;
        _open = page;
        Changed?.Invoke();
    }
}
