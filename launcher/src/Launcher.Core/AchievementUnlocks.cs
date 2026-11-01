using System.Globalization;

namespace TopSpin4.Launcher.Core;

/// <summary>
/// The achievements the player has earned, as the game records them: <c>userdata\achievements\54540859.toml</c>, which the
/// game's runtime (ReXGlue's AchievementManager) writes as one <c>[unlocked.&lt;id&gt;]</c> table per achievement holding
/// <c>filetime = &lt;Windows FILETIME&gt;</c>. Only that shape is read; a file that is missing or unreadable means nothing is
/// earned yet, never an error.
/// </summary>
public sealed class AchievementUnlocks
{
    private const string Section = "[unlocked.";

    private readonly Dictionary<int, DateTime?> _unlocked;

    private AchievementUnlocks(Dictionary<int, DateTime?> unlocked, string stamp)
    {
        _unlocked = unlocked;
        Stamp = stamp;
    }

    public static AchievementUnlocks None { get; } = new([], "");

    /// <summary>The game's unlock file inside a user data folder.</summary>
    public static string PathIn(string userDataRoot) =>
        Path.Combine(userDataRoot, "achievements", $"{ReferenceBuild.TitleId:X8}.toml");

    /// <summary>The file's text; changes whenever an achievement is earned, so the window can tell when to redraw.</summary>
    public string Stamp { get; }

    public int Count => _unlocked.Count;

    public static AchievementUnlocks Read(string path)
    {
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : None;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return None;
        }
    }

    public static AchievementUnlocks Parse(string text)
    {
        var unlocked = new Dictionary<int, DateTime?>();
        int? current = null;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                current = line.StartsWith(Section, StringComparison.Ordinal) && line.EndsWith(']')
                    && int.TryParse(line[Section.Length..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
                if (current is { } earned) unlocked.TryAdd(earned, null);
                continue;
            }
            if (current is not { } owner) continue;
            var cut = line.IndexOf('=');
            if (cut <= 0 || line[..cut].Trim() != "filetime") continue;
            if (long.TryParse(line[(cut + 1)..].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var filetime) && filetime > 0
                && filetime <= DateTime.MaxValue.ToFileTimeUtc())
                unlocked[owner] = DateTime.FromFileTimeUtc(filetime);
        }
        return new AchievementUnlocks(unlocked, text);
    }

    public bool IsUnlocked(int id) => _unlocked.ContainsKey(id);

    /// <summary>When the achievement was earned (UTC), or null when it is locked or the file gives no time.</summary>
    public DateTime? UnlockedAt(int id) => _unlocked.GetValueOrDefault(id);
}
