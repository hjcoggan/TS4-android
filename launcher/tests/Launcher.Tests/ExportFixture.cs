using System.Text.Json;

namespace TopSpin4.Launcher.Tests;

/// <summary>
/// The shape of <c>topspin4.exe --print-settings-schema</c> format 2 (src/settings/schema.h), cut
/// down to a few settings and the two Presets the game has, so the tests need no game.
/// </summary>
internal static class ExportFixture
{
    /// <summary>One setting line of the export. Its choice labels are its choices, capitalised, unless given.</summary>
    internal static string Setting(string key, string kind, string @default, string[] choices, string appliesWhen,
        string renderers, string section, string label, int order, string help = "",
        string[]? choiceLabels = null, (string Device, string Action)? binding = null)
    {
        static string Array(IEnumerable<string> items) => "[" + string.Join(", ", items.Select(item => JsonSerializer.Serialize(item))) + "]";
        var labels = choiceLabels ?? choices.Select(choice => char.ToUpperInvariant(choice[0]) + choice[1..]).ToArray();
        var bound = binding is { } b
            ? $"{{\"device\": {JsonSerializer.Serialize(b.Device)}, \"action\": {JsonSerializer.Serialize(b.Action)}}}"
            : "null";
        return $"{{\"key\": {JsonSerializer.Serialize(key)}, \"kind\": \"{kind}\", \"default\": {JsonSerializer.Serialize(@default)}, "
            + $"\"choices\": {Array(choices)}, \"choice_labels\": {Array(labels)}, \"applies_when\": \"{appliesWhen}\", "
            + $"\"renderers\": \"{renderers}\", \"section\": {JsonSerializer.Serialize(section)}, \"label\": {JsonSerializer.Serialize(label)}, "
            + $"\"order\": {order}, \"help\": {JsonSerializer.Serialize(help)}, \"binding\": {bound}}}";
    }

    /// <summary>The Graphics Presets as the game exports them: Original is the defaults, Enhanced the best look.</summary>
    internal const string Presets = """
        [{"value": "original", "label": "Original", "values": {"ts4_resolution_scale": "1", "ts4_msaa": "original", "ts4_anisotropic_filtering": "original", "ts4_sharpening": "off"}},
         {"value": "enhanced", "label": "Enhanced", "values": {"ts4_resolution_scale": "auto", "ts4_msaa": "4", "ts4_anisotropic_filtering": "16", "ts4_sharpening": "off"}}]
        """;

    /// <summary>The Graphics and Controls settings of a cut-down game, in registry order.</summary>
    internal static readonly string[] GameSettings =
    [
        Setting("ts4_renderer", "choice", "classic", ["classic", "native"], "next_start", "both", "Graphics", "Renderer", 10, "Which renderer draws the game."),
        Setting("ts4_resolution_scale", "choice", "1", ["1", "auto", "2", "3"], "now", "native", "Graphics", "Resolution scale", 20, "Scale help",
            ["1x", "Auto", "2x", "3x"]),
        Setting("ts4_msaa", "choice", "original", ["original", "4", "8"], "now", "native", "Graphics", "MSAA", 30, "MSAA help",
            ["Original", "4x", "8x"]),
        Setting("ts4_anisotropic_filtering", "choice", "original", ["original", "4", "8", "16"], "now", "both", "Graphics", "Anisotropic filtering", 40, "Aniso help",
            ["Original", "4x", "8x", "16x"]),
        Setting("ts4_sharpening", "choice", "off", ["off", "low", "medium", "high"], "now", "both", "Graphics", "Sharpening", 50, "Sharpening help"),
        Setting("mnk_mode", "bool", "false", [], "now", "both", "Controls", "Keyboard and mouse", 10, "Mnk help"),
        Setting("ts4_pad_a", "choice", "a", ["a", "b", "ls"], "next_start", "both", "Controls", "Pad: A", 100, "Pad help",
            ["A", "B", "LS click"], ("pad", "A")),
        Setting("keybind_a", "text", "Semicolon,Space", [], "now", "both", "Controls", "Keys: A", 200, "Keyboard help", null, ("keyboard", "A")),
    ];

    /// <summary>A format 2 export of the given settings; the sections are the ones they name, in first-seen order unless given.</summary>
    internal static string Json(IEnumerable<string>? settings = null, string[]? sections = null, long? fileVersion = 2,
        int format = 2, string presets = Presets)
    {
        var lines = (settings ?? GameSettings).ToList();
        var names = sections ?? lines.Select(line => JsonDocument.Parse(line).RootElement.GetProperty("section").GetString()!).Distinct().ToArray();
        var version = fileVersion is { } v ? $"\"settings_file_version\": {v}, " : "";
        return $"{{\"format\": {format}, {version}\"sections\": [{string.Join(", ", names.Select(name => JsonSerializer.Serialize(name)))}],\n"
            + $" \"preset_section\": \"Graphics\", \"preset_label\": \"Graphics preset\", \"preset_help\": \"Original: defaults. Enhanced: best look.\",\n"
            + $" \"presets\": {presets},\n \"settings\": [\n  {string.Join(",\n  ", lines)}\n ]}}\n";
    }
}
