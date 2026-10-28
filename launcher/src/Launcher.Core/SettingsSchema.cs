using System.Text.Json;

namespace TopSpin4.Launcher.Core;

/// <summary>The device a binding setting is for. The export names them "pad" and "keyboard".</summary>
public enum BindingDevice { Pad, Keyboard }

/// <summary>What a controller or key binding setting is for: its device and the game input, as the game names it.</summary>
public sealed record BindingDescription(BindingDevice Device, string Action)
{
    /// <summary>The device as the export names it; a name this Launcher does not know is a format it cannot read.</summary>
    public static BindingDevice ParseDevice(string name) => name switch
    {
        "pad" => BindingDevice.Pad,
        "keyboard" => BindingDevice.Keyboard,
        _ => throw new FormatException($"a binding for the device \"{name}\", which this Launcher does not know"),
    };
}

/// <summary>One player setting as the game's own export describes it (src/settings/schema.h).</summary>
public sealed record SettingDescription(
    string Key, string Kind, string Default, IReadOnlyList<string> Choices, IReadOnlyList<string> ChoiceLabels,
    string AppliesWhen, string Renderers, string Section, string Label, int Order, string Help, BindingDescription? Binding)
{
    public bool IsBool => Kind == "bool";
    public bool IsChoice => Kind == "choice";
    public bool IsText => Kind == "text";

    /// <summary>Whether the setting does anything on the renderer ("native" or "classic"), as the game decides it (Setting::AvailableOn).</summary>
    public bool AvailableOn(string renderer) => Renderers == "both" || renderer == SettingsSchema.NativeRenderer;

    /// <summary>The choice as the in-game page names it; the value itself for a text, "On"/"Off" for a bool.</summary>
    public string LabelOf(string value)
    {
        if (IsBool) return value == "true" ? "On" : "Off";
        var index = Choices.ToList().IndexOf(value);
        return index >= 0 && index < ChoiceLabels.Count ? ChoiceLabels[index] : value;
    }

    /// <summary>Whether the game would accept the value (Setting::Allows in src/settings/registry.h).</summary>
    public bool Allows(string value) => Kind switch
    {
        "bool" => value is "true" or "false",
        "text" => value.IndexOfAny(['"', '\\', '\r', '\n']) < 0,
        _ => Choices.Contains(value),
    };
}

/// <summary>A Graphics Preset as the game exports it: its value, its label and the value of each setting it covers.</summary>
public sealed record PresetDescription(string Value, string Label, IReadOnlyList<KeyValuePair<string, string>> Values);

/// <summary>
/// The game's list of settings, read from <c>topspin4.exe --print-settings-schema</c>. The Launcher
/// keeps no list of its own: the sections, rows, labels, choices, defaults, help and Presets are
/// what the export says.
/// </summary>
public sealed class SettingsSchema
{
    /// <summary>The export format this Launcher reads; the game says which one it prints.</summary>
    public const int SupportedFormat = 2;

    /// <summary>The renderer value the game's Native-only settings need (<c>kRendererNative</c> in src/settings/registry.h).</summary>
    public const string NativeRenderer = "native";

    /// <summary>The renderer setting's key, whose value says which renderer the game will draw with.</summary>
    public const string RendererKey = "ts4_renderer";

    public IReadOnlyList<SettingDescription> Settings { get; }

    /// <summary>The sections of the in-game Settings page, in the order it shows them.</summary>
    public IReadOnlyList<string> Sections { get; }

    /// <summary>The Graphics Presets in page order; custom is derived, never listed.</summary>
    public IReadOnlyList<PresetDescription> Presets { get; }

    /// <summary>The section whose rows start with the Preset row.</summary>
    public string PresetSection { get; }

    public string PresetLabel { get; }

    public string PresetHelp { get; }

    /// <summary>The version the game writes into the Settings file (<c>settings_file_version</c>); the fallback when the export has none.</summary>
    public long FileVersion { get; }

    private SettingsSchema(IReadOnlyList<SettingDescription> settings, IReadOnlyList<string> sections,
        IReadOnlyList<PresetDescription> presets, string presetSection, string presetLabel, string presetHelp, long fileVersion)
    {
        Settings = settings;
        Sections = sections;
        Presets = presets;
        PresetSection = presetSection;
        PresetLabel = presetLabel;
        PresetHelp = presetHelp;
        FileVersion = fileVersion;
    }

    public static SettingsSchema Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var format = root.GetProperty("format").GetInt32();
            if (format != SupportedFormat)
                throw new SettingsExportException($"The game describes its settings in format {format}; this Launcher reads format {SupportedFormat}. Update the Launcher and the game together.");
            static List<string> Strings(JsonElement array) => array.EnumerateArray().Select(item => item.GetString()!).ToList();
            var settings = new List<SettingDescription>();
            foreach (var item in root.GetProperty("settings").EnumerateArray())
            {
                var binding = item.GetProperty("binding");
                settings.Add(new SettingDescription(
                    item.GetProperty("key").GetString()!,
                    item.GetProperty("kind").GetString()!,
                    item.GetProperty("default").GetString()!,
                    Strings(item.GetProperty("choices")),
                    Strings(item.GetProperty("choice_labels")),
                    item.GetProperty("applies_when").GetString()!,
                    item.GetProperty("renderers").GetString()!,
                    item.GetProperty("section").GetString()!,
                    item.GetProperty("label").GetString()!,
                    item.GetProperty("order").GetInt32(),
                    item.GetProperty("help").GetString()!,
                    binding.ValueKind == JsonValueKind.Null
                        ? null
                        : new BindingDescription(BindingDescription.ParseDevice(binding.GetProperty("device").GetString()!), binding.GetProperty("action").GetString()!)));
            }
            var presets = root.GetProperty("presets").EnumerateArray().Select(preset => new PresetDescription(
                preset.GetProperty("value").GetString()!,
                preset.GetProperty("label").GetString()!,
                preset.GetProperty("values").EnumerateObject()
                    .Select(member => new KeyValuePair<string, string>(member.Name, member.Value.GetString()!)).ToList())).ToList();
            var fileVersion = root.TryGetProperty("settings_file_version", out var version) && version.TryGetInt64(out var number) && number >= 1
                ? number
                : SettingsFile.FallbackVersion;
            return new SettingsSchema(settings, Strings(root.GetProperty("sections")), presets,
                root.GetProperty("preset_section").GetString()!, root.GetProperty("preset_label").GetString()!,
                root.GetProperty("preset_help").GetString()!, fileVersion);
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new SettingsExportException("The game's list of settings could not be read: " + error.Message, error);
        }
    }
}

public sealed class SettingsExportException(string message, Exception? inner = null) : Exception(message, inner);
