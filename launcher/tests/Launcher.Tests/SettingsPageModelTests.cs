using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

public sealed class SettingsPageModelTests : IDisposable
{
    // The shape of `topspin4.exe --print-settings-schema`, cut down to the renderer and an in-game-only setting.
    private const string Export = """
        {"format": 1, "sections": ["Graphics"],
         "settings": [
          {"key": "ts4_renderer", "kind": "choice", "default": "native", "choices": ["classic", "native"], "applies_when": "next_start", "renderers": "both", "section": "Graphics", "label": "Renderer", "order": 10, "help": "Which renderer draws the game."},
          {"key": "ts4_msaa", "kind": "choice", "default": "original", "choices": ["original", "4", "8"], "applies_when": "now", "renderers": "native", "section": "Graphics", "label": "MSAA", "order": 30, "help": "MSAA help"}
         ]}
        """;

    private readonly string _folder = Directory.CreateTempSubdirectory("ts4-settings-page-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string SettingsPath => SettingsFile.PathIn(_folder);

    private SettingsPageModel Page() => SettingsPageModel.From(SettingsSchema.Parse(Export), SettingsPath);

    [Fact]
    public void The_renderer_row_has_a_label_the_current_value_and_plain_options()
    {
        var row = Page().Rows.Single();

        Assert.Equal("Renderer", row.Label);
        Assert.Equal("Which renderer draws the game.", row.Help);
        Assert.Equal(["Classic (compatibility)", "Native (recommended)"], row.Options.Select(option => option.Label));
        Assert.Equal("Native (recommended)", row.ValueLabel);   // the export's default, no settings file yet
    }

    [Fact]
    public void The_row_shows_what_the_settings_file_holds()
    {
        File.WriteAllText(SettingsPath, "version = 1\nts4_renderer = \"classic\"\n");

        Assert.Equal("Classic (compatibility)", Page().Rows.Single().ValueLabel);
    }

    [Fact]
    public void Choosing_an_option_writes_the_value_to_the_settings_file_at_once()
    {
        var page = Page();

        var change = page.Choose(page.Rows.Single(), "classic");

        Assert.Equal(SettingChange.Saved, change);
        Assert.Contains("ts4_renderer = \"classic\"", File.ReadAllText(SettingsPath));
        Assert.Equal("Classic (compatibility)", page.Rows.Single().ValueLabel);
    }

    [Fact]
    public void The_page_says_the_other_settings_are_in_game_and_how_to_open_them()
    {
        var line = Page().InGameNote;

        Assert.Contains("PC SETTINGS", line);
        Assert.Contains("OPTIONS", line);
        Assert.Contains("F4", line);
        Assert.Contains("Back and Start", line);
    }

    [Fact]
    public void When_the_games_settings_cannot_be_read_the_page_says_why_instead_of_showing_nothing()
    {
        var page = SettingsPageModel.Unavailable("topspin4.exe was not found next to the Launcher.");

        Assert.Empty(page.Rows);
        Assert.Equal("topspin4.exe was not found next to the Launcher.", page.UnavailableReason);
        Assert.Contains("PC SETTINGS", page.InGameNote);
    }
}
