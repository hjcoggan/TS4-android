using System.Diagnostics;

namespace TopSpin4.Launcher.Core;

/// <summary>A process could not be started or did not finish; the message is the player's.</summary>
public sealed class LauncherException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>What to start: the executable, its working folder and its arguments.</summary>
public sealed record ProcessStart(string Executable, string WorkingDirectory, IReadOnlyList<string> Arguments);

/// <summary>Runs one process and hands each standard output line over as it arrives; tests replace it.</summary>
public interface IPrepareRunner
{
    /// <summary>Returns the exit code.</summary>
    /// <exception cref="LauncherException">The process did not start or did not exit in time.</exception>
    Task<int> RunAsync(ProcessStart start, Action<string> onLine, CancellationToken cancel);
}

/// <summary>
/// Runs one of the game's headless modes with its standard output redirected and read line by line (copied from
/// WWE All Stars, ADR-0004). The mode exits by itself. The Launcher never kills the game (a killed GPU process can
/// take the driver down with it): past <paramref name="timeout"/> it asks the game to close, waits
/// <paramref name="closeGrace"/>, then stops waiting and reports it, leaving the process alone. A cancel stops
/// waiting too, also without ending the process.
/// </summary>
public sealed class StreamingPrepareRunner(TimeSpan timeout, string what, TimeSpan? closeGrace = null) : IPrepareRunner
{
    private readonly TimeSpan _closeGrace = closeGrace ?? TimeSpan.FromSeconds(30);

    public async Task<int> RunAsync(ProcessStart start, Action<string> onLine, CancellationToken cancel)
    {
        if (!File.Exists(start.Executable))
            throw new LauncherException($"{LaunchArguments.GameExeNotFound}, so {what} cannot run.");
        var info = new ProcessStartInfo(start.Executable)
        {
            WorkingDirectory = start.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        foreach (var argument in start.Arguments) info.ArgumentList.Add(argument);
        Process process;
        try
        {
            process = Process.Start(info) ?? throw new LauncherException($"The game did not start: {start.Executable}");
        }
        catch (System.ComponentModel.Win32Exception error)
        {
            throw new LauncherException($"The game did not start: {error.Message}", error);
        }
        using (process)
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            limit.CancelAfter(timeout);
            try
            {
                while (await process.StandardOutput.ReadLineAsync(limit.Token).ConfigureAwait(false) is { } line)
                    onLine(line);
                await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
            {
                // Same as the window's X; the mode's window is hidden, so this may find none.
                process.CloseMainWindow();
                using var grace = new CancellationTokenSource(_closeGrace);
                try
                {
                    await process.WaitForExitAsync(grace.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw new LauncherException(
                        $"{what} did not finish within {timeout.TotalMinutes:0} minutes and did not close when asked. " +
                        $"{Path.GetFileName(start.Executable)} may still be running; close it from Task Manager before you play.");
                }
                throw new LauncherException($"{what} did not finish within {timeout.TotalMinutes:0} minutes and was closed.");
            }
            return process.ExitCode;
        }
    }
}
