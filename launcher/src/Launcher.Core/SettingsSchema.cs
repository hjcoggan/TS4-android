using System.Text.Json;

namespace TopSpin4.Launcher.Core;

/// <summary>One player setting as the game's own export describes it (src/settings/schema.h).</summary>
public sealed record SettingDescription(
    string Key, string Kind, string Default, IReadOnlyList<string> Choices,
    string AppliesWhen, string Renderers, string Section, string Label, int Order, string Help)
{
    public bool IsBool => Kind == "bool";
    public bool IsChoice => Kind == "choice";

    /// <summary>Whether the game would accept the value (Setting::Allows in src/settings/registry.h).</summary>
    public bool Allows(string value) => Kind switch
    {
        "bool" => value is "true" or "false",
        "text" => value.IndexOfAny(['"', '\\', '\r', '\n']) < 0,
        _ => Choices.Contains(value),
    };

    /// <summary>The value after <paramref name="current"/> when a player presses the setting: the next choice, or the other bool.</summary>
    public string Next(string current)
    {
        if (IsBool) return current == "true" ? "false" : "true";
        if (Choices.Count == 0) return current;
        var index = Choices.ToList().IndexOf(current);
        return Choices[(index + 1) % Choices.Count];
    }
}

/// <summary>
/// The game's list of settings, read from <c>topspin4.exe --print-settings-schema</c>. The Launcher
/// keeps no list of its own: it shows what the export says and picks the before-start ones from it.
/// </summary>
public sealed class SettingsSchema
{
    /// <summary>The export format this Launcher reads; the game says which one it prints.</summary>
    public const int SupportedFormat = 1;

    public IReadOnlyList<SettingDescription> Settings { get; }

    /// <summary>The version the game writes into the Settings file (<c>settings_file_version</c>); the fallback when the export has none.</summary>
    public long FileVersion { get; }

    private SettingsSchema(IReadOnlyList<SettingDescription> settings, long fileVersion)
    {
        Settings = settings;
        FileVersion = fileVersion;
    }

    /// <summary>
    /// The settings that only make sense before the game starts, in page order: those the export
    /// marks <c>applies_when == "next_start"</c> in the Graphics section (the renderer and, when
    /// the game gains one, the GPU). Controls bindings also apply at next start, but remapping is
    /// the Settings page's job, not the Launcher's.
    /// </summary>
    public IReadOnlyList<SettingDescription> BeforeStart() =>
        Settings.Where(setting => setting.AppliesWhen == "next_start" && setting.Section == "Graphics"
                                  && setting.Kind is "choice" or "bool")
            .OrderBy(setting => setting.Order)
            .ToList();

    public static SettingsSchema Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var format = root.GetProperty("format").GetInt32();
            if (format != SupportedFormat)
                throw new SettingsExportException($"The game describes its settings in format {format}; this Launcher reads format {SupportedFormat}. Update the Launcher and the game together.");
            var settings = new List<SettingDescription>();
            foreach (var item in root.GetProperty("settings").EnumerateArray())
            {
                settings.Add(new SettingDescription(
                    item.GetProperty("key").GetString()!,
                    item.GetProperty("kind").GetString()!,
                    item.GetProperty("default").GetString()!,
                    item.GetProperty("choices").EnumerateArray().Select(choice => choice.GetString()!).ToList(),
                    item.GetProperty("applies_when").GetString()!,
                    item.GetProperty("renderers").GetString()!,
                    item.GetProperty("section").GetString()!,
                    item.GetProperty("label").GetString()!,
                    item.GetProperty("order").GetInt32(),
                    item.GetProperty("help").GetString()!));
            }
            var fileVersion = root.TryGetProperty("settings_file_version", out var version) && version.TryGetInt64(out var number) && number >= 1
                ? number
                : SettingsFile.FallbackVersion;
            return new SettingsSchema(settings, fileVersion);
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new SettingsExportException("The game's list of settings could not be read: " + error.Message, error);
        }
    }
}

public sealed class SettingsExportException(string message, Exception? inner = null) : Exception(message, inner);
