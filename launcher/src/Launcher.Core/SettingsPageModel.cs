namespace TopSpin4.Launcher.Core;

/// <summary>One choice of a setting, with the label the player sees.</summary>
public sealed record SettingOption(string Value, string Label);

/// <summary>One row of the Settings page: a setting, its label, current value, help and choices.</summary>
public sealed record SettingRow(SettingDescription Setting, string Label, string Help, string Value, string ValueLabel,
    IReadOnlyList<SettingOption> Options);

/// <summary>
/// The Settings page as a model with no UI. The rows come from the game's settings export (the
/// Launcher keeps no list of its own) and the values from the Settings file the game reads, so
/// the page and the in-game Settings page agree. Today it shows the before-start settings; the
/// rest of the export arrives in later tickets. A change is written to the Settings file at once.
/// </summary>
public sealed class SettingsPageModel
{
    /// <summary>Plain words for the choices whose raw values mean little to a player.</summary>
    private static readonly Dictionary<(string Key, string Value), string> ChoiceLabels = new()
    {
        [("ts4_renderer", "native")] = "Native (recommended)",
        [("ts4_renderer", "classic")] = "Classic (compatibility)",
    };

    private readonly BeforeStartSettings? _settings;

    private SettingsPageModel(BeforeStartSettings? settings, string? unavailableReason)
    {
        _settings = settings;
        UnavailableReason = unavailableReason;
    }

    /// <summary>The page for a settings export read from the game.</summary>
    public static SettingsPageModel From(SettingsSchema schema, string settingsPath) =>
        new(new BeforeStartSettings(schema, settingsPath), null);

    /// <summary>The page when the game's settings could not be read: no rows, and the reason.</summary>
    public static SettingsPageModel Unavailable(string reason) => new(null, reason);

    /// <summary>Why the settings cannot be shown, or null when they can.</summary>
    public string? UnavailableReason { get; }

    /// <summary>Said under the rows: the rest of the settings are in game, and how to open them (Glossary: Settings page).</summary>
    public string InGameNote =>
        "More settings are in the game: choose PC SETTINGS in OPTIONS, press F4, or hold Back and Start on a controller.";

    public IReadOnlyList<SettingRow> Rows => _settings is null ? [] : _settings.Settings.Select(Row).ToList();

    /// <summary>Writes the chosen value of a row's setting into the Settings file.</summary>
    public SettingChange Choose(SettingRow row, string value) => _settings!.Set(row.Setting, value);

    private SettingRow Row(SettingDescription setting)
    {
        var value = _settings!.ValueOf(setting);
        var options = (setting.IsBool ? new[] { "true", "false" } : setting.Choices.ToArray())
            .Select(choice => new SettingOption(choice, LabelOf(setting, choice))).ToList();
        return new SettingRow(setting, setting.Label, setting.Help, value, LabelOf(setting, value), options);
    }

    private static string LabelOf(SettingDescription setting, string value) =>
        ChoiceLabels.TryGetValue((setting.Key, value), out var label) ? label
        : setting.IsBool ? (value == "true" ? "On" : "Off")
        : value;
}
