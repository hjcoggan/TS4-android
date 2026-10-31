using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

public sealed class DiscArtAchievementsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ts4-ach-").FullName;
    private readonly List<string> _log = [];

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Art => Path.Combine(_root, "art");

    private static TestAchievement Achievement(ushort id, uint image, int seed, string name = "", string description = "", string howTo = "") =>
        new(id, image, (ushort)(id * 5), name.Length > 0 ? name : $"Name {id}", description.Length > 0 ? description : $"Did {id}",
            howTo.Length > 0 ? howTo : $"Do {id}", TestArtImages.Png(4, 4, seed));

    private DiscArtResult Read(TestXexImage module)
    {
        var disc = new TestArtDisc();
        disc.Files[TestArtDisc.Module] = (module with { TitleImage = disc.TitleIcon }).Build();
        return DiscArt.FromFolder(disc.WriteFolder(Path.Combine(_root, "disc")), Art, _log.Add);
    }

    [Fact]
    public void Each_achievement_is_kept_with_its_icon_name_description_and_points_in_table_order()
    {
        TestAchievement[] achievements = [Achievement(2, 0x31, 1, "Ace"), Achievement(1, 0x32, 2, "Baseliner", "Won a rally")];

        Read(new TestXexImage { Achievements = achievements, Compressed = true, Encrypted = true });

        var entries = DiscArtFolder.Open(Art).Entries(DiscArtCollection.Achievements);
        Assert.Equal(["Ace", "Baseliner"], entries.Select(entry => entry.Title));
        Assert.Equal(["Did 2", "Won a rally"], entries.Select(entry => entry.Detail));
        Assert.Equal([10, 5], entries.Select(entry => entry.Points));
        Assert.Equal(achievements[0].Icon, File.ReadAllBytes(entries[0].ImagePath));
        Assert.Equal(achievements[1].Icon, File.ReadAllBytes(entries[1].ImagePath));
        Assert.Equal(Path.Combine(Art, "achievements"), Path.GetDirectoryName(entries[0].ImagePath));
        Assert.Empty(_log);
        Assert.True(DiscArtFolder.Open(Art).IsComplete);
    }

    [Fact]
    public void Without_a_string_table_the_achievements_keep_their_icons_and_have_no_names()
    {
        Read(new TestXexImage { Achievements = [Achievement(1, 0x1, 1)], OmitStrings = true });

        var entry = Assert.Single(DiscArtFolder.Open(Art).Entries(DiscArtCollection.Achievements));
        Assert.Equal("", entry.Title);
        Assert.Empty(_log);
    }

    [Fact]
    public void An_achievement_whose_icon_is_missing_is_left_out_and_the_others_stay()
    {
        Read(new TestXexImage { Achievements = [Achievement(1, 0x1, 1) with { Icon = null }, Achievement(2, 0x2, 2)] });

        Assert.Equal(["Name 2"], DiscArtFolder.Open(Art).Entries(DiscArtCollection.Achievements).Select(entry => entry.Title));
        Assert.Empty(_log);
    }

    [Fact]
    public void An_achievement_whose_icon_is_not_a_picture_is_logged_and_left_out()
    {
        Read(new TestXexImage { Achievements = [Achievement(1, 0x1, 1) with { Icon = [1, 2, 3, 4] }, Achievement(2, 0x2, 2)] });

        Assert.Single(DiscArtFolder.Open(Art).Entries(DiscArtCollection.Achievements));
        Assert.Contains(_log, line => line.Contains("achievement 1", StringComparison.Ordinal));
    }

    [Fact]
    public void A_damaged_achievement_table_is_logged_and_the_title_icon_still_counts()
    {
        var result = Read(new TestXexImage { RawAchievementTable = [.. "XACH"u8, 0, 0, 0, 1, 0, 0, 0, 6, 0xFF, 0xFF] });

        Assert.Contains(DiscArtSlot.TitleIcon, result.Found);
        Assert.Empty(DiscArtFolder.Open(Art).Entries(DiscArtCollection.Achievements));
        Assert.Single(_log);
        Assert.False(DiscArtFolder.Open(Art).IsComplete);
    }

    [Fact]
    public void Text_with_tabs_or_line_breaks_stays_on_its_line()
    {
        Read(new TestXexImage { Achievements = [Achievement(1, 0x1, 1, "Tab\there", "Two\nlines")] });

        var entry = Assert.Single(DiscArtFolder.Open(Art).Entries(DiscArtCollection.Achievements));
        Assert.Equal("Tab here", entry.Title);
        Assert.Equal("Two lines", entry.Detail);
    }

    [Fact]
    public void A_disc_without_achievements_empties_an_earlier_discs_collection()
    {
        Read(new TestXexImage { Achievements = [Achievement(1, 0x1, 1), Achievement(2, 0x2, 2)] });

        Read(new TestXexImage());

        Assert.Empty(DiscArtFolder.Open(Art).Entries(DiscArtCollection.Achievements));
        Assert.Empty(Directory.GetFiles(Path.Combine(Art, "achievements")));
    }

    [Fact]
    public void A_shorter_list_removes_the_pictures_it_no_longer_names()
    {
        Read(new TestXexImage { Achievements = [Achievement(1, 0x1, 1), Achievement(2, 0x2, 2)] });

        Read(new TestXexImage { Achievements = [Achievement(3, 0x3, 3)] });

        Assert.Equal(["001.png", "list.txt"], Directory.GetFiles(Path.Combine(Art, "achievements")).Select(Path.GetFileName).Order());
    }

    [Fact]
    public void A_folder_written_before_the_achievements_were_read_counts_as_incomplete()
    {
        Read(new TestXexImage());
        var index = Path.Combine(Art, DiscArtFolder.IndexFileName);
        File.WriteAllLines(index, File.ReadAllLines(index).Where(line => !line.StartsWith("format=", StringComparison.Ordinal)));

        Assert.False(DiscArtFolder.Open(Art).IsComplete);
    }

    [Fact]
    public void A_list_line_naming_a_path_outside_the_folder_is_skipped()
    {
        Read(new TestXexImage { Achievements = [Achievement(1, 0x1, 1)] });
        var list = Path.Combine(Art, "achievements", DiscArtFolder.ListFileName);
        File.AppendAllText(list, "..\\background.jpg\tEscape\tx\t0\n");

        Assert.Single(DiscArtFolder.Open(Art).Entries(DiscArtCollection.Achievements));
    }
}
