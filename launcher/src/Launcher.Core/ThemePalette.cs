namespace TopSpin4.Launcher.Core;

/// <summary>
/// The colours of one look, as <c>#RRGGBB</c> (or <c>#AARRGGBB</c> where there is transparency). The window sets its
/// brushes from these (<see cref="Brushes"/>); <see cref="Original"/> is the Launcher's own look (ball yellow and court
/// blue on navy, no art) and <see cref="Art"/> is retuned to the disc's art (red, white and silver on near-black).
/// </summary>
/// <param name="Base">The window behind everything.</param>
/// <param name="Scrim">
/// The panel under text when a picture is behind the page: dark and mostly opaque, so text never sits on the picture.
/// Fully transparent in the original look, which has no picture.
/// </param>
/// <param name="Dim">Laid over a picture to quieten it where no text is (Setup and Settings); transparent when none.</param>
/// <param name="Panel">A card on a page (a setup row, the trouble-starting box).</param>
/// <param name="Accent">The big button's fill, and the logo-less title.</param>
/// <param name="AccentHot">The big button's fill under the pointer.</param>
/// <param name="AccentText">The accent as text on a panel (the setup row numbers, the title).</param>
/// <param name="Secondary">Fill of a secondary button under the pointer, and of the attention button.</param>
/// <param name="Silver">Kickers and links.</param>
/// <param name="OnAccent">Text on the big button and on a hot secondary button.</param>
/// <param name="AccentDisabled">Fill of the big button when it cannot be pressed.</param>
/// <param name="OnAccentDisabled">Text on the big button when it cannot be pressed.</param>
/// <param name="SecondaryFill">Fill of a secondary button at rest.</param>
/// <param name="SecondaryBorder">Border of a secondary button.</param>
public sealed record ThemePalette(
    string Base, string Scrim, string Dim, string Panel,
    string Accent, string AccentHot, string AccentText, string Secondary, string Silver,
    string Text, string Muted, string Error, string OnAccent, string AccentDisabled, string OnAccentDisabled,
    string SecondaryFill, string SecondaryBorder)
{
    /// <summary>The Launcher's own look.</summary>
    public static ThemePalette Original { get; } = new(
        Base: "#0A1220", Scrim: "#00000000", Dim: "#00000000", Panel: "#142038",
        Accent: "#D7E44A", AccentHot: "#EAF56E", AccentText: "#D7E44A", Secondary: "#2A6FDB", Silver: "#8CB4FF",
        Text: "#E6ECFF", Muted: "#9AA8D0", Error: "#FF8A7A", OnAccent: "#0A1220", AccentDisabled: "#4A4E2A", OnAccentDisabled: "#2A2A2A",
        SecondaryFill: "#33203055", SecondaryBorder: "#4A6BC4");

    /// <summary>The look retuned to the disc's art: a deep navy-black base, the red of the logo's "4", white and silver.</summary>
    public static ThemePalette Art { get; } = new(
        Base: "#080B14", Scrim: "#DB080B14", Dim: "#B3080B14", Panel: "#141A28",
        Accent: "#C8202A", AccentHot: "#B01B24", AccentText: "#FF6B72", Secondary: "#8F1A22", Silver: "#D6D9E0",
        Text: "#F2F4F8", Muted: "#B9C0CF", Error: "#FF9C8F", OnAccent: "#FFFFFF", AccentDisabled: "#2E2A36", OnAccentDisabled: "#C9C4CE",
        SecondaryFill: "#1FFFFFFF", SecondaryBorder: "#6B7690");

    /// <summary>
    /// The brushes <c>Theme.axaml</c> declares, by resource key, with this palette's colour for each. The window sets each
    /// brush's colour in place, so every control that uses it follows.
    /// </summary>
    public IReadOnlyList<(string Key, string Colour)> Brushes() =>
    [
        ("BaseBrush", Base), ("ScrimBrush", Scrim), ("DimBrush", Dim), ("PanelBrush", Panel),
        ("AccentBrush", Accent), ("AccentHotBrush", AccentHot), ("AccentTextBrush", AccentText),
        ("SecondaryBrush", Secondary), ("SilverBrush", Silver),
        ("TextBrush", Text), ("MutedBrush", Muted), ("ErrorBrush", Error),
        ("OnAccentBrush", OnAccent), ("AccentDisabledBrush", AccentDisabled), ("OnAccentDisabledBrush", OnAccentDisabled),
        ("SecondaryFillBrush", SecondaryFill), ("SecondaryBorderBrush", SecondaryBorder),
    ];
}
