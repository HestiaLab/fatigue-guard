using FatigueGuard.Core.Analysis;
using FatigueGuard.Core.Config;

namespace FatigueGuard.Core.Tests;

public sealed class ClosureTrackerTests
{
    private static ClosureTracker CreateTracker() => new(new BlinkConfig());

    private static TimeSpan T(double seconds) => TimeSpan.FromSeconds(seconds);

    [Fact]
    public void ThreeFrameDip_IsBlink()
    {
        var tracker = CreateTracker();
        Assert.False(tracker.Update(T(0.0), closed: true, T(0.0), false).MicrosleepDetectedNow);
        tracker.Update(T(0.033), closed: true, T(0.033), false);
        tracker.Update(T(0.066), closed: true, T(0.033), false);
        tracker.Update(T(0.100), closed: false, T(0.034), false);

        ClosureUpdate update = tracker.Update(T(0.133), closed: false, T(0.033), false);

        Assert.Equal(ClosureOutcome.Blink, update.Outcome);
        Assert.Equal(0.066, update.DurationSeconds, 3);
    }

    [Fact]
    public void LongClosure_IsClassified()
    {
        var tracker = CreateTracker();
        for (int i = 0; i <= 18; i++)
        {
            tracker.Update(T(i * 0.033), closed: true, T(0.033), false);
        }

        tracker.Update(T(0.627), closed: false, T(0.033), false);
        ClosureUpdate update = tracker.Update(T(0.660), closed: false, T(0.033), false);

        Assert.Equal(ClosureOutcome.LongClosure, update.Outcome);
        Assert.Equal(0.594, update.DurationSeconds, 3);
    }

    [Fact]
    public void Microsleep_IsReportedImmediatelyAtThreshold()
    {
        var tracker = CreateTracker();
        bool reported = false;
        double reportedAt = 0.0;

        for (int i = 0; i <= 40; i++)
        {
            double seconds = i * 0.033;
            ClosureUpdate update = tracker.Update(T(seconds), closed: true, T(0.033), false);
            if (update.MicrosleepDetectedNow)
            {
                reported = true;
                reportedAt = seconds;
                Assert.True(reportedAt >= 1.2);
            }
        }

        Assert.True(reported);
        Assert.True(reportedAt < 1.3);
    }

    [Fact]
    public void Microsleep_NotReportedTwiceWithinSameSegment()
    {
        var tracker = CreateTracker();
        int reportCount = 0;
        for (int i = 0; i <= 60; i++)
        {
            ClosureUpdate update = tracker.Update(T(i * 0.033), closed: true, T(0.033), false);
            if (update.MicrosleepDetectedNow)
            {
                reportCount++;
            }
        }

        Assert.Equal(1, reportCount);
    }

    [Fact]
    public void Gap_AbortsCurrentSegment()
    {
        var tracker = CreateTracker();
        tracker.Update(T(0.0), closed: true, T(0.0), false);
        tracker.Update(T(0.033), closed: true, T(0.033), false);
        tracker.Update(T(0.066), closed: true, T(0.033), false);

        ClosureUpdate gapped = tracker.Update(T(1.0), closed: true, T(0.934), gapExceeded: true);

        Assert.Equal(ClosureOutcome.None, gapped.Outcome);
        Assert.False(tracker.IsActive);

        tracker.Update(T(1.033), closed: false, T(0.033), false);
        ClosureUpdate after = tracker.Update(T(1.066), closed: false, T(0.033), false);
        Assert.Equal(ClosureOutcome.None, after.Outcome);
    }

    [Fact]
    public void VeryShortDip_IsIgnored()
    {
        var tracker = CreateTracker();
        tracker.Update(T(0.0), closed: true, T(0.0), false);
        tracker.Update(T(0.033), closed: false, T(0.033), false);

        ClosureUpdate update = tracker.Update(T(0.066), closed: false, T(0.033), false);

        Assert.Equal(ClosureOutcome.None, update.Outcome);
    }

    [Fact]
    public void ReopeningRequiresTwoConsecutiveOpenFrames()
    {
        var tracker = CreateTracker();
        tracker.Update(T(0.0), closed: true, T(0.0), false);
        tracker.Update(T(0.033), closed: true, T(0.033), false);
        tracker.Update(T(0.066), closed: true, T(0.033), false);
        tracker.Update(T(0.100), closed: false, T(0.034), false);
        Assert.True(tracker.IsActive);

        ClosureUpdate update = tracker.Update(T(0.133), closed: false, T(0.033), false);
        Assert.Equal(ClosureOutcome.Blink, update.Outcome);
        Assert.False(tracker.IsActive);
    }
}
