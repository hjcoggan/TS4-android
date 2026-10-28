namespace TopSpin4.Launcher.Core;

/// <summary>
/// The prepared data the runtime is started with: the disc's game partition (mounted as
/// <c>game:</c>, <c>--game_data_root</c>) and TU1's files (mounted as <c>update:</c>,
/// <c>--update_data_root</c>). <see cref="Reused"/> says the cache already held them.
/// </summary>
public sealed record PreparedData(string GameDataRoot, string UpdateDataRoot, bool Reused);

/// <summary>
/// Disc and TU1 preparation: checks that the player's disc image and TU1 package are the
/// Reference build, then extracts the disc and unpacks TU1 into a per-user cache
/// (<see cref="UserDirectories.PreparedDataRoot()"/>). Both inputs are checked before anything
/// is written. A later launch finds the cache complete with a cheap check and needs neither
/// input; a preparation that stopped part way never counts as complete and is resumed.
/// </summary>
public sealed class Preparation(string cacheRoot, FileManifest disc, FileManifest titleUpdate, IDiskSpace space, TimeProvider time)
{
    public const string GameDataFolderName = "gamedata";
    public const string UpdateDataFolderName = "updatedata";

    /// <summary>Head room kept free beyond the prepared files (logs, caches, saves).</summary>
    public const long SpareBytes = 512L << 20;

    private const double Gigabyte = 1 << 30;

    /// <summary>
    /// The shader and pipeline cache the game keeps beside the prepared folders
    /// (<c>CacheFolder</c> in src/user_paths.h; keep the names equal). A move carries it along.
    /// </summary>
    public const string ShaderCacheFolderName = "cache";

    private static string Normalised(string root) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

    private string _cacheRoot = Normalised(cacheRoot);
    private PreparedFolder _gameData = new(Path.Combine(Normalised(cacheRoot), GameDataFolderName), disc);
    private PreparedFolder _updateData = new(Path.Combine(Normalised(cacheRoot), UpdateDataFolderName), titleUpdate);

    /// <summary>Preparation of the Reference build into <paramref name="cacheRoot"/>.</summary>
    public Preparation(string cacheRoot)
        : this(cacheRoot, FileManifest.Disc, FileManifest.TitleUpdate, new DriveDiskSpace(), TimeProvider.System)
    {
    }

    /// <summary>The folder that holds the prepared folders: where the game files go.</summary>
    public string CacheRoot => _cacheRoot;

    /// <summary>A preparation is running; the place must not change meanwhile.</summary>
    public bool Busy { get; private set; }

    public string GameDataRoot => _gameData.Directory;
    public string UpdateDataRoot => _updateData.Directory;

    /// <summary>The disc image is still needed: the cache lacks the game data.</summary>
    public bool NeedsDisc => _gameData.Status() != SetupStatus.Done;

    /// <summary>The TU1 package is still needed: the cache lacks the update data.</summary>
    public bool NeedsTitleUpdate => _updateData.Status() != SetupStatus.Done;

    /// <summary>The bytes the disc's game partition comes to; the weight of the extract step.</summary>
    public long DiscBytes => disc.TotalBytes;

    /// <summary>The bytes TU1's files come to; the weight of the unpack step.</summary>
    public long TitleUpdateBytes => titleUpdate.TotalBytes;

    /// <summary>An earlier preparation stopped part way and left files that the next one resumes from.</summary>
    public bool HasPartial => _gameData.Status() == SetupStatus.Partial || _updateData.Status() == SetupStatus.Partial;

    /// <summary>Removes what an unfinished preparation left, so the next one starts from nothing. Finished folders stay.</summary>
    public void DiscardPartial()
    {
        _gameData.DiscardUnfinished();
        _updateData.DiscardUnfinished();
    }

    /// <summary>Checks the file the player picked for the disc row: a supported disc image, or the reason it can't be used.</summary>
    public InputCheck CheckDisc(string? path, CancellationToken cancel) => InputCheck.Run(() =>
    {
        if (!string.IsNullOrEmpty(path) && !path.EndsWith(".iso", StringComparison.OrdinalIgnoreCase))
            throw new SetupException(SetupError.NotAnXboxImage, $"Not a disc image (.iso): {path}", null,
                "This is not a disc image. Choose the .iso file you made from your Top Spin 4 disc.", Path.GetFileName(path));
        using var image = DiscSetup.Open(path);
        new DiscSetup(disc, space).Check(image, cancel);
        return "Top Spin 4 disc, supported edition";
    }, cancel, "The disc image could not be read");

    /// <summary>Checks the file the player picked for the Title Update 1 row.</summary>
    public InputCheck CheckTitleUpdate(string? path, CancellationToken cancel) => InputCheck.Run(() =>
    {
        using var package = TitleUpdateSetup.Open(path);
        new TitleUpdateSetup(titleUpdate, space).Check(package, cancel);
        return "Title Update 1 for Top Spin 4";
    }, cancel, "The file could not be read");

