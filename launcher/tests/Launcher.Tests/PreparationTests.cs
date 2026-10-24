using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

public sealed class PreparationTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ts4-prepare-").FullName;
    private readonly TestDisc _disc = new();
    private readonly FakeDiskSpace _space = new();
    private readonly FakeTime _time = new();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Iso => Path.Combine(_root, "input", "Top Spin 4.iso");
    private string Cache => Path.Combine(_root, "cache");
    private string GameData => Path.Combine(Cache, "gamedata");
    private string UpdateData => Path.Combine(Cache, "updatedata");

    private Preparation Preparation() =>
        new(Cache, FileManifest.Parse(_disc.ManifestText()), FileManifest.Parse(TestTitleUpdate.ManifestText()), _space, _time);

    private string WriteDisc(TestDisc? disc = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Iso)!);
        (disc ?? _disc).WriteImage(Iso);
        return Iso;
    }

    private string WriteTu(TestStfs? package = null, string name = "TU_1A5822P_000000C000000.0000000000083") =>
        (package ?? TestTitleUpdate.Package()).WriteTo(Path.Combine(_root, "input", name));

    private PreparedData Prepare(string? disc, string? tu, IProgress<SetupProgress>? progress = null, CancellationToken? cancel = null) =>
        Preparation().Prepare(disc, tu, progress, cancel ?? TestContext.Current.CancellationToken);

    private SetupException Refused(string? disc, string? tu)
    {
        var error = Assert.Throws<SetupException>(() => Prepare(disc, tu));
        Assert.False(Directory.Exists(Cache) && Directory.EnumerateFileSystemEntries(Cache).Any(),
            "A refused input must leave nothing in the cache.");
        return error;
    }

    private void AssertPrepared()
    {
        foreach (var (path, data) in _disc.Files) Assert.Equal(data, File.ReadAllBytes(Path.Combine(GameData, path)));
        foreach (var (path, data) in TestTitleUpdate.Tu1Files) Assert.Equal(data, File.ReadAllBytes(Path.Combine(UpdateData, path)));
    }

    [Fact]
    public void The_reference_disc_and_tu1_are_prepared_into_the_cache()
    {
        var prepared = Prepare(WriteDisc(), WriteTu());

        Assert.Equal(GameData, prepared.GameDataRoot);
        Assert.Equal(UpdateData, prepared.UpdateDataRoot);
        Assert.False(prepared.Reused);
        AssertPrepared();
        Assert.Equal(prepared with { Reused = true }, Preparation().Existing());
    }

    [Fact]
    public void A_later_launch_reuses_the_cache_without_reading_either_input()
    {
        Prepare(WriteDisc(), WriteTu());
        var marker = Path.Combine(GameData, ".topspin4-prepared");
        var written = File.GetLastWriteTimeUtc(marker);
        Directory.Delete(Path.Combine(_root, "input"), recursive: true);

        var again = Prepare(Iso, null);

        Assert.True(again.Reused);
        Assert.Equal(written, File.GetLastWriteTimeUtc(marker));
        AssertPrepared();
    }

    [Fact]
    public void An_interrupted_preparation_is_not_complete_and_the_next_one_resumes_it()
    {
        var disc = WriteDisc();
        var tu = WriteTu();
        using var cancel = new CancellationTokenSource();
        var progress = new Reports(report =>
        {
            if (report is { Step: SetupStep.Extracting, CurrentFile: "Layoutall.bin" }) cancel.Cancel();
        });
        Assert.ThrowsAny<OperationCanceledException>(() => Prepare(disc, tu, progress, cancel.Token));

        Assert.Null(Preparation().Existing());
        Assert.False(Directory.Exists(GameData));
        var kept = Path.Combine(GameData + ".partial", "Amsterdam.arc");
        var keptWritten = File.GetLastWriteTimeUtc(kept);

        var prepared = Prepare(disc, tu);

        Assert.False(prepared.Reused);
        AssertPrepared();
        Assert.Equal(keptWritten, File.GetLastWriteTimeUtc(Path.Combine(GameData, "Amsterdam.arc")));
        Assert.False(Directory.Exists(GameData + ".partial"));
    }

    [Fact]
    public void Game_data_without_tu1_is_not_complete_and_only_tu1_is_then_needed()
    {
        var disc = WriteDisc();
        Assert.Equal(SetupError.NoTitleUpdate, Assert.Throws<SetupException>(() => Prepare(disc, null)).Error);
        Prepare(disc, WriteTu());
        Directory.Delete(UpdateData, recursive: true);

        Assert.Null(Preparation().Existing());
        File.Delete(disc);
        var prepared = Prepare(null, WriteTu());

        Assert.False(prepared.Reused);
        AssertPrepared();
    }

    [Theory]
    [InlineData("truncated")]
    [InlineData("deleted")]
    [InlineData("other manifest")]
    public void A_cache_that_was_changed_afterwards_is_not_complete_and_is_repaired(string change)
    {
        var disc = WriteDisc();
        var tu = WriteTu();
        Prepare(disc, tu);
        var file = Path.Combine(GameData, "Amsterdam.arc");
        switch (change)
        {
            case "truncated": File.WriteAllBytes(file, [1, 2, 3]); break;
            case "deleted": File.Delete(file); break;
            default: File.WriteAllText(Path.Combine(GameData, ".topspin4-prepared"), "manifest_version=older\n"); break;
        }

        Assert.Null(Preparation().Existing());
        Prepare(disc, tu);

        AssertPrepared();
        Assert.NotNull(Preparation().Existing());
    }

    [Fact]
    public void A_damaged_disc_file_fails_verification_and_leaves_no_game_data()
    {
        _disc.ManifestOverrides["Amsterdam.arc"] = TestDisc.Bytes(3 * 2048 + 17, 77);

        var error = Assert.Throws<SetupException>(() => Prepare(WriteDisc(), WriteTu()));

        Assert.Equal(SetupError.DamagedFile, error.Error);
        Assert.Contains("Amsterdam.arc", error.Message);
        Assert.False(Directory.Exists(GameData));
        Assert.Null(Preparation().Existing());
    }

    [Fact]
    public void Not_enough_free_space_is_refused_before_extracting()
    {
        _space.Free = _disc.TotalBytes + Core.Preparation.SpareBytes - 1;

        var error = Refused(WriteDisc(), WriteTu());

        Assert.Equal(SetupError.DiskFull, error.Error);
    }

    [Fact]
    public void A_file_that_is_not_an_xbox_image_is_refused()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Iso)!);
        File.WriteAllBytes(Iso, TestDisc.Bytes(200_000, 9));

        var error = Refused(Iso, WriteTu());

        Assert.Equal(SetupError.NotAnXboxImage, error.Error);
    }

    [Fact]
    public void No_disc_image_is_refused()
    {
        Assert.Equal(SetupError.ImageNotFound, Refused(null, WriteTu()).Error);
        Assert.Equal(SetupError.ImageNotFound, Refused(Iso, WriteTu()).Error);
    }

    [Fact]
    public void A_disc_of_another_game_is_refused_as_the_wrong_disc()
    {
        _disc.Files["default.xex"] = TestXex.Module(titleId: 0x54510866);

        var error = Refused(WriteDisc(), WriteTu());

        Assert.Equal(SetupError.WrongDisc, error.Error);
        Assert.Contains("another game", error.Message);
        Assert.Contains("54510866", error.Message);
    }

    [Fact]
    public void A_disc_of_another_edition_is_refused_as_the_wrong_disc()
    {
        foreach (var module in new[] { "default.xex", "Loader_DLL.xex", "Swing_DLL.xex" })
            _disc.Files[module] = TestXex.Module(mediaId: 0x12345678);

        var error = Refused(WriteDisc(), WriteTu());

        Assert.Equal(SetupError.WrongDisc, error.Error);
        Assert.Contains("media ID 12345678", error.Message);
        Assert.Contains("60819732", error.Message);
    }

    [Fact]
    public void A_disc_whose_module_has_another_version_is_refused_as_the_wrong_disc()
    {
        _disc.Files["Swing_DLL.xex"] = TestXex.Module(version: 0x00000203, seed: 3);

        var error = Refused(WriteDisc(), WriteTu());

        Assert.Equal(SetupError.WrongDisc, error.Error);
        Assert.Contains("Swing_DLL.xex is v0.0.2.3", error.Message);
        Assert.Contains("v0.0.0.3", error.Message);
    }

    [Fact]
    public void A_disc_missing_a_module_is_refused_as_the_wrong_disc()
    {
        _disc.Files.Remove("Loader_DLL.xex");

        var error = Refused(WriteDisc(), WriteTu());

        Assert.Equal(SetupError.WrongDisc, error.Error);
        Assert.Contains("Loader_DLL.xex", error.Message);
    }

    [Fact]
    public void A_disc_whose_file_list_differs_is_refused_as_the_wrong_disc()
    {
        var disc = new TestDisc();
        disc.Files["Extra.arc"] = TestDisc.Bytes(10, 5);

        var error = Refused(WriteDisc(disc), WriteTu());

        Assert.Equal(SetupError.WrongDisc, error.Error);
        Assert.Contains("files differ", error.Message);
    }

    [Fact]
    public void A_disc_whose_module_bytes_differ_is_refused_as_the_wrong_disc()
    {
        _disc.ManifestOverrides["Loader_DLL.xex"] = TestXex.Module(seed: 99);

        var error = Refused(WriteDisc(), WriteTu());

        Assert.Equal(SetupError.WrongDisc, error.Error);
        Assert.Contains("Loader_DLL.xex", error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData((byte)0xA5)]
    public void A_stripped_or_region_free_image_with_the_right_game_partition_is_accepted(byte? outside)
    {
        // Zeros outside the game partition are a security-sector-stripped dump like the owner's;
        // filled, they stand for a full dump. Neither is hashed: only the partition's files are.
        var disc = new TestDisc { PartitionOffset = 0x2080000, OutsideFill = outside };

        Prepare(WriteDisc(disc), WriteTu());

        AssertPrepared();
    }

    [Fact]
    public void The_inputs_are_unchanged_byte_for_byte()
    {
        var disc = WriteDisc();
        var tu = WriteTu();
        var before = (File.ReadAllBytes(disc), File.ReadAllBytes(tu));

        Prepare(disc, tu);

        Assert.Equal(before.Item1, File.ReadAllBytes(disc));
        Assert.Equal(before.Item2, File.ReadAllBytes(tu));
    }
}
