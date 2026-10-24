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
            throw new SetupException(SetupError.NoTitleUpdate, "The port needs Title Update 1. " + WhatTitleUpdate1Is);
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

    public void Check(StfsPackage package, CancellationToken cancel)
    {
    }

    public void Unpack(StfsPackage package, PreparedFolder folder, Reporter reporter, CancellationToken cancel)
    {
        var pending = folder.Resume(file => reporter.Report(file), cancel);
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
