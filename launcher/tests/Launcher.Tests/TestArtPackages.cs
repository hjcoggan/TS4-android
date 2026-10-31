using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace TopSpin4.Launcher.Tests;

/// <summary>A generated XUIZ package (the layout in docs/research/disc-art.md, section 3.3), entries gapped like the real ones.</summary>
internal static class TestXuiz
{
    public static byte[] Build(params (string Name, byte[] Data)[] entries)
    {
        var directory = new List<byte>();
        var data = new List<byte>();
        foreach (var (name, content) in entries)
        {
            var entry = new byte[9];
            BinaryPrimitives.WriteUInt32BigEndian(entry, (uint)content.Length);
            BinaryPrimitives.WriteUInt32BigEndian(entry.AsSpan(4), (uint)data.Count);
            entry[8] = (byte)name.Length;
            directory.AddRange(entry);
            directory.AddRange(Encoding.BigEndianUnicode.GetBytes(name));
            data.AddRange(content);
            data.AddRange(new byte[0x11]); // the real packages leave gaps between entries
        }
        var package = new byte[0x16 + directory.Count + data.Count];
        "XUIZ"u8.CopyTo(package);
        BinaryPrimitives.WriteUInt32BigEndian(package.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(package.AsSpan(8), (uint)package.Length);
        BinaryPrimitives.WriteUInt32BigEndian(package.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt32BigEndian(package.AsSpan(16), (uint)directory.Count);
        BinaryPrimitives.WriteUInt16BigEndian(package.AsSpan(20), (ushort)entries.Length);
        directory.CopyTo(package, 0x16);
        data.CopyTo(package, 0x16 + directory.Count);
        return package;
    }
}

/// <summary>One achievement of a generated title resource.</summary>
internal sealed record TestAchievement(ushort Id, uint ImageId, ushort Gamerscore, string Name, string Description, string HowTo, byte[]? Icon);

/// <summary>
/// A generated XEX2 Module that carries a title resource (an XDBF with a title image) the way the
/// Reference build's default.xex does: execution info, resource info, file format info, security
/// info, then the image data, optionally LZX-compressed (stored blocks, a real LZX stream) in
/// hash-chained blocks and AES-128-CBC encrypted. No game data.
/// </summary>
internal sealed record TestXexImage
{
    public const uint LoadAddress = 0x82000000;
    private const int PeOffset = 0x1000;
    private const int Frame = 0x8000;
    private static readonly byte[] RetailKey = Convert.FromHexString("20B185A59D28FDC340583FBB0896BF91");

    public byte[]? TitleImage { get; init; }
    public bool Encrypted { get; init; }
    public bool Compressed { get; init; }
    /// <summary>The offset of the XDBF in the image; the image is padded out past it.</summary>
    public int ResourceOffset { get; init; } = 0x8800;
    public int ImageSize { get; init; } = 0xB000;
    public bool IncludeResourceInfo { get; init; } = true;
    public int Seed { get; init; } = 1;
    /// <summary>The image key is protected with the all-zero development key instead of the retail key.</summary>
    public bool DevkitKey { get; init; }

    public byte[] Build()
    {
        var image = new byte[ImageSize];
        for (var i = 0; i < image.Length; i++) image[i] = (byte)(i * 13 + Seed);
        var resource = Xdbf();
        resource.CopyTo(image, ResourceOffset);

        var key = new byte[16];
        RandomNumberGenerator.Fill(key);
        var firstSize = 0;
        var firstHash = new byte[20];
        var body = Compressed ? Blocks(image, out firstSize, out firstHash) : Pad(image);
        if (Encrypted)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            body = aes.EncryptCbc(body, new byte[16], PaddingMode.None);
        }

        var xex = new byte[PeOffset + body.Length];
        "XEX2"u8.CopyTo(xex);
        BinaryPrimitives.WriteUInt32BigEndian(xex.AsSpan(8), PeOffset);
        BinaryPrimitives.WriteUInt32BigEndian(xex.AsSpan(0x10), 0x180);
        var headers = new List<(uint Key, uint Value)> { (0x00040006, 0x100), (0x000003FF, 0x140) };
        if (IncludeResourceInfo) headers.Add((0x000002FF, 0x120));
        BinaryPrimitives.WriteUInt32BigEndian(xex.AsSpan(0x14), (uint)headers.Count);
        for (var i = 0; i < headers.Count; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(xex.AsSpan(0x18 + i * 8), headers[i].Key);
            BinaryPrimitives.WriteUInt32BigEndian(xex.AsSpan(0x1C + i * 8), headers[i].Value);
        }

        // Execution info: media ID, version, base version, title ID (as TestXex.Module writes them).
        BinaryPrimitives.WriteUInt32BigEndian(xex.AsSpan(0x100), TestXex.MediaId);
        BinaryPrimitives.WriteUInt32BigEndian(xex.AsSpan(0x104), TestXex.DiscVersion);
        BinaryPrimitives.WriteUInt32BigEndian(xex.AsSpan(0x108), TestXex.DiscVersion);
        BinaryPrimitives.WriteUInt32BigEndian(xex.AsSpan(0x10C), TestXex.TitleId);

        // Resource info: total size, then name[8], address, size per resource.
        BinaryPrimitives.WriteUInt32BigEndian(xex.AsSpan(0x120), 4 + 16);
        Encoding.ASCII.GetBytes(TestXex.TitleId.ToString("X8")).CopyTo(xex, 0x124);
        BinaryPrimitives.WriteUInt32BigEndian(xex.AsSpan(0x12C), LoadAddress + (uint)ResourceOffset);
        BinaryPrimitives.WriteUInt32BigEndian(xex.AsSpan(0x130), (uint)resource.Length);

        // File format info: size, encryption, compression, then for normal compression the window and the first block.
        BinaryPrimitives.WriteUInt32BigEndian(xex.AsSpan(0x140), Compressed ? 0x30u : 8u);
        BinaryPrimitives.WriteUInt16BigEndian(xex.AsSpan(0x144), (ushort)(Encrypted ? 1 : 0));
        BinaryPrimitives.WriteUInt16BigEndian(xex.AsSpan(0x146), (ushort)(Compressed ? 2 : 0));
        if (Compressed)
        {
            BinaryPrimitives.WriteUInt32BigEndian(xex.AsSpan(0x148), Frame);
            BinaryPrimitives.WriteUInt32BigEndian(xex.AsSpan(0x14C), (uint)firstSize);
            firstHash.CopyTo(xex, 0x150);
        }

        // Security info: image size, load address, the image key encrypted with the retail key.
        BinaryPrimitives.WriteUInt32BigEndian(xex.AsSpan(0x184), (uint)ImageSize);
        BinaryPrimitives.WriteUInt32BigEndian(xex.AsSpan(0x180 + 0x110), LoadAddress);
        using (var retail = Aes.Create())
        {
            retail.Key = DevkitKey ? new byte[16] : RetailKey;
            retail.EncryptCbc(key, new byte[16], PaddingMode.None).CopyTo(xex, 0x180 + 0x150);
        }
        body.CopyTo(xex, PeOffset);
        return xex;
    }

    /// <summary>The achievement table (XACH) and its English strings (XSTR); each icon is image <c>ImageId</c> unless null.</summary>
    public IReadOnlyList<TestAchievement> Achievements { get; init; } = [];
    /// <summary>Leave out the English string table, so the achievements have no names.</summary>
    public bool OmitStrings { get; init; }
    /// <summary>Replaces the achievement table's bytes (a damaged table).</summary>
    public byte[]? RawAchievementTable { get; init; }

    // An XDBF: header, an image that is no achievement's icon (id 0x7F), the title image (id 0x8000), and the achievements.
    private byte[] Xdbf()
    {
        var other = TestArtImages.Png(4, 4, 99);
        var title = TitleImage ?? [];
        var entries = new List<(ushort Section, ulong Id, byte[] Data)> { (2, 0x7F, other) };
        if (TitleImage is not null) entries.Add((2, 0x8000, title));
        if (Achievements.Count > 0 || RawAchievementTable is not null)
        {
            entries.Add((1, 0x58414348, RawAchievementTable ?? AchievementTable()));
            if (!OmitStrings) entries.Add((3, 1, StringTable()));
            foreach (var achievement in Achievements)
                if (achievement.Icon is not null) entries.Add((2, achievement.ImageId, achievement.Icon));
        }
        var tableLength = entries.Count + 2;
        const int freeLength = 2;
        var dataStart = 0x18 + tableLength * 18 + freeLength * 8;
        var xdbf = new byte[dataStart + entries.Sum(entry => entry.Data.Length)];
        "XDBF"u8.CopyTo(xdbf);
        BinaryPrimitives.WriteUInt32BigEndian(xdbf.AsSpan(4), 0x10000);
        BinaryPrimitives.WriteUInt32BigEndian(xdbf.AsSpan(8), (uint)tableLength);
        BinaryPrimitives.WriteUInt32BigEndian(xdbf.AsSpan(12), (uint)entries.Count);
        BinaryPrimitives.WriteUInt32BigEndian(xdbf.AsSpan(16), freeLength);
        BinaryPrimitives.WriteUInt32BigEndian(xdbf.AsSpan(20), 1);
        var offset = 0;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = xdbf.AsSpan(0x18 + i * 18, 18);
            BinaryPrimitives.WriteUInt16BigEndian(entry, entries[i].Section);
            BinaryPrimitives.WriteUInt64BigEndian(entry[2..], entries[i].Id);
            BinaryPrimitives.WriteUInt32BigEndian(entry[10..], (uint)offset);
            BinaryPrimitives.WriteUInt32BigEndian(entry[14..], (uint)entries[i].Data.Length);
            entries[i].Data.CopyTo(xdbf, dataStart + offset);
            offset += entries[i].Data.Length;
        }
        return xdbf;
    }

