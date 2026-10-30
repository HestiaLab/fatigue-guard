using FatigueGuard.Core.Config;

namespace FatigueGuard.Core.Analysis;

public sealed class FatigueAnalyzer : IFatigueAnalyzer
{
    private static readonly TimeSpan EvaluationInterval = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan NoFaceDelay = TimeSpan.FromSeconds(2.0);

    private static readonly FatigueMetrics EmptyMetrics = new(
        null, null, 0.0, false, 0, 0, 0, 0, 0, null);

    private readonly FatigueConfig _config;
    private readonly EarSmoother _smoother = new(0.6);
    private readonly ClosureTracker _closures;
    private readonly MetricWindow _window;
    private readonly FatigueStateMachine _stateMachine;
    private readonly CalibrationTracker? _calibration;

    private TimeSpan? _firstTime;
    private TimeSpan? _lastTime;
    private TimeSpan? _lastValidTime;
    private TimeSpan? _lastFaceTime;
    private TimeSpan? _monitoringStart;
    private TimeSpan? _lastEvalTime;
    private double? _baselineEar;
    private double? _closedThreshold;
    private bool _calibrated;
    private bool _initialized;
    private bool _calibrationInsufficient;
    private int _lastFaceCount;
    private FatigueDisplayState _display = FatigueDisplayState.Calibrating;
    private FatigueMetrics _metrics = EmptyMetrics;

    public FatigueAnalyzer(FatigueConfig config, bool skipCalibration)
    {
        _config = config;
        _closures = new ClosureTracker(config.Blink);
        _window = new MetricWindow(config.Metrics, config.Fatigue.MicrosleepLookbackSeconds);
        _stateMachine = new FatigueStateMachine(config.Fatigue);

        if (config.Calibration.BaselineOverride is { } overrideBaseline)
        {
            _baselineEar = overrideBaseline;
            _closedThreshold = CalibrationTracker.ComputeClosedThreshold(overrideBaseline, config.Blink);
            _calibrated = true;
            _stateMachine.SetCalibrated();
            _display = FatigueDisplayState.Ok;
        }
        else if (skipCalibration || config.Calibration.Seconds <= 0.0)
        {
            _baselineEar = CalibrationTracker.DefaultBaselineEar;
            _closedThreshold = CalibrationTracker.ComputeClosedThreshold(_baselineEar.Value, config.Blink);
            _calibrated = true;
            _stateMachine.SetCalibrated();
            _display = FatigueDisplayState.Ok;
        }
        else
        {
            _calibration = new CalibrationTracker(config.Calibration, config.Blink);
        }
    }

    public FatigueUpdate Process(FaceObservation? obs)
    {
        if (obs is null)
        {
            return BuildUpdate(_lastTime ?? TimeSpan.Zero, new List<FatigueEvent>(), null);
        }

        TimeSpan time = obs.StreamTime;
        _lastTime = time;
        _firstTime ??= time;
        _lastFaceCount = obs.FaceCount;

        var events = new List<FatigueEvent>(3);

        if (!_initialized)
        {
            _initialized = true;
            if (_calibration is null)
            {
                _monitoringStart ??= time;
                events.Add(new FatigueEvent(
                    time,
                    FatigueEventKind.CalibrationSkipped,
                    BaselineEar: _baselineEar,
                    ClosedEarThreshold: _closedThreshold));
            }
            else
            {
                events.Add(new FatigueEvent(time, FatigueEventKind.CalibrationStarted));
            }
        }

        if (obs.FaceCount > 0)
        {
            _lastFaceTime = time;
        }

        if (!_calibrated)
        {
            return ProcessCalibration(obs, time, events);
        }

        return ProcessMonitoring(obs, time, events);
    }

    private FatigueUpdate ProcessCalibration(FaceObservation obs, TimeSpan time, List<FatigueEvent> events)
    {
        double? combined = EarCalculator.Combine(obs.LeftEar, obs.RightEar);
        double? smoothed = combined.HasValue ? _smoother.Add(combined.Value) : null;
        _calibration!.Record(time, combined.HasValue, smoothed);

        CalibrationResult? result = _calibration.TryComplete(time);
        if (result is null)
        {
            _display = FatigueDisplayState.Calibrating;
            var info = new CalibrationInfo(
                _calibration.ElapsedSeconds(time),
                _config.Calibration.Seconds,
                _calibration.ValidRatio);
            return BuildUpdate(time, events, info);
        }

        _baselineEar = result.BaselineEar;
        _closedThreshold = result.ClosedThreshold;
        _calibrationInsufficient = result.Insufficient;
        _calibrated = true;
        _monitoringStart = time;
        _lastValidTime = null;
        _window.Reset();
        _stateMachine.SetCalibrated();
        _display = FatigueDisplayState.Ok;

        events.Add(result.Insufficient
            ? new FatigueEvent(
                time,
                FatigueEventKind.CalibrationInsufficient,
                BaselineEar: result.BaselineEar,
                ClosedEarThreshold: result.ClosedThreshold)
            : new FatigueEvent(
                time,
                FatigueEventKind.CalibrationCompleted,
                BaselineEar: result.BaselineEar,
                ClosedEarThreshold: result.ClosedThreshold));

        return BuildUpdate(time, events, null);
    }

