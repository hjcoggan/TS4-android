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

/// <summary>The sets of pictures the Launcher takes from the player's disc, each in its own folder inside the art folder.</summary>
public enum DiscArtCollection
{
    /// <summary>The achievement icons, names and descriptions in <c>default.xex</c>'s title resource.</summary>
    Achievements,
    /// <summary>Player portraits from the game's archives.</summary>
    Players,
    /// <summary>Venue pictures from the game's archives.</summary>
    Venues,
}

/// <summary>One picture of a collection as it is read from the disc.</summary>
/// <param name="Image">A PNG or JPEG.</param>
/// <param name="Points">The gamerscore for an achievement; 0 otherwise.</param>
/// <param name="Id">The achievement's id, which the game's unlock file names it by; 0 otherwise.</param>
/// <param name="HowTo">What to do to earn the achievement, shown while it is locked; empty otherwise.</param>
public sealed record DiscArtItem(byte[] Image, string Title, string Detail, int Points = 0, int Id = 0, string HowTo = "");

/// <summary>One picture of a collection as the art folder holds it.</summary>
public sealed record DiscArtEntry(string ImagePath, string Title, string Detail, int Points, int Id = 0, string HowTo = "");

/// <summary>The disc an art folder was read from, as its default.xex names it.</summary>
internal readonly record struct DiscIdentity(uint MediaId, uint TitleId);

/// <summary>What one run of the Disc art module found and what stopped it finding more.</summary>
/// <param name="Found">The slots now filled in the art folder.</param>
/// <param name="Problems">Why a source gave nothing, in the words that were also logged. A source the disc simply lacks is no problem.</param>
public sealed record DiscArtResult(IReadOnlyList<DiscArtSlot> Found, IReadOnlyList<string> Problems);

