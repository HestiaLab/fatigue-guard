using FatigueGuard.Core.Config;

namespace FatigueGuard.Core.Analysis;

public enum ClosureOutcome
{
    None,
    Blink,
    LongClosure,
    Microsleep,
}

public readonly record struct ClosureUpdate(
    ClosureOutcome Outcome,
    double DurationSeconds,
    bool MicrosleepDetectedNow);

public sealed class ClosureTracker
{
    private readonly BlinkConfig _config;
    private bool _active;
    private bool _microsleepReported;
    private TimeSpan _start;
    private TimeSpan _lastClosed;
    private int _openStreak;

    public ClosureTracker(BlinkConfig config)
    {
        _config = config;
    }

    public bool IsActive => _active;

    public void Abort()
    {
        _active = false;
        _microsleepReported = false;
        _openStreak = 0;
    }

    public ClosureUpdate Update(TimeSpan time, bool closed, TimeSpan dt, bool gapExceeded)
    {
        if (gapExceeded)
        {
            Abort();
            return default;
        }

        bool microsleepNow = false;
        ClosureOutcome outcome = ClosureOutcome.None;
        double duration = 0.0;

        if (closed)
        {
            if (!_active)
            {
                _active = true;
                _start = time;
                _microsleepReported = false;
            }

            _openStreak = 0;
            _lastClosed = time;
            duration = (time - _start).TotalSeconds;
            if (!_microsleepReported && duration * 1000.0 >= _config.MicrosleepMs)
            {
                _microsleepReported = true;
                microsleepNow = true;
            }
        }
        else if (_active)
        {
            _openStreak++;
            if (_openStreak >= 2)
            {
                duration = (_lastClosed - _start).TotalSeconds;
                _active = false;
                double ms = duration * 1000.0;
                if (ms >= _config.MicrosleepMs)
                {
                    outcome = ClosureOutcome.Microsleep;
                }
                else if (ms >= _config.BlinkMaxMs)
                {
                    outcome = ClosureOutcome.LongClosure;
                }
                else if (ms >= _config.MinClosedMs)
                {
                    outcome = ClosureOutcome.Blink;
                }
            }
            else
            {
                duration = (_lastClosed - _start).TotalSeconds;
            }
        }

        return new ClosureUpdate(outcome, duration, microsleepNow);
    }
}
