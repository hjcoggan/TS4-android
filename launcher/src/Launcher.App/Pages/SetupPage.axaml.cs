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
    private readonly IReadOnlyList<string> _discs;
    private readonly IReadOnlyList<string> _titleUpdates;
    private CancellationTokenSource? _stop;
    private SetupPhase? _shownPhase;

    // The designer and the XAML loader need a parameterless constructor.
    public SetupPage() : this(new SetupFlow(new Preparation(Path.GetTempPath())), [], []) { }

    public SetupPage(SetupFlow flow, IReadOnlyList<string> discs, IReadOnlyList<string> titleUpdates)
    {
        InitializeComponent();
        _flow = flow;
        _discs = discs;
        _titleUpdates = titleUpdates;
        ChooseDiscButton.Click += async (_, _) => _flow.ChooseDisc(await Pick("Choose your Top Spin 4 disc image", "*.iso") ?? _flow.DiscPath);
        ChooseTitleUpdateButton.Click += async (_, _) => _flow.ChooseTitleUpdate(await Pick("Choose Title Update 1", null) ?? _flow.TitleUpdatePath);
        NextDiscButton.Click += (_, _) => _flow.ChooseDisc(Next(_discs, _flow.DiscPath));
        NextTitleUpdateButton.Click += (_, _) => _flow.ChooseTitleUpdate(Next(_titleUpdates, _flow.TitleUpdatePath));
        PrepareButton.Click += async (_, _) => await Prepare();
        CancelButton.Click += (_, _) => Stop();
    }

    /// <summary>Stops a running setup; it resumes from the files already written.</summary>
    public void Stop() => _stop?.Cancel();

    private static string? Next(IReadOnlyList<string> candidates, string? current)
    {
        if (candidates.Count == 0) return current;
        var index = candidates.ToList().FindIndex(path => path == current);
        return candidates[(index + 1) % candidates.Count];
    }

    private async Task<string?> Pick(string title, string? pattern)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return null;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
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

    /// <summary>Draws the flow. Focus moves only when the phase changes, so a pad press is never undone.</summary>
    public void Refresh()
    {
        var phase = _flow.Phase;
        var preparing = phase == SetupPhase.Preparing;
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
        FocusFirst();
    }

    /// <summary>Focuses the control the player most likely wants next, after the layout has settled.</summary>
    public void FocusFirst() => Dispatcher.UIThread.Post(() =>
    {
        Control first = _flow.Phase switch
        {
            SetupPhase.Preparing => CancelButton,
            _ when _flow.CanPrepare => PrepareButton,
            _ when _flow.NeedsDisc && _flow.DiscPath is null => ChooseDiscButton,
            _ => ChooseTitleUpdateButton,
        };
        first.Focus(NavigationMethod.Tab);
    }, DispatcherPriority.Background);

    /// <summary>The control the pad's first press lands on when nothing has focus.</summary>
    public Control EntryControl => PrepareButton.IsEnabled ? PrepareButton : ChooseDiscButton;
}
