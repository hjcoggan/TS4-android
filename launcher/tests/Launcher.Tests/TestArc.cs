using System.Buffers.Binary;

namespace TopSpin4.Launcher.Tests;

/// <summary>
/// Synthetic PAM archives (<c>.arc</c>) and shader containers. A "compressed" entry is a real PAM-LZX entry whose
/// LZX stream holds one uncompressed block, so the frame headers and the decoder are exercised without an encoder.
/// No game data.
/// </summary>
internal sealed class TestArc
{
    private const int FrameSize = 0x8000;
    private const int EntryHeader = 0x2C;

    private readonly bool _stored;
    private readonly List<byte[]> _entries = [];

    private TestArc(bool stored) => _stored = stored;

    /// <summary>A PAM-LZX archive, like every game archive but CutScenes.arc.</summary>
    public static TestArc Lzx() => new(stored: false);

    /// <summary>A PAM-NIL archive: entries are kept as they are, like CutScenes.arc.</summary>
    public static TestArc Stored() => new(stored: true);

    /// <summary>A single-part entry (compressed in a PAM-LZX archive, as it is in a PAM-NIL one).</summary>
    public TestArc Add(byte[] data)
    {
        _entries.Add(_stored ? data : Compressed(data));
        return this;
    }

    /// <summary>An entry as the game's multi-part textures have it: two parts, a longer header, nothing a reader may decode.</summary>
    public TestArc AddMultiPart()
    {
        var entry = new byte[0x34 + 64];
        "PAM-LZX\0"u8.CopyTo(entry);
        BinaryPrimitives.WriteUInt32BigEndian(entry.AsSpan(8), 2);
        BinaryPrimitives.WriteUInt32BigEndian(entry.AsSpan(0x0C), 0x100000);
        BinaryPrimitives.WriteUInt32BigEndian(entry.AsSpan(0x10), 0x200000);
        BinaryPrimitives.WriteUInt32BigEndian(entry.AsSpan(0x24), 0x34);
        for (var i = 0x34; i < entry.Length; i++) entry[i] = 0xEE;
        _entries.Add(entry);
        return this;
    }

    /// <summary>The file bytes: header, the entry table, then the entries.</summary>
    public byte[] ToBytes()
    {
        var table = 0x24 + _entries.Count * 12;
        var file = new List<byte>();
        var head = new byte[0x24];
        BinaryPrimitives.WriteUInt32BigEndian(head, (uint)(table - 4));
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(4), 0x20);
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(8), (uint)_entries.Count);
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(12), (uint)_entries.Count);
        (_stored ? "PAM-NIL\0"u8 : "PAM-LZX\0"u8).CopyTo(head.AsSpan(0x1C));
        file.AddRange(head);
        var offset = 0u;
        for (var i = 0; i < _entries.Count; i++)
        {
            var row = new byte[12];
            BinaryPrimitives.WriteUInt32BigEndian(row, 0x1000u + (uint)i);
            BinaryPrimitives.WriteUInt32BigEndian(row.AsSpan(4), offset);
            BinaryPrimitives.WriteUInt32BigEndian(row.AsSpan(8), (uint)_entries[i].Length);
            file.AddRange(row);
            offset += (uint)_entries[i].Length;
        }
        foreach (var entry in _entries) file.AddRange(entry);
        return [.. file];
    }

    public string WriteTo(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, ToBytes());
        return path;
    }

    /// <summary>A shader container: flags, header size, microcode size, then the header and microcode filler.</summary>
    public static byte[] Container(bool vertex, int microcodeWords, byte seed = 1)
    {
        var header = 0x10;
        var data = new byte[header + microcodeWords * 4];
        data[0] = 0x10; data[1] = 0x2A; data[2] = 0x11; data[3] = (byte)(vertex ? 1 : 0);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4), (uint)header);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), (uint)(microcodeWords * 4));
        for (var i = 12; i < data.Length; i++) data[i] = (byte)(i * 7 + seed);
        return data;
    }

    // PAM-LZX entry: magic, one part, window 2^20, sizes, three words of check data, header size, a word, then the frames.
    private static byte[] Compressed(byte[] data)
    {
        var entry = new List<byte>();
        var header = new byte[EntryHeader];
        "PAM-LZX\0"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0x0C), 0x100000);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0x10), (uint)data.Length);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0x14), (uint)data.Length);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0x24), EntryHeader);
        entry.AddRange(header);
        for (var at = 0; at < data.Length; at += FrameSize)
        {
            var chunk = data.AsSpan(at, Math.Min(FrameSize, data.Length - at));
            var stream = at == 0 ? LzxStart(data.Length).Concat(chunk.ToArray()).ToArray() : chunk.ToArray();
            if (chunk.Length == FrameSize)
            {
                entry.AddRange(U16(stream.Length));
            }
            else
            {
                entry.Add(0xFF);
                entry.AddRange(U16(chunk.Length));
                entry.AddRange(U16(stream.Length));
            }
            entry.AddRange(stream);
        }
        return [.. entry];
    }

    private static byte[] U16(int value) => [(byte)(value >> 8), (byte)value];

    // LZX: intel flag 0, block type 3 (uncompressed) and a 24-bit size fill two 16-bit words (little-endian, most significant bit first),
    // then the three repeat offsets, then the raw bytes.
    private static byte[] LzxStart(int size)
    {
        var bits = (ulong)0b0011 << 24 | (uint)size;
        var word0 = (ushort)(bits >> 12);
        var word1 = (ushort)((bits & 0xFFF) << 4);
        byte[] start = [(byte)word0, (byte)(word0 >> 8), (byte)word1, (byte)(word1 >> 8)];
        byte[] repeats = [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0];
        return [.. start, .. repeats];
    }
}
