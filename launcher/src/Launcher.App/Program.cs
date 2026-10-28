using Avalonia;

namespace TopSpin4.Launcher;

/// <summary>What the command line changes: scripts and tests point the Launcher at other folders.</summary>
internal sealed record LauncherOptions(string? CacheRoot = null, string? GameExe = null, string? UserDataRoot = null)
{
    public const string Usage = "Usage: topspin4-launcher [--cache <folder>] [--game-exe <topspin4.exe>] [--user-data <folder>]";

    public static LauncherOptions Parse(string[] args)
    {
        var options = new LauncherOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : null;
            switch (args[i])
            {
                case "--cache" when value is not null: options = options with { CacheRoot = value }; i++; break;
                case "--game-exe" when value is not null: options = options with { GameExe = value }; i++; break;
                case "--user-data" when value is not null: options = options with { UserDataRoot = value }; i++; break;
                default: throw new ArgumentException($"Unknown or incomplete option: {args[i]}\n{Usage}");
            }
        }
        return options;
    }
}

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            App.Options = LauncherOptions.Parse(args);
        }
        catch (ArgumentException error)
        {
            Console.Error.WriteLine(error.Message);
            return 64;
        }
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Also used by the Avalonia previewer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
