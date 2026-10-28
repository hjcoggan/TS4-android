using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

public sealed class SettingsPageModelTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("ts4-settings-page-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string SettingsPath => SettingsFile.PathIn(_folder);

    private SettingsPageModel Page(string? export = null) =>
        SettingsPageModel.From(SettingsSchema.Parse(export ?? ExportFixture.Json()), SettingsPath);

    private static SettingRow Row(SettingsPageModel page, string key) =>
        page.Tabs.SelectMany(tab => tab.Rows).Single(row => row.Key == key);

    private void WriteSettings(string text) => File.WriteAllText(SettingsPath, text);

    [Fact]
    public void Tabs_follow_the_exports_sections_in_its_order_and_hold_every_setting_in_page_order()
    {
        var page = Page();

        Assert.Equal(["Graphics", "Controls"], page.Tabs.Select(tab => tab.Name));
        Assert.Equal(
            ["graphics_preset", "ts4_renderer", "ts4_resolution_scale", "ts4_msaa", "ts4_anisotropic_filtering", "ts4_sharpening"],
            page.Tabs[0].Rows.Select(row => row.Key));
        Assert.Equal(["mnk_mode", "ts4_pad_a", "keybind_a"], page.Tabs[1].Rows.Select(row => row.Key));
    }

    [Fact]
    public void The_page_keeps_no_list_of_its_own_a_setting_and_a_section_the_export_adds_appear()
    {
        var export = ExportFixture.Json([
            .. ExportFixture.GameSettings,
            ExportFixture.Setting("ts4_volume", "choice", "loud", ["loud", "quiet"], "now", "both", "Sound", "Volume", 10, "Volume help")]);

        var page = Page(export);

        Assert.Equal(["Graphics", "Controls", "Sound"], page.Tabs.Select(tab => tab.Name));
        var volume = Row(page, "ts4_volume");
        Assert.Equal(("Volume", "Volume help", "loud"), (volume.Label, volume.Help, volume.Value));
    }

    [Fact]
    public void A_row_has_its_label_value_choices_and_help_as_the_game_names_them()
    {
        var scale = Row(Page(), "ts4_resolution_scale");

        Assert.Equal(("Resolution scale", "Scale help", "1", "1x"), (scale.Label, scale.Help, scale.Value, scale.ValueLabel));
        Assert.Equal(["1x", "Auto", "2x", "3x"], scale.Options.Select(option => option.Label));
        Assert.Equal(["1", "auto", "2", "3"], scale.Options.Select(option => option.Value));
        Assert.Equal(["Off", "On"], Row(Page(), "mnk_mode").Options.Select(option => option.Label));
    }

    [Fact]
    public void The_renderer_row_has_plain_options_and_shows_the_default_when_there_is_no_settings_file()
    {
        var renderer = Row(Page(), "ts4_renderer");

        Assert.Equal(["Classic (compatibility)", "Native (recommended)"], renderer.Options.Select(option => option.Label));
        Assert.Equal("Classic (compatibility)", renderer.ValueLabel);
    }

    [Fact]
    public void A_row_shows_what_the_settings_file_holds_and_the_default_for_a_value_the_game_would_refuse()
    {
        WriteSettings("version = 2\nts4_renderer = \"native\"\nts4_msaa = \"16\"\n");

        var page = Page();

        Assert.Equal("Native (recommended)", Row(page, "ts4_renderer").ValueLabel);
        Assert.Equal("original", Row(page, "ts4_msaa").Value);
    }

    [Fact]
    public void The_graphics_tab_opens_with_the_preset_row_showing_Original_on_the_defaults()
    {
        var preset = Page().Tabs[0].Rows[0];

        Assert.Equal(SettingRowKind.Preset, preset.Kind);
        Assert.Equal(("Graphics preset", "original", "Original"), (preset.Label, preset.Value, preset.ValueLabel));
        Assert.Equal(["Original", "Enhanced"], preset.Options.Select(option => option.Label));
        Assert.Equal("Original: defaults. Enhanced: best look.", preset.Help);
        Assert.All(Page().Tabs[1].Rows, row => Assert.Equal(SettingRowKind.Setting, row.Kind));
    }

    [Fact]
    public void Choosing_Enhanced_writes_every_setting_it_covers_and_the_row_then_shows_Enhanced()
    {
        WriteSettings("version = 2\n# mine\nts4_msaa = \"8\"\n");
        var page = Page();

        Assert.Equal(SettingChange.Saved, page.Choose(Row(page, "graphics_preset"), "enhanced"));

        var file = File.ReadAllText(SettingsPath);
        Assert.Equal(
            "version = 2\n# mine\nts4_msaa = \"4\"\nts4_resolution_scale = \"auto\"\nts4_anisotropic_filtering = \"16\"\nts4_sharpening = \"off\"\n",
            file);
        Assert.Equal("Enhanced", Row(page, "graphics_preset").ValueLabel);
        Assert.Equal("4x", Row(page, "ts4_msaa").ValueLabel);
    }

    [Fact]
    public void A_tweak_turns_the_preset_row_to_Custom_and_choosing_Original_returns_it()
    {
        var page = Page();
        page.Choose(Row(page, "graphics_preset"), "enhanced");

        page.Choose(Row(page, "ts4_anisotropic_filtering"), "4");

        var preset = Row(page, "graphics_preset");
        Assert.Equal(("custom", "Custom"), (preset.Value, preset.ValueLabel));
        Assert.DoesNotContain(preset.Options, option => option.Value == "custom");   // never chosen, only shown

        page.Choose(preset, "original");
        Assert.Equal("Original", Row(page, "graphics_preset").ValueLabel);
        Assert.Equal("original", Row(page, "ts4_anisotropic_filtering").Value);
    }

    [Fact]
    public void Native_only_settings_do_not_make_the_preset_Custom_on_Classic_but_do_on_Native()
    {
        WriteSettings("version = 2\nts4_msaa = \"8\"\nts4_resolution_scale = \"3\"\n");

        Assert.Equal("original", Row(Page(), "graphics_preset").Value);   // Classic by default: both are left out

        WriteSettings("version = 2\nts4_renderer = \"native\"\nts4_msaa = \"8\"\n");
        Assert.Equal("custom", Row(Page(), "graphics_preset").Value);
    }

    [Fact]
    public void Enhanced_on_Classic_needs_only_the_settings_that_do_something_there()
    {
        WriteSettings("version = 2\nts4_anisotropic_filtering = \"16\"\n");

        Assert.Equal("enhanced", Row(Page(), "graphics_preset").Value);
    }

    [Fact]
    public void Native_only_rows_are_unavailable_on_Classic_with_the_reason_and_available_on_Native()
    {
        var classic = Page();
        var msaa = Row(classic, "ts4_msaa");

        Assert.False(msaa.Available);
        Assert.False(msaa.Editable);
        Assert.Contains("Native renderer", msaa.Note);
        Assert.True(Row(classic, "ts4_sharpening").Available);
        Assert.Null(Row(classic, "ts4_sharpening").Note);

        WriteSettings("version = 2\nts4_renderer = \"native\"\n");
        var native = Page();
        Assert.True(Row(native, "ts4_msaa").Available);
        Assert.True(Row(native, "ts4_msaa").Editable);
        Assert.Null(Row(native, "ts4_msaa").Note);
    }

    [Fact]
    public void The_preset_row_says_which_settings_do_not_count_on_Classic()
    {
        var preset = Row(Page(), "graphics_preset");

        Assert.Contains("Resolution scale", preset.Note);
        Assert.Contains("MSAA", preset.Note);
        Assert.Contains("Classic", preset.Note);
    }

    [Fact]
    public void A_change_rewrites_only_its_line_in_an_existing_settings_file()
    {
        const string Before = "version = 2\r\n# mine\r\nmnk_mode = true   # keep\r\nts4_renderer = \"classic\"\r\nts4_msaa = \"4\"\r\n\r\n[table]\r\nx = 1";
        WriteSettings(Before);
        var page = Page();

        Assert.Equal(SettingChange.Saved, page.Choose(Row(page, "ts4_renderer"), "native"));

        Assert.Equal(Before.Replace("ts4_renderer = \"classic\"", "ts4_renderer = \"native\""), File.ReadAllText(SettingsPath));
        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }

    [Fact]
    public void A_bool_is_written_bare_and_a_file_from_a_newer_game_is_left_alone()
    {
        var page = Page();
        Assert.Equal(SettingChange.Saved, page.Choose(Row(page, "mnk_mode"), "true"));
        Assert.Equal("version = 2\nmnk_mode = true\n", File.ReadAllText(SettingsPath));

        WriteSettings("version = 9\nmnk_mode = false\n");
        Assert.Equal(SettingChange.FileIsNewer, page.Choose(Row(page, "mnk_mode"), "true"));
        Assert.Equal("version = 9\nmnk_mode = false\n", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void Reset_to_defaults_writes_every_setting_and_keeps_the_rest_of_the_file()
    {
        WriteSettings("version = 2\n# mine\nts4_renderer = \"native\"\nts4_msaa = \"8\"\nmnk_mode = true\nunknown_key = 5\n");
        var page = Page();

        Assert.Equal(SettingChange.Saved, page.ResetToDefaults());

        var file = File.ReadAllText(SettingsPath);
        foreach (var setting in SettingsSchema.Parse(ExportFixture.Json()).Settings)
            Assert.Equal(setting.Default, SettingsFile.ValueOf(file, setting.Key));   // every key is there, at its default
        Assert.StartsWith("version = 2\n# mine\n", file);
        Assert.Contains("unknown_key = 5\n", file);
        Assert.Contains("mnk_mode = false\n", file);
        Assert.Equal("original", Row(page, "graphics_preset").Value);
        Assert.Equal("Classic (compatibility)", Row(page, "ts4_renderer").ValueLabel);
    }

    [Fact]
    public void Reset_to_defaults_on_a_file_from_a_newer_game_changes_nothing()
    {
        WriteSettings("version = 9\nts4_renderer = \"native\"\n");

        Assert.Equal(SettingChange.FileIsNewer, Page().ResetToDefaults());
        Assert.Equal("version = 9\nts4_renderer = \"native\"\n", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void Binding_rows_are_shown_read_only_with_their_device_and_action()
    {
        var page = Page();
        var pad = Row(page, "ts4_pad_a");
        var keys = Row(page, "keybind_a");

        Assert.Equal(("pad", "A"), (pad.Binding!.Device, pad.Binding.Action));
        Assert.Equal(("keyboard", "A"), (keys.Binding!.Device, keys.Binding.Action));
        Assert.False(pad.Editable);
        Assert.False(keys.Editable);
        Assert.Equal("A", pad.ValueLabel);
        Assert.Equal("Semicolon,Space", keys.ValueLabel);
        Assert.Null(Row(page, "mnk_mode").Binding);

        WriteSettings("version = 2\nts4_pad_a = \"ls\"\n");
        Assert.Equal("LS click", Row(Page(), "ts4_pad_a").ValueLabel);
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

        Assert.Empty(page.Tabs);
        Assert.Equal("topspin4.exe was not found next to the Launcher.", page.UnavailableReason);
        Assert.Contains("PC SETTINGS", page.InGameNote);
    }

    [Theory]
    [InlineData("not json", "could not be read")]
    [InlineData("""{"format": 1, "sections": [], "settings": []}""", "format 1")]
    [InlineData("""{"format": 3, "sections": [], "settings": []}""", "format 3")]
    [InlineData("""{"format": 2, "settings": []}""", "could not be read")]
    public void An_export_the_launcher_cannot_read_gives_a_reason_not_an_empty_page(string export, string reason)
    {
        var page = SettingsPageModel.FromExport(export, SettingsPath);

        Assert.Empty(page.Tabs);
        Assert.Contains(reason, page.UnavailableReason);
    }

    [Fact]
    public void An_export_in_format_2_reads_into_a_page()
    {
        var page = SettingsPageModel.FromExport(ExportFixture.Json(), SettingsPath);

        Assert.Null(page.UnavailableReason);
        Assert.Equal(2, page.Tabs.Count);
    }
}
