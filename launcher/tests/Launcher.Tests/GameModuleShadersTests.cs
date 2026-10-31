using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

public sealed class GameModuleShadersTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ts4 modules ").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Cache => Path.Combine(_root, "cache");

    private GameModuleShaders Modules(FakeRunner runner, string? exe = null)
    {
        var game = exe ?? Path.Combine(_root, "topspin4.exe");
        return new GameModuleShaders(game, new Preparation(_root), runner);
    }

    [Fact]
    public void The_games_module_mode_is_started_headless_on_the_prepared_folders_and_its_count_is_returned()
    {
        var runner = new FakeRunner(0, "topspin4-prepare-modules 1", "result status=ok containers=91 ms=2400");

        var count = Modules(runner).Write(Cache, CancellationToken.None);

        Assert.Equal(91, count);
        var start = Assert.Single(runner.Starts);
        Assert.Equal(Path.Combine(_root, "topspin4.exe"), start.Executable);
        Assert.Equal(_root, start.WorkingDirectory);
        Assert.Contains("--ts4_prepare_modules=true", start.Arguments);
        Assert.Contains("--game_data_root=" + Path.Combine(_root, "gamedata"), start.Arguments);
        Assert.Contains("--update_data_root=" + Path.Combine(_root, "updatedata"), start.Arguments);
        Assert.Contains("--cache_root=" + Cache, start.Arguments);
        Assert.Contains("--headless", start.Arguments);
        Assert.Contains("--window_hidden=true", start.Arguments);
    }

    [Fact]
    public void A_failed_result_gives_the_reason()
    {
        var runner = new FakeRunner(2, "topspin4-prepare-modules 1", "result status=failed reason=module_load ms=40");

        var error = Assert.Throws<ShaderPreparationException>(() => Modules(runner).Write(Cache, CancellationToken.None));

        Assert.Contains("module_load", error.Message);
    }

    [Fact]
    public void A_game_that_ends_without_a_result_is_a_crash_the_player_hears_about()
    {
        var runner = new FakeRunner(-1073741819);

        var error = Assert.Throws<ShaderPreparationException>(() => Modules(runner).Write(Cache, CancellationToken.None));

        Assert.Contains("stopped", error.Message);
    }

    [Fact]
    public void A_game_that_cannot_start_names_why()
    {
        var runner = new FakeRunner(0) { StartFailure = "topspin4.exe was not found next to the Launcher" };

        var error = Assert.Throws<ShaderPreparationException>(() => Modules(runner).Write(Cache, CancellationToken.None));

        Assert.Contains("topspin4.exe was not found", error.Message);
    }

    [Fact]
    public void Cancelling_stops_waiting_and_is_not_a_failure()
    {
        using var cancel = new CancellationTokenSource();
        var runner = new FakeRunner(0) { OnRun = cancel.Cancel };

        Assert.ThrowsAny<OperationCanceledException>(() => Modules(runner).Write(Cache, cancel.Token));
    }

    private sealed class FakeRunner(int exitCode, params string[] lines) : IPrepareRunner
    {
        public List<ProcessStart> Starts { get; } = [];
        public string? StartFailure { get; init; }
        public Action? OnRun { get; init; }

        public Task<int> RunAsync(ProcessStart start, Action<string> onLine, CancellationToken cancel)
        {
            Starts.Add(start);
            if (StartFailure is not null) throw new LauncherException(StartFailure);
            OnRun?.Invoke();
            cancel.ThrowIfCancellationRequested();
            foreach (var line in lines) onLine(line);
            return Task.FromResult(exitCode);
        }
    }
}
