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

    private SetupFlow Flow(Action<string>? discArt)
    {
        var preparation = new Preparation(Path.Combine(_root, "cache"), FileManifest.Parse(_disc.ManifestText()),
            FileManifest.Parse(TestTitleUpdate.ManifestText()), new FakeDiskSpace(), _time);
        return new SetupFlow(preparation, _time, _log.Add, discArt);
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
}
