using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

public sealed class AchievementUnlocksTests
{
    [Fact]
    public void The_games_unlock_file_gives_each_earned_achievement_and_when()
    {
        // As ReXGlue's AchievementManager writes it.
        var unlocks = AchievementUnlocks.Parse(
            "# Achievement unlock state - managed by ReXGlue runtime\n\n[unlocked.46]\nfiletime = 134379915150402610\n\n[unlocked.3]\nfiletime = 0\n");

        Assert.Equal(2, unlocks.Count);
        Assert.True(unlocks.IsUnlocked(46));
        Assert.True(unlocks.IsUnlocked(3));
        Assert.False(unlocks.IsUnlocked(1));
        Assert.Equal(DateTime.FromFileTimeUtc(134379915150402610), unlocks.UnlockedAt(46));
        Assert.Null(unlocks.UnlockedAt(3));
    }

    [Fact]
    public void Other_tables_and_damaged_lines_are_ignored()
    {
        var unlocks = AchievementUnlocks.Parse("[other]\nfiletime = 5\n[unlocked.x]\n[unlocked.7]\nfiletime = nonsense\r\n");

        Assert.Equal(1, unlocks.Count);
        Assert.Null(unlocks.UnlockedAt(7));
    }

    [Fact]
    public void A_missing_file_means_nothing_is_earned()
    {
        var unlocks = AchievementUnlocks.Read(Path.Combine(Path.GetTempPath(), "ts4-no-such-folder", "54540859.toml"));

        Assert.Equal(0, unlocks.Count);
    }

    [Fact]
    public void The_file_is_named_after_the_title_in_the_user_data_folder()
    {
        Assert.Equal(Path.Combine("root", "achievements", "54540859.toml"), AchievementUnlocks.PathIn("root"));
    }
}
