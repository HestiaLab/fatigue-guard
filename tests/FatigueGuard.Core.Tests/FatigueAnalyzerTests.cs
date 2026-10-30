using FatigueGuard.Core;
using FatigueGuard.Core.Analysis;
using FatigueGuard.Core.Config;

namespace FatigueGuard.Core.Tests;

public sealed class FatigueAnalyzerTests
{
    private static (List<FatigueUpdate> Updates, List<FatigueEvent> Events) Run(
        FatigueAnalyzer analyzer,
        IEnumerable<FaceObservation> frames)
    {
        var updates = new List<FatigueUpdate>();
        var events = new List<FatigueEvent>();
        foreach (FaceObservation frame in frames)
        {
            FatigueUpdate update = analyzer.Process(frame);
            updates.Add(update);
            events.AddRange(update.Events);
        }

        return (updates, events);
    }

    private static IEnumerable<FaceObservation> OpenFrames(double seconds, double ear = 0.30)
    {
        int count = (int)Math.Round(seconds * TestSupport.Fps);
        for (int i = 0; i <= count; i++)
        {
            yield return TestSupport.Frame(i, ear);
        }
    }

    [Fact]
    public void SkipCalibration_UsesDefaultBaseline()
    {
        var analyzer = new FatigueAnalyzer(TestSupport.BaseConfig(), skipCalibration: true);
        FatigueUpdate update = analyzer.Process(TestSupport.Frame(0, 0.30));

        FatigueEvent skipped = Assert.Single(update.Events);
        Assert.Equal(FatigueEventKind.CalibrationSkipped, skipped.Kind);
        Assert.Equal(0.28, skipped.BaselineEar!.Value, 6);
        Assert.Equal(0.16, skipped.ClosedEarThreshold!.Value, 6);
        Assert.Equal(FatigueDisplayState.Ok, update.DisplayState);
    }

    [Fact]
    public void Calibration_CompletesAndReportsBaseline()
    {
        var config = TestSupport.BaseConfig();
        config.Calibration.Seconds = 1.0;
        config.Calibration.MaxSeconds = 2.0;
        var analyzer = new FatigueAnalyzer(config, skipCalibration: false);

        (List<FatigueUpdate> updates, List<FatigueEvent> events) = Run(analyzer, OpenFrames(1.5));

        Assert.Contains(events, e => e.Kind == FatigueEventKind.CalibrationStarted);
        FatigueEvent completed = Assert.Single(events, e => e.Kind == FatigueEventKind.CalibrationCompleted);
        Assert.Equal(1.0, completed.Time.TotalSeconds, 3);
        Assert.Equal(0.30, completed.BaselineEar!.Value, 6);
        Assert.Equal(0.16, completed.ClosedEarThreshold!.Value, 6);
        Assert.Equal(FatigueDisplayState.Ok, updates[^1].DisplayState);
        Assert.False(updates[^1].CalibrationInsufficient);
    }

    [Fact]
    public void Calibration_InsufficientFallsBackToDefaults()
    {
        var config = TestSupport.BaseConfig();
        config.Calibration.Seconds = 1.0;
        config.Calibration.MaxSeconds = 2.0;
        var analyzer = new FatigueAnalyzer(config, skipCalibration: false);

        var frames = new List<FaceObservation>();
        for (int i = 0; i <= 75; i++)
        {
            bool valid = i % 2 == 0;
            frames.Add(TestSupport.Frame(i, valid ? 0.30 : null));
        }

        (List<FatigueUpdate> updates, List<FatigueEvent> events) = Run(analyzer, frames);

        FatigueEvent insufficient = Assert.Single(events, e => e.Kind == FatigueEventKind.CalibrationInsufficient);
        Assert.Equal(2.0, insufficient.Time.TotalSeconds, 3);
        Assert.Equal(0.28, insufficient.BaselineEar!.Value, 6);
        Assert.Equal(0.16, insufficient.ClosedEarThreshold!.Value, 6);
        Assert.True(updates[^1].CalibrationInsufficient);
    }

