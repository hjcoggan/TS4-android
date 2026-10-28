namespace TopSpin4.Launcher.Core;

/// <summary>The places the Launcher draws a picture from the player's disc.</summary>
public enum ThemeImage
{
    /// <summary>Behind the Home page.</summary>
    HomeBackground,
    /// <summary>Behind Setup and Settings.</summary>
    SetupBackground,
    /// <summary>The Top Spin 4 logo in the header, in place of the title text.</summary>
    Logo,
    /// <summary>The card beside PLAY.</summary>
    BoxArt,
    /// <summary>The window and taskbar icon.</summary>
    WindowIcon,
}

/// <summary>
/// Where one image of the look comes from: a file in the art folder, or (when <see cref="ArtPath"/> is null) the
/// original look's own resource, which is the title text for the logo, a plain base colour for a background, the
/// Launcher's own window icon, and no card beside PLAY.
/// </summary>
public readonly record struct ThemeSource(string? ArtPath)
{
    public bool IsArt => ArtPath is not null;
}

/// <summary>
/// The Launcher's look: which image resources point at the player's art and which fall back to the original look's
/// (<see cref="ThemeSource.IsArt"/>), the colours that go with them, and the credit line for the footer. The window
/// draws it; nothing here reads or holds any picture, so the Launcher itself contains no game art (ADR-0006).
/// </summary>
public sealed class LauncherTheme
{
    private readonly Dictionary<ThemeImage, ThemeSource> _sources;

    private LauncherTheme(Dictionary<ThemeImage, ThemeSource> sources, ThemePalette palette, string creditLine)
    {
        _sources = sources;
        Palette = palette;
        CreditLine = creditLine;
    }

    /// <summary>The accent and surface colours: the art's when any picture from the disc shows, else the original look's.</summary>
    public ThemePalette Palette { get; }

    /// <summary>The small footer line: what the Launcher is, that it is not 2K's, and (when art shows) where the art comes from.</summary>
    public string CreditLine { get; }

    public const string Disclaimer = "A fan recompilation, not affiliated with 2K Sports or 2K Czech.";

    /// <summary>Both draw the same pictures in the same colours, so a window showing one has nothing to redraw for the other.</summary>
    public bool SameLookAs(LauncherTheme other) =>
        Palette == other.Palette && CreditLine == other.CreditLine && Enum.GetValues<ThemeImage>().All(image => Source(image) == other.Source(image));

    public ThemeSource Source(ThemeImage image) => _sources[image];

    /// <summary>The picture behind <paramref name="page"/>; none when the art folder lacks that page's own picture.</summary>
    public ThemeSource BackgroundFor(LauncherPage page) =>
        Source(page == LauncherPage.Home ? ThemeImage.HomeBackground : ThemeImage.SetupBackground);

    /// <summary>The look for an art folder: each image from the folder where it has it, the original look's otherwise.</summary>
    public static LauncherTheme From(DiscArtFolder art)
    {
        ThemeSource Of(DiscArtSlot slot) => new(art.ImagePath(slot));
        var sources = new Dictionary<ThemeImage, ThemeSource>
        {
            [ThemeImage.HomeBackground] = Of(DiscArtSlot.Background),
            [ThemeImage.SetupBackground] = Of(DiscArtSlot.SetupBackground),
            [ThemeImage.Logo] = Of(DiscArtSlot.Logo),
            [ThemeImage.BoxArt] = Of(DiscArtSlot.BoxArt),
            // The .ico is what the window, taskbar and shortcut use; it exists only when the title icon was read.
            [ThemeImage.WindowIcon] = new ThemeSource(art.IconPath),
        };
        // The icon alone does not change the colours: only a picture on the page does.
        var showsArt = new[] { ThemeImage.HomeBackground, ThemeImage.SetupBackground, ThemeImage.Logo, ThemeImage.BoxArt }
            .Any(image => sources[image].IsArt);
        return new LauncherTheme(sources, showsArt ? ThemePalette.Art : ThemePalette.Original,
            showsArt ? Disclaimer + " The pictures are from your own disc." : Disclaimer);
    }

    /// <summary>The original look: no pictures, the Launcher's own colours.</summary>
    public static LauncherTheme Original { get; } = new(
        Enum.GetValues<ThemeImage>().ToDictionary(image => image, _ => new ThemeSource(null)), ThemePalette.Original, Disclaimer);
}
