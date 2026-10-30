using FatigueGuard.Core.Config;

namespace FatigueGuard.Core.Analysis;

public sealed record FatigueMetrics(
    double? Perclos,
    double? BlinkRatePerMinute,
    double ValidSeconds,
    bool DataSufficient,
    int BlinksInWindow,
    int LongClosuresInWindow,
    int LongClosuresInLookback,
    int MicrosleepsInLookback,
    int MicrosleepsInLongLookback,
    double? QualityValidRatio);

public sealed class MetricWindow
{
    public const double QualityWindowSeconds = 10.0;

    public const double DowngradeLookbackSeconds = 120.0;

    private readonly MetricsConfig _config;
    private readonly double _microsleepLookbackSeconds;

    private readonly List<Interval> _intervals = new();
    private readonly Queue<TimeSpan> _blinks = new();
    private readonly Queue<TimeSpan> _longClosures = new();
    private readonly Queue<TimeSpan> _microsleeps = new();
    private readonly Queue<FrameMark> _frames = new();
    private int _head;

    public MetricWindow(MetricsConfig config, double microsleepLookbackSeconds)
    {
        _config = config;
        _microsleepLookbackSeconds = microsleepLookbackSeconds;
    }

    public void AddFrame(TimeSpan time, bool valid)
    {
        _frames.Enqueue(new FrameMark(time, valid));
        PruneFrames(time);
    }

    public void AddInterval(TimeSpan end, double dtSeconds, bool closed)
    {
        if (dtSeconds <= 0.0)
        {
            return;
        }

        _intervals.Add(new Interval(end, dtSeconds, closed));
        PruneIntervals(end);
    }

    public void RecordBlink(TimeSpan time) => _blinks.Enqueue(time);

    public void RecordLongClosure(TimeSpan time) => _longClosures.Enqueue(time);

    public void RecordMicrosleep(TimeSpan time) => _microsleeps.Enqueue(time);

    public void Reset()
    {
        _intervals.Clear();
        _blinks.Clear();
        _longClosures.Clear();
        _microsleeps.Clear();
        _frames.Clear();
        _head = 0;
    }

    public double? QualityRatio(TimeSpan now)
    {
        TimeSpan cutoff = now - TimeSpan.FromSeconds(QualityWindowSeconds);
        int total = 0;
        int valid = 0;
        foreach (FrameMark frame in _frames)
        {
            if (frame.Time <= cutoff)
            {
                continue;
            }

            total++;
            if (frame.Valid)
            {
                valid++;
            }
        }

        return total == 0 ? null : (double)valid / total;
    }

    public FatigueMetrics Snapshot(TimeSpan now)
    {
        TimeSpan windowCutoff = now - TimeSpan.FromSeconds(_config.WindowSeconds);
        double validSeconds = 0.0;
        double closedSeconds = 0.0;
        for (int i = _head; i < _intervals.Count; i++)
        {
            Interval interval = _intervals[i];
            if (interval.End <= windowCutoff)
            {
                continue;
            }

            validSeconds += interval.DtSeconds;
            if (interval.Closed)
            {
                closedSeconds += interval.DtSeconds;
            }
        }

        double? perclos = validSeconds > 0.0 ? closedSeconds / validSeconds : null;
        bool sufficient = validSeconds >= _config.MinValidSeconds;

        TimeSpan longLookbackCutoff = now - TimeSpan.FromSeconds(DowngradeLookbackSeconds);
        TimeSpan microsleepLookbackCutoff = now - TimeSpan.FromSeconds(_microsleepLookbackSeconds);

        TrimEvents(_blinks, now);
        TrimEvents(_longClosures, now);
        TrimEvents(_microsleeps, now);

        int blinks = CountSince(_blinks, windowCutoff);
        int longClosures = CountSince(_longClosures, windowCutoff);
        int longClosuresLookback = CountSince(_longClosures, longLookbackCutoff);
        int microsleepsLookback = CountSince(_microsleeps, microsleepLookbackCutoff);
        int microsleepsLongLookback = CountSince(_microsleeps, longLookbackCutoff);

        double? blinkRate = sufficient && validSeconds > 0.0
            ? blinks * 60.0 / validSeconds
            : null;

        return new FatigueMetrics(
            perclos,
            blinkRate,
            validSeconds,
            sufficient,
            blinks,
            longClosures,
            longClosuresLookback,
            microsleepsLookback,
            microsleepsLongLookback,
            QualityRatio(now));
    }

    private void PruneIntervals(TimeSpan now)
    {
        TimeSpan cutoff = now - TimeSpan.FromSeconds(_config.WindowSeconds + 2.0);
        while (_head < _intervals.Count && _intervals[_head].End < cutoff)
        {
            _head++;
        }

        if (_head > 4096)
        {
            _intervals.RemoveRange(0, _head);
            _head = 0;
        }
    }

    private void PruneFrames(TimeSpan now)
    {
        TimeSpan cutoff = now - TimeSpan.FromSeconds(QualityWindowSeconds + 2.0);
        while (_frames.Count > 0 && _frames.Peek().Time < cutoff)
        {
            _frames.Dequeue();
        }
    }

    private static void TrimEvents(Queue<TimeSpan> queue, TimeSpan now)
    {
        TimeSpan cutoff = now - TimeSpan.FromSeconds(DowngradeLookbackSeconds + 2.0);
        while (queue.Count > 0 && queue.Peek() < cutoff)
        {
            queue.Dequeue();
        }
    }

    private static int CountSince(Queue<TimeSpan> queue, TimeSpan cutoff)
    {
        int count = 0;
        foreach (TimeSpan time in queue)
        {
            if (time > cutoff)
            {
                count++;
            }
        }

        return count;
    }

    private readonly record struct Interval(TimeSpan End, double DtSeconds, bool Closed);

    private readonly record struct FrameMark(TimeSpan Time, bool Valid);
}
