namespace TopSpin4.Launcher.Core;

/// <summary>The game exe and the command line the Launcher starts it with.</summary>
public static class LaunchArguments
{
    /// <summary>The game exe, which sits beside the Launcher.</summary>
    public const string GameExeName = "topspin4.exe";

    /// <summary>The game exe beside the Launcher, or null when it is not there.</summary>
    public static string? FindGameExe(string launcherDirectory)
    {
        var path = Path.Combine(launcherDirectory, GameExeName);
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// The arguments for Play: the prepared data folders and nothing else. A player setting never
    /// goes on the command line, because a Launch option pins the setting in game; settings go
    /// through the Settings file. <paramref name="extra"/> is the seam for Safe start (ticket 15),
    /// which adds <c>--safe</c>.
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
