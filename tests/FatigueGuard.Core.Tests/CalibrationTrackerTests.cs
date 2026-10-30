using FatigueGuard.Core.Analysis;
using FatigueGuard.Core.Config;

namespace FatigueGuard.Core.Tests;

public sealed class CalibrationTrackerTests
{
    private static CalibrationTracker CreateTracker(double seconds = 2, double maxSeconds = 4, double minRatio = 0.7)
    {
        var config = new CalibrationConfig
        {
            Seconds = seconds,
            MaxSeconds = maxSeconds,
            MinValidRatio = minRatio,
            BaselinePercentile = 75,
        };

        return new CalibrationTracker(config, new BlinkConfig());
    }

    [Fact]
    public void FullValidCalibration_UsesPercentileBaseline()
    {
        CalibrationTracker tracker = CreateTracker();
        CalibrationResult? result = null;

        for (int i = 0; i <= 60; i++)
        {
            TimeSpan time = TimeSpan.FromSeconds(i / TestSupport.Fps);
            tracker.Record(time, valid: true, smoothedEar: 0.30);
            result = tracker.TryComplete(time);
            if (i < 60)
            {
                Assert.Null(result);
            }
        }

        Assert.NotNull(result);
        Assert.False(result!.Insufficient);
        Assert.Equal(0.30, result.BaselineEar, 6);
        Assert.Equal(0.16, result.ClosedThreshold, 6);
        Assert.Equal(1.0, result.ValidRatio, 6);
    }

    [Fact]
    public void LowValidRatio_ExtendsUntilMaxThenFallsBack()
    {
        CalibrationTracker tracker = CreateTracker();
        CalibrationResult? result = null;

        for (int i = 0; i <= 120; i++)
        {
            TimeSpan time = TimeSpan.FromSeconds(i / TestSupport.Fps);
            bool valid = i % 2 == 0;
            tracker.Record(time, valid, valid ? 0.30 : null);
            result = tracker.TryComplete(time);
            if (time.TotalSeconds < 4.0)
            {
                Assert.Null(result);
            }
        }

        Assert.NotNull(result);
        Assert.True(result!.Insufficient);
        Assert.Equal(CalibrationTracker.DefaultBaselineEar, result.BaselineEar, 6);
        Assert.Equal(0.16, result.ClosedThreshold, 6);
    }

    [Fact]
    public void ValidRatioRecovering_CompletesBeforeMaxSeconds()
    {
        CalibrationTracker tracker = CreateTracker(seconds: 2, maxSeconds: 6);
        CalibrationResult? result = null;
        TimeSpan? completedAt = null;

        for (int i = 0; i <= 150; i++)
        {
            TimeSpan time = TimeSpan.FromSeconds(i / TestSupport.Fps);
            bool valid = time.TotalSeconds < 2.0 ? i % 2 == 0 : true;
            tracker.Record(time, valid, valid ? 0.30 : null);
            CalibrationResult? current = tracker.TryComplete(time);
            if (current is not null && result is null)
            {
                result = current;
                completedAt = time;
            }
        }

        Assert.NotNull(result);
        Assert.False(result!.Insufficient);
        Assert.NotNull(completedAt);
        Assert.True(completedAt!.Value.TotalSeconds < 5.0);
    }

    [Fact]
    public void Percentile_InterpolatesLinearly()
    {
        double[] values = { 0.10, 0.20, 0.30, 0.40 };
        Assert.Equal(0.325, Statistics.Percentile(values, 75), 6);
    }

    [Fact]
    public void ClosedThreshold_IsClamped()
    {
        var blink = new BlinkConfig();
        Assert.Equal(0.08, CalibrationTracker.ComputeClosedThreshold(0.10, blink), 6);
        Assert.Equal(0.16, CalibrationTracker.ComputeClosedThreshold(0.40, blink), 6);
        Assert.Equal(0.12, CalibrationTracker.ComputeClosedThreshold(0.20, blink), 6);
    }
}
