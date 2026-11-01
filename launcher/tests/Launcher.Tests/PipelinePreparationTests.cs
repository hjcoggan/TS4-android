using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

/// <summary>The pipeline step's run and its rebuild decision, with the game's prepare mode played back.</summary>
public sealed class PipelinePreparationTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ts4-pipelines-").FullName;
    private readonly FakePipelineRunner _runner = new();
    private readonly Preparation _preparation;
    private readonly PipelinePreparation _pipelines;

    public PipelinePreparationTests()
    {
        _preparation = new Preparation(Path.Combine(_root, "install"));
        Directory.CreateDirectory(_preparation.ShaderCacheDirectory);
        Directory.CreateDirectory(_preparation.UpdateDataRoot);
        File.WriteAllText(PackPath, "pack");
        _pipelines = new PipelinePreparation(Path.Combine(_root, "install", "topspin4.exe"), _preparation, _runner);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string PackPath => Path.Combine(_preparation.ShaderCacheDirectory, ShaderPackFile.FileName);
    private string Store => Path.Combine(_preparation.ShaderCacheDirectory, PipelinePreparation.StoreFolderName);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_finished_prepare_reports_rising_progress_and_needs_no_rebuild_afterwards()
    {
        Assert.Equal(PipelineRebuild.NoStore, _pipelines.Check(shaderPackReady: true));
        var fractions = new List<double>();

        var result = await _pipelines.RunAsync(new SyncProgress<PipelineProgress>(report => fractions.Add(report.Fraction)), Token);

        Assert.Equal(PipelineOutcome.Ready, result.Outcome);
        Assert.Equal(fractions.Order(), fractions);
        Assert.Equal(1, fractions[^1]);
        Assert.Equal(PipelineRebuild.None, _pipelines.Check(shaderPackReady: true));
        Assert.Contains("--ts4_prepare_pipelines=true", _runner.Last!.Arguments);
        Assert.Contains("--cache_root=" + _preparation.ShaderCacheDirectory, _runner.Last.Arguments);
        Assert.Contains("--update_data_root=" + _preparation.UpdateDataRoot, _runner.Last.Arguments);
    }

    [Fact]
    public async Task A_partial_prepare_is_usable_and_a_failed_or_crashed_one_is_not_recorded()
    {
        _runner.Lines = [.. FakePipelineRunner.Finished[..^1], "result status=partial modules=10 pipelines=38 failed=2 ms=900"];
        _runner.ExitCode = 3;
        var partial = await _pipelines.RunAsync(null, Token);
        Assert.Equal(PipelineOutcome.Partial, partial.Outcome);
        Assert.Equal(PipelineRebuild.None, _pipelines.Check(true));

        _pipelines.Forget();
        _runner.Lines = ["topspin4-prepare-pipelines 1", "result status=failed reason=vulkan_setup ms=40"];
        _runner.ExitCode = 2;
        var failed = await _pipelines.RunAsync(null, Token);
        Assert.Equal(PipelineOutcome.Failed, failed.Outcome);
        Assert.Contains("Vulkan", failed.Message);
        Assert.Equal(PipelineRebuild.NoStore, _pipelines.Check(true));

        _runner.Lines = ["topspin4-prepare-pipelines 1"];
        _runner.ExitCode = unchecked((int)0xC0000005);
        var crashed = await _pipelines.RunAsync(null, Token);
        Assert.Equal(PipelineOutcome.Crashed, crashed.Outcome);
        Assert.Contains("C0000005", crashed.Message);
    }

    [Fact]
    public async Task A_crashed_prepare_is_run_once_more_and_a_second_crash_is_reported()
    {
        _runner.ExitCodes.Enqueue(unchecked((int)0xC0000005));
        var recovered = await _pipelines.RunAsync(null, Token);
        Assert.Equal(PipelineOutcome.Ready, recovered.Outcome);
        Assert.Equal(2, _runner.Runs);
        Assert.Equal(PipelineRebuild.None, _pipelines.Check(true));

        _pipelines.Forget();
        _runner.Lines = ["topspin4-prepare-pipelines 1"];
        _runner.ExitCode = unchecked((int)0xC0000005);
        var crashed = await _pipelines.RunAsync(null, Token);
        Assert.Equal(PipelineOutcome.Crashed, crashed.Outcome);
        Assert.Equal(4, _runner.Runs);
    }

    [Fact]
    public async Task The_stale_marker_a_changed_pack_a_new_top_up_or_a_changed_update_folder_rebuilds()
    {
        await _pipelines.RunAsync(null, Token);

        File.WriteAllText(Path.Combine(Store, "pipelines-stale"), "driver");
        Assert.Equal(PipelineRebuild.Stale, _pipelines.Check(true));
        await _pipelines.RunAsync(null, Token);

        File.WriteAllText(PackPath, "another pack");
        Assert.Equal(PipelineRebuild.PackChanged, _pipelines.Check(true));
        await _pipelines.RunAsync(null, Token);

        var topUps = Path.Combine(_preparation.ShaderCacheDirectory, ShaderPreparation.TopUpFolderName);
        Directory.CreateDirectory(topUps);
        File.WriteAllText(Path.Combine(topUps, "ps_0123456789abcdef.bin"), "container");
        Assert.Equal(PipelineRebuild.TopUpsChanged, _pipelines.Check(true));
        await _pipelines.RunAsync(null, Token);

        File.WriteAllText(Path.Combine(_preparation.UpdateDataRoot, "default.xexp"), "patch");
        Assert.Equal(PipelineRebuild.UpdateFolderChanged, _pipelines.Check(true));
    }

    [Fact]
    public async Task A_missing_store_or_Repair_rebuilds_and_without_a_pack_there_is_nothing_to_build()
    {
        await _pipelines.RunAsync(null, Token);

        Assert.Equal(PipelineRebuild.Repair, _pipelines.Check(true, repair: true));
        Assert.Equal(PipelineRebuild.NoShaderPack, _pipelines.Check(false));
        Assert.False(PipelineRebuild.NoShaderPack.Rebuilds());

        File.Delete(Path.Combine(Store, "keys.bin"));
        Assert.Equal(PipelineRebuild.NoStore, _pipelines.Check(true));
    }

    private sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
