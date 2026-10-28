namespace TopSpin4.Launcher.Core;

/// <summary>What the home screen's status line reports.</summary>
public enum HomeStatus
{
    Ready,
    /// <summary>The game exe is not beside the Launcher, so PLAY cannot start anything.</summary>
    GameExeMissing,
    /// <summary>The prepared folders are gone or incomplete; setup has to run again.</summary>
    GameFilesMissing,
}

/// <summary>The labelled actions of the home screen. PLAY is not one: it is the screen's main button.</summary>
public enum HomeAction
{
    Settings,
    Help,
    OpenLogs,
    SafeStart,
}

/// <summary>
/// The home screen as a model with no UI: the status line and which actions are offered. The
/// status is read from the disk each time it is asked for, so the window shows the missing exe the
/// moment it opens, not when PLAY is pressed, and notices prepared folders that went away.
/// </summary>
public sealed class HomeModel(Preparation preparation, string? gameExe)
{
    public const string TroubleStartingHeading = "Trouble starting?";

    /// <summary>The game exe PLAY starts, or null when there is none beside the Launcher.</summary>
    public string? GameExe { get; } = gameExe;

    public HomeStatus Status =>
        GameExe is null || !File.Exists(GameExe) ? HomeStatus.GameExeMissing
        : preparation.Existing() is null ? HomeStatus.GameFilesMissing
        : HomeStatus.Ready;

    public bool CanPlay => Status == HomeStatus.Ready;

    /// <summary>The prepared folders PLAY points the game at, or null when they are not all there.</summary>
    public PreparedData? Prepared => preparation.Existing();

    /// <summary>One line under PLAY, in the player's terms.</summary>
    public string StatusLine => Status switch
    {
        HomeStatus.Ready => "Ready to play.",
        HomeStatus.GameExeMissing =>
            $"{LaunchArguments.GameExeName} was not found next to the Launcher. Put the Launcher in the game's folder.",
        _ => "The game files are missing. Set up the game again.",
    };

    /// <summary>The actions shown as labelled buttons beside PLAY, in order.</summary>
    public IReadOnlyList<HomeAction> Actions { get; } = [HomeAction.Settings, HomeAction.Help, HomeAction.OpenLogs];

    /// <summary>The recovery actions kept in the small "Trouble starting?" area.</summary>
    public IReadOnlyList<HomeAction> TroubleStartingActions { get; } = [HomeAction.SafeStart];

    /// <summary>The folder Open logs shows: the game's, which the Launcher writes beside (src/crash_report.cpp).</summary>
    public string? LogsFolder => GameExe is null ? null : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(GameExe))!, "logs");

    /// <summary>How the last session ended, when the player should hear about it; null after a normal exit.</summary>
    public string? Note { get; private set; }

    /// <summary>Records how a game session ended.</summary>
    public void SessionEnded(int exitCode) => Note = GameExitReport.Describe(exitCode);

    /// <summary>Shows a line under the status, e.g. when an action could not be carried out.</summary>
    public void Say(string note) => Note = note;
}
