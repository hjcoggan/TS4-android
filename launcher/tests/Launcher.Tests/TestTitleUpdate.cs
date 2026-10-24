namespace TopSpin4.Launcher.Tests;

/// <summary>
/// A stand-in TU1: a LIVE title update package for the Reference build's title and media ID
/// with three module patches and two data files, and the matching TU1 manifest.
/// </summary>
internal sealed class TestTitleUpdate
{
    public const string ManifestVersion = "test-tu1-v1";

    public static readonly (string Path, byte[] Data)[] Tu1Files =
    [
        ("default.xexp", TestXex.Patch(seed: 11)),
        ("Loader_DLL.xexp", TestXex.Patch(seed: 12)),
        ("Swing_DLL.xexp", TestXex.Patch(seed: 13)),
        ("Boot.bin", TestStfs.Content(64, 14)),
        ("TitleUpdate.arc", TestStfs.Content(3 * 0x1000 + 5, 15)),
    ];

    public static string ManifestText() =>
        TestManifest.Text(ManifestVersion, Tu1Files.Select(file => (file.Path, file.Data.Length, file.Data)));

    public static TestStfs Package(uint titleId = TestXex.TitleId, uint mediaId = TestXex.MediaId,
        uint contentType = 0x000B0000, params (string Path, byte[] Data)[] files)
    {
        var package = new TestStfs
        {
            ContentType = contentType, TitleId = titleId, MediaId = mediaId, Version = 3, BaseVersion = 3,
            DisplayName = "Top Spin 4 Title Update #1",
        };
        package.Files.AddRange(files.Length > 0 ? files : Tu1Files);
        return package;
    }

    /// <summary>TU1's files with one replaced (or added, when the path is new).</summary>
    public static (string Path, byte[] Data)[] With(string path, byte[] data) =>
        Tu1Files.Any(file => file.Path == path)
            ? Tu1Files.Select(file => file.Path == path ? (path, data) : file).ToArray()
            : [.. Tu1Files, (path, data)];
}
