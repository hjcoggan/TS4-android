namespace TopSpin4.Launcher.Core;

/// <summary>
/// One progress value for the whole of setup. Setup runs as bands: checking the disc, checking
/// Title Update 1, extracting the disc, unpacking Title Update 1, preparing the shaders, preparing the pipelines (a band is
/// left out when its input is already set up, the shaders band when the pack is usable, the pipelines band when the store is). The copy bands weigh what they move; each
/// check weighs a small share of the copying, since its length is not known; the shaders and pipelines bands weigh a share of the
/// copying too (or all of setup when nothing is copied). The value never goes back, and is flagged indeterminate while a
/// step can't say how far along it is. The time left comes from the copy rate so far, and in the shaders and pipelines bands from
/// each band's own pace.
/// </summary>
internal sealed class OverallProgress
{
    private const double CheckShare = 0.02;
    private const double ShaderShare = 0.08;
    private const double PipelineShare = 0.03;

    private readonly bool _needDisc;
    private readonly bool _needTitleUpdate;
    private readonly bool _needShaders;
    private readonly bool _needPipelines;
    private readonly double _discBytes;
    private readonly double _titleUpdateBytes;
    private readonly double _check;
    private readonly double _shaders;
    private readonly double _pipelines;
    private readonly double _total;
    private readonly TimeProvider _time;
    private long _copyStart;
    private double _copyBaseline = -1;
    private double _copyDone;
    private long _bandStart;
    private SetupStep? _band;
    private bool _shadersDone;
    private bool _pipelinesDone;

    public OverallProgress(Preparation preparation, bool needDisc, bool needTitleUpdate, TimeProvider time, bool needShaders = false,
        bool needPipelines = false)
    {
        _needDisc = needDisc;
        _needTitleUpdate = needTitleUpdate;
        _needShaders = needShaders;
        _needPipelines = needPipelines;
        _discBytes = needDisc ? preparation.DiscBytes : 0;
        _titleUpdateBytes = needTitleUpdate ? preparation.TitleUpdateBytes : 0;
        _time = time;
        var copy = _discBytes + _titleUpdateBytes;
        _check = Math.Max(copy * CheckShare, 1);
        _shaders = needShaders ? Math.Max(copy * ShaderShare, 1) : 0;
        _pipelines = needPipelines ? Math.Max(copy * PipelineShare, 1) : 0;
        _total = copy + Checks + _shaders + _pipelines;
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
            case SetupStep.PreparingShaders:
                position = Checks + _discBytes + _titleUpdateBytes + progress.Fraction * _shaders;
                _shadersDone |= progress.Fraction >= 1;
                Band(progress.Step, progress.Fraction);
                break;
            case SetupStep.PreparingPipelines:
                position = Checks + _discBytes + _titleUpdateBytes + _shaders + progress.Fraction * _pipelines;
                _pipelinesDone |= progress.Fraction >= 1;
                Band(progress.Step, progress.Fraction);
                break;
            default:
                // The disc and Title Update 1 are done; with a band still ahead, that is not the end of setup.
                position = _total - (_needShaders && !_shadersDone ? _shaders : 0) - (_needPipelines && !_pipelinesDone ? _pipelines : 0);
                IsIndeterminate = false;
                TimeLeft = null;
                break;
        }
        Fraction = Math.Max(Fraction, Math.Min(position / _total, 1));
    }

    // The band reports its own fraction from the first moment, so it is determinate straight away; the time left
    // is its pace so far, shown once it has moved a little.
    private void Band(SetupStep step, double fraction)
    {
        IsIndeterminate = false;
        if (_band != step)
        {
            _band = step;
            _bandStart = _time.GetTimestamp();
        }
        if (fraction < 0.02)
        {
            TimeLeft = null;
            return;
        }
        var elapsed = _time.GetElapsedTime(_bandStart);
        TimeLeft = TimeSpan.FromTicks((long)(elapsed.Ticks * ((1 - fraction) / fraction)));
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
