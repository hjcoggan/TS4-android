using System.Buffers.Binary;
using System.Text;

namespace TopSpin4.Launcher.Core;

/// <summary>
/// The art folder the Disc art module fills (<c>userdata\art</c> beside the game exe): one image file
/// per slot, stored as it came off the disc (a PNG stays a PNG, a JPEG a JPEG), the title icon
/// again as <c>topspin4.ico</c>, and <c>index.txt</c>, which names what was found and the disc it
/// came from. The index is written after the files it names and before the files it no longer
/// names are removed, so the index never names a file that is gone (a write that stops half way
/// can leave some files already replaced under the earlier index), and a folder without an index
/// counts as empty. This type writes the folder and, opened later, says which
/// images exist.
/// </summary>
/// <remarks>
/// <c>index.txt</c> is lines of <c>key=value</c>; <c>#</c> starts a comment. Keys: <c>source</c> (the
/// <c>.iso</c> or disc folder), <c>media_id</c> and <c>title_id</c> (hex, from default.xex), <c>read</c>
/// (<c>complete</c> when the read logged no problem), one key per slot (<see cref="Stem"/>) with the image's
/// file name, and <c>ico</c> with the icon's file name.
/// </remarks>
public sealed class DiscArtFolder
{
    public const string IndexFileName = "index.txt";
    public const string IconFileName = "topspin4.ico";

    /// <summary>The name of the list file inside each collection's folder.</summary>
    public const string ListFileName = "list.txt";

    private const string IconKey = "ico";
    private const string ReadKey = "read";
    private const string ReadComplete = "complete";
    private const string FormatKey = "format";
    // Raised when the Launcher reads more from the disc than before, so an older folder counts as incomplete and is read again.
    private const string Format = "3";
    private static readonly string[] Extensions = [".png", ".jpg"];

    /// <summary>
    /// One writer to an art folder at a time: the window's background read and a disc the player has just chosen can both
    /// write it. Held while a folder is written and while a restore decides whether to read at all.
    /// </summary>
    internal static readonly object WriterLock = new();

    private readonly string _folder;
    private readonly Dictionary<DiscArtSlot, string> _files;
    private readonly string? _icon;
    private readonly bool _indexed;
    private readonly bool _readComplete;
    private readonly bool _listedFileMissing;
    private readonly Dictionary<DiscArtCollection, IReadOnlyList<DiscArtEntry>> _collections;

    private DiscArtFolder(string folder, Dictionary<DiscArtSlot, string> files, string? icon, bool indexed, bool readComplete,
        bool listedFileMissing, string stamp, Dictionary<DiscArtCollection, IReadOnlyList<DiscArtEntry>> collections)
    {
        _collections = collections;
        _folder = folder;
        _files = files;
        _icon = icon;
        _indexed = indexed;
        _readComplete = readComplete;
        _listedFileMissing = listedFileMissing;
        Stamp = stamp;
    }

    /// <summary>
    /// Says whether the folder's content changed: equal for two opens of an unchanged folder, different once another disc's
    /// art has been written, even under the same file names (it holds the index's lines and each listed file's size and
    /// write time). Empty for a folder with no index.
    /// </summary>
    public string Stamp { get; }

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

    /// <summary>The folder name of a collection inside the art folder.</summary>
    public static string Folder(DiscArtCollection collection) => collection switch
    {
        DiscArtCollection.Achievements => "achievements",
        DiscArtCollection.Players => "players",
        DiscArtCollection.Venues => "venues",
        _ => throw new ArgumentOutOfRangeException(nameof(collection)),
    };

