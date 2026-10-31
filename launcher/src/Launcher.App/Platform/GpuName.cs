using Microsoft.Win32;

namespace TopSpin4.Launcher.Platform;

/// <summary>
/// The name of the graphics card for the footer, from the display adapters Windows lists in the registry (each adapter's
/// DriverDesc). A discrete card wins over the integrated one, as the game's own device choice prefers it. Null where
/// the registry has no adapter or cannot be read (Wine may list none).
/// </summary>
internal static class GpuName
{
    private const string DisplayClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
    private static readonly string[] Discrete = ["NVIDIA", "AMD", "Radeon", "Arc"];

    public static string? Read()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            using var display = Registry.LocalMachine.OpenSubKey(DisplayClass);
            if (display is null) return null;
            var names = new List<string>();
            foreach (var adapter in display.GetSubKeyNames().Where(name => name.All(char.IsAsciiDigit)).Order())
            {
                using var key = display.OpenSubKey(adapter);
                if (key?.GetValue("DriverDesc") is string name && !name.Contains("Microsoft Basic", StringComparison.OrdinalIgnoreCase)
                    && !name.Contains("Remote", StringComparison.OrdinalIgnoreCase))
                    names.Add(name.Trim());
            }
            return names.FirstOrDefault(name => Discrete.Any(maker => name.Contains(maker, StringComparison.OrdinalIgnoreCase))) ?? names.FirstOrDefault();
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }
}
