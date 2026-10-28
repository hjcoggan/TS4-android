namespace TopSpin4.Launcher.Core;

public enum SetupPhase
{
    /// <summary>The prepared files lack the disc, Title Update 1 or both; the player chooses what is missing.</summary>
    NeedsInputs,
    Preparing,
    /// <summary>The last preparation was refused or stopped; <see cref="SetupFlow.Message"/> says why.</summary>
    Failed,
    /// <summary>The prepared files hold everything; Play is available.</summary>
    Ready,
}

/// <summary>
/// The Launcher's first-run setup as a model with no UI: two rows ("1 Disc", "2 Title Update 1"), each
/// holding the one file the player chose and the result of checking it, and how preparation went.
/// A file is checked the moment it is chosen, and nothing is chosen for the player. The window only
/// draws it. It asks only for the input the prepared files lack, so a setup that stopped part way resumes.
/// </summary>
public sealed class SetupFlow
{
    public const string SetUpLabel = "Set up the game";
    public const string ResumeLabel = "Resume setup";

    private readonly Preparation _preparation;
    private readonly TimeProvider _time;
    private readonly Action<string>? _log;
    private readonly Slot _discCheck = new();
    private readonly Slot _titleUpdateCheck = new();
    private OverallProgress? _overall;
    private string? _stepLabel;

    public SetupFlow(Preparation preparation, TimeProvider? time = null, Action<string>? log = null)
    {
        _preparation = preparation;
        _time = time ?? TimeProvider.System;
        _log = log;
        Prepared = preparation.Existing();
        Phase = Prepared is null ? SetupPhase.NeedsInputs : SetupPhase.Ready;
        Disc = Fresh(preparation.NeedsDisc);
        TitleUpdate = Fresh(preparation.NeedsTitleUpdate);
    }

    public SetupPhase Phase { get; private set; }
    /// <summary>Row 1: the disc image.</summary>
    public SetupRow Disc { get; private set; }
    /// <summary>Row 2: Title Update 1.</summary>
    public SetupRow TitleUpdate { get; private set; }
    public PreparedData? Prepared { get; private set; }
    /// <summary>Why the last preparation failed or stopped, in the player's terms.</summary>
    public string? Message { get; private set; }
    /// <summary>The technical text behind <see cref="Message"/>, shown only behind "Details".</summary>
    public string? MessageDetails { get; private set; }
    public SetupError? Error { get; private set; }
    public SetupProgress? Progress { get; private set; }

    /// <summary>One value for all of setup, 0 to 1, that never goes back.</summary>
    public double ProgressFraction => _overall?.Fraction ?? 0;

    /// <summary>The progress bar can't say how far along it is (a check, or starting).</summary>
    public bool ProgressIsIndeterminate => _overall?.IsIndeterminate ?? true;

    /// <summary>One line under the progress bar: the step, how far all of setup has come and the time left.</summary>
    public string ProgressText
    {
        get
        {
            var step = _stepLabel ?? "Starting";
            if (ProgressIsIndeterminate) return step;
            var line = $"{step}  {(int)(ProgressFraction * 100)}%";
            if (_overall?.TimeLeft is { } left)
                line += left.TotalMinutes >= 1 ? $", {(int)Math.Ceiling(left.TotalMinutes)} min left" : ", under a minute left";
            return line;
        }
    }

    /// <summary>Raised on whichever thread changed the flow; the window marshals it.</summary>
    public event Action? Changed;

    /// <summary>Setup can start: the flow is idle and both rows are accepted.</summary>
    public bool CanPrepare =>
        Phase is SetupPhase.NeedsInputs or SetupPhase.Failed && Disc.Accepted && TitleUpdate.Accepted;

    /// <summary>A stopped or failed setup left files that setup resumes from.</summary>
    public bool CanStartOver => Phase != SetupPhase.Preparing && Phase != SetupPhase.Ready && _preparation.HasPartial;

    /// <summary>"Resume setup" while earlier files are waiting, else "Set up the game".</summary>
    public string PrimaryLabel => CanStartOver ? ResumeLabel : SetUpLabel;

    /// <summary>A file is being read or setup is running, so the rows can't be changed.</summary>
    public bool Busy => Phase == SetupPhase.Preparing;

    /// <summary>Checks the disc image the player chose and shows the result on row 1; null empties the row.</summary>
    public Task ChooseDisc(string? path) =>
        Choose(path, _discCheck, row => Disc = row, Disc.Needed, _preparation.CheckDisc, "Disc image");

    /// <summary>Checks the Title Update 1 file the player chose and shows the result on row 2; null empties the row.</summary>
    public Task ChooseTitleUpdate(string? path) =>
        Choose(path, _titleUpdateCheck, row => TitleUpdate = row, TitleUpdate.Needed, _preparation.CheckTitleUpdate, "Title Update 1");