    /// <summary>The prepared data when the cache holds all of it, else null. Reads no input.</summary>
    public PreparedData? Existing() =>
        _gameData.Status() == SetupStatus.Done && _updateData.Status() == SetupStatus.Done
            ? new PreparedData(GameDataRoot, UpdateDataRoot, Reused: true)
            : null;

    /// <summary>
    /// What setup still has to write, with <see cref="SpareBytes"/> kept free beyond it; zero when
    /// the cache holds everything. Files an unfinished preparation left count as written.
    /// </summary>
    public long NeededBytes =>
        Existing() is not null ? 0 : _gameData.PendingBytes() + _updateData.PendingBytes() + SpareBytes;

    /// <summary>Free space on the drive that holds <see cref="CacheRoot"/>.</summary>
    public long FreeBytes => space.FreeBytes(_cacheRoot);

    /// <summary>The drive holds what setup still has to write.</summary>
    public bool HasRoomForSetup => FreeBytes >= NeededBytes;

    /// <summary>
    /// Points the preparation at another folder, for a place chosen before setup or a finished
    /// move. Files are neither moved nor deleted here.
    /// </summary>
    public void Relocate(string newRoot)
    {
        if (Busy) throw new InvalidOperationException("The game files cannot change place while setup is running.");
        _cacheRoot = Normalised(newRoot);
        _gameData = new PreparedFolder(Path.Combine(_cacheRoot, GameDataFolderName), disc);
        _updateData = new PreparedFolder(Path.Combine(_cacheRoot, UpdateDataFolderName), titleUpdate);
    }

    /// <summary>
    /// Moves the prepared folders to <paramref name="newRoot"/>: copies each file there with its
    /// hash checked against the manifest, marks the new folders complete, calls
    /// <paramref name="record"/> with the new place, switches to it and only then removes the old
    /// copy. A move that stops or fails before that leaves the old copy in use, untouched; what was
    /// copied stays as a partial folder that the next move resumes from. Returns false when the
    /// move is done but part of the old copy could not be removed.
    /// </summary>
    public bool Move(string newRoot, Action<string> record, IProgress<SetupProgress>? progress, CancellationToken cancel)
    {
        if (Existing() is null) throw new InvalidOperationException("There are no prepared game files to move.");
        if (Busy) throw new InvalidOperationException("The game files cannot change place while setup is running.");
        var target = Normalised(newRoot);
        if (string.Equals(target, _cacheRoot, StringComparison.OrdinalIgnoreCase))
            throw new SetupException(SetupError.BadPlace, "The game files are already in that folder.");
        if (IsInside(target, _gameData.Directory) || IsInside(target, _updateData.Directory)
            || IsInside(target, Path.Combine(_cacheRoot, ShaderCacheFolderName)))
            throw new SetupException(SetupError.BadPlace,
                "That place is inside the current game files. Choose a folder of its own.");

        Busy = true;
        try
        {
            var reporter = new Reporter(progress, time);
            var total = disc.TotalBytes + titleUpdate.TotalBytes;
            var gameData = new PreparedFolder(Path.Combine(target, GameDataFolderName), disc);
            var updateData = new PreparedFolder(Path.Combine(target, UpdateDataFolderName), titleUpdate);
            // Both folders must fit before the first byte is copied, so a full drive never leaves half a move behind.
            var needed = gameData.PendingBytes() + updateData.PendingBytes() + SpareBytes;
            var free = space.FreeBytes(target);
            if (free < needed)
                throw new SetupException(SetupError.DiskFull,
                    $"Not enough free space on {Path.GetPathRoot(target)}: the move needs about {needed / Gigabyte:0.#} GB " +
                    $"and {free / Gigabyte:0.#} GB is free. Free some space or choose another place.");
            try
            {
                var copied = Copy(_gameData, gameData, disc, "the game files", reporter, total, 0, cancel);
                Copy(_updateData, updateData, titleUpdate, "Title Update 1", reporter, total, copied, cancel);
                CopyShaderCache(_cacheRoot, target, cancel);
            }
            catch (UnauthorizedAccessException error)
            {
                throw new SetupException(SetupError.NoPermission,
                    $"Not allowed to write to {target}. Choose a place for the game files that you can write to.", error);
            }

            var oldFolders = new[] { _gameData.Directory, _gameData.Partial, _updateData.Directory, _updateData.Partial,
                Path.Combine(_cacheRoot, ShaderCacheFolderName) };
            record(target);
            _cacheRoot = target;
            _gameData = gameData;
            _updateData = updateData;
            reporter.Start(SetupStep.Done, 1, 1);
            return oldFolders.Select(Remove).ToList().All(removed => removed);
        }
        finally
        {
            Busy = false;
        }
    }

