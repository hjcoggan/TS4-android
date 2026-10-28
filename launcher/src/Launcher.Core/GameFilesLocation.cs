namespace TopSpin4.Launcher.Core;

/// <summary>
/// The small location file in the per-user folder that says where the prepared game files are
/// when the player moved them off the default folder. The game reads the same file when it looks
/// for prepared data (src/user_paths.h): the command line first, then this file, then the default
/// prepared-data folder. One line: the folder that holds <c>gamedata</c> and <c>updatedata</c>. The
/// game's shader cache (<c>cache</c>) stays beside them, in the same folder, and moves with them.
/// </summary>
public sealed class GameFilesLocation(string userFolder)
{
    public const string FileName = "game-files-location.txt";

    public string FilePath => Path.Combine(userFolder, FileName);

    /// <summary>The recorded folder, or null when none is recorded or the file does not hold an absolute path.</summary>
    public string? Recorded
    {
        get
        {
            try
            {
                var line = File.ReadLines(FilePath).Select(text => text.Trim()).FirstOrDefault(text => text.Length > 0);
                // Fully qualified, as the game's is_absolute() asks (src/user_paths.h): "\folder" and "C:folder" are not.
                return line is not null && Path.IsPathFullyQualified(line) ? line : null;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    /// <summary>The folder to use: the recorded one, else <paramref name="defaultRoot"/>.</summary>
    public string Current(string defaultRoot) => Recorded ?? defaultRoot;

    /// <summary>
    /// The folder the prepared files are in or go to, as a full path: the command line's when it gave
    /// one, else the recorded place, else <paramref name="defaultRoot"/>. The Launcher window and
    /// topspin4-setup both look it up this way, as the game does.
    /// </summary>
    public string Resolve(string? commandLine, string defaultRoot) => Path.GetFullPath(commandLine ?? Current(defaultRoot));

    /// <summary>Records <paramref name="root"/>; the file is replaced whole, so a reader never sees half a path.</summary>
    public void Record(string root)
    {
        Directory.CreateDirectory(userFolder);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, root + "\n");
        File.Move(temp, FilePath, overwrite: true);
    }
}
