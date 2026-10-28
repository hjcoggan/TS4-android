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

    private SetupFlow Flow() => new(Preparation(), _time);

    private string WriteDisc(TestDisc? disc = null, string name = "Top Spin 4.iso")
    {
        var path = Path.Combine(_root, "input", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        (disc ?? _disc).WriteImage(path);
        return path;
    }

    private string WriteTu(TestStfs? package = null) =>
        (package ?? TestTitleUpdate.Package()).WriteTo(Path.Combine(_root, "input", "TU_1A5822P_000000C000000.0000000000083"));

    private string WriteText(string name)
    {
        var path = Path.Combine(_root, "input", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "just some words, nothing an Xbox would read");
        return path;
    }

    private static void AssertRejectedPlainly(SetupRow row, string hexInDetails)
    {
        Assert.Equal(RowState.Rejected, row.State);
        Assert.False(string.IsNullOrWhiteSpace(row.Reason));
        Assert.DoesNotContain(hexInDetails, row.Reason);
        Assert.DoesNotContain("Exception", row.Reason);
        Assert.Contains(hexInDetails, row.Details);
    }

    [Fact]
    public void A_clean_user_folder_shows_two_empty_rows_and_fills_neither_by_itself()
    {
        // Files in the places the old search looked (beside the Launcher, Downloads) must stay unused.
        WriteDisc();
        WriteTu();

        var flow = Flow();

        Assert.Equal(SetupPhase.NeedsInputs, flow.Phase);
        Assert.Equal(RowState.Empty, flow.Disc.State);
        Assert.Equal(RowState.Empty, flow.TitleUpdate.State);
        Assert.Null(flow.Disc.Path);
        Assert.Null(flow.TitleUpdate.Path);
        Assert.False(flow.CanPrepare);
    }

    [Fact]
    public async Task A_supported_disc_gives_an_accepted_row_naming_the_edition()
    {
        var flow = Flow();

        await flow.ChooseDisc(WriteDisc());

        Assert.Equal(RowState.Accepted, flow.Disc.State);
        Assert.Equal("Top Spin 4 disc, supported edition", flow.Disc.Summary);
        Assert.Null(flow.Disc.Reason);
        Assert.Equal(RowState.Empty, flow.TitleUpdate.State);
    }

    [Fact]
    public async Task A_supported_title_update_gives_an_accepted_row_naming_the_update()
    {
        var flow = Flow();

        await flow.ChooseTitleUpdate(WriteTu());

        Assert.Equal(RowState.Accepted, flow.TitleUpdate.State);
        Assert.Equal("Title Update 1 for Top Spin 4", flow.TitleUpdate.Summary);
        Assert.Null(flow.TitleUpdate.Reason);
    }

    [Fact]
    public async Task The_row_shows_Checking_while_the_file_is_read()
    {
        var flow = Flow();
        var states = new List<RowState>();
        flow.Changed += () => states.Add(flow.Disc.State);

        await flow.ChooseDisc(WriteDisc());

        Assert.Equal([RowState.Checking, RowState.Accepted], states);
    }

    [Fact]
    public async Task A_disc_of_another_edition_is_rejected_on_its_row_with_the_media_id_only_in_the_details()
    {
        foreach (var module in new[] { "default.xex", "Loader_DLL.xex", "Swing_DLL.xex" })
            _disc.Files[module] = TestXex.Module(mediaId: 0x12345678);
        var flow = Flow();

        await flow.ChooseDisc(WriteDisc());

        AssertRejectedPlainly(flow.Disc, "12345678");
        Assert.Contains("another edition", flow.Disc.Reason);
        Assert.Equal(RowState.Empty, flow.TitleUpdate.State);
        Assert.False(flow.CanPrepare);
    }

    [Fact]
    public async Task A_disc_of_another_game_is_rejected_with_the_title_id_only_in_the_details()
    {
        _disc.Files["default.xex"] = TestXex.Module(titleId: 0x54510866);
        var flow = Flow();

        await flow.ChooseDisc(WriteDisc());

        AssertRejectedPlainly(flow.Disc, "54510866");
        Assert.Contains("another game", flow.Disc.Reason);
    }

    [Fact]
    public async Task A_file_that_is_not_a_disc_image_is_rejected_whatever_it_is_called()
    {
        var flow = Flow();

        await flow.ChooseDisc(WriteText("notes.txt"));
        Assert.Equal(RowState.Rejected, flow.Disc.State);
        Assert.Contains(".iso", flow.Disc.Reason);

        await flow.ChooseDisc(WriteText("fake.iso"));
        Assert.Equal(RowState.Rejected, flow.Disc.State);
        Assert.Contains("not an Xbox 360 disc image", flow.Disc.Reason);
        Assert.NotNull(flow.Disc.Details);
    }

    [Fact]
    public async Task A_disc_image_that_cannot_be_read_is_rejected_with_the_system_error_in_the_details()
    {
        var path = WriteDisc();
        var flow = Flow();

        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            await flow.ChooseDisc(path);

        Assert.Equal(RowState.Rejected, flow.Disc.State);
        Assert.Contains("could not be read", flow.Disc.Reason);
        Assert.False(string.IsNullOrWhiteSpace(flow.Disc.Details));
        Assert.DoesNotContain("Exception", flow.Disc.Reason);
    }

    [Fact]
    public async Task A_disc_image_that_is_gone_is_rejected()
    {
        var flow = Flow();

        await flow.ChooseDisc(Path.Combine(_root, "input", "gone.iso"));

        Assert.Equal(RowState.Rejected, flow.Disc.State);
        Assert.Contains("not found", flow.Disc.Reason);
    }

    [Fact]
    public async Task A_title_update_for_another_edition_is_rejected_with_the_media_id_only_in_the_details()
    {
        var flow = Flow();

        await flow.ChooseTitleUpdate(WriteTu(TestTitleUpdate.Package(mediaId: 0x12345678)));

        AssertRejectedPlainly(flow.TitleUpdate, "12345678");
        Assert.Contains("another edition", flow.TitleUpdate.Reason);
    }

    [Fact]
    public async Task A_title_update_with_other_module_patches_is_rejected_with_the_versions_only_in_the_details()
    {
        var flow = Flow();

        await flow.ChooseTitleUpdate(WriteTu(TestTitleUpdate.Package(files: TestTitleUpdate.With("Swing_DLL.xexp", TestXex.Patch(target: 0x00000203, seed: 13)))));

        AssertRejectedPlainly(flow.TitleUpdate, "v0.0.2.3");
        Assert.Contains("not Title Update 1", flow.TitleUpdate.Reason);
    }

    [Fact]
    public async Task A_file_that_is_not_a_content_package_is_rejected_on_the_title_update_row()
    {
        var flow = Flow();

        await flow.ChooseTitleUpdate(WriteText("TU_something"));

        Assert.Equal(RowState.Rejected, flow.TitleUpdate.State);
        Assert.Contains("not an Xbox 360 title update", flow.TitleUpdate.Reason);
        Assert.NotNull(flow.TitleUpdate.Details);
    }

    [Fact]
    public async Task A_title_update_that_cannot_be_read_is_rejected_with_the_system_error_in_the_details()
    {
        var path = WriteTu();
        var flow = Flow();

        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            await flow.ChooseTitleUpdate(path);

        Assert.Equal(RowState.Rejected, flow.TitleUpdate.State);
        Assert.Contains("could not be read", flow.TitleUpdate.Reason);
        Assert.False(string.IsNullOrWhiteSpace(flow.TitleUpdate.Details));
    }

    [Fact]
    public async Task The_disc_row_does_not_accept_a_title_update_and_the_other_way_round()
    {
        var flow = Flow();

        await flow.ChooseDisc(WriteTu());
        await flow.ChooseTitleUpdate(WriteDisc());

        Assert.Equal(RowState.Rejected, flow.Disc.State);
        Assert.Equal(RowState.Rejected, flow.TitleUpdate.State);
    }

    [Fact]
    public async Task Choosing_again_replaces_a_rejected_row_and_choosing_nothing_empties_it()
    {
        var flow = Flow();
        await flow.ChooseDisc(WriteText("notes.txt"));

        await flow.ChooseDisc(WriteDisc());
        Assert.Equal(RowState.Accepted, flow.Disc.State);
        Assert.Null(flow.Disc.Reason);

        await flow.ChooseDisc(null);
        Assert.Equal(RowState.Empty, flow.Disc.State);
        Assert.Null(flow.Disc.Path);
    }

    [Fact]
    public async Task Set_up_the_game_is_enabled_only_when_both_rows_are_accepted()
    {
        var flow = Flow();
        Assert.False(flow.CanPrepare);

        await flow.ChooseDisc(WriteDisc());
        Assert.False(flow.CanPrepare);

        await flow.ChooseTitleUpdate(WriteText("TU_something"));
        Assert.False(flow.CanPrepare);

        await flow.ChooseTitleUpdate(WriteTu());
        Assert.True(flow.CanPrepare);

        await flow.ChooseDisc(WriteText("notes.txt"));
        Assert.False(flow.CanPrepare);
    }

    [Fact]
    public async Task Preparing_with_both_rows_accepted_ends_ready_to_play()
    {
        var flow = Flow();
        await flow.ChooseDisc(WriteDisc());
        await flow.ChooseTitleUpdate(WriteTu());
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
        var first = Flow();
        await first.ChooseDisc(WriteDisc());
        await first.ChooseTitleUpdate(WriteTu());
        await first.PrepareAsync(TestContext.Current.CancellationToken);

        var later = Flow();

        Assert.Equal(SetupPhase.Ready, later.Phase);
        Assert.True(later.Prepared!.Reused);
    }

    [Theory]
    [InlineData(185, "4 min left")]
    [InlineData(60, "1 min left")]
    [InlineData(59, "under a minute left")]
    [InlineData(1, "under a minute left")]
    public void The_time_left_is_said_in_whole_minutes_rounded_up_or_as_under_a_minute(int seconds, string said) =>
        Assert.Equal(said, SetupProgress.TimeLeftText(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public async Task Prepared_files_that_went_away_bring_the_flow_back_to_its_input_rows()
    {
        var first = Flow();
        await first.ChooseDisc(WriteDisc());
        await first.ChooseTitleUpdate(WriteTu());
        await first.PrepareAsync(TestContext.Current.CancellationToken);
        var flow = Flow();
        Assert.Equal(SetupPhase.Ready, flow.Phase);
        Directory.Delete(Path.Combine(Cache, "gamedata"), recursive: true);
        var changes = 0;
        flow.Changed += () => changes++;

        flow.SetUpAgain();

        Assert.Equal(SetupPhase.NeedsInputs, flow.Phase);
        Assert.Null(flow.Prepared);
        Assert.True(flow.Disc.Needed);
        Assert.Equal(RowState.Empty, flow.Disc.State);
        Assert.False(flow.TitleUpdate.Needed, "Title Update 1 is still prepared.");
        Assert.Equal(1, changes);
        await flow.ChooseDisc(WriteDisc());
        await flow.ChooseTitleUpdate(null);
        Assert.True(flow.CanPrepare);
    }

    [Fact]
    public async Task Setting_up_again_changes_nothing_while_the_prepared_files_are_all_there()
    {
        var first = Flow();
        await first.ChooseDisc(WriteDisc());
        await first.ChooseTitleUpdate(WriteTu());
        await first.PrepareAsync(TestContext.Current.CancellationToken);
        var flow = Flow();

        flow.SetUpAgain();

        Assert.Equal(SetupPhase.Ready, flow.Phase);
    }

    [Fact]
    public async Task A_row_whose_files_are_already_set_up_is_accepted_without_a_choice()
    {
        // The disc was extracted, then setup stopped before Title Update 1 was unpacked.
        var first = Flow();
        await first.ChooseDisc(WriteDisc());
        await first.ChooseTitleUpdate(WriteTu());
        using var stop = new CancellationTokenSource();
        first.Changed += () =>
        {
            if (first.Progress?.Step == SetupStep.UnpackingTitleUpdate) stop.Cancel();
        };
        await first.PrepareAsync(stop.Token);

        var later = Flow();

        Assert.Equal(RowState.Accepted, later.Disc.State);
        Assert.False(later.Disc.Needed);
        Assert.Equal("Already set up", later.Disc.Summary);
        Assert.Equal(RowState.Empty, later.TitleUpdate.State);
        Assert.True(later.TitleUpdate.Needed);
        Assert.False(later.CanPrepare);
        await later.ChooseTitleUpdate(WriteTu());
        Assert.True(later.CanPrepare);
    }

    [Fact]
    public async Task Progress_is_one_value_that_never_goes_back_from_check_to_done()
    {
        var flow = Flow();
        await flow.ChooseDisc(WriteDisc());
        await flow.ChooseTitleUpdate(WriteTu());
        var values = new List<double>();
        var steps = new List<SetupStep>();
        flow.Changed += () =>
        {
            if (flow.Phase != SetupPhase.Preparing) return;
            values.Add(flow.ProgressFraction);
            if (flow.Progress is { } progress) steps.Add(progress.Step);
        };

        await flow.PrepareAsync(TestContext.Current.CancellationToken);

        Assert.Contains(SetupStep.Extracting, steps);
        Assert.Contains(SetupStep.UnpackingTitleUpdate, steps);
        Assert.True(values.Count > 4);
        Assert.Equal(values.Order(), values);
        Assert.InRange(values[0], 0, 0.05);
        Assert.InRange(values[^1], 0.9, 1);
    }

    [Fact]
    public async Task Progress_is_indeterminate_while_checking_and_shows_the_time_left_while_copying()
    {
        var flow = Flow();
        await flow.ChooseDisc(WriteDisc());
        await flow.ChooseTitleUpdate(WriteTu());
        var checking = new List<bool>();
        var copying = new List<bool>();
        var texts = new List<string>();
        flow.Changed += () =>
        {
            if (flow.Phase != SetupPhase.Preparing) return;
            if (flow.Progress is null or { Step: SetupStep.CheckingDisc or SetupStep.CheckingTitleUpdate }) checking.Add(flow.ProgressIsIndeterminate);
            else if (flow.Progress is { Step: SetupStep.Extracting }) copying.Add(flow.ProgressIsIndeterminate);
            texts.Add(flow.ProgressText);
        };

        await flow.PrepareAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(checking);
        Assert.All(checking, indeterminate => Assert.True(indeterminate));
        Assert.NotEmpty(copying);
        Assert.Contains(copying, indeterminate => !indeterminate);
        Assert.Contains(texts, text => text.StartsWith("Extracting the disc") && text.Contains('%') && text.Contains("left"));
    }

    [Fact]
    public async Task A_stopped_setup_offers_resume_and_start_over_and_resuming_finishes_it()
    {
        var flow = Flow();
        Assert.Equal("Set up the game", flow.PrimaryLabel);
        Assert.False(flow.CanStartOver);
        await flow.ChooseDisc(WriteDisc());
        await flow.ChooseTitleUpdate(WriteTu());
        using var stop = new CancellationTokenSource();
        flow.Changed += () =>
        {
            if (flow.Progress?.Step == SetupStep.Extracting) stop.Cancel();
        };

        await flow.PrepareAsync(stop.Token);

        Assert.Equal(SetupPhase.NeedsInputs, flow.Phase);
        Assert.Null(flow.Error);
        Assert.Contains("resume", flow.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Resume setup", flow.PrimaryLabel);
        Assert.True(flow.CanStartOver);
        Assert.True(flow.CanPrepare);

        await flow.PrepareAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SetupPhase.Ready, flow.Phase);
    }

    [Fact]
    public async Task Start_over_removes_the_unfinished_files_and_keeps_the_chosen_rows()
    {
        var flow = Flow();
        await flow.ChooseDisc(WriteDisc());
        await flow.ChooseTitleUpdate(WriteTu());
        using var stop = new CancellationTokenSource();
        flow.Changed += () =>
        {
            if (flow.Progress?.Step == SetupStep.Extracting) stop.Cancel();
        };
        await flow.PrepareAsync(stop.Token);

        flow.StartOver();

        Assert.False(Directory.Exists(Path.Combine(Cache, "gamedata.partial")));
        Assert.Equal("Set up the game", flow.PrimaryLabel);
        Assert.False(flow.CanStartOver);
        Assert.Null(flow.Message);
        Assert.Equal(RowState.Accepted, flow.Disc.State);
        Assert.True(flow.CanPrepare);
    }

    [Fact]
    public async Task A_failed_setup_names_the_problem_without_a_control_the_window_lacks()
    {
        _space.Free = 1;
        var flow = Flow();
        await flow.ChooseDisc(WriteDisc());
        await flow.ChooseTitleUpdate(WriteTu());

        await flow.PrepareAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SetupPhase.Failed, flow.Phase);
        Assert.Equal(SetupError.DiskFull, flow.Error);
        Assert.Contains("Not enough disk space", flow.Message);
        Assert.DoesNotContain("cache folder", flow.Message);
        Assert.True(flow.CanPrepare);
    }

    [Fact]
    public async Task A_change_of_the_file_after_it_was_accepted_is_caught_and_marks_its_row()
    {
        var disc = WriteDisc();
        var flow = Flow();
        await flow.ChooseDisc(disc);
        await flow.ChooseTitleUpdate(WriteTu());
        File.Delete(disc);

        await flow.PrepareAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SetupPhase.Failed, flow.Phase);
        Assert.Equal(RowState.Rejected, flow.Disc.State);
        Assert.Contains("not found", flow.Disc.Reason);
        Assert.False(flow.CanPrepare);
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
