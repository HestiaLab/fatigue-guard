using FatigueGuard.Core.Analysis;

namespace FatigueGuard.Core.Tests;

public sealed class EarSmootherTests
{
    [Fact]
    public void MedianFilter_RemovesSingleFrameSpike()
    {
        var smoother = new EarSmoother(0.6);
        Assert.Equal(0.3, smoother.Add(0.3), 6);
        Assert.Equal(0.3, smoother.Add(0.3), 6);
        Assert.Equal(0.3, smoother.Add(0.3), 6);

        double smoothed = smoother.Add(0.05);

        Assert.Equal(0.3, smoothed, 6);
    }

    [Fact]
    public void Ema_FollowsConfiguredAlpha()
    {
        var smoother = new EarSmoother(0.6);
        Assert.Equal(0.2, smoother.Add(0.2), 6);
        Assert.Equal(0.26, smoother.Add(0.4), 6);
        Assert.Equal(0.344, smoother.Add(0.4), 6);
    }

    [Fact]
    public void Reset_StartsSmoothingFromScratch()
    {
        var smoother = new EarSmoother(0.6);
        smoother.Add(0.5);
        smoother.Reset();

        Assert.Null(smoother.Smoothed);
        Assert.Equal(0.25, smoother.Add(0.25), 6);
    }

    [Fact]
    public void InvalidAlpha_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EarSmoother(0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EarSmoother(1.5));
    }
}
