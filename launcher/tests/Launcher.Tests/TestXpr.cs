using System.Buffers.Binary;
using System.Text;
using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

/// <summary>
/// Generated XPR2 containers as the game's archives hold them (docs/research/disc-art.md, section 6.3): named 2D textures,
/// tiled, byte-swapped as the console stores them (8888 is 8in32, DXT is 8in16), with the GPU fetch constant at +0x1C of
/// each texture header. No game data.
/// </summary>
internal sealed class TestXpr
{
    private const int HeaderSize = 0x800;
    private readonly List<(string Name, int Width, int Height, int Format, byte[] Data, bool Tiled)> _textures = [];

    /// <summary>An 8888 texture from R, G, B, A pixels.</summary>
    public TestXpr Add8888(string name, int width, int height, byte[] rgba, bool tiled = true)
    {
        var pitch = Pitch(width, 1);
        var heightBlocks = Align(height, 32);
        var guest = new byte[pitch * heightBlocks * 4 + 0x1000];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var source = (y * width + x) * 4;
                // Guest bytes are A, R, G, B: the 8in32 swap of B, G, R, A.
                byte[] argb = [rgba[source + 3], rgba[source], rgba[source + 1], rgba[source + 2]];
                var at = tiled ? XprTexture.TiledOffset(x, y, pitch, 2) : y * Align(pitch * 4, 256) + x * 4;
                argb.CopyTo(guest, at);
            }
        }
        _textures.Add((name, width, height, 6, guest, tiled));
        return this;
    }

    /// <summary>A DXT1 (format 18) or DXT5 (format 20) texture from little-endian BC blocks, row by row.</summary>
    public TestXpr AddDxt(string name, int width, int height, int format, byte[] blocks)
    {
        var blockBytes = format == 18 ? 8 : 16;
        var bx = (width + 3) / 4;
        var by = (height + 3) / 4;
        var pitch = Pitch(width, 4);
        var guest = new byte[pitch * Align(by, 32) * blockBytes + 0x1000];
        for (var y = 0; y < by; y++)
        {
            for (var x = 0; x < bx; x++)
            {
                var block = blocks.AsSpan((y * bx + x) * blockBytes, blockBytes).ToArray();
                for (var i = 0; i + 1 < block.Length; i += 2) (block[i], block[i + 1]) = (block[i + 1], block[i]);
                block.CopyTo(guest, XprTexture.TiledOffset(x, y, pitch, format == 18 ? 3 : 4));
            }
        }
        _textures.Add((name, width, height, format, guest, true));
        return this;
    }

    /// <summary>A texture in a format the decoder does not read (CTX1, format 60).</summary>
    public TestXpr AddOther(string name)
    {
        _textures.Add((name, 4, 4, 60, new byte[0x2000], true));
        return this;
    }

    // Pitch in blocks, a multiple of 32 blocks as the console lays tiled textures out.
    private static int Pitch(int width, int blockSide) => Align((width + blockSide - 1) / blockSide, 32);

    private static int Align(int value, int to) => (value + to - 1) / to * to;

    public byte[] ToBytes()
    {
        // The header size counts from 0x0C, where the data section's offset is measured from too.
        var header = new byte[12 + HeaderSize];
        "XPR2"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), HeaderSize);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(12), (uint)_textures.Count);
        var resources = 16 + _textures.Count * 16;
        var names = resources + _textures.Count * 0x34;
        var data = new List<byte>();
        for (var i = 0; i < _textures.Count; i++)
        {
            var (name, width, height, format, bytes, tiled) = _textures[i];
            var row = header.AsSpan(16 + i * 16, 16);
            "TX2D"u8.CopyTo(row);
            var resource = resources + i * 0x34;
            BinaryPrimitives.WriteUInt32BigEndian(row[4..], (uint)(resource - 12));
            BinaryPrimitives.WriteUInt32BigEndian(row[8..], 0x34);
            BinaryPrimitives.WriteUInt32BigEndian(row[12..], (uint)(names - 12));
            Encoding.ASCII.GetBytes(name).CopyTo(header, names);
            names += name.Length + 1;

            var blockSide = format == 6 ? 1 : 4;
            var pitchTexels = Pitch(width, blockSide) * blockSide;
            var fetch = header.AsSpan(resource + 0x1C, 24);
            BinaryPrimitives.WriteUInt32BigEndian(fetch, (tiled ? 1u << 31 : 0) | (uint)(pitchTexels >> 5) << 22 | 2);
            var endian = format == 6 ? 2u : 1u;
            BinaryPrimitives.WriteUInt32BigEndian(fetch[4..], (uint)format | endian << 6 | (uint)data.Count);
            BinaryPrimitives.WriteUInt32BigEndian(fetch[8..], (uint)(width - 1) | (uint)(height - 1) << 13);
            data.AddRange(bytes);
            while (data.Count % 0x1000 != 0) data.Add(0);
        }
        return [.. header, .. data];
    }

    /// <summary>A plain RGBA picture with a different colour in each pixel.</summary>
    public static byte[] Pixels(int width, int height, int seed)
    {
        var rgba = new byte[width * height * 4];
        for (var i = 0; i < rgba.Length; i++) rgba[i] = (byte)(i * 7 + seed * 31 + i / 4 * 3);
        return rgba;
    }

    /// <summary>A BC1 block of one opaque colour (RGB565 <paramref name="colour"/>, every index 0).</summary>
    public static byte[] SolidBc1(ushort colour) => [(byte)colour, (byte)(colour >> 8), 0, 0, 0, 0, 0, 0];
}
