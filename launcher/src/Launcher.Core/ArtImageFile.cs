using System.Buffers.Binary;

namespace TopSpin4.Launcher.Core;

/// <summary>What the header of an art image says it is: PNG or JPEG, and its size in pixels.</summary>
internal readonly record struct ArtImageFile(string Extension, int Width, int Height)
{
    private const int MaxSide = 16384;
    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public bool IsPng => Extension == ".png";

    /// <summary>
    /// Reads the header of a PNG or JPEG and checks that the file ends where such a file ends (the
    /// IEND chunk, the EOI marker), so a cut-off file is refused. The pixels are not decoded:
    /// the Launcher stores the image as it is and the UI toolkit draws it.
    /// Throws <see cref="InvalidDataException"/> for anything else.
    /// </summary>
    public static ArtImageFile Describe(ReadOnlySpan<byte> bytes)
    {
        var image = bytes.StartsWith(PngSignature) ? DescribePng(bytes)
            : bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF ? DescribeJpeg(bytes)
            : throw new InvalidDataException("It is neither a PNG nor a JPEG image.");
        if (image.Width is < 1 or > MaxSide || image.Height is < 1 or > MaxSide)
            throw new InvalidDataException($"The image size {image.Width}x{image.Height} is not plausible.");
        return image;
    }

    private static ArtImageFile DescribePng(ReadOnlySpan<byte> bytes)
    {
        // Signature, then the IHDR chunk: length 13, "IHDR", width, height.
        if (bytes.Length < 8 + 8 + 13 + 4 + 12 || !bytes.Slice(12, 4).SequenceEqual("IHDR"u8)
            || BinaryPrimitives.ReadUInt32BigEndian(bytes[8..]) != 13)
            throw new InvalidDataException("The PNG has no valid header.");
        if (!bytes[^8..^4].SequenceEqual("IEND"u8)) throw new InvalidDataException("The PNG is cut off.");
        return new ArtImageFile(".png", BinaryPrimitives.ReadInt32BigEndian(bytes[16..]), BinaryPrimitives.ReadInt32BigEndian(bytes[20..]));
    }

    private static ArtImageFile DescribeJpeg(ReadOnlySpan<byte> bytes)
    {
        if (bytes[^2] != 0xFF || bytes[^1] != 0xD9) throw new InvalidDataException("The JPEG is cut off.");
        // Walk the marker segments up to the frame header (SOF), which holds the size.
        var at = 2;
        while (at + 4 <= bytes.Length)
        {
            if (bytes[at] != 0xFF) throw new InvalidDataException("The JPEG has a damaged header.");
            var marker = bytes[at + 1];
            if (marker == 0xFF) { at++; continue; }
            if (marker is 0x01 or >= 0xD0 and <= 0xD8) { at += 2; continue; }
            if (marker == 0xDA) break; // the pixel data starts before any frame header
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes[(at + 2)..]);
            if (length < 2) throw new InvalidDataException("The JPEG has a damaged header.");
            var frame = marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC);
            if (frame)
            {
                if (length < 8 || at + 9 > bytes.Length) throw new InvalidDataException("The JPEG has a damaged header.");
                return new ArtImageFile(".jpg", BinaryPrimitives.ReadUInt16BigEndian(bytes[(at + 7)..]),
                    BinaryPrimitives.ReadUInt16BigEndian(bytes[(at + 5)..]));
            }
            at += 2 + length;
        }
        throw new InvalidDataException("The JPEG has no frame header.");
    }
}
