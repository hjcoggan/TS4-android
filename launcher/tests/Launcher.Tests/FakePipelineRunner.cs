using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

/// <summary>
/// The game's prepare-pipelines mode, played back: it prints the given lines, writes the store into the
/// <c>--cache_root</c> it was given (as the game does) when it exits 0 or 3, and returns the exit code.
/// </summary>
internal sealed class FakePipelineRunner : IPrepareRunner
{
    public static readonly string[] Finished =
    [
        "topspin4-prepare-pipelines 1",
        "progress stage=modules done=0 total=10",
        "progress stage=modules done=10 total=10",
        "progress stage=pipelines done=0 total=40",
        "progress stage=pipelines done=40 total=40",
        "result status=ok modules=10 pipelines=40 failed=0 ms=900",
    ];

    public string[] Lines { get; set; } = Finished;
    public int ExitCode { get; set; }
    public int Runs { get; private set; }
    public ProcessStart? Last { get; private set; }
    /// <summary>Cancelled before the first line, as a stopped setup is.</summary>
    public CancellationTokenSource? CancelOnStart { get; set; }

    public Task<int> RunAsync(ProcessStart start, Action<string> onLine, CancellationToken cancel)
    {
        Runs++;
        Last = start;
        CancelOnStart?.Cancel();
        cancel.ThrowIfCancellationRequested();
        foreach (var line in Lines) onLine(line);
        if (ExitCode is 0 or 3)
        {
            var cacheRoot = start.Arguments.Single(argument => argument.StartsWith("--cache_root=", StringComparison.Ordinal))["--cache_root=".Length..];
            var store = Path.Combine(cacheRoot, PipelinePreparation.StoreFolderName);
            Directory.CreateDirectory(store);
            File.WriteAllText(Path.Combine(store, "keys.bin"), "keys");
            File.WriteAllText(Path.Combine(store, "driver-cache.bin"), "cache");
            File.Delete(Path.Combine(store, "pipelines-stale"));
        }
        return Task.FromResult(ExitCode);
    }
}
