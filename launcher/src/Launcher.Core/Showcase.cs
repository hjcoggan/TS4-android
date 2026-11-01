namespace TopSpin4.Launcher.Core;

/// <summary>
/// What the home screen's card row shows: the players, venues and achievements the art folder holds (each card only when
/// its collection is not empty) and the Launcher's own release notes, which always show. <paramref name="Unlocks"/> says which
/// achievements the player has earned.
/// </summary>
public sealed record Showcase(IReadOnlyList<DiscArtEntry> Players, IReadOnlyList<DiscArtEntry> Venues, IReadOnlyList<DiscArtEntry> Achievements,
    AchievementUnlocks? Unlocks = null)
{
    /// <summary>The release notes of this version: the project's own words.</summary>
    public static IReadOnlyList<string> WhatsNew { get; } =
    [
        "Setup prepares the shader pack and the Native renderer's pipelines, so matches start without shader stutter.",
        "Shaders the game meets while you play are added to the pack when the session ends.",
        "The Launcher shows the players, venues and achievements from your own disc.",
        "Achievements you have earned are marked, and unlocking one in the game shows a console-style notice.",
    ];

    public static Showcase Empty { get; } = new([], [], []);

    public static Showcase From(DiscArtFolder art, AchievementUnlocks? unlocks = null) =>
        new(art.Entries(DiscArtCollection.Players), art.Entries(DiscArtCollection.Venues), art.Entries(DiscArtCollection.Achievements), unlocks);

    private AchievementUnlocks Earned => Unlocks ?? AchievementUnlocks.None;

    public bool IsUnlocked(DiscArtEntry achievement) => Earned.IsUnlocked(achievement.Id);

    public DateTime? UnlockedAt(DiscArtEntry achievement) => Earned.UnlockedAt(achievement.Id);

    /// <summary>As the console's list puts them: the earned ones first, latest first, then the locked ones in the disc's order.</summary>
    public IReadOnlyList<DiscArtEntry> AchievementsInOrder =>
        [.. Achievements.Where(IsUnlocked).OrderByDescending(entry => UnlockedAt(entry) ?? DateTime.MinValue),
            .. Achievements.Where(entry => !IsUnlocked(entry))];

    public int UnlockedCount => Achievements.Count(IsUnlocked);

    public int TotalPoints => Achievements.Sum(entry => entry.Points);

    public int EarnedPoints => Achievements.Where(IsUnlocked).Sum(entry => entry.Points);

    /// <summary>
    /// "40 achievements, 1,000 G" for the achievements card; once one is earned, "3 of 40 achievements, 45 of 1,000 G".
    /// Empty without achievements.
    /// </summary>
    public string AchievementsSummary
    {
        get
        {
            if (Achievements.Count == 0) return "";
            var total = TotalPoints;
            var unlocked = UnlockedCount;
            var plural = Achievements.Count == 1 ? "" : "s";
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            if (unlocked == 0) return string.Create(culture, $"{Achievements.Count} achievement{plural}, {total:N0} G");
            return string.Create(culture, $"{unlocked} of {Achievements.Count} achievement{plural}, {EarnedPoints:N0} of {total:N0} G");
        }
    }
}
