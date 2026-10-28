using System.Buffers.Binary;
using System.Text;

namespace TopSpin4.Launcher.Core;

/// <summary>
/// An Xbox 360 XUI package (<c>.xzp</c>, magic <c>XUIZ</c>) held in memory: a directory of named
/// entries (<c>Bitmaps\Logo.PNG</c>) and their data. No public specification exists; the layout is
/// read off the Reference build's three packages (docs/research/disc-art.md, section 3.3). All
/// numbers are big-endian: magic, version, total length, one unknown word, the directory's length,
/// the entry count, then per entry the size, the offset from the end of the directory, the name's length
/// in UTF-16 units and the name in UTF-16BE.
/// </summary>
internal sealed class XuizPackage
{
    private const int HeaderSize = 0x16;

    private readonly byte[] _bytes;
    private readonly Dictionary<string, (int Offset, int Size)> _entries;

    private XuizPackage(byte[] bytes, Dictionary<string, (int Offset, int Size)> entries)
    {
        _bytes = bytes;
        _entries = entries;
    }

    /// <summary>Reads the directory. Throws <see cref="InvalidDataException"/> unless the package is whole and every entry lies inside it.</summary>
    public static XuizPackage Parse(byte[] bytes)
    {
        if (bytes.Length < HeaderSize || !bytes.AsSpan(0, 4).SequenceEqual("XUIZ"u8))
            throw new InvalidDataException("Not an XUI package.");
        if (BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8)) != bytes.Length)
            throw new InvalidDataException("The package is cut off or damaged: its length does not match.");
        var directoryLength = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(0x10));
        var count = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(0x14));
        if (directoryLength > bytes.Length - HeaderSize) throw new InvalidDataException("The package's directory is damaged.");
        var dataStart = HeaderSize + (int)directoryLength;
        var entries = new Dictionary<string, (int, int)>(StringComparer.OrdinalIgnoreCase);
        var at = HeaderSize;
        for (var i = 0; i < count; i++)
        {
            if (at + 9 > dataStart) throw new InvalidDataException("The package's directory is damaged.");
            var size = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(at));
            var offset = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(at + 4));
            var nameLength = bytes[at + 8];
            if (nameLength == 0 || at + 9 + nameLength * 2 > dataStart) throw new InvalidDataException("The package's directory is damaged.");
            var name = Encoding.BigEndianUnicode.GetString(bytes, at + 9, nameLength * 2);
            if ((long)dataStart + offset + size > bytes.Length) throw new InvalidDataException($"The package's entry {name} lies outside it.");
            entries[name] = (dataStart + (int)offset, (int)size);
            at += 9 + nameLength * 2;
        }
        return new XuizPackage(bytes, entries);
    }

    /// <summary>The entry named <paramref name="name"/> ('\\' between folders, any case), or null.</summary>
    public byte[]? Find(string name) =>
        _entries.TryGetValue(name, out var entry) ? _bytes.AsSpan(entry.Offset, entry.Size).ToArray() : null;
}
