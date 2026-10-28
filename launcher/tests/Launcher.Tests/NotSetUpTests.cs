using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

public sealed class NotSetUpTests
{
    // The tokens the game passes (src/user_paths.h, kNotSetUp...): keep the two lists equal.
    [Theory]
    [InlineData("disc", "disc")]
    [InlineData("title-update", "Title Update 1")]
    [InlineData("disc-and-title-update", "disc image and Title Update 1")]
    public void The_reason_the_game_names_becomes_a_plain_line_for_the_setup_page(string token, string mentions)
    {
        var line = NotSetUp.Describe(token);

        Assert.Contains(mentions, line);
        Assert.DoesNotContain("-", line);
    }

    [Fact]
    public void A_reason_this_launcher_does_not_know_still_gives_a_line()
    {
        Assert.Contains("set up", NotSetUp.Describe("something-newer"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_argument_the_game_starts_the_launcher_with_is_the_flag_then_the_reason()
    {
        Assert.Equal(["--not-set-up", "disc"], NotSetUp.Arguments("disc"));
    }
}
