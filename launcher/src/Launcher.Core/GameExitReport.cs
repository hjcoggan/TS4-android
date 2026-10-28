using System.Globalization;

namespace TopSpin4.Launcher.Core;

/// <summary>How a game session ended, as the player needs to hear it.</summary>
public enum GameExitKind
{
    /// <summary>Exit code 0.</summary>
    Closed,
    /// <summary>The GPU or driver stopped working under the Native renderer and the game ended the session.</summary>
    GpuFailure,
    /// <summary>Any other exit: a crash, or the process was ended from outside.</summary>
    Crashed,
}

/// <summary>
/// What the Launcher tells the player when the game exits: read from the exit code and, for the
/// crash report link, the session log (classification copied from WWE All Stars' Launcher, ADR-0004).
/// </summary>
public sealed record GameExitReport(GameExitKind Kind, int ExitCode, string? Message, string? CrashReportPath)
{
    /// <summary>
    /// The exit code the game ends with when the GPU or driver stopped working under the Native
    /// renderer: "GPUF" in ASCII (kGpuFailureExitCode in src/native/gpu_failure.h; keep the two).
    /// </summary>
    public const int GpuFailureExitCode = 0x47505546;

    // The game logs "crash report: saved <path>" after it writes a crash report
    // (src/crash_report.cpp). Keep the wording.
    private const string CrashReportMarker = "crash report: saved ";

    /// <summary>Plain words for the Windows exception codes a crashing game exits with.</summary>
    private static readonly Dictionary<uint, string> CrashCodes = new()
    {
        [0xC0000005] = "it read or wrote memory it does not own",
        [0xC000001D] = "it ran an instruction this CPU does not have",
        [0xC0000094] = "it divided by zero",
        [0xC00000FD] = "it ran out of stack",
        [0xC0000374] = "its memory heap was corrupted",
        [0xC0000409] = "it stopped itself after detecting a fatal error",
        [0x80000003] = "it hit a debug breakpoint",
        [0xE06D7363] = "an error was not handled",
        // Raised by the game's crash handler (kAbortCode and the two after it in src/crash_report_plan.h).
        [0xE0545301] = "it gave up after an internal error (abort)",
        [0xE0545302] = "it called a C++ function that no longer exists",
        [0xE0545303] = "it passed a bad value to the C runtime",
    };

    /// <summary>
    /// Classifies an exit. <paramref name="sessionLogPath"/> is the game's newest session log, read
    /// for the crash report the game saved; the report counts only while its file is still there.
    /// </summary>
    public static GameExitReport FromExit(int exitCode, string? sessionLogPath)
    {
        if (exitCode == 0) return new GameExitReport(GameExitKind.Closed, exitCode, null, null);
        if (exitCode == GpuFailureExitCode)
            return new GameExitReport(GameExitKind.GpuFailure, exitCode,
                "The graphics card or its driver stopped working while the game was running. Switch the renderer in Settings, then press Play again.",
                null);
        var report = sessionLogPath is null ? null : LastCrashReport(sessionLogPath);
        var message = CrashMessage(exitCode);
        if (report is not null) message += " The game saved a crash report: Open crash report to attach it to your GitHub issue.";
        return new GameExitReport(GameExitKind.Crashed, exitCode, message, report);
    }

    /// <summary>The newest <c>topspin4_NNN.log</c> in the game's logs folder (the current session's), or null.</summary>
    public static string? NewestSessionLog(string logsFolder)
    {
        if (!Directory.Exists(logsFolder)) return null;
        return Directory.EnumerateFiles(logsFolder, "topspin4_*.log")
            .OrderByDescending(path => Path.GetFileName(path), StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static string CrashMessage(int exitCode)
    {
        var code = unchecked((uint)exitCode);
        // Error codes (0xC..., 0xE...) are crashes; small codes are the process ending on purpose,
        // or being ended (Task Manager leaves 1).
        if (CrashCodes.TryGetValue(code, out var words)) return $"The game crashed: {words} (0x{code:X8}).";
        if (code >= 0xC0000000) return $"The game crashed (0x{code:X8}).";
        return $"The game stopped unexpectedly (exit code {exitCode.ToString(CultureInfo.InvariantCulture)}).";
    }

    // The path on the last "crash report: saved" line, when that file still exists.
    private static string? LastCrashReport(string logPath)
    {
        string? report = null;
        try
        {
            // Shared read: something may still hold the log open for writing.
            using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line)
            {
                var saved = line.IndexOf(CrashReportMarker, StringComparison.Ordinal);
                if (saved >= 0) report = line[(saved + CrashReportMarker.Length)..].Trim();
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
        return report is not null && File.Exists(report) ? report : null;
    }
}
