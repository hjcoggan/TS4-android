namespace TopSpin4.Launcher.Core;

/// <summary>The pictures the Launcher takes from the player's disc, one per place it shows them.</summary>
public enum DiscArtSlot
{
    /// <summary>The Home page's background (the dashboard background in <c>nxeart</c>).</summary>
    Background,
    /// <summary>The game tile beside PLAY (the dashboard slot art in <c>nxeart</c>).</summary>
    BoxArt,
    /// <summary>The Top Spin 4 logo, with transparency (<c>PressStart.xzp</c>).</summary>
    Logo,
    /// <summary>The title icon in <c>default.xex</c>; the window, taskbar and shortcut icon.</summary>
    TitleIcon,
    /// <summary>The calmer picture behind Setup and Settings (<c>PressStart.xzp</c>).</summary>
    SetupBackground,
}

/// <summary>The disc an art folder was read from, as its default.xex names it.</summary>
internal readonly record struct DiscIdentity(uint MediaId, uint TitleId);

/// <summary>What one run of the Disc art module found and what stopped it finding more.</summary>
/// <param name="Found">The slots now filled in the art folder.</param>
/// <param name="Problems">Why a source gave nothing, in the words that were also logged. A source the disc simply lacks is no problem.</param>
public sealed record DiscArtResult(IReadOnlyList<DiscArtSlot> Found, IReadOnlyList<string> Problems);

/// <summary>
/// The Disc art module. Reads the Launcher's pictures from the player's own disc, the
/// <c>.iso</c> or the prepared disc folder, and writes them to an art folder in the per-user
/// folder (<see cref="DiscArtFolder"/> reads them back). Each source stands alone: one that the
/// disc lacks gives nothing and no error; one that is damaged is logged and gives nothing,
/// and the others still count. Nothing from the disc is kept in the repository or the
/// Launcher itself (ADR-0006).
/// </summary>
public static class DiscArt
{
    /// <summary>The name of the art folder inside the per-user folder.</summary>
    public const string FolderName = "art";

    private const string NxeArt = "nxeart";
    private const string PressStart = "Media/XUI/PressStart.xzp";
    private const string Module = "default.xex";

    /// <summary>Reads the art from a disc image and writes it to <paramref name="artFolder"/>.</summary>
    public static DiscArtResult FromIso(string isoPath, string artFolder, Action<string>? log = null)
    {
        IsoFileSource? source;
        try
        {
            source = IsoFileSource.TryOpen(isoPath);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return Failed(log, $"Disc art: the disc image {isoPath} could not be read: {error.Message}");
        }
        if (source is null) return Failed(log, $"Disc art: {isoPath} is not an Xbox 360 disc image.");
        using (source) return Run(source, isoPath, artFolder, log);
    }

    /// <summary>Reads the art from the prepared disc folder (the extracted game partition) and writes it to <paramref name="artFolder"/>.</summary>
    public static DiscArtResult FromFolder(string discFolder, string artFolder, Action<string>? log = null)
    {
        if (!Directory.Exists(discFolder)) return Failed(log, $"Disc art: the disc folder {discFolder} was not found.");
        using var source = new FolderFileSource(discFolder);
        return Run(source, discFolder, artFolder, log);
    }

    private static DiscArtResult Failed(Action<string>? log, string problem)
    {
        log?.Invoke(problem);
        return new DiscArtResult([], [problem]);
    }

    private static DiscArtResult Run(IDiscFileSource disc, string origin, string artFolder, Action<string>? log)
    {
        var problems = new List<string>();
        void Problem(string text)
        {
            var line = "Disc art: " + text;
            problems.Add(line);
            log?.Invoke(line);
        }

        var images = new Dictionary<DiscArtSlot, byte[]>();
        ReadNxeArt(disc, images, Problem);
        ReadPressStart(disc, images, Problem);
        var module = ReadModule(disc, images, Problem);
        var found = DiscArtFolder.Write(artFolder, images, origin, module, Problem);
        return new DiscArtResult(found, problems);
    }

    // The press-start screen's package: the Top Spin 4 logo and a plain picture, in a XUI package.
    private static void ReadPressStart(IDiscFileSource disc, Dictionary<DiscArtSlot, byte[]> images, Action<string> problem)
    {
        try
        {
            if (disc.Read(PressStart) is not { } bytes) return;
            var package = XuizPackage.Parse(bytes);
            foreach (var (slot, name) in new[] { (DiscArtSlot.Logo, "Bitmaps\\Logo.PNG"), (DiscArtSlot.SetupBackground, "Bitmaps\\PressStart.PNG") })
                if (package.Find(name) is { } entry) Accept(images, slot, $"{PressStart}: {name}", entry, problem);
        }
        catch (Exception error) when (error is IOException or InvalidDataException)
        {
            problem($"{PressStart} is damaged: {error.Message}");
        }
    }

    // default.xex: its title image is the icon. Its execution info also names the disc the art came from.
    private static DiscIdentity? ReadModule(IDiscFileSource disc, Dictionary<DiscArtSlot, byte[]> images, Action<string> problem)
    {
        byte[]? bytes;
        try
        {
            bytes = disc.Read(Module);
        }
        catch (Exception error) when (error is IOException or InvalidDataException)
        {
            problem($"{Module} could not be read: {error.Message}");
            return null;
        }
        if (bytes is null) return null;
        var identity = XexHeader.Parse(bytes)?.Execution is { } info ? new DiscIdentity(info.MediaId, info.TitleId) : (DiscIdentity?)null;
        try
        {
            if (XexTitleImage.Read(bytes) is { } icon) Accept(images, DiscArtSlot.TitleIcon, $"{Module}: title image", icon, problem);
        }
        catch (InvalidDataException error)
        {
            problem($"{Module} is damaged: {error.Message}");
        }
        return identity;
    }

    // The dashboard artwork: nxeart is an STFS package holding nxebg.jpg and nxeslot.jpg.
    private static void ReadNxeArt(IDiscFileSource disc, Dictionary<DiscArtSlot, byte[]> images, Action<string> problem)
    {
        try
        {
            if (disc.Read(NxeArt) is not { } bytes) return;
            using var package = StfsPackage.Open(bytes, NxeArt);
            package.Verify(null, CancellationToken.None);
            foreach (var (slot, name) in new[] { (DiscArtSlot.Background, "nxebg.jpg"), (DiscArtSlot.BoxArt, "nxeslot.jpg") })
                if (package.Find(name) is { } file) Accept(images, slot, $"{NxeArt}/{name}", package.ReadAll(file), problem);
        }
        catch (Exception error) when (error is IOException or InvalidDataException)
        {
            problem($"{NxeArt} is damaged: {error.Message}");
        }
    }

    // Keeps an entry when it really is the image it should be; the slot stays empty otherwise.
    private static void Accept(Dictionary<DiscArtSlot, byte[]> images, DiscArtSlot slot, string name, byte[] bytes, Action<string> problem)
    {
        try
        {
            ArtImageFile.Describe(bytes);
            images[slot] = bytes;
        }
        catch (InvalidDataException error)
        {
            problem($"{name} was not used: {error.Message}");
        }
    }
}
