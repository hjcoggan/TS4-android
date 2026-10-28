namespace TopSpin4.Launcher.Core;

/// <summary>
/// The "Game files go to … [Change…]" line as a model with no UI: where the prepared files are
/// or will be, the free space there against what setup needs, and changing the place. Before
/// setup a change only points setup at the new folder; after setup it moves the prepared files
/// (<see cref="GameFilesMove"/>). Either way the new place goes into the location file, which the
/// game reads. The setup page and the home screen both draw this one model.
/// </summary>
public sealed class GameFilesLocationModel(Preparation preparation, GameFilesLocation store)
{
    private const double Gigabyte = 1 << 30;

    /// <summary>The folder that holds the game files.</summary>
    public string Path => preparation.CacheRoot;

    /// <summary>Setup has run: a change moves files instead of only choosing a place.</summary>
    public bool FilesPrepared => preparation.Existing() is not null;

    /// <summary>
    /// The place can be changed now: neither setup nor a move is running, and the files are either
    /// all there (a move) or none of them (a choice). With only one of the two folders prepared the
    /// place stays until setup has written the other.
    /// </summary>
    public bool CanChange => !preparation.Busy && !Moving
        && (FilesPrepared || (preparation.NeedsDisc && preparation.NeedsTitleUpdate));

    public bool Moving { get; private set; }

    /// <summary>Progress of the move that is running, else null.</summary>
    public SetupProgress? Progress { get; private set; }

    /// <summary>Why the last change did not happen, or a note about it; null otherwise.</summary>
    public string? Message { get; private set; }

    /// <summary>Raised on whichever thread changed the model; the window marshals it.</summary>
    public event Action? Changed;

    /// <summary>Setup would fit: the drive has what it still has to write, plus some head room.</summary>
    public bool EnoughSpace => FilesPrepared || preparation.HasRoomForSetup;

    /// <summary>"42.9 GB free on D:\; setup needs about 6.2 GB", for the line under the place.</summary>
    public string SpaceLine
    {
        get
        {
            var free = $"{preparation.FreeBytes / Gigabyte:0.#} GB free on {System.IO.Path.GetPathRoot(Path)}";
            return FilesPrepared ? free : $"{free}; setup needs about {preparation.NeededBytes / Gigabyte:0.#} GB";
        }
    }

    /// <summary>A warning, shown before setup starts, when the drive cannot hold the game files; else null.</summary>
    public string? Warning => EnoughSpace ? null
        : $"Not enough free space on {System.IO.Path.GetPathRoot(Path)}: setup needs about {preparation.NeededBytes / Gigabyte:0.#} GB " +
          $"and {preparation.FreeBytes / Gigabyte:0.#} GB is free. Free some space or choose another place.";

    /// <summary>
    /// Before setup: points setup at <paramref name="folder"/> and records it. Nothing is written
    /// there until setup runs.
    /// </summary>
    public void Choose(string folder)
    {
        if (!CanChange || FilesPrepared) return;
        preparation.Relocate(folder);
        store.Record(preparation.CacheRoot);
        Message = null;
        Changed?.Invoke();
    }

    /// <summary>
    /// Changes the place to <paramref name="folder"/>: before setup by choosing it, after setup by
    /// moving the prepared files there. A move that stops or fails leaves the files where they were
    /// and says so in <see cref="Message"/>.
    /// </summary>
    public async Task ChangeAsync(string folder, CancellationToken cancel)
    {
        if (!CanChange) return;
        if (!FilesPrepared)
        {
            Choose(folder);
            return;
        }
        Moving = true;
        Message = null;
        Progress = null;
        Changed?.Invoke();
        var progress = new SyncProgress(report =>
        {
            Progress = report;
            Changed?.Invoke();
        });
        try
        {
            var removed = await Task.Run(() => preparation.Move(folder, store.Record, progress, cancel), CancellationToken.None);
            if (!removed) Message = "The game files are in their new place, but part of the old copy could not be removed. You can delete it yourself.";
        }
        catch (SetupException problem)
        {
            Message = problem.Message;
        }
        catch (OperationCanceledException)
        {
            Message = "Stopped. The game files stay where they were.";
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Message = "The game files could not be moved, and stay where they were: " + problem.Message;
        }
        Moving = false;
        Progress = null;
        Changed?.Invoke();
    }

    /// <summary>One line for the progress bar, e.g. "Moving the game files  42%, 3 min left".</summary>
    public static string Describe(SetupProgress progress)
    {
        if (progress.Step == SetupStep.CheckingExistingFiles) return "Checking files from an earlier, unfinished move";
        var line = $"Moving the game files  {(int)(progress.Fraction * 100)}%";
        if (progress.TimeLeft is { } left)
            line += left.TotalMinutes >= 1 ? $", {(int)Math.Ceiling(left.TotalMinutes)} min left" : ", under a minute left";
        return line;
    }

    /// <summary>Reports on the reporting thread, unlike Progress&lt;T&gt;, which posts to the thread pool.</summary>
    private sealed class SyncProgress(Action<SetupProgress> report) : IProgress<SetupProgress>
    {
        public void Report(SetupProgress value) => report(value);
    }
}
