using System.Buffers.Binary;
using Microsoft.Xna.Framework.Content;

namespace TopSpin4.Launcher.Core;

/// <summary>
/// Reads the game's shader containers straight from the prepared game folder's <c>.arc</c> archives
/// (the shader reader of .scratch/shader-setup/spec.md). An archive is a 0x24-byte header (data base
/// at word 0 + 4, entry count at word 2, <c>PAM-LZX</c> or <c>PAM-NIL</c> at 0x1C), then
/// <c>{name hash, offset from the data base, length}</c> per entry. A PAM-LZX entry has its own 0x2C-byte
/// header (<c>PAM-LZX</c>, part count, window, uncompressed size, ..., header size at 0x24) and then frames:
/// <c>FF u16 uncompressed u16 compressed</c>, or <c>u16 compressed</c> for a whole 0x8000-byte frame, each an
/// LZX stream continuing the entry's window. A PAM-NIL entry (CutScenes.arc) is stored as it is.
/// Entries are matched on the container magic, not on names, because the archives' name tables are incomplete.
/// An entry with more than one part (textures over 1 MB; no shader is multi-part) is skipped.
/// </summary>
public static class ArchiveShaders
{
    private const string ArchivePattern = "*.arc";
    private const int TableStart = 0x24;
    private const int EntryRow = 12;
    private const int EntryHeader = 0x2C;
    private const int FrameSize = 0x8000;
    private static readonly byte[] LzxMagic = "PAM-LZX\0"u8.ToArray();
    private static readonly byte[] StoredMagic = "PAM-NIL\0"u8.ToArray();

    /// <summary>
    /// The unique containers of every archive under <paramref name="gameDataDirectory"/>, in name order.
    /// Progress counts entries done of all entries.
    /// </summary>
    /// <exception cref="InvalidDataException">An archive is damaged; the message names it.</exception>
    public static IReadOnlyList<ShaderContainer> Read(string gameDataDirectory, IProgress<(int Done, int Total)>? progress,
        CancellationToken cancel)
    {
        var archives = new List<Archive>();
        foreach (var file in Directory.EnumerateFiles(gameDataDirectory, ArchivePattern, SearchOption.AllDirectories)
                     .Order(StringComparer.OrdinalIgnoreCase))
        {
            cancel.ThrowIfCancellationRequested();
            var name = Path.GetRelativePath(gameDataDirectory, file);
            try
            {
                archives.Add(Archive.Open(file, name));
            }
            catch (Exception error) when (error is InvalidDataException or EndOfStreamException)
            {
                throw Damaged(name, error.Message, error);
            }
        }

        var total = archives.Sum(archive => archive.Entries.Count);
        var done = 0;
        var found = new Dictionary<string, ShaderContainer>(StringComparer.Ordinal);
        foreach (var archive in archives)
        {
            using var stream = new FileStream(archive.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
            for (var i = 0; i < archive.Entries.Count; i++)
            {
                cancel.ThrowIfCancellationRequested();
                try
                {
                    if (ReadEntry(stream, archive, archive.Entries[i]) is { } container) found.TryAdd(container.Name, container);
                }
                catch (Exception error) when (error is InvalidDataException or EndOfStreamException or IndexOutOfRangeException
                                                  or ArgumentException or OverflowException or UnsupportedWindowSizeRange)
                {
                    throw Damaged(archive.Name, $"entry {i + 1} is damaged ({error.Message})", error);
                }
                progress?.Report((++done, total));
            }
        }
        return [.. found.Values.OrderBy(container => container.Name, StringComparer.Ordinal)];
    }

    private static InvalidDataException Damaged(string archive, string why, Exception inner) =>
        new($"A game file could not be read for shaders: {archive}. {why.TrimEnd('.')}.", inner);

    private readonly record struct Entry(long Offset, long Length);

    private sealed record Archive(string Path, string Name, List<Entry> Entries, bool Stored)
    {
        public static Archive Open(string path, string name)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
            var head = new byte[TableStart];
            if (stream.Read(head) < head.Length) throw new InvalidDataException("the file is cut short");
            var kind = head.AsSpan(0x1C, 8);
            var stored = kind.SequenceEqual(StoredMagic);
            if (!stored && !kind.SequenceEqual(LzxMagic)) throw new InvalidDataException("it is not a game archive");
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
            return new Archive(path, name, entries, stored);
        }
    }

    private static ShaderContainer? ReadEntry(FileStream stream, Archive archive, Entry entry)
    {
        if (entry.Length < 12) return null;
        stream.Position = entry.Offset;
        var head = new byte[Math.Min(entry.Length, EntryHeader)];
        stream.ReadExactly(head);
        if (!head.AsSpan().StartsWith(LzxMagic))
        {
            // Stored: the entry is the data. Only a plausible container is read in full.
            if (!ShaderContainer.HasMagic(head) || entry.Length > ShaderContainer.MaxSize) return null;
            var stored = new byte[entry.Length];
            head.CopyTo(stored, 0);
            stream.ReadExactly(stored.AsSpan(head.Length));
            return ShaderContainer.IsContainer(stored) ? ShaderContainer.Of(stored) : null;
        }

        if (head.Length < EntryHeader) throw new InvalidDataException("its header is cut short");
        var parts = BinaryPrimitives.ReadUInt32BigEndian(head.AsSpan(8));
        var headerSize = BinaryPrimitives.ReadUInt32BigEndian(head.AsSpan(0x24));
        if (parts != 1 || headerSize != EntryHeader) return null;
        var size = BinaryPrimitives.ReadUInt32BigEndian(head.AsSpan(0x10));
        if (size < 12 || size > ShaderContainer.MaxSize) return null;
        var window = BinaryPrimitives.ReadUInt32BigEndian(head.AsSpan(0x0C));
        if (window < 1 << 15 || window > 1 << 21 || !System.Numerics.BitOperations.IsPow2(window))
            throw new InvalidDataException("its window size is not one the game uses");

        var body = new byte[entry.Length - EntryHeader];
        stream.ReadExactly(body);
        var data = Decode(body, (int)size, System.Numerics.BitOperations.Log2(window));
        return data is not null && ShaderContainer.IsContainer(data) ? ShaderContainer.Of(data) : null;
    }

    /// <summary>
    /// Decodes the frames of an entry; null as soon as the first frame shows the entry is not a container,
    /// so the rest of a texture is never decoded.
    /// </summary>
    private static byte[]? Decode(byte[] body, int size, int windowBits)
    {
        var lzx = new LzxDecoder(windowBits);
        var output = new MemoryStream(size);
        var at = 0;
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
            if (output.Length == frame && !ShaderContainer.HasMagic(output.GetBuffer().AsSpan(0, (int)Math.Min(output.Length, 4))))
                return null;
        }
        return output.ToArray();
    }
}
