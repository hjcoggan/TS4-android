namespace TopSpin4.Launcher.Core;

/// <summary>
/// The portable install (ADR-0009): every default folder is resolved from the folder the Launcher
/// runs from, which holds <c>topspin4.exe</c> too. The prepared disc and Title Update 1
/// (<c>gamedata</c>, <c>updatedata</c>, ~5.7 GB, derivable again from the disc) and the game's
/// shader cache (<c>cache</c>) sit directly in it; the Settings file, saves, achievements, the
/// disc art and the game files location file go in <c>userdata</c>. Nothing defaults to
/// <c>%APPDATA%</c>, <c>%LOCALAPPDATA%</c> or the XDG data folder, on Windows or under Proton, so
/// the same text in the game's src/user_paths.h applies (keep the folder names equal). The command
/// line (<c>--user-data</c>, <c>--cache</c>) and a recorded game files location still win.
/// </summary>
public static class UserDirectories
{
    public const string UserDataFolderName = "userdata";

    /// <summary>Where the prepared data lives: the folder the Launcher runs from.</summary>
    public static string PreparedDataRoot(string launcherDirectory) => Install(launcherDirectory);

    /// <summary>Where the game keeps its Settings file and saves, and the Launcher its art and location file.</summary>
    public static string UserDataRoot(string launcherDirectory) => Path.Combine(Install(launcherDirectory), UserDataFolderName);

    /// <summary>The disc art folder inside a user data folder.</summary>
    public static string ArtFolder(string userDataRoot) => Path.Combine(userDataRoot, DiscArt.FolderName);

    /// <summary>
    /// The player's reason a folder cannot be used, or null when it can be written to. The folder is made
    /// if missing and a file is written in it and removed again. There is no fallback to the user's own
    /// folders, so an install never ends up split between two places.
    /// </summary>
    public static string? Refusal(string folder)
    {
        if (CanWrite(folder)) return null;
        return $"This folder can't be written to: {folder}. Move the game folder somewhere you can write to, " +
               "such as your Documents or a games drive, and start the Launcher again.";
    }

    private static string Install(string launcherDirectory) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(launcherDirectory));

    private static bool CanWrite(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var probe = Path.Combine(folder, ".topspin4-write-test");
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return false;
        }
    }
}
