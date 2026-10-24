using TopSpin4.Launcher.Core;

namespace TopSpin4.Launcher.Tests;

/// <summary>Records reports synchronously, unlike Progress&lt;T&gt;, which posts them.</summary>
internal sealed class Reports(Action<SetupProgress> onReport) : IProgress<SetupProgress>
{
    public List<SetupProgress> All { get; } = [];

    public void Report(SetupProgress value)
    {
        All.Add(value);
        onReport(value);
    }
}

internal sealed class FakeDiskSpace : IDiskSpace
{
    public long Free { get; set; } = long.MaxValue / 2;
    public List<string> Asked { get; } = [];

    public long FreeBytes(string directory)
    {
        Asked.Add(directory);
        return Free;
    }
}

/// <summary>A clock that moves one second each time setup reads it.</summary>
internal sealed class FakeTime : TimeProvider
{
    private long _ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => _ticks += TimeSpan.TicksPerSecond;
}

/// <summary>An IProgress that calls back on the reporting thread.</summary>
internal sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}
