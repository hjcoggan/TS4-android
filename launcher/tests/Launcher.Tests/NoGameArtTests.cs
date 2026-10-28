using System.Xml.Linq;

namespace TopSpin4.Launcher.Tests;

/// <summary>The Launcher holds no game art (ADR-0006): the art is read from the player's disc at run time, never bundled.</summary>
public sealed class NoGameArtTests
{
    private static string LauncherRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "launcher", "TopSpin4Launcher.slnx"))) return Path.Combine(dir.FullName, "launcher");
        throw new InvalidOperationException("The launcher folder was not found above " + AppContext.BaseDirectory);
    }

    [Fact]
    public void The_app_bundles_only_fonts_and_their_licences()
    {
        var assets = Path.Combine(LauncherRoot(), "src", "Launcher.App", "Assets");

        var files = Directory.EnumerateFiles(assets, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(assets, file).Replace('\\', '/'))
            .Where(file => !file.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) && !file.EndsWith("-OFL.txt", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(files);
    }

    [Fact]
    public void The_app_project_declares_no_resource_but_fonts()
    {
        var project = XDocument.Load(Path.Combine(LauncherRoot(), "src", "Launcher.App", "Launcher.App.csproj"));

        var bundled = project.Descendants()
            .Where(element => element.Name.LocalName is "AvaloniaResource" or "EmbeddedResource" or "Content" or "Resource" or "None" or "ApplicationIcon")
            .Select(element => (string?)element.Attribute("Include") ?? element.Value)
            .Where(include => !include.StartsWith("Assets/Fonts/", StringComparison.Ordinal) || !include.EndsWith(".ttf", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(bundled);
    }

    [Fact]
    public void The_axaml_names_no_image_file()
    {
        var app = Path.Combine(LauncherRoot(), "src", "Launcher.App");

        var mentions = Directory.EnumerateFiles(app, "*.axaml", SearchOption.AllDirectories)
            .Where(file => System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(file), @"\.(png|jpe?g|ico|bmp)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(mentions);
    }
}
