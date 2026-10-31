using System.Buffers.Binary;
using System.IO.Compression;
using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

public sealed class XprTextureTests
{
    // Values of Xenia's GetTiledOffset2D (block x, block y, pitch in blocks, log2 of bytes per block).
    [Theory]
    [InlineData(0, 0, 32, 2, 0)]
    [InlineData(1, 0, 32, 2, 4)]
    [InlineData(0, 1, 32, 2, 16)]
    [InlineData(7, 3, 32, 2, 316)]
    [InlineData(9, 17, 64, 2, 2132)]
    [InlineData(40, 33, 64, 2, 12368)]
    [InlineData(3, 5, 32, 4, 4400)]
    [InlineData(31, 31, 32, 3, 8056)]
    public void The_tiled_offset_is_xenias(int x, int y, int pitch, int log2, int expected) =>
        Assert.Equal(expected, XprTexture.TiledOffset(x, y, pitch, log2));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void An_8888_texture_comes_back_as_its_pixels_tiled_or_linear(bool tiled)
    {
        var pixels = TestXpr.Pixels(40, 10, 3);
        var container = new TestXpr().Add8888("Federer_Portrait", 40, 10, pixels, tiled).ToBytes();

        var texture = Assert.Single(XprTexture.Parse(container));
        var image = texture.Decode();

        Assert.Equal("Federer_Portrait", texture.Name);
        Assert.Equal((40, 10), (image.Width, image.Height));
        Assert.Equal(pixels, image.Pixels);
    }

    [Fact]
    public void The_names_are_read_from_the_header_alone()
    {
        var container = new TestXpr().Add8888("A_Picture", 4, 4, TestXpr.Pixels(4, 4, 1)).AddOther("B_Normal").ToBytes();

        Assert.Equal(["A_Picture", "B_Normal"], XprTexture.Names(container.AsSpan(0, 0x800)));
        Assert.Empty(XprTexture.Names("not a container"u8));
    }

    [Fact]
    public void A_dxt1_texture_decodes_each_block_to_its_colour_and_is_cropped_to_its_size()
    {
        // 6x5 texels: 2x2 blocks of pure red, green, blue and white.
        byte[] blocks = [.. TestXpr.SolidBc1(0xF800), .. TestXpr.SolidBc1(0x07E0), .. TestXpr.SolidBc1(0x001F), .. TestXpr.SolidBc1(0xFFFF)];
        var container = new TestXpr().AddDxt("Rome_Logo", 6, 5, 18, blocks).ToBytes();

        var image = Assert.Single(XprTexture.Parse(container)).Decode();

        Assert.Equal((6, 5), (image.Width, image.Height));
        Assert.Equal([255, 0, 0, 255], Pixel(image, 0, 0));
        Assert.Equal([0, 255, 0, 255], Pixel(image, 4, 0));
        Assert.Equal([0, 0, 255, 255], Pixel(image, 3, 4));
        Assert.Equal([255, 255, 255, 255], Pixel(image, 5, 4));
    }

    [Fact]
    public void A_dxt5_block_keeps_its_interpolated_alpha()
    {
        // Alpha endpoints 255 and 0 with index 1 everywhere gives 0; the colour part is BC1 grey.
        var block = new byte[16];
        block[0] = 255;
        block[1] = 0;
        ulong indices = 0;
        for (var i = 0; i < 16; i++) indices |= 1UL << (3 * i);
        for (var i = 0; i < 6; i++) block[2 + i] = (byte)(indices >> (8 * i));
        TestXpr.SolidBc1(0x8410).CopyTo(block, 8);
        var container = new TestXpr().AddDxt("Boston_Preview", 4, 4, 20, block).ToBytes();

        var image = Assert.Single(XprTexture.Parse(container)).Decode();

        Assert.All(Enumerable.Range(0, 16), i => Assert.Equal(0, image.Pixels[i * 4 + 3]));
        Assert.Equal(132, image.Pixels[0]);
    }

    [Fact]
    public void A_format_it_does_not_read_says_so()
    {
        var texture = Assert.Single(XprTexture.Parse(new TestXpr().AddOther("Court_Normal").ToBytes()));

        Assert.Throws<NotSupportedException>(texture.Decode);
    }

    [Fact]
    public void A_cut_short_container_is_damaged()
    {
        var container = new TestXpr().Add8888("A_Picture", 64, 64, TestXpr.Pixels(64, 64, 1)).ToBytes();

        Assert.Throws<InvalidDataException>(() => XprTexture.Parse(container[..0x900])[0].Decode());
    }

    private static byte[] Pixel(RgbaImage image, int x, int y) => image.Pixels.AsSpan((y * image.Width + x) * 4, 4).ToArray();
}

public sealed class PngWriterTests
{
    [Fact]
    public void The_png_holds_the_pixels_and_passes_the_art_check()
    {
        var pixels = TestXpr.Pixels(5, 3, 9);

        var png = PngWriter.Encode(new RgbaImage(5, 3, pixels));

        var described = ArtImageFile.Describe(png);
        Assert.True(described.IsPng);
        Assert.Equal((5, 3), (described.Width, described.Height));
        Assert.Equal(pixels, Inflate(png, 5, 3));
    }

    // The rows of the one IDAT, filter bytes dropped (the writer uses filter 0 only).
    private static byte[] Inflate(byte[] png, int width, int height)
    {
        var at = 8;
        while (at < png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(at));
            if (png.AsSpan(at + 4, 4).SequenceEqual("IDAT"u8))
            {
                using var zlib = new ZLibStream(new MemoryStream(png, at + 8, length), CompressionMode.Decompress);
                var rows = new MemoryStream();
                zlib.CopyTo(rows);
                var raw = rows.ToArray();
                var pixels = new byte[width * height * 4];
                for (var y = 0; y < height; y++)
                {
                    Assert.Equal(0, raw[y * (width * 4 + 1)]);
                    raw.AsSpan(y * (width * 4 + 1) + 1, width * 4).CopyTo(pixels.AsSpan(y * width * 4));
                }
                return pixels;
            }
            at += 12 + length;
        }
        throw new InvalidDataException("no IDAT");
    }
}

