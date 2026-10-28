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
}

/// <summary>
/// Which page the window shows. Setup gives way to Home when setup finishes; Settings opens from
/// Home and Back returns to it. Later tickets add pages and per-page models here rather than
/// growing the window.
/// </summary>
public sealed class LauncherShell
{
    private readonly SetupFlow _flow;
    private bool _settingsOpen;

    public LauncherShell(SetupFlow flow)
    {
        _flow = flow;
        flow.Changed += () => Changed?.Invoke();
    }

    public LauncherPage Page =>
        _flow.Phase != SetupPhase.Ready ? LauncherPage.Setup
        : _settingsOpen ? LauncherPage.Settings
        : LauncherPage.Home;

    /// <summary>Raised when the page may have changed, on whichever thread changed it; the window marshals it.</summary>
    public event Action? Changed;

    public void OpenSettings()
    {
        if (Page != LauncherPage.Home) return;
        _settingsOpen = true;
        Changed?.Invoke();
    }

    /// <summary>Esc and the pad's B: leaves Settings for Home; the other pages have nowhere to go back to.</summary>
    public void Back()
    {
        if (Page != LauncherPage.Settings) return;
        _settingsOpen = false;
        Changed?.Invoke();
    }
}
