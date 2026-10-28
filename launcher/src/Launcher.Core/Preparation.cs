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

    private readonly PreparedFolder _gameData = new(Path.Combine(cacheRoot, GameDataFolderName), disc);
    private readonly PreparedFolder _updateData = new(Path.Combine(cacheRoot, UpdateDataFolderName), titleUpdate);

    /// <summary>Preparation of the Reference build into <paramref name="cacheRoot"/>.</summary>
    public Preparation(string cacheRoot)
        : this(cacheRoot, FileManifest.Disc, FileManifest.TitleUpdate, new DriveDiskSpace(), TimeProvider.System)
    {
    }

    public string GameDataRoot => _gameData.Directory;
    public string UpdateDataRoot => _updateData.Directory;

    /// <summary>The disc image is still needed: the cache lacks the game data.</summary>
    public bool NeedsDisc => _gameData.Status() != SetupStatus.Done;

    /// <summary>The TU1 package is still needed: the cache lacks the update data.</summary>
    public bool NeedsTitleUpdate => _updateData.Status() != SetupStatus.Done;

    /// <summary>The prepared data when the cache holds all of it, else null. Reads no input.</summary>
    public PreparedData? Existing() =>
        _gameData.Status() == SetupStatus.Done && _updateData.Status() == SetupStatus.Done
            ? new PreparedData(GameDataRoot, UpdateDataRoot, Reused: true)
            : null;

    /// <summary>
    /// Prepares whatever the cache lacks. An input the cache already holds may be null; the
    /// other is required and is checked before anything is written.
    /// </summary>
    public PreparedData Prepare(string? discImage, string? titleUpdatePackage, IProgress<SetupProgress>? progress,
        CancellationToken cancel)
    {
        if (Existing() is { } existing) return existing;
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
            Directory.CreateDirectory(cacheRoot);
            if (image is not null) discSetup.Extract(image, files!, _gameData, reporter, cancel);
            if (package is not null) updateSetup.Unpack(package, _updateData, reporter, cancel);
        }
        catch (UnauthorizedAccessException error)
        {
            throw new SetupException(SetupError.NoPermission,
                $"Preparation is not allowed to write to {cacheRoot}. Choose a cache folder you can write to.", error);
        }
        reporter.Start(SetupStep.Done, 1, 1);
        return new PreparedData(GameDataRoot, UpdateDataRoot, Reused: false);
    }
}
