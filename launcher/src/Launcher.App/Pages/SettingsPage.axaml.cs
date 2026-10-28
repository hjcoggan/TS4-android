using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Pages;

/// <summary>
/// The settings page: draws <see cref="SettingsPageModel"/>, one tab per section of the game's
/// Settings page. The model is read from the game's export the first time the page opens (a short
/// run of the game exe) and kept; every change is written to the Settings file at once and the
/// page is drawn again from the file, so what it shows is what the game will read.
/// </summary>
public partial class SettingsPage : UserControl
{
    private readonly Func<Task<SettingsPageModel>> _load;
    private SettingsPageModel? _model;
    private bool _loading;
    private string? _error;

    public SettingsPage() : this(() => Task.FromResult(SettingsPageModel.Unavailable(""))) { }

    public SettingsPage(Func<Task<SettingsPageModel>> load)
    {
        InitializeComponent();
        _load = load;
        BackButton.Click += (_, _) => BackPressed?.Invoke();
        ResetButton.Click += (_, _) =>
        {
            if (_model is { } model) Apply(model.ResetToDefaults());
        };
    }

    public event Action? BackPressed;

    public Control EntryControl => BackButton;

    public void FocusFirst() => BackButton.Focus(NavigationMethod.Tab);

    /// <summary>Shows the page, reading the game's settings export on first use.</summary>
    public async Task OpenAsync()
    {
        if (_model is null && !_loading)
        {
            _loading = true;
            StatusText.Text = "Reading the game's settings…";
            StatusText.IsVisible = true;
            _model = await _load();
            _loading = false;
        }
        Draw();
    }

    // The change was written (or refused): say why when it was not, and show what the file holds now.
    private void Apply(SettingChange change)
    {
        _error = change == SettingChange.Saved ? null : SettingsStore.Describe(change);
        // Every change draws the tab again, which drops the control that was just pressed: put focus back on its twin.
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as RadioButton;
        var key = focused?.Tag as string;
        var keyboard = focused?.Classes.Contains(":focus-visible") == true;
        Draw();
        if (key is not null) Dispatcher.UIThread.Post(() => RestoreFocus(key, keyboard), DispatcherPriority.Background);
    }

    // The radio button with this identity in the tab as drawn now; the outline shows only for a keyboard or pad player.
    private void RestoreFocus(string key, bool keyboard)
    {
        var twin = Tabs.GetVisualDescendants().OfType<RadioButton>().FirstOrDefault(radio => radio.Tag as string == key);
        twin?.Focus(keyboard ? NavigationMethod.Directional : NavigationMethod.Pointer);
    }

    private void Draw()
    {
        if (_model is not { } model) return;
        StatusText.IsVisible = false;
        var error = model.UnavailableReason ?? _error;
        ErrorText.IsVisible = !string.IsNullOrEmpty(error);
        ErrorText.Text = error;
        InGameNoteText.Text = model.InGameNote;
        var tabs = model.Tabs;
        var selected = Tabs.SelectedIndex;
        ResetButton.IsVisible = tabs.Count > 0;
        Tabs.IsVisible = tabs.Count > 0;
        Tabs.ItemsSource = tabs.Select(tab => new TabItem { Header = tab.Name, Content = Rows(model, tab) }).ToList();
        Tabs.SelectedIndex = tabs.Count == 0 ? -1 : Math.Clamp(selected, 0, tabs.Count - 1);
    }

    private Control Rows(SettingsPageModel model, SettingsTab tab)
    {
        var rows = new StackPanel { Spacing = 12, Margin = new Avalonia.Thickness(0, 14, 0, 0) };
        foreach (var row in tab.Rows) rows.Children.Add(Row(model, row));
        return rows;
    }

    private Control Row(SettingsPageModel model, SettingRow row)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock { Text = row.Label, FontSize = 22, FontWeight = FontWeight.Bold, Foreground = Brushes.White });
        if (row.Editable)
        {
            var options = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            foreach (var option in row.Options)
            {
                var radio = new RadioButton
                {
                    Content = option.Label, GroupName = row.Key, IsChecked = option.Value == row.Value,
                    Tag = row.Key + "=" + option.Value,
                };
                radio.Click += (_, _) => Apply(model.Choose(row, option.Value));
                options.Children.Add(radio);
            }
            panel.Children.Add(options);
            // A Preset the player has moved off is Custom: no option is checked, so say it.
            if (row.Kind == SettingRowKind.Preset && row.Options.All(option => option.Value != row.Value))
                panel.Children.Add(new TextBlock { Text = row.ValueLabel, Classes = { "body" } });
        }
        else
        {
            // Shown, not changed here: a binding (remapping is the next page's job) or a setting this renderer ignores.
            panel.Children.Add(new TextBlock { Text = row.ValueLabel, Classes = { "body" } });
        }
        if (row.Note is not null) panel.Children.Add(new TextBlock { Text = row.Note, Classes = { "small" }, TextAlignment = TextAlignment.Left });
        panel.Children.Add(new TextBlock { Text = row.Help, Classes = { "small" }, TextAlignment = TextAlignment.Left });
        return new Border
        {
            Background = (IBrush)this.FindResource("PanelBrush")!, CornerRadius = new Avalonia.CornerRadius(4),
            Padding = new Avalonia.Thickness(18), Child = panel, Opacity = row.Available ? 1 : 0.6,
        };
    }
}
