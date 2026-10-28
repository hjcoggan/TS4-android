namespace TopSpin4.Launcher.Core;

/// <summary>The game exe and the command line the Launcher starts it with.</summary>
public static class LaunchArguments
{
    /// <summary>The game exe, which sits beside the Launcher.</summary>
    public const string GameExeName = "topspin4.exe";

    /// <summary>"topspin4.exe was not found next to the Launcher": the first half of every message about the missing exe.</summary>
    public static string GameExeNotFound => NotFoundNextToLauncher(GameExeName);

    /// <summary>"&lt;file&gt; was not found next to the Launcher", for the files that ship beside it.</summary>
    public static string NotFoundNextToLauncher(string fileName) => $"{fileName} was not found next to the Launcher";

    /// <summary>The game exe beside the Launcher, or null when it is not there.</summary>
    public static string? FindGameExe(string launcherDirectory)
    {
        var path = Path.Combine(launcherDirectory, GameExeName);
        return File.Exists(path) ? path : null;
    }

    /// <summary>The flag that runs one session on all defaults without reading or writing the Settings file.</summary>
    public const string SafeFlag = "--safe";

    /// <summary>
    /// The arguments for Safe start: Play's, plus <c>--safe</c>. The game applies every default and
    /// leaves the Settings file alone; the Launcher writes nothing for it either.
    /// </summary>
    public static IReadOnlyList<string> SafeStart(PreparedData prepared, IEnumerable<string>? extra = null) =>
        Play(prepared, (extra ?? []).Append(SafeFlag));

    /// <summary>
    /// The arguments for Play: the prepared data folders and nothing else. A player setting never
    /// goes on the command line, because a Launch option pins the setting in game; settings go
    /// through the Settings file. <paramref name="extra"/> is for flags that are not settings.
    /// </summary>
    public static IReadOnlyList<string> Play(PreparedData prepared, IEnumerable<string>? extra = null)
    {
        List<string> arguments =
        [
            "--game_data_root=" + prepared.GameDataRoot,
            "--update_data_root=" + prepared.UpdateDataRoot,
        ];
        if (extra is not null) arguments.AddRange(extra);
        return arguments;
    }
}
