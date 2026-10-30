namespace FatigueGuard.Core.Analysis;

public sealed class EarSmoother
{
    private const int MedianWindow = 3;

    private readonly double _alpha;
    private readonly double[] _window = new double[MedianWindow];
    private int _count;
    private double? _ema;

    public EarSmoother(double alpha = 0.6)
    {
        if (alpha <= 0.0 || alpha > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(alpha));
        }

        _alpha = alpha;
    }

    public double? Smoothed => _ema;

    public double Add(double raw)
    {
        if (_count < MedianWindow)
        {
            _window[_count++] = raw;
        }
        else
        {
            _window[0] = _window[1];
            _window[1] = _window[2];
            _window[2] = raw;
        }

        double median = MedianOfWindow();
        _ema = _ema.HasValue ? (_alpha * median) + ((1.0 - _alpha) * _ema.Value) : median;
        return _ema.Value;
    }

    public void Reset()
    {
        _count = 0;
        _ema = null;
    }

    private double MedianOfWindow()
    {
        if (_count == 1)
        {
            return _window[0];
        }

        if (_count == 2)
        {
            return (_window[0] + _window[1]) / 2.0;
        }

        double a = _window[0];
        double b = _window[1];
        double c = _window[2];
        double max = Math.Max(a, Math.Max(b, c));
        double min = Math.Min(a, Math.Min(b, c));
        return a + b + c - max - min;
    }
}