    /// <summary>Opens the art folder as its index describes it. A missing folder or index, or an image the index names that is gone, leaves that slot empty.</summary>
    public static DiscArtFolder Open(string folder)
    {
        var files = new Dictionary<DiscArtSlot, string>();
        string? icon = null;
        var readComplete = false;
        var format = "";
        var listedMissing = false;
        var stamp = new StringBuilder();
        try
        {
            var slots = Enum.GetValues<DiscArtSlot>().ToDictionary(Stem);
            var index = Path.Combine(folder, IndexFileName);
            foreach (var line in File.ReadLines(index))
            {
                var cut = line.IndexOf('=');
                if (cut <= 0 || line.StartsWith('#')) continue;
                var key = line[..cut].Trim();
                var value = line[(cut + 1)..].Trim();
                stamp.Append(key).Append('=').Append(value).Append('\n');
                if (key == ReadKey) readComplete = value == ReadComplete;
                else if (key == FormatKey) format = value;
                else if (key == "source" || key == "media_id" || key == "title_id") continue;
                else if (key != IconKey && !slots.ContainsKey(key)) continue;
                else if (!IsPlainFileInside(folder, value)) listedMissing = true;
                else
                {
                    if (key == IconKey) icon = value;
                    else files[slots[key]] = value;
                    Stat(stamp, Path.Combine(folder, value));
                }
            }
            Stat(stamp, index);
            var collections = new Dictionary<DiscArtCollection, IReadOnlyList<DiscArtEntry>>();
            foreach (var collection in Enum.GetValues<DiscArtCollection>())
                collections[collection] = OpenCollection(Path.Combine(folder, Folder(collection)), stamp);
            return new DiscArtFolder(folder, files, icon, indexed: true, readComplete && format == Format, listedMissing, stamp.ToString(), collections);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new DiscArtFolder(folder, [], null, indexed: false, readComplete: false, listedFileMissing: false, "", []);
        }
    }

    // A collection's list: one line per picture, "file<TAB>title<TAB>detail<TAB>points". A missing list is an empty
    // collection; a line naming a file that is gone is skipped.
    private static IReadOnlyList<DiscArtEntry> OpenCollection(string folder, StringBuilder stamp)
    {
        var list = Path.Combine(folder, ListFileName);
        if (!File.Exists(list)) return [];
        var entries = new List<DiscArtEntry>();
        foreach (var line in File.ReadLines(list))
        {
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var fields = line.Split('\t');
            if (!IsPlainFileInside(folder, fields[0])) continue;
            var points = fields.Length > 3 && int.TryParse(fields[3], out var value) ? value : 0;
            entries.Add(new DiscArtEntry(Path.Combine(folder, fields[0]), fields.ElementAtOrDefault(1) ?? "", fields.ElementAtOrDefault(2) ?? "", points));
        }
        Stat(stamp, list);
        return entries;
    }

    // An index entry is a file name in the folder, never a path out of it; and the file must still be there.
    private static bool IsPlainFileInside(string folder, string name) =>
        name.Length > 0 && name == Path.GetFileName(name) && File.Exists(Path.Combine(folder, name));

    private static void Stat(StringBuilder stamp, string path)
    {
        var info = new FileInfo(path);
        stamp.Append(info.Name).Append('|').Append(info.Length).Append('|').Append(info.LastWriteTimeUtc.Ticks).Append('\n');
    }

    /// <summary>The full path of a slot's image, or null when the folder has none.</summary>
    public string? ImagePath(DiscArtSlot slot) => _files.TryGetValue(slot, out var name) ? Path.Combine(_folder, name) : null;

    /// <summary>The full path of the multi-purpose <c>.ico</c> (window, taskbar and shortcut icon), or null.</summary>
    public string? IconPath => _icon is null ? null : Path.Combine(_folder, _icon);

    /// <summary>The pictures of a collection, in the order the disc gave them; empty when the folder has none.</summary>
    public IReadOnlyList<DiscArtEntry> Entries(DiscArtCollection collection) =>
        _collections.TryGetValue(collection, out var entries) ? entries : [];

    /// <summary>
    /// An index from a read that went through without a problem exists, and every file it lists is there. A disc that
    /// genuinely lacks an image still gives a complete folder; one with a file gone, or a read that logged a problem
    /// (or none yet), is read again.
    /// </summary>
    public bool IsComplete => _indexed && _readComplete && !_listedFileMissing;

