using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace TopSpin4.Launcher;

/// <summary>
/// The Launcher's own line icons, drawn on a 24-unit grid as stroked paths in the colour of the control that holds them.
/// Original work, not taken from any icon set or from the game.
/// </summary>
internal static class Icons
{
    public const string Home = "M4,11 L12,4 L20,11 M6,9.5 V20 H10 V14 H14 V20 H18 V9.5";
    public const string Settings = "M4,7 H20 M4,12 H20 M4,17 H20 M8,5 V9 M15,10 V14 M10,15 V19";
    public const string Trophy = "M7,4 H17 V9 A5,5 0 0 1 7,9 Z M12,14 V18 M8,20 H16 M7,6 H4 V8 A3,3 0 0 0 7,11 M17,6 H20 V8 A3,3 0 0 1 17,11";
    public const string Help = "M12,3 A9,9 0 1 1 11.99,3 M9.5,9.5 A2.5,2.5 0 1 1 12,12 V14 M12,17 V17.2";
    public const string Folder = "M3,6 H10 L12,8 H21 V19 H3 Z";
    public const string Shortcut = "M14,4 H20 V10 M20,4 L11,13 M18,14 V20 H4 V6 H10";
    public const string Again = "M20,12 A8,8 0 1 1 17.66,6.34 M20,4 V8 H16";
    public const string Warning = "M12,3 L22,20 H2 Z M12,9 V14 M12,17 V17.2";
    public const string Shield = "M12,3 L20,6 V12 C20,17 16,20 12,21 C8,20 4,17 4,12 V6 Z";
    public const string Back = "M15,5 L8,12 L15,19";
    public const string Padlock = "M6,11 H18 V20 H6 Z M8.5,11 V8 A3.5,3.5 0 0 1 15.5,8 V11";

    /// <summary>A 24x24 icon (drawn at <paramref name="size"/>) whose stroke follows the Foreground of the button it is in.</summary>
    public static Control Make(string data, double size = 24)
    {
        var path = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(data),
            StrokeThickness = 2,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Stretch = Stretch.None,
        };
        path.Bind(Shape.StrokeProperty, new Binding(nameof(TemplatedControl.Foreground))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(TemplatedControl) },
        });
        return new Viewbox
        {
            Width = size,
            Height = size,
            Child = new Canvas { Width = 24, Height = 24, Children = { path } },
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }
}
