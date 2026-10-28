namespace TopSpin4.Launcher.Core;

/// <summary>
/// One progress value for the whole of setup. Setup runs as bands: checking the disc, checking
/// Title Update 1, extracting the disc, unpacking Title Update 1 (a band is left out when its input is
/// already set up). The copy bands weigh what they move; each check weighs a small share of the copying,
/// since its length is not known. The value never goes back, and is flagged indeterminate while a step
/// can't say how far along it is. The time left comes from the copy rate so far.
/// </summary>
internal sealed class OverallProgress
{
    private const double CheckShare = 0.02;

    private readonly bool _needDisc;
    private readonly bool _needTitleUpdate;
    private readonly double _discBytes;
    private readonly double _titleUpdateBytes;
    private readonly double _check;
    private readonly double _total;
    private readonly TimeProvider _time;
    private long _copyStart;
    private double _copyBaseline = -1;
    private double _copyDone;

    public OverallProgress(Preparation preparation, bool needDisc, bool needTitleUpdate, TimeProvider time)
    {
        _needDisc = needDisc;
        _needTitleUpdate = needTitleUpdate;
        _discBytes = needDisc ? preparation.DiscBytes : 0;
        _titleUpdateBytes = needTitleUpdate ? preparation.TitleUpdateBytes : 0;
        _time = time;
        var copy = _discBytes + _titleUpdateBytes;
        _check = Math.Max(copy * CheckShare, 1);
        _total = copy + Checks;
    }

    private double Checks => _check * ((_needDisc ? 1 : 0) + (_needTitleUpdate ? 1 : 0));

    /// <summary>0 to 1, never decreasing.</summary>
    public double Fraction { get; private set; }

    /// <summary>The current step can't say how long it takes.</summary>
    public bool IsIndeterminate { get; private set; } = true;

    public TimeSpan? TimeLeft { get; private set; }

    public void Update(SetupProgress progress)
    {
        double position;
        switch (progress.Step)
        {
            case SetupStep.CheckingDisc:
                position = 0;
                IsIndeterminate = true;
                break;
            case SetupStep.CheckingTitleUpdate:
                position = _needDisc ? _check : 0;
                IsIndeterminate = true;
                break;
            case SetupStep.CheckingExistingFiles:
                position = Fraction * _total;
                IsIndeterminate = true;
                break;
            case SetupStep.Extracting:
                position = Checks + progress.Fraction * _discBytes;
                Copying(position - Checks);
                break;
            case SetupStep.UnpackingTitleUpdate:
                position = Checks + _discBytes + progress.Fraction * _titleUpdateBytes;
                Copying(position - Checks);
                break;
            default:
                position = _total;
                IsIndeterminate = false;
                TimeLeft = null;
                break;
        }
        Fraction = Math.Max(Fraction, Math.Min(position / _total, 1));
    }

    private void Copying(double copied)
    {
        IsIndeterminate = false;
        if (_copyBaseline < 0)
        {
            _copyBaseline = copied;
            _copyStart = _time.GetTimestamp();
        }
        _copyDone = Math.Max(_copyDone, copied);
        var moved = _copyDone - _copyBaseline;
        if (moved <= 0)
        {
            TimeLeft = null;
            return;
        }
        var elapsed = _time.GetElapsedTime(_copyStart);
        var remaining = _discBytes + _titleUpdateBytes - _copyDone;
        TimeLeft = TimeSpan.FromTicks((long)(elapsed.Ticks * (remaining / moved)));
    }
}
