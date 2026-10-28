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
    /// <summary>The place chosen for the game files cannot hold them (inside the old place, or the same one).</summary>
    BadPlace,
}

/// <summary>A preparation failure with a plain message that says what to do.</summary>
public sealed class SetupException(SetupError error, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public SetupError Error { get; } = error;
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
    /// <summary>Copying the prepared folders to a new place (<see cref="Preparation.Move"/>).</summary>
    MovingFiles,
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
