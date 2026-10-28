using System.Buffers.Binary;
using System.Text;

namespace TopSpin4.Launcher.Core;

/// <summary>
/// The art folder the Disc art module fills (<c>art</c> in the per-user folder): one image file
/// per slot, stored as it came off the disc (a PNG stays a PNG, a JPEG a JPEG), the title icon
/// again as <c>topspin4.ico</c>, and <c>index.txt</c>, which names what was found and the disc it
/// came from. The index is written last, so a folder without one counts as empty. This type writes
/// the folder and, opened later, says which images exist.
/// </summary>
/// <remarks>
/// <c>index.txt</c> is lines of <c>key=value</c>; <c>#</c> starts a comment. Keys: <c>source</c> (the
/// <c>.iso</c> or disc folder), <c>media_id</c> and <c>title_id</c> (hex, from default.xex), one key per slot
/// (<see cref="Stem"/>) with the image's file name, and <c>ico</c> with the icon's file name.
/// </remarks>
public sealed class DiscArtFolder
{
    public const string IndexFileName = "index.txt";
    public const string IconFileName = "topspin4.ico";

    private const string IconKey = "ico";
    private static readonly string[] Extensions = [".png", ".jpg"];

    private readonly string _folder;
    private readonly Dictionary<DiscArtSlot, string> _files;
    private readonly string? _icon;

    private DiscArtFolder(string folder, Dictionary<DiscArtSlot, string> files, string? icon, string? source)
    {
        _folder = folder;
        _files = files;
        _icon = icon;
        Source = source;
    }

    /// <summary>The disc the art came from: the <c>.iso</c> or disc folder the index names, or null.</summary>
    public string? Source { get; }

    /// <summary>The file name stem of a slot's image ("background" gives background.jpg or background.png).</summary>
    public static string Stem(DiscArtSlot slot) => slot switch
    {
        DiscArtSlot.Background => "background",
        DiscArtSlot.BoxArt => "boxart",
        DiscArtSlot.Logo => "logo",
        DiscArtSlot.TitleIcon => "icon",
        DiscArtSlot.SetupBackground => "setup-background",
        _ => throw new ArgumentOutOfRangeException(nameof(slot)),
    };

