using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TopSpin4.Launcher.Core;
using TopSpin4.Launcher.Pages;
using TopSpin4.Launcher.Platform;

namespace TopSpin4.Launcher;

/// <summary>
/// The Launcher's one window. It owns no behaviour of its own beyond input and starting the
/// game: <see cref="LauncherShell"/> says which page shows, and each page draws its model
/// (<see cref="SetupFlow"/>, <see cref="HomeModel"/>, <see cref="SettingsPageModel"/>). Everything
/// is reachable with a pad: the pad is polled (<see cref="XInputPad"/>) and mapped to focus moves
/// and presses (<see cref="PadNavigator"/>), and the keyboard's arrow keys do the same.
/// </summary>
public partial class MainWindow : Window
{
    private readonly string _launcherDirectory = AppContext.BaseDirectory;
    private readonly string _settingsPath;
    private readonly string _gameExe;
    private readonly SetupFlow _flow;
    private readonly LauncherShell _shell;
    private readonly HomeModel _home;
    private readonly SetupPage _setupPage;
    private readonly HomePage _homePage;
    private readonly SettingsPage _settingsPage;
    private readonly GameFilesLocationModel _location;
    private readonly GameFilesLocationPanel _setupLocation;
    private readonly GameFilesLocationPanel _homeLocation;
    private readonly PadNavigator _pad = new();
    private readonly DispatcherTimer _padTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private LauncherPage? _shownPage;
    private bool _gameRunning;

    public MainWindow()
    {
        InitializeComponent();
        // The command line wins, then the place the player moved the game files to (the location file the
        // game reads too), then the default folder.
        var userRoot = Path.GetFullPath(App.Options.UserDataRoot ?? UserDirectories.SettingsRoot());
        var locationFile = new GameFilesLocation(userRoot);
        var cacheRoot = Path.GetFullPath(App.Options.CacheRoot ?? locationFile.Current(UserDirectories.PreparedDataRoot()));
        var preparation = new Preparation(cacheRoot);
        _location = new GameFilesLocationModel(preparation, locationFile);
        _settingsPath = SettingsFile.PathIn(userRoot);
        _gameExe = App.Options.GameExe ?? Path.Combine(_launcherDirectory, LaunchArguments.GameExeName);
        _home = new HomeModel(preparation, _gameExe);
        // Nothing is searched for or filled in: the rows stay empty until the player picks a file.
        _flow = new SetupFlow(preparation, log: Log);
        _shell = new LauncherShell(_flow);

        _setupPage = new SetupPage(_flow);
        _homePage = new HomePage(_home);
        _settingsPage = new SettingsPage(LoadSettingsPage);
        _setupLocation = new GameFilesLocationPanel(_location);
        _homeLocation = new GameFilesLocationPanel(_location);
        _setupPage.LocationHost.Children.Add(_setupLocation);
        _homePage.LocationHost.Children.Add(_homeLocation);
        _location.Changed += () => Dispatcher.UIThread.Post(Refresh);
        PageHost.Children.Add(_setupPage);
        PageHost.Children.Add(_homePage);
        PageHost.Children.Add(_settingsPage);

        _shell.Changed += () => Dispatcher.UIThread.Post(Refresh);
        _setupPage.HelpPressed += OpenHelp;
        _homePage.PlayPressed += async () => await StartGame();
        _homePage.ActionPressed += async action => await OnHomeAction(action);
        _settingsPage.BackPressed += _shell.Back;
        // The exe or the prepared folders may change while the window is open; look again when it is used.
        Activated += (_, _) => Refresh();

        // The game started without setup done: the setup page says why (or the home page, if this Launcher finds the files).
        if (App.Options.NotSetUp is { } reason) Say(NotSetUp.Describe(reason));

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        _padTimer.Tick += (_, _) => PollPad();
        Opened += (_, _) => _padTimer.Start();
        Closed += (_, _) => _padTimer.Stop();
        Refresh();
    }