    // XACH: magic, version, size, count u16, then 0x24 bytes per achievement (id, name, description and how-to string
    // ids, image id u32, gamerscore u16, then flags and reserved bytes). String ids: 100 + 3i, 101 + 3i, 102 + 3i.
    private byte[] AchievementTable()
    {
        var table = new byte[14 + Achievements.Count * 0x24];
        "XACH"u8.CopyTo(table);
        BinaryPrimitives.WriteUInt32BigEndian(table.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(table.AsSpan(8), (uint)table.Length - 8);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(12), (ushort)Achievements.Count);
        for (var i = 0; i < Achievements.Count; i++)
        {
            var row = table.AsSpan(14 + i * 0x24, 0x24);
            BinaryPrimitives.WriteUInt16BigEndian(row, Achievements[i].Id);
            BinaryPrimitives.WriteUInt16BigEndian(row[2..], (ushort)(100 + 3 * i));
            BinaryPrimitives.WriteUInt16BigEndian(row[4..], (ushort)(101 + 3 * i));
            BinaryPrimitives.WriteUInt16BigEndian(row[6..], (ushort)(102 + 3 * i));
            BinaryPrimitives.WriteUInt32BigEndian(row[8..], Achievements[i].ImageId);
            BinaryPrimitives.WriteUInt16BigEndian(row[12..], Achievements[i].Gamerscore);
            BinaryPrimitives.WriteUInt32BigEndian(row[16..], 9);
        }
        return table;
    }

