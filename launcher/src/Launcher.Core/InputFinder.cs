namespace TopSpin4.Launcher.Core;

/// <summary>
/// Finds the player's disc image and TU1 package in the usual places, so a pad-only player
/// (a file picker may not work in Steam Deck Game Mode) usually has nothing to browse for.
/// Candidates are chosen by name only; preparation checks that they are the Reference build.
/// </summary>
public static class InputFinder
{
    /// <summary>Where the player is likely to have put the files: beside the Launcher, in its import folder, Downloads, Desktop.</summary>
    public static IReadOnlyList<string> DefaultFolders(string launcherDirectory)
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        List<string> folders = [launcherDirectory, Path.Combine(launcherDirectory, "import")];
        if (profile.Length > 0)
        {
            folders.Add(Path.Combine(profile, "Downloads"));
            folders.Add(Path.Combine(profile, "Desktop"));
        }
        return folders;
    }

    /// <summary>The <c>.iso</c> files directly in the folders, in folder order.</summary>
    public static IReadOnlyList<string> DiscImages(IEnumerable<string> folders) =>
        Files(folders, name => name.EndsWith(".iso", StringComparison.OrdinalIgnoreCase));

    /// <summary>The Title Update packages directly in the folders: Xbox 360 content named <c>TU_...</c>.</summary>
    public static IReadOnlyList<string> TitleUpdates(IEnumerable<string> folders) =>
        Files(folders, name => name.StartsWith("TU_", StringComparison.OrdinalIgnoreCase));

    private static List<string> Files(IEnumerable<string> folders, Func<string, bool> match)
    {
        List<string> found = [];
        foreach (var folder in folders)
        {
            try
            {
                if (!Directory.Exists(folder)) continue;
                found.AddRange(Directory.EnumerateFiles(folder).Where(path => match(Path.GetFileName(path))).Order(StringComparer.OrdinalIgnoreCase));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // An unreadable folder is just not searched.
            }
        }
        return found;
    }
}
