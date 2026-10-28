using System.Diagnostics;

namespace TopSpin4.Launcher.Core;

/// <summary>Runs the game exe to get its settings export.</summary>
public static class SettingsExport
{
    public const string Switch = "--print-settings-schema";

    /// <summary>
    /// Runs <c>topspin4.exe --print-settings-schema</c> with standard output redirected to a pipe and
    /// parses what it prints. The exe is a GUI-subsystem program; it prints and exits before any
    /// window opens or any game data is needed.
    /// </summary>
    public static async Task<SettingsSchema> ReadAsync(string gameExe, TimeSpan? timeout = null, CancellationToken cancel = default)
    {
        var start = new ProcessStartInfo(gameExe)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(gameExe))!,
        };
        start.ArgumentList.Add(Switch);

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        limit.CancelAfter(timeout ?? TimeSpan.FromSeconds(20));
        Process process;
        try
        {
            process = Process.Start(start) ?? throw new SettingsExportException("The game could not be started to read its settings.");
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new SettingsExportException("The game could not be started to read its settings: " + error.Message, error);
        }
        using (process)
        {
            try
            {
                var output = process.StandardOutput.ReadToEndAsync(limit.Token);
                var errors = process.StandardError.ReadToEndAsync(limit.Token);
                await process.WaitForExitAsync(limit.Token);
                var text = await output;
                await errors;
                if (process.ExitCode != 0)
                    throw new SettingsExportException($"The game exited with code {process.ExitCode} when asked for its settings.");
                return SettingsSchema.Parse(text);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(); } catch (InvalidOperationException) { }
                throw new SettingsExportException("The game did not print its settings in time.");
            }
        }
    }
}
