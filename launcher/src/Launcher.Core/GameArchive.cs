using System.Buffers.Binary;
using Microsoft.Xna.Framework.Content;

namespace TopSpin4.Launcher.Core;

/// <summary>
/// One of the game's <c>.arc</c> archives (docs/research/disc-art.md, section 6.2). An archive is a 0x24-byte header
/// (data base at word 0 + 4, entry count at word 2, <c>PAM-LZX</c> or <c>PAM-NIL</c> at 0x1C), then
/// <c>{name hash, offset from the data base, length}</c> per entry. A PAM-LZX entry has its own 0x2C-byte header
/// (<c>PAM-LZX</c>, part count, window, uncompressed size, ..., header size at 0x24) and then frames:
/// <c>FF u16 uncompressed u16 compressed</c>, or <c>u16 compressed</c> for a whole 0x8000-byte frame, each an LZX stream
/// continuing the entry's window. A PAM-NIL entry (CutScenes.arc) is stored as it is. An entry with more than one part
/// (over 1 MB) is not read: neither the shaders nor the Launcher's pictures need one.
/// </summary>
internal sealed class GameArchive
{
    private const int TableStart = 0x24;
    private const int EntryRow = 12;
    private const int EntryHeader = 0x2C;
    private const int FrameSize = 0x8000;
    private static readonly byte[] LzxMagic = "PAM-LZX\0"u8.ToArray();
    private static readonly byte[] StoredMagic = "PAM-NIL\0"u8.ToArray();

    /// <summary>Where one entry lies in the archive.</summary>
    public readonly record struct Entry(long Offset, long Length);

    private GameArchive(List<Entry> entries) => Entries = entries;

    public IReadOnlyList<Entry> Entries { get; }

    /// <summary>Reads the entry table. Throws <see cref="InvalidDataException"/> for a file that is no archive or is damaged.</summary>
    public static GameArchive Open(Stream stream)
    {
        var head = new byte[TableStart];
        stream.Position = 0;
        if (stream.Read(head) < head.Length) throw new InvalidDataException("the file is cut short");
        var kind = head.AsSpan(0x1C, 8);
        if (!kind.SequenceEqual(StoredMagic) && !kind.SequenceEqual(LzxMagic)) throw new InvalidDataException("it is not a game archive");
        var dataBase = (long)BinaryPrimitives.ReadUInt32BigEndian(head) + 4;
        var count = BinaryPrimitives.ReadUInt32BigEndian(head.AsSpan(8));
        if (TableStart + (long)count * EntryRow > stream.Length || dataBase > stream.Length)
            throw new InvalidDataException("its entry table lies outside the file");
        var table = new byte[count * EntryRow];
        stream.ReadExactly(table);
        var entries = new List<Entry>((int)count);
        for (var i = 0; i < count; i++)
        {
            var row = table.AsSpan(i * EntryRow);
            var offset = dataBase + BinaryPrimitives.ReadUInt32BigEndian(row[4..]);
            var length = BinaryPrimitives.ReadUInt32BigEndian(row[8..]);
            if (offset + length > stream.Length) throw new InvalidDataException($"entry {i + 1} lies outside the file");
            entries.Add(new Entry(offset, length));
        }
        return new GameArchive(entries);
    }

    /// <summary>
    /// The decoded bytes of <paramref name="entry"/>, or null when it is larger than <paramref name="maxSize"/>, has more than
    /// one part, or <paramref name="wanted"/> turns it down. <paramref name="wanted"/> sees the start of the data (the first
    /// frame, or the first 0x8000 bytes of a stored entry) before the rest is read, so an unwanted entry costs one frame.
    /// Throws <see cref="InvalidDataException"/> (or the LZX decoder's exceptions) for a damaged entry.
    /// </summary>
    public static byte[]? Read(Stream stream, Entry entry, Func<ReadOnlySpan<byte>, bool> wanted, long maxSize)
    {
        if (entry.Length < 12) return null;
        stream.Position = entry.Offset;
        var head = new byte[Math.Min(entry.Length, EntryHeader)];
        stream.ReadExactly(head);
        if (!head.AsSpan().StartsWith(LzxMagic))
        {
            // Stored: the entry is the data.
            if (entry.Length > maxSize) return null;
            var start = new byte[Math.Min(entry.Length, FrameSize)];
            head.CopyTo(start, 0);
            stream.ReadExactly(start.AsSpan(head.Length));
            if (!wanted(start)) return null;
            var stored = new byte[entry.Length];
            start.CopyTo(stored, 0);
            stream.ReadExactly(stored.AsSpan(start.Length));
            return stored;
        }

        if (head.Length < EntryHeader) throw new InvalidDataException("its header is cut short");
        var parts = BinaryPrimitives.ReadUInt32BigEndian(head.AsSpan(8));
        var headerSize = BinaryPrimitives.ReadUInt32BigEndian(head.AsSpan(0x24));
        if (parts != 1 || headerSize != EntryHeader) return null;
        var size = BinaryPrimitives.ReadUInt32BigEndian(head.AsSpan(0x10));
        if (size < 12 || size > maxSize) return null;
        var window = BinaryPrimitives.ReadUInt32BigEndian(head.AsSpan(0x0C));
        if (window < 1 << 15 || window > 1 << 21 || !System.Numerics.BitOperations.IsPow2(window))
            throw new InvalidDataException("its window size is not one the game uses");

        var body = new byte[entry.Length - EntryHeader];
        stream.ReadExactly(body);
        return Decode(body, (int)size, System.Numerics.BitOperations.Log2(window), wanted);
    }

    // Decodes the frames of an entry; null as soon as the first frame shows the entry is not wanted.
    private static byte[]? Decode(byte[] body, int size, int windowBits, Func<ReadOnlySpan<byte>, bool> wanted)
    {
        var lzx = new LzxDecoder(windowBits);
        var output = new MemoryStream(size);
        var at = 0;
        var first = true;
        while (output.Length < size)
        {
            if (at + 2 > body.Length) throw new InvalidDataException("its compressed data is cut short");
            int frame, chunk;
            if (body[at] == 0xFF)
            {
                if (at + 5 > body.Length) throw new InvalidDataException("its compressed data is cut short");
                frame = BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(at + 1));
                chunk = BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(at + 3));
                at += 5;
            }
            else
            {
                frame = FrameSize;
                chunk = BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(at));
                at += 2;
            }
            frame = (int)Math.Min(frame, size - output.Length);
            if (frame <= 0 || chunk == 0 || at + chunk > body.Length) throw new InvalidDataException("a frame lies outside the entry");
            using var input = new MemoryStream(body, at, chunk, writable: false);
            if (lzx.Decompress(input, chunk, output, frame) != 0) throw new InvalidDataException("its compressed data does not decode");
            at += chunk;
            if (first)
            {
                first = false;
                if (!wanted(output.GetBuffer().AsSpan(0, (int)output.Length))) return null;
            }
        }
        return output.ToArray();
    }

    /// <summary>The exceptions a damaged entry can raise while it is read.</summary>
    public static bool IsDamage(Exception error) =>
        error is InvalidDataException or EndOfStreamException or IndexOutOfRangeException or ArgumentException or OverflowException
            or UnsupportedWindowSizeRange;
}
