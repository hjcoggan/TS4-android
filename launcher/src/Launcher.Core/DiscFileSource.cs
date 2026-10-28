namespace TopSpin4.Launcher.Core;

/// <summary>Where the Disc art module reads the disc's small files from: the <c>.iso</c> or the prepared disc folder.</summary>
internal interface IDiscFileSource : IDisposable
{
    // The art sources are about 1 MB each; this keeps a damaged directory entry from asking for a whole disc.
    const long MaxFileBytes = 16 << 20;

    /// <summary>The whole file at <paramref name="path"/> ('/' between folders, any case), or null when the disc has none.</summary>
    byte[]? Read(string path);
}

/// <summary>The files of a disc image, read straight from it without extracting anything.</summary>
internal sealed class IsoFileSource : IDiscFileSource
{
    private readonly XdvdfsImage _image;
    private readonly Dictionary<string, DiscFile> _files;

    private IsoFileSource(XdvdfsImage image)
    {
        _image = image;
        _files = image.Files().ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Opens the image; null when it holds no Xbox 360 disc volume. Throws for an unreadable or damaged one.</summary>
    public static IsoFileSource? TryOpen(string isoPath)
    {
        var image = XdvdfsImage.TryOpen(isoPath);
        if (image is null) return null;
        try
        {
            return new IsoFileSource(image);
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    public byte[]? Read(string path)
    {
        if (!_files.TryGetValue(path, out var file)) return null;
        if (file.Size > IDiscFileSource.MaxFileBytes) throw new InvalidDataException($"{path} is {file.Size} bytes, far larger than the art it should be.");
        return _image.ReadStart(file, (int)file.Size);
    }

    public void Dispose() => _image.Dispose();
}

/// <summary>The files of the prepared disc folder (the extracted game partition).</summary>
internal sealed class FolderFileSource(string root) : IDiscFileSource
{
    public byte[]? Read(string path)
    {
        var full = SafePath.Inside(root, path);
        if (full is null || !File.Exists(full)) return null;
        if (new FileInfo(full).Length > IDiscFileSource.MaxFileBytes) throw new InvalidDataException($"{path} is far larger than the art it should be.");
        return File.ReadAllBytes(full);
    }

    public void Dispose()
    {
    }
}