    [Fact]
    public void NoFace_IsReportedAfterTwoSeconds()
    {
        var analyzer = new FatigueAnalyzer(TestSupport.BaseConfig(), skipCalibration: true);
        var frames = new List<FaceObservation>();
        for (int i = 0; i <= 75; i++)
        {
            frames.Add(TestSupport.Frame(i, ear: null, faceCount: 0));
        }

        (List<FatigueUpdate> updates, List<FatigueEvent> events) = Run(analyzer, frames);

        FatigueEvent noFace = Assert.Single(events, e => e.Kind == FatigueEventKind.NoFace);
        Assert.InRange(noFace.Time.TotalSeconds, 2.0, 2.1);
        Assert.Equal(FatigueDisplayState.NoFace, updates[^1].DisplayState);
        Assert.Equal(0, updates[^1].FaceCount);
    }

    [Fact]
    public void FaceRestored_IsReportedOnce()
    {
        var analyzer = new FatigueAnalyzer(TestSupport.BaseConfig(), skipCalibration: true);
        var frames = new List<FaceObservation>();
        for (int i = 0; i <= 75; i++)
        {
            frames.Add(TestSupport.Frame(i, ear: null, faceCount: 0));
        }

        for (int i = 76; i <= 100; i++)
        {
            frames.Add(TestSupport.Frame(i, 0.30));
        }

        (List<FatigueUpdate> updates, List<FatigueEvent> events) = Run(analyzer, frames);

        Assert.Single(events, e => e.Kind == FatigueEventKind.NoFace);
        Assert.Single(events, e => e.Kind == FatigueEventKind.FaceRestored);
        Assert.Equal(FatigueDisplayState.Ok, updates[^1].DisplayState);
    }

    [Fact]
    public void Microsleep_TriggersCriticalWithinClosureWindow()
    {
        var analyzer = new FatigueAnalyzer(TestSupport.BaseConfig(), skipCalibration: true);
        var frames = new List<FaceObservation>();
        frames.AddRange(OpenFrames(30.0));

        int start = frames.Count;
        for (int i = start; i < start + 45; i++)
        {
            frames.Add(TestSupport.Frame(i, 0.03));
        }

        (List<FatigueUpdate> updates, List<FatigueEvent> events) = Run(analyzer, frames);

        FatigueEvent critical = Assert.Single(events, e => e.Kind == FatigueEventKind.CriticalEntered);
        Assert.Equal(FatigueTrigger.Microsleep, critical.Trigger);
        Assert.NotNull(critical.MicrosleepSeconds);
        Assert.True(critical.MicrosleepSeconds!.Value >= 1.2);
        Assert.InRange(critical.Time.TotalSeconds, 30.0, 31.5);
        Assert.Equal(FatigueDisplayState.Critical, updates[^1].DisplayState);
    }

    [Fact]
    public void FrameGap_DiscardsPendingClosure()
    {
        var analyzer = new FatigueAnalyzer(TestSupport.BaseConfig(), skipCalibration: true);
        var frames = new List<FaceObservation>();
        for (int i = 0; i <= 6; i++)
        {
            frames.Add(TestSupport.Frame(i, 0.30));
        }

        for (int i = 7; i <= 18; i++)
        {
            frames.Add(TestSupport.Frame(i, 0.03));
        }

        long gapIndex = 49;
        for (int i = (int)gapIndex; i <= gapIndex + 10; i++)
        {
            frames.Add(TestSupport.Frame(i, 0.30));
        }

        (List<FatigueUpdate> updates, List<FatigueEvent> events) = Run(analyzer, frames);

        Assert.DoesNotContain(events, e => e.Kind == FatigueEventKind.CriticalEntered);
        Assert.Equal(0, updates[^1].BlinksInWindow);
        Assert.Equal(0, updates[^1].LongClosuresInWindow);
    }

    [Fact]
    public void PerclosWarning_IsRaisedAfterDwell()
    {
        var analyzer = new FatigueAnalyzer(TestSupport.BaseConfig(), skipCalibration: true);
        var frames = new List<FaceObservation>();
        for (int i = 0; i <= (int)(66 * TestSupport.Fps); i++)
        {
            double t = i / TestSupport.Fps;
            bool closed = t % 2.0 < 0.45;
            frames.Add(TestSupport.Frame(i, closed ? 0.03 : 0.30));
        }

        (List<FatigueUpdate> updates, List<FatigueEvent> events) = Run(analyzer, frames);

        FatigueEvent warning = Assert.Single(events, e => e.Kind == FatigueEventKind.WarningEntered);
        Assert.Equal(FatigueTrigger.Perclos, warning.Trigger);
        Assert.InRange(warning.Time.TotalSeconds, 30.0, 40.0);
        Assert.True(updates[^1].DataSufficient);
    }

