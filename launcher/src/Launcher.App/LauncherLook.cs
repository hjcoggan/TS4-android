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
    /// A grey copy of a picture, for a locked achievement; null when its pixels are in a layout this does not read (the
    /// page then dims the coloured picture instead).
    /// </summary>
    public static unsafe Bitmap? Grayscale(Bitmap source)
    {
        if (source.Format is not { } format || source.AlphaFormat is not { } alpha) return null;
        var red = format == Avalonia.Platform.PixelFormat.Bgra8888 ? 2 : format == Avalonia.Platform.PixelFormat.Rgba8888 ? 0 : -1;
        if (red < 0) return null;
        var size = source.PixelSize;
        WriteableBitmap? target = null;
        try
        {
            target = new WriteableBitmap(size, source.Dpi, format, alpha);
            using (var buffer = target.Lock())
            {
                source.CopyPixels(new PixelRect(size), buffer.Address, buffer.RowBytes * size.Height, buffer.RowBytes);
                for (var y = 0; y < size.Height; y++)
                {
                    var row = (byte*)buffer.Address + y * buffer.RowBytes;
                    for (var x = 0; x < size.Width; x++)
                    {
                        var pixel = row + x * 4;
                        var grey = (byte)((pixel[red] * 77 + pixel[1] * 150 + pixel[2 - red] * 29) >> 8);
                        pixel[0] = pixel[1] = pixel[2] = grey;
                    }
                }
            }
            return target;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            target?.Dispose();
            return null;
        }
    }

    /// <summary>An art file decoded at <paramref name="width"/> pixels wide (a card shows it small); null and a log line when it cannot be read.</summary>
    public static Bitmap? LoadScaled(string path, int width, Action<string> log)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Bitmap.DecodeToWidth(stream, width, BitmapInterpolationMode.HighQuality);
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