    /// <summary>
    /// Writes <paramref name="images"/> to <paramref name="folder"/> and returns the slots it holds now. The files go in first,
    /// each by writing a uniquely named temporary file beside it and renaming; then the index; then the files the new
    /// disc has no use for are removed. A write that stops half way leaves the earlier index, and every file it names still exists
    /// (some may already hold the new disc's bytes), so the folder never lists a file that is gone. One writer at a time (<see cref="WriterLock"/>).
    /// </summary>
    /// <param name="cleanRead">The read that produced <paramref name="images"/> logged no problem; only then does the index say <c>read=complete</c>.</param>
    /// <param name="collections">The pictures of each collection read; a collection left out is emptied.</param>
    internal static IReadOnlyList<DiscArtSlot> Write(string folder, IReadOnlyDictionary<DiscArtSlot, byte[]> images, string source,
        DiscIdentity? disc, bool cleanRead, Action<string> problem,
        IReadOnlyDictionary<DiscArtCollection, IReadOnlyList<DiscArtItem>>? collections = null)
    {
        lock (WriterLock)
        {
            Directory.CreateDirectory(folder);
            DeleteStrayTemporaries(folder);

            var wanted = new List<(string Name, byte[] Bytes)>();
            var index = new StringBuilder("# Top Spin 4 Recomp: art read from the player's disc. Written by the Launcher.\n");
            index.Append("source=").Append(source).Append('\n');
            if (disc is { } identity)
                index.Append("media_id=").Append(identity.MediaId.ToString("X8")).Append("\ntitle_id=").Append(identity.TitleId.ToString("X8")).Append('\n');
            var found = new List<DiscArtSlot>();
            foreach (var slot in Enum.GetValues<DiscArtSlot>())
            {
                if (!images.TryGetValue(slot, out var bytes)) continue;
                var name = Stem(slot) + ArtImageFile.Describe(bytes).Extension;
                wanted.Add((name, bytes));
                index.Append(Stem(slot)).Append('=').Append(name).Append('\n');
                found.Add(slot);
            }

            var clean = cleanRead;
            if (images.TryGetValue(DiscArtSlot.TitleIcon, out var icon))
            {
                if (Ico(icon) is { } ico)
                {
                    wanted.Add((IconFileName, ico));
                    index.Append(IconKey).Append('=').Append(IconFileName).Append('\n');
                }
                else
                {
                    clean = false;
                    problem("the title icon is not a PNG of 256 pixels or less, so no .ico was made.");
                }
            }
            index.Append(FormatKey).Append('=').Append(Format).Append('\n');
            if (clean) index.Append(ReadKey).Append('=').Append(ReadComplete).Append('\n');

            foreach (var (name, bytes) in wanted) WriteAtomically(Path.Combine(folder, name), bytes);
            foreach (var collection in Enum.GetValues<DiscArtCollection>())
                WriteCollection(Path.Combine(folder, Folder(collection)), collections?.GetValueOrDefault(collection) ?? []);
            WriteAtomically(Path.Combine(folder, IndexFileName), Encoding.UTF8.GetBytes(index.ToString()));

            var keep = wanted.Select(file => file.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var slot in Enum.GetValues<DiscArtSlot>())
                foreach (var extension in Extensions)
                    DeleteObsolete(folder, Stem(slot) + extension, keep);
            DeleteObsolete(folder, IconFileName, keep);
            return found;
        }
    }

    // The pictures first, then the list that names them, then the files the list no longer names. An empty collection
    // removes its list and pictures (the folder itself stays; Explorer may have it open).
    private static void WriteCollection(string folder, IReadOnlyList<DiscArtItem> items)
    {
        if (items.Count == 0 && !Directory.Exists(folder)) return;
        Directory.CreateDirectory(folder);
        DeleteStrayTemporaries(folder);
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new StringBuilder("# Top Spin 4 Recomp: file, title, detail, points. Written by the Launcher.\n");
        for (var i = 0; i < items.Count; i++)
        {
            var name = $"{i + 1:D3}{ArtImageFile.Describe(items[i].Image).Extension}";
            WriteAtomically(Path.Combine(folder, name), items[i].Image);
            keep.Add(name);
            list.Append(name).Append('\t').Append(Field(items[i].Title)).Append('\t').Append(Field(items[i].Detail))
                .Append('\t').Append(items[i].Points).Append('\n');
        }
        if (items.Count > 0)
        {
            keep.Add(ListFileName);
            WriteAtomically(Path.Combine(folder, ListFileName), Encoding.UTF8.GetBytes(list.ToString()));
        }
        foreach (var path in Directory.EnumerateFiles(folder))
            DeleteObsolete(folder, Path.GetFileName(path), keep);
    }

    private static string Field(string text) => text.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();

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

    // A file an earlier disc left that the new index does not name. One that cannot be removed (a window still draws it)
    // is harmless: nothing lists it, and the next write removes it.
    private static void DeleteObsolete(string folder, string name, HashSet<string> keep)
    {
        var path = Path.Combine(folder, name);
        if (keep.Contains(name) || !File.Exists(path)) return;
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }

    // Temporary files only a write that was cut short (a crash, the power) leaves; none is in use while the writer lock is held.
    private static void DeleteStrayTemporaries(string folder)
    {
        foreach (var path in Directory.EnumerateFiles(folder, "*.tmp"))
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    // A reader never sees half a file: write beside it under a name no other writer uses, then rename over it.
    private static void WriteAtomically(string path, byte[] bytes)
    {
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temp, bytes);
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(temp);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
            }
            throw;
        }
    }
}
