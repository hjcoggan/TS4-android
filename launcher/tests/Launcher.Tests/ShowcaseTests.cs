using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

public sealed class ShowcaseTests
{
    private static DiscArtEntry Achievement(int points) => new("a.png", "A", "B", points);

    [Fact]
    public void The_achievements_summary_counts_them_and_adds_their_points()
    {
        Assert.Equal("2 achievements, 1,010 G", new Showcase([], [], [Achievement(10), Achievement(1000)]).AchievementsSummary);
        Assert.Equal("1 achievement, 5 G", new Showcase([], [], [Achievement(5)]).AchievementsSummary);
        Assert.Equal("", Showcase.Empty.AchievementsSummary);
    }

    [Fact]
    public void There_are_release_notes()
    {
        Assert.NotEmpty(Showcase.WhatsNew);
    }
}