    [Fact]
    public void QualityLow_IsRaisedAndCleared()
    {
        var analyzer = new FatigueAnalyzer(TestSupport.BaseConfig(), skipCalibration: true);
        var frames = new List<FaceObservation>();
        for (int i = 0; i <= 315; i++)
        {
            frames.Add(TestSupport.Frame(i, ear: null, faceCount: 1));
        }

        for (int i = 316; i <= 500; i++)
        {
            frames.Add(TestSupport.Frame(i, 0.30));
        }

        (List<FatigueUpdate> updates, List<FatigueEvent> events) = Run(analyzer, frames);

        FatigueEvent low = Assert.Single(events, e => e.Kind == FatigueEventKind.QualityLow);
        Assert.InRange(low.Time.TotalSeconds, 10.0, 10.1);
        Assert.Single(events, e => e.Kind == FatigueEventKind.QualityRestored);
        Assert.Equal(FatigueDisplayState.Ok, updates[^1].DisplayState);
    }

    [Fact]
    public void ShallowBlink_IsDetectedWithMoreSensitiveThresholds()
    {
        var config = TestSupport.BaseConfig();
        config.Calibration.BaselineOverride = 0.28;
        config.Blink.ClosedThresholdRatio = 0.75;
        config.Blink.ClosedThresholdMax = 0.22;
        config.Blink.MinClosedMs = 40;
        var analyzer = new FatigueAnalyzer(config, skipCalibration: true);

        var frames = new List<FaceObservation>();
        long index = 0;
        for (int i = 0; i < 30; i++)
        {
            frames.Add(TestSupport.Frame(index++, 0.28));
        }

        for (int i = 0; i < 4; i++)
        {
            frames.Add(TestSupport.Frame(index++, 0.18));
        }

        for (int i = 0; i < 12; i++)
        {
            frames.Add(TestSupport.Frame(index++, 0.28));
        }

        (List<FatigueUpdate> updates, _) = Run(analyzer, frames);

        Assert.Equal(1, updates[^1].BlinksInWindow);
    }

    [Fact]
    public void SingleEye_IsUsedWhenOtherEyeInvalid()
    {
        var analyzer = new FatigueAnalyzer(TestSupport.BaseConfig(), skipCalibration: true);
        FatigueUpdate? last = null;
        for (int i = 0; i < 5; i++)
        {
            last = analyzer.Process(new FaceObservation(
                i,
                TimeSpan.FromSeconds(i / TestSupport.Fps),
                new OpenCvSharp.Rect(10, 10, 100, 100),
                1,
                LeftEar: null,
                RightEar: 0.30));
        }

        Assert.NotNull(last);
        Assert.Equal(0.30, last!.SmoothedEar!.Value, 6);
        Assert.False(last.DataSufficient);
        Assert.Null(last.BlinkRatePerMinute);
    }

    [Fact]
    public void SameSyntheticStream_ProducesIdenticalEvents()
    {
        var frames = new List<FaceObservation>();
        for (int i = 0; i <= (int)(40 * TestSupport.Fps); i++)
        {
            double t = i / TestSupport.Fps;
            double ear = (t % 7.0) switch
            {
                < 0.3 => 0.02,
                < 0.5 => 0.30,
                _ => 0.29,
            };
            frames.Add(TestSupport.Frame(i, ear));
        }

        (List<FatigueUpdate> u1, List<FatigueEvent> e1) =
            Run(new FatigueAnalyzer(TestSupport.BaseConfig(), skipCalibration: true), frames);
        (List<FatigueUpdate> u2, List<FatigueEvent> e2) =
            Run(new FatigueAnalyzer(TestSupport.BaseConfig(), skipCalibration: true), frames);

        Assert.Equal(e1.Count, e2.Count);
        for (int i = 0; i < e1.Count; i++)
        {
            Assert.Equal(e1[i], e2[i]);
        }

        Assert.Equal(u1.Count, u2.Count);
        for (int i = 0; i < u1.Count; i++)
        {
            Assert.Equal(u1[i].DisplayState, u2[i].DisplayState);
            Assert.Equal(u1[i].Perclos, u2[i].Perclos);
            Assert.Equal(u1[i].SmoothedEar, u2[i].SmoothedEar);
            Assert.Equal(u1[i].BlinkRatePerMinute, u2[i].BlinkRatePerMinute);
        }
    }
}
