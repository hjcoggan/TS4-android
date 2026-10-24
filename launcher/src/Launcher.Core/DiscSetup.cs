namespace TopSpin4.Launcher.Core;

/// <summary>
/// The disc half of preparation: open the image and check it is the Reference build's disc,
/// then extract its game partition into the game data folder, verifying every file. The image
/// is only ever read.
/// </summary>
internal sealed class DiscSetup(FileManifest manifest, IDiskSpace space)
{
    public static XdvdfsImage Open(string? isoPath)
    {
        if (string.IsNullOrEmpty(isoPath))
            throw new SetupException(SetupError.ImageNotFound, "Choose the disc image (.iso) you made from your Top Spin 4 disc.");
        if (!File.Exists(isoPath))
            throw new SetupException(SetupError.ImageNotFound, $"The disc image was not found: {isoPath}");
        try
        {
            return XdvdfsImage.TryOpen(isoPath) ?? throw NotAnXboxImage(null);
        }
        catch (InvalidDataException error)
        {
            throw NotAnXboxImage(error);
        }
        catch (UnauthorizedAccessException error)
        {
            throw new SetupException(SetupError.NoPermission, $"The disc image cannot be read: {isoPath}. {error.Message}", error);
        }
    }

    /// <summary>The disc's files, once the image is known to be the Reference build's disc.</summary>
    public IReadOnlyList<DiscFile> Check(XdvdfsImage image, CancellationToken cancel)
    {
        try
        {
            return image.Files();
        }
        catch (InvalidDataException error)
        {
            throw NotAnXboxImage(error);
        }
    }

    public void Extract(XdvdfsImage image, IReadOnlyList<DiscFile> files, PreparedFolder folder, Reporter reporter,
        CancellationToken cancel)
    {
        var pending = folder.Resume(file => reporter.Report(file), cancel);
        folder.CheckSpace(space, pending, Preparation.SpareBytes, "the game data");
        reporter.Start(SetupStep.Extracting, manifest.TotalBytes, manifest.TotalBytes - pending.Sum(entry => entry.Size));
        var byPath = files.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in pending)
        {
            var file = byPath[entry.Path];
            reporter.Report(entry.Path, force: true);
            folder.Write(entry, output => image.CopyTo(file, block =>
            {
                output(block);
                reporter.Advance(block.Length, entry.Path);
            }, cancel), () => new SetupException(SetupError.DamagedFile,
                $"A game file failed verification: {entry.Path}. Your disc image may be damaged; make a new copy of your disc and try again."));
        }
        folder.Commit();
    }

    private static SetupException NotAnXboxImage(Exception? inner) =>
        new(SetupError.NotAnXboxImage,
            "This file is not an Xbox 360 disc image. Choose the .iso you made from your Top Spin 4 disc.", inner);
}
