using System.Buffers.Binary;
using System.Text;

namespace TopSpin4.Launcher.Core;

/// <summary>A decoded picture: <see cref="Width"/> x <see cref="Height"/> texels, 4 bytes each, R, G, B, A.</summary>
internal sealed record RgbaImage(int Width, int Height, byte[] Pixels)
{
    /// <summary>The rectangle at (<paramref name="x"/>, <paramref name="y"/>) of <paramref name="width"/> x <paramref name="height"/>.</summary>
    public RgbaImage Crop(int x, int y, int width, int height)
    {
        if (x < 0 || y < 0 || width <= 0 || height <= 0 || x + width > Width || y + height > Height)
            throw new ArgumentOutOfRangeException(nameof(width), "The crop lies outside the picture.");
        var pixels = new byte[width * height * 4];
        for (var row = 0; row < height; row++)
            Pixels.AsSpan(((y + row) * Width + x) * 4, width * 4).CopyTo(pixels.AsSpan(row * width * 4));
        return new RgbaImage(width, height, pixels);
    }
}

/// <summary>
/// The 2D textures of an XPR2 container from the game's archives, decoded to RGBA (layout and recipe in
/// docs/research/disc-art.md, sections 6.3 and 6.4): the container lists named resources, each a D3D texture header with
/// the GPU fetch constant at +0x1C; level 0 is untiled with the Xenos 2D tiling (<see cref="TiledOffset"/>, ported from
/// Xenia's <c>GetTiledOffset2D</c> as <c>src/native/texture_layout.cpp</c> uses it), byte-swapped, then converted from
/// 8888 or DXT1/3/5. Other formats are not decoded. All integers big-endian.
/// </summary>
internal sealed class XprTexture
{
    private const int Format8888 = 6;
    private const int FormatDxt1 = 18;
    private const int FormatDxt3 = 19;
    private const int FormatDxt5 = 20;
    private const int MaxSide = 4096;

    private readonly byte[] _container;
    private readonly int _dataStart;
    private readonly uint[] _fetch;

    private XprTexture(string name, byte[] container, int dataStart, uint[] fetch)
    {
        Name = name;
        _container = container;
        _dataStart = dataStart;
        _fetch = fetch;
    }

    public string Name { get; }

    public int Width => (int)(_fetch[2] & 0x1FFF) + 1;

    public int Height => (int)((_fetch[2] >> 13) & 0x1FFF) + 1;

    private int Format => (int)(_fetch[1] & 0x3F);

    /// <summary>The container starts with the XPR2 magic.</summary>
    public static bool IsContainer(ReadOnlySpan<byte> bytes) => bytes.StartsWith("XPR2"u8);

    /// <summary>
    /// The names of the 2D textures in a container, read from its header alone (which the first frame of an archive entry
    /// holds), so a caller can decide whether to read the rest. Empty for anything that is no XPR2 or is cut short.
    /// </summary>
    public static IReadOnlyList<string> Names(ReadOnlySpan<byte> start)
    {
        var names = new List<string>();
        if (start.Length < 16 || !IsContainer(start)) return names;
        var count = BinaryPrimitives.ReadUInt32BigEndian(start[12..]);
        for (var i = 0; i < count && 16 + (i + 1) * 16 <= start.Length; i++)
        {
            var row = start.Slice(16 + i * 16, 16);
            if (!row.StartsWith("TX2D"u8)) continue;
            if (NameAt(start, BinaryPrimitives.ReadUInt32BigEndian(row[12..])) is { } name) names.Add(name);
        }
        return names;
    }

    private static string? NameAt(ReadOnlySpan<byte> container, uint offset)
    {
        var at = 12L + offset;
        if (at >= container.Length) return null;
        var end = container[(int)at..].IndexOf((byte)0);
        return end < 0 ? null : Encoding.Latin1.GetString(container.Slice((int)at, end));
    }

    /// <summary>The 2D textures of a whole container. Throws <see cref="InvalidDataException"/> when it is damaged.</summary>
    public static IReadOnlyList<XprTexture> Parse(byte[] container)
    {
        if (container.Length < 16 || !IsContainer(container)) throw new InvalidDataException("It is not an XPR2 container.");
        var headerSize = BinaryPrimitives.ReadUInt32BigEndian(container.AsSpan(4));
        var count = BinaryPrimitives.ReadUInt32BigEndian(container.AsSpan(12));
        if (12L + headerSize > container.Length || 16L + count * 16 > container.Length) throw new InvalidDataException("The XPR2 header is damaged.");
        var textures = new List<XprTexture>();
        for (var i = 0; i < count; i++)
        {
            var row = container.AsSpan(16 + i * 16, 16);
            if (!row.StartsWith("TX2D"u8)) continue;
            var resource = 12L + BinaryPrimitives.ReadUInt32BigEndian(row[4..]);
            if (resource + 0x1C + 24 > container.Length) throw new InvalidDataException("A texture header lies outside the container.");
            var fetch = new uint[6];
            for (var d = 0; d < 6; d++) fetch[d] = BinaryPrimitives.ReadUInt32BigEndian(container.AsSpan((int)resource + 0x1C + d * 4));
            var name = NameAt(container, BinaryPrimitives.ReadUInt32BigEndian(row[12..])) ?? throw new InvalidDataException("A texture name is damaged.");
            textures.Add(new XprTexture(name, container, 12 + (int)headerSize, fetch));
        }
        return textures;
    }

