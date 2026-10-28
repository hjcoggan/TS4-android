using System.Buffers.Binary;
using System.IO.Compression;

namespace TopSpin4.Launcher.Tests;

/// <summary>
/// Tiny generated images for the Disc art tests: a valid PNG (signature, IHDR, one IDAT, IEND, all
/// CRCs right) and a JPEG-shaped file (SOI, one SOF0 segment, EOI) that no decoder would draw
/// but whose header says how big it is. The pixels are a seeded pattern; nothing is game art.
/// </summary>
internal static class TestArtImages
{
    public static byte[] Png(int width, int height, int seed)
    {
        var raw = new byte[height * (1 + width * 4)];
        for (var i = 0; i < raw.Length; i++) raw[i] = (byte)(i * 7 + seed * 13);
        for (var row = 0; row < height; row++) raw[row * (1 + width * 4)] = 0;
        using var data = new MemoryStream();
        using (var zlib = new ZLibStream(data, CompressionLevel.Fastest, leaveOpen: true)) zlib.Write(raw);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; // bit depth
        header[9] = 6; // RGBA

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", data.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    public static byte[] Jpeg(int width, int height, int seed)
    {
        var jpeg = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x4A, 0x46 }; // SOI, a short APP0
        jpeg.AddRange([0xFF, 0xC0, 0x00, 0x0B, 0x08, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 0x01, 0x01, 0x11, 0x00]);
        for (var i = 0; i < 64; i++) jpeg.Add((byte)(i * 5 + seed));
        jpeg.AddRange([0xFF, 0xD9]);
        return [.. jpeg];
    }

    private static void Chunk(Stream output, string type, byte[] data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        var body = new byte[4 + data.Length];
        System.Text.Encoding.ASCII.GetBytes(type).CopyTo(body, 0);
        data.CopyTo(body, 4);
        output.Write(body);
        var crc = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(body));
        output.Write(crc);
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? crc >> 1 ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }
}
