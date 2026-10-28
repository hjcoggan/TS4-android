using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Xna.Framework.Content;

namespace TopSpin4.Launcher.Core;

/// <summary>
/// Reads the title image (the dashboard's title icon) out of an XEX2 Module. The image is in the
/// title resource, an XDBF inside the PE image, which the XEX keeps encrypted and LZX-compressed.
/// The steps are Xenia's <c>XexModule::ReadImageCompressed</c> (and ReXGlue's copy), as written
/// down in docs/research/disc-art.md, section 3.2: decrypt the image key with the published retail
/// key (the all-zero development key when that fails), decrypt the data with it, walk the
/// hash-chained blocks, LZX-decompress each chunk and read the XDBF. Only as much of the image
/// as reaches the resource is decoded.
/// </summary>
internal static class XexTitleImage
{
    private const uint ResourceInfoKey = 0x000002FF;
    private const uint FileFormatInfoKey = 0x000003FF;
    private const ulong TitleImageId = 0x8000;
    private const int Frame = 0x8000;
    private const int MaxImageBytes = 64 << 20;

    // The retail key every console and emulator uses to unlock an XEX2's image key: a published constant (Xenia, Free60), not game data.
    private static readonly byte[] RetailKey = [0x20, 0xB1, 0x85, 0xA5, 0x9D, 0x28, 0xFD, 0xC3, 0x40, 0x58, 0x3F, 0xBB, 0x08, 0x96, 0xBF, 0x91];

    /// <summary>
    /// The PNG title image of the Module in <paramref name="xex"/>, or null when it carries none
    /// (no resource for its title, or no title image in it). <paramref name="execution"/> is the
    /// Module's execution info, already read from its header (<see cref="XexHeader.Parse"/>); the
    /// title resource is named after its title id. Throws <see cref="InvalidDataException"/>
    /// when the Module is damaged, cut off or encoded in a way this does not read.
    /// </summary>
    public static byte[]? Read(byte[] xex, XexExecutionInfo? execution)
    {
        if (xex.Length < 0x18 || !xex.AsSpan(0, 4).SequenceEqual("XEX2"u8)) throw new InvalidDataException("It is not an Xbox 360 executable.");
        var peOffset = U32(xex, 8);
        var security = U32(xex, 0x10);
        var count = U32(xex, 0x14);
        if (peOffset < 0x18 || peOffset > xex.Length || count > (peOffset - 0x18) / 8) throw new InvalidDataException("The executable's header is damaged.");
        var headers = new Dictionary<uint, uint>();
        for (var i = 0; i < count; i++) headers[U32(xex, 0x18 + i * 8)] = U32(xex, 0x1C + i * 8);

        if (!headers.TryGetValue(ResourceInfoKey, out var resourceInfo) || execution is not { } info) return null;
        if (!TryFindResource(xex, resourceInfo, info.TitleId.ToString("X8"), out var address, out var resourceSize)) return null;
        if (!headers.TryGetValue(FileFormatInfoKey, out var format)) throw new InvalidDataException("The executable has no file format information.");
        if (security > xex.Length - 0x160) throw new InvalidDataException("The executable's security information is damaged.");
        var imageSize = U32(xex, security + 4);
        var loadAddress = U32(xex, security + 0x110);
        if (imageSize is 0 or > MaxImageBytes || address < loadAddress || address - loadAddress > imageSize
            || resourceSize > imageSize - (address - loadAddress) || resourceSize < 0x18)
            throw new InvalidDataException("The title resource lies outside the executable's image.");
        var resourceOffset = (int)(address - loadAddress);

        InvalidDataException? first = null;
        foreach (var key in new[] { RetailKey, new byte[16] })
        {
            try
            {
                var image = DecodeImage(xex, format, peOffset, security, key, (int)imageSize, resourceOffset + (int)resourceSize);
                return TitleImage(image.AsSpan(resourceOffset, (int)resourceSize));
            }
            catch (InvalidDataException error)
            {
                first ??= error;
            }
        }
        throw first!;
    }

    private static bool TryFindResource(byte[] xex, uint info, string name, out uint address, out uint size)
    {
        address = size = 0;
        if (info > xex.Length - 4) throw new InvalidDataException("The executable's resource list is damaged.");
        var total = U32(xex, info);
        if (total < 4 || total > xex.Length - info) throw new InvalidDataException("The executable's resource list is damaged.");
        for (var entry = info + 4; entry + 16 <= info + total; entry += 16)
        {
            if (Encoding.ASCII.GetString(xex, (int)entry, 8).TrimEnd('\0') != name) continue;
            address = U32(xex, entry + 8);
            size = U32(xex, entry + 12);
            return true;
        }
        return false;
    }

