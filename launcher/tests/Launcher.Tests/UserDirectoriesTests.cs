using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

public sealed class UserDirectoriesTests
{
    private const string LocalAppData = @"C:\Users\player\AppData\Local";

    private static Func<string, string?> Environment(params (string Name, string Value)[] variables) =>
        name => variables.FirstOrDefault(variable => variable.Name == name).Value;

    [Fact]
    public void On_windows_prepared_data_goes_under_local_app_data()
    {
        Assert.Equal(@"C:\Users\player\AppData\Local\TopSpin4Recomp",
            UserDirectories.PreparedDataRoot(Environment(("HOME", "/home/deck")), underWine: false, LocalAppData));
    }

    [Fact]
    public void Under_proton_prepared_data_goes_under_the_xdg_data_home_of_the_linux_user()
    {
        Assert.Equal(@"Z:\home\deck\.local\share\TopSpin4Recomp",
            UserDirectories.PreparedDataRoot(Environment(("HOME", "/home/deck")), underWine: true, LocalAppData));
        Assert.Equal(@"Z:\run\media\sd\data\TopSpin4Recomp",
            UserDirectories.PreparedDataRoot(Environment(("HOME", "/home/deck"), ("XDG_DATA_HOME", "/run/media/sd/data")),
                underWine: true, LocalAppData));
    }

    [Fact]
    public void Under_wine_without_a_linux_home_prepared_data_stays_in_the_prefix()
    {
        Assert.Equal(@"C:\Users\player\AppData\Local\TopSpin4Recomp",
            UserDirectories.PreparedDataRoot(Environment(("XDG_DATA_HOME", "relative/is/ignored")), underWine: true, LocalAppData));
    }
}
