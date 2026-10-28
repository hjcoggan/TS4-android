using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using TopSpin4.Launcher.Core;
using TopSpin4.Launcher.Platform;

namespace TopSpin4.Launcher;

/// <summary>
/// The Launcher's one window: first-run disc and TU1 setup, then Play. It only draws
/// <see cref="SetupFlow"/>. Everything is reachable with a pad: the pad is polled
/// (<see cref="XInputPad"/>) and mapped to focus moves and presses (<see cref="PadNavigator"/>),
/// and the keyboard's arrow keys do the same.
/// </summary>
public partial class MainWindow : Window
{
    private readonly SetupFlow _flow;
    private readonly string _launcherDirectory = AppContext.BaseDirectory;
    private readonly PadNavigator _pad = new();
    private readonly DispatcherTimer _padTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private IReadOnlyList<string> _discs = [];
    private IReadOnlyList<string> _titleUpdates = [];
    private CancellationTokenSource? _stop;
    private SetupPhase? _shownPhase;
    private readonly string _settingsPath;
    private BeforeStartSettings? _beforeStart;
    private bool _settingsRequested;
    private bool _gameRunning;

    public MainWindow()
    {
        InitializeComponent();
        var cacheRoot = Path.GetFullPath(App.Options.CacheRoot ?? UserDirectories.PreparedDataRoot());
        _flow = new SetupFlow(new Preparation(cacheRoot));
        _settingsPath = SettingsFile.PathIn(Path.GetFullPath(App.Options.UserDataRoot ?? UserDirectories.SettingsRoot()));
        FindInputs();

        _flow.Changed += () => Dispatcher.UIThread.Post(Refresh);
        ChooseDiscButton.Click += async (_, _) => _flow.ChooseDisc(await Pick("Choose your Top Spin 4 disc image", "*.iso")  ?? _flow.DiscPath);
        ChooseTitleUpdateButton.Click += async (_, _) => _flow.ChooseTitleUpdate(await Pick("Choose Title Update 1", null) ?? _flow.TitleUpdatePath);
        NextDiscButton.Click += (_, _) => _flow.ChooseDisc(Next(_discs, _flow.DiscPath));
        NextTitleUpdateButton.Click += (_, _) => _flow.ChooseTitleUpdate(Next(_titleUpdates, _flow.TitleUpdatePath));
        PrepareButton.Click += async (_, _) => await Prepare();
        CancelButton.Click += (_, _) => _stop?.Cancel();
        PlayButton.Click += async (_, _) => await StartGame();
        SafeStartButton.Click += async (_, _) => await StartGame(safe: true);

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        _padTimer.Tick += (_, _) => PollPad();
        Opened += (_, _) => _padTimer.Start();
        Closed += (_, _) => _padTimer.Stop();
        Refresh();
    }

    // Pre-fills the inputs from the usual folders so a pad-only player rarely has to browse.
    private void FindInputs()
    {
        var folders = InputFinder.DefaultFolders(_launcherDirectory);
        _discs = InputFinder.DiscImages(folders);
        _titleUpdates = InputFinder.TitleUpdates(folders);
        if (_flow.NeedsDisc && _discs.Count > 0) _flow.ChooseDisc(_discs[0]);
        if (_flow.NeedsTitleUpdate && _titleUpdates.Count > 0) _flow.ChooseTitleUpdate(_titleUpdates[0]);
    }

    private static string? Next(IReadOnlyList<string> candidates, string? current)
    {
        if (candidates.Count == 0) return current;
        var index = candidates.ToList().FindIndex(path => path == current);
        return candidates[(index + 1) % candidates.Count];
    }

    private async Task<string?> Pick(string title, string? pattern)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = pattern is null
                ? [FilePickerFileTypes.All]
                : [new FilePickerFileType("Disc image") { Patterns = [pattern] }, FilePickerFileTypes.All],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private async Task Prepare()
    {
        _stop = new CancellationTokenSource();
        try
        {
            await _flow.PrepareAsync(_stop.Token);
        }
        finally
        {
            _stop.Dispose();
            _stop = null;
        }
    }

