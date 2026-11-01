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

    private static DiscArtEntry Achievement(int id, int points) => new($"{id}.png", $"A{id}", "Did it", points, id, "Do it");

    [Fact]
    public void Once_one_is_earned_the_summary_counts_the_earned_ones_and_their_points()
    {
        var unlocks = AchievementUnlocks.Parse("[unlocked.2]\nfiletime = 134379915150402610\n");
        var showcase = new Showcase([], [], [Achievement(1, 10), Achievement(2, 25), Achievement(3, 1000)], unlocks);

        Assert.Equal("1 of 3 achievements, 25 of 1,035 G", showcase.AchievementsSummary);
        Assert.Equal(1, showcase.UnlockedCount);
    }

    [Fact]
    public void Earned_achievements_come_first_latest_first_then_the_locked_ones_in_disc_order()
    {
        var unlocks = AchievementUnlocks.Parse("[unlocked.3]\nfiletime = 100000000000000000\n\n[unlocked.1]\nfiletime = 200000000000000000\n");
        var showcase = new Showcase([], [], [Achievement(1, 5), Achievement(2, 5), Achievement(3, 5), Achievement(4, 5)], unlocks);

        Assert.Equal([1, 3, 2, 4], showcase.AchievementsInOrder.Select(entry => entry.Id));
        Assert.True(showcase.IsUnlocked(showcase.Achievements[0]));
        Assert.False(showcase.IsUnlocked(showcase.Achievements[1]));
    }

    [Fact]
    public void There_are_release_notes()
    {
        Assert.NotEmpty(Showcase.WhatsNew);
    }
}
