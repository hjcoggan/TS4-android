using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

/// <summary>What a test can see of an art folder from outside: which slots have an image, and the disc its index names.</summary>
internal static class TestArtFolder
{
    public static IReadOnlyList<DiscArtSlot> Found(string folder)
    {
        var art = DiscArtFolder.Open(folder);
        return Enum.GetValues<DiscArtSlot>().Where(slot => art.ImagePath(slot) is not null).ToList();
    }

    public static string? Source(string folder)
    {
        var index = Path.Combine(folder, DiscArtFolder.IndexFileName);
        return File.Exists(index) ? File.ReadLines(index).FirstOrDefault(line => line.StartsWith("source=", StringComparison.Ordinal))?["source=".Length..] : null;
    }

    /// <summary>The names of the files in the folder, sorted.</summary>
    public static string[] Files(string folder) =>
        Directory.Exists(folder) ? Directory.GetFiles(folder).Select(file => Path.GetFileName(file)).OrderBy(name => name, StringComparer.Ordinal).ToArray() : [];
}
