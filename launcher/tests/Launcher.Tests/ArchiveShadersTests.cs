using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

public sealed class ArchiveShadersTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ts4-arc-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static readonly byte[] Pixel = TestArc.Container(vertex: false, 6);
    private static readonly byte[] Vertex = TestArc.Container(vertex: true, 9, seed: 5);

    private string Game(string name) => Path.Combine(_root, name);

    private IReadOnlyList<ShaderContainer> Read(IProgress<(int Done, int Total)>? progress = null) =>
        ArchiveShaders.Read(_root, progress, TestContext.Current.CancellationToken);

    [Fact]
    public void A_compressed_entry_that_is_a_container_is_read_and_named_like_the_games_dump()
    {
        TestArc.Lzx().Add(Pixel).Add(Vertex).WriteTo(Game("Arena.arc"));

        var found = Read();

        Assert.Equal([Pixel, Vertex], found.OrderBy(c => c.Bytes[3]).Select(c => c.Bytes));
        Assert.Equal(["ps_" + ShaderContainer.Fnv1a64(Pixel).ToString("x16"), "vs_" + ShaderContainer.Fnv1a64(Vertex).ToString("x16")],
            found.OrderBy(c => c.Bytes[3]).Select(c => c.Name));
    }

    [Fact]
    public void The_game_names_a_container_by_the_FNV_1a_64_of_its_bytes()
    {
        // The offset basis for no bytes; "a" is the published test vector for 64-bit FNV-1a.
        Assert.Equal(0xcbf29ce484222325ul, ShaderContainer.Fnv1a64([]));
        Assert.Equal(0xaf63dc4c8601ec8cul, ShaderContainer.Fnv1a64("a"u8));
    }

    [Fact]
    public void A_container_larger_than_one_frame_is_decoded_whole()
    {
        var big = TestArc.Container(vertex: false, 0x2400, seed: 9);
        TestArc.Lzx().Add(big).WriteTo(Game("Big.arc"));

        Assert.Equal(big, Assert.Single(Read()).Bytes);
    }

    [Fact]
    public void A_stored_entry_is_read_as_it_is()
    {
        TestArc.Stored().Add(Vertex).WriteTo(Game("CutScenes.arc"));

        Assert.Equal(Vertex, Assert.Single(Read()).Bytes);
    }

    [Fact]
    public void A_multi_part_entry_is_skipped_without_failing_the_archive()
    {
        TestArc.Lzx().AddMultiPart().Add(Pixel).WriteTo(Game("Textures.arc"));

        Assert.Equal(Pixel, Assert.Single(Read()).Bytes);
    }

    [Fact]
    public void An_entry_that_is_not_a_container_is_ignored()
    {
        var text = new byte[256];
        "not a shader, just data"u8.CopyTo(text);
        TestArc.Lzx().Add(text).Add(Vertex).WriteTo(Game("Mixed.arc"));
        TestArc.Stored().Add(text).WriteTo(Game("Stored.arc"));

        Assert.Equal(Vertex, Assert.Single(Read()).Bytes);
    }

    [Fact]
    public void A_container_whose_sizes_do_not_add_up_is_ignored()
    {
        var wrong = (byte[])Pixel.Clone();
        wrong[11] += 4; // the microcode size no longer matches the entry
        TestArc.Lzx().Add(wrong).WriteTo(Game("Wrong.arc"));

        Assert.Empty(Read());
    }

    [Fact]
    public void A_container_in_two_archives_is_listed_once_in_name_order()
    {
        TestArc.Lzx().Add(Vertex).Add(Pixel).WriteTo(Game("A.arc"));
        TestArc.Lzx().Add(Pixel).WriteTo(Path.Combine(_root, "sub", "B.arc"));

        var names = Read().Select(c => c.Name).ToList();

        Assert.Equal(2, names.Count);
        Assert.Equal(names.Order(StringComparer.Ordinal), names);
    }

    [Fact]
    public void Files_that_are_not_archives_are_left_alone()
    {
        File.WriteAllText(Game("Readme.txt"), "no archive");
        Assert.Empty(Read());
    }

    [Fact]
    public void A_damaged_archive_fails_with_its_name_in_the_message()
    {
        var bytes = TestArc.Lzx().Add(Pixel).ToBytes();
        File.WriteAllBytes(Game("Good.arc"), TestArc.Lzx().Add(Vertex).ToBytes());
        File.WriteAllBytes(Path.Combine(_root, "Broken.arc"), bytes[..^20]);

        var error = Assert.Throws<InvalidDataException>(() => Read());

        Assert.Contains("Broken.arc", error.Message);
    }

    [Fact]
    public void An_archive_with_a_cut_header_fails_with_its_name_in_the_message()
    {
        File.WriteAllBytes(Game("Cut.arc"), new byte[10]);

        Assert.Contains("Cut.arc", Assert.Throws<InvalidDataException>(() => Read()).Message);
    }

    [Fact]
    public void An_entry_with_damaged_compressed_data_fails_with_the_archives_name()
    {
        var bytes = TestArc.Lzx().Add(Pixel).ToBytes();
        // Frame data starts after the table (0x30), the entry header (0x2C) and the frame header (5): make the LZX block type invalid.
        bytes[0x30 + 0x2C + 5 + 1] = 0x00;
        File.WriteAllBytes(Game("Garbled.arc"), bytes);

        Assert.Contains("Garbled.arc", Assert.Throws<InvalidDataException>(() => Read()).Message);
    }

    [Fact]
    public void Progress_counts_entries_up_to_the_total()
    {
        TestArc.Lzx().Add(Pixel).Add(Vertex).WriteTo(Game("A.arc"));
        TestArc.Stored().Add(Pixel).WriteTo(Game("B.arc"));
        var reports = new List<(int Done, int Total)>();

        Read(new SyncProgress<(int Done, int Total)>(reports.Add));

        Assert.Equal((3, 3), reports[^1]);
        Assert.All(reports, report => Assert.Equal(3, report.Total));
    }

    [Fact]
    public void Cancelling_stops_the_read()
    {
        TestArc.Lzx().Add(Pixel).WriteTo(Game("A.arc"));
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => ArchiveShaders.Read(_root, null, cancel.Token));
    }
}
