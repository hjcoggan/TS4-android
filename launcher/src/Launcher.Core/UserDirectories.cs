using System.Runtime.InteropServices;

namespace TopSpin4.Launcher.Core;

/// <summary>
/// Per-user folders. Prepared data (~5.7 GB, derivable again from the disc) goes under
/// <c>%LOCALAPPDATA%</c>, apart from saves and settings, which will go under <c>%APPDATA%</c>
/// (spec, "Saves"). Under Proton the Windows folders sit inside the game's Wine prefix, so
/// the Linux user's XDG data folder is used instead, as .NET itself maps LocalApplicationData
/// on Linux, and reached through Wine's Z: drive.
/// </summary>
public static class UserDirectories
{
    public const string FolderName = "TopSpin4Recomp";

    /// <summary>Where this user's prepared data lives on this machine.</summary>
    public static string PreparedDataRoot() =>
        PreparedDataRoot(System.Environment.GetEnvironmentVariable, UnderWine(),
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData));

    public static string PreparedDataRoot(Func<string, string?> environment, bool underWine, string localApplicationData)
    {
        if (underWine && XdgDataHome(environment) is { } unix)
            return "Z:" + unix.TrimEnd('/').Replace('/', '\\') + '\\' + FolderName;
        return Path.Combine(localApplicationData, FolderName);
    }

    // XDG_DATA_HOME when it is absolute (the spec says to ignore a relative one), else ~/.local/share.
    private static string? XdgDataHome(Func<string, string?> environment)
    {
        if (environment("XDG_DATA_HOME") is { } data && data.StartsWith('/')) return data;
        return environment("HOME") is { } home && home.StartsWith('/') ? home.TrimEnd('/') + "/.local/share" : null;
    }

    // Wine's ntdll exports wine_get_version; Windows' does not.
    private static bool UnderWine() =>
        OperatingSystem.IsWindows() && NativeLibrary.TryLoad("ntdll.dll", out var ntdll)
        && NativeLibrary.TryGetExport(ntdll, "wine_get_version", out _);
}