    // The image up to at least `needed` bytes (it is not decoded past the end of the last chunk that reaches it).
    private static byte[] DecodeImage(byte[] xex, uint format, uint peOffset, uint security, byte[] key, int imageSize, int needed)
    {
        if (format > xex.Length - 0x24) throw new InvalidDataException("The executable's file format information is damaged.");
        var encryption = BinaryPrimitives.ReadUInt16BigEndian(xex.AsSpan((int)format + 4));
        var compression = BinaryPrimitives.ReadUInt16BigEndian(xex.AsSpan((int)format + 6));
        if (encryption > 1 || compression is not (0 or 2))
            throw new InvalidDataException($"The executable's encoding (encryption {encryption}, compression {compression}) is not supported.");

        var body = xex.AsSpan((int)peOffset, (xex.Length - (int)peOffset) & ~15).ToArray();
        if (encryption == 1)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.Key = aes.DecryptCbc(xex.AsSpan((int)security + 0x150, 16), new byte[16], PaddingMode.None);
            body = aes.DecryptCbc(body, new byte[16], PaddingMode.None);
        }
        if (compression == 0)
        {
            if (body.Length < needed) throw new InvalidDataException("The executable's image is cut off.");
            return body;
        }

        var window = U32(xex, format + 8);
        if (!BitOperations.IsPow2(window)) throw new InvalidDataException("The executable's compression window is damaged.");
        var image = new MemoryStream(Math.Min(imageSize, needed + Frame));
        try
        {
            var lzx = new LzxDecoder(BitOperations.Log2(window));
            var position = 0;
            var size = U32(xex, format + 12);
            var digest = xex.AsSpan((int)format + 16, 20).ToArray();
            while (size != 0 && image.Length < needed)
            {
                if (size < 26 || position > body.Length - size) throw new InvalidDataException("The executable's image is cut off.");
                var block = body.AsSpan(position, (int)size);
                if (!SHA1.HashData(block).AsSpan().SequenceEqual(digest)) throw new InvalidDataException("A block of the executable's image does not match its hash.");
                var nextSize = U32(block, 0);
                var nextDigest = block.Slice(4, 20).ToArray();
                var at = 24;
                while (image.Length < needed && at + 2 <= block.Length)
                {
                    var chunk = BinaryPrimitives.ReadUInt16BigEndian(block[at..]);
                    at += 2;
                    if (chunk == 0) break;
                    var frame = Math.Min(Frame, imageSize - (int)image.Length);
                    if (frame <= 0 || at + chunk > block.Length) throw new InvalidDataException("The executable's compressed data is damaged.");
                    using var input = new MemoryStream(body, position + at, chunk, writable: false);
                    if (lzx.Decompress(input, chunk, image, frame) != 0) throw new InvalidDataException("The executable's compressed data is damaged.");
                    at += chunk;
                }
                position += (int)size;
                size = nextSize;
                digest = nextDigest;
            }
        }
        catch (Exception error) when (error is IndexOutOfRangeException or ArgumentException or OverflowException or UnsupportedWindowSizeRange)
        {
            throw new InvalidDataException("The executable's compressed data is damaged.", error);
        }
        if (image.Length < needed) throw new InvalidDataException("The executable's image is cut off.");
        return image.ToArray();
    }

    // The XDBF in the title resource: header, entry table (section, id, offset, size), free table, then the data the offsets count from.
    private static byte[]? TitleImage(ReadOnlySpan<byte> resource)
    {
        if (!resource.StartsWith("XDBF"u8)) throw new InvalidDataException("The title resource is damaged.");
        var tableLength = BinaryPrimitives.ReadUInt32BigEndian(resource[8..]);
        var used = BinaryPrimitives.ReadUInt32BigEndian(resource[12..]);
        var freeLength = BinaryPrimitives.ReadUInt32BigEndian(resource[16..]);
        var dataStart = 0x18L + (long)tableLength * 18 + (long)freeLength * 8;
        if (used > tableLength || dataStart > resource.Length) throw new InvalidDataException("The title resource is damaged.");
        for (var i = 0; i < used; i++)
        {
            var entry = resource.Slice(0x18 + i * 18, 18);
            if (BinaryPrimitives.ReadUInt16BigEndian(entry) != 2 || BinaryPrimitives.ReadUInt64BigEndian(entry[2..]) != TitleImageId) continue;
            long offset = dataStart + BinaryPrimitives.ReadUInt32BigEndian(entry[10..]);
            long size = BinaryPrimitives.ReadUInt32BigEndian(entry[14..]);
            if (offset + size > resource.Length) throw new InvalidDataException("The title image lies outside the title resource.");
            return resource.Slice((int)offset, (int)size).ToArray();
        }
        return null;
    }

    private static uint U32(ReadOnlySpan<byte> bytes, uint offset) => BinaryPrimitives.ReadUInt32BigEndian(bytes[(int)offset..]);

    private static uint U32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32BigEndian(bytes[offset..]);
}
