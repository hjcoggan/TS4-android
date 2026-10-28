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
    private readonly PadNavigator _pad = new();
    private readonly DispatcherTimer _padTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private LauncherPage? _shownPage;
    private bool _gameRunning;

    public MainWindow()
    {
        InitializeComponent();
        var cacheRoot = Path.GetFullPath(App.Options.CacheRoot ?? UserDirectories.PreparedDataRoot());
        var preparation = new Preparation(cacheRoot);
        _flow = new SetupFlow(preparation);
        _shell = new LauncherShell(_flow);
        _settingsPath = SettingsFile.PathIn(Path.GetFullPath(App.Options.UserDataRoot ?? UserDirectories.SettingsRoot()));
        _gameExe = App.Options.GameExe ?? Path.Combine(_launcherDirectory, LaunchArguments.GameExeName);
        _home = new HomeModel(preparation, _gameExe);

        var folders = InputFinder.DefaultFolders(_launcherDirectory);
        var discs = InputFinder.DiscImages(folders);
        var titleUpdates = InputFinder.TitleUpdates(folders);
        if (_flow.NeedsDisc && discs.Count > 0) _flow.ChooseDisc(discs[0]);
        if (_flow.NeedsTitleUpdate && titleUpdates.Count > 0) _flow.ChooseTitleUpdate(titleUpdates[0]);

        _setupPage = new SetupPage(_flow, discs, titleUpdates);
        _homePage = new HomePage(_home);
        _settingsPage = new SettingsPage(LoadSettingsPage);
        PageHost.Children.Add(_setupPage);
        PageHost.Children.Add(_homePage);
        PageHost.Children.Add(_settingsPage);

        _shell.Changed += () => Dispatcher.UIThread.Post(Refresh);
        _homePage.PlayPressed += async () => await StartGame();
        _homePage.ActionPressed += async action => await OnHomeAction(action);
        _settingsPage.BackPressed += _shell.Back;
        // The exe or the prepared folders may change while the window is open; look again when it is used.
        Activated += (_, _) => Refresh();

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
            case HomeAction.Help:
                OpenHelp();
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
        else _home.Say($"{HelpDocument.FileName} was not found next to the Launcher.");
        Refresh();
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
            _home.Say("Could not open it: " + error.Message);
            Refresh();
        }
    }

    /// <summary>
    /// Starts the game exe with Play's arguments (Safe start's, with <c>--safe</c>, when
    /// <paramref name="safe"/>) and waits for it. A normal exit closes the Launcher; the game's GPU
    /// failure exit code brings the Launcher back with a note to switch renderer.
    /// </summary>
    private async Task StartGame(bool safe = false)
    {
        if (_home.Prepared is not { } prepared || _gameRunning) return;
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
            if (_home.Note is not null)
            {
                Show();
                Refresh();
                return;
            }
            Close();
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _gameRunning = false;
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
