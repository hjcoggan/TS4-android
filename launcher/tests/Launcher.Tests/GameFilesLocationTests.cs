using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

/// <summary>Where the game files go: shown and chosen before setup, moved after it, shared with the game.</summary>
public sealed class GameFilesLocationTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ts4-location-").FullName;
    private readonly TestDisc _disc = new();
    private readonly FakeDiskSpace _space = new();
    private readonly FakeTime _time = new();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Default => Path.Combine(_root, "default");
    private string Elsewhere => Path.Combine(_root, "elsewhere");
    private string UserFolder => Path.Combine(_root, "user");
    private string LocationFile => Path.Combine(UserFolder, GameFilesLocation.FileName);

    private Preparation Preparation(string? root = null) =>
        new(root ?? Default, FileManifest.Parse(_disc.ManifestText()), FileManifest.Parse(TestTitleUpdate.ManifestText()), _space, _time);

    private GameFilesLocationModel Model(Preparation preparation) => new(preparation, new GameFilesLocation(UserFolder));

    private (string Disc, string TitleUpdate) WriteInputs()
    {
        var input = Path.Combine(_root, "input");
        Directory.CreateDirectory(input);
        var disc = Path.Combine(input, "Top Spin 4.iso");
        _disc.WriteImage(disc);
        return (disc, TestTitleUpdate.Package().WriteTo(Path.Combine(input, "TU_1A5822P_000000C000000.0000000000083")));
    }

    // Setup done the way the player does it, into the preparation's current place.
    private async Task<SetupFlow> SetUp(Preparation preparation)
    {
        var (disc, titleUpdate) = WriteInputs();
        var flow = new SetupFlow(preparation);
        flow.ChooseDisc(disc);
        flow.ChooseTitleUpdate(titleUpdate);
        await flow.PrepareAsync(TestContext.Current.CancellationToken);
        Assert.Equal(SetupPhase.Ready, flow.Phase);
        return flow;
    }

    [Fact]
    public void Before_setup_the_page_says_where_the_game_files_go_and_how_much_room_there_is()
    {
        _space.Free = 40L << 30;
        var model = Model(Preparation());

        Assert.Equal(Default, model.Path);
        Assert.True(model.EnoughSpace);
        Assert.Null(model.Warning);
        Assert.Contains("40", model.SpaceLine);
    }

    [Fact]
    public void Not_enough_free_space_is_reported_before_setup_starts()
    {
        _space.Free = 100L << 20;
        var preparation = Preparation();
        var model = Model(preparation);
        var (disc, titleUpdate) = WriteInputs();
        var flow = new SetupFlow(preparation);
        flow.ChooseDisc(disc);
        flow.ChooseTitleUpdate(titleUpdate);

        Assert.False(model.EnoughSpace);
        Assert.NotNull(model.Warning);
        Assert.False(flow.CanPrepare, "Setup must not start on a drive that cannot hold the files.");
        Assert.False(Directory.Exists(Default), "Nothing may be written before the warning.");
    }

    [Fact]
    public void Choosing_another_place_checks_the_room_there()
    {
        _space.Free = 100L << 20;
        var model = Model(Preparation());
        Assert.False(model.EnoughSpace);

        _space.Free = 40L << 30;
        model.Choose(Elsewhere);

        Assert.Equal(Elsewhere, model.Path);
        Assert.True(model.EnoughSpace);
        Assert.Equal(Elsewhere, _space.Asked[^1]);
    }

    [Fact]
    public async Task Setup_into_a_chosen_place_records_it_in_the_location_file()
    {
        var preparation = Preparation();
        Model(preparation).Choose(Elsewhere);

        await SetUp(preparation);

        Assert.True(File.Exists(Path.Combine(Elsewhere, "gamedata", ".topspin4-prepared")));
        Assert.False(Directory.Exists(Path.Combine(Default, "gamedata")));
        Assert.Equal(Elsewhere, new GameFilesLocation(UserFolder).Recorded);
    }

    // Files in the place the preparation points at, read back as bytes by manifest path.
    private void AssertFilesIn(string root)
    {
        foreach (var (path, data) in _disc.Files) Assert.Equal(data, File.ReadAllBytes(Path.Combine(root, "gamedata", path)));
        foreach (var (path, data) in TestTitleUpdate.Tu1Files) Assert.Equal(data, File.ReadAllBytes(Path.Combine(root, "updatedata", path)));
    }

    private GameFilesLocationModel ModelFor(Preparation preparation) => Model(preparation);

    [Fact]
    public async Task A_move_lands_verified_files_in_the_new_place_records_it_and_removes_the_old_copy()
    {
        var preparation = Preparation();
        await SetUp(preparation);
        var model = ModelFor(preparation);
        var progress = new List<SetupProgress>();
        model.Changed += () => { if (model.Progress is { } report) progress.Add(report); };

        await model.ChangeAsync(Elsewhere, TestContext.Current.CancellationToken);

        Assert.Null(model.Message);
        Assert.Equal(Elsewhere, model.Path);
        AssertFilesIn(Elsewhere);
        Assert.NotNull(preparation.Existing());
        Assert.Equal(Path.Combine(Elsewhere, "gamedata"), preparation.Existing()!.GameDataRoot);
        Assert.Equal(Elsewhere, new GameFilesLocation(UserFolder).Recorded);
        Assert.False(Directory.Exists(Path.Combine(Default, "gamedata")));
        Assert.False(Directory.Exists(Path.Combine(Default, "updatedata")));
        Assert.Contains(progress, report => report.Step == SetupStep.MovingFiles);
        Assert.Equal(progress.Where(report => report.Step == SetupStep.MovingFiles).OrderBy(report => report.BytesDone),
            progress.Where(report => report.Step == SetupStep.MovingFiles));
    }

    [Fact]
    public async Task An_interrupted_move_leaves_the_old_place_recorded_and_intact_and_a_later_move_finishes_it()
    {
        var preparation = Preparation();
        var model = ModelFor(preparation);
        model.Choose(Default);
        await SetUp(preparation);
        using var stop = new CancellationTokenSource();
        model.Changed += () => { if (model.Progress is { Step: SetupStep.MovingFiles, BytesDone: > 0 }) stop.Cancel(); };

        await model.ChangeAsync(Elsewhere, stop.Token);

        Assert.NotNull(model.Message);
        Assert.False(model.Moving);
        Assert.Equal(Default, model.Path);
        Assert.Equal(Default, new GameFilesLocation(UserFolder).Recorded);
        AssertFilesIn(Default);
        Assert.NotNull(preparation.Existing());
        Assert.False(Directory.Exists(Path.Combine(Elsewhere, "gamedata")), "A half-copied folder must never look complete.");

        var again = ModelFor(preparation);
        await again.ChangeAsync(Elsewhere, TestContext.Current.CancellationToken);
        AssertFilesIn(Elsewhere);
        Assert.Equal(Elsewhere, new GameFilesLocation(UserFolder).Recorded);
        Assert.False(Directory.Exists(Path.Combine(Default, "gamedata")));
    }

    [Fact]
    public async Task A_damaged_file_in_the_old_place_stops_the_move_with_the_old_copy_still_in_use()
    {
        var preparation = Preparation();
        await SetUp(preparation);
        var victim = Path.Combine(Default, "gamedata", "Layoutall.bin");
        var bytes = File.ReadAllBytes(victim);
        bytes[7] ^= 0xFF;
        File.WriteAllBytes(victim, bytes);
        var model = ModelFor(preparation);

        await model.ChangeAsync(Elsewhere, TestContext.Current.CancellationToken);

        Assert.Contains("Layoutall.bin", model.Message);
        Assert.Equal(Default, model.Path);
        Assert.Null(new GameFilesLocation(UserFolder).Recorded);
        Assert.True(File.Exists(Path.Combine(Default, "gamedata", ".topspin4-prepared")));
        Assert.False(Directory.Exists(Path.Combine(Elsewhere, "gamedata")));
    }

    [Fact]
    public async Task A_move_to_a_drive_without_room_is_refused_before_anything_is_copied()
    {
        var preparation = Preparation();
        await SetUp(preparation);
        var model = ModelFor(preparation);
        _space.Free = 100L << 20;

        await model.ChangeAsync(Elsewhere, TestContext.Current.CancellationToken);

        Assert.Contains("space", model.Message);
        Assert.Equal(Default, model.Path);
        Assert.False(Directory.Exists(Elsewhere));
        AssertFilesIn(Default);
    }

    [Theory]
    [InlineData("inside")]
    [InlineData("same")]
    public async Task A_place_inside_the_old_one_or_the_old_one_itself_moves_nothing(string which)
    {
        var preparation = Preparation();
        await SetUp(preparation);
        var model = ModelFor(preparation);

        await model.ChangeAsync(which == "same" ? Default : Path.Combine(Default, "gamedata", "deeper"), TestContext.Current.CancellationToken);

        Assert.Equal(Default, model.Path);
        AssertFilesIn(Default);
        Assert.Null(new GameFilesLocation(UserFolder).Recorded);
    }

    [Fact]
    public async Task With_one_folder_prepared_and_the_other_missing_the_place_stays_until_setup_finishes()
    {
        var preparation = Preparation();
        await SetUp(preparation);
        Directory.Delete(Path.Combine(Default, "updatedata"), recursive: true);
        var model = ModelFor(preparation);

        Assert.False(model.CanChange);
        model.Choose(Elsewhere);
        await model.ChangeAsync(Elsewhere, TestContext.Current.CancellationToken);

        Assert.Equal(Default, model.Path);
        Assert.Null(new GameFilesLocation(UserFolder).Recorded);
    }
}
