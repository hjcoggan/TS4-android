namespace TopSpin4.Launcher.Core;

/// <summary>
/// What the home screen's card row shows: the players, venues and achievements the art folder holds (each card only when
/// its collection is not empty) and the Launcher's own release notes, which always show.
/// </summary>
public sealed record Showcase(IReadOnlyList<DiscArtEntry> Players, IReadOnlyList<DiscArtEntry> Venues, IReadOnlyList<DiscArtEntry> Achievements)
{
    /// <summary>The release notes of this version: the project's own words.</summary>
    public static IReadOnlyList<string> WhatsNew { get; } =
    [
        "Setup prepares the shader pack and the Native renderer's pipelines, so matches start without shader stutter.",
        "Shaders the game meets while you play are added to the pack when the session ends.",
        "The Launcher shows the players, venues and achievements from your own disc.",
    ];

    public static Showcase Empty { get; } = new([], [], []);

    public static Showcase From(DiscArtFolder art) =>
        new(art.Entries(DiscArtCollection.Players), art.Entries(DiscArtCollection.Venues), art.Entries(DiscArtCollection.Achievements));

    /// <summary>"40 achievements, 1,000 G" for the achievements card; empty without achievements.</summary>
    public string AchievementsSummary => Achievements.Count == 0 ? ""
        : string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{Achievements.Count} achievement{(Achievements.Count == 1 ? "" : "s")}, {Achievements.Sum(entry => entry.Points):N0} G");
}