public sealed class ArchiveArtTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ts4-arcart-").FullName;
    private readonly List<string> _log = [];

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Art => Path.Combine(_root, "art");

    private DiscArtResult Read(TestArc archive, params string[] baked)
    {
        var disc = new TestArtDisc();
        disc.Files["Global.arc"] = archive.ToBytes();
        foreach (var name in baked) disc.Files[name] = [0];
        return DiscArt.FromFolder(disc.WriteFolder(Path.Combine(_root, "disc")), Art, _log.Add);
    }

    private static byte[] Picture(int seed) => TestXpr.Pixels(8, 8, seed);

    [Fact]
    public void The_roster_is_the_baked_archive_names_without_the_academy_player()
    {
        Assert.Equal(["Federer", "Nadal", "WaltWilliams"],
            ArchiveArt.Roster(["Baked_Nadal1.arc", "Baked_Federer1.arc", "Baked_Federer2.arc", "Baked_WaltWilliams1.arc", "Baked_PlayerAcademy1.arc", "Global.arc"]));
    }

    [Theory]
    [InlineData("RolandGarros", "Roland Garros")]
    [InlineData("LondonO2", "London O2")]
    [InlineData("Rome", "Rome")]
    public void A_name_is_spelled_out(string name, string spelled) => Assert.Equal(spelled, ArchiveArt.Spell(name));

    [Fact]
    public void The_roster_gets_its_pictures_and_portraits_stand_in_for_missing_pictures()
    {
        var archive = TestArc.Lzx()
            .Add(new TestXpr().Add8888("Federer_Picture", 8, 8, Picture(1)).Add8888("Federer_Portrait", 8, 8, Picture(2)).ToBytes())
            .Add(new TestXpr().Add8888("Nadal_Portrait", 8, 8, Picture(3)).ToBytes())
            .Add(new TestXpr().Add8888("Coach1_Picture", 8, 8, Picture(4)).ToBytes());

        Read(archive, "Baked_Federer1.arc", "Baked_Nadal1.arc", "Baked_Murray1.arc");

        var players = DiscArtFolder.Open(Art).Entries(DiscArtCollection.Players);
        Assert.Equal(["Federer", "Nadal"], players.Select(player => player.Title));
        Assert.Equal(PngWriter.Encode(new RgbaImage(8, 8, Picture(1))), File.ReadAllBytes(players[0].ImagePath));
        Assert.Equal(PngWriter.Encode(new RgbaImage(8, 8, Picture(3))), File.ReadAllBytes(players[1].ImagePath));
        Assert.Empty(_log);
    }

    [Fact]
    public void A_venue_preview_is_cropped_to_its_photo_and_the_placeholder_is_left_out()
    {
        var archive = TestArc.Lzx()
            .Add(new TestXpr().Add8888("RolandGarros_Preview", 1024, 512, TestXpr.Pixels(1024, 512, 5)).ToBytes())
            .Add(new TestXpr().Add8888("Placeholder_Preview", 8, 8, Picture(6)).Add8888("Athens_Preview", 8, 8, Picture(7)).ToBytes());

        Read(archive);

        var venues = DiscArtFolder.Open(Art).Entries(DiscArtCollection.Venues);
        Assert.Equal(["Athens", "Roland Garros"], venues.Select(venue => venue.Title));
        var photo = ArtImageFile.Describe(File.ReadAllBytes(venues[1].ImagePath));
        Assert.Equal((764, 382), (photo.Width, photo.Height));
        Assert.True(DiscArtFolder.Open(Art).IsComplete);
    }

    [Fact]
    public void A_texture_that_does_not_decode_is_logged_and_the_others_stay()
    {
        var archive = TestArc.Lzx().Add(new TestXpr().AddOther("Rome_Preview").Add8888("Athens_Preview", 8, 8, Picture(1)).ToBytes());

        Read(archive);

        Assert.Equal(["Athens"], DiscArtFolder.Open(Art).Entries(DiscArtCollection.Venues).Select(venue => venue.Title));
        Assert.Contains(_log, line => line.Contains("Rome_Preview", StringComparison.Ordinal));
    }

    [Fact]
    public void A_damaged_archive_is_logged_and_the_rest_of_the_art_still_counts()
    {
        var disc = new TestArtDisc();
        disc.Files["Global.arc"] = "PAM-NOT"u8.ToArray();

        var result = DiscArt.FromFolder(disc.WriteFolder(Path.Combine(_root, "disc")), Art, _log.Add);

        Assert.Contains(DiscArtSlot.Background, result.Found);
        Assert.Contains(_log, line => line.Contains("Global.arc", StringComparison.Ordinal));
    }

    [Fact]
    public void A_disc_image_gives_the_same_pictures_as_the_folder()
    {
        var art = new TestArtDisc();
        art.Files["Global.arc"] = TestArc.Lzx().Add(new TestXpr().Add8888("Athens_Preview", 8, 8, Picture(1)).ToBytes()).ToBytes();
        var iso = Path.Combine(_root, "disc.iso");
        art.AddTo(new TestDisc()).WriteImage(iso);

        DiscArt.FromIso(iso, Art, _log.Add);

        Assert.Equal(["Athens"], DiscArtFolder.Open(Art).Entries(DiscArtCollection.Venues).Select(venue => venue.Title));
    }
}
