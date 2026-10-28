using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

/// <summary>
/// Remapping on the Settings page: what a captured pad button or key writes to the Settings file,
/// the clash it reports, and Reset controls. The clash rules are the game's (SettingsModel::Change
/// and BindKey in src/settings/page_model.h): a pad button another binding uses swaps, a key
/// another binding uses is taken from it.
/// </summary>
public sealed class BindingTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("ts4-bindings-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string SettingsPath => SettingsFile.PathIn(_folder);

    // A cut-down Controls section: three pad bindings and three key bindings, plus a setting that is not a binding.
    private static readonly string[] Controls =
    [
        ExportFixture.Setting("ts4_renderer", "choice", "classic", ["classic", "native"], "next_start", "both", "Graphics", "Renderer", 10),
        ExportFixture.Setting("ts4_msaa", "choice", "original", ["original", "4"], "now", "native", "Graphics", "MSAA", 30),
        ExportFixture.Setting("mnk_mode", "bool", "false", [], "now", "both", "Controls", "Keyboard and mouse", 10),
        ExportFixture.Setting("ts4_pad_a", "choice", "a", ["a", "b", "ls", "lt"], "next_start", "both", "Controls", "Pad: A", 100, "",
            ["A", "B", "LS click", "LT"], ("pad", "A")),
        ExportFixture.Setting("ts4_pad_b", "choice", "b", ["a", "b", "ls", "lt"], "next_start", "both", "Controls", "Pad: B", 110, "",
            ["A", "B", "LS click", "LT"], ("pad", "B")),
        ExportFixture.Setting("ts4_pad_ls", "choice", "ls", ["a", "b", "ls", "lt"], "next_start", "both", "Controls", "Pad: LS click", 120, "",
            ["A", "B", "LS click", "LT"], ("pad", "LS click")),
        ExportFixture.Setting("keybind_a", "text", "Semicolon,Space", [], "now", "both", "Controls", "Keys: A", 200, "", null, ("keyboard", "A")),
        ExportFixture.Setting("keybind_b", "text", "Quote,Backspace", [], "now", "both", "Controls", "Keys: B", 210, "", null, ("keyboard", "B")),
        ExportFixture.Setting("keybind_x", "text", "L", [], "now", "both", "Controls", "Keys: X", 220, "", null, ("keyboard", "X")),
    ];

    private SettingsPageModel Page() =>
        SettingsPageModel.From(SettingsSchema.Parse(ExportFixture.Json(Controls)), SettingsPath);

    private static SettingRow Row(SettingsPageModel page, string key) =>
        page.Tabs.SelectMany(tab => tab.Rows).Single(row => row.Key == key);

    private string Saved(string key) => SettingsFile.ValueOf(File.ReadAllText(SettingsPath), key)!;

    [Fact]
    public void A_pad_button_is_written_as_the_input_the_game_names()
    {
        var page = Page();

        var result = page.Bind(Row(page, "ts4_pad_a"), "lt");

        Assert.Equal(SettingChange.Saved, result.Change);
        Assert.Empty(result.Clashes);
        Assert.Equal("lt", Saved("ts4_pad_a"));
        Assert.Equal("LT", Row(page, "ts4_pad_a").ValueLabel);
        Assert.Contains("ts4_pad_a = \"lt\"\n", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void A_pad_button_another_binding_uses_is_reported_and_the_two_swap_as_in_game()
    {
        var page = Page();

        var result = page.Bind(Row(page, "ts4_pad_a"), "b");

        Assert.Equal(SettingChange.Saved, result.Change);
        Assert.Equal("b", Saved("ts4_pad_a"));
        Assert.Equal("a", Saved("ts4_pad_b"));
        var clash = Assert.Single(result.Clashes);
        Assert.Contains("Pad: B", clash);
        Assert.Contains("B", clash);
        Assert.Equal("ls", Row(page, "ts4_pad_ls").Value);
    }

    [Fact]
    public void Choosing_the_button_a_pad_binding_already_has_changes_nothing()
    {
        var page = Page();

        var result = page.Bind(Row(page, "ts4_pad_a"), "a");

        Assert.Equal(SettingChange.Saved, result.Change);
        Assert.Empty(result.Clashes);
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public void A_key_is_written_as_the_single_key_in_the_games_format_with_its_modifiers()
    {
        var page = Page();

        var result = page.Bind(Row(page, "keybind_a"), "Shift+W");

        Assert.Equal(SettingChange.Saved, result.Change);
        Assert.Empty(result.Clashes);
        Assert.Equal("Shift+W", Saved("keybind_a"));
        Assert.Contains("keybind_a = \"Shift+W\"\n", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void A_key_another_binding_uses_is_reported_and_taken_from_it_as_in_game()
    {
        var page = Page();

        var result = page.Bind(Row(page, "keybind_b"), "Space");

        Assert.Equal("Space", Saved("keybind_b"));
        Assert.Equal("Semicolon", Saved("keybind_a"));
        var clash = Assert.Single(result.Clashes);
        Assert.Contains("Keys: A", clash);
        Assert.Contains("Space", clash);
    }

    [Fact]
    public void A_binding_left_with_no_key_takes_the_old_keys_of_the_one_that_took_its_key()
    {
        var page = Page();

        var result = page.Bind(Row(page, "keybind_a"), "L");

        Assert.Equal("L", Saved("keybind_a"));
        Assert.Equal("Semicolon,Space", Saved("keybind_x"));
        Assert.Contains("Keys: X", Assert.Single(result.Clashes));
    }

    [Fact]
    public void A_value_the_game_would_refuse_is_not_written()
    {
        var page = Page();

        Assert.Equal(SettingChange.Refused, page.Bind(Row(page, "ts4_pad_a"), "nope").Change);
        Assert.Equal(SettingChange.Refused, page.Bind(Row(page, "keybind_a"), "").Change);
        Assert.Equal(SettingChange.Refused, page.Bind(Row(page, "keybind_a"), "Say \"hi\"").Change);
        Assert.Equal(SettingChange.Refused, page.Bind(Row(page, "mnk_mode"), "true").Change);   // not a binding
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public void A_binding_in_a_file_from_a_newer_game_is_left_alone()
    {
        File.WriteAllText(SettingsPath, "version = 9\nts4_pad_a = \"ls\"\n");
        var page = Page();

        var result = page.Bind(Row(page, "ts4_pad_a"), "lt");

        Assert.Equal(SettingChange.FileIsNewer, result.Change);
        Assert.Equal("version = 9\nts4_pad_a = \"ls\"\n", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void Choosing_on_a_binding_row_goes_through_the_same_clash_rules()
    {
        var page = Page();

        Assert.Equal(SettingChange.Saved, page.Choose(Row(page, "ts4_pad_a"), "b"));

        Assert.Equal("a", Saved("ts4_pad_b"));
    }

    [Fact]
    public void Reset_controls_writes_the_defaults_of_the_controls_section_only()
    {
        File.WriteAllText(SettingsPath,
            "version = 2\n# mine\nts4_renderer = \"native\"\nts4_msaa = \"4\"\nmnk_mode = true\nts4_pad_a = \"lt\"\nkeybind_a = \"Shift+W\"\nunknown_key = 5\n");
        var page = Page();
        page.Bind(Row(page, "keybind_b"), "Z");

        var change = page.ResetControls();

        Assert.Equal(SettingChange.Saved, change);
        var file = File.ReadAllText(SettingsPath);
        Assert.Equal("a", SettingsFile.ValueOf(file, "ts4_pad_a"));
        Assert.Equal("b", SettingsFile.ValueOf(file, "ts4_pad_b"));
        Assert.Equal("ls", SettingsFile.ValueOf(file, "ts4_pad_ls"));
        Assert.Equal("Semicolon,Space", SettingsFile.ValueOf(file, "keybind_a"));
        Assert.Equal("Quote,Backspace", SettingsFile.ValueOf(file, "keybind_b"));
        Assert.Equal("L", SettingsFile.ValueOf(file, "keybind_x"));
        // Everything that is not a binding stays as it was, including keyboard and mouse on.
        Assert.Equal("native", SettingsFile.ValueOf(file, "ts4_renderer"));
        Assert.Equal("4", SettingsFile.ValueOf(file, "ts4_msaa"));
        Assert.Equal("true", SettingsFile.ValueOf(file, "mnk_mode"));
        Assert.StartsWith("version = 2\n# mine\n", file);
        Assert.Contains("unknown_key = 5\n", file);
    }

    [Fact]
    public void Reset_controls_on_a_file_from_a_newer_game_changes_nothing()
    {
        File.WriteAllText(SettingsPath, "version = 9\nts4_pad_a = \"lt\"\n");

        Assert.Equal(SettingChange.FileIsNewer, Page().ResetControls());
        Assert.Equal("version = 9\nts4_pad_a = \"lt\"\n", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void The_controls_tab_ends_with_a_Reset_controls_row_and_the_bindings_can_be_remapped()
    {
        var page = Page();
        var controls = page.Tabs.Single(tab => tab.Name == "Controls");

        var reset = controls.Rows[^1];
        Assert.Equal((SettingRowKind.Action, "reset_controls", "Reset controls"), (reset.Kind, reset.Key, reset.Label));
        Assert.DoesNotContain(page.Tabs.Single(tab => tab.Name == "Graphics").Rows, row => row.Kind == SettingRowKind.Action);
        Assert.True(Row(page, "ts4_pad_a").Remappable);
        Assert.True(Row(page, "keybind_a").Remappable);
        Assert.False(Row(page, "mnk_mode").Remappable);
    }
}
