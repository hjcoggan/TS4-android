using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

/// <summary>The art folder as a shared place: what makes it complete, when it counts as changed, and writes that overlap or fail.</summary>
public sealed class DiscArtFolderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ts4-art-folder-").FullName;
    private readonly List<string> _log = [];

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Art => Path.Combine(_root, "art");

    private string DiscFolder(TestArtDisc disc, string name = "disc") => disc.WriteFolder(Path.Combine(_root, name));

    private DiscArtResult Read(TestArtDisc disc, string name = "disc") => DiscArt.FromFolder(DiscFolder(disc, name), Art, _log.Add);

    // Item 12: complete means a successful read left an index and every image it lists is there.

    [Fact]
    public void A_disc_that_genuinely_lacks_an_image_still_gives_a_complete_folder_that_is_not_restored_again()
    {
        var disc = new TestArtDisc();
        disc.Files.Remove(TestArtDisc.NxeArt);
        var folder = DiscFolder(disc);
        DiscArt.FromFolder(folder, Art, _log.Add);

        Assert.True(DiscArtFolder.Open(Art).IsComplete);
        Assert.Null(DiscArt.RestoreFromFolder(folder, Art, _log.Add));
    }

    [Fact]
    public void A_disc_with_no_art_at_all_gives_a_complete_folder_too()
    {
        var empty = Path.Combine(_root, "empty-disc");
        Directory.CreateDirectory(empty);
        DiscArt.FromFolder(empty, Art, _log.Add);

        Assert.True(DiscArtFolder.Open(Art).IsComplete);
        Assert.Null(DiscArt.RestoreFromFolder(empty, Art, _log.Add));
    }

    [Fact]
    public void A_listed_image_that_is_gone_makes_the_folder_incomplete_and_a_restore_reads_it_again()
    {
        var disc = new TestArtDisc();
        disc.Files.Remove(TestArtDisc.NxeArt);
        var folder = DiscFolder(disc);
        DiscArt.FromFolder(folder, Art, _log.Add);
        File.Delete(DiscArtFolder.Open(Art).ImagePath(DiscArtSlot.Logo)!);

        Assert.False(DiscArtFolder.Open(Art).IsComplete);
        Assert.NotNull(DiscArt.RestoreFromFolder(folder, Art, _log.Add));
        Assert.True(DiscArtFolder.Open(Art).IsComplete);
        Assert.NotNull(DiscArtFolder.Open(Art).ImagePath(DiscArtSlot.Logo));
    }

    [Fact]
    public void A_missing_index_or_a_missing_folder_is_incomplete()
    {
        Assert.False(DiscArtFolder.Open(Art).IsComplete);

        Read(new TestArtDisc());
        File.Delete(Path.Combine(Art, DiscArtFolder.IndexFileName));
        Assert.False(DiscArtFolder.Open(Art).IsComplete);
    }

    [Fact]
    public void A_missing_icon_file_the_index_lists_makes_the_folder_incomplete()
    {
        Read(new TestArtDisc());
        File.Delete(Path.Combine(Art, DiscArtFolder.IconFileName));

        Assert.False(DiscArtFolder.Open(Art).IsComplete);
    }

    [Fact]
    public void A_read_that_logged_a_problem_is_not_a_successful_read_so_it_is_tried_again()
    {
        var disc = new TestArtDisc();
        disc.Files[TestArtDisc.NxeArt][TestStfs.DataBlockOffset(2) + 16] ^= 0xFF;

        Read(disc);

        Assert.NotEmpty(_log);
        Assert.False(DiscArtFolder.Open(Art).IsComplete);
    }

    // Item 1: a second disc that writes the same file names is still a different look.

    [Fact]
    public void The_stamp_is_the_same_for_the_same_folder_and_changes_when_a_second_disc_writes_the_same_file_names()
    {
        Read(new TestArtDisc());
        var first = DiscArtFolder.Open(Art).Stamp;
        Assert.Equal(first, DiscArtFolder.Open(Art).Stamp);
        var names = TestArtFolder.Files(Art);

        Read(OtherArt(), "disc-b");

        Assert.Equal(names, TestArtFolder.Files(Art));
        Assert.NotEqual(first, DiscArtFolder.Open(Art).Stamp);
        Assert.NotEmpty(first);
    }

    [Fact]
    public void An_empty_or_missing_folder_has_an_empty_stamp()
    {
        Assert.Equal("", DiscArtFolder.Open(Path.Combine(_root, "never-made")).Stamp);
        Directory.CreateDirectory(Art);
        Assert.Equal("", DiscArtFolder.Open(Art).Stamp);
    }

    // The same four file names and the same slots, with pictures of other sizes.
    private static TestArtDisc OtherArt()
    {
        var disc = new TestArtDisc();
        var nxeart = new TestStfs { ContentType = 0x00030000, DisplayName = "nxeart", Magic = "PIRS" };
        nxeart.Files.Add(("DashStyle", new byte[4]));
        nxeart.Files.Add(("nxebg.jpg", TestArtImages.Jpeg(96, 54, 11)));
        nxeart.Files.Add(("nxeslot.jpg", TestArtImages.Jpeg(21, 16, 12)));
        disc.Files[TestArtDisc.NxeArt] = nxeart.Build();
        disc.Files[TestArtDisc.PressStart] = TestXuiz.Build(
            ("Bitmaps\\PressStart.PNG", TestArtImages.Png(32, 18, 13)),
            ("Bitmaps\\Logo.PNG", TestArtImages.Png(28, 9, 14)));
        disc.Files[TestArtDisc.Module] = new TestXexImage { TitleImage = TestArtImages.Png(48, 48, 15) }.Build();
        return disc;
    }

    // Item 3: two writers to one folder.

    [Fact]
    public void Writers_that_overlap_neither_fail_nor_leave_temporary_files()
    {
        var a = DiscFolder(new TestArtDisc(), "disc-a");
        var b = DiscFolder(OtherArt(), "disc-b");
        var problems = new System.Collections.Concurrent.ConcurrentBag<string>();
        var results = new System.Collections.Concurrent.ConcurrentBag<DiscArtResult>();

        Parallel.For(0, 40, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            try
            {
                results.Add(DiscArt.FromFolder(i % 2 == 0 ? a : b, Art, problems.Add));
            }
            catch (Exception error)
            {
                problems.Add(error.GetType().Name + ": " + error.Message);
            }
        });

        Assert.Empty(problems);
        Assert.All(results, result => Assert.Equal(5, result.Found.Count));
        Assert.DoesNotContain(TestArtFolder.Files(Art), name => name.EndsWith(".tmp", StringComparison.Ordinal));
        Assert.True(DiscArtFolder.Open(Art).IsComplete);
    }

    [Fact]
    public void A_restore_that_overlaps_a_disc_read_does_not_fail_either()
    {
        var a = DiscFolder(new TestArtDisc(), "disc-a");
        var problems = new System.Collections.Concurrent.ConcurrentBag<string>();

        Parallel.For(0, 40, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            if (i % 2 == 0) DiscArt.FromFolder(a, Art, problems.Add);
            else DiscArt.RestoreFromFolder(a, Art, problems.Add);
        });

        Assert.Empty(problems);
        Assert.DoesNotContain(TestArtFolder.Files(Art), name => name.EndsWith(".tmp", StringComparison.Ordinal));
        Assert.True(DiscArtFolder.Open(Art).IsComplete);
    }

    [Fact]
    public void Temporary_files_a_crashed_write_left_behind_are_cleaned_up()
    {
        Directory.CreateDirectory(Art);
        File.WriteAllText(Path.Combine(Art, "logo.png.tmp"), "half a file");
        File.WriteAllText(Path.Combine(Art, "index.txt.3f2a.tmp"), "half a file");

        Read(new TestArtDisc());

        Assert.DoesNotContain(TestArtFolder.Files(Art), name => name.EndsWith(".tmp", StringComparison.Ordinal));
        Assert.True(DiscArtFolder.Open(Art).IsComplete);
    }

    // Item 4: a write that stops half way leaves the earlier disc's folder as it was.

    [Fact]
    public void A_write_that_fails_half_way_keeps_the_earlier_folder_whole()
    {
        Read(new TestArtDisc());
        var before = DiscArtFolder.Open(Art);
        var stamp = before.Stamp;
        var other = OtherArt();
        other.Files.Remove(TestArtDisc.NxeArt);
        var folder = DiscFolder(other, "disc-b");
        _log.Clear();

        // The Logo file is open in a reader (a window drawing it), so it cannot be replaced.
        using (new FileStream(before.ImagePath(DiscArtSlot.Logo)!, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var result = DiscArt.FromFolder(folder, Art, _log.Add);
            Assert.Empty(result.Found);
        }

        Assert.Contains("Disc art", Assert.Single(_log));
        var after = DiscArtFolder.Open(Art);
        Assert.True(after.IsComplete);
        Assert.Equal(stamp, after.Stamp);
        Assert.All(Enum.GetValues<DiscArtSlot>(), slot => Assert.True(File.Exists(after.ImagePath(slot)), slot.ToString()));
        Assert.DoesNotContain(TestArtFolder.Files(Art), name => name.EndsWith(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    public void Files_the_new_disc_has_no_use_for_are_removed_once_the_new_index_is_written()
    {
        Read(new TestArtDisc());
        var second = new TestArtDisc();
        second.Files.Remove(TestArtDisc.NxeArt);

        Read(second, "disc-b");

        Assert.Equal(["icon.png", "index.txt", "logo.png", "setup-background.png", "topspin4.ico"], TestArtFolder.Files(Art));
        Assert.True(DiscArtFolder.Open(Art).IsComplete);
    }
}
