namespace TopSpin4.Launcher.Core;

/// <summary>What went wrong in preparation, in the player's terms.</summary>
public enum SetupError
{
    /// <summary>No disc image was chosen, or the path does not exist.</summary>
    ImageNotFound,
    NotAnXboxImage,
    /// <summary>An Xbox 360 disc, but not the Reference build's (another game, edition or version).</summary>
    WrongDisc,
    /// <summary>No TU1 package was chosen, or the file is not a content package at all.</summary>
    NoTitleUpdate,
    /// <summary>A content package, but not TU1 for this disc (another game, edition or update).</summary>
    WrongTitleUpdate,
    /// <summary>A file of the right input fails verification: the dump is damaged.</summary>
    DamagedFile,
    DiskFull,
    NoPermission,
}

/// <summary>
/// A preparation failure. <see cref="Exception.Message"/> is the full sentence for the log and the
/// command line; <see cref="Reason"/> is what the window shows, in plain words with the next step;
/// <see cref="Details"/> holds the technical part (media IDs, versions) kept behind "Details".
/// </summary>
public sealed class SetupException(SetupError error, string message, Exception? inner = null, string? reason = null, string? details = null)
    : Exception(message, inner)
{
    public SetupError Error { get; } = error;
    public string Reason { get; } = reason ?? message;
    public string? Details { get; } = details;
}

/// <summary>Where a setup row stands.</summary>
public enum RowState
{
    /// <summary>Nothing chosen.</summary>
    Empty,
    /// <summary>The chosen file is being read.</summary>
    Checking,
    Accepted,
    Rejected,
}

/// <summary>
/// One of the setup page's two rows. <see cref="Summary"/> says what an accepted row found;
/// <see cref="Reason"/> says in plain words why a rejected one can't be used and
/// <see cref="Details"/> keeps the technical text apart. <see cref="Needed"/> is false when the
/// prepared files already hold this input, so the row is accepted without a choice.
/// </summary>
public sealed record SetupRow(
    RowState State = RowState.Empty, string? Path = null, string? Summary = null,
    string? Reason = null, string? Details = null, bool Needed = true)
{
    public bool Accepted => State == RowState.Accepted;
}

/// <summary>Where a prepared folder stands.</summary>
public enum SetupStatus
{
    NeedsSetup,
    /// <summary>A previous preparation stopped part way; the next one resumes it.</summary>
    Partial,
    Done,
}

public enum SetupStep
{
    CheckingDisc,
    CheckingTitleUpdate,
    /// <summary>Re-hashing the files an interrupted preparation left, to keep the good ones.</summary>
    CheckingExistingFiles,
    Extracting,
    UnpackingTitleUpdate,
    Done,
}

/// <summary>Progress: the step, bytes done of the total, the current file and the time left.</summary>
public sealed record SetupProgress(SetupStep Step, long BytesDone, long BytesTotal, string? CurrentFile, TimeSpan? TimeLeft)
{
    public double Fraction => BytesTotal > 0 ? (double)BytesDone / BytesTotal : 0;
}

/// <summary>Free space on the drive that holds a folder; tests replace it with a fake.</summary>
public interface IDiskSpace
{
    long FreeBytes(string directory);
}

public sealed class DriveDiskSpace : IDiskSpace
{
    public long FreeBytes(string directory) =>
        new DriveInfo(Path.GetPathRoot(Path.GetFullPath(directory))!).AvailableFreeSpace;
}

/// <summary>The result of checking one picked file: what it is, or why it can't be used.</summary>
public sealed record InputCheck(string? Summary, string? Reason, string? Details, SetupError? Error)
{
    public bool Accepted => Reason is null;

    internal static InputCheck Run(Func<string> check, CancellationToken cancel, string unreadable)
    {
        try
        {
            return new InputCheck(check(), null, null, null);
        }
        catch (SetupException problem)
        {
            var details = problem.Details ?? problem.Message;
            if (problem.InnerException is { } inner && inner.Message != details) details += Environment.NewLine + inner.Message;
            return new InputCheck(null, problem.Reason, details, problem.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            cancel.ThrowIfCancellationRequested();
            return new InputCheck(null, unreadable + ". Check that no other program is using the file, then choose it again.",
                problem.GetType().Name + ": " + problem.Message, null);
        }
    }
}
