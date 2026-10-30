using FatigueGuard.Core.Analysis;
using FatigueGuard.Core.Detection;

namespace FatigueGuard.Core.Tests;

public sealed class EarCalculatorTests
{
    private static readonly LandmarkPoint OpenP1 = new(0, 0);
    private static readonly LandmarkPoint OpenP2 = new(1, 1);
    private static readonly LandmarkPoint OpenP3 = new(3, 1);
    private static readonly LandmarkPoint OpenP4 = new(4, 0);
    private static readonly LandmarkPoint OpenP5 = new(3, -1);
    private static readonly LandmarkPoint OpenP6 = new(1, -1);

    [Fact]
    public void OpenEyeGeometry_ProducesExpectedEar()
    {
        double? ear = EarCalculator.ComputeEyeEar(OpenP1, OpenP2, OpenP3, OpenP4, OpenP5, OpenP6);

        Assert.NotNull(ear);
        Assert.Equal(0.5, ear!.Value, 6);
    }

    [Fact]
    public void ClosedEyeGeometry_ProducesLowEar()
    {
        double? ear = EarCalculator.ComputeEyeEar(
            OpenP1,
            new LandmarkPoint(1, 0.1),
            new LandmarkPoint(3, 0.1),
            OpenP4,
            new LandmarkPoint(3, -0.1),
            new LandmarkPoint(1, -0.1));

        Assert.NotNull(ear);
        Assert.Equal(0.05, ear!.Value, 6);
    }

    [Fact]
    public void ZeroWidth_IsInvalid()
    {
        double? ear = EarCalculator.ComputeEyeEar(
            new LandmarkPoint(2, 0),
            new LandmarkPoint(2, 1),
            new LandmarkPoint(2, 1),
            new LandmarkPoint(2, 0),
            new LandmarkPoint(2, -1),
            new LandmarkPoint(2, -1));

        Assert.Null(ear);
    }

    [Fact]
    public void TooLargeEar_IsRejected()
    {
        double? ear = EarCalculator.ComputeEyeEar(
            OpenP1,
            new LandmarkPoint(1, 2),
            new LandmarkPoint(3, 2),
            OpenP4,
            new LandmarkPoint(3, -2),
            new LandmarkPoint(1, -2));

        Assert.Null(ear);
    }

    [Fact]
    public void Combine_UsesSingleEyeOrAverage()
    {
        Assert.Equal(0.25, EarCalculator.Combine(0.2, 0.3)!.Value, 6);
        Assert.Equal(0.2, EarCalculator.Combine(0.2, null)!.Value, 6);
        Assert.Equal(0.2, EarCalculator.Combine(null, 0.2)!.Value, 6);
        Assert.Null(EarCalculator.Combine(null, null));
    }

    [Fact]
    public void MediaPipeIndexMapping_MatchesConfiguredContours()
    {
        var values = new float[468 * 3];
        SetPoint(values, 33, 0, 0);
        SetPoint(values, 160, 1, 1);
        SetPoint(values, 158, 3, 1);
        SetPoint(values, 133, 4, 0);
        SetPoint(values, 153, 3, -1);
        SetPoint(values, 144, 1, -1);

        SetPoint(values, 362, 10, 0);
        SetPoint(values, 385, 11, 0.5);
        SetPoint(values, 387, 13, 0.5);
        SetPoint(values, 263, 14, 0);
        SetPoint(values, 373, 13, -0.5);
        SetPoint(values, 380, 11, -0.5);

        double? right = EarCalculator.ComputeEyeEar(
            values,
            EyeLandmarks.RightEye[0],
            EyeLandmarks.RightEye[1],
            EyeLandmarks.RightEye[2],
            EyeLandmarks.RightEye[3],
            EyeLandmarks.RightEye[4],
            EyeLandmarks.RightEye[5]);

        double? left = EarCalculator.ComputeEyeEar(
            values,
            EyeLandmarks.LeftEye[0],
            EyeLandmarks.LeftEye[1],
            EyeLandmarks.LeftEye[2],
            EyeLandmarks.LeftEye[3],
            EyeLandmarks.LeftEye[4],
            EyeLandmarks.LeftEye[5]);

        Assert.Equal(0.5, right!.Value, 6);
        Assert.Equal(0.25, left!.Value, 6);
    }

    private static void SetPoint(float[] values, int index, double x, double y)
    {
        values[index * 3] = (float)x;
        values[(index * 3) + 1] = (float)y;
    }
}
