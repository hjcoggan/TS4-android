namespace TopSpin4.Launcher.Core;

/// <summary>
/// Where one picture of the look comes from: a file in the art folder, or (when <see cref="ArtPath"/> is null) the
/// original look's own resource, which is the title text for the logo, a plain base colour for a background, the
/// Launcher's own window icon, and no card beside PLAY.
/// </summary>
public readonly record struct ThemeSource(string? ArtPath)
{
    public bool IsArt => ArtPath is not null;
}

/// <summary>What one page draws: its background, whether the background is quietened, and its header.</summary>
/// <param name="Background">The picture behind the page; none when the art folder lacks that page's own picture.</param>
/// <param name="DimsBackground">The picture is quietened (Setup and Settings); there is no text on Home's, which sits under the scrim.</param>
/// <param name="ShowsLogo">The header is the logo; otherwise it is the title text.</param>
/// <param name="LogoHeight">How tall the logo is drawn: large on Home, small elsewhere.</param>
public readonly record struct PageLook(ThemeSource Background, bool DimsBackground, bool ShowsLogo, double LogoHeight);

/// <summary>
/// The Launcher's look, decided in one place: which pictures come from the player's art and which fall back to the
/// original look's own (<see cref="ThemeSource.IsArt"/>), the colours that go with them, what each page draws, and the
/// credit line for the footer. The window only applies it; nothing here reads or holds any picture, so the Launcher
/// itself contains no game art (ADR-0006). Pictures are named by Disc art slot.
/// </summary>
public sealed class LauncherTheme
{
    public const string Disclaimer = "A fan recompilation, not affiliated with 2K Sports or 2K Czech.";

    private readonly Dictionary<DiscArtSlot, ThemeSource> _sources;

    private LauncherTheme(Dictionary<DiscArtSlot, ThemeSource> sources, ThemePalette palette, string creditLine, string artStamp)
    {
        _sources = sources;
        Palette = palette;
        CreditLine = creditLine;
        ArtStamp = artStamp;
    }

    /// <summary>The accent and surface colours: the art's when any picture from the disc shows, else the original look's.</summary>
    public ThemePalette Palette { get; }

    /// <summary>The small footer line: what the Launcher is, that it is not 2K's, and (when art shows) where the art comes from.</summary>
    public string CreditLine { get; }

    /// <summary>The <see cref="DiscArtFolder.Stamp"/> of the art folder this look was made from; the window reloads when the folder's stamp is not this.</summary>
    public string ArtStamp { get; }

    /// <summary>
    /// Both draw the same pictures in the same colours from the same art: the same file names under another disc's art
    /// are not the same look.
    /// </summary>
    public bool SameLookAs(LauncherTheme other) =>
        ArtStamp == other.ArtStamp && Palette == other.Palette && CreditLine == other.CreditLine
        && Enum.GetValues<DiscArtSlot>().All(slot => Source(slot) == other.Source(slot));

    /// <summary>
    /// Where a slot's picture comes from. For <see cref="DiscArtSlot.TitleIcon"/> it is the art folder's <c>.ico</c>
    /// (the window, taskbar and shortcut icon), which exists only when the title icon was read.
    /// </summary>
    public ThemeSource Source(DiscArtSlot slot) => _sources[slot];

    /// <summary>What <paramref name="page"/> draws.</summary>
    public PageLook LookFor(LauncherPage page)
    {
        var home = page == LauncherPage.Home;
        var background = Source(home ? DiscArtSlot.Background : DiscArtSlot.SetupBackground);
        return new PageLook(background, DimsBackground: background.IsArt && !home, ShowsLogo: Source(DiscArtSlot.Logo).IsArt, LogoHeight: home ? 110 : 52);
    }

    /// <summary>
    /// The look for an art folder: each picture from the folder where it has it, the original look's otherwise. A picture
    /// that <paramref name="canDraw"/> refuses (the window could not decode it) counts as missing, and the colours follow
    /// what is left: with no picture on a page at all, the original palette.
    /// </summary>
    /// <param name="canDraw">Asked once for each file the look would use, with its slot and path. Null accepts every file.</param>
    public static LauncherTheme From(DiscArtFolder art, Func<DiscArtSlot, string, bool>? canDraw = null)
    {
        var sources = new Dictionary<DiscArtSlot, ThemeSource>();
        foreach (var slot in Enum.GetValues<DiscArtSlot>())
        {
            var path = slot == DiscArtSlot.TitleIcon ? art.IconPath : art.ImagePath(slot);
            sources[slot] = path is not null && (canDraw?.Invoke(slot, path) ?? true) ? new ThemeSource(path) : new ThemeSource(null);
        }
        // The icon alone does not change the colours: only a picture on the page does.
        var showsArt = Enum.GetValues<DiscArtSlot>().Any(slot => slot != DiscArtSlot.TitleIcon && sources[slot].IsArt);
        return new LauncherTheme(sources, showsArt ? ThemePalette.Art : ThemePalette.Original,
            showsArt ? Disclaimer + " The pictures are from your own disc." : Disclaimer, art.Stamp);
    }

    /// <summary>The original look: no pictures, the Launcher's own colours.</summary>
    public static LauncherTheme Original { get; } = new(
        Enum.GetValues<DiscArtSlot>().ToDictionary(slot => slot, _ => new ThemeSource(null)), ThemePalette.Original, Disclaimer, "");
}
