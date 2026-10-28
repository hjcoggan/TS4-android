using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

/// <summary>Text keeps 4.5:1 against what lies under it (WCAG 2 AA), whatever the picture behind the scrim is.</summary>
public sealed class ThemeContrastTests
{
    private const double Aa = 4.5;

    // WCAG's own worked values: black on white is 21:1, white on white 1:1, #767676 on white 4.54:1 (the lightest grey that passes AA).
    [Theory]
    [InlineData("#000000", "#FFFFFF", 21.0)]
    [InlineData("#FFFFFF", "#FFFFFF", 1.0)]
    [InlineData("#767676", "#FFFFFF", 4.54)]
    [InlineData("#FFFFFF", "#767676", 4.54)]
    public void The_ratio_matches_the_WCAG_values(string a, string b, double expected) =>
        Assert.Equal(expected, Contrast.Ratio(a, b), 2);

    [Fact]
    public void A_colour_with_transparency_is_laid_over_what_lies_under_it()
    {
        // Alpha 0x80 is 128/255 of black: white keeps 127/255 of its light.
        Assert.Equal("#7F7F7F", Contrast.Over("#80000000", "#FFFFFF"), ignoreCase: true);
        Assert.Equal("#FFFFFF", Contrast.Over("#00000000", "#FFFFFF"), ignoreCase: true);
        Assert.Equal("#102030", Contrast.Over("#FF102030", "#FFFFFF"), ignoreCase: true);
    }

    public static IEnumerable<object[]> Palettes() => [[nameof(ThemePalette.Original), ThemePalette.Original], [nameof(ThemePalette.Art), ThemePalette.Art]];

    // What a scrim shows as over the worst picture there could be (pure white). The original look has no picture, so its base stands in.
    private static string WorstScrim(ThemePalette p) => Contrast.Alpha(p.Scrim) == 0 ? p.Base : Contrast.Over(p.Scrim, "#FFFFFF");

    // The surfaces text can sit on: the base, a panel, and the scrim over the worst picture.
    private static IEnumerable<(string Name, string Colour)> Surfaces(ThemePalette p) =>
    [
        ("base", p.Base),
        ("panel", p.Panel),
        ("scrim over a white picture", WorstScrim(p)),
    ];

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Text_colours_pass_AA_on_every_surface_text_sits_on(string name, ThemePalette palette)
    {
        // The window's text is kept on the scrim (the main panel) or a panel on it; the base shows under both when no picture does.
        (string Role, string Colour)[] texts =
        [
            ("text", palette.Text), ("muted", palette.Muted), ("silver", palette.Silver),
            ("accent text", palette.AccentText), ("error", palette.Error), ("white", "#FFFFFF"),
        ];
        var failures = new List<string>();
        foreach (var (surface, colour) in Surfaces(palette))
            foreach (var (role, text) in texts)
                if (Contrast.Ratio(text, colour) < Aa)
                    failures.Add($"{name}: {role} {text} on {surface} {colour} is {Contrast.Ratio(text, colour):F2}:1");
        Assert.Empty(failures);
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Button_text_passes_AA_on_its_fill(string name, ThemePalette palette)
    {
        (string What, string Text, string Fill)[] pairs =
        [
            ("big button", palette.OnAccent, palette.Accent),
            ("big button hot", palette.OnAccent, palette.AccentHot),
            ("secondary hot / attention", "#FFFFFF", palette.Secondary),
            ("attention hot", palette.Base, palette.Silver),
            ("secondary at rest", palette.Text, Contrast.Over(palette.SecondaryFill, palette.Panel)),
            ("secondary at rest on the scrim", palette.Text, Contrast.Over(palette.SecondaryFill, WorstScrim(palette))),
        ];
        Assert.All(pairs, pair => Assert.True(Contrast.Ratio(pair.Text, pair.Fill) >= Aa,
            $"{name}: {pair.What}, {pair.Text} on {pair.Fill} is {Contrast.Ratio(pair.Text, pair.Fill):F2}:1"));
    }

    [Fact]
    public void The_art_look_puts_a_dark_mostly_opaque_scrim_under_text_and_the_original_look_has_none()
    {
        Assert.Equal(0, Contrast.Alpha(ThemePalette.Original.Scrim));
        Assert.InRange(Contrast.Alpha(ThemePalette.Art.Scrim), 0.8, 0.95);
    }
}