    private async Task Choose(string? path, Slot slot, Action<SetupRow> set, bool needed,
        Func<string?, CancellationToken, InputCheck> check, string name)
    {
        if (Phase == SetupPhase.Preparing || !needed) return;
        slot.Running?.Cancel();
        if (string.IsNullOrWhiteSpace(path))
        {
            set(new SetupRow());
            Changed?.Invoke();
            return;
        }
        var mine = slot.Running = new CancellationTokenSource();
        set(new SetupRow(RowState.Checking, path));
        Changed?.Invoke();
        InputCheck result;
        try
        {
            result = await Task.Run(() => check(path, mine.Token), CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (mine.IsCancellationRequested) return;
        set(Row(path, result));
        if (!result.Accepted) _log?.Invoke($"{name}: {path} was refused: {result.Reason} {result.Details}");
        Changed?.Invoke();
    }

    private static SetupRow Row(string path, InputCheck result) =>
        result.Accepted
            ? new SetupRow(RowState.Accepted, path, result.Summary)
            : new SetupRow(RowState.Rejected, path, null, result.Reason, result.Details);

    private static SetupRow Fresh(bool needed) =>
        needed ? new SetupRow() : new SetupRow(RowState.Accepted, null, "Already set up", Needed: false);

    /// <summary>Prepares what is missing; ends Ready, Failed or, when stopped, NeedsInputs with the files kept for a resume.</summary>
    public async Task PrepareAsync(CancellationToken cancel)
    {
        if (!CanPrepare) return;
        Phase = SetupPhase.Preparing;
        Message = null;
        MessageDetails = null;
        Error = null;
        Progress = null;
        _stepLabel = null;
        _overall = new OverallProgress(_preparation, Disc.Needed, TitleUpdate.Needed, _time);
        Changed?.Invoke();
        var progress = new SyncProgress(report =>
        {
            Progress = report;
            _stepLabel = StepLabel(report.Step);
            _overall.Update(report);
            Changed?.Invoke();
        });
        try
        {
            Prepared = await Task.Run(() => _preparation.Prepare(Disc.Path, TitleUpdate.Path, progress, cancel), CancellationToken.None);
            Phase = SetupPhase.Ready;
        }
        catch (SetupException problem)
        {
            Phase = SetupPhase.Failed;
            Error = problem.Error;
            Message = problem.Reason;
            MessageDetails = problem.Details;
            _log?.Invoke("Setup failed: " + problem.Message);
            MarkRow(problem);
        }
        catch (OperationCanceledException)
        {
            Phase = SetupPhase.NeedsInputs;
            Message = "Stopped. Resume setup to carry on where it left off, or start over.";
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Phase = SetupPhase.Failed;
            Message = "Setup failed: " + problem.Message;
            MessageDetails = problem.GetType().Name;
            _log?.Invoke("Setup failed: " + problem);
        }
        _overall = null;
        Progress = null;
        RefreshNeeded();
        Changed?.Invoke();
    }

    // An input that failed its check again at setup (it changed, moved or went away since it was accepted) marks its own row.
    private void MarkRow(SetupException problem)
    {
        SetupRow Rejected(SetupRow row) =>
            row with { State = RowState.Rejected, Summary = null, Reason = problem.Reason, Details = problem.Details };
        switch (problem.Error)
        {
            case SetupError.ImageNotFound or SetupError.NotAnXboxImage or SetupError.WrongDisc:
                Disc = Rejected(Disc);
                break;
            case SetupError.NoTitleUpdate or SetupError.WrongTitleUpdate:
                TitleUpdate = Rejected(TitleUpdate);
                break;
        }
    }

    // After a preparation, an input the prepared files now hold needs no choice any more.
    private void RefreshNeeded()
    {
        if (!_preparation.NeedsDisc && Disc.Needed) Disc = Fresh(false);
        if (!_preparation.NeedsTitleUpdate && TitleUpdate.Needed) TitleUpdate = Fresh(false);
    }

    /// <summary>Shows a line on the setup page, e.g. when an action outside setup could not be carried out.</summary>
    public void Notice(string note)
    {
        Message = note;
        MessageDetails = null;
        Changed?.Invoke();
    }

    /// <summary>Throws away what a stopped or failed setup wrote, so the next setup starts from nothing. The chosen files stay.</summary>
    public void StartOver()
    {
        if (!CanStartOver) return;
        _preparation.DiscardPartial();
        Message = null;
        MessageDetails = null;
        Error = null;
        Phase = SetupPhase.NeedsInputs;
        Changed?.Invoke();
    }

    private static string StepLabel(SetupStep step) => step switch
    {
        SetupStep.CheckingDisc => "Checking the disc image",
        SetupStep.CheckingTitleUpdate => "Checking Title Update 1",
        SetupStep.CheckingExistingFiles => "Checking the files of an earlier setup",
        SetupStep.Extracting => "Extracting the disc",
        SetupStep.UnpackingTitleUpdate => "Unpacking Title Update 1",
        _ => "Done",
    };

    /// <summary>The check still running for one row, so a newer choice can cancel it.</summary>
    private sealed class Slot
    {
        public CancellationTokenSource? Running;
    }

    /// <summary>Reports on the reporting thread, unlike Progress&lt;T&gt;, which posts to the thread pool.</summary>
    private sealed class SyncProgress(Action<SetupProgress> report) : IProgress<SetupProgress>
    {
        public void Report(SetupProgress value) => report(value);
    }
}
