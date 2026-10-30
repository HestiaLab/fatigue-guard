using FatigueGuard.Core.Config;

namespace FatigueGuard.Core.Analysis;

public sealed record CalibrationResult(
    double BaselineEar,
    double ClosedThreshold,
    bool Insufficient,
    double ValidRatio,
    double ElapsedSeconds);

public sealed class CalibrationTracker
{
    public const double DefaultBaselineEar = 0.28;

    private readonly CalibrationConfig _config;
    private readonly BlinkConfig _blinkConfig;
    private readonly List<double> _samples = new();
    private TimeSpan? _start;
    private long _frames;
    private long _validFrames;

    public CalibrationTracker(CalibrationConfig config, BlinkConfig blinkConfig)
    {
        _config = config;
        _blinkConfig = blinkConfig;
    }

    public double ValidRatio => _frames == 0 ? 0.0 : (double)_validFrames / _frames;

    public double ElapsedSeconds(TimeSpan now) => _start is null ? 0.0 : (now - _start.Value).TotalSeconds;

    public void Record(TimeSpan time, bool valid, double? smoothedEar)
    {
        _start ??= time;
        _frames++;
        if (valid && smoothedEar.HasValue)
        {
            _validFrames++;
            _samples.Add(smoothedEar.Value);
        }
    }

    public CalibrationResult? TryComplete(TimeSpan now)
    {
        if (_start is null)
        {
            return null;
        }

        double elapsed = ElapsedSeconds(now);
        if (elapsed < _config.Seconds)
        {
            return null;
        }

        double ratio = ValidRatio;
        if (ratio >= _config.MinValidRatio && _samples.Count > 0)
        {
            double baseline = Statistics.Percentile(_samples, _config.BaselinePercentile);
            return new CalibrationResult(baseline, ComputeClosedThreshold(baseline, _blinkConfig), false, ratio, elapsed);
        }

        if (elapsed >= _config.MaxSeconds)
        {
            double baseline = DefaultBaselineEar;
            return new CalibrationResult(baseline, ComputeClosedThreshold(baseline, _blinkConfig), true, ratio, elapsed);
        }

        return null;
    }

    public static double ComputeClosedThreshold(double baseline, BlinkConfig config)
    {
        double raw = config.ClosedThresholdRatio * baseline;
        return Math.Clamp(raw, config.ClosedThresholdMin, config.ClosedThresholdMax);
    }
}
