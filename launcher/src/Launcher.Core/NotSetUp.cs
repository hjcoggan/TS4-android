namespace TopSpin4.Launcher.Core;

/// <summary>
/// The game started without setup done (no game folders on its command line and the prepared disc
/// or Title Update 1 not found) starts the Launcher with <c>--not-set-up &lt;reason&gt;</c>
/// (src/user_paths.h, SetupNeededReason; keep the tokens equal) and exits. The Launcher opens on
/// the setup page showing this line.
/// </summary>
public static class NotSetUp
{
    public const string Flag = "--not-set-up";

    public static IReadOnlyList<string> Arguments(string reason) => [Flag, reason];

    public static string Describe(string reason) => reason switch
    {
        "disc" => "The game could not find the prepared disc files. Choose your disc image to set up again.",
        "title-update" => "The game could not find the prepared Title Update 1 files. Choose the Title Update 1 file to set up again.",
        "disc-and-title-update" => "The game has not been set up yet. Choose your disc image and Title Update 1 below.",
        _ => "The game needs to be set up before it can start.",
    };
}
