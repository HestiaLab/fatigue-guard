namespace FatigueGuard.Core.Analysis;

public static class Statistics
{
    public static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            throw new ArgumentException("至少需要一个样本。", nameof(values));
        }

        if (percentile < 0.0 || percentile > 100.0)
        {
            throw new ArgumentOutOfRangeException(nameof(percentile));
        }

        double[] sorted = values.ToArray();
        Array.Sort(sorted);

        if (sorted.Length == 1)
        {
            return sorted[0];
        }

        double rank = (percentile / 100.0) * (sorted.Length - 1);
        int lower = (int)Math.Floor(rank);
        int upper = (int)Math.Ceiling(rank);
        if (lower == upper)
        {
            return sorted[lower];
        }

        double weight = rank - lower;
        return (sorted[lower] * (1.0 - weight)) + (sorted[upper] * weight);
    }
}
