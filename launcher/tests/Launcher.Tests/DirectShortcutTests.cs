using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

public sealed class DirectShortcutTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ts4-shortcut-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Desktop => Path.Combine(_root, "Desktop");
    private string StartMenu => Path.Combine(_root, "Start Menu", "Programs");
    private string GameExe => Path.Combine(_root, "game", LaunchArguments.GameExeName);

    // Writes a file the way a real shortcut writer does, and refuses to add a second one beside it.
    private sealed class FileWriter : IShortcutWriter
    {
        public List<(string LinkPath, string Target, string Arguments, string WorkingDirectory, string IconPath)> Calls { get; } = [];

        public void Write(string linkPath, string target, string arguments, string workingDirectory, string iconPath)
        {
            Calls.Add((linkPath, target, arguments, workingDirectory, iconPath));
            if (File.Exists(linkPath)) throw new IOException("A shortcut was already there: " + linkPath);
            File.WriteAllText(linkPath, target);
        }
    }

    private DirectShortcut Shortcut(FileWriter writer) => new(writer, [Desktop, StartMenu]);

    [Fact]
    public void The_shortcut_targets_the_game_exe_with_no_arguments_and_the_exes_folder_as_working_folder()
    {
        var writer = new FileWriter();

        Shortcut(writer).Create(GameExe);

        Assert.All(writer.Calls, call =>
        {
            Assert.Equal(Path.GetFullPath(GameExe), call.Target);
            Assert.Equal("", call.Arguments);
            Assert.Equal(Path.GetFullPath(Path.GetDirectoryName(GameExe)!), call.WorkingDirectory);
        });
    }

    [Fact]
    public void A_shortcut_named_Top_Spin_4_goes_on_the_desktop_and_in_the_start_menu()
    {
        var writer = new FileWriter();

        var created = Shortcut(writer).Create(GameExe);

        Assert.Equal([Path.Combine(Desktop, "Top Spin 4.lnk"), Path.Combine(StartMenu, "Top Spin 4.lnk")], created);
        // Written beside the final file (a new one, moved over the old), so a failed write never costs the old shortcut.
        Assert.Equal(created.Select(Path.GetDirectoryName), writer.Calls.Select(call => Path.GetDirectoryName(call.LinkPath)));
        Assert.All(created, link => Assert.True(File.Exists(link)));
    }

    [Fact]
    public void Creating_it_again_replaces_the_old_shortcut_rather_than_adding_one()
    {
        var writer = new FileWriter();
        var shortcut = Shortcut(writer);
        shortcut.Create(GameExe);

        var moved = Path.Combine(_root, "moved", LaunchArguments.GameExeName);
        shortcut.Create(moved);

        Assert.Single(Directory.GetFiles(Desktop));
        Assert.Single(Directory.GetFiles(StartMenu));
        Assert.Equal(Path.GetFullPath(moved), File.ReadAllText(Path.Combine(Desktop, "Top Spin 4.lnk")));
        Assert.Equal(4, writer.Calls.Count);
    }

    [Fact]
    public void The_icon_is_the_ico_in_the_art_folder_when_it_exists()
    {
        var ico = Path.Combine(_root, "art", DiscArtFolder.IconFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(ico)!);
        File.WriteAllBytes(ico, [0, 0, 1, 0, 0, 0]);
        var writer = new FileWriter();

        Shortcut(writer).Create(GameExe, ico);

        Assert.Equal(2, writer.Calls.Count);
        Assert.All(writer.Calls, call => Assert.Equal(Path.GetFullPath(ico), call.IconPath));
        Assert.All(writer.Calls, call => Assert.Equal(Path.GetFullPath(GameExe), call.Target));
    }

    [Fact]
    public void The_icon_is_the_exe_when_the_art_folder_has_no_ico()
    {
        var missing = Path.Combine(_root, "art", DiscArtFolder.IconFileName);
        var writer = new FileWriter();

        Shortcut(writer).Create(GameExe, missing);

        Assert.All(writer.Calls, call => Assert.Equal(Path.GetFullPath(GameExe), call.IconPath));
    }

    [Fact]
    public void The_icon_is_the_exe_when_there_is_no_art_at_all()
    {
        var writer = new FileWriter();

        Shortcut(writer).Create(GameExe, null);
        Shortcut(writer).Create(GameExe);

        Assert.All(writer.Calls, call => Assert.Equal(Path.GetFullPath(GameExe), call.IconPath));
    }

    // Fails the way a locked or full folder does, after the shell has been asked to write.
    private sealed class FailingWriter : IShortcutWriter
    {
        public void Write(string linkPath, string target, string arguments, string workingDirectory, string iconPath) =>
            throw new IOException("The disk is full.");
    }

    [Fact]
    public void A_failed_write_keeps_the_old_shortcut_and_leaves_no_stray_file()
    {
        Shortcut(new FileWriter()).Create(GameExe);
        var old = Path.Combine(Desktop, "Top Spin 4.lnk");
        Assert.Equal(Path.GetFullPath(GameExe), File.ReadAllText(old));

        var moved = Path.Combine(_root, "moved", LaunchArguments.GameExeName);
        Assert.Throws<IOException>(() => new DirectShortcut(new FailingWriter(), [Desktop, StartMenu]).Create(moved));

        Assert.Equal(Path.GetFullPath(GameExe), File.ReadAllText(old));
        Assert.Equal([old], Directory.GetFiles(Desktop));
        Assert.Single(Directory.GetFiles(StartMenu));
    }

    [Fact]
    public void A_folder_that_cannot_be_written_is_reported_and_the_other_one_is_still_written()
    {
        var blocked = Path.Combine(_root, "blocked");
        File.WriteAllText(blocked, "a file where a folder should be");
        var writer = new FileWriter();

        var shortcut = new DirectShortcut(writer, [blocked, StartMenu]);

        var failure = Assert.Throws<IOException>(() => shortcut.Create(GameExe));
        Assert.Contains("Top Spin 4", failure.Message);
        Assert.True(File.Exists(Path.Combine(StartMenu, "Top Spin 4.lnk")));
    }
}