    /// <summary>
    /// Level 0 as RGBA. Throws <see cref="NotSupportedException"/> for a format this does not decode and
    /// <see cref="InvalidDataException"/> when the texture's data lies outside the container.
    /// </summary>
    public RgbaImage Decode()
    {
        var (blockSide, blockLog2) = Format switch
        {
            Format8888 => (1, 2),
            FormatDxt1 => (4, 3),
            FormatDxt3 or FormatDxt5 => (4, 4),
            _ => throw new NotSupportedException($"{Name} is in texture format {Format}, which is not decoded."),
        };
        var width = Width;
        var height = Height;
        if (width > MaxSide || height > MaxSide) throw new InvalidDataException($"{Name} is too large.");
        var tiled = (_fetch[0] >> 31) != 0;
        var pitch = (int)((_fetch[0] >> 22) & 0x1FF) << 5;
        var endian = (int)(_fetch[1] >> 6) & 3;
        var start = (long)_dataStart + (_fetch[1] >> 12 << 12);
        var blockBytes = 1 << blockLog2;
        var bx = (width + blockSide - 1) / blockSide;
        var by = (height + blockSide - 1) / blockSide;
        var pitchBlocks = Math.Max(pitch / blockSide, bx);

        var blocks = new byte[bx * by * blockBytes];
        var linearRow = ((pitchBlocks << blockLog2) + 255) & ~255;
        for (var y = 0; y < by; y++)
        {
            for (var x = 0; x < bx; x++)
            {
                var offset = start + (tiled ? TiledOffset(x, y, pitchBlocks, blockLog2) : (long)y * linearRow + x * blockBytes);
                if (offset < 0 || offset + blockBytes > _container.Length) throw new InvalidDataException($"{Name}'s data lies outside the container.");
                _container.AsSpan((int)offset, blockBytes).CopyTo(blocks.AsSpan((y * bx + x) * blockBytes));
            }
        }
        Swap(blocks, endian);

        var image = Format == Format8888 ? FromBgra(blocks, bx, by) : BlockCompression.Decode(blocks, bx, by, Format switch
        {
            FormatDxt1 => BlockCompression.Kind.Bc1,
            FormatDxt3 => BlockCompression.Kind.Bc2,
            _ => BlockCompression.Kind.Bc3,
        });
        return image.Width == width && image.Height == height ? image : image.Crop(0, 0, width, height);
    }

    /// <summary>The Xenos 2D tiled offset of block (<paramref name="x"/>, <paramref name="y"/>); Xenia's <c>GetTiledOffset2D</c>.</summary>
    internal static int TiledOffset(int x, int y, int pitchBlocks, int blockLog2)
    {
        var pitch = (pitchBlocks + 31) & ~31;
        var macro = ((x >> 5) + (y >> 5) * (pitch >> 5)) << (blockLog2 + 7);
        var micro = ((x & 7) + ((y & 0xE) << 2)) << blockLog2;
        var offset = macro + ((micro & ~0xF) << 1) + (micro & 0xF) + ((y & 1) << 4);
        return ((offset & ~0x1FF) << 3) + ((y & 16) << 7) + ((offset & 0x1C0) << 2) + (((((y & 8) >> 2) + (x >> 3)) & 3) << 6) + (offset & 0x3F);
    }

    // 1 = 8in16, 2 = 8in32, 3 = 16in32 (EndianSwap in src/native/texture_layout.cpp).
    private static void Swap(byte[] data, int endian)
    {
        switch (endian)
        {
            case 1:
                for (var i = 0; i + 1 < data.Length; i += 2) (data[i], data[i + 1]) = (data[i + 1], data[i]);
                break;
            case 2:
                for (var i = 0; i + 3 < data.Length; i += 4)
                {
                    (data[i], data[i + 3]) = (data[i + 3], data[i]);
                    (data[i + 1], data[i + 2]) = (data[i + 2], data[i + 1]);
                }
                break;
            case 3:
                for (var i = 0; i + 3 < data.Length; i += 4)
                {
                    (data[i], data[i + 2]) = (data[i + 2], data[i]);
                    (data[i + 1], data[i + 3]) = (data[i + 3], data[i + 1]);
                }
                break;
        }
    }

