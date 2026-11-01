using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
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
    private string? _notice;
    private readonly BindingCapture _capture = new();
    private readonly Dictionary<string, Control> _rowButtons = [];
    private string? _focusAfterDraw;
    private string? _captureKey;
    private bool _focusKeyboard;

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
        _notice = null;
        // Every change draws the tab again, which drops the control that was just pressed: Draw puts focus back on its twin.
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is RadioButton { Tag: string key } focused)
        {
            _focusAfterDraw = key;
            _focusKeyboard = focused.Classes.Contains(":focus-visible");
        }
        Draw();
    }

    /// <summary>Whether a binding row is waiting for a key or pad button (the window then routes input here, not to the focus).</summary>
    public bool Capturing => _capture.Waiting;

    /// <summary>A key went down anywhere in the window. True when the page was waiting for a key, so the window leaves it alone.</summary>
    public bool OfferKeyDown(KeyEventArgs e)
    {
        var waiting = _capture.Waiting;
        var captured = _capture.KeyDown(e.Key.ToString(), e.KeyModifiers.HasFlag(KeyModifiers.Control),
            e.KeyModifiers.HasFlag(KeyModifiers.Alt), e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        if (waiting)
        {
            _focusKeyboard = true;
            Finish(captured);
        }
        return waiting;
    }

    public void OfferKeyUp(KeyEventArgs e) => _capture.KeyUp(e.Key.ToString());

    /// <summary>The pad was read. True when the page was waiting for a pad button.</summary>
    public bool OfferPad(PadState state)
    {
        var waiting = _capture.Waiting;
        var captured = _capture.PadChanged(state);
        if (waiting)
        {
            _focusKeyboard = true;
            Finish(captured);
        }
        return waiting;
    }

    /// <summary>Called on every poll: a capture that is not answered in time is cancelled.</summary>
    public void Tick()
    {
        if (!_capture.Waiting) return;
        if (!IsVisible) _capture.Cancel();
        else _capture.Expire(Environment.TickCount64);
        if (_capture.Waiting) return;
        _focusAfterDraw = _captureKey;
        Draw();
    }

    // Esc, a press or the timeout ended (or did not end) the capture: write what was pressed, and report what it clashed with.
    private void Finish(CapturedBinding? captured)
    {
        if (_capture.Waiting) return;   // an ignored key or button: keep waiting
        if (captured is not null && _model is { } model)
        {
            var result = model.Bind(captured.Row, captured.Value);
            _error = result.Change == SettingChange.Saved ? null : SettingsStore.Describe(result.Change);
            _notice = result.Clashes.Count == 0 ? null : string.Join(" ", result.Clashes);
        }
        _focusAfterDraw = _captureKey;
        Draw();
    }

    private void Draw()
    {
        if (_model is not { } model) return;
        _rowButtons.Clear();
        StatusText.IsVisible = !string.IsNullOrEmpty(_notice);
        StatusText.Text = _notice;
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
        // The rows are drawn again from the file, so put the focus back on the row the player was using.
        if (_focusAfterDraw is { } key && _rowButtons.TryGetValue(key, out var button))
        {
            var method = _focusKeyboard ? NavigationMethod.Directional : NavigationMethod.Pointer;   // the outline shows only for a keyboard or pad player
            Dispatcher.UIThread.Post(() => button.Focus(method), DispatcherPriority.Background);
        }
        _focusAfterDraw = null;
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
        panel.Children.Add(new TextBlock { Text = row.Label, Classes = { "rowtitle" } });
        if (row.Kind == SettingRowKind.Action)
        {
            var reset = new Button { Content = row.Label, Classes = { "secondary" }, HorizontalAlignment = HorizontalAlignment.Left };
            AutomationProperties.SetName(reset, row.Label);
            reset.Click += (_, _) =>
            {
                _focusKeyboard = reset.Classes.Contains(":focus-visible");
                _capture.Cancel();
                var change = model.Choose(row, "");
                _error = change == SettingChange.Saved ? null : SettingsStore.Describe(change);
                _notice = change == SettingChange.Saved ? "Controls are back to the original layout." : null;
                _focusAfterDraw = row.Key;
                Draw();
            };
            _rowButtons[row.Key] = reset;
            panel.Children.Add(reset);
        }
        else if (row.Remappable)
        {
            var waiting = _capture.Row?.Key == row.Key;
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
            line.Children.Add(new TextBlock { Text = row.ValueLabel.Replace(",", ", "), Classes = { "body" }, VerticalAlignment = VerticalAlignment.Center });
            var change = new Button { Content = waiting ? _capture.Prompt : "Change", Classes = { "secondary" } };
            AutomationProperties.SetName(change, "Change " + row.Label);
            change.Click += (_, _) =>
            {
                _focusKeyboard = change.Classes.Contains(":focus-visible");
                // A second press cancels, so the pad and mouse can back out as well as Esc.
                if (_capture.Waiting && _capture.Row?.Key == row.Key) _capture.Cancel();
                else
                {
                    _capture.Begin(row, Environment.TickCount64);
                    _captureKey = row.Key;
                    _notice = null;
                    _error = null;
                }
                _focusAfterDraw = row.Key;
                Draw();
            };
            _rowButtons[row.Key] = change;
            line.Children.Add(change);
            panel.Children.Add(line);
        }
        else if (row.Editable && row.TextEntry)
        {
            // Saved on Enter or when the box loses focus, not per key: every save draws the tab again.
            var box = new TextBox { Text = row.Value, MaxLength = 15, Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
            AutomationProperties.SetName(box, row.Label);
            var saved = false;
            void Save()
            {
                if (saved || box.Text == row.Value) return;
                saved = true;
                _focusKeyboard = true;
                _focusAfterDraw = row.Key;
                var change = model.Choose(row, box.Text ?? "");
                _error = change == SettingChange.Saved ? null
                    : change == SettingChange.Refused ? "A gamertag is 1 to 15 letters, digits and spaces, starting with a letter."
                    : SettingsStore.Describe(change);
                _notice = change == SettingChange.Saved ? $"{row.Label} saved. It shows the next time the game starts." : null;
                // Leave the handler that fired before the box is replaced.
                Dispatcher.UIThread.Post(Draw);
            }
            box.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter) return;
                e.Handled = true;
                Save();
            };
            box.LostFocus += (_, _) => Save();
            _rowButtons[row.Key] = box;
            panel.Children.Add(box);
        }
        else if (row.Editable)
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
                _rowButtons[(string)radio.Tag] = radio;
                options.Children.Add(radio);
            }
            panel.Children.Add(options);
            // A Preset the player has moved off is Custom: no option is checked, so say it.
            if (row.Kind == SettingRowKind.Preset && row.Options.All(option => option.Value != row.Value))
                panel.Children.Add(new TextBlock { Text = row.ValueLabel, Classes = { "body" } });
        }
        else
        {
            // Shown, not changed: a setting this renderer ignores, or a binding of one that does nothing here.
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
