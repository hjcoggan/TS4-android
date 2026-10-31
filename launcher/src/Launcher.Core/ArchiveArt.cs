using System.Text.RegularExpressions;

namespace TopSpin4.Launcher.Core;

/// <summary>
/// The player and venue pictures in the game's <c>Global.arc</c> (docs/research/disc-art.md, section 6): each playable
/// pro's cut-out head and shoulders (<c>&lt;Name&gt;_Picture</c>, else the square <c>&lt;Name&gt;_Portrait</c>) and each
/// venue's photo (<c>&lt;Venue&gt;_Preview</c>, cropped to the photo inside its transparent border). Textures are found by
/// their names inside the XPR2 containers; the playable pros are the names of the <c>Baked_&lt;Name&gt;&lt;n&gt;.arc</c>
/// archives. Pictures are decoded to PNG. Names are the texture names spelled out ("RolandGarros" is "Roland Garros").
/// </summary>
internal static partial class ArchiveArt
{
    public const string Archive = "Global.arc";

    private const string Picture = "_Picture";
    private const string Portrait = "_Portrait";
    private const string Preview = "_Preview";
    private const long MaxEntry = 4 << 20;

    // A venue preview is 1024x512 with the photo in this rectangle and a transparent border round it.
    private const int PreviewWidth = 1024;
    private const int PreviewHeight = 512;
    private static readonly (int X, int Y, int Width, int Height) PreviewPhoto = (130, 65, 764, 382);

    private static readonly HashSet<string> NotPlayers = new(StringComparer.OrdinalIgnoreCase) { "PlayerAcademy" };
    private static readonly HashSet<string> NotVenues = new(StringComparer.OrdinalIgnoreCase) { "Placeholder" };

    [GeneratedRegex(@"^Baked_([A-Za-z]+?)\d*\.arc$", RegexOptions.IgnoreCase)]
    private static partial Regex BakedArchive();

    [GeneratedRegex(@"(?<=[a-z])(?=[A-Z0-9])")]
    private static partial Regex WordBreak();

    /// <summary>The playable pros: the names of the disc's <c>Baked_&lt;Name&gt;&lt;n&gt;.arc</c> archives, sorted.</summary>
    public static IReadOnlyList<string> Roster(IEnumerable<string> topLevelFiles) =>
    [
        .. topLevelFiles.Select(file => BakedArchive().Match(file)).Where(match => match.Success)
            .Select(match => match.Groups[1].Value).Where(name => !NotPlayers.Contains(name))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>"RolandGarros" as "Roland Garros".</summary>
    public static string Spell(string name) => WordBreak().Replace(name, " ");

    /// <summary>
    /// The players' and venues' pictures from <paramref name="archive"/>. Throws <see cref="InvalidDataException"/> (or another
    /// <see cref="GameArchive.IsDamage"/> exception) when the archive is damaged; a texture that does not decode is passed to
    /// <paramref name="problem"/> and left out.
    /// </summary>
    public static (IReadOnlyList<DiscArtItem> Players, IReadOnlyList<DiscArtItem> Venues) Read(Stream archive, IReadOnlyList<string> roster,
        Action<string> problem)
    {
        var table = GameArchive.Open(archive);
        var players = new Dictionary<string, RgbaImage>(StringComparer.OrdinalIgnoreCase);
        var portraits = new Dictionary<string, RgbaImage>(StringComparer.OrdinalIgnoreCase);
        var venues = new SortedDictionary<string, RgbaImage>(StringComparer.OrdinalIgnoreCase);
        var pros = roster.ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool Wanted(string name) =>
            Pro(name, Picture) is { } a ? !players.ContainsKey(a)
            : Pro(name, Portrait) is { } b ? !portraits.ContainsKey(b)
            : Venue(name) is { } v && !venues.ContainsKey(v);
        string? Pro(string name, string suffix) =>
            name.EndsWith(suffix, StringComparison.Ordinal) && pros.Contains(name[..^suffix.Length]) ? name[..^suffix.Length] : null;
        static string? Venue(string name) =>
            name.EndsWith(Preview, StringComparison.Ordinal) && name.Length > Preview.Length && !NotVenues.Contains(name[..^Preview.Length])
                ? name[..^Preview.Length] : null;

        foreach (var entry in table.Entries)
        {
            var data = GameArchive.Read(archive, entry, start => XprTexture.Names(start).Any(Wanted), MaxEntry);
            if (data is null) continue;
            foreach (var texture in XprTexture.Parse(data))
            {
                if (!Wanted(texture.Name)) continue;
                RgbaImage image;
                try
                {
                    image = texture.Decode();
                }
                catch (Exception error) when (error is InvalidDataException or NotSupportedException)
                {
                    problem($"{Archive}: {texture.Name} was not used: {error.Message}");
                    continue;
                }
                if (Pro(texture.Name, Picture) is { } pro) players[pro] = image;
                else if (Pro(texture.Name, Portrait) is { } face) portraits[face] = image;
                else if (Venue(texture.Name) is { } venue)
                    venues[venue] = image.Width == PreviewWidth && image.Height == PreviewHeight
                        ? image.Crop(PreviewPhoto.X, PreviewPhoto.Y, PreviewPhoto.Width, PreviewPhoto.Height) : image;
            }
        }

        var playerItems = roster
            .Select(name => (name, image: players.GetValueOrDefault(name) ?? portraits.GetValueOrDefault(name)))
            .Where(pair => pair.image is not null)
            .Select(pair => new DiscArtItem(PngWriter.Encode(pair.image!), Spell(pair.name), ""))
            .ToList();
        var venueItems = venues.Select(pair => new DiscArtItem(PngWriter.Encode(pair.Value), Spell(pair.Key), "")).ToList();
        return (playerItems, venueItems);
    }
}
