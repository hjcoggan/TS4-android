using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

public sealed class SetupFlowTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ts4-flow-").FullName;
    private readonly TestDisc _disc = new();
    private readonly FakeDiskSpace _space = new();
    private readonly FakeTime _time = new();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Cache => Path.Combine(_root, "cache");

    private Preparation Preparation() =>
        new(Cache, FileManifest.Parse(_disc.ManifestText()), FileManifest.Parse(TestTitleUpdate.ManifestText()), _space, _time);

    private string WriteDisc(TestDisc? disc = null)
    {
        var path = Path.Combine(_root, "input", "Top Spin 4.iso");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        (disc ?? _disc).WriteImage(path);
        return path;
    }

    private string WriteTu() =>
        TestTitleUpdate.Package().WriteTo(Path.Combine(_root, "input", "TU_1A5822P_000000C000000.0000000000083"));

    [Fact]
    public void A_clean_user_folder_asks_for_the_disc_and_tu1()
    {
        var flow = new SetupFlow(Preparation());

        Assert.Equal(SetupPhase.NeedsInputs, flow.Phase);
        Assert.True(flow.NeedsDisc);
        Assert.True(flow.NeedsTitleUpdate);
        Assert.False(flow.CanPrepare);

        flow.ChooseDisc(WriteDisc());
        Assert.False(flow.CanPrepare);
        flow.ChooseTitleUpdate(WriteTu());
        Assert.True(flow.CanPrepare);
    }

    [Fact]
    public async Task Preparing_with_both_inputs_ends_ready_to_play()
    {
        var flow = new SetupFlow(Preparation());
        flow.ChooseDisc(WriteDisc());
        flow.ChooseTitleUpdate(WriteTu());
        var phases = new List<SetupPhase>();
        flow.Changed += () => phases.Add(flow.Phase);

        await flow.PrepareAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SetupPhase.Ready, flow.Phase);
        Assert.Equal(Path.Combine(Cache, "gamedata"), flow.Prepared!.GameDataRoot);
        Assert.Equal(Path.Combine(Cache, "updatedata"), flow.Prepared.UpdateDataRoot);
        Assert.Contains(SetupPhase.Preparing, phases);
    }

    [Fact]
    public async Task A_prepared_cache_opens_ready_and_needs_no_input()
    {
        var first = new SetupFlow(Preparation());
        first.ChooseDisc(WriteDisc());
        first.ChooseTitleUpdate(WriteTu());
        await first.PrepareAsync(TestContext.Current.CancellationToken);

        var later = new SetupFlow(Preparation());

        Assert.Equal(SetupPhase.Ready, later.Phase);
        Assert.False(later.NeedsDisc);
        Assert.False(later.NeedsTitleUpdate);
        Assert.True(later.Prepared!.Reused);
    }

    [Fact]
    public async Task A_wrong_disc_fails_with_the_reason_and_can_be_retried()
    {
        var other = new TestDisc();
        other.Files["default.xex"] = TestXex.Module(titleId: 0x54510866);
        var flow = new SetupFlow(Preparation());
        flow.ChooseDisc(WriteDisc(other));
        flow.ChooseTitleUpdate(WriteTu());

        await flow.PrepareAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SetupPhase.Failed, flow.Phase);
        Assert.Equal(SetupError.WrongDisc, flow.Error);
        Assert.Contains("another game", flow.Message);
        Assert.True(flow.CanPrepare);

        flow.ChooseDisc(WriteDisc());
        await flow.PrepareAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SetupPhase.Ready, flow.Phase);
    }

    [Fact]
    public async Task Stopping_returns_to_the_inputs_without_an_error()
    {
        var flow = new SetupFlow(Preparation());
        flow.ChooseDisc(WriteDisc());
        flow.ChooseTitleUpdate(WriteTu());
        using var stop = new CancellationTokenSource();
        flow.Changed += () =>
        {
            if (flow.Progress?.Step == SetupStep.Extracting) stop.Cancel();
        };

        await flow.PrepareAsync(stop.Token);

        Assert.Equal(SetupPhase.NeedsInputs, flow.Phase);
        Assert.Null(flow.Error);
        Assert.Contains("resumes", flow.Message);
    }

    [Fact]
    public void Play_passes_the_prepared_folders_and_no_player_setting()
    {
        var prepared = new PreparedData(@"C:\data\gamedata", @"C:\data\updatedata", Reused: true);

        Assert.Equal(
            [@"--game_data_root=C:\data\gamedata", @"--update_data_root=C:\data\updatedata"],
            LaunchArguments.Play(prepared));
    }

    [Fact]
    public void Play_appends_extra_arguments_for_later_start_options()
    {
        var prepared = new PreparedData("g", "u", Reused: true);

        Assert.Equal(["--game_data_root=g", "--update_data_root=u", "--safe"], LaunchArguments.Play(prepared, ["--safe"]));
    }

    [Fact]
    public void The_game_exe_is_found_beside_the_launcher()
    {
        Assert.Null(LaunchArguments.FindGameExe(_root));

        File.WriteAllBytes(Path.Combine(_root, LaunchArguments.GameExeName), []);

        Assert.Equal(Path.Combine(_root, LaunchArguments.GameExeName), LaunchArguments.FindGameExe(_root));
    }

    [Fact]
    public void Inputs_are_found_by_name_in_the_given_folders()
    {
        File.WriteAllBytes(Path.Combine(_root, "Top Spin 4.ISO"), []);
        File.WriteAllBytes(Path.Combine(_root, "TU_1A5822P_000000C000000.0000000000083"), []);
        File.WriteAllBytes(Path.Combine(_root, "notes.txt"), []);

        Assert.Equal([Path.Combine(_root, "Top Spin 4.ISO")], InputFinder.DiscImages([_root, Path.Combine(_root, "missing")]));
        Assert.Equal([Path.Combine(_root, "TU_1A5822P_000000C000000.0000000000083")], InputFinder.TitleUpdates([_root]));
    }

    [Fact]
    public void The_pad_moves_accepts_and_goes_back_with_repeat_on_a_held_direction()
    {
        var pad = new PadNavigator();

        Assert.Equal(PadAction.None, pad.Update(default, 0));
        Assert.Equal(PadAction.Down, pad.Update(new(PadState.DPadDown, 0, 0), 10));
        Assert.Equal(PadAction.None, pad.Update(new(PadState.DPadDown, 0, 0), 200));
        Assert.Equal(PadAction.Down, pad.Update(new(PadState.DPadDown, 0, 0), 10 + PadNavigator.FirstRepeatMs));
        Assert.Equal(PadAction.None, pad.Update(default, 600));
        Assert.Equal(PadAction.Right, pad.Update(new(0, 30000, 0), 700));
        Assert.Equal(PadAction.Up, pad.Update(new(0, 0, 30000), 800));
        Assert.Equal(PadAction.Accept, pad.Update(new(PadState.A, 0, 0), 900));
        Assert.Equal(PadAction.None, pad.Update(new(PadState.A, 0, 0), 5000));
        Assert.Equal(PadAction.Back, pad.Update(new(PadState.B, 0, 0), 5100));
    }
}