    private async Task OnHomeAction(HomeAction action)
    {
        switch (action)
        {
            case HomeAction.Settings:
                _shell.OpenSettings();
                await _settingsPage.OpenAsync();
                break;
            case HomeAction.CreateShortcut:
                CreateShortcut();
                break;
            case HomeAction.Help:
                OpenHelp();
                break;
            case HomeAction.OpenCrashReport:
                OpenCrashReport();
                break;
            case HomeAction.OpenLogs:
                OpenLogs();
                break;
            case HomeAction.SafeStart:
                await StartGame(safe: true);
                break;
        }
    }

    // The game's settings export, read once when Settings first opens; a reason instead of rows when it can't be read.
    private async Task<SettingsPageModel> LoadSettingsPage()
    {
        if (!File.Exists(_gameExe))
            return SettingsPageModel.Unavailable($"{LaunchArguments.GameExeName} was not found next to the Launcher, so the game's settings cannot be read.");
        try
        {
            return SettingsPageModel.From(await SettingsExport.ReadAsync(_gameExe), _settingsPath);
        }
        catch (SettingsExportException error)
        {
            return SettingsPageModel.Unavailable(error.Message);
        }
    }

    private void OpenHelp()
    {
        if (HelpDocument.Find(_launcherDirectory) is { } readme) Open(readme);
        else Say($"{HelpDocument.FileName} was not found next to the Launcher.");
        Refresh();
    }

    // A line for the page the player is on: the setup page has its own message line, the home screen the note.
    private void Say(string note)
    {
        if (_shell.Page == LauncherPage.Setup) _flow.Notice(note);
        else _home.Say(note);
    }

