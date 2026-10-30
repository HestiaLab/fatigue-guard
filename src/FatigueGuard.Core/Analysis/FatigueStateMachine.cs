using FatigueGuard.Core.Config;

namespace FatigueGuard.Core.Analysis;

public sealed class FatigueStateMachine
{
    private readonly FatigueThresholds _config;
    private TimeSpan? _upgradeSince;
    private TimeSpan? _downgradeSince;
    private TimeSpan? _lastCriticalReminder;

    public FatigueStateMachine(FatigueThresholds config)
    {
        _config = config;
    }

    public FatigueDisplayState State { get; private set; } = FatigueDisplayState.Calibrating;

    public void SetCalibrated()
    {
        State = FatigueDisplayState.Ok;
        ResetTimers();
    }

    public FatigueEvent? Evaluate(TimeSpan now, FatigueMetrics metrics, bool qualityLow, bool noFace)
    {
        if (State == FatigueDisplayState.Calibrating)
        {
            return null;
        }

        if (noFace)
        {
            _upgradeSince = null;
            _downgradeSince = null;
            return null;
        }

        if (!qualityLow &&
            metrics.MicrosleepsInLookback > 0 &&
            State is FatigueDisplayState.Ok or FatigueDisplayState.Warning)
        {
            Transition(FatigueDisplayState.Critical, now);
            return new FatigueEvent(now, FatigueEventKind.CriticalEntered, FatigueTrigger.Microsleep);
        }

        switch (State)
        {
            case FatigueDisplayState.Ok:
                return EvaluateOk(now, metrics, qualityLow);
            case FatigueDisplayState.Warning:
                return EvaluateWarning(now, metrics, qualityLow);
            case FatigueDisplayState.Critical:
                return EvaluateCritical(now, metrics);
            default:
                return null;
        }
    }

    public FatigueEvent? ForceMicrosleep(TimeSpan now, double durationSeconds, bool qualityLow, bool noFace)
    {
        if (State is FatigueDisplayState.Calibrating or FatigueDisplayState.Critical)
        {
            return null;
        }

        if (qualityLow || noFace)
        {
            return null;
        }

        Transition(FatigueDisplayState.Critical, now);
        return new FatigueEvent(
            now,
            FatigueEventKind.CriticalEntered,
            FatigueTrigger.Microsleep,
            MicrosleepSeconds: durationSeconds);
    }

    private FatigueEvent? EvaluateOk(TimeSpan now, FatigueMetrics metrics, bool qualityLow)
    {
        _downgradeSince = null;
        if (qualityLow || !WarningCondition(metrics))
        {
            _upgradeSince = null;
            return null;
        }

        _upgradeSince ??= now;
        if (now - _upgradeSince.Value < Seconds(_config.WarnDwellSeconds))
        {
            return null;
        }

        Transition(FatigueDisplayState.Warning, now);
        return WarningEvent(now, metrics);
    }

    private FatigueEvent? EvaluateWarning(TimeSpan now, FatigueMetrics metrics, bool qualityLow)
    {
        if (qualityLow)
        {
            _upgradeSince = null;
        }
        else if (AtLeast(metrics, _config.PerclosCritical))
        {
            _upgradeSince ??= now;
            if (now - _upgradeSince.Value >= Seconds(_config.CriticalDwellSeconds))
            {
                Transition(FatigueDisplayState.Critical, now);
                return new FatigueEvent(
                    now,
                    FatigueEventKind.CriticalEntered,
                    FatigueTrigger.Perclos,
                    Perclos: metrics.Perclos,
                    Threshold: _config.PerclosCritical);
            }
        }
        else
        {
            _upgradeSince = null;
        }

        if (WarningExitCondition(metrics))
        {
            _downgradeSince ??= now;
            if (now - _downgradeSince.Value >= Seconds(_config.ExitDwellSeconds))
            {
                Transition(FatigueDisplayState.Ok, now);
                return new FatigueEvent(now, FatigueEventKind.Recovery);
            }
        }
        else
        {
            _downgradeSince = null;
        }

        return null;
    }

