using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

public sealed class LauncherShellTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ts4-shell-").FullName;
    private readonly TestDisc _disc = new();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private Preparation Preparation() =>
        new(Path.Combine(_root, "cache"), FileManifest.Parse(_disc.ManifestText()),
            FileManifest.Parse(TestTitleUpdate.ManifestText()), new FakeDiskSpace(), new FakeTime());

    private void PrepareCache()
    {
        var input = Path.Combine(_root, "input");
        Directory.CreateDirectory(input);
        var disc = Path.Combine(input, "Top Spin 4.iso");
        _disc.WriteImage(disc);
        var titleUpdate = TestTitleUpdate.Package().WriteTo(Path.Combine(input, "TU_1A5822P_000000C000000.0000000000083"));
        Preparation().Prepare(disc, titleUpdate, null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void A_clean_user_folder_opens_on_the_setup_page_and_a_prepared_one_on_home()
    {
        Assert.Equal(LauncherPage.Setup, new LauncherShell(new SetupFlow(Preparation())).Page);

        PrepareCache();

        Assert.Equal(LauncherPage.Home, new LauncherShell(new SetupFlow(Preparation())).Page);
    }

    [Fact]
    public async Task Finishing_setup_moves_to_home()
    {
        var flow = new SetupFlow(Preparation());
        var shell = new LauncherShell(flow);
        var input = Path.Combine(_root, "input");
        Directory.CreateDirectory(input);
        var disc = Path.Combine(input, "Top Spin 4.iso");
        _disc.WriteImage(disc);
        await flow.ChooseDisc(disc);
        await flow.ChooseTitleUpdate(TestTitleUpdate.Package().WriteTo(Path.Combine(input, "TU_1A5822P_000000C000000.0000000000083")));

        await flow.PrepareAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LauncherPage.Home, shell.Page);
    }

    [Fact]
    public void Prepared_files_that_went_away_take_the_player_back_to_the_setup_page_on_set_up_again()
    {
        PrepareCache();
        var flow = new SetupFlow(Preparation());
        var shell = new LauncherShell(flow);
        Directory.Delete(Path.Combine(_root, "cache", "gamedata"), recursive: true);
        Assert.Equal(LauncherPage.Home, shell.Page);
        var changed = 0;
        shell.Changed += () => changed++;

        flow.SetUpAgain();

        Assert.Equal(LauncherPage.Setup, shell.Page);
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Settings_opens_from_home_and_back_returns_home()
    {
        PrepareCache();
        var shell = new LauncherShell(new SetupFlow(Preparation()));
        var pages = new List<LauncherPage>();
        shell.Changed += () => pages.Add(shell.Page);

        shell.OpenSettings();
        Assert.Equal(LauncherPage.Settings, shell.Page);

        shell.Back();
        Assert.Equal(LauncherPage.Home, shell.Page);
        Assert.Equal([LauncherPage.Settings, LauncherPage.Home], pages);
    }

    [Fact]
    public void Back_on_home_or_setup_stays_put_and_settings_cannot_open_before_setup()
    {
        var shell = new LauncherShell(new SetupFlow(Preparation()));

        shell.OpenSettings();
        shell.Back();

        Assert.Equal(LauncherPage.Setup, shell.Page);
    }
}
