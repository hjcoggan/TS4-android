using System.Security.Cryptography;

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
            throw new SetupException(SetupError.ImageNotFound, $"The disc image was not found: {isoPath}",
                reason: "The disc image was not found. Choose it again.", details: isoPath);
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
            throw new SetupException(SetupError.NoPermission, $"The disc image cannot be read: {isoPath}. {error.Message}", error,
                "The disc image could not be read: Windows does not allow it. Choose a copy you can open.", error.Message);
        }
    }

    /// <summary>The disc's files, once the image is known to be the Reference build's disc.</summary>
    /// <remarks>
    /// The Modules' execution info comes first, so another game or edition is named as such;
    /// then the file list and sizes against the manifest; then the Modules' hashes. Only the
    /// game partition's files are compared, never the whole image: a dump with its security
    /// sectors stripped, like the owner's, has the same files but another image hash.
    /// </remarks>
    public IReadOnlyList<DiscFile> Check(XdvdfsImage image, CancellationToken cancel)
    {
        IReadOnlyList<DiscFile> files;
        try
        {
            files = image.Files();
        }
        catch (InvalidDataException error)
        {
            throw NotAnXboxImage(error);
        }
        var byPath = files.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
        foreach (var module in ReferenceBuild.Modules)
        {
            if (!byPath.TryGetValue(module, out var file)) throw WrongDisc(NotTopSpin4, $"it has no {module}, so it is not a Top Spin 4 disc.");
            if (XexHeader.Parse(image.ReadStart(file, XexHeader.MaxHeaderBytes))?.Execution is not { } info)
                throw WrongDisc(NotTopSpin4, $"its {module} is not an Xbox 360 executable.");
            if (info.TitleId != ReferenceBuild.TitleId)
                throw WrongDisc("This disc is for another game, not Top Spin 4. Choose the image of your Top Spin 4 disc.",
                    $"it is a disc of another game (title ID {info.TitleId:X8}), not Top Spin 4.");
            if (info.MediaId != ReferenceBuild.MediaId)
                throw WrongDisc(AnotherEdition, $"it is another edition of Top Spin 4 (media ID {info.MediaId:X8}).");
            if (info.Version != ReferenceBuild.DiscVersion)
                throw WrongDisc(AnotherEdition, $"its {module} is {info.Version}, and the supported disc's Modules are {ReferenceBuild.DiscVersion}.");
        }
        if (files.Count != manifest.Files.Count
            || files.Any(file => manifest.Find(file.Path) is not { } entry || entry.Size != file.Size))
            throw WrongDisc(Modified, "its files differ from the supported disc's.");
        foreach (var module in ReferenceBuild.Modules)
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            image.CopyTo(byPath[module], hash.AppendData, cancel);
            if (Convert.ToHexStringLower(hash.GetHashAndReset()) != manifest.Find(module)?.Sha256)
                throw WrongDisc(Modified, $"its {module} differs from the supported disc's, so the image is modified or damaged.");
        }
        return files;
    }

    private const string NotTopSpin4 = "This is not a Top Spin 4 disc image. Choose the .iso you made from your Top Spin 4 Xbox 360 disc.";
    private const string AnotherEdition = "This is another edition of Top Spin 4. The port runs only the Xbox 360 disc it was built on.";
    private const string Modified = "This image does not match the supported disc. It may be modified or damaged; make a new image of your disc.";

    private static SetupException WrongDisc(string plain, string reason) =>
        new(SetupError.WrongDisc,
            $"This disc image is not the supported Top Spin 4 disc: {reason} " +
            $"The port runs only the Xbox 360 disc with media ID {ReferenceBuild.MediaId:X8}.",
            reason: plain, details: $"The image {reason} The supported disc has media ID {ReferenceBuild.MediaId:X8}.");

    public void Extract(XdvdfsImage image, IReadOnlyList<DiscFile> files, PreparedFolder folder, Reporter reporter,
        CancellationToken cancel)
    {
        var pending = folder.Resume(reporter, cancel);
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
            "This file is not an Xbox 360 disc image. Choose the .iso you made from your Top Spin 4 disc.", inner,
            "This is not an Xbox 360 disc image. Choose the .iso you made from your Top Spin 4 disc.", inner?.Message);
}
