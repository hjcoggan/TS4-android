using Avalonia.Controls;
using Avalonia.Input;
using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Pages;

/// <summary>The home page: draws <see cref="HomeModel"/> and reports which button was pressed.</summary>
public partial class HomePage : UserControl
{
    private readonly HomeModel _home;
    private readonly Dictionary<HomeAction, Button> _buttons = [];
    private IReadOnlyList<HomeAction>? _shownActions;

    public HomePage() : this(new HomeModel(new Preparation(Path.GetTempPath()), null)) { }

    public HomePage(HomeModel home)
    {
        InitializeComponent();
        _home = home;
        TroubleHeading.Text = HomeModel.TroubleStartingHeading;
        PlayButton.Click += (_, _) => PlayPressed?.Invoke();
        foreach (var action in Enum.GetValues<HomeAction>()) Button(action);
        foreach (var action in home.TroubleStartingActions) TroubleHost.Children.Add(_buttons[action]);
        ShowActions();
    }

    public event Action? PlayPressed;
    public event Action<HomeAction>? ActionPressed;

    public Control EntryControl => PlayButton.IsEnabled ? PlayButton : _buttons[_home.Actions[0]];

    private static string Label(HomeAction action) => action switch
    {
        HomeAction.SetUpAgain => "Set up again",
        HomeAction.Settings => "Settings",
        HomeAction.CreateShortcut => "Create shortcut",
        HomeAction.OpenCrashReport => "Open crash report",
        HomeAction.Help => "Help",
        HomeAction.OpenLogs => "Open logs",
        HomeAction.SafeStart => "Safe start",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "A home action with no label"),
    };

    private Button Button(HomeAction action)
    {
        var button = new Button { Content = Label(action) };
        button.Classes.Add("secondary");
        button.Click += (_, _) => ActionPressed?.Invoke(action);
        _buttons[action] = button;
        return button;
    }

    /// <summary>Draws the status; the status is read from the disk each time, so a missing exe shows when the window opens.</summary>
    public void Refresh()
    {
        PlayButton.IsEnabled = _home.CanPlay;
        StatusText.Text = _home.StatusLine;
        NoteText.Text = _home.Note;
        NoteText.IsVisible = _home.Note is not null;
        ShowActions();
        var safe = _buttons[HomeAction.SafeStart];
        safe.IsEnabled = _home.CanPlay;
        // Safe start is the way back into the game after a failure: it moves up under the note.
        var host = _home.SafeStartProminent ? ProminentHost : TroubleHost;
        if (safe.Parent != host)
        {
            (safe.Parent as Panel)?.Children.Remove(safe);
            host.Children.Add(safe);
            if (_home.SafeStartProminent) safe.Classes.Add("attention");
            else safe.Classes.Remove("attention");
        }
    }

    // The labelled actions; the crash report joins them after a crash. Rebuilt only when the list changes, so focus stays put.
    private void ShowActions()
    {
        var wanted = _home.Actions;
        if (_shownActions is not null && _shownActions.SequenceEqual(wanted)) return;
        _shownActions = wanted;
        ActionsHost.Children.Clear();
        foreach (var action in wanted) ActionsHost.Children.Add(_buttons[action]);
    }

    /// <summary>PLAY answers Enter only while this page is the one on show.</summary>
    public void SetDefault(bool isDefault) => PlayButton.IsDefault = isDefault;

    public void FocusFirst() => EntryControl.Focus(NavigationMethod.Tab);
}
