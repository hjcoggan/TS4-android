using System.Diagnostics;
using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

public sealed class ShellLinkWriterTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ts4-lnk-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // Reads a shortcut back the way a user's tools do, through WScript.Shell.
    private static string[] ReadBack(string link)
    {
        var script = $"$s = (New-Object -ComObject WScript.Shell).CreateShortcut('{link}'); " +
                     "$s.TargetPath; '[' + $s.Arguments + ']'; $s.WorkingDirectory";
        var start = new ProcessStartInfo("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", script])
        {
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    [Fact]
    public void A_written_shortcut_has_the_target_no_arguments_and_the_working_folder()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Shortcuts are written through the Windows shell.");
        var exe = Path.Combine(_root, "game", "topspin4.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(exe)!);
        File.WriteAllText(exe, "exe");
        var link = Path.Combine(_root, "Top Spin 4.lnk");

        new ShellLinkWriter().Write(link, exe, "", Path.GetDirectoryName(exe)!);

        Assert.Equal([exe, "[]", Path.GetDirectoryName(exe)!], ReadBack(link));
    }
}
