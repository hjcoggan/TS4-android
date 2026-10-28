namespace TopSpin4.Launcher.Tests;

/// <summary>
/// The disc files that hold the art, generated: the <c>nxeart</c> STFS package (a JPEG background
/// and box art), <c>Media/XUI/PressStart.xzp</c> (a PNG logo and a PNG setup background) and a
/// <c>default.xex</c> whose title resource holds a PNG title image. Drop entries from
/// <see cref="Files"/> to model a disc that lacks them.
/// </summary>
internal sealed class TestArtDisc
{
    public const string NxeArt = "nxeart";
    public const string PressStart = "Media/XUI/PressStart.xzp";
    public const string Module = "default.xex";

    public byte[] Background { get; } = TestArtImages.Jpeg(192, 108, 1);
    public byte[] BoxArt { get; } = TestArtImages.Jpeg(42, 32, 2);
    public byte[] Logo { get; } = TestArtImages.Png(56, 17, 3);
    public byte[] SetupBackground { get; } = TestArtImages.Png(64, 36, 4);
    public byte[] TitleIcon { get; } = TestArtImages.Png(64, 64, 5);

    public SortedDictionary<string, byte[]> Files { get; }

    public TestArtDisc(bool compressedEncryptedModule = true)
    {
        var nxeart = new TestStfs { ContentType = 0x00030000, DisplayName = "nxeart", Magic = "PIRS" };
        nxeart.Files.Add(("DashStyle", new byte[4]));
        nxeart.Files.Add(("nxebg.jpg", Background));
        nxeart.Files.Add(("nxeslot.jpg", BoxArt));
        Files = new SortedDictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
        {
            [NxeArt] = nxeart.Build(),
            [PressStart] = TestXuiz.Build(
                ("Scenes\\PressStartScreen.xur", TestStfs.Content(40, 6)),
                ("Bitmaps\\PressStart.PNG", SetupBackground),
                ("Bitmaps\\Logo.PNG", Logo),
                ("Bitmaps\\LoadingIcon.PNG", TestArtImages.Png(8, 8, 7))),
            [Module] = new TestXexImage { TitleImage = TitleIcon, Encrypted = compressedEncryptedModule, Compressed = compressedEncryptedModule }.Build(),
        };
    }

    /// <summary>Puts the art files into a stand-in disc image (replacing its plain default.xex), for a test that reads an <c>.iso</c>.</summary>
    public TestDisc AddTo(TestDisc disc)
    {
        foreach (var (path, data) in Files) disc.Files[path] = data;
        return disc;
    }

    /// <summary>Writes the files under <paramref name="root"/> as the prepared disc folder lays them out.</summary>
    public string WriteFolder(string root)
    {
        foreach (var (path, data) in Files)
        {
            var target = Path.Combine(root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, data);
        }
        return root;
    }
}
