namespace TopSpin4.Launcher.Core;

/// <summary>Where Help points: the README's help section, shipped beside the Launcher.</summary>
public static class HelpDocument
{
    public const string FileName = "README.md";

    /// <summary>The README beside the Launcher, or null when it is not there.</summary>
    public static string? Find(string launcherDirectory)
    {
        var path = Path.Combine(launcherDirectory, FileName);
        return File.Exists(path) ? path : null;
    }
}
