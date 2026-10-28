namespace TopSpin4.Launcher.Core;

public enum SetupPhase
{
    /// <summary>The cache lacks the disc, TU1 or both; the player chooses what is missing.</summary>
    NeedsInputs,
    Preparing,
    /// <summary>The last preparation was refused or stopped; <see cref="SetupFlow.Message"/> says why.</summary>
    Failed,
    /// <summary>The cache holds everything; Play is available.</summary>
    Ready,
}

/// <summary>
/// The Launcher's first-run setup as a model with no UI: which inputs are still needed, what the
/// player chose, and how preparation went. The window only draws it. It asks for just the input
/// the cache lacks, so a preparation that stopped part way resumes.
/// </summary>
public sealed class SetupFlow(Preparation preparation)
{
    public SetupPhase Phase { get; private set; } = preparation.Existing() is null ? SetupPhase.NeedsInputs : SetupPhase.Ready;
    public bool NeedsDisc { get; private set; } = preparation.NeedsDisc;
    public bool NeedsTitleUpdate { get; private set; } = preparation.NeedsTitleUpdate;
    public string? DiscPath { get; private set; }
    public string? TitleUpdatePath { get; private set; }
    public PreparedData? Prepared { get; private set; } = preparation.Existing();
    /// <summary>Why the last preparation failed or stopped, in the player's terms.</summary>
    public string? Message { get; private set; }
    public SetupError? Error { get; private set; }
    public SetupProgress? Progress { get; private set; }

    /// <summary>Raised on whichever thread changed the flow; the window marshals it.</summary>
    public event Action? Changed;

    /// <summary>Preparation can start: the flow is idle and every needed input is chosen.</summary>
    public bool CanPrepare =>
        Phase is SetupPhase.NeedsInputs or SetupPhase.Failed
        && preparation.HasRoomForSetup
        && (!NeedsDisc || !string.IsNullOrWhiteSpace(DiscPath))
        && (!NeedsTitleUpdate || !string.IsNullOrWhiteSpace(TitleUpdatePath));

    public void ChooseDisc(string? path)
    {
        if (Phase == SetupPhase.Preparing) return;
        DiscPath = path;
        Changed?.Invoke();
    }

    public void ChooseTitleUpdate(string? path)
    {
        if (Phase == SetupPhase.Preparing) return;
        TitleUpdatePath = path;
        Changed?.Invoke();
    }

    /// <summary>Checks the inputs and prepares the cache; ends Ready, Failed or, when stopped, NeedsInputs.</summary>
    public async Task PrepareAsync(CancellationToken cancel)
    {
        if (!CanPrepare) return;
        Phase = SetupPhase.Preparing;
        Message = null;
        Error = null;
        Progress = null;
        Changed?.Invoke();
        var progress = new SyncProgress(report =>
        {
            Progress = report;
            Changed?.Invoke();
        });
        try
        {
            Prepared = await Task.Run(() => preparation.Prepare(DiscPath, TitleUpdatePath, progress, cancel), CancellationToken.None);
            Phase = SetupPhase.Ready;
        }
        catch (SetupException problem)
        {
            Phase = SetupPhase.Failed;
            Error = problem.Error;
            Message = problem.Message;
        }
        catch (OperationCanceledException)
        {
            Phase = SetupPhase.NeedsInputs;
            Message = "Stopped. The next preparation resumes where this one left off.";
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Phase = SetupPhase.Failed;
            Message = "Preparation failed: " + problem.Message;
        }
        Progress = null;
        NeedsDisc = preparation.NeedsDisc;
        NeedsTitleUpdate = preparation.NeedsTitleUpdate;
        Changed?.Invoke();
    }

    /// <summary>One line for the progress bar, e.g. "Extracting the disc  42%, 7 min left".</summary>
    public static string Describe(SetupProgress progress)
    {
        var step = progress.Step switch
        {
            SetupStep.CheckingDisc => "Checking the disc image",
            SetupStep.CheckingTitleUpdate => "Checking Title Update 1",
            SetupStep.CheckingExistingFiles => "Checking the files of an earlier, unfinished preparation",
            SetupStep.Extracting => "Extracting the disc",
            SetupStep.UnpackingTitleUpdate => "Unpacking Title Update 1",
            _ => "Done",
        };
        if (progress.Step is not (SetupStep.Extracting or SetupStep.UnpackingTitleUpdate)) return step;
        var line = $"{step}  {(int)(progress.Fraction * 100)}%";
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
