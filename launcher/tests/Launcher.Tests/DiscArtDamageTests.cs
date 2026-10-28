using System.Security.Cryptography;
using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

/// <summary>The Disc art module never throws for what is on the disc: whatever stops a source from being decoded is a logged problem.</summary>
public sealed class DiscArtDamageTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ts4-art-damage-").FullName;
    private readonly List<string> _log = [];

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Art => Path.Combine(_root, "art");

    // A disc whose named files fail in the given way when they are read or decoded, and the generated art for the others.
    private sealed class FailingDisc(TestArtDisc disc, Dictionary<string, Exception> failures) : IDiscFileSource
    {
        public byte[]? Read(string path)
        {
            if (failures.TryGetValue(path, out var failure)) throw failure;
            return disc.Files.TryGetValue(path, out var bytes) ? bytes : null;
        }

        public void Dispose()
        {
        }
    }

    public static IEnumerable<object[]> Failures() =>
    [
        [new CryptographicException("the key did not fit")],
        [new InvalidOperationException("a decoder in a state it should not be in")],
        [new NullReferenceException()],
        [new ArgumentOutOfRangeException("offset")],
        [new OverflowException()],
        [new NotSupportedException("a format nobody reads")],
    ];

    [Theory]
    [MemberData(nameof(Failures))]
    public void Any_failure_of_a_source_is_logged_for_that_source_and_the_others_still_count(Exception failure)
    {
        var source = new FailingDisc(new TestArtDisc(), new()
        {
            [TestArtDisc.Module] = failure,
            [TestArtDisc.NxeArt] = failure,
            [TestArtDisc.PressStart] = failure,
        });

        var result = DiscArt.FromSource(source, "a disc", Art, _log.Add);

        Assert.Empty(result.Found);
        Assert.Equal(3, _log.Count);
        Assert.All(_log, line => Assert.StartsWith("Disc art:", line));
        Assert.Contains(_log, line => line.Contains("nxeart"));
        Assert.Contains(_log, line => line.Contains("default.xex"));
        Assert.Contains(_log, line => line.Contains("PressStart.xzp"));
    }

    [Fact]
    public void A_failing_module_does_not_stop_the_other_sources()
    {
        var source = new FailingDisc(new TestArtDisc(), new() { [TestArtDisc.Module] = new CryptographicException("no") });

        var result = DiscArt.FromSource(source, "a disc", Art, _log.Add);

        Assert.Equal([DiscArtSlot.Background, DiscArtSlot.BoxArt, DiscArtSlot.Logo, DiscArtSlot.SetupBackground], result.Found);
        Assert.Contains("default.xex", Assert.Single(_log));
    }

    [Fact]
    public void Restoring_from_a_disc_folder_with_a_garbage_module_is_logged_and_never_thrown()
    {
        var disc = new TestArtDisc();
        var folder = disc.WriteFolder(Path.Combine(_root, "disc"));
        var damaged = (byte[])disc.Files[TestArtDisc.Module].Clone();
        Array.Fill(damaged, (byte)0xFF, 4, damaged.Length - 4);
        File.WriteAllBytes(Path.Combine(folder, TestArtDisc.Module), damaged);

        var result = DiscArt.RestoreFromFolder(folder, Art, _log.Add);

        Assert.NotNull(result);
        Assert.Contains(_log, line => line.StartsWith("Disc art:") && line.Contains("default.xex"));
    }
}
