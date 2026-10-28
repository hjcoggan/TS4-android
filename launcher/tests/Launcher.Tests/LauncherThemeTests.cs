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

        Assert.Equal(Path.Combine(ArtFolder, "background.jpg"), art.Source(ThemeImage.HomeBackground).ArtPath);
        Assert.Equal(Path.Combine(ArtFolder, "setup-background.png"), art.Source(ThemeImage.SetupBackground).ArtPath);
        Assert.Equal(Path.Combine(ArtFolder, "logo.png"), art.Source(ThemeImage.Logo).ArtPath);
        Assert.Equal(Path.Combine(ArtFolder, "boxart.jpg"), art.Source(ThemeImage.BoxArt).ArtPath);
        Assert.Equal(Path.Combine(ArtFolder, DiscArtFolder.IconFileName), art.Source(ThemeImage.WindowIcon).ArtPath);
        Assert.All(Enum.GetValues<ThemeImage>(), image => Assert.True(art.Source(image).IsArt));
    }

    [Fact]
    public void With_no_art_folder_every_resource_is_the_original_look_and_nothing_is_a_picture()
    {
        var original = LauncherTheme.From(DiscArtFolder.Open(Path.Combine(_root, "never-written")));

        Assert.All(Enum.GetValues<ThemeImage>(), image =>
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

        Assert.All(Enum.GetValues<ThemeImage>(), image => Assert.False(theme.Source(image).IsArt));
        Assert.Equal(ThemePalette.Original, theme.Palette);
    }

    [Fact]
    public void In_a_partial_art_folder_only_the_missing_images_fall_back()
    {
        var theme = ThemeFor(new TestArtDisc(), TestArtDisc.NxeArt, TestArtDisc.Module);

        Assert.False(theme.Source(ThemeImage.HomeBackground).IsArt);
        Assert.False(theme.Source(ThemeImage.BoxArt).IsArt);
        Assert.False(theme.Source(ThemeImage.WindowIcon).IsArt);
        Assert.True(theme.Source(ThemeImage.Logo).IsArt);
        Assert.True(theme.Source(ThemeImage.SetupBackground).IsArt);
    }

    [Fact]
    public void An_icon_alone_changes_the_window_icon_and_nothing_else()
    {
        var theme = ThemeFor(new TestArtDisc(), TestArtDisc.NxeArt, TestArtDisc.PressStart);

        Assert.True(theme.Source(ThemeImage.WindowIcon).IsArt);
        Assert.False(theme.Source(ThemeImage.Logo).IsArt);
        Assert.Equal(ThemePalette.Original, theme.Palette);
    }

    [Fact]
    public void Home_takes_the_home_background_and_Setup_and_Settings_take_the_setup_background()
    {
        var theme = ThemeFor(new TestArtDisc());

        Assert.Equal(theme.Source(ThemeImage.HomeBackground), theme.BackgroundFor(LauncherPage.Home));
        Assert.Equal(theme.Source(ThemeImage.SetupBackground), theme.BackgroundFor(LauncherPage.Setup));
        Assert.Equal(theme.Source(ThemeImage.SetupBackground), theme.BackgroundFor(LauncherPage.Settings));
    }

    [Fact]
    public void A_page_whose_background_is_missing_shows_no_picture_not_the_other_pages_one()
    {
        var theme = ThemeFor(new TestArtDisc(), TestArtDisc.PressStart);

        Assert.True(theme.BackgroundFor(LauncherPage.Home).IsArt);
        Assert.False(theme.BackgroundFor(LauncherPage.Setup).IsArt);
        Assert.False(theme.BackgroundFor(LauncherPage.Settings).IsArt);
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
}
