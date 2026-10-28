using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Pages;

/// <summary>
/// The settings page: draws <see cref="SettingsPageModel"/>. The model is read from the game's
/// export the first time the page opens (a short run of the game exe) and kept.
/// </summary>
public partial class SettingsPage : UserControl
{
    private readonly Func<Task<SettingsPageModel>> _load;
    private SettingsPageModel? _model;
    private bool _loading;

    public SettingsPage() : this(() => Task.FromResult(SettingsPageModel.Unavailable(""))) { }

    public SettingsPage(Func<Task<SettingsPageModel>> load)
    {
        InitializeComponent();
        _load = load;
        BackButton.Click += (_, _) => BackPressed?.Invoke();
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

    private void Draw()
    {
        if (_model is not { } model) return;
        StatusText.IsVisible = false;
        ErrorText.IsVisible = model.UnavailableReason is not null;
        ErrorText.Text = model.UnavailableReason;
        InGameNoteText.Text = model.InGameNote;
        RowsHost.Children.Clear();
        foreach (var row in model.Rows) RowsHost.Children.Add(Row(model, row));
    }

    private Control Row(SettingsPageModel model, SettingRow row)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock { Text = row.Label, FontSize = 22, FontWeight = FontWeight.Bold, Foreground = Brushes.White });
        var options = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        foreach (var option in row.Options)
        {
            var radio = new RadioButton { Content = option.Label, GroupName = row.Setting.Key, IsChecked = option.Value == row.Value };
            radio.Click += (_, _) =>
            {
                var change = model.Choose(row, option.Value);
                ErrorText.Text = BeforeStartSettings.Describe(change);
                ErrorText.IsVisible = change != SettingChange.Saved;
                // Show what the file now holds, whatever was written.
                var current = model.Rows.Single(candidate => candidate.Setting.Key == row.Setting.Key).Value;
                foreach (var other in options.Children.OfType<RadioButton>())
                    other.IsChecked = (string?)other.Tag == current;
            };
            radio.Tag = option.Value;
            options.Children.Add(radio);
        }
        panel.Children.Add(options);
        var help = new TextBlock { Text = row.Help, TextAlignment = TextAlignment.Left };
        help.Classes.Add("small");
        panel.Children.Add(help);
        return new Border { Background = (IBrush)this.FindResource("PanelBrush")!, CornerRadius = new Avalonia.CornerRadius(4), Padding = new Avalonia.Thickness(18), Child = panel };
    }
}
