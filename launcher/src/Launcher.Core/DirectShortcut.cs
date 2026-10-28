namespace TopSpin4.Launcher.Core;

/// <summary>
/// Writes one shortcut file (.lnk). It sits behind an interface so tests see the target and the
/// arguments without touching the real Desktop or Start menu. Failures are <see cref="IOException"/>s.
/// </summary>
public interface IShortcutWriter
{
    void Write(string linkPath, string target, string arguments, string workingDirectory);
}

/// <summary>
/// The Direct shortcut: "Top Spin 4" on the Desktop and in the Start menu, running the game exe
/// with no arguments from its own folder, so the game reads the saved Settings file and finds its
/// prepared data itself (src/user_paths.h), or opens the Launcher when setup is missing.
/// </summary>
public sealed class DirectShortcut(IShortcutWriter writer, IEnumerable<string> folders)
{
    public const string Name = "Top Spin 4";
    public const string FileName = Name + ".lnk";

    private readonly string[] _folders = [.. folders];

    /// <summary>
    /// Creates the shortcut in each folder, replacing the one already there, and returns the files
    /// written. A folder that can't be written to does not stop the others; the failures are
    /// reported together once the rest are done.
    /// </summary>
    public IReadOnlyList<string> Create(string gameExe)
    {
        var target = Path.GetFullPath(gameExe);
        var workingDirectory = Path.GetDirectoryName(target)!;
        List<string> created = [];
        List<string> failures = [];
        foreach (var folder in _folders)
        {
            var link = Path.Combine(folder, FileName);
            // The new shortcut is written beside the old one and moved over it, so a write that fails keeps the old one.
            var staged = Path.Combine(folder, "~" + FileName);
            try
            {
                Directory.CreateDirectory(folder);
                writer.Write(staged, target, "", workingDirectory);
                File.Move(staged, link, overwrite: true);
                created.Add(link);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                failures.Add($"{folder}: {error.Message}");
            }
            finally
            {
                try
                {
                    if (File.Exists(staged)) File.Delete(staged);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // A leftover staging file is harmless; the shortcut itself is what was asked for.
                }
            }
        }
        if (failures.Count > 0)
            throw new IOException($"Could not create the {Name} shortcut in {string.Join("; ", failures)}");
        return created;
    }
}
