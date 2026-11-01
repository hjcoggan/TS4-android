using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Media.Imaging;
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
    private readonly AchievementsPage _achievementsPage = new();
    private readonly GameFilesLocationModel _location;
    private readonly GameFilesLocationPanel _setupLocation;
    private readonly GameFilesLocationPanel _settingsLocation;
    private readonly Dictionary<LauncherPage, Button> _nav = [];
    private readonly string _version = FooterInfo.Version(typeof(MainWindow).Assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion);
    private readonly string? _gpu = GpuName.Read();
    private readonly PadNavigator _pad = new();
    private readonly InputHints _hints = new();
    private Avalonia.Point _pointerAt;
    private bool _pressing;
    private readonly DispatcherTimer _padTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private LauncherPage? _shownPage;
    private bool _padHeldForBinding;
    private readonly string _artFolder;
    private LauncherTheme _theme = LauncherTheme.Original;
    private bool _themeLoaded;
    private Dictionary<string, Bitmap> _pictures = [];
    // The achievement icons in grey, for the locked ones.
    private Dictionary<string, Bitmap> _greyPictures = [];
    private readonly string _unlocksPath;
    private Showcase _showcase = Showcase.Empty;
    private (RowState Disc, SetupPhase Phase)? _themeKey;
    private string? _shownShaderFailure;

    public MainWindow()
    {
        InitializeComponent();
        // The command line wins, then the place the player moved the game files to (the location file the
        // game reads too), then the default folder.
        var userRoot = Path.GetFullPath(App.Options.UserDataRoot ?? UserDirectories.UserDataRoot(_launcherDirectory));
        var locationFile = new GameFilesLocation(userRoot);
        var cacheRoot = locationFile.Resolve(App.Options.CacheRoot, UserDirectories.PreparedDataRoot(_launcherDirectory));
        // A portable install writes beside the exe (ADR-0009): a folder that can't be written to is refused on the
        // setup page, never replaced by a per-user one. With --user-data the developer's folder is the one checked.
        var refusal = UserDirectories.Refusal(App.Options.UserDataRoot is null ? Path.TrimEndingDirectorySeparator(_launcherDirectory) : userRoot);
        var preparation = new Preparation(cacheRoot);
        _location = new GameFilesLocationModel(preparation, locationFile);
        _settingsPath = SettingsFile.PathIn(userRoot);
        _gameExe = App.Options.GameExe ?? Path.Combine(_launcherDirectory, LaunchArguments.GameExeName);
        _home = new HomeModel(preparation, _gameExe);
        // Nothing is searched for or filled in: the rows stay empty until the player picks a file.
        // The disc row reads the disc's art the moment it accepts the disc; the art folder is userdata\art.
        _artFolder = UserDirectories.ArtFolder(userRoot);
        // The game records each achievement the player earns; the Achievements page marks them.
        _unlocksPath = AchievementUnlocks.PathIn(userRoot);
        // Once the disc is extracted (and at each start) the art is read again from there if the art folder is missing or incomplete.
        // Setup ends with the shader step: the shaders of the game's archives and Modules, converted with the XenosRecomp
        // the release ships. A pack another XenosRecomp built is rebuilt when the window opens (below), before Play.
        var shaders = new ShaderPreparation(XenosRecompConverter.ShippedWith(_launcherDirectory), ShaderConverter.Id,
            new GameModuleShaders(_gameExe, preparation, new StreamingPrepareRunner(TimeSpan.FromMinutes(10), "Reading the game's Module shaders")));
        // Then the pipeline step: the game's prepare-pipelines mode builds the pipeline store and the driver cache.
        var pipelines = new PipelinePreparation(_gameExe, preparation,
            new StreamingPrepareRunner(TimeSpan.FromMinutes(10), "Preparing pipelines"));
        _flow = new SetupFlow(preparation, log: Log, readDiscArt: iso => DiscArt.FromIso(iso, _artFolder, Log),
            readPreparedArt: root => DiscArt.RestoreFromFolder(root, _artFolder, Log), refusal: refusal, shaders: shaders,
            pipelines: pipelines);
        _shell = new LauncherShell(_flow);

        _setupPage = new SetupPage(_flow);
        _homePage = new HomePage(_home);
        _settingsPage = new SettingsPage(LoadSettingsPage);
        _setupLocation = new GameFilesLocationPanel(_location);
        _settingsLocation = new GameFilesLocationPanel(_location);
        _setupPage.LocationHost.Children.Add(_setupLocation);
        _settingsPage.LocationHost.Children.Add(_settingsLocation);
        _location.Changed += () =>
        {
            Dispatcher.UIThread.Post(ShowFooter);
            RequestRefresh();
        };
        // Home fills the page area; the other pages share the scrolling column on the scrim.
        PageHost.Children.Add(_homePage);
        FormHost.Children.Add(_setupPage);
        FormHost.Children.Add(_settingsPage);
        FormHost.Children.Add(_achievementsPage);
        BuildRail();

        _shell.Changed += RequestRefresh;
        _setupPage.HelpPressed += OpenHelp;
        _homePage.PlayPressed += async () => await StartGame();
        _homePage.ActionPressed += async action => await OnHomeAction(action);
        _homePage.AchievementsPressed += _shell.OpenAchievements;
        _settingsPage.BackPressed += _shell.Back;
        _achievementsPage.BackPressed += _shell.Back;
        // The exe or the prepared folders may change while the window is open; look again when it is used.
        Activated += (_, _) =>
        {
            ShowFooter();
            // The game may have been run outside the Launcher and earned achievements meanwhile.
            ReloadUnlocks();
            Refresh();
        };

        // The look starts from whatever the art folder holds. If the folder is missing or incomplete the art is read again
        // from the prepared disc folder in the background (never failing the start), and the look follows when it is done.
        ReloadTheme();
        Opened += async (_, _) =>
        {
            await Task.Run(_flow.RefreshArt);
            ReloadTheme();
            Refresh();
            // A missing or stale shader pack or pipeline store is built again at once, with the progress bar, so no click is needed.
            if (_flow.WantsRebuild) await _setupPage.PrepareAsync();
        };

        // The game started without setup done: the setup page says why (or the home page, if this Launcher finds the files).
        // A refused folder keeps its own message, which already says why.
        if (App.Options.NotSetUp is { } reason && refusal is null) Say(NotSetUp.Describe(reason));

        // Arrow keys and pad moves both go through XYFocus, so focus follows the layout.
        XYFocus.SetNavigationModes(this, XYFocusNavigationModes.Enabled);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, (_, e) => _settingsPage.OfferKeyUp(e), RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, (_, _) => _hints.Used(InputSource.Mouse), RoutingStrategies.Tunnel);
        _hints.Changed += Refresh;
        _padTimer.Tick += (_, _) => PollPad();
        Opened += (_, _) => _padTimer.Start();
        Closed += (_, _) => _padTimer.Stop();
        ShowFooter();
        Refresh();
    }

    private async Task OnHomeAction(HomeAction action)
    {
        switch (action)
        {
            case HomeAction.SetUpAgain:
                _flow.SetUpAgain();
                break;
            case HomeAction.Settings:
                await OpenSettings();
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

    private async Task OpenSettings()
    {
        _shell.OpenSettings();
        await _settingsPage.OpenAsync();
    }

    // The side rail: Home, Settings and Achievements (after setup), and Help. Gamepad and arrows reach it through XYFocus.
    private void BuildRail()
    {
        Button Add(string label, string icon, Action pressed)
        {
            var button = new Button
            {
                Content = new StackPanel
                {
                    Spacing = 4,
                    Children = { Icons.Make(icon, 28), new TextBlock { Text = label, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center } },
                },
            };
            Avalonia.Automation.AutomationProperties.SetName(button, label);
            button.Classes.Add("nav");
            button.Click += (_, _) => pressed();
            NavHost.Children.Add(button);
            return button;
        }
        _nav[LauncherPage.Home] = Add("Home", Icons.Home, _shell.OpenHome);
        _nav[LauncherPage.Settings] = Add("Settings", Icons.Settings, async () => await OpenSettings());
        _nav[LauncherPage.Achievements] = Add("Achievements", Icons.Trophy, _shell.OpenAchievements);
        var help = Add("Help", Icons.Help, OpenHelp);
        // Down from the rail's last page stays in the rail instead of jumping to the nearest card.
        XYFocus.SetDown(help, help);
    }

    // The rail marks the open page; before setup only Help is there.
    private void ShowRail(LauncherPage page)
    {
        foreach (var (target, button) in _nav)
        {
            button.IsVisible = page != LauncherPage.Setup;
            if (target == page) button.Classes.Add("current");
            else button.Classes.Remove("current");
        }
    }

    // Version and commit, the renderer the Settings file chooses, the GPU, and where the game files are.
    private void ShowFooter()
    {
        string? settings = null;
        try
        {
            if (File.Exists(_settingsPath)) settings = File.ReadAllText(_settingsPath);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
        FooterText.Text = FooterInfo.Line(_version, FooterInfo.Renderer(settings), _gpu, _location.Folder);
    }

    // The game's settings export, read once when Settings first opens; a reason instead of rows when it can't be read.
    private async Task<SettingsPageModel> LoadSettingsPage()
    {
        if (!File.Exists(_gameExe))
            return SettingsPageModel.Unavailable($"{LaunchArguments.GameExeNotFound}, so the game's settings cannot be read.");
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
        if (HelpDocument.Find(_launcherDirectory) is not { } readme)
        {
            Say(LaunchArguments.NotFoundNextToLauncher(HelpDocument.FileName) + ".");
        }
        else
        {
            // Windows has no app for .md files unless the player installed one, so shell-opening it fails:
            // the README is plain text, and Notepad is on every Windows.
            try
            {
                Process.Start(new ProcessStartInfo(readme) { UseShellExecute = true })?.Dispose();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                try
                {
                    var notepad = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "notepad.exe")) { UseShellExecute = false };
                    notepad.ArgumentList.Add(readme);
                    Process.Start(notepad)?.Dispose();
                }
                catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    Say("Could not open the help: " + error.Message);
                }
            }
        }
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
            _home.Say($"{LaunchArguments.GameExeNotFound}, so there is nothing to make a shortcut to.");
            Refresh();
            return;
        }
        try
        {
            var shortcut = new DirectShortcut(new ShellLinkWriter(),
                [Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.Programs)]);
            shortcut.Create(_gameExe, DiscArtFolder.Open(_artFolder).IconPath);
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
        if (_home.Prepared is not { } prepared || _home.Status == HomeStatus.GameRunning || _location.Moving) return;
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
        Process? game;
        try
        {
            game = Process.Start(start);
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // The system would not run the exe: a failed start, so Safe start comes up front.
            Log("Start game: " + error.Message);
            _home.StartFailed(error.Message);
            Refresh();
            return;
        }
        if (game is null)
        {
            _home.StartFailed("no process was started.");
            Refresh();
            return;
        }
        using (game)
        {
            _home.GameStarted();
            Hide();
            await game.WaitForExitAsync();
            _home.SessionEnded(game.ExitCode);
        }
        ReloadUnlocks();
        Show();
        Activate();
        Refresh();
        // Shaders the game met that the pack lacks join it now, so they are never converted live again.
        await _setupPage.AddTopUpsAsync();
    }

    // Reads the art folder and, if it changed since the last look (LauncherTheme.ArtStamp: another disc's art under the same
    // file names counts), builds the look from it and puts it on screen. LauncherTheme decides what shows; the window loads the
    // pictures it is asked about, and a picture it cannot draw is reported back as missing.
    private void ReloadTheme()
    {
        var art = DiscArtFolder.Open(_artFolder);
        if (_themeLoaded && art.Stamp == _theme.ArtStamp)
        {
            ReloadUnlocks();
            return;
        }
        var pictures = new Dictionary<string, Bitmap>();
        WindowIcon? icon = null;
        bool CanDraw(DiscArtSlot slot, string path)
        {
            if (slot == DiscArtSlot.TitleIcon) return (icon = LauncherLook.Icon(path, Log)) is not null;
            if (LauncherLook.Load(path, Log) is not { } bitmap) return false;
            pictures[path] = bitmap;
            return true;
        }
        var theme = LauncherTheme.From(art, CanDraw);
        // The card row's pictures, decoded small: a card never shows them larger.
        var showcase = Showcase.From(art, AchievementUnlocks.Read(_unlocksPath));
        void LoadAll(IEnumerable<DiscArtEntry> entries, int width)
        {
            foreach (var entry in entries)
                if (!pictures.ContainsKey(entry.ImagePath) && LauncherLook.LoadScaled(entry.ImagePath, width, Log) is { } bitmap)
                    pictures[entry.ImagePath] = bitmap;
        }
        LoadAll(showcase.Players, 144);
        LoadAll(showcase.Venues, 400);
        LoadAll(showcase.Achievements, 64);
        var grey = new Dictionary<string, Bitmap>();
        foreach (var entry in showcase.Achievements)
            if (pictures.GetValueOrDefault(entry.ImagePath) is { } colour && !grey.ContainsKey(entry.ImagePath) && LauncherLook.Grayscale(colour) is { } dull)
                grey[entry.ImagePath] = dull;
        if (art.ImagePath(DiscArtSlot.TitleIcon) is { } iconPng && LauncherLook.Load(iconPng, Log) is { } iconBitmap) pictures[iconPng] = iconBitmap;

        var old = _pictures.Values.Concat(_greyPictures.Values).ToList();
        _pictures = pictures;
        _greyPictures = grey;
        _showcase = showcase;
        _theme = theme;
        _themeLoaded = true;
        LauncherLook.Apply(theme.Palette);
        Icon = icon;
        CreditText.Text = theme.CreditLine;
        var logo = Picture(theme.Source(DiscArtSlot.Logo));
        _homePage.ShowLogo(logo);
        ShowShowcase();
        LogoImage.Source = logo;
        var railIcon = art.ImagePath(DiscArtSlot.TitleIcon) is { } path ? _pictures.GetValueOrDefault(path) : null;
        RailIcon.Source = railIcon;
        RailIcon.IsVisible = railIcon is not null;
        RailWordmark.IsVisible = railIcon is null;
        ShowPageLook(_shell.Page);
        foreach (var bitmap in old) bitmap.Dispose();
    }

    private void ShowShowcase()
    {
        _homePage.ShowCards(_showcase, path => _pictures.GetValueOrDefault(path));
        _achievementsPage.Show(_showcase, path => _pictures.GetValueOrDefault(path), path => _greyPictures.GetValueOrDefault(path));
    }

    // The game writes its unlock file as achievements are earned; the pages follow once it changed.
    private void ReloadUnlocks()
    {
        var unlocks = AchievementUnlocks.Read(_unlocksPath);
        if (unlocks.Stamp == (_showcase.Unlocks ?? AchievementUnlocks.None).Stamp) return;
        _showcase = _showcase with { Unlocks = unlocks };
        ShowShowcase();
    }

    private (bool Home, PageLook Look, ThemePalette Palette, Bitmap? Background)? _shownLook;

    private Bitmap? Picture(ThemeSource source) => source.ArtPath is { } path && _pictures.TryGetValue(path, out var bitmap) ? bitmap : null;

    // What the theme says the page draws: its picture, then the scrim over it. Home fades from dark on the left (where its
    // text is) to the clear picture, and darkens again under the card row; the other pages quieten the whole picture, and
    // their text sits on the content scrim. Without a picture the original look's own gradient shows.
    private void ShowPageLook(LauncherPage page)
    {
        var look = _theme.LookFor(page);
        var palette = _theme.Palette;
        var background = Picture(look.Background);
        // Refresh runs often; the backdrop is rebuilt only when what it shows changes.
        var lookKey = (page == LauncherPage.Home, look, palette, background);
        if (_shownLook is { } shown && shown.Equals(lookKey)) return;
        _shownLook = lookKey;
        BackgroundImage.Source = background;
        BackgroundImage.IsVisible = background is not null;
        OriginalBackdrop.Background = background is null ? Gradient(palette.Base, palette.Panel, new(0, 0, RelativeUnit.Relative), new(1, 1, RelativeUnit.Relative)) : null;
        var clear = WithAlpha(palette.Scrim, 0);
        if (background is null)
        {
            SideScrim.Background = BottomScrim.Background = null;
        }
        else if (page == LauncherPage.Home)
        {
            var dark = Avalonia.Media.Color.Parse(palette.Scrim);
            SideScrim.Background = new LinearGradientBrush
            {
                StartPoint = new(0, 0, RelativeUnit.Relative),
                EndPoint = new(1, 0, RelativeUnit.Relative),
                GradientStops = { new(dark, 0), new(dark, 0.38), new(clear, 0.78) },
            };
            BottomScrim.Background = new LinearGradientBrush
            {
                StartPoint = new(0, 0, RelativeUnit.Relative),
                EndPoint = new(0, 1, RelativeUnit.Relative),
                GradientStops = { new(clear, 0.55), new(WithAlpha(palette.Base, 0xE6), 1) },
            };
        }
        else
        {
            SideScrim.Background = look.DimsBackground ? new SolidColorBrush(Avalonia.Media.Color.Parse(palette.Dim)) : null;
            BottomScrim.Background = null;
        }
        LogoBlock.IsVisible = look.ShowsLogo;
        TitleText.IsVisible = !look.ShowsLogo;
    }

    private static Avalonia.Media.Color WithAlpha(string colour, byte alpha)
    {
        var parsed = Avalonia.Media.Color.Parse(colour);
        return Avalonia.Media.Color.FromArgb(alpha, parsed.R, parsed.G, parsed.B);
    }

    private static LinearGradientBrush Gradient(string from, string to, RelativePoint start, RelativePoint end) => new()
    {
        StartPoint = start,
        EndPoint = end,
        GradientStops = { new(Avalonia.Media.Color.Parse(from), 0), new(Avalonia.Media.Color.Parse(to), 1) },
    };

    // Setup reports progress through Changed many times a second, from worker threads: one refresh waits on the UI thread
    // at a time, so a burst of reports never queues more refreshes than the UI thread can draw (that froze the window
    // as setup ended).
    private int _refreshPending;

    private void RequestRefresh()
    {
        if (Interlocked.Exchange(ref _refreshPending, 1) == 1) return;
        // Below rendering and input, so the window paints and answers while reports keep coming.
        Dispatcher.UIThread.Post(() =>
        {
            Volatile.Write(ref _refreshPending, 0);
            Refresh();
        }, DispatcherPriority.Background);
    }

    // Draws the page the shell says. Focus moves only when the page changes, so a pad press is never undone.
    private void Refresh()
    {
        var page = _shell.Page;
        // The art arrives with the disc row's accept and when setup finishes; look again at those moments.
        if (_themeKey != (_flow.Disc.State, _flow.Phase))
        {
            _themeKey = (_flow.Disc.State, _flow.Phase);
            ReloadTheme();
        }
        ShowPageLook(page);
        // A shader or pipeline step that failed does not stop Play, so its message goes where the player is: the home screen's note.
        if ((_flow.ShaderFailure ?? _flow.PipelineFailure) is { } shaderFailure && shaderFailure != _shownShaderFailure)
        {
            _shownShaderFailure = shaderFailure;
            _home.Say(shaderFailure);
        }
        _setupPage.IsVisible = page == LauncherPage.Setup;
        _homePage.IsVisible = page == LauncherPage.Home;
        _settingsPage.IsVisible = page == LauncherPage.Settings;
        _achievementsPage.IsVisible = page == LauncherPage.Achievements;
        FormScroller.IsVisible = page != LauncherPage.Home;
        ShowRail(page);
        _setupPage.Refresh();
        _homePage.Refresh();
        _setupPage.PrepareButton.IsEnabled = _location.AllowsSetup(_flow);
        _setupLocation.Refresh();
        _settingsLocation.Refresh();
        if (_location.Moving) _homePage.PlayButton.IsEnabled = false;
        HintText.Text = InputHints.Text(page, _flow.Busy);
        HintText.IsVisible = _hints.Visible;
        // Enter triggers the default button anywhere in the window, so only the page on show has one.
        _homePage.SetDefault(page == LauncherPage.Home);
        _setupPage.SetDefault(page == LauncherPage.Setup);

        if (page == _shownPage) return;
        var first = _shownPage is null;
        _shownPage = page;
        FormScroller.Offset = default;
        // A light fade into the new page (the transition is in Theme.axaml); not on the first page, which the window opens with.
        if (!first)
        {
            var transitions = PageHost.Transitions;
            PageHost.Transitions = null;
            PageHost.Opacity = 0;
            PageHost.Transitions = transitions;
            Dispatcher.UIThread.Post(() => PageHost.Opacity = 1, DispatcherPriority.Render);
        }
        // After the layout has settled, so the control is visible and enabled.
        Dispatcher.UIThread.Post(() =>
        {
            switch (page)
            {
                case LauncherPage.Setup: _setupPage.FocusFirst(); break;
                case LauncherPage.Home: _homePage.FocusFirst(); break;
                case LauncherPage.Achievements: _achievementsPage.FocusFirst(); break;
                default: _settingsPage.FocusFirst(); break;
            }
        }, DispatcherPriority.Background);
    }

    private void PollPad()
    {
        _settingsPage.Tick();
        if (!IsActive || !XInputPad.TryRead(out var state))
        {
            _pad.Update(default, Environment.TickCount64);
            return;
        }
        // A binding row waiting for a pad button takes every press, so the menu must not also act on it; nor on
        // the press that answered, until the pad is let go (B would otherwise go back as it is captured).
        if (_settingsPage.Capturing) _padHeldForBinding = true;
        _settingsPage.OfferPad(state);
        if (_padHeldForBinding)
        {
            _padHeldForBinding = state.Buttons != 0;
            if (_padHeldForBinding) _hints.Used(InputSource.Pad);
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
        // A binding row waiting for a key takes every key, Esc cancelling it, so none moves the focus or goes back.
        // (A pad press that presses a button arrives as a made-up Enter: not a key the player pressed.)
        if (!_pressing && _settingsPage.OfferKeyDown(e))
        {
            e.Handled = true;
            return;
        }
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
                LauncherPage.Achievements => _achievementsPage.EntryControl,
                _ => _setupPage.EntryControl,
            }).Focus(NavigationMethod.Directional);
            return;
        }
        KeyboardNavigationHandler.GetNext(current, direction)?.Focus(NavigationMethod.Directional);
    }
}
