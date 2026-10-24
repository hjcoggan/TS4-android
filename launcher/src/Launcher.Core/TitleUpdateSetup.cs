using System.Security.Cryptography;

namespace TopSpin4.Launcher.Core;

/// <summary>
/// The TU1 half of preparation: open the package and check it is Title Update 1 for the
/// Reference build's disc, then unpack its files into the update data folder, verifying each.
/// The runtime mounts that folder as <c>update:</c> and applies each module patch from it. The
/// package is only ever read.
/// </summary>
internal sealed class TitleUpdateSetup(FileManifest manifest, IDiskSpace space)
{
    public static StfsPackage Open(string? packagePath)
    {
        if (string.IsNullOrEmpty(packagePath))
            throw new SetupException(SetupError.NoTitleUpdate, "No title update was chosen. " + WhatTitleUpdate1Is);
        if (!File.Exists(packagePath))
            throw new SetupException(SetupError.NoTitleUpdate, $"The title update was not found: {packagePath}. " + WhatTitleUpdate1Is);
        try
        {
            return StfsPackage.Open(packagePath);
        }
        catch (InvalidDataException error)
        {
            throw new SetupException(SetupError.NoTitleUpdate,
                $"That file is not an Xbox 360 title update package: {packagePath}. " + WhatTitleUpdate1Is, error);
        }
    }

    /// <summary>What a player without TU1 is told: what it is and where it comes from, no links.</summary>
    public const string WhatTitleUpdate1Is =
        "Title Update 1 is the free update Top Spin 4 got on Xbox 360; the port is built on it and refuses to run without it. " +
        "It is not included: copy it from your own console (the TU_1A5822P_000000C000000.0000000000083 file in the console's Content " +
        "or Cache folder) and choose it here.";

    /// <remarks>
    /// The header first (a title update for this title and disc edition), then every block
    /// against its hash, so that what follows a mismatch means another update rather than
    /// damage: the module patches' target versions, then every file against the manifest.
    /// </remarks>
    public void Check(StfsPackage package, CancellationToken cancel)
    {
        if (package.TitleId != ReferenceBuild.TitleId)
            throw WrongTitleUpdate($"it is for another game (title ID {package.TitleId:X8}).");
        if (package.ContentType != StfsContentType.TitleUpdate)
            throw WrongTitleUpdate($"it is Top Spin 4 content ({package.DisplayName}), but not a title update.");
        if (package.MediaId != ReferenceBuild.MediaId)
            throw WrongTitleUpdate($"it is for another edition of the game (media ID {package.MediaId:X8}).");
        try
        {
            package.Verify(null, cancel);
        }
        catch (InvalidDataException error)
        {
            throw new SetupException(SetupError.DamagedFile,
                "The title update package is damaged: " + error.Message + " Copy it from your console again.", error);
        }
        foreach (var patch in ReferenceBuild.Modules.Select(ReferenceBuild.PatchOf))
        {
            if (package.Find(patch) is not { } file) throw WrongTitleUpdate($"it has no {patch}.");
            var header = XexHeader.Parse(package.ReadAll(file).AsSpan(0, (int)Math.Min(file.Size, XexHeader.MaxHeaderBytes)));
            if (header?.DeltaPatch is not { } delta) throw WrongTitleUpdate($"its {patch} is not a module patch.");
            if (delta.Target != ReferenceBuild.TitleUpdateVersion)
                throw WrongTitleUpdate($"its {patch} raises the Module to {delta.Target}, and Title Update 1 raises it to {ReferenceBuild.TitleUpdateVersion}.");
        }
        if (package.Files.Count != manifest.Files.Count || manifest.Files.Any(entry =>
                package.Find(entry.Path) is not { } file || file.Size != entry.Size || Hash(package, file, cancel) != entry.Sha256))
            throw WrongTitleUpdate("its files differ from Title Update 1's.");
    }

    private static string Hash(StfsPackage package, StfsFile file, CancellationToken cancel)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        package.CopyTo(file, hash.AppendData, cancel);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static SetupException WrongTitleUpdate(string reason) =>
        new(SetupError.WrongTitleUpdate,
            $"That package is not Title Update 1 for the supported Top Spin 4 disc: {reason} " +
            "The port is built on Title Update 1 (TU_1A5822P_000000C000000.0000000000083) and needs exactly that update.");

    public void Unpack(StfsPackage package, PreparedFolder folder, Reporter reporter, CancellationToken cancel)
    {
        var pending = folder.Resume(reporter, cancel);
        folder.CheckSpace(space, pending, Preparation.SpareBytes, "Title Update 1");
        reporter.Start(SetupStep.UnpackingTitleUpdate, manifest.TotalBytes, manifest.TotalBytes - pending.Sum(entry => entry.Size));
        foreach (var entry in pending)
        {
            var file = package.Find(entry.Path)!;
            reporter.Report(entry.Path, force: true);
            folder.Write(entry, output => package.CopyTo(file, block =>
            {
                output(block);
                reporter.Advance(block.Length, entry.Path);
            }, cancel), () => new SetupException(SetupError.DamagedFile,
                $"A Title Update 1 file failed verification: {entry.Path}. Copy the update from your console again."));
        }
        folder.Commit();
    }
}
