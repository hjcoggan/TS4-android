using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Pages;

/// <summary>The setup page: draws <see cref="SetupFlow"/> and turns clicks into its actions.</summary>
public partial class SetupPage : UserControl
{
    private readonly SetupFlow _flow;
    private CancellationTokenSource? _stop;
    private SetupPhase? _shownPhase;
    private RowState _shownDisc = RowState.Empty;
    private RowState _shownTitleUpdate = RowState.Empty;

    // The designer and the XAML loader need a parameterless constructor.
    public SetupPage() : this(new SetupFlow(new Preparation(Path.GetTempPath()))) { }

    public SetupPage(SetupFlow flow)
    {
        InitializeComponent();
        _flow = flow;
        ChooseDiscButton.Click += async (_, _) =>
        {
            if (await Pick("Choose your Top Spin 4 disc image", [new FilePickerFileType("Disc image") { Patterns = ["*.iso"] }]) is { } path)
                await _flow.ChooseDisc(path);
        };
        ChooseTitleUpdateButton.Click += async (_, _) =>
        {
            if (await Pick("Choose Title Update 1", []) is { } path)
                await _flow.ChooseTitleUpdate(path);
        };
        HelpButton.Click += (_, _) => HelpPressed?.Invoke();
        PrepareButton.Click += async (_, _) => await PrepareAsync();
        StartOverButton.Click += (_, _) => _flow.StartOver();
        CancelButton.Click += (_, _) => Stop();
    }

    /// <summary>"How do I get these files?": the window opens the README's help section.</summary>
    public event Action? HelpPressed;

    /// <summary>Stops a running setup; it resumes from the files already written.</summary>
    public void Stop() => _stop?.Cancel();

    private async Task<string?> Pick(string title, IReadOnlyList<FilePickerFileType> types)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return null;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [.. types, FilePickerFileTypes.All],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    /// <summary>Runs setup from the flow's state (the button does; the window does too for a shader pack that went stale).</summary>
    public async Task PrepareAsync()
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

    // ✓ and ✗ carry the result as well as colour.
    private static void DrawRow(SetupRow row, string emptyPath, TextBlock path, Button choose, TextBlock result,
        Expander details, TextBlock detailsText, bool busy)
    {
        path.Text = row.Path ?? (row.Needed ? emptyPath : "The game files already hold this.");
        choose.IsVisible = row.Needed;
        choose.IsEnabled = !busy;
        result.IsVisible = row.State != RowState.Empty;
        result.Classes.Set("error", row.State == RowState.Rejected);
        result.Classes.Set("body", row.State != RowState.Rejected);
        result.Text = row.State switch
        {
            RowState.Checking => "Checking…",
            RowState.Accepted => "✓ " + row.Summary,
            RowState.Rejected => "✗ " + row.Reason,
            _ => null,
        };
        details.IsVisible = row.State == RowState.Rejected && !string.IsNullOrWhiteSpace(row.Details);
        detailsText.Text = row.Details;
    }

    /// <summary>Draws the flow. Focus moves only when the phase or a row's result changes, so a pad press is never undone.</summary>
    public void Refresh()
    {
        var phase = _flow.Phase;
        var preparing = _flow.Busy;
        DrawRow(_flow.Disc, "No file chosen", DiscPathText, ChooseDiscButton, DiscResult, DiscDetails, DiscDetailsText, preparing);
        DrawRow(_flow.TitleUpdate, "No file chosen", TitleUpdatePathText, ChooseTitleUpdateButton, TitleUpdateResult,
            TitleUpdateDetails, TitleUpdateDetailsText, preparing);

        PrepareButton.IsVisible = !preparing;
        PrepareButton.IsEnabled = _flow.CanPrepare;
        PrepareButton.Content = _flow.PrimaryLabel.ToUpperInvariant();
        StartOverButton.IsVisible = _flow.CanStartOver;
        HelpButton.IsEnabled = true;
        ProgressPanel.IsVisible = preparing;
        SetupMessage.IsVisible = _flow.Message is not null;
        SetupMessage.Text = _flow.Message;
        MessageDetails.IsVisible = _flow.MessageDetails is not null;
        MessageDetailsText.Text = _flow.MessageDetails;
        if (preparing)
        {
            SetupProgressBar.IsIndeterminate = _flow.ProgressIsIndeterminate;
            SetupProgressBar.Value = _flow.ProgressFraction;
            SetupProgressText.Text = _flow.ProgressText;
        }

        // A file that has just been rejected: the player's next move is to choose another one, so focus goes there.
        var rejected = (_flow.Disc.State == RowState.Rejected && _shownDisc != RowState.Rejected ? ChooseDiscButton : null)
            ?? (_flow.TitleUpdate.State == RowState.Rejected && _shownTitleUpdate != RowState.Rejected ? ChooseTitleUpdateButton : null);
        _shownDisc = _flow.Disc.State;
        _shownTitleUpdate = _flow.TitleUpdate.State;
        if (rejected is not null)
            Dispatcher.UIThread.Post(() => rejected.Focus(NavigationMethod.Tab), DispatcherPriority.Background);

        if (phase == _shownPhase) return;
        _shownPhase = phase;
        FocusFirst();
    }

    /// <summary>Focuses the control the player most likely wants next, after the layout has settled.</summary>
    public void FocusFirst() => Dispatcher.UIThread.Post(() =>
    {
        Control first = _flow.Phase switch
        {
            SetupPhase.Preparing => CancelButton,
            _ when _flow.CanPrepare => PrepareButton,
            _ when _flow.Disc.State != RowState.Accepted => ChooseDiscButton,
            _ => ChooseTitleUpdateButton,
        };
        first.Focus(NavigationMethod.Tab);
    }, DispatcherPriority.Background);

    /// <summary>"Set up the game" answers Enter only while this page is the one on show.</summary>
    public void SetDefault(bool isDefault) => PrepareButton.IsDefault = isDefault;

    /// <summary>The control the pad's first press lands on when nothing has focus.</summary>
    public Control EntryControl => PrepareButton.IsEnabled ? PrepareButton : ChooseDiscButton;
}
