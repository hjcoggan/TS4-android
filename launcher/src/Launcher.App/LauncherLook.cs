using System.Buffers.Binary;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher;

/// <summary>
/// Puts a <see cref="LauncherTheme"/> on screen: the colours go into the brushes <c>Theme.axaml</c> declares (changed in
/// place, so every control that uses them follows), and the art files are read into bitmaps. The Launcher itself holds no
/// picture: every image comes from the player's art folder. What to show is the theme's decision, not this class's: a
/// picture that cannot be read is reported back to <see cref="LauncherTheme.From"/>, which then counts it as missing.
/// </summary>
internal static class LauncherLook
{
    /// <summary>Sets the declared brushes to <paramref name="palette"/>.</summary>
    public static void Apply(ThemePalette palette)
    {
        var app = Application.Current ?? throw new InvalidOperationException("The look is applied once the application exists.");
        foreach (var (key, colour) in palette.Brushes())
        {
            if (app.TryFindResource(key, out var resource) && resource is SolidColorBrush brush) brush.Color = Color.Parse(colour);
            else throw new InvalidOperationException($"Theme.axaml declares no SolidColorBrush named {key}.");
        }
    }

    /// <summary>An art file as a bitmap; null (and a log line) when it cannot be read, so the theme counts it as missing.</summary>
    public static Bitmap? Load(string path, Action<string> log)
    {
        try
        {
            return new Bitmap(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            log($"Disc art: {path} could not be drawn: {error.Message}");
            return null;
        }
    }

    /// <summary>
    /// The window icon from the art folder's <c>.ico</c>: the Disc art module writes it as one PNG image, which is what is
    /// drawn. Null when there is none or it cannot be read; the window then keeps the Launcher's own icon.
    /// </summary>
    public static WindowIcon? Icon(string path, Action<string> log)
    {
        try
        {
            if (PngInIco(File.ReadAllBytes(path)) is not { } png) throw new InvalidDataException("it holds no PNG image");
            using var stream = new MemoryStream(png);
            return new WindowIcon(new Bitmap(stream));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or NotSupportedException)
        {
            log($"Disc art: the icon {path} could not be used: {error.Message}");
            return null;
        }
    }

    // ICONDIR (reserved, type 1, count) then ICONDIRENTRYs of 16 bytes: size at 8 and offset at 12 of each entry.
    private static byte[]? PngInIco(byte[] ico)
    {
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47];
        if (ico.Length < 6 || BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(2)) != 1) return null;
        var count = BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(4));
        for (var i = 0; i < count && 6 + (i + 1) * 16 <= ico.Length; i++)
        {
            var entry = 6 + i * 16;
            var size = BinaryPrimitives.ReadInt32LittleEndian(ico.AsSpan(entry + 8));
            var offset = BinaryPrimitives.ReadInt32LittleEndian(ico.AsSpan(entry + 12));
            if (size > 0 && offset >= 0 && offset <= ico.Length - size && ico.AsSpan(offset, size).StartsWith(signature))
                return ico[offset..(offset + size)];
        }
        return null;
    }
}
