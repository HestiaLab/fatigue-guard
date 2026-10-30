using FatigueGuard.Core.Analysis;
using FatigueGuard.Core.Config;

namespace FatigueGuard.Core.Tests;

public sealed class MetricWindowTests
{
    private static MetricWindow CreateWindow(double minValid = 30)
    {
        var config = new MetricsConfig { WindowSeconds = 60, MinValidSeconds = minValid };
        return new MetricWindow(config, microsleepLookbackSeconds: 60);
    }

    [Fact]
    public void Perclos_UsesRollingWindow()
    {
        MetricWindow window = CreateWindow();
        for (int end = 1; end <= 100; end++)
        {
            window.AddInterval(TimeSpan.FromSeconds(end), 1.0, closed: end % 6 == 0);
        }

        FatigueMetrics metrics = window.Snapshot(TimeSpan.FromSeconds(100));

        Assert.Equal(60.0, metrics.ValidSeconds, 6);
        Assert.Equal(10 / 60.0, metrics.Perclos!.Value, 6);
        Assert.True(metrics.DataSufficient);
    }

    [Fact]
    public void Perclos_ExcludesIntervalsOlderThanWindow()
    {
        MetricWindow window = CreateWindow();
        for (int end = 1; end <= 40; end++)
        {
            window.AddInterval(TimeSpan.FromSeconds(end), 1.0, closed: true);
        }

        FatigueMetrics metrics = window.Snapshot(TimeSpan.FromSeconds(100));

        Assert.Equal(0.0, metrics.ValidSeconds, 6);
        Assert.Null(metrics.Perclos);
    }

    [Fact]
    public void BlinkRate_UsesValidSecondsAndRequiresMinimumData()
    {
        MetricWindow window = CreateWindow(minValid: 30);
        for (int end = 1; end <= 60; end++)
        {
            window.AddInterval(TimeSpan.FromSeconds(end), 1.0, closed: false);
        }

        for (int i = 0; i < 15; i++)
        {
            window.RecordBlink(TimeSpan.FromSeconds(10 + (i * 3)));
        }

        FatigueMetrics metrics = window.Snapshot(TimeSpan.FromSeconds(60));
        Assert.True(metrics.DataSufficient);
        Assert.Equal(15.0, metrics.BlinkRatePerMinute!.Value, 6);

        MetricWindow shortWindow = CreateWindow(minValid: 30);
        for (int end = 1; end <= 20; end++)
        {
            shortWindow.AddInterval(TimeSpan.FromSeconds(end), 1.0, closed: false);
        }

        shortWindow.RecordBlink(TimeSpan.FromSeconds(5));
        FatigueMetrics shortMetrics = shortWindow.Snapshot(TimeSpan.FromSeconds(20));
        Assert.False(shortMetrics.DataSufficient);
        Assert.Null(shortMetrics.BlinkRatePerMinute);
        Assert.Equal(1, shortMetrics.BlinksInWindow);
    }

    [Fact]
    public void LongClosures_TrackWindowAndLookback()
    {
        MetricWindow window = CreateWindow();
        window.RecordLongClosure(TimeSpan.FromSeconds(10));
        window.RecordLongClosure(TimeSpan.FromSeconds(50));
        window.RecordLongClosure(TimeSpan.FromSeconds(80));

        FatigueMetrics metrics = window.Snapshot(TimeSpan.FromSeconds(125));
        Assert.Equal(1, metrics.LongClosuresInWindow);
        Assert.Equal(3, metrics.LongClosuresInLookback);

        metrics = window.Snapshot(TimeSpan.FromSeconds(131));
        Assert.Equal(1, metrics.LongClosuresInWindow);
        Assert.Equal(2, metrics.LongClosuresInLookback);

        metrics = window.Snapshot(TimeSpan.FromSeconds(200));
        Assert.Equal(0, metrics.LongClosuresInWindow);
        Assert.Equal(0, metrics.LongClosuresInLookback);
    }

    [Fact]
    public void Microsleeps_TrackConfiguredLookback()
    {
        MetricWindow window = CreateWindow();
        window.RecordMicrosleep(TimeSpan.FromSeconds(10));

        Assert.Equal(1, window.Snapshot(TimeSpan.FromSeconds(60)).MicrosleepsInLookback);
        Assert.Equal(0, window.Snapshot(TimeSpan.FromSeconds(75)).MicrosleepsInLookback);
        Assert.Equal(0, window.Snapshot(TimeSpan.FromSeconds(200)).MicrosleepsInLongLookback);
    }

    [Fact]
    public void QualityRatio_UsesFrameCountsInTenSeconds()
    {
        MetricWindow window = CreateWindow();
        for (int i = 0; i < 100; i++)
        {
            window.AddFrame(TimeSpan.FromSeconds(20.1 + (i * 0.1)), valid: i % 2 == 0);
        }

        double? ratio = window.QualityRatio(TimeSpan.FromSeconds(30));

        Assert.NotNull(ratio);
        Assert.Equal(0.5, ratio!.Value, 6);
    }

    [Fact]
    public void Reset_ClearsAllData()
    {
        MetricWindow window = CreateWindow();
        window.AddInterval(TimeSpan.FromSeconds(1), 1.0, closed: true);
        window.RecordBlink(TimeSpan.FromSeconds(1));
        window.AddFrame(TimeSpan.FromSeconds(1), valid: true);

        window.Reset();
        FatigueMetrics metrics = window.Snapshot(TimeSpan.FromSeconds(1));

        Assert.Equal(0.0, metrics.ValidSeconds, 6);
        Assert.Null(metrics.QualityValidRatio);
        Assert.Equal(0, metrics.BlinksInWindow);
    }
}
