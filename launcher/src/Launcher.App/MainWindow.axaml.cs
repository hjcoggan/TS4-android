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
/// and presses (<see cref="PadNavigator"/>, <see cref="LauncherInput"/>); the keyboard reaches
/// everything with Tab, Enter, arrows and Esc, and pad hints show only after pad input
/// (<see cref="InputHints"/>).
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
    private readonly InputHints _hints = new();
    private Avalonia.Point _pointerAt;
    private bool _pressing;
    private readonly DispatcherTimer _padTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private LauncherPage? _shownPage;
    private bool _gameRunning;

    public MainWindow()
    {
        InitializeComponent();
        var cacheRoot = Path.GetFullPath(App.Options.CacheRoot ?? UserDirectories.PreparedDataRoot());
        var preparation = new Preparation(cacheRoot);
        _settingsPath = SettingsFile.PathIn(Path.GetFullPath(App.Options.UserDataRoot ?? UserDirectories.SettingsRoot()));
        _gameExe = App.Options.GameExe ?? Path.Combine(_launcherDirectory, LaunchArguments.GameExeName);
        _home = new HomeModel(preparation, _gameExe);
        // Nothing is searched for or filled in: the rows stay empty until the player picks a file.
        _flow = new SetupFlow(preparation, log: Log);
        _shell = new LauncherShell(_flow);

        _setupPage = new SetupPage(_flow);
        _homePage = new HomePage(_home);
        _settingsPage = new SettingsPage(LoadSettingsPage);
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

        // Arrow keys and pad moves both go through XYFocus, so focus follows the layout.
        XYFocus.SetNavigationModes(this, XYFocusNavigationModes.Enabled);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, (_, _) => _hints.Used(InputSource.Mouse), RoutingStrategies.Tunnel);
        _hints.Changed += Refresh;
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
        HintText.Text = InputHints.Text(page, _flow.Busy);
        HintText.IsVisible = _hints.Visible;
        // Enter triggers the default button anywhere in the window, so only the page on show has one.
        _homePage.SetDefault(page == LauncherPage.Home);
        _setupPage.SetDefault(page == LauncherPage.Setup);

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
        var action = _pad.Update(state, Environment.TickCount64);
        if (action == PadAction.None) return;
        _hints.Used(InputSource.Pad);
        Act(action);
    }

    // Esc is Back. Tab, Enter and the arrows are Avalonia's own: arrows move focus through XYFocus, set on the window.
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_pressing) _hints.Used(InputSource.Keyboard);
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        Act(PadAction.Back);
    }

    // A real mouse move, not the move Avalonia repeats when content scrolls under a still pointer.
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var at = e.GetPosition(this);
        if (at == _pointerAt) return;
        _pointerAt = at;
        _hints.Used(InputSource.Mouse);
    }

    private void Act(PadAction action)
    {
        switch (LauncherInput.Route(action, _shell.Page, _flow.Busy))
        {
            case InputCommand.FocusUp: MoveFocus(NavigationDirection.Up); break;
            case InputCommand.FocusDown: MoveFocus(NavigationDirection.Down); break;
            case InputCommand.FocusLeft: MoveFocus(NavigationDirection.Left); break;
            case InputCommand.FocusRight: MoveFocus(NavigationDirection.Right); break;
            case InputCommand.Press: PressFocused(); break;
            case InputCommand.GoBack: _shell.Back(); break;
            case InputCommand.StopSetup: _setupPage.Stop(); break;
        }
    }

    // A presses what has focus. Enter makes Button, RadioButton and an Expander's header do exactly what a click does;
    // a tab header is selected directly.
    private void PressFocused()
    {
        switch (FocusManager?.GetFocusedElement())
        {
            case TabItem tab:
                tab.IsSelected = true;
                break;
            case Button { IsEnabled: true } button:
                _pressing = true;
                try
                {
                    button.RaiseEvent(new KeyEventArgs { RoutedEvent = KeyDownEvent, Key = Key.Enter, Source = button });
                }
                finally
                {
                    _pressing = false;
                }
                break;
        }
    }

    // Directional focus: to the control that is visually next to the focused one, as XYFocus finds it.
    private void MoveFocus(NavigationDirection direction)
    {
        if (FocusManager?.GetFocusedElement() is not InputElement current)
        {
            (_shell.Page switch
            {
                LauncherPage.Home => _homePage.EntryControl,
                LauncherPage.Settings => _settingsPage.EntryControl,
                _ => _setupPage.EntryControl,
            }).Focus(NavigationMethod.Directional);
            return;
        }
        KeyboardNavigationHandler.GetNext(current, direction)?.Focus(NavigationMethod.Directional);
    }
}
