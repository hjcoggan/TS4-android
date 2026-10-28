using System.Security.Cryptography;

namespace TopSpin4.Launcher.Core;

/// <summary>
/// One folder of the cache, prepared from one input against its manifest. Files are written
/// into <c>&lt;folder&gt;.partial</c>, each through a temp file and verified, then the marker is
/// written and the partial folder renamed into place. The rename is the commit point, so the
/// folder is either complete or absent, and an interrupted preparation resumes from the files
/// that verify.
/// </summary>
internal sealed class PreparedFolder(string directory, FileManifest manifest)
{
    /// <summary>Names the manifest version the folder was verified against.</summary>
    public const string MarkerFileName = ".topspin4-prepared";

    private const string MarkerKey = "manifest_version=";
    private const string TempSuffix = ".tmp";

    public string Directory { get; } = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
    public string Partial => Directory + ".partial";

    /// <summary>
    /// Done when the marker names this manifest and every manifest file is there at its size: a
    /// check cheap enough for every launch. Hashes are checked when the folder is written.
    /// </summary>
    public SetupStatus Status()
    {
        if (ReadMarker() == manifest.Version && manifest.Files.All(entry =>
                Path.Combine(Directory, entry.Path) is var path && File.Exists(path) && new FileInfo(path).Length == entry.Size))
            return SetupStatus.Done;
        return System.IO.Directory.Exists(Partial) || System.IO.Directory.Exists(Directory) ? SetupStatus.Partial : SetupStatus.NeedsSetup;
    }

    /// <summary>
    /// Starts or resumes writing: a folder that is not done becomes the partial folder, whose
    /// files that verify are kept and everything else deleted. Returns the manifest entries still to write.
    /// </summary>
    public List<ManifestEntry> Resume(Reporter reporter, CancellationToken cancel)
    {
        if (System.IO.Directory.Exists(Directory))
        {
            // The cache's own folder, so a stale copy is resumed from, or dropped for a newer partial one.
            if (System.IO.Directory.Exists(Partial)) System.IO.Directory.Delete(Directory, recursive: true);
            else System.IO.Directory.Move(Directory, Partial);
        }
        var kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existing = System.IO.Directory.Exists(Partial)
            ? System.IO.Directory.EnumerateFiles(Partial, "*", SearchOption.AllDirectories).ToList() : [];
        if (existing.Count > 0) reporter.Start(SetupStep.CheckingExistingFiles, 0);
        foreach (var path in existing)
        {
            cancel.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(Partial, path).Replace(Path.DirectorySeparatorChar, '/');
            reporter.Report(relative);
            if (manifest.Find(relative) is { } entry && new FileInfo(path).Length == entry.Size
                && FileHashes.Sha256(path, cancel) == entry.Sha256)
                kept.Add(entry.Path);
            else File.Delete(path);
        }
        return manifest.Files.Where(entry => !kept.Contains(entry.Path)).ToList();
    }

    /// <summary>Deletes the partial folder and a folder that is not done; a done folder is left alone.</summary>
    public void DiscardUnfinished()
    {
        if (Status() == SetupStatus.Done) return;
        if (System.IO.Directory.Exists(Partial)) System.IO.Directory.Delete(Partial, recursive: true);
        if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, recursive: true);
    }

    /// <summary>Refuses to start writing <paramref name="pending"/> when the drive lacks the room.</summary>
    public void CheckSpace(IDiskSpace space, IReadOnlyList<ManifestEntry> pending, long spare, string what)
    {
        var needed = pending.Sum(entry => entry.Size) + spare;
        var free = space.FreeBytes(Path.GetDirectoryName(Directory)!);
        if (free < needed)
            throw new SetupException(SetupError.DiskFull,
                $"Not enough disk space for {what}: it needs {needed / 1e9:0.0} GB free on {Path.GetPathRoot(Directory)} " +
                $"and only {free / 1e9:0.0} GB is free. Free some space on that drive, then set up again.");
    }

    /// <summary>
    /// Writes one manifest file into the partial folder from <paramref name="copy"/>, which
    /// passes the bytes block by block, and checks its hash; <paramref name="damaged"/> is
    /// thrown when it differs.
    /// </summary>
    public void Write(ManifestEntry entry, Action<Action<ReadOnlySpan<byte>>> copy, Func<SetupException> damaged)
    {
        var target = SafePath.Inside(Partial, entry.Path) ?? throw new InvalidDataException("A path outside the folder: " + entry.Path);
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temp = target + TempSuffix;
        try
        {
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
            {
                copy(block =>
                {
                    hash.AppendData(block);
                    output.Write(block);
                });
                output.Flush(flushToDisk: true);
                if (Convert.ToHexStringLower(hash.GetHashAndReset()) != entry.Sha256) throw damaged();
            }
            File.Move(temp, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    /// <summary>Marks the partial folder complete and renames it into place.</summary>
    public void Commit()
    {
        System.IO.Directory.CreateDirectory(Partial);
        File.WriteAllText(Path.Combine(Partial, MarkerFileName), MarkerKey + manifest.Version + "\n");
        System.IO.Directory.Move(Partial, Directory);
    }

    private string? ReadMarker()
    {
        try
        {
            var text = File.ReadAllText(Path.Combine(Directory, MarkerFileName)).Trim();
            return text.StartsWith(MarkerKey, StringComparison.Ordinal) ? text[MarkerKey.Length..] : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
