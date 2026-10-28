namespace TopSpin4.Launcher.Core;

/// <summary>What the Launcher tells the player about how the game ended.</summary>
public static class GameExitReport
{
    /// <summary>
    /// The exit code the game ends with when the GPU or driver stopped working under the Native
    /// renderer: "GPUF" in ASCII (kGpuFailureExitCode in src/native/gpu_failure.h; keep the two).
    /// </summary>
    public const int GpuFailureExitCode = 0x47505546;

    /// <summary>The message for the player, or null when the exit needs none.</summary>
    public static string? Describe(int exitCode) => exitCode == GpuFailureExitCode
        ? "The graphics card or its driver stopped working while the game was running. Switch the renderer in Settings, then press Play again."
        : null;
}
