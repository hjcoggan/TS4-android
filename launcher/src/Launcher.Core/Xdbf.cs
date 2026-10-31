using System.Buffers.Binary;
using System.Text;

namespace TopSpin4.Launcher.Core;

/// <summary>One achievement as the title resource describes it.</summary>
/// <param name="Id">The achievement's id.</param>
/// <param name="ImageId">The id of its icon among the resource's images.</param>
/// <param name="Gamerscore">The points it gives.</param>
/// <param name="Name">Its name, or empty when the string table lacks it.</param>
/// <param name="Description">What earned it, or empty.</param>
/// <param name="HowTo">What to do to earn it (the text shown while it is locked), or empty.</param>
public sealed record XdbfAchievement(ushort Id, uint ImageId, ushort Gamerscore, string Name, string Description, string HowTo);

/// <summary>
/// An XDBF, the title resource inside an XEX2 Module (layout in docs/research/disc-art.md, section 3.2): a header, an
/// entry table of (section, id, offset, size), a free table, then the data the offsets count from. Section 1 holds
/// metadata records by four-character id (<c>XACH</c> is the achievement table), section 2 images by id, section 3
/// string tables by locale (1 is English). All big-endian.
/// </summary>
internal sealed class Xdbf
{
    private const ushort MetadataSection = 1;
    private const ushort ImageSection = 2;
    private const ushort StringSection = 3;
    private const ulong AchievementTable = 0x58414348; // "XACH"
    private const ulong English = 1;
    private const int AchievementSize = 0x24;

    private readonly byte[] _bytes;
    private readonly Dictionary<(ushort Section, ulong Id), (int Offset, int Size)> _entries = [];

    private Xdbf(byte[] bytes) => _bytes = bytes;

    /// <summary>Parses the XDBF in <paramref name="resource"/>. Throws <see cref="InvalidDataException"/> when it is damaged.</summary>
    public static Xdbf Parse(ReadOnlySpan<byte> resource)
    {
        if (resource.Length < 0x18 || !resource.StartsWith("XDBF"u8)) throw new InvalidDataException("The title resource is damaged.");
        var tableLength = BinaryPrimitives.ReadUInt32BigEndian(resource[8..]);
        var used = BinaryPrimitives.ReadUInt32BigEndian(resource[12..]);
        var freeLength = BinaryPrimitives.ReadUInt32BigEndian(resource[16..]);
        var dataStart = 0x18L + (long)tableLength * 18 + (long)freeLength * 8;
        if (used > tableLength || dataStart > resource.Length) throw new InvalidDataException("The title resource is damaged.");
        var xdbf = new Xdbf(resource.ToArray());
        for (var i = 0; i < used; i++)
        {
            var entry = resource.Slice(0x18 + i * 18, 18);
            long offset = dataStart + BinaryPrimitives.ReadUInt32BigEndian(entry[10..]);
            long size = BinaryPrimitives.ReadUInt32BigEndian(entry[14..]);
            if (offset + size > resource.Length) throw new InvalidDataException("An entry lies outside the title resource.");
            xdbf._entries[(BinaryPrimitives.ReadUInt16BigEndian(entry), BinaryPrimitives.ReadUInt64BigEndian(entry[2..]))] = ((int)offset, (int)size);
        }
        return xdbf;
    }

    /// <summary>The image with id <paramref name="id"/>, or null when there is none.</summary>
    public byte[]? Image(ulong id) => Entry(ImageSection, id)?.ToArray();

    /// <summary>
    /// The achievements, in table order, named from the English string table (empty names when it is missing). Empty when
    /// the resource has no achievement table. Throws <see cref="InvalidDataException"/> when the table is damaged.
    /// </summary>
    public IReadOnlyList<XdbfAchievement> Achievements()
    {
        if (Entry(MetadataSection, AchievementTable) is not { } table) return [];
        var span = table.Span;
        if (span.Length < 14 || !span.StartsWith("XACH"u8)) throw new InvalidDataException("The achievement table is damaged.");
        var count = BinaryPrimitives.ReadUInt16BigEndian(span[12..]);
        if (14L + count * AchievementSize > span.Length) throw new InvalidDataException("The achievement table is cut off.");
        var strings = Strings(English);
        string Text(ushort id) => strings.TryGetValue(id, out var text) ? text : "";
        var list = new List<XdbfAchievement>(count);
        for (var i = 0; i < count; i++)
        {
            var row = span.Slice(14 + i * AchievementSize, AchievementSize);
            list.Add(new XdbfAchievement(
                Id: BinaryPrimitives.ReadUInt16BigEndian(row),
                ImageId: BinaryPrimitives.ReadUInt32BigEndian(row[8..]),
                Gamerscore: BinaryPrimitives.ReadUInt16BigEndian(row[12..]),
                Name: Text(BinaryPrimitives.ReadUInt16BigEndian(row[2..])),
                Description: Text(BinaryPrimitives.ReadUInt16BigEndian(row[4..])),
                HowTo: Text(BinaryPrimitives.ReadUInt16BigEndian(row[6..]))));
        }
        return list;
    }

    // An XSTR string table: magic, version, size, count, then (id u16, length u16, UTF-8 bytes) per string.
    private Dictionary<ushort, string> Strings(ulong locale)
    {
        var strings = new Dictionary<ushort, string>();
        if (Entry(StringSection, locale) is not { } table) return strings;
        var span = table.Span;
        if (span.Length < 14 || !span.StartsWith("XSTR"u8)) throw new InvalidDataException("The string table is damaged.");
        var count = BinaryPrimitives.ReadUInt16BigEndian(span[12..]);
        var at = 14;
        for (var i = 0; i < count; i++)
        {
            if (at + 4 > span.Length) throw new InvalidDataException("The string table is cut off.");
            var id = BinaryPrimitives.ReadUInt16BigEndian(span[at..]);
            var length = BinaryPrimitives.ReadUInt16BigEndian(span[(at + 2)..]);
            at += 4;
            if (at + length > span.Length) throw new InvalidDataException("The string table is cut off.");
            strings[id] = Encoding.UTF8.GetString(span.Slice(at, length));
            at += length;
        }
        return strings;
    }

    private ReadOnlyMemory<byte>? Entry(ushort section, ulong id)
    {
        if (!_entries.TryGetValue((section, id), out var place)) return null;
        return new ReadOnlyMemory<byte>(_bytes, place.Offset, place.Size);
    }
}