/// <summary>
/// The Disc art module. Reads the Launcher's pictures from the player's own disc, the
/// <c>.iso</c> or the prepared disc folder, and writes them to an art folder, <c>userdata\art</c>
/// (<see cref="DiscArtFolder"/> reads them back). Each source stands alone: one that the
/// disc lacks gives nothing and no error; one that is damaged is logged and gives nothing,
/// and the others still count. Nothing from the disc is kept in the repository or the
/// Launcher itself (ADR-0006).
/// </summary>
public static class DiscArt
{
    /// <summary>The name of the art folder inside the user data folder.</summary>
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
        catch (Exception error)
        {
            return Failed(log, $"Disc art: the disc image {isoPath} could not be read: {error.Message}");
        }
        if (source is null) return Failed(log, $"Disc art: {isoPath} is not an Xbox 360 disc image.");
        using (source) return FromSource(source, isoPath, artFolder, log);
    }

    /// <summary>Reads the art from the prepared disc folder (the extracted game partition) and writes it to <paramref name="artFolder"/>.</summary>
    public static DiscArtResult FromFolder(string discFolder, string artFolder, Action<string>? log = null)
    {
        if (!Directory.Exists(discFolder)) return Failed(log, $"Disc art: the disc folder {discFolder} was not found.");
        using var source = new FolderFileSource(discFolder);
        return FromSource(source, discFolder, artFolder, log);
    }

    /// <summary>
    /// Reads the art from the prepared disc folder again when <paramref name="artFolder"/> is missing or incomplete
    /// (<see cref="DiscArtFolder.IsComplete"/>), and does nothing, returning null, when it is complete. It never
    /// throws: whatever stops it is logged, so neither setup nor a start can fail on the art. The check and the read
    /// happen under the folder's writer lock, so a restore that overlaps a disc read does not do the work twice.
    /// </summary>
    public static DiscArtResult? RestoreFromFolder(string discFolder, string artFolder, Action<string>? log = null)
    {
        try
        {
            lock (DiscArtFolder.WriterLock)
            {
                return DiscArtFolder.Open(artFolder).IsComplete ? null : FromFolder(discFolder, artFolder, log);
            }
        }
        catch (Exception error)
        {
            return Failed(log, $"Disc art: could not read the art from {discFolder}: {error.Message}");
        }
    }

    private static DiscArtResult Failed(Action<string>? log, string problem)
    {
        log?.Invoke(problem);
        return new DiscArtResult([], [problem]);
    }

    internal static DiscArtResult FromSource(IDiscFileSource disc, string origin, string artFolder, Action<string>? log)
    {
        var problems = new List<string>();
        void Problem(string text)
        {
            var line = "Disc art: " + text;
            problems.Add(line);
            log?.Invoke(line);
        }

        var images = new Dictionary<DiscArtSlot, byte[]>();
        var collections = new Dictionary<DiscArtCollection, IReadOnlyList<DiscArtItem>>();
        ReadNxeArt(disc, images, Problem);
        ReadPressStart(disc, images, Problem);
        var module = ReadModule(disc, images, collections, Problem);
        ReadArchives(disc, collections, Problem);
        // An index is written for a read that logged nothing; a read with a problem is tried again next time (DiscArtFolder.IsComplete).
        var clean = problems.Count == 0;
        try
        {
            var found = DiscArtFolder.Write(artFolder, images, origin, module, clean, Problem, collections);
            return new DiscArtResult(found, problems);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The folder is as it was (DiscArtFolder.Write changes the index only after the new files are in).
            Problem($"the art could not be written to {artFolder}: {error.Message}");
            return new DiscArtResult([], problems);
        }
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
        catch (Exception error)
        {
            problem($"{PressStart} is damaged: {error.Message}");
        }
    }

    // default.xex: its title image is the icon, and its title resource holds the achievements. Its execution info also
    // names the disc the art came from.
    private static DiscIdentity? ReadModule(IDiscFileSource disc, Dictionary<DiscArtSlot, byte[]> images,
        Dictionary<DiscArtCollection, IReadOnlyList<DiscArtItem>> collections, Action<string> problem)
    {
        byte[]? bytes;
        try
        {
            bytes = disc.Read(Module);
        }
        catch (Exception error)
        {
            problem($"{Module} could not be read: {error.Message}");
            return null;
        }
        if (bytes is null) return null;
        DiscIdentity? identity = null;
        Xdbf? resource;
        try
        {
            // The header is read once: it names the disc, and the title resource is found through it.
            var execution = XexHeader.Parse(bytes)?.Execution;
            if (execution is { } info) identity = new DiscIdentity(info.MediaId, info.TitleId);
            resource = XexTitleImage.ReadResource(bytes, execution);
            if (resource?.Image(XexTitleImage.TitleImageId) is { } icon) Accept(images, DiscArtSlot.TitleIcon, $"{Module}: title image", icon, problem);
        }
        catch (Exception error)
        {
            problem($"{Module} is damaged: {error.Message}");
            return identity;
        }
        if (resource is not null) ReadAchievements(resource, collections, problem);
        return identity;
    }

    // Each achievement with its icon; one whose icon is missing or not a picture is left out, the others still count.
    private static void ReadAchievements(Xdbf resource, Dictionary<DiscArtCollection, IReadOnlyList<DiscArtItem>> collections, Action<string> problem)
    {
        try
        {
            var items = new List<DiscArtItem>();
            foreach (var achievement in resource.Achievements())
            {
                if (resource.Image(achievement.ImageId) is not { } icon) continue;
                try
                {
                    ArtImageFile.Describe(icon);
                }
                catch (Exception error)
                {
                    problem($"{Module}: achievement {achievement.Id}'s icon was not used: {error.Message}");
                    continue;
                }
                items.Add(new DiscArtItem(icon, achievement.Name, achievement.Description.Length > 0 ? achievement.Description : achievement.HowTo,
                    achievement.Gamerscore, achievement.Id, achievement.HowTo.Length > 0 ? achievement.HowTo : achievement.Description));
            }
            if (items.Count > 0) collections[DiscArtCollection.Achievements] = items;
        }
        catch (Exception error)
        {
            problem($"{Module}: the achievements could not be read: {error.Message}");
        }
    }

    // Global.arc: the players' and venues' pictures, decoded from the game's own textures.
    private static void ReadArchives(IDiscFileSource disc, Dictionary<DiscArtCollection, IReadOnlyList<DiscArtItem>> collections, Action<string> problem)
    {
        try
        {
            using var archive = disc.Open(ArchiveArt.Archive);
            if (archive is null) return;
            var (players, venues) = ArchiveArt.Read(archive, ArchiveArt.Roster(disc.TopLevelFiles()), problem);
            if (players.Count > 0) collections[DiscArtCollection.Players] = players;
            if (venues.Count > 0) collections[DiscArtCollection.Venues] = venues;
        }
        catch (Exception error)
        {
            problem($"{ArchiveArt.Archive} is damaged: {error.Message}");
        }
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
        catch (Exception error)
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
        catch (Exception error)
        {
            problem($"{name} was not used: {error.Message}");
        }
    }
}
