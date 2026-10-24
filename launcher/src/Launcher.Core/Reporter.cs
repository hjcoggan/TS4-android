namespace TopSpin4.Launcher.Core;

/// <summary>Turns byte counts into progress reports at most every 100 ms, with the time left.</summary>
internal sealed class Reporter(IProgress<SetupProgress>? progress, TimeProvider time)
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(100);

    private long _lastReport = long.MinValue;
    private long _start;
    private long _doneAtStart;
    private long _total;
    private SetupStep _step;

    public long Done { get; private set; }

    /// <summary>Starts a step that moves <paramref name="total"/> bytes, <paramref name="alreadyDone"/> of them done.</summary>
    public void Start(SetupStep step, long total, long alreadyDone = 0)
    {
        _step = step;
        _total = total;
        Done = _doneAtStart = alreadyDone;
        _start = time.GetTimestamp();
        Report(null, force: true);
    }

    public void Advance(long bytes, string file)
    {
        Done += bytes;
        Report(file);
    }

    public void Report(string? file, bool force = false)
    {
        if (progress is null) return;
        var now = time.GetTimestamp();
        if (!force && _lastReport != long.MinValue && time.GetElapsedTime(_lastReport, now) < Interval) return;
        _lastReport = now;
        progress.Report(new SetupProgress(_step, Done, _total, file, TimeLeft(now)));
    }

    private TimeSpan? TimeLeft(long now)
    {
        var moved = Done - _doneAtStart;
        if (moved <= 0) return null;
        var elapsed = time.GetElapsedTime(_start, now);
        return TimeSpan.FromTicks((long)(elapsed.Ticks * ((double)(_total - Done) / moved)));
    }
}