    // The technical text behind a refused file or a failed setup goes to logs/launcher.log beside the game, with the game's logs.
    private void Log(string line)
    {
        try
        {
            if (_home.LogsFolder is not { } folder) return;
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "launcher.log"), $"{DateTime.Now:s} {line}{Environment.NewLine}");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The log is a convenience; a folder that can't be written to is not worth a message.
        }
    }

    // "Top Spin 4" on the Desktop and in the Start menu, running the game exe with no arguments (DirectShortcut).
    private void CreateShortcut()
    {
        if (!File.Exists(_gameExe))
        {
            _home.Say($"{LaunchArguments.GameExeName} was not found next to the Launcher, so there is nothing to make a shortcut to.");
            Refresh();
            return;
        }
        try
        {
            var shortcut = new DirectShortcut(new ShellLinkWriter(),
                [Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.Programs)]);
            shortcut.Create(_gameExe);
            _home.Say("Created the Top Spin 4 shortcut on your Desktop and in the Start menu. It starts the game with your saved settings.");
        }
        catch (IOException error)
        {
            Log("Create shortcut: " + error.Message);
            _home.Say(error.Message);
        }
        Refresh();
    }

    private void OpenCrashReport()
    {
        if (_home.CrashReportPath is { } report) Open(report);
    }

    private void OpenLogs()
    {
        if (_home.LogsFolder is { } logs)
        {
            Directory.CreateDirectory(logs);
            Open(logs);
        }
    }

    private void Open(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Say("Could not open it: " + error.Message);
            Refresh();
        }
    }

    /// <summary>
    /// Starts the game exe with Play's arguments (Safe start's, with <c>--safe</c>, when
    /// <paramref name="safe"/>), hides the Launcher and waits for it. The home screen comes back
    /// when the game exits, saying how it ended (<see cref="HomeModel.SessionEnded"/>).
    /// </summary>
    private async Task StartGame(bool safe = false)
    {
        // Not while the game files are being moved: the old copy is removed at the end of the move.
        if (_home.Prepared is not { } prepared || _gameRunning || _location.Moving) return;
        if (!_home.CanPlay)
        {
            Refresh();
            return;
        }
        var start = new ProcessStartInfo(_gameExe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(_gameExe))! };
        // Only a Launcher started with --user-data points the game elsewhere; otherwise the game finds the same folder itself.
        List<string> extra = [];
        if (App.Options.UserDataRoot is { } userData) extra.Add("--user_data_root=" + Path.GetFullPath(userData));
        foreach (var argument in safe ? LaunchArguments.SafeStart(prepared, extra) : LaunchArguments.Play(prepared, extra))
            start.ArgumentList.Add(argument);
        try
        {
            using var game = Process.Start(start);
            if (game is null) return;
            _gameRunning = true;
            Hide();
            await game.WaitForExitAsync();
            _gameRunning = false;
            _home.SessionEnded(game.ExitCode);
            Show();
            Activate();
            Refresh();
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _gameRunning = false;
            Show();
            _home.Say("The game could not be started: " + error.Message);
            Refresh();
        }
    }

    // Draws the page the shell says. Focus moves only when the page changes, so a pad press is never undone.
    private void Refresh()
    {
        var page = _shell.Page;
        _setupPage.IsVisible = page == LauncherPage.Setup;
        _homePage.IsVisible = page == LauncherPage.Home;
        _settingsPage.IsVisible = page == LauncherPage.Settings;
        _setupPage.Refresh();
        _homePage.Refresh();
        _setupPage.PrepareButton.IsEnabled = _location.AllowsSetup(_flow);
        _setupLocation.Refresh();
        _homeLocation.Refresh();
        if (_location.Moving) _homePage.PlayButton.IsEnabled = false;
        HintText.Text = page switch
        {
            LauncherPage.Setup => "D-pad or stick: move      A: select      B: stop setup",
            LauncherPage.Settings => "D-pad or stick: move      A: select      B: back",
            _ => "D-pad or stick: move      A: select",
        };

        if (page == _shownPage) return;
        _shownPage = page;
        // After the layout has settled, so the control is visible and enabled.
        Dispatcher.UIThread.Post(() =>
        {
            switch (page)
            {
                case LauncherPage.Setup: _setupPage.FocusFirst(); break;
                case LauncherPage.Home: _homePage.FocusFirst(); break;
                default: _settingsPage.FocusFirst(); break;
            }
        }, DispatcherPriority.Background);
    }

    private void PollPad()
    {
        if (!IsActive || !XInputPad.TryRead(out var state))
        {
            _pad.Update(default, Environment.TickCount64);
            return;
        }
        Act(_pad.Update(state, Environment.TickCount64));
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var action = e.Key switch
        {
            Key.Up => PadAction.Up,
            Key.Down => PadAction.Down,
            Key.Left => PadAction.Left,
            Key.Right => PadAction.Right,
            Key.Escape => PadAction.Back,
            _ => PadAction.None,
        };
        if (action == PadAction.None) return;
        e.Handled = true;
        Act(action);
    }

    private void Act(PadAction action)
    {
        switch (action)
        {
            case PadAction.Up or PadAction.Left:
                MoveFocus(NavigationDirection.Previous);
                break;
            case PadAction.Down or PadAction.Right:
                MoveFocus(NavigationDirection.Next);
                break;
            case PadAction.Accept:
                if (FocusManager?.GetFocusedElement() is Button { IsEnabled: true } button)
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                break;
            case PadAction.Back:
                _setupPage.Stop();
                _shell.Back();
                break;
        }
    }

    // Every page is a short list of controls, so previous and next are enough for all four directions.
    private void MoveFocus(NavigationDirection direction)
    {
        if (FocusManager?.GetFocusedElement() is not InputElement current)
        {
            (_shell.Page switch
            {
                LauncherPage.Home => _homePage.EntryControl,
                LauncherPage.Settings => _settingsPage.EntryControl,
                _ => _setupPage.EntryControl,
            }).Focus(NavigationMethod.Tab);
            return;
        }
        KeyboardNavigationHandler.GetNext(current, direction)?.Focus(NavigationMethod.Tab);
    }
}
