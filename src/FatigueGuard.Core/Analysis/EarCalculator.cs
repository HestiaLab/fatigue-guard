namespace FatigueGuard.Core.Analysis;

public readonly record struct LandmarkPoint(double X, double Y);

public static class EarCalculator
{
    public const double MaxValidEar = 0.6;

    public const double MinWidthPixels = 1e-6;

    public static double? ComputeEyeEar(
        LandmarkPoint p1,
        LandmarkPoint p2,
        LandmarkPoint p3,
        LandmarkPoint p4,
        LandmarkPoint p5,
        LandmarkPoint p6)
    {
        double width = Distance(p1, p4);
        if (width < MinWidthPixels)
        {
            return null;
        }

        double ear = (Distance(p2, p6) + Distance(p3, p5)) / (2.0 * width);
        return ear > 0.0 && ear < MaxValidEar ? ear : null;
    }

    public static double? ComputeEyeEar(ReadOnlySpan<float> xyz, int i1, int i2, int i3, int i4, int i5, int i6)
    {
        return ComputeEyeEar(
            PointAt(xyz, i1),
            PointAt(xyz, i2),
            PointAt(xyz, i3),
            PointAt(xyz, i4),
            PointAt(xyz, i5),
            PointAt(xyz, i6));
    }

    private static LandmarkPoint PointAt(ReadOnlySpan<float> xyz, int index)
        => new(xyz[index * 3], xyz[(index * 3) + 1]);

    public static double? Combine(double? left, double? right)
    {
        if (left.HasValue && right.HasValue)
        {
            return (left.Value + right.Value) / 2.0;
        }

        return left ?? right;
    }

    private static double Distance(LandmarkPoint a, LandmarkPoint b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
