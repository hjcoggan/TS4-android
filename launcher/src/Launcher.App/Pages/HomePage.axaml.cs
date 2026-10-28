using Avalonia.Controls;
using Avalonia.Input;
using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Pages;

/// <summary>The home page: draws <see cref="HomeModel"/> and reports which button was pressed.</summary>
public partial class HomePage : UserControl
{
    private readonly HomeModel _home;
    private readonly Dictionary<HomeAction, Button> _buttons = [];

    public HomePage() : this(new HomeModel(new Preparation(Path.GetTempPath()), null)) { }

    public HomePage(HomeModel home)
    {
        InitializeComponent();
        _home = home;
        TroubleHeading.Text = HomeModel.TroubleStartingHeading;
        PlayButton.Click += (_, _) => PlayPressed?.Invoke();
        foreach (var action in home.Actions) ActionsHost.Children.Add(Button(action));
        foreach (var action in home.TroubleStartingActions) TroubleHost.Children.Add(Button(action));
    }

    public event Action? PlayPressed;
    public event Action<HomeAction>? ActionPressed;

    public Control EntryControl => PlayButton.IsEnabled ? PlayButton : (Control)_buttons[HomeAction.Settings];

    private static string Label(HomeAction action) => action switch
    {
        HomeAction.Settings => "Settings",
        HomeAction.Help => "Help",
        HomeAction.OpenLogs => "Open logs",
        _ => "Safe start",
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
        if (_buttons.TryGetValue(HomeAction.SafeStart, out var safe)) safe.IsEnabled = _home.CanPlay;
    }

    /// <summary>PLAY answers Enter only while this page is the one on show.</summary>
    public void SetDefault(bool isDefault) => PlayButton.IsDefault = isDefault;

    public void FocusFirst() => EntryControl.Focus(NavigationMethod.Tab);
}
