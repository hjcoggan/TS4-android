using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

public class FileManifestTests
{
    [Fact]
    public void The_embedded_disc_manifest_is_the_reference_builds_disc()
    {
        var manifest = FileManifest.Disc;

        Assert.Equal("54540859-60819732-v0.0.0.3", manifest.Version);
        Assert.Equal(230, manifest.Files.Count);
        Assert.Equal(5_737_767_977, manifest.TotalBytes);
        // The same Modules scripts/build.ps1 hashes for codegen.
        Assert.Equal("2d72e84feab9a84ac0e1239785a9af1ed7b6e693cf39041f206a92ac9454ce28", manifest.Find("default.xex")?.Sha256);
        Assert.Equal("8f9ed530c9cf32cf2ae33534ba1ca99e3abdcf2f5859bc8f68658e23187fd092", manifest.Find("Loader_DLL.xex")?.Sha256);
        Assert.Equal("dfa71292f75a649773c280de7bc2688e6e47e473aba397c248fb4204c45fac44", manifest.Find("Swing_DLL.xex")?.Sha256);
    }

    [Fact]
    public void The_embedded_tu1_manifest_is_title_update_1()
    {
        var manifest = FileManifest.TitleUpdate;

        Assert.Equal("54540859-60819732-tu1-v0.0.1.3", manifest.Version);
        Assert.Equal(["Boot.bin", "CoreTech_Xenon.bin", "default.xexp", "Layoutall.bin", "Loader_DLL.xexp", "Swing_DLL.xexp", "TitleUpdate.arc"],
            manifest.Files.Select(file => file.Path));
        Assert.Equal("1ba6119f45561ca5b916e3d13df81b6318b3a9ac89e8fd3dc8bf292d30bd3fc7", manifest.Find("default.xexp")?.Sha256);
        Assert.Equal("9666df4d181db804eb9b59994274a7075475bb5c433e7cb12d116b5fd310f657", manifest.Find("Loader_DLL.xexp")?.Sha256);
        Assert.Equal("d5837ef967415ecd77547f3434cf8973f025be8cb45e398ff7eecfd68a0726ed", manifest.Find("Swing_DLL.xexp")?.Sha256);
    }

    [Fact]
    public void Paths_are_found_regardless_of_case()
    {
        Assert.Equal(638_976, FileManifest.Disc.Find("DEFAULT.XEX")?.Size);
        Assert.Null(FileManifest.Disc.Find("missing.bin"));
    }

    [Theory]
    [InlineData("abc\t1\tx.bin\n")]
    [InlineData("manifest_version\tv1\nnot-a-hash\t1\tdefault.xex\n")]
    [InlineData("# only a comment\n")]
    public void A_malformed_manifest_is_rejected(string text)
    {
        Assert.Throws<InvalidDataException>(() => FileManifest.Parse(text));
    }
}
