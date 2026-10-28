using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

public sealed class BeforeStartSettingsTests : IDisposable
{
    // The shape of `topspin4.exe --print-settings-schema` (src/settings/schema.h), cut down.
    private const string Export = """
        {"format": 1, "sections": ["Graphics", "Controls"],
         "settings": [
          {"key": "ts4_renderer", "kind": "choice", "default": "classic", "choices": ["classic", "native"], "applies_when": "next_start", "renderers": "both", "section": "Graphics", "label": "Renderer", "order": 10, "help": "Renderer help \"quoted\""},
          {"key": "ts4_msaa", "kind": "choice", "default": "original", "choices": ["original", "4", "8"], "applies_when": "now", "renderers": "native", "section": "Graphics", "label": "MSAA", "order": 30, "help": "MSAA help"},
          {"key": "ts4_gpu", "kind": "choice", "default": "auto", "choices": ["auto", "1", "2"], "applies_when": "next_start", "renderers": "native", "section": "Graphics", "label": "GPU", "order": 15, "help": "GPU help"},
          {"key": "ts4_pad_a", "kind": "choice", "default": "a", "choices": ["a", "b"], "applies_when": "next_start", "renderers": "both", "section": "Controls", "label": "Pad: A", "order": 100, "help": "Pad help"},
          {"key": "mnk_mode", "kind": "bool", "default": "false", "choices": [], "applies_when": "now", "renderers": "both", "section": "Controls", "label": "Keyboard and mouse", "order": 10, "help": "Mnk help"}
         ]}
        """;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ts4-launcher-settings-" + Guid.NewGuid().ToString("N"));

    private string SettingsPath => SettingsFile.PathIn(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private BeforeStartSettings Settings() => new(SettingsSchema.Parse(Export), SettingsPath);

    [Fact]
    public void Reading_the_export_gives_every_setting_with_its_fields()
    {
        var schema = SettingsSchema.Parse(Export);

        Assert.Equal(5, schema.Settings.Count);
        var renderer = schema.Settings[0];
        Assert.Equal(("ts4_renderer", "choice", "classic", "next_start", "Graphics", "Renderer", 10),
            (renderer.Key, renderer.Kind, renderer.Default, renderer.AppliesWhen, renderer.Section, renderer.Label, renderer.Order));
        Assert.Equal(["classic", "native"], renderer.Choices);
        Assert.Equal("Renderer help \"quoted\"", renderer.Help);
        Assert.True(schema.Settings[4].IsBool);
    }

    [Fact]
    public void The_before_start_settings_are_the_ones_the_export_marks_next_start_in_graphics()
    {
        var keys = SettingsSchema.Parse(Export).BeforeStart().Select(setting => setting.Key);

        Assert.Equal(["ts4_renderer", "ts4_gpu"], keys);   // in page order; no MSAA (applies now), no pad binding (Controls)
    }

    [Fact]
    public void An_export_in_another_format_or_not_json_is_refused()
    {
        Assert.Throws<SettingsExportException>(() => SettingsSchema.Parse("""{"format": 2, "settings": []}"""));
        Assert.Throws<SettingsExportException>(() => SettingsSchema.Parse("not json"));
        Assert.Throws<SettingsExportException>(() => SettingsSchema.Parse("""{"format": 1}"""));
    }

    [Fact]
    public void Writing_a_setting_into_an_existing_file_leaves_the_other_lines_untouched()
    {
        Directory.CreateDirectory(_folder);
        const string Before = "version = 1\r\n# mine\r\nmnk_mode = true   # keep\r\nts4_renderer = \"classic\"\r\nts4_msaa = \"4\"\r\n\r\n[table]\r\nx = 1";
        File.WriteAllText(SettingsPath, Before);
        var settings = Settings();
        var renderer = settings.Settings.Single(setting => setting.Key == "ts4_renderer");

        Assert.Equal(SettingChange.Saved, settings.Set(renderer, "native"));

        Assert.Equal(Before.Replace("ts4_renderer = \"classic\"", "ts4_renderer = \"native\""), File.ReadAllText(SettingsPath));
        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }

    [Fact]
    public void A_setting_missing_from_the_file_is_appended_and_an_unversioned_file_gets_its_version_first()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsPath, "mnk_mode = true");
        var renderer = Settings().Settings.Single(setting => setting.Key == "ts4_renderer");

        Settings().Set(renderer, "native");