    /// <summary>Opens the art folder as its index describes it. A missing folder or index, or an image the index names that is gone, leaves that slot empty.</summary>
    public static DiscArtFolder Open(string folder)
    {
        var files = new Dictionary<DiscArtSlot, string>();
        string? source = null;
        string? icon = null;
        try
        {
            var slots = Enum.GetValues<DiscArtSlot>().ToDictionary(Stem);
            foreach (var line in File.ReadLines(Path.Combine(folder, IndexFileName)))
            {
                var cut = line.IndexOf('=');
                if (cut <= 0 || line.StartsWith('#')) continue;
                var key = line[..cut].Trim();
                var value = line[(cut + 1)..].Trim();
                if (key == "source") source = value;
                else if (!IsPlainFileInside(folder, value)) continue;
                else if (key == IconKey) icon = value;
                else if (slots.TryGetValue(key, out var slot)) files[slot] = value;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new DiscArtFolder(folder, [], null, null);
        }
        return new DiscArtFolder(folder, files, icon, source);
    }

    // An index entry is a file name in the folder, never a path out of it; and the file must still be there.
    private static bool IsPlainFileInside(string folder, string name) =>
        name.Length > 0 && name == Path.GetFileName(name) && File.Exists(Path.Combine(folder, name));

    /// <summary>The full path of a slot's image, or null when the folder has none.</summary>
    public string? ImagePath(DiscArtSlot slot) => _files.TryGetValue(slot, out var name) ? Path.Combine(_folder, name) : null;

    /// <summary>The full path of the multi-purpose <c>.ico</c> (window, taskbar and shortcut icon), or null.</summary>
    public string? IconPath => _icon is null ? null : Path.Combine(_folder, _icon);

    public bool Has(DiscArtSlot slot) => _files.ContainsKey(slot);

    /// <summary>The slots that have an image, in slot order.</summary>
    public IReadOnlyList<DiscArtSlot> Found => Enum.GetValues<DiscArtSlot>().Where(_files.ContainsKey).ToList();

    /// <summary>Every slot has an image and the <c>.ico</c> exists.</summary>
    public bool IsComplete => _icon is not null && Enum.GetValues<DiscArtSlot>().All(_files.ContainsKey);

    /// <summary>
    /// Writes <paramref name="images"/> to <paramref name="folder"/> and returns the slots it holds now.
    /// A slot not in <paramref name="images"/> loses any file an earlier disc left.
    /// </summary>
    internal static IReadOnlyList<DiscArtSlot> Write(string folder, IReadOnlyDictionary<DiscArtSlot, byte[]> images, string source,
        DiscIdentity? disc, Action<string> problem)
    {
        Directory.CreateDirectory(folder);
        var index = new StringBuilder("# Top Spin 4 Recomp: art read from the player's disc. Written by the Launcher.\n");
        index.Append("source=").Append(source).Append('\n');
        if (disc is { } identity)
            index.Append("media_id=").Append(identity.MediaId.ToString("X8")).Append("\ntitle_id=").Append(identity.TitleId.ToString("X8")).Append('\n');
        var found = new List<DiscArtSlot>();
        foreach (var slot in Enum.GetValues<DiscArtSlot>())
        {
            var stem = Stem(slot);
            foreach (var extension in Extensions) DeleteIfThere(Path.Combine(folder, stem + extension));
            if (!images.TryGetValue(slot, out var bytes)) continue;
            var name = stem + ArtImageFile.Describe(bytes).Extension;
            WriteAtomically(Path.Combine(folder, name), bytes);
            index.Append(stem).Append('=').Append(name).Append('\n');
            found.Add(slot);
        }

        DeleteIfThere(Path.Combine(folder, IconFileName));
        if (images.TryGetValue(DiscArtSlot.TitleIcon, out var icon))
        {
            if (Ico(icon) is { } ico)
            {
                WriteAtomically(Path.Combine(folder, IconFileName), ico);
                index.Append(IconKey).Append('=').Append(IconFileName).Append('\n');
            }
            else
            {
                problem("the title icon is not a PNG of 256 pixels or less, so no .ico was made.");
            }
        }
        WriteAtomically(Path.Combine(folder, IndexFileName), Encoding.UTF8.GetBytes(index.ToString()));
        return found;
    }

    // A one-image icon file: ICONDIR (reserved, type 1, count), one ICONDIRENTRY, then the PNG itself (Windows Vista and later
    // read PNG entries). The title icon is 64 pixels; Windows scales it down for the smaller sizes.
    private static byte[]? Ico(byte[] png)
    {
        var image = ArtImageFile.Describe(png);
        if (!image.IsPng || image.Width > 256 || image.Height > 256) return null;
        var ico = new byte[6 + 16 + png.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(ico.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(ico.AsSpan(4), 1);
        ico[6] = (byte)(image.Width == 256 ? 0 : image.Width);
        ico[7] = (byte)(image.Height == 256 ? 0 : image.Height);
        BinaryPrimitives.WriteUInt16LittleEndian(ico.AsSpan(10), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(ico.AsSpan(12), 32);
        BinaryPrimitives.WriteInt32LittleEndian(ico.AsSpan(14), png.Length);
        BinaryPrimitives.WriteInt32LittleEndian(ico.AsSpan(18), 22);
        png.CopyTo(ico, 22);
        return ico;
    }

    private static void DeleteIfThere(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    // A reader never sees half a file: write beside it, then rename over it.
    private static void WriteAtomically(string path, byte[] bytes)
    {
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, path, overwrite: true);
    }
}
