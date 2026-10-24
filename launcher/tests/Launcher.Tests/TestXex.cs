using System.Buffers.Binary;

namespace TopSpin4.Launcher.Tests;

/// <summary>
/// Synthetic XEX2 headers: a stand-in Module carrying only the execution info, and a stand-in
/// module patch (.xexp) carrying only the delta patch descriptor. No game data.
/// </summary>
internal static class TestXex
{
    public const uint TitleId = 0x54540859;
    public const uint MediaId = 0x60819732;
    /// <summary>v0.0.0.3, the disc's Modules.</summary>
    public const uint DiscVersion = 0x00000003;
    /// <summary>v0.0.1.3, the Modules after TU1.</summary>
    public const uint Tu1Version = 0x00000103;

    private const int HeaderSize = 0x400;
    private const int Field = 0x100;

    public static byte[] Module(uint titleId = TitleId, uint mediaId = MediaId, uint version = DiscVersion, int seed = 1)
    {
        var data = Header(0x00040006, seed, 0x3000);
        var info = data.AsSpan(Field);
        BinaryPrimitives.WriteUInt32BigEndian(info, mediaId);
        BinaryPrimitives.WriteUInt32BigEndian(info[4..], version);
        BinaryPrimitives.WriteUInt32BigEndian(info[8..], version);
        BinaryPrimitives.WriteUInt32BigEndian(info[12..], titleId);
        return data;
    }

    public static byte[] Patch(uint target = Tu1Version, uint source = DiscVersion, int seed = 1)
    {
        var data = Header(0x000005FF, seed, 0x1800);
        var descriptor = data.AsSpan(Field);
        BinaryPrimitives.WriteUInt32BigEndian(descriptor, 0x70);
        BinaryPrimitives.WriteUInt32BigEndian(descriptor[4..], target);
        BinaryPrimitives.WriteUInt32BigEndian(descriptor[8..], source);
        return data;
    }

    // "XEX2", the PE data offset, one optional header pointing at the field, then filler.
    private static byte[] Header(uint key, int seed, int size)
    {
        var data = new byte[size];
        for (var i = HeaderSize; i < size; i++) data[i] = (byte)(i * 13 + seed);
        "XEX2"u8.CopyTo(data);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), HeaderSize);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(20), 1);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(24), key);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(28), Field);
        return data;
    }
}