    // 8888 with the game's swizzle reads B, G, R, A per texel once swapped.
    private static RgbaImage FromBgra(byte[] bgra, int width, int height)
    {
        var rgba = new byte[bgra.Length];
        for (var i = 0; i < bgra.Length; i += 4)
        {
            rgba[i] = bgra[i + 2];
            rgba[i + 1] = bgra[i + 1];
            rgba[i + 2] = bgra[i];
            rgba[i + 3] = bgra[i + 3];
        }
        return new RgbaImage(width, height, rgba);
    }
}

/// <summary>A CPU decoder for the BC1, BC2 and BC3 (DXT1, DXT3, DXT5) block formats, little-endian blocks.</summary>
internal static class BlockCompression
{
    public enum Kind { Bc1, Bc2, Bc3 }

    public static RgbaImage Decode(byte[] blocks, int blocksWide, int blocksHigh, Kind kind)
    {
        var blockBytes = kind == Kind.Bc1 ? 8 : 16;
        var width = blocksWide * 4;
        var pixels = new byte[width * blocksHigh * 4 * 4];
        Span<byte> colours = stackalloc byte[16];
        Span<byte> alphas = stackalloc byte[16];
        for (var by = 0; by < blocksHigh; by++)
        {
            for (var bx = 0; bx < blocksWide; bx++)
            {
                var block = blocks.AsSpan((by * blocksWide + bx) * blockBytes, blockBytes);
                var colour = kind == Kind.Bc1 ? block : block[8..];
                Palette(colour, colours, opaqueFourColour: kind != Kind.Bc1);
                if (kind == Kind.Bc2) ExplicitAlpha(block, alphas);
                else if (kind == Kind.Bc3) InterpolatedAlpha(block, alphas);
                var indices = BinaryPrimitives.ReadUInt32LittleEndian(colour[4..]);
                for (var i = 0; i < 16; i++)
                {
                    var index = (int)(indices >> (2 * i)) & 3;
                    var at = ((by * 4 + i / 4) * width + bx * 4 + i % 4) * 4;
                    colours.Slice(index * 4, 4).CopyTo(pixels.AsSpan(at));
                    if (kind != Kind.Bc1) pixels[at + 3] = alphas[i];
                }
            }
        }
        return new RgbaImage(width, blocksHigh * 4, pixels);
    }

    // The block's four colours, R, G, B, A each. BC1 with c0 <= c1 has three colours and transparent black.
    private static void Palette(ReadOnlySpan<byte> block, Span<byte> colours, bool opaqueFourColour)
    {
        var c0 = BinaryPrimitives.ReadUInt16LittleEndian(block);
        var c1 = BinaryPrimitives.ReadUInt16LittleEndian(block[2..]);
        Span<int> a = [Expand(c0 >> 11, 5), Expand((c0 >> 5) & 63, 6), Expand(c0 & 31, 5)];
        Span<int> b = [Expand(c1 >> 11, 5), Expand((c1 >> 5) & 63, 6), Expand(c1 & 31, 5)];
        var four = opaqueFourColour || c0 > c1;
        for (var ch = 0; ch < 3; ch++)
        {
            colours[ch] = (byte)a[ch];
            colours[4 + ch] = (byte)b[ch];
            colours[8 + ch] = (byte)(four ? (2 * a[ch] + b[ch]) / 3 : (a[ch] + b[ch]) / 2);
            colours[12 + ch] = (byte)(four ? (a[ch] + 2 * b[ch]) / 3 : 0);
        }
        colours[3] = colours[7] = colours[11] = 255;
        colours[15] = (byte)(four ? 255 : 0);
    }

    private static int Expand(int value, int bits) => (value << (8 - bits)) | (value >> (2 * bits - 8));

    private static void ExplicitAlpha(ReadOnlySpan<byte> block, Span<byte> alphas)
    {
        for (var i = 0; i < 16; i++)
        {
            var nibble = (block[i / 2] >> (4 * (i % 2))) & 15;
            alphas[i] = (byte)(nibble * 17);
        }
    }

    private static void InterpolatedAlpha(ReadOnlySpan<byte> block, Span<byte> alphas)
    {
        int a0 = block[0], a1 = block[1];
        Span<int> levels = stackalloc int[8];
        levels[0] = a0;
        levels[1] = a1;
        if (a0 > a1)
            for (var i = 1; i < 7; i++) levels[i + 1] = ((7 - i) * a0 + i * a1) / 7;
        else
        {
            for (var i = 1; i < 5; i++) levels[i + 1] = ((5 - i) * a0 + i * a1) / 5;
            levels[6] = 0;
            levels[7] = 255;
        }
        ulong bits = 0;
        for (var i = 0; i < 6; i++) bits |= (ulong)block[2 + i] << (8 * i);
        for (var i = 0; i < 16; i++) alphas[i] = (byte)levels[(int)(bits >> (3 * i)) & 7];
    }
}