    /// <summary>
    /// Starts the game exe with Play's arguments (Safe start's, with <c>--safe</c>, when
    /// <paramref name="safe"/>) and waits for it. A normal exit closes the Launcher; the game's GPU
    /// failure exit code brings the Launcher back with a note to switch renderer.
    /// </summary>
    private async Task StartGame(bool safe = false)
    {
        if (_flow.Prepared is not { } prepared || _gameRunning) return;
        var exe = App.Options.GameExe ?? LaunchArguments.FindGameExe(_launcherDirectory);
        if (exe is null || !File.Exists(exe))
        {
            PlayMessage.Text = $"{LaunchArguments.GameExeName} was not found next to the Launcher. Put the Launcher in the game's folder.";
            return;
        }
        var start = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(exe))! };
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
            if (GameExitReport.Describe(game.ExitCode) is { } message)
            {
                PlayMessage.Text = message;
                Show();
                PlayButton.Focus(NavigationMethod.Tab);
                return;
            }
            Close();
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _gameRunning = false;
            PlayMessage.Text = "The game could not be started: " + error.Message;
        }
    }

    // Reads the game's settings export once, when Play first shows, and draws the before-start settings.
    private async Task LoadBeforeStartSettings()
    {
        var exe = App.Options.GameExe ?? LaunchArguments.FindGameExe(_launcherDirectory);
        if (exe is null || !File.Exists(exe)) return;
        try
        {
            _beforeStart = new BeforeStartSettings(await SettingsExport.ReadAsync(exe), _settingsPath);
        }
        catch (SettingsExportException error)
        {
            SettingsMessage.Text = error.Message;
            SettingsMessage.IsVisible = true;
            return;
        }
        DrawBeforeStartSettings();
    }

    private void DrawBeforeStartSettings()
    {
        if (_beforeStart is not { } settings) return;
        BeforeStartSettingsHost.Children.Clear();
        foreach (var setting in settings.Settings)
        {
            var row = new StackPanel { Spacing = 4, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
            var button = new Button { Content = $"{setting.Label}: {settings.ValueOf(setting)}", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
            button.Classes.Add("secondary");
            button.Click += (_, _) =>
            {
                var change = settings.Cycle(setting);
                SettingsMessage.Text = BeforeStartSettings.Describe(change);
                SettingsMessage.IsVisible = change != SettingChange.Saved;
                button.Content = $"{setting.Label}: {settings.ValueOf(setting)}";
            };
            row.Children.Add(button);
            var help = new TextBlock { Text = setting.Help, TextAlignment = Avalonia.Media.TextAlignment.Center };
            help.Classes.Add("small");
            row.Children.Add(help);
            BeforeStartSettingsHost.Children.Add(row);
        }
    }

    // Draws the flow. Focus moves only when the phase changes, so a pad press is never undone.
    private void Refresh()
    {
        var phase = _flow.Phase;
        var ready = phase == SetupPhase.Ready;
        var preparing = phase == SetupPhase.Preparing;
        SetupView.IsVisible = !ready;
        PlayView.IsVisible = ready;
        if (ready && !_settingsRequested)
        {
            _settingsRequested = true;
            _ = LoadBeforeStartSettings();
        }

        DiscPanel.IsVisible = _flow.NeedsDisc;
        TitleUpdatePanel.IsVisible = _flow.NeedsTitleUpdate;
        DiscPathText.Text = _flow.DiscPath ?? "No disc image chosen";
        TitleUpdatePathText.Text = _flow.TitleUpdatePath ?? "No Title Update 1 chosen";
        NextDiscButton.IsVisible = _discs.Count > 1;
        NextTitleUpdateButton.IsVisible = _titleUpdates.Count > 1;
        foreach (var button in new[] { ChooseDiscButton, ChooseTitleUpdateButton, NextDiscButton, NextTitleUpdateButton })
            button.IsEnabled = !preparing;

        PrepareButton.IsVisible = !preparing;
        PrepareButton.IsEnabled = _flow.CanPrepare;
        ProgressPanel.IsVisible = preparing;
        SetupMessage.IsVisible = _flow.Message is not null;
        SetupMessage.Text = _flow.Message;
        if (_flow.Progress is { } progress)
        {
            SetupProgressBar.Value = progress.Fraction;
            SetupProgressText.Text = SetupFlow.Describe(progress);
        }
        else if (preparing)
        {
            SetupProgressBar.Value = 0;
            SetupProgressText.Text = "Starting";
        }

        if (phase == _shownPhase) return;
        _shownPhase = phase;
        // After the layout has settled, so the control is visible and enabled.
        Dispatcher.UIThread.Post(() =>
        {
            Control first = phase switch
            {
                SetupPhase.Ready => PlayButton,
                SetupPhase.Preparing => CancelButton,
                _ when _flow.CanPrepare => PrepareButton,
                _ when _flow.NeedsDisc && _flow.DiscPath is null => ChooseDiscButton,
                _ => ChooseTitleUpdateButton,
            };
            first.Focus(NavigationMethod.Tab);
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
                _stop?.Cancel();
                break;
        }
    }

    // Every screen is a short list of controls, so previous and next are enough for all four directions.
    private void MoveFocus(NavigationDirection direction)
    {
        if (FocusManager?.GetFocusedElement() is not InputElement current)
        {
            (PlayView.IsVisible ? PlayButton : PrepareButton.IsEnabled ? PrepareButton : ChooseDiscButton).Focus(NavigationMethod.Tab);
            return;
        }
        KeyboardNavigationHandler.GetNext(current, direction)?.Focus(NavigationMethod.Tab);
    }
}
