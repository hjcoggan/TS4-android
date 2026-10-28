namespace TopSpin4.Launcher.Core;

/// <summary>
/// The before-start settings (those the game's export marks next_start in Graphics: the
/// renderer, the GPU) over the Settings file. The Launcher has no list of settings of its own: the
/// rows, labels, choices, defaults and help text all come from the export, and the values from
/// the file the game itself reads, so the Launcher and the in-game Settings page agree.
/// </summary>
public sealed class BeforeStartSettings(SettingsSchema schema, string settingsPath)
{
    public IReadOnlyList<SettingDescription> Settings { get; } = schema.BeforeStart();

    public string SettingsPath { get; } = settingsPath;

    /// <summary>The value the game will read: the file's, or the default when it has none or the game would refuse it.</summary>
    public string ValueOf(SettingDescription setting)
    {
        var text = File.Exists(SettingsPath) ? File.ReadAllText(SettingsPath) : "";
        return SettingsFile.ValueOf(text, setting.Key) is { } value && setting.Allows(value) ? value : setting.Default;
    }

    /// <summary>Writes the value into the Settings file; a value the game would refuse is not written.</summary>
    public SettingChange Set(SettingDescription setting, string value)
    {
        if (!setting.Allows(value)) return SettingChange.Refused;
        var text = File.Exists(SettingsPath) ? File.ReadAllText(SettingsPath) : "";
        if (SettingsFile.IsNewerThanBuild(text)) return SettingChange.FileIsNewer;
        return SettingsFile.Save(SettingsPath, setting.Key, SettingsFile.Literal(setting, value))
            ? SettingChange.Saved
            : SettingChange.NotWritten;
    }

    /// <summary>Moves the setting to its next choice (the other value for a bool) and writes it.</summary>
    public SettingChange Cycle(SettingDescription setting) => Set(setting, setting.Next(ValueOf(setting)));

    public static string Describe(SettingChange change) => change switch
    {
        SettingChange.Saved => "",
        SettingChange.FileIsNewer => "Your settings file was written by a newer version of the game, so the Launcher left it alone. Change this setting in the game.",
        SettingChange.Refused => "The game does not allow that value.",
        _ => "The settings file could not be written.",
    };
}

public enum SettingChange { Saved, Refused, FileIsNewer, NotWritten }
