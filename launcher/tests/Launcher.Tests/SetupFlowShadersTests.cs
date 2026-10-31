using System.Text;
using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

/// <summary>The shaders band of setup: after the disc and Title Update 1, and the rebuild of a pack that went stale.</summary>
public sealed class SetupFlowShadersTests : IDisposable
{
    private const ulong ConverterId = 0xA1B2C3D4E5F60718;

    private readonly string _root = Directory.CreateTempSubdirectory("ts4-flow-shaders-").FullName;
    private readonly TestDisc _disc = new();
    private readonly FakeDiskSpace _space = new();
    private readonly FakeTime _time = new();
    private readonly FakeConverter _converter = new();

    public SetupFlowShadersTests()
    {
        _disc.Files["Amsterdam.arc"] = TestArc.Lzx().Add(TestArc.Container(vertex: false, 4)).Add(TestArc.Container(vertex: true, 6, seed: 3)).ToBytes();
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Cache => Path.Combine(_root, "cache");
    private string PackPath => Path.Combine(Cache, "cache", ShaderPackFile.FileName);

    private Preparation Preparation() =>
        new(Cache, FileManifest.Parse(_disc.ManifestText()), FileManifest.Parse(TestTitleUpdate.ManifestText()), _space, _time);

    private SetupFlow Flow(ulong converterId = ConverterId) => new(Preparation(), _time, shaders: new ShaderPreparation(_converter, converterId));

    private async Task<SetupFlow> Chosen(SetupFlow? flow = null)
    {
        flow ??= Flow();
        var input = Path.Combine(_root, "input");
        Directory.CreateDirectory(input);
        var iso = Path.Combine(input, "Top Spin 4.iso");
        if (!File.Exists(iso)) _disc.WriteImage(iso);
        await flow.ChooseDisc(iso);
        await flow.ChooseTitleUpdate(TestTitleUpdate.Package().WriteTo(Path.Combine(input, "TU_1A5822P_000000C000000.0000000000083")));
        return flow;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Setup_ends_with_a_usable_shader_pack_after_the_disc_and_title_update_bands()
    {
        var flow = await Chosen();
        var steps = new List<SetupStep>();
        var texts = new List<string>();
        var fractions = new List<double>();
        flow.Changed += () =>
        {
            if (flow.Phase != SetupPhase.Preparing) return;
            if (flow.Progress is { } progress && (steps.Count == 0 || steps[^1] != progress.Step)) steps.Add(progress.Step);
            texts.Add(flow.ProgressText);
            fractions.Add(flow.ProgressFraction);
        };

        await flow.PrepareAsync(Token);

        Assert.Equal(SetupPhase.Ready, flow.Phase);
        Assert.True(ShaderPackFile.Check(PackPath, ConverterId).Ready);
        Assert.Null(flow.ShaderFailure);
        Assert.Equal(SetupStep.Extracting, steps.Single(step => step is SetupStep.Extracting));
        Assert.True(steps.IndexOf(SetupStep.UnpackingTitleUpdate) < steps.IndexOf(SetupStep.PreparingShaders));
        Assert.Contains(texts, text => text.StartsWith("Preparing shaders") && text.Contains('%'));
        Assert.Equal(fractions.Order(), fractions);
        Assert.InRange(fractions[^1], 0.9, 1);
    }

    [Fact]
    public async Task Prepared_files_without_a_pack_are_not_setup_done_and_the_shader_step_alone_finishes_it()
    {
        var first = await Chosen(new SetupFlow(Preparation(), _time));
        await first.PrepareAsync(Token);
        Assert.Equal(SetupPhase.Ready, first.Phase);

        var flow = Flow();

        Assert.Equal(SetupPhase.NeedsInputs, flow.Phase);
        Assert.False(flow.Disc.Needed);
        Assert.False(flow.TitleUpdate.Needed);
        Assert.True(flow.CanPrepare);
        var steps = new List<SetupStep>();
        flow.Changed += () =>
        {
            if (flow.Progress is { } progress) steps.Add(progress.Step);
        };
        await flow.PrepareAsync(Token);
        Assert.Equal(SetupPhase.Ready, flow.Phase);
        Assert.True(ShaderPackFile.Check(PackPath, ConverterId).Ready);
        Assert.DoesNotContain(SetupStep.Extracting, steps);
        Assert.DoesNotContain(SetupStep.UnpackingTitleUpdate, steps);
        Assert.Equal(SetupPhase.Ready, Flow().Phase);
    }

    [Fact]
    public async Task A_setup_stopped_at_the_shaders_resumes_there_without_redoing_the_disc()
    {
        var flow = await Chosen();
        using var stop = new CancellationTokenSource();
        flow.Changed += () =>
        {
            if (flow.Progress?.Step == SetupStep.PreparingShaders) stop.Cancel();
        };

        await flow.PrepareAsync(stop.Token);

        Assert.Equal(SetupPhase.NeedsInputs, flow.Phase);
        Assert.Contains("resume", flow.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Resume setup", flow.PrimaryLabel);
        Assert.True(flow.CanPrepare);
        Assert.False(File.Exists(PackPath));
        var steps = new List<SetupStep>();
        flow.Changed += () =>
        {
            if (flow.Progress is { } progress) steps.Add(progress.Step);
        };

        await flow.PrepareAsync(Token);

        Assert.Equal(SetupPhase.Ready, flow.Phase);
        Assert.True(ShaderPackFile.Check(PackPath, ConverterId).Ready);
        Assert.DoesNotContain(SetupStep.Extracting, steps);
        Assert.Contains(SetupStep.PreparingShaders, steps);
    }

    [Fact]
    public async Task A_failed_shader_step_is_shown_and_Play_stays_available()
    {
        _converter.Failure = "XenosRecomp could not convert 2 shaders.";
        var flow = await Chosen();

        await flow.PrepareAsync(Token);

        Assert.Equal(SetupPhase.Ready, flow.Phase);
        Assert.NotNull(flow.Prepared);
        Assert.Contains("XenosRecomp could not convert 2 shaders", flow.ShaderFailure);
        Assert.Contains("Classic", flow.ShaderFailure);
        Assert.Equal(flow.ShaderFailure, flow.Message);
        Assert.False(File.Exists(PackPath));
        Assert.Equal(LauncherPage.Home, new LauncherShell(flow).Page);
        Assert.NotNull(new HomeModel(Preparation(), Path.Combine(_root, "topspin4.exe")).Prepared);
    }

    [Fact]
    public async Task A_pack_from_another_converter_or_a_missing_one_is_rebuilt_without_a_click()
    {
        await (await Chosen()).PrepareAsync(Token);
        Assert.False(Flow().WantsShaderRebuild);

        var stale = Flow(ConverterId + 1);

        Assert.Equal(SetupPhase.NeedsInputs, stale.Phase);
        Assert.True(stale.WantsShaderRebuild);
        await stale.PrepareAsync(Token);
        Assert.Equal(SetupPhase.Ready, stale.Phase);
        Assert.True(ShaderPackFile.Check(PackPath, ConverterId + 1).Ready);
        Assert.False(stale.WantsShaderRebuild);

        File.Delete(PackPath);
        Assert.True(Flow().WantsShaderRebuild);
    }

    [Fact]
    public async Task A_shader_step_that_failed_last_time_is_retried_once_at_the_next_start_and_Play_stays()
    {
        _converter.Failure = "XenosRecomp is missing.";
        await (await Chosen()).PrepareAsync(Token);

        var restarted = Flow();
        Assert.True(restarted.WantsShaderRebuild);
        await restarted.PrepareAsync(Token);

        Assert.Equal(SetupPhase.Ready, restarted.Phase);
        Assert.NotNull(restarted.ShaderFailure);
        Assert.Equal(LauncherPage.Home, new LauncherShell(restarted).Page);
    }

    [Fact]
    public async Task Repair_throws_the_pack_away_so_the_next_setup_builds_it_again()
    {
        var flow = await Chosen();
        await flow.PrepareAsync(Token);

        flow.Repair();

        Assert.False(File.Exists(PackPath));
        Assert.Equal(SetupPhase.NeedsInputs, flow.Phase);
        Assert.True(flow.WantsShaderRebuild);
        await flow.PrepareAsync(Token);
        Assert.Equal(SetupPhase.Ready, flow.Phase);
        Assert.True(ShaderPackFile.Check(PackPath, ConverterId).Ready);
    }

    [Fact]
    public async Task The_shader_step_refuses_a_drive_without_room_for_the_pack()
    {
        var first = await Chosen(new SetupFlow(Preparation(), _time));
        await first.PrepareAsync(Token);
        _space.Free = Core.Preparation.ShaderPackBytes - 1;

        var flow = Flow();
        await flow.PrepareAsync(Token);

        Assert.Contains("disk space", flow.ShaderFailure);
        Assert.Equal(SetupPhase.Ready, flow.Phase);
        Assert.False(File.Exists(PackPath));
    }

    [Fact]
    public void The_space_a_setup_needs_counts_the_pack()
    {
        var preparation = Preparation();

        Assert.True(preparation.NeededBytes >= _disc.TotalBytes + Core.Preparation.ShaderPackBytes);
    }

    /// <summary>XenosRecomp's output shape for whatever containers it is given.</summary>
    private sealed class FakeConverter : IShaderConverter
    {
        public string? Failure { get; set; }

        public void Convert(string inputDirectory, string outputDirectory, IProgress<double>? progress, CancellationToken cancel)
        {
            var inputs = Directory.EnumerateFiles(inputDirectory).Select(Path.GetFileName).Order().ToList();
            cancel.ThrowIfCancellationRequested();
            progress?.Report(0.5);
            if (Failure is not null) throw new ShaderConverterException(Failure);
            var cache = new StringBuilder("ShaderCacheEntry g_shaderCacheEntries[] = {\n");
            Directory.CreateDirectory(Path.Combine(outputDirectory, "hlsl"));
            for (var i = 0; i < inputs.Count; i++)
            {
                cache.Append($"\t{{ 0x{i + 1:X}, 0, 0, {i}, 1, 0 }},\n");
                File.WriteAllText(Path.Combine(outputDirectory, "hlsl", Path.ChangeExtension(inputs[i]!, ".hlsl")), "float4 main();\n");
            }
            cache.Append("};\nconst uint8_t g_compressedSpirvCache[] = {1,2,3,};\nconst size_t g_spirvCacheCompressedSize = 3;\n")
                .Append($"const size_t g_spirvCacheDecompressedSize = {inputs.Count};\n");
            File.WriteAllText(Path.Combine(outputDirectory, "shader_cache.cpp"), cache.ToString());
            progress?.Report(1);
        }
    }
}
