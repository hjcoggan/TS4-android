using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

// The portable install (ADR-0009): every default folder is resolved from the Launcher's own folder,
// which sits beside the game exe.
public sealed class UserDirectoriesTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ts4-userdirs-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Prepared_data_and_user_data_default_to_the_launchers_folder()
    {
        Assert.Equal(@"D:\Games\TopSpin4", UserDirectories.PreparedDataRoot(@"D:\Games\TopSpin4"));
        Assert.Equal(@"D:\Games\TopSpin4\userdata", UserDirectories.UserDataRoot(@"D:\Games\TopSpin4"));
    }

    [Fact]
    public void A_trailing_separator_on_the_launchers_folder_changes_nothing()
    {
        // AppContext.BaseDirectory ends with one.
        Assert.Equal(@"D:\Games\TopSpin4", UserDirectories.PreparedDataRoot(@"D:\Games\TopSpin4\"));
        Assert.Equal(@"D:\Games\TopSpin4\userdata", UserDirectories.UserDataRoot(@"D:\Games\TopSpin4\"));
    }

    [Fact]
    public void Under_wine_the_roots_stay_beside_the_launcher_on_the_linux_users_drive()
    {
        Assert.Equal(@"Z:\home\deck\games\TopSpin4", UserDirectories.PreparedDataRoot(@"Z:\home\deck\games\TopSpin4\"));
        Assert.Equal(@"Z:\home\deck\games\TopSpin4\userdata", UserDirectories.UserDataRoot(@"Z:\home\deck\games\TopSpin4\"));
    }

    [Fact]
    public void The_art_folder_is_userdata_art()
    {
        Assert.Equal(@"D:\Games\TopSpin4\userdata\art",
            UserDirectories.ArtFolder(UserDirectories.UserDataRoot(@"D:\Games\TopSpin4")));
    }

    [Fact]
    public void A_folder_that_can_be_written_to_gives_no_refusal()
    {
        Assert.Null(UserDirectories.Refusal(_root));
        Assert.Empty(Directory.GetFileSystemEntries(_root));
    }

    [Fact]
    public void A_folder_that_cannot_be_written_to_is_refused_with_the_owners_wording()
    {
        var file = Path.Combine(_root, "a-file");
        File.WriteAllText(file, "x");
        var folder = Path.Combine(file, "below");

        Assert.Equal($"This folder can't be written to: {folder}. Move the game folder somewhere you can write to, " +
            "such as your Documents or a games drive, and start the Launcher again.", UserDirectories.Refusal(folder));
    }
}
