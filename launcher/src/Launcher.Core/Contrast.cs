using System.Globalization;

namespace TopSpin4.Launcher.Core;

/// <summary>WCAG 2 colour contrast on <c>#RRGGBB</c> and <c>#AARRGGBB</c> text, so the look's colours can be checked without a window.</summary>
public static class Contrast
{
    /// <summary>The contrast ratio of two opaque colours, 1 to 21 (WCAG 2: relative luminance, +0.05 on each).</summary>
    public static double Ratio(string a, string b)
    {
        var (x, y) = (Luminance(a), Luminance(b));
        if (x < y) (x, y) = (y, x);
        return (x + 0.05) / (y + 0.05);
    }

    /// <summary>The opacity of a colour: the <c>AA</c> of <c>#AARRGGBB</c>, 0 to 1; 1 for <c>#RRGGBB</c>.</summary>
    public static double Alpha(string colour) => Channels(colour).A / 255.0;

    /// <summary><paramref name="top"/> laid over the opaque <paramref name="under"/>, as <c>#RRGGBB</c>.</summary>
    public static string Over(string top, string under)
    {
        var (a, r, g, b) = Channels(top);
        var below = Channels(under);
        int Mix(int over, int beneath) => (int)Math.Round((over * a + beneath * (255 - a)) / 255.0);
        return $"#{Mix(r, below.R):X2}{Mix(g, below.G):X2}{Mix(b, below.B):X2}";
    }

    private static double Luminance(string colour)
    {
        var (_, r, g, b) = Channels(colour);
        static double Linear(int channel)
        {
            var c = channel / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linear(r) + 0.7152 * Linear(g) + 0.0722 * Linear(b);
    }

    private static (int A, int R, int G, int B) Channels(string colour)
    {
        if (colour is not ['#', ..] || colour.Length is not (7 or 9)) throw new FormatException($"Not #RRGGBB or #AARRGGBB: {colour}");
        var hex = colour[1..];
        var a = hex.Length == 8 ? int.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture) : 255;
        var rgb = hex[^6..];
        return (a, Part(rgb, 0), Part(rgb, 2), Part(rgb, 4));
    }

    private static int Part(string rgb, int at) => int.Parse(rgb.AsSpan(at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
