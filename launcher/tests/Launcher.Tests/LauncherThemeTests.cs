using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

/// <summary>Which resources the Launcher's look uses: the player's art where the art folder has it, the original look's resource where it does not.</summary>
public sealed class LauncherThemeTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ts4-theme-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string ArtFolder => Path.Combine(_root, "art");

    // The art folder the Disc art module writes for a disc that holds these files.
    private LauncherTheme ThemeFor(TestArtDisc disc, params string[] without)
    {
        foreach (var name in without) disc.Files.Remove(name);
        DiscArt.FromFolder(disc.WriteFolder(Path.Combine(_root, "disc")), ArtFolder);
        return LauncherTheme.From(DiscArtFolder.Open(ArtFolder));
    }

    [Fact]
    public void With_a_full_art_folder_every_image_resource_points_at_the_art_file()
    {
        var art = ThemeFor(new TestArtDisc());

        Assert.Equal(Path.Combine(ArtFolder, "background.jpg"), art.Source(DiscArtSlot.Background).ArtPath);
        Assert.Equal(Path.Combine(ArtFolder, "setup-background.png"), art.Source(DiscArtSlot.SetupBackground).ArtPath);
        Assert.Equal(Path.Combine(ArtFolder, "logo.png"), art.Source(DiscArtSlot.Logo).ArtPath);
        Assert.Equal(Path.Combine(ArtFolder, "boxart.jpg"), art.Source(DiscArtSlot.BoxArt).ArtPath);
        Assert.Equal(Path.Combine(ArtFolder, DiscArtFolder.IconFileName), art.Source(DiscArtSlot.TitleIcon).ArtPath);
        Assert.All(Enum.GetValues<DiscArtSlot>(), image => Assert.True(art.Source(image).IsArt));
    }

    [Fact]
    public void With_no_art_folder_every_resource_is_the_original_look_and_nothing_is_a_picture()
    {
        var original = LauncherTheme.From(DiscArtFolder.Open(Path.Combine(_root, "never-written")));

        Assert.All(Enum.GetValues<DiscArtSlot>(), image =>
        {
            Assert.False(original.Source(image).IsArt);
            Assert.Null(original.Source(image).ArtPath);
        });
        Assert.Equal(ThemePalette.Original, original.Palette);
    }

    [Fact]
    public void An_empty_art_folder_is_the_original_look()
    {
        Directory.CreateDirectory(ArtFolder);

        var theme = LauncherTheme.From(DiscArtFolder.Open(ArtFolder));

        Assert.All(Enum.GetValues<DiscArtSlot>(), image => Assert.False(theme.Source(image).IsArt));
        Assert.Equal(ThemePalette.Original, theme.Palette);
    }

    [Fact]
    public void In_a_partial_art_folder_only_the_missing_images_fall_back()
    {
        var theme = ThemeFor(new TestArtDisc(), TestArtDisc.NxeArt, TestArtDisc.Module);

        Assert.False(theme.Source(DiscArtSlot.Background).IsArt);
        Assert.False(theme.Source(DiscArtSlot.BoxArt).IsArt);
        Assert.False(theme.Source(DiscArtSlot.TitleIcon).IsArt);
        Assert.True(theme.Source(DiscArtSlot.Logo).IsArt);
        Assert.True(theme.Source(DiscArtSlot.SetupBackground).IsArt);
    }

    [Fact]
    public void An_icon_alone_changes_the_window_icon_and_nothing_else()
    {
        var theme = ThemeFor(new TestArtDisc(), TestArtDisc.NxeArt, TestArtDisc.PressStart);

        Assert.True(theme.Source(DiscArtSlot.TitleIcon).IsArt);
        Assert.False(theme.Source(DiscArtSlot.Logo).IsArt);
        Assert.Equal(ThemePalette.Original, theme.Palette);
    }

    [Fact]
    public void Home_takes_the_home_background_and_Setup_and_Settings_take_the_setup_background()
    {
        var theme = ThemeFor(new TestArtDisc());

        Assert.Equal(theme.Source(DiscArtSlot.Background), theme.LookFor(LauncherPage.Home).Background);
        Assert.Equal(theme.Source(DiscArtSlot.SetupBackground), theme.LookFor(LauncherPage.Setup).Background);
        Assert.Equal(theme.Source(DiscArtSlot.SetupBackground), theme.LookFor(LauncherPage.Settings).Background);
    }

    [Fact]
    public void A_page_whose_background_is_missing_shows_no_picture_not_the_other_pages_one()
    {
        var theme = ThemeFor(new TestArtDisc(), TestArtDisc.PressStart);

        Assert.True(theme.LookFor(LauncherPage.Home).Background.IsArt);
        Assert.False(theme.LookFor(LauncherPage.Setup).Background.IsArt);
        Assert.False(theme.LookFor(LauncherPage.Settings).Background.IsArt);
    }

    [Fact]
    public void Any_picture_from_the_disc_retunes_the_accents_to_the_art_and_an_empty_folder_keeps_the_original()
    {
        var withArt = ThemeFor(new TestArtDisc(), TestArtDisc.NxeArt, TestArtDisc.Module);

        Assert.Equal(ThemePalette.Art, withArt.Palette);
        Assert.NotEqual(ThemePalette.Original, withArt.Palette);
    }

    [Fact]
    public void The_credit_line_says_fan_recompilation_and_not_affiliated_with_2K_and_names_the_disc_only_when_art_shows()
    {
        var original = LauncherTheme.From(DiscArtFolder.Open(Path.Combine(_root, "never-written")));
        var withArt = ThemeFor(new TestArtDisc());

        foreach (var line in new[] { original.CreditLine, withArt.CreditLine })
        {
            Assert.Contains("fan recompilation", line);
            Assert.Contains("not affiliated with 2K Sports or 2K Czech", line);
        }
        Assert.DoesNotContain("disc", original.CreditLine);
        Assert.Contains("from your own disc", withArt.CreditLine);
    }

    [Fact]
    public void Reading_the_same_art_folder_again_is_the_same_look_and_a_changed_one_is_not()
    {
        var full = ThemeFor(new TestArtDisc());
        var again = LauncherTheme.From(DiscArtFolder.Open(ArtFolder));
        Directory.Delete(ArtFolder, recursive: true);
        var gone = LauncherTheme.From(DiscArtFolder.Open(ArtFolder));

        Assert.True(full.SameLookAs(again));
        Assert.False(full.SameLookAs(gone));
        Assert.True(gone.SameLookAs(LauncherTheme.Original));
    }

    // Item 1: the file names are always the same; what the pictures are is not.
    [Fact]
    public void A_second_disc_that_writes_the_same_file_names_is_a_different_look()
    {
        var first = ThemeFor(new TestArtDisc());
        var names = TestArtFolder.Files(ArtFolder);

        var other = new TestArtDisc();
        other.Files[TestArtDisc.PressStart] = TestXuiz.Build(
            ("Bitmaps\\PressStart.PNG", TestArtImages.Png(32, 18, 13)),
            ("Bitmaps\\Logo.PNG", TestArtImages.Png(28, 9, 14)));
        DiscArt.FromFolder(other.WriteFolder(Path.Combine(_root, "disc-b")), ArtFolder);
        var second = LauncherTheme.From(DiscArtFolder.Open(ArtFolder));

        Assert.Equal(names, TestArtFolder.Files(ArtFolder));
        Assert.Equal(first.Source(DiscArtSlot.Logo), second.Source(DiscArtSlot.Logo));
        Assert.False(first.SameLookAs(second));
        Assert.NotEqual(first.ArtStamp, second.ArtStamp);
    }

    // Item 5: the theme is the one place that decides; the window only applies it.

    [Fact]
    public void An_image_that_cannot_be_drawn_counts_as_missing()
    {
        DiscArt.FromFolder(new TestArtDisc().WriteFolder(Path.Combine(_root, "disc")), ArtFolder);
        var art = DiscArtFolder.Open(ArtFolder);

        var theme = LauncherTheme.From(art, (slot, _) => slot != DiscArtSlot.Logo && slot != DiscArtSlot.TitleIcon);

        Assert.False(theme.Source(DiscArtSlot.Logo).IsArt);
        Assert.False(theme.Source(DiscArtSlot.TitleIcon).IsArt);
        Assert.True(theme.Source(DiscArtSlot.Background).IsArt);
        Assert.False(theme.LookFor(LauncherPage.Home).ShowsLogo);
        Assert.Equal(ThemePalette.Art, theme.Palette);
    }

    [Fact]
    public void When_no_picture_can_be_drawn_the_whole_look_is_the_original_one()
    {
        DiscArt.FromFolder(new TestArtDisc().WriteFolder(Path.Combine(_root, "disc")), ArtFolder);

        var theme = LauncherTheme.From(DiscArtFolder.Open(ArtFolder), (slot, _) => slot == DiscArtSlot.TitleIcon);

        Assert.Equal(ThemePalette.Original, theme.Palette);
        Assert.Equal(LauncherTheme.Original.CreditLine, theme.CreditLine);
        Assert.True(theme.Source(DiscArtSlot.TitleIcon).IsArt);
        Assert.All(new[] { LauncherPage.Home, LauncherPage.Setup, LauncherPage.Settings }, page =>
        {
            var look = theme.LookFor(page);
            Assert.False(look.Background.IsArt);
            Assert.False(look.ShowsLogo);
            Assert.False(look.DimsBackground);
        });
    }

    [Fact]
    public void The_drawing_test_is_asked_about_each_file_the_theme_would_use_and_nothing_else()
    {
        DiscArt.FromFolder(new TestArtDisc().WriteFolder(Path.Combine(_root, "disc")), ArtFolder);
        var asked = new List<(DiscArtSlot Slot, string Path)>();

        LauncherTheme.From(DiscArtFolder.Open(ArtFolder), (slot, path) =>
        {
            asked.Add((slot, path));
            return true;
        });

        Assert.Equal(Enum.GetValues<DiscArtSlot>().Count(), asked.Count);
        Assert.Contains((DiscArtSlot.TitleIcon, Path.Combine(ArtFolder, DiscArtFolder.IconFileName)), asked);
        Assert.Contains((DiscArtSlot.Logo, Path.Combine(ArtFolder, "logo.png")), asked);
    }

    [Fact]
    public void Each_page_takes_its_background_its_dimming_and_its_header_from_the_theme()
    {
        var theme = ThemeFor(new TestArtDisc());

        var home = theme.LookFor(LauncherPage.Home);
        var setup = theme.LookFor(LauncherPage.Setup);
        var settings = theme.LookFor(LauncherPage.Settings);

        Assert.Equal(theme.Source(DiscArtSlot.Background), home.Background);
        Assert.False(home.DimsBackground);
        Assert.Equal(theme.Source(DiscArtSlot.SetupBackground), setup.Background);
        Assert.True(setup.DimsBackground);
        Assert.Equal(settings, setup);
        Assert.True(home.ShowsLogo);
        Assert.True(home.LogoHeight > setup.LogoHeight);
    }

    [Fact]
    public void A_page_with_no_picture_is_not_dimmed_and_the_original_look_shows_the_title_text()
    {
        var look = LauncherTheme.Original.LookFor(LauncherPage.Setup);

        Assert.False(look.Background.IsArt);
        Assert.False(look.DimsBackground);
        Assert.False(look.ShowsLogo);
    }

    // Item 7 and 11: the brushes the window sets are the palette's, under names that say what they hold.

    private static string ThemeAxaml() =>
        File.ReadAllText(Path.Combine(NoGameArtTests.LauncherRoot(), "src", "Launcher.App", "Theme.axaml"));

    [Fact]
    public void Theme_axaml_declares_each_palette_brush_with_the_original_colour()
    {
        var axaml = ThemeAxaml();

        foreach (var (key, colour) in ThemePalette.Original.Brushes())
            Assert.Matches($"<SolidColorBrush x:Key=\"{key}\" Color=\"{colour}\"\\s*/>", axaml);
    }

    [Fact]
    public void No_page_names_a_brush_the_theme_does_not_declare()
    {
        var declared = ThemePalette.Original.Brushes().Select(brush => brush.Key).ToHashSet();
        var app = Path.Combine(NoGameArtTests.LauncherRoot(), "src", "Launcher.App");

        var used = Directory.EnumerateFiles(app, "*.axaml", SearchOption.AllDirectories)
            .SelectMany(file => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(file), @"StaticResource (\w+Brush)").Select(match => match.Groups[1].Value))
            .Distinct()
            .Where(name => !declared.Contains(name))
            .ToList();

        Assert.Empty(used);
    }

    [Fact]
    public void Brush_names_say_what_they_hold()
    {
        var keys = ThemePalette.Original.Brushes().Select(brush => brush.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.DoesNotContain(keys, key => key is "BallBrush" or "BallHotBrush" or "CourtBrush" or "SkyBrush");
        Assert.Contains("AccentBrush", keys);
        Assert.Contains("SecondaryBrush", keys);
        Assert.Contains("SilverBrush", keys);
    }

    [Fact]
    public void The_original_look_keeps_its_disabled_play_colours_and_the_art_look_keeps_the_text_readable()
    {
        Assert.Equal("#4A4E2A", ThemePalette.Original.AccentDisabled);
        Assert.Equal("#2A2A2A", ThemePalette.Original.OnAccentDisabled);
        Assert.True(Contrast.Ratio(ThemePalette.Art.OnAccentDisabled, ThemePalette.Art.AccentDisabled) >= 4.5);
    }

    [Fact]
    public void Disabled_play_takes_its_colours_from_the_palette_brushes_not_from_opacity()
    {
        var axaml = ThemeAxaml();

        var disabled = System.Text.RegularExpressions.Regex.Match(axaml, @"Button\.play:disabled[\s\S]*?</Style>").Value;

        Assert.Contains("AccentDisabledBrush", disabled);
        Assert.Contains("OnAccentDisabledBrush", disabled);
        Assert.DoesNotContain("Opacity", disabled);
    }
}
