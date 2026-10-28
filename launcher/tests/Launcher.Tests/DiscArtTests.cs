using System.Buffers.Binary;
using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

public sealed class DiscArtTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ts4-art-").FullName;
    private readonly List<string> _log = [];

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Art => Path.Combine(_root, "art");

    private string DiscFolder(TestArtDisc disc) => disc.WriteFolder(Path.Combine(_root, "disc"));

    private DiscArtResult FromFolder(TestArtDisc disc) => DiscArt.FromFolder(DiscFolder(disc), Art, _log.Add);

    private byte[] Read(string name) => File.ReadAllBytes(Path.Combine(Art, name));

    private bool Exists(string name) => File.Exists(Path.Combine(Art, name));

    [Fact]
    public void The_nxeart_package_gives_the_background_and_box_art_as_they_are()
    {
        var disc = new TestArtDisc();

        var result = FromFolder(disc);

        Assert.Equal(disc.Background, Read("background.jpg"));
        Assert.Equal(disc.BoxArt, Read("boxart.jpg"));
        Assert.Contains(DiscArtSlot.Background, result.Found);
        Assert.Contains(DiscArtSlot.BoxArt, result.Found);
        Assert.Empty(_log);
    }

    [Fact]
    public void The_press_start_package_gives_the_logo_and_the_setup_background_whatever_the_case_of_their_names()
    {
        var disc = new TestArtDisc();
        disc.Files[TestArtDisc.PressStart] = TestXuiz.Build(
            ("Scenes\\PressStartScreen.xur", TestStfs.Content(40, 6)),
            ("bitmaps\\pressstart.png", disc.SetupBackground),
            ("BITMAPS\\LOGO.PNG", disc.Logo));

        var result = FromFolder(disc);

        Assert.Equal(disc.Logo, Read("logo.png"));
        Assert.Equal(disc.SetupBackground, Read("setup-background.png"));
        Assert.Contains(DiscArtSlot.Logo, result.Found);
        Assert.Contains(DiscArtSlot.SetupBackground, result.Found);
        Assert.Empty(_log);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void The_modules_title_image_is_found_plain_encrypted_or_compressed_as_well(bool encrypted, bool compressed)
    {
        var disc = new TestArtDisc();
        disc.Files[TestArtDisc.Module] = new TestXexImage { TitleImage = disc.TitleIcon, Encrypted = encrypted, Compressed = compressed }.Build();

        var result = FromFolder(disc);

        Assert.Equal(disc.TitleIcon, Read("icon.png"));
        Assert.Contains(DiscArtSlot.TitleIcon, result.Found);
        Assert.Empty(_log);
    }

    [Fact]
    public void A_module_protected_with_the_development_key_still_gives_its_title_image()
    {
        var disc = new TestArtDisc();
        disc.Files[TestArtDisc.Module] = new TestXexImage { TitleImage = disc.TitleIcon, Encrypted = true, Compressed = true, DevkitKey = true }.Build();

        FromFolder(disc);

        Assert.Equal(disc.TitleIcon, Read("icon.png"));
        Assert.Empty(_log);
    }

    [Fact]
    public void The_title_icon_is_also_written_as_an_ico_wrapping_the_png()
    {
        var disc = new TestArtDisc();

        FromFolder(disc);

        var ico = Read("topspin4.ico");
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(ico));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(2)));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(4)));
        Assert.Equal(64, ico[6]);
        Assert.Equal(64, ico[7]);
        Assert.Equal(32, BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(12)));
        Assert.Equal(disc.TitleIcon.Length, BinaryPrimitives.ReadInt32LittleEndian(ico.AsSpan(14)));
        Assert.Equal(22, BinaryPrimitives.ReadInt32LittleEndian(ico.AsSpan(18)));
        Assert.Equal(disc.TitleIcon, ico[22..]);
    }

    [Fact]
    public void The_index_names_what_was_found_and_the_disc_it_came_from()
    {
        var disc = new TestArtDisc();
        var folder = DiscFolder(disc);

        DiscArt.FromFolder(folder, Art, _log.Add);

        var index = File.ReadAllLines(Path.Combine(Art, "index.txt"));
        Assert.Contains("source=" + folder, index);
        Assert.Contains("media_id=60819732", index);
        Assert.Contains("title_id=54540859", index);
        Assert.Contains("background=background.jpg", index);
        Assert.Contains("boxart=boxart.jpg", index);
        Assert.Contains("logo=logo.png", index);
        Assert.Contains("icon=icon.png", index);
        Assert.Contains("setup-background=setup-background.png", index);
        Assert.Contains("ico=topspin4.ico", index);
    }

    [Fact]
    public void A_disc_without_the_nxeart_package_gives_no_background_or_box_art_and_no_error()
    {
        var disc = new TestArtDisc();
        disc.Files.Remove(TestArtDisc.NxeArt);

        var result = FromFolder(disc);

        Assert.False(Exists("background.jpg"));
        Assert.False(Exists("boxart.jpg"));
        Assert.Equal([DiscArtSlot.Logo, DiscArtSlot.TitleIcon, DiscArtSlot.SetupBackground], result.Found);
        Assert.Empty(result.Problems);
        Assert.Empty(_log);
    }

    [Fact]
    public void A_package_without_the_image_gives_no_file_for_it_and_no_error()
    {
        var disc = new TestArtDisc();
        // PressStart.xzp without a logo; the module with a resource that has no title image; default.xex without any resource.
        disc.Files[TestArtDisc.PressStart] = TestXuiz.Build(("Bitmaps\\PressStart.PNG", disc.SetupBackground));
        disc.Files[TestArtDisc.Module] = new TestXexImage { TitleImage = null }.Build();
        var nxeart = new TestStfs { ContentType = 0x00030000, Magic = "PIRS" };
        nxeart.Files.Add(("nxebg.jpg", disc.Background));
        disc.Files[TestArtDisc.NxeArt] = nxeart.Build();

        var result = FromFolder(disc);

        Assert.Equal([DiscArtSlot.Background, DiscArtSlot.SetupBackground], result.Found);
        Assert.False(Exists("boxart.jpg"));
        Assert.False(Exists("logo.png"));
        Assert.False(Exists("icon.png"));
        Assert.False(Exists("topspin4.ico"));
        Assert.Empty(_log);

        disc.Files[TestArtDisc.Module] = new TestXexImage { IncludeResourceInfo = false }.Build();
        Assert.Empty(FromFolder(disc).Problems);
        Assert.False(Exists("icon.png"));
    }

    [Fact]
    public void A_damaged_nxeart_package_gives_a_logged_reason_and_no_file_for_its_images_but_the_other_sources_still_count()
    {
        var disc = new TestArtDisc();
        disc.Files[TestArtDisc.NxeArt][TestStfs.DataBlockOffset(2) + 16] ^= 0xFF;

        var result = FromFolder(disc);

        Assert.False(Exists("background.jpg"));
        Assert.False(Exists("boxart.jpg"));
        var line = Assert.Single(_log);
        Assert.Contains("nxeart", line);
        Assert.Contains("damaged", line);
        Assert.Equal(_log, result.Problems);
        Assert.Equal([DiscArtSlot.Logo, DiscArtSlot.TitleIcon, DiscArtSlot.SetupBackground], result.Found);
    }

    [Fact]
    public void A_damaged_xui_package_gives_a_logged_reason_and_no_file()
    {
        var disc = new TestArtDisc();
        disc.Files[TestArtDisc.PressStart] = disc.Files[TestArtDisc.PressStart][..^200];

        FromFolder(disc);

        Assert.False(Exists("logo.png"));
        Assert.False(Exists("setup-background.png"));
        var line = Assert.Single(_log);
        Assert.Contains("PressStart.xzp", line);
        Assert.True(Exists("background.jpg"));
    }

    [Fact]
    public void A_damaged_module_gives_a_logged_reason_and_no_title_icon_or_ico()
    {
        var disc = new TestArtDisc();
        disc.Files[TestArtDisc.Module][0x1000 + 100] ^= 0xFF;

        FromFolder(disc);

        Assert.False(Exists("icon.png"));
        Assert.False(Exists("topspin4.ico"));
        var line = Assert.Single(_log);
        Assert.Contains("default.xex", line);
        Assert.True(Exists("logo.png"));
    }

    [Fact]
    public void An_entry_that_is_not_the_image_it_should_be_is_logged_and_left_out()
    {
        var disc = new TestArtDisc();
        var cutOff = disc.Logo[..^20];
        disc.Files[TestArtDisc.PressStart] = TestXuiz.Build(("Bitmaps\\PressStart.PNG", disc.SetupBackground), ("Bitmaps\\Logo.PNG", cutOff));

        FromFolder(disc);

        Assert.False(Exists("logo.png"));
        Assert.True(Exists("setup-background.png"));
        Assert.Contains("Logo.PNG", Assert.Single(_log));
    }

    [Fact]
    public void Images_keep_their_own_format()
    {
        var disc = new TestArtDisc();
        var jpegLogo = TestArtImages.Jpeg(56, 17, 9);
        disc.Files[TestArtDisc.PressStart] = TestXuiz.Build(("Bitmaps\\Logo.PNG", jpegLogo));

        FromFolder(disc);

        Assert.Equal(jpegLogo, Read("logo.jpg"));
        Assert.False(Exists("logo.png"));
    }

    [Fact]
    public void A_second_disc_replaces_what_the_first_left_even_in_the_other_format()
    {
        var first = new TestArtDisc();
        FromFolder(first);
        Assert.True(Exists("logo.png"));
        var second = new TestArtDisc();
        second.Files[TestArtDisc.PressStart] = TestXuiz.Build(("Bitmaps\\PressStart.PNG", second.SetupBackground));
        Directory.Delete(Path.Combine(_root, "disc"), recursive: true);

        FromFolder(second);

        Assert.False(Exists("logo.png"));
        Assert.Equal(second.SetupBackground, Read("setup-background.png"));
    }

    [Fact]
    public void The_art_comes_out_the_same_from_an_iso_as_from_the_prepared_folder()
    {
        var disc = new TestArtDisc();
        var iso = Path.Combine(_root, "Top Spin 4.iso");
        disc.AddTo(new TestDisc()).WriteImage(iso);

        var result = DiscArt.FromIso(iso, Art, _log.Add);

        Assert.Equal(disc.Background, Read("background.jpg"));
        Assert.Equal(disc.BoxArt, Read("boxart.jpg"));
        Assert.Equal(disc.Logo, Read("logo.png"));
        Assert.Equal(disc.SetupBackground, Read("setup-background.png"));
        Assert.Equal(disc.TitleIcon, Read("icon.png"));
        Assert.Equal(5, result.Found.Count);
        Assert.Contains("source=" + iso, File.ReadAllLines(Path.Combine(Art, "index.txt")));
        Assert.Empty(_log);
    }

    [Fact]
    public void A_file_that_is_not_a_disc_image_gives_a_logged_reason_and_leaves_the_art_folder_alone()
    {
        FromFolder(new TestArtDisc());
        var notAnIso = Path.Combine(_root, "words.iso");
        File.WriteAllText(notAnIso, "just some words");
        _log.Clear();

        var result = DiscArt.FromIso(notAnIso, Art, _log.Add);

        Assert.Empty(result.Found);
        Assert.Contains("not an Xbox 360 disc image", Assert.Single(_log));
        Assert.True(Exists("background.jpg"));
        Assert.True(Exists("index.txt"));
    }

    [Fact]
    public void A_missing_disc_gives_a_logged_reason()
    {
        var result = DiscArt.FromIso(Path.Combine(_root, "nothing.iso"), Art, _log.Add);

        Assert.Empty(result.Found);
        Assert.Single(_log);
        Assert.False(Directory.Exists(Art));
    }

    [Fact]
    public void The_art_folder_says_which_images_exist()
    {
        var disc = new TestArtDisc();
        disc.Files.Remove(TestArtDisc.NxeArt);
        var folder = DiscFolder(disc);
        DiscArt.FromFolder(folder, Art, _log.Add);

        var art = DiscArtFolder.Open(Art);

        Assert.False(art.Has(DiscArtSlot.Background));
        Assert.Null(art.ImagePath(DiscArtSlot.BoxArt));
        Assert.Equal(Path.Combine(Art, "logo.png"), art.ImagePath(DiscArtSlot.Logo));
        Assert.Equal(Path.Combine(Art, "icon.png"), art.ImagePath(DiscArtSlot.TitleIcon));
        Assert.Equal(Path.Combine(Art, "topspin4.ico"), art.IconPath);
        Assert.Equal([DiscArtSlot.Logo, DiscArtSlot.TitleIcon, DiscArtSlot.SetupBackground], art.Found);
        Assert.False(art.IsComplete);
        Assert.Equal(folder, art.Source);
    }

    [Fact]
    public void A_full_art_folder_is_complete()
    {
        FromFolder(new TestArtDisc());

        Assert.True(DiscArtFolder.Open(Art).IsComplete);
    }

    [Fact]
    public void A_missing_folder_a_missing_index_or_a_deleted_image_leaves_slots_empty()
    {
        Assert.Empty(DiscArtFolder.Open(Path.Combine(_root, "never-made")).Found);

        FromFolder(new TestArtDisc());
        File.Delete(Path.Combine(Art, "logo.png"));
        var art = DiscArtFolder.Open(Art);
        Assert.False(art.Has(DiscArtSlot.Logo));
        Assert.False(art.IsComplete);
        Assert.True(art.Has(DiscArtSlot.Background));

        File.Delete(Path.Combine(Art, "index.txt"));
        Assert.Empty(DiscArtFolder.Open(Art).Found);
    }

    [Fact]
    public void An_index_cannot_point_outside_the_art_folder()
    {
        FromFolder(new TestArtDisc());
        File.WriteAllText(Path.Combine(_root, "secret.png"), "x");
        File.WriteAllText(Path.Combine(Art, "index.txt"), "logo=../secret.png\nbackground=background.jpg\n");

        var art = DiscArtFolder.Open(Art);

        Assert.False(art.Has(DiscArtSlot.Logo));
        Assert.True(art.Has(DiscArtSlot.Background));
    }
}
