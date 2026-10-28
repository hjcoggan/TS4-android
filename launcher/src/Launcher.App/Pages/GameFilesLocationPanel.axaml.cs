using Avalonia.Controls;
using Avalonia.Platform.Storage;
using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Pages;

/// <summary>
/// The "Game files go to … [Change…]" line: draws <see cref="GameFilesLocationModel"/> and turns
/// Change… into a folder picker and then <see cref="GameFilesLocationModel.ChangeAsync"/>.
/// </summary>
public partial class GameFilesLocationPanel : UserControl
{
    private readonly GameFilesLocationModel _model;
    private CancellationTokenSource? _stop;

    // The designer and the XAML loader need a parameterless constructor.
    public GameFilesLocationPanel() : this(new GameFilesLocationModel(new Preparation(Path.GetTempPath()), new GameFilesLocation(Path.GetTempPath()))) { }

    public GameFilesLocationPanel(GameFilesLocationModel model)
    {
        InitializeComponent();
        _model = model;
        ChangeButton.Click += async (_, _) => await Change();
        StopMoveButton.Click += (_, _) => _stop?.Cancel();
    }

    public Control EntryControl => ChangeButton;

    private async Task Change()
    {
        if (!_model.CanChange || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose where the game files go",
            AllowMultiple = false,
        });
        if (folders.Count == 0 || folders[0].TryGetLocalPath() is not { } folder) return;
        // The game files go into a folder of their own, so the pick is never filled with them.
        var place = Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any()
            ? Path.Combine(folder, "TopSpin4Recomp") : folder;
        _stop = new CancellationTokenSource();
        try
        {
            await _model.ChangeAsync(place, _stop.Token);
        }
        finally
        {
            _stop.Dispose();
            _stop = null;
        }
    }

    /// <summary>Draws the model. The free space is only asked while nothing is being written.</summary>
    public void Refresh()
    {
        var idle = _model.CanChange;
        var prepared = _model.FilesPrepared;
        PathText.Text = (prepared ? "Game files are in " : "Game files go to ") + _model.Path;
        SpaceText.IsVisible = idle;
        WarningText.IsVisible = idle && _model.Warning is not null;
        if (idle)
        {
            SpaceText.Text = _model.SpaceLine;
            WarningText.Text = _model.Warning;
        }
        ChangeButton.IsEnabled = idle;
        MessageText.IsVisible = _model.Message is not null;
        MessageText.Text = _model.Message;
        MoveProgressPanel.IsVisible = _model.Moving;
        if (_model.Progress is { } progress)
        {
            MoveProgressBar.Value = progress.Fraction;
            MoveProgressText.Text = GameFilesLocationModel.Describe(progress);
        }
        else if (_model.Moving)
        {
            MoveProgressBar.Value = 0;
            MoveProgressText.Text = "Starting";
        }
    }
}