        Assert.Equal("version = 1\nmnk_mode = true\nts4_renderer = \"native\"\n", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void A_new_file_is_created_at_the_current_version()
    {
        var renderer = Settings().Settings.Single(setting => setting.Key == "ts4_renderer");

        Settings().Set(renderer, "native");

        Assert.Equal("version = 1\nts4_renderer = \"native\"\n", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void The_version_written_and_the_newer_check_come_from_the_export()
    {
        var versioned = Export.Replace("\"format\": 1,", "\"format\": 1, \"settings_file_version\": 3,");
        var schema = SettingsSchema.Parse(versioned);
        var renderer = schema.Settings[0];
        Assert.Equal(3, schema.FileVersion);
        Assert.Equal(SettingsFile.FallbackVersion, SettingsSchema.Parse(Export).FileVersion);

        new BeforeStartSettings(schema, SettingsPath).Set(renderer, "native");
        Assert.Equal("version = 3\nts4_renderer = \"native\"\n", File.ReadAllText(SettingsPath));

        File.WriteAllText(SettingsPath, "version = 4\n");
        Assert.Equal(SettingChange.FileIsNewer, new BeforeStartSettings(schema, SettingsPath).Set(renderer, "classic"));
    }

    [Fact]
    public void Saving_into_a_version_1_file_migrates_a_resolution_scale_of_4_to_3_as_the_game_does()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsPath, "version = 1\r\n# mine\r\nts4_resolution_scale = \"4\"  # big\r\nts4_msaa = \"4\"\r\n");
        var schema = SettingsSchema.Parse(Export.Replace("\"format\": 1,", "\"format\": 1, \"settings_file_version\": 2,"));

        new BeforeStartSettings(schema, SettingsPath).Set(schema.Settings[0], "native");

        Assert.Equal(
            "version = 2\r\n# mine\r\nts4_resolution_scale = \"3\"  # big\r\nts4_msaa = \"4\"\r\nts4_renderer = \"native\"\n",
            File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void Saving_into_a_version_1_file_keeps_any_other_resolution_scale()
    {
        var schema = SettingsSchema.Parse(Export.Replace("\"format\": 1,", "\"format\": 1, \"settings_file_version\": 2,"));
        foreach (var scale in new[] { "1", "auto", "2", "3" })
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(SettingsPath, $"version = 1\nts4_resolution_scale = \"{scale}\"\n");

            new BeforeStartSettings(schema, SettingsPath).Set(schema.Settings[0], "native");

            Assert.Equal($"version = 2\nts4_resolution_scale = \"{scale}\"\nts4_renderer = \"native\"\n", File.ReadAllText(SettingsPath));
        }
    }

    [Fact]
    public void A_version_2_file_is_not_migrated_again_and_an_export_without_a_version_runs_no_migration()
    {
        Directory.CreateDirectory(_folder);
        var schema = SettingsSchema.Parse(Export.Replace("\"format\": 1,", "\"format\": 1, \"settings_file_version\": 2,"));
        File.WriteAllText(SettingsPath, "version = 2\nts4_resolution_scale = \"4\"\n");
        new BeforeStartSettings(schema, SettingsPath).Set(schema.Settings[0], "native");
        Assert.Equal("version = 2\nts4_resolution_scale = \"4\"\nts4_renderer = \"native\"\n", File.ReadAllText(SettingsPath));

        // The game's version decides how many migrations run: at version 1 there are none.
        File.WriteAllText(SettingsPath, "version = 1\nts4_resolution_scale = \"4\"\n");
        Settings().Set(Settings().Settings[0], "native");
        Assert.Equal("version = 1\nts4_resolution_scale = \"4\"\nts4_renderer = \"native\"\n", File.ReadAllText(SettingsPath));
    }

    [Theory]
    [InlineData("ts4_resolution_scale = \"4\"\n", "ts4_resolution_scale = \"3\"\n")]
    [InlineData("  ts4_resolution_scale='4' # x\r\n", "  ts4_resolution_scale='3' # x\r\n")]
    [InlineData("ts4_resolution_scale = 4\n", "ts4_resolution_scale = 3\n")]
    [InlineData("ts4_resolution_scale = 4 # x", "ts4_resolution_scale = 3 # x")]
    [InlineData("ts4_resolution_scale = \"2\"\n", "ts4_resolution_scale = \"2\"\n")]
    [InlineData("ts4_resolution_scale = \"40\"\n", "ts4_resolution_scale = \"40\"\n")]
    [InlineData("ts4_resolution_scale = \"4\n", "ts4_resolution_scale = \"4\n")]
    [InlineData("ts4_resolution_scale_old = \"4\"\nts4_msaa = \"4\"\n", "ts4_resolution_scale_old = \"4\"\nts4_msaa = \"4\"\n")]
    [InlineData("ts4_resolution_scale =\n", "ts4_resolution_scale =\n")]
    public void Replacing_a_value_changes_only_that_keys_value_and_keeps_the_line(string text, string expected) =>
        Assert.Equal(expected, SettingsFile.ReplaceValue(text, "ts4_resolution_scale", "4", "3"));

    [Theory]
    [InlineData("version = 3\n", 3L)]
    [InlineData("version = -2 # old\n", -2L)]
    [InlineData("version = 4 \t\r\n", 4L)]
    [InlineData("version = \"2\"\n", null)]
    [InlineData("version = 2x\n", null)]
    [InlineData("version = 99999999999999999999\n", null)]
    [InlineData("version_old = 2\n", null)]
    public void The_file_version_is_read_as_the_game_reads_it(string text, long? expected) =>
        Assert.Equal(expected, SettingsFile.Version(text));

    [Fact]
    public void A_file_from_a_newer_build_is_not_rewritten()
    {
        Directory.CreateDirectory(_folder);
        const string Newer = "version = 9\nts4_renderer = \"classic\"\n";
        File.WriteAllText(SettingsPath, Newer);
        var settings = Settings();

        Assert.Equal(SettingChange.FileIsNewer, settings.Set(settings.Settings[0], "native"));
        Assert.Equal(Newer, File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void A_value_the_game_would_refuse_is_not_written()
    {
        var settings = Settings();

        Assert.Equal(SettingChange.Refused, settings.Set(settings.Settings[0], "vulkan"));
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public void The_value_shown_is_the_files_or_the_default()
    {
        var settings = Settings();
        var renderer = settings.Settings.Single(setting => setting.Key == "ts4_renderer");
        Assert.Equal("classic", settings.ValueOf(renderer));

        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsPath, "version = 1\nts4_renderer = \"native\" # why\n");
        Assert.Equal("native", settings.ValueOf(renderer));

        File.WriteAllText(SettingsPath, "ts4_renderer = \"vulkan\"\n");
        Assert.Equal("classic", settings.ValueOf(renderer));
    }

    [Fact]
    public void Pressing_a_setting_moves_it_to_the_next_choice_and_around()
    {
        var settings = Settings();
        var renderer = settings.Settings.Single(setting => setting.Key == "ts4_renderer");

        settings.Cycle(renderer);
        Assert.Equal("native", settings.ValueOf(renderer));
        settings.Cycle(renderer);
        Assert.Equal("classic", settings.ValueOf(renderer));
    }

    [Fact]
    public void A_bool_is_written_bare()
    {
        var mnk = SettingsSchema.Parse(Export).Settings.Single(setting => setting.Key == "mnk_mode");

        Assert.Equal("true", SettingsFile.Literal(mnk, "true"));
        Assert.Equal("\"native\"", SettingsFile.Literal(SettingsSchema.Parse(Export).Settings[0], "native"));
    }

    [Fact]
    public void The_gpu_failure_exit_code_maps_to_the_switch_renderer_message()
    {
        Assert.Equal(0x47505546, GameExitReport.GpuFailureExitCode);
        Assert.Contains("Switch the renderer", GameExitReport.Describe(0x47505546));
        Assert.Null(GameExitReport.Describe(0));
        Assert.Null(GameExitReport.Describe(1));
    }

    [Fact]
    public void Safe_start_arguments_are_plays_plus_safe_and_no_setting()
    {
        var prepared = new PreparedData(@"C:\data\gamedata", @"C:\data\updatedata", Reused: true);

        Assert.Equal(["--game_data_root=" + prepared.GameDataRoot, "--update_data_root=" + prepared.UpdateDataRoot, "--safe"],
            LaunchArguments.SafeStart(prepared));
        Assert.Equal(LaunchArguments.Play(prepared), LaunchArguments.SafeStart(prepared).Where(argument => argument != "--safe"));
        Assert.DoesNotContain(LaunchArguments.Play(prepared), argument => argument == "--safe");
    }

    [Fact]
    public async Task Reading_the_export_from_a_gui_exe_through_a_pipe_works()
    {
        // Needs a built game: set TOPSPIN4_EXE to its path to run this against the real exe.
        var exe = System.Environment.GetEnvironmentVariable("TOPSPIN4_EXE");
        Assert.SkipWhen(string.IsNullOrEmpty(exe) || !File.Exists(exe), "TOPSPIN4_EXE is not set to a built topspin4.exe.");

        var schema = await SettingsExport.ReadAsync(exe!, cancel: TestContext.Current.CancellationToken);

        Assert.Contains(schema.BeforeStart(), setting => setting.Key == "ts4_renderer");
    }
}
