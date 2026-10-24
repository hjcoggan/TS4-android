namespace TopSpin4.Launcher.Core;

/// <summary>
/// The one build the port supports (ADR-0002): the Xbox 360 disc with media ID 60819732 and
/// Title Update 1 applied. Facts from docs/research/module-inspection.md.
/// </summary>
public static class ReferenceBuild
{
    public const uint TitleId = 0x54540859;
    public const uint MediaId = 0x60819732;

    /// <summary>The three Modules on the disc: Boot, Loader and Swing.</summary>
    public static IReadOnlyList<string> Modules { get; } = ["default.xex", "Loader_DLL.xex", "Swing_DLL.xex"];

    /// <summary>Every Module's version on the disc.</summary>
    public static ModuleVersion DiscVersion { get; } = new(0x00000003);

    /// <summary>Every Module's version once TU1's patch is applied.</summary>
    public static ModuleVersion TitleUpdateVersion { get; } = new(0x00000103);

    /// <summary>The module patch TU1 carries for a Module ("default.xex" → "default.xexp").</summary>
    public static string PatchOf(string module) => module + "p";
}
