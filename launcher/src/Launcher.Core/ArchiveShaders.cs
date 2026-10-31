
namespace TopSpin4.Launcher.Core;

/// <summary>
/// Reads the game's shader containers straight from the prepared game folder's <c>.arc</c> archives (the Launcher's
/// shader step; the archive layout is <see cref="GameArchive"/>'s). Entries are matched on the container magic, not on
/// names, because the archives' name tables are incomplete. No shader is in a multi-part entry.
/// </summary>
public static class ArchiveShaders
{
    private const string ArchivePattern = "*.arc";

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
                    if (ReadEntry(stream, archive.Entries[i]) is { } container) found.TryAdd(container.Name, container);
                }
                catch (Exception error) when (GameArchive.IsDamage(error))
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

    private sealed record Archive(string Path, string Name, GameArchive Table)
    {
        public IReadOnlyList<GameArchive.Entry> Entries => Table.Entries;

        public static Archive Open(string path, string name)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
            return new Archive(path, name, GameArchive.Open(stream));
        }
    }

    // Only a plausible container is read past its first frame.
    private static ShaderContainer? ReadEntry(FileStream stream, GameArchive.Entry entry)
    {
        var data = GameArchive.Read(stream, entry, start => ShaderContainer.HasMagic(start[..Math.Min(start.Length, 4)]), ShaderContainer.MaxSize);
        return data is not null && ShaderContainer.IsContainer(data) ? ShaderContainer.Of(data) : null;
    }
}