    // XSTR: magic, version, size, count u16, then (id u16, length u16, UTF-8) per string.
    private byte[] StringTable()
    {
        var strings = new List<(ushort Id, byte[] Text)> { (0x8000, Encoding.UTF8.GetBytes("Test Title")) };
        for (var i = 0; i < Achievements.Count; i++)
        {
            strings.Add(((ushort)(100 + 3 * i), Encoding.UTF8.GetBytes(Achievements[i].Name)));
            strings.Add(((ushort)(101 + 3 * i), Encoding.UTF8.GetBytes(Achievements[i].Description)));
            strings.Add(((ushort)(102 + 3 * i), Encoding.UTF8.GetBytes(Achievements[i].HowTo)));
        }
        var table = new List<byte>(new byte[14]);
        foreach (var (id, text) in strings)
        {
            var head = new byte[4];
            BinaryPrimitives.WriteUInt16BigEndian(head, id);
            BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(2), (ushort)text.Length);
            table.AddRange(head);
            table.AddRange(text);
        }
        var bytes = table.ToArray();
        "XSTR"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), (uint)bytes.Length - 8);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(12), (ushort)strings.Count);
        return bytes;
    }

    private static byte[] Pad(byte[] data)
    {
        var padded = new byte[(data.Length + 15) & ~15];
        data.CopyTo(padded, 0);
        return padded;
    }

    // Hash-chained blocks, one 32 KiB frame each: [next size u32][next SHA-1][chunk length u16, LZX bytes]..., then a zero length.
    private static byte[] Blocks(byte[] image, out int firstSize, out byte[] firstHash)
    {
        var frames = new List<byte[]>();
        for (var at = 0; at < image.Length; at += Frame)
            frames.Add(StoredLzxFrame(image.AsSpan(at, Math.Min(Frame, image.Length - at)).ToArray(), first: at == 0));
        var blocks = new byte[frames.Count][];
        for (var i = frames.Count - 1; i >= 0; i--)
        {
            var block = new byte[24 + 2 + frames[i].Length + 2];
            if (i + 1 < frames.Count)
            {
                BinaryPrimitives.WriteUInt32BigEndian(block, (uint)blocks[i + 1].Length);
                SHA1.HashData(blocks[i + 1]).CopyTo(block, 4);
            }
            BinaryPrimitives.WriteUInt16BigEndian(block.AsSpan(24), (ushort)frames[i].Length);
            frames[i].CopyTo(block, 26);
            blocks[i] = block;
        }
        firstSize = blocks[0].Length;
        firstHash = SHA1.HashData(blocks[0]);
        return Pad([.. blocks.SelectMany(block => block)]);
    }

    // One LZX frame holding the data as an uncompressed block (type 3): 16-bit little-endian words,
    // the first frame starting with the one header bit, then 3 bits of type and 24 of length, the
    // three repeat offsets, and the bytes. Even lengths keep the frames free of padding bytes.
    private static byte[] StoredLzxFrame(byte[] data, bool first)
    {
        if (data.Length % 2 != 0) throw new ArgumentException("Even frame sizes only.");
        var bits = new List<bool>();
        void Put(uint value, int count) { for (var i = count - 1; i >= 0; i--) bits.Add((value >> i & 1) != 0); }
        if (first) Put(0, 1); // no Intel E8 translation
        Put(3, 3);
        Put((uint)data.Length, 24);
        while (bits.Count % 16 != 0) bits.Add(false);
        var frame = new List<byte>();
        for (var i = 0; i < bits.Count; i += 16)
        {
            var word = 0;
            for (var b = 0; b < 16; b++) word = word << 1 | (bits[i + b] ? 1 : 0);
            frame.Add((byte)word);
            frame.Add((byte)(word >> 8));
        }
        for (var r = 0; r < 3; r++) frame.AddRange([1, 0, 0, 0]);
        frame.AddRange(data);
        return [.. frame];
    }
}
