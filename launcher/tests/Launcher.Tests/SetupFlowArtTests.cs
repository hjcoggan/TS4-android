using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

/// <summary>The setup page's disc row asks the Disc art module for the disc's art the moment it accepts the disc.</summary>
public sealed class SetupFlowArtTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ts4-flow-art-").FullName;
    private readonly TestDisc _disc;
    private readonly TestArtDisc _art = new();
    private readonly FakeTime _time = new();
    private readonly List<string> _log = [];

    public SetupFlowArtTests()
    {
        _disc = _art.AddTo(new TestDisc());
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string ArtFolder => Path.Combine(_root, "user", DiscArt.FolderName);

    private SetupFlow Flow(Action<string>? discArt, Action<string>? preparedArt = null)
    {
        var preparation = new Preparation(Path.Combine(_root, "cache"), FileManifest.Parse(_disc.ManifestText()),
            FileManifest.Parse(TestTitleUpdate.ManifestText()), new FakeDiskSpace(), _time);
        return new SetupFlow(preparation, _time, _log.Add, discArt, preparedArt);
    }

    private string WriteTu() =>
        TestTitleUpdate.Package().WriteTo(Path.Combine(_root, "input", "TU_1A5822P_000000C000000.0000000000083"));

    private async Task<SetupFlow> PreparedFlow(Action<string>? discArt, Action<string>? preparedArt)
    {
        var flow = Flow(discArt, preparedArt);
        await flow.ChooseDisc(WriteDisc());
        await flow.ChooseTitleUpdate(WriteTu());
        await flow.PrepareAsync(TestContext.Current.CancellationToken);
        Assert.Equal(SetupPhase.Ready, flow.Phase);
        return flow;
    }

    private string WriteDisc(TestDisc? disc = null)
    {
        var path = Path.Combine(_root, "input", "Top Spin 4.iso");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        (disc ?? _disc).WriteImage(path);
        return path;
    }

    [Fact]
    public async Task Accepting_a_disc_writes_its_art_to_the_art_folder()
    {
        var flow = Flow(iso => DiscArt.FromIso(iso, ArtFolder, _log.Add));

        await flow.ChooseDisc(WriteDisc());

        Assert.Equal(RowState.Accepted, flow.Disc.State);
        var art = DiscArtFolder.Open(ArtFolder);
        Assert.True(art.IsComplete);
        Assert.Equal(_art.Background, File.ReadAllBytes(art.ImagePath(DiscArtSlot.Background)!));
        Assert.Empty(_log);
    }

    [Fact]
    public async Task The_art_is_ready_before_the_row_reports_the_change()
    {
        var flow = Flow(iso => DiscArt.FromIso(iso, ArtFolder, _log.Add));
        bool? completeWhenAccepted = null;
        flow.Changed += () =>
        {
            if (flow.Disc.State == RowState.Accepted) completeWhenAccepted = DiscArtFolder.Open(ArtFolder).IsComplete;
        };

        await flow.ChooseDisc(WriteDisc());

        Assert.True(completeWhenAccepted);
    }

    [Fact]
    public async Task A_failure_while_reading_the_art_is_logged_and_leaves_the_disc_row_accepted()
    {
        var flow = Flow(_ => throw new IOException("the art folder is read-only"));

        await flow.ChooseDisc(WriteDisc());

        Assert.Equal(RowState.Accepted, flow.Disc.State);
        Assert.Null(flow.Disc.Reason);
        Assert.Contains("the art folder is read-only", Assert.Single(_log));
        Assert.Contains("Disc art", _log[0]);
    }

    [Fact]
    public async Task A_damaged_source_inside_the_disc_leaves_the_row_accepted_and_the_other_art_written()
    {
        _disc.Files[TestArtDisc.NxeArt][TestStfs.DataBlockOffset(2) + 16] ^= 0xFF;
        var flow = Flow(iso => DiscArt.FromIso(iso, ArtFolder, _log.Add));

        await flow.ChooseDisc(WriteDisc());

        Assert.Equal(RowState.Accepted, flow.Disc.State);
        Assert.Contains("nxeart", Assert.Single(_log));
        Assert.True(DiscArtFolder.Open(ArtFolder).Has(DiscArtSlot.Logo));
        Assert.False(DiscArtFolder.Open(ArtFolder).Has(DiscArtSlot.Background));
    }

    [Fact]
    public async Task A_refused_disc_does_not_read_any_art()
    {
        var asked = new List<string>();
        var flow = Flow(asked.Add);
        var other = new TestDisc();
        other.Files["default.xex"] = TestXex.Module(mediaId: 0x11111111);

        await flow.ChooseDisc(WriteDisc(other));

        Assert.Equal(RowState.Rejected, flow.Disc.State);
        Assert.Empty(asked);
    }

    [Fact]
    public async Task The_hook_gets_the_path_of_the_accepted_image()
    {
        var asked = new List<string>();
        var flow = Flow(asked.Add);
        var path = WriteDisc();

        await flow.ChooseDisc(path);

        Assert.Equal([path], asked);
    }

    // Setup read the art from the .iso when the disc was chosen; once the disc is extracted it is read again from there if that left the folder incomplete.
    [Fact]
    public async Task After_setup_the_art_is_read_again_from_the_prepared_disc_folder_when_the_art_folder_is_missing()
    {
        var flow = await PreparedFlow(discArt: null, preparedArt: root => DiscArt.RestoreFromFolder(root, ArtFolder, _log.Add));

        Assert.True(DiscArtFolder.Open(ArtFolder).IsComplete);
        Assert.Equal(flow.Prepared!.GameDataRoot, DiscArtFolder.Open(ArtFolder).Source);
        Assert.Empty(_log);
    }

    [Fact]
    public async Task After_setup_a_complete_art_folder_from_the_iso_is_not_read_again()
    {
        await PreparedFlow(iso => DiscArt.FromIso(iso, ArtFolder, _log.Add), root => DiscArt.RestoreFromFolder(root, ArtFolder, _log.Add));

        Assert.True(DiscArtFolder.Open(ArtFolder).IsComplete);
        Assert.EndsWith(".iso", DiscArtFolder.Open(ArtFolder).Source);
    }

    [Fact]
    public async Task A_failure_reading_the_prepared_folder_is_logged_and_setup_still_ends_ready()
    {
        var flow = await PreparedFlow(null, _ => throw new IOException("the art folder is read-only"));

        Assert.Equal(SetupPhase.Ready, flow.Phase);
        Assert.Null(flow.Message);
        Assert.Contains("the art folder is read-only", Assert.Single(_log));
        Assert.Contains("Disc art", _log[0]);
    }

    [Fact]
    public async Task The_prepared_folder_is_read_again_at_a_later_start_through_RefreshArt()
    {
        await PreparedFlow(null, null);
        var asked = new List<string>();
        var later = Flow(null, asked.Add);

        later.RefreshArt();

        Assert.Equal(SetupPhase.Ready, later.Phase);
        Assert.Equal([later.Prepared!.GameDataRoot], asked);
    }

    [Fact]
    public void Without_prepared_files_there_is_nothing_to_read_the_art_from()
    {
        var asked = new List<string>();
        var flow = Flow(null, asked.Add);

        flow.RefreshArt();

        Assert.Empty(asked);
    }

    [Fact]
    public async Task A_failure_in_RefreshArt_is_logged_and_never_thrown()
    {
        await PreparedFlow(null, null);
        var later = Flow(null, _ => throw new UnauthorizedAccessException("denied"));

        later.RefreshArt();

        Assert.Contains("denied", Assert.Single(_log));
    }
}