    private static bool IsInside(string path, string folder) =>
        path.StartsWith(Path.TrimEndingDirectorySeparator(folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    // Copies one prepared folder file by file, each verified as it is written; returns the bytes done so far.
    private long Copy(PreparedFolder from, PreparedFolder to, FileManifest manifest, string what, Reporter reporter,
        long total, long copied, CancellationToken cancel)
    {
        if (to.Status() != SetupStatus.Done)
        {
            var pending = to.Resume(reporter, cancel);
            to.CheckSpace(space, pending, SpareBytes, what);
            reporter.Start(SetupStep.MovingFiles, total, copied + manifest.TotalBytes - pending.Sum(entry => entry.Size));
            foreach (var entry in pending)
            {
                cancel.ThrowIfCancellationRequested();
                var source = SafePath.Inside(from.Directory, entry.Path) ?? throw new InvalidDataException("A path outside the folder: " + entry.Path);
                if (!File.Exists(source))
                    throw new SetupException(SetupError.DamagedFile, $"{entry.Path} is missing from the current game files. Set the game up again.");
                reporter.Report(entry.Path, force: true);
                to.Write(entry, sink =>
                {
                    using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20);
                    var buffer = new byte[1 << 20];
                    int read;
                    while ((read = input.Read(buffer)) > 0)
                    {
                        sink(buffer.AsSpan(0, read));
                        reporter.Advance(read, entry.Path);
                        cancel.ThrowIfCancellationRequested();
                    }
                }, () => new SetupException(SetupError.DamagedFile,
                    $"{entry.Path} in the current game files is damaged, so the move was stopped. Set the game up again."));
            }
            to.Commit();
        }
        return copied + manifest.TotalBytes;
    }

    // The game's shader cache, carried to the new place so the next start does not rebuild it. It can be built
    // again, so a missing one is fine and one that cannot be copied is skipped; only a stop is passed on.
    // It lands under a temporary name and is renamed whole, so the game never reads half a cache.
    private static void CopyShaderCache(string fromRoot, string toRoot, CancellationToken cancel)
    {
        var from = Path.Combine(fromRoot, ShaderCacheFolderName);
        if (!Directory.Exists(from)) return;
        var to = Path.Combine(toRoot, ShaderCacheFolderName);
        var partial = to + ".partial";
        try
        {
            if (Directory.Exists(partial)) Directory.Delete(partial, recursive: true);
            foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            {
                cancel.ThrowIfCancellationRequested();
                var target = Path.Combine(partial, Path.GetRelativePath(from, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);
            }
            Directory.CreateDirectory(partial);
            if (Directory.Exists(to)) Directory.Delete(to, recursive: true);
            Directory.Move(partial, to);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Remove(partial);
        }
    }

    // Removes a folder; false when something stayed behind.
    private static bool Remove(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Prepares whatever the cache lacks. An input the cache already holds may be null; the
    /// other is required and is checked before anything is written.
    /// </summary>
    public PreparedData Prepare(string? discImage, string? titleUpdatePackage, IProgress<SetupProgress>? progress,
        CancellationToken cancel)
    {
        if (Existing() is { } existing) return existing;
        Busy = true;
        try
        {
            return PrepareCore(discImage, titleUpdatePackage, progress, cancel);
        }
        finally
        {
            Busy = false;
        }
    }

    private PreparedData PrepareCore(string? discImage, string? titleUpdatePackage, IProgress<SetupProgress>? progress,
        CancellationToken cancel)
    {
        var reporter = new Reporter(progress, time);
        var needDisc = NeedsDisc;
        var needUpdate = NeedsTitleUpdate;

        if (needDisc) reporter.Start(SetupStep.CheckingDisc, 0);
        using var image = needDisc ? DiscSetup.Open(discImage) : null;
        var discSetup = new DiscSetup(disc, space);
        var files = image is null ? null : discSetup.Check(image, cancel);

        if (needUpdate) reporter.Start(SetupStep.CheckingTitleUpdate, 0);
        using var package = needUpdate ? TitleUpdateSetup.Open(titleUpdatePackage) : null;
        var updateSetup = new TitleUpdateSetup(titleUpdate, space);
        if (package is not null) updateSetup.Check(package, cancel);

        try
        {
            Directory.CreateDirectory(_cacheRoot);
            if (image is not null) discSetup.Extract(image, files!, _gameData, reporter, cancel);
            if (package is not null) updateSetup.Unpack(package, _updateData, reporter, cancel);
        }
        catch (UnauthorizedAccessException error)
        {
            throw new SetupException(SetupError.NoPermission,
                $"Setup is not allowed to write to {_cacheRoot}. Check that the folder can be written to, then set up again.", error);
        }
        reporter.Start(SetupStep.Done, 1, 1);
        return new PreparedData(GameDataRoot, UpdateDataRoot, Reused: false);
    }
}
