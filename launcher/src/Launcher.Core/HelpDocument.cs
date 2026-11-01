namespace TopSpin4.Launcher.Core;

/// <summary>Where Help points: HELP.md, the player help shipped beside the Launcher.</summary>
public static class HelpDocument
{
    public const string FileName = "HELP.md";

    /// <summary>HELP.md beside the Launcher, or null when it is not there.</summary>
    public static string? Find(string launcherDirectory)
    {
        var path = Path.Combine(launcherDirectory, FileName);
        return File.Exists(path) ? path : null;
    }
}
