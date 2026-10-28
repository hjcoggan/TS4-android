using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

/// <summary>
/// After setup (and at every start) the art is read again from the prepared disc folder when the art folder is missing or
/// incomplete, so deleting it, or an earlier read that failed, costs nothing but a moment.
/// </summary>
public sealed class DiscArtRestoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ts4-art-restore-").FullName;
    private readonly List<string> _log = [];

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Art => Path.Combine(_root, "user", DiscArt.FolderName);
    private string Disc => Path.Combine(_root, "prepared", "gamedata");

    [Fact]
    public void A_missing_art_folder_is_read_from_the_prepared_disc_folder()
    {
        var disc = new TestArtDisc();
        disc.WriteFolder(Disc);

        var result = DiscArt.RestoreFromFolder(Disc, Art, _log.Add);

        Assert.NotNull(result);
        Assert.True(DiscArtFolder.Open(Art).IsComplete);
        Assert.Equal(disc.Logo, File.ReadAllBytes(DiscArtFolder.Open(Art).ImagePath(DiscArtSlot.Logo)!));
        Assert.Equal(Disc, TestArtFolder.Source(Art));
        Assert.Empty(_log);
    }

    [Fact]
    public void An_incomplete_art_folder_is_read_again_and_completed()
    {
        var disc = new TestArtDisc();
        disc.WriteFolder(Disc);
        DiscArt.FromFolder(Disc, Art);
        File.Delete(DiscArtFolder.Open(Art).ImagePath(DiscArtSlot.BoxArt)!);
        Assert.False(DiscArtFolder.Open(Art).IsComplete);

        var result = DiscArt.RestoreFromFolder(Disc, Art, _log.Add);

        Assert.NotNull(result);
        Assert.True(DiscArtFolder.Open(Art).IsComplete);
    }

    [Fact]
    public void A_complete_art_folder_is_left_alone_and_the_disc_folder_is_not_even_looked_at()
    {
        new TestArtDisc().WriteFolder(Disc);
        DiscArt.FromFolder(Disc, Art);
        Directory.Delete(Disc, recursive: true);

        var result = DiscArt.RestoreFromFolder(Disc, Art, _log.Add);

        Assert.Null(result);
        Assert.True(DiscArtFolder.Open(Art).IsComplete);
        Assert.Empty(_log);
    }

    [Fact]
    public void A_prepared_folder_that_is_gone_is_logged_and_gives_no_error()
    {
        var result = DiscArt.RestoreFromFolder(Disc, Art, _log.Add);

        Assert.NotNull(result);
        Assert.Empty(result.Found);
        Assert.Contains("Disc art", Assert.Single(_log));
        Assert.Null(DiscArtFolder.Open(Art).ImagePath(DiscArtSlot.Logo));
    }

    [Fact]
    public void A_damaged_source_in_the_prepared_folder_is_logged_and_the_rest_still_counts()
    {
        var disc = new TestArtDisc();
        disc.Files[TestArtDisc.NxeArt][TestStfs.DataBlockOffset(2) + 16] ^= 0xFF;
        disc.WriteFolder(Disc);

        DiscArt.RestoreFromFolder(Disc, Art, _log.Add);

        Assert.Contains("nxeart", Assert.Single(_log));
        Assert.NotNull(DiscArtFolder.Open(Art).ImagePath(DiscArtSlot.Logo));
        Assert.Null(DiscArtFolder.Open(Art).ImagePath(DiscArtSlot.Background));
    }

    [Fact]
    public void An_art_folder_that_cannot_be_written_is_logged_and_never_thrown()
    {
        new TestArtDisc().WriteFolder(Disc);
        var blocked = Path.Combine(_root, "blocked");
        File.WriteAllText(blocked, "a file where the art folder should be");

        var result = DiscArt.RestoreFromFolder(Disc, blocked, _log.Add);

        Assert.NotNull(result);
        Assert.Empty(result.Found);
        Assert.Contains("Disc art", Assert.Single(_log));
    }
}