    private FatigueUpdate ProcessMonitoring(FaceObservation obs, TimeSpan time, List<FatigueEvent> events)
    {
        double? combined = EarCalculator.Combine(obs.LeftEar, obs.RightEar);
        _window.AddFrame(time, combined.HasValue);

        if (combined.HasValue)
        {
            bool gap = _lastValidTime is { } lastValid &&
                       (time - lastValid).TotalMilliseconds > _config.Blink.MaxGapMs;
            if (gap)
            {
                _smoother.Reset();
                _closures.Abort();
            }

            double smoothed = _smoother.Add(combined.Value);

            if (_lastValidTime is { } previous)
            {
                TimeSpan dt = time - previous;
                bool closed = smoothed < _closedThreshold!.Value;
                if (!gap)
                {
                    _window.AddInterval(time, dt.TotalSeconds, closed);
                }

                ClosureUpdate closure = _closures.Update(time, closed, dt, gap);
                if (closure.MicrosleepDetectedNow)
                {
                    _window.RecordMicrosleep(time);
                    _metrics = _window.Snapshot(time);
                    FatigueEvent? critical = _stateMachine.ForceMicrosleep(
                        time,
                        closure.DurationSeconds,
                        IsQualityLow(time),
                        IsNoFace(time));
                    if (critical is not null)
                    {
                        events.Add(critical);
                    }
                }
                else if (closure.Outcome == ClosureOutcome.Blink)
                {
                    _window.RecordBlink(time);
                }
                else if (closure.Outcome == ClosureOutcome.LongClosure)
                {
                    _window.RecordLongClosure(time);
                }
            }

            _lastValidTime = time;
        }

        _metrics = _window.Snapshot(time);
        bool noFace = IsNoFace(time);
        bool qualityLow = IsQualityLow(time);

        if (_lastEvalTime is null || time - _lastEvalTime.Value >= EvaluationInterval)
        {
            _lastEvalTime = time;
            FatigueEvent? stateEvent = _stateMachine.Evaluate(time, _metrics, qualityLow, noFace);
            if (stateEvent is not null)
            {
                events.Add(stateEvent);
            }
        }

        FatigueDisplayState display = ComputeDisplay(noFace, qualityLow);
        AppendDisplayEvent(display, time, events);

        return BuildUpdate(time, events, null);
    }

    private void AppendDisplayEvent(FatigueDisplayState next, TimeSpan time, List<FatigueEvent> events)
    {
        if (next == _display)
        {
            return;
        }

        if (next == FatigueDisplayState.NoFace)
        {
            events.Add(new FatigueEvent(time, FatigueEventKind.NoFace));
        }
        else if (next == FatigueDisplayState.QualityLow)
        {
            events.Add(new FatigueEvent(time, FatigueEventKind.QualityLow));
        }
        else if (_display == FatigueDisplayState.NoFace)
        {
            events.Add(new FatigueEvent(time, FatigueEventKind.FaceRestored));
        }
        else if (_display == FatigueDisplayState.QualityLow)
        {
            events.Add(new FatigueEvent(time, FatigueEventKind.QualityRestored));
        }

        _display = next;
    }

    private FatigueDisplayState ComputeDisplay(bool noFace, bool qualityLow)
    {
        if (!_calibrated)
        {
            return FatigueDisplayState.Calibrating;
        }

        if (noFace)
        {
            return FatigueDisplayState.NoFace;
        }

        if (qualityLow)
        {
            return FatigueDisplayState.QualityLow;
        }

        return _stateMachine.State;
    }

    private bool IsNoFace(TimeSpan time)
    {
        TimeSpan? reference = _lastFaceTime ?? _firstTime;
        return reference is { } start && time - start > NoFaceDelay;
    }

    private bool IsQualityLow(TimeSpan time)
    {
        if (_monitoringStart is null ||
            time - _monitoringStart.Value < TimeSpan.FromSeconds(MetricWindow.QualityWindowSeconds))
        {
            return false;
        }

        return _window.QualityRatio(time) is { } ratio && ratio < 0.5;
    }

    private FatigueUpdate BuildUpdate(TimeSpan time, List<FatigueEvent> events, CalibrationInfo? calibration)
    {
        return new FatigueUpdate(
            time,
            _display,
            events,
            _metrics.Perclos,
            _metrics.BlinkRatePerMinute,
            _smoother.Smoothed,
            _metrics.DataSufficient,
            _baselineEar,
            _closedThreshold,
            calibration,
            _calibrationInsufficient,
            _lastFaceCount,
            _metrics.QualityValidRatio,
            _metrics.BlinksInWindow,
            _metrics.LongClosuresInWindow,
            _metrics.MicrosleepsInLookback,
            _metrics.ValidSeconds);
    }
}