    private FatigueEvent? EvaluateCritical(TimeSpan now, FatigueMetrics metrics)
    {
        _upgradeSince = null;

        if (CriticalExitCondition(metrics))
        {
            _downgradeSince ??= now;
            if (now - _downgradeSince.Value >= Seconds(_config.ExitDwellSeconds))
            {
                Transition(FatigueDisplayState.Warning, now);
                return new FatigueEvent(
                    now,
                    FatigueEventKind.WarningDowngraded,
                    Perclos: metrics.Perclos,
                    Threshold: _config.PerclosExitCritical);
            }
        }
        else
        {
            _downgradeSince = null;
        }

        _lastCriticalReminder ??= now;
        if (now - _lastCriticalReminder.Value >= Seconds(_config.CriticalRepeatSeconds))
        {
            _lastCriticalReminder = now;
            return new FatigueEvent(
                now,
                FatigueEventKind.CriticalReminder,
                Perclos: metrics.Perclos);
        }

        return null;
    }

    private bool WarningCondition(FatigueMetrics metrics)
    {
        if (!metrics.DataSufficient)
        {
            return false;
        }

        return AtLeast(metrics, _config.PerclosWarn) ||
               (metrics.BlinkRatePerMinute is { } rate && rate >= _config.BlinkRateWarn) ||
               metrics.LongClosuresInWindow >= _config.LongClosuresWarn;
    }

    private bool WarningExitCondition(FatigueMetrics metrics)
    {
        if (!metrics.DataSufficient || metrics.BlinkRatePerMinute is not { } rate)
        {
            return false;
        }

        return metrics.Perclos is { } perclos &&
               perclos <= _config.PerclosExitWarn &&
               rate <= _config.BlinkRateExit &&
               metrics.LongClosuresInLookback == 0;
    }

    private bool CriticalExitCondition(FatigueMetrics metrics)
    {
        if (!metrics.DataSufficient)
        {
            return false;
        }

        return metrics.MicrosleepsInLongLookback == 0 &&
               metrics.Perclos is { } perclos &&
               perclos <= _config.PerclosExitCritical;
    }

    private static bool AtLeast(FatigueMetrics metrics, double threshold)
        => metrics.DataSufficient && metrics.Perclos is { } perclos && perclos >= threshold;

    private FatigueEvent WarningEvent(TimeSpan now, FatigueMetrics metrics)
    {
        if (AtLeast(metrics, _config.PerclosWarn))
        {
            return new FatigueEvent(
                now,
                FatigueEventKind.WarningEntered,
                FatigueTrigger.Perclos,
                Perclos: metrics.Perclos,
                Threshold: _config.PerclosWarn);
        }

        if (metrics.BlinkRatePerMinute is { } rate && rate >= _config.BlinkRateWarn)
        {
            return new FatigueEvent(
                now,
                FatigueEventKind.WarningEntered,
                FatigueTrigger.BlinkRate,
                BlinkRate: rate,
                Threshold: _config.BlinkRateWarn);
        }

        return new FatigueEvent(
            now,
            FatigueEventKind.WarningEntered,
            FatigueTrigger.LongClosures,
            LongClosures: metrics.LongClosuresInWindow,
            Threshold: _config.LongClosuresWarn);
    }

    private void Transition(FatigueDisplayState state, TimeSpan now)
    {
        State = state;
        _upgradeSince = null;
        _downgradeSince = null;
        _lastCriticalReminder = state == FatigueDisplayState.Critical ? now : null;
    }

    private void ResetTimers()
    {
        _upgradeSince = null;
        _downgradeSince = null;
        _lastCriticalReminder = null;
    }

    private static TimeSpan Seconds(double value) => TimeSpan.FromSeconds(value);
}
