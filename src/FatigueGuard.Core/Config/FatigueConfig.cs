namespace FatigueGuard.Core.Config;

public sealed class FatigueConfig
{
    public CameraConfig Camera { get; set; } = new();

    public DetectionConfig Detection { get; set; } = new();

    public CalibrationConfig Calibration { get; set; } = new();

    public BlinkConfig Blink { get; set; } = new();

    public MetricsConfig Metrics { get; set; } = new();

    public FatigueThresholds Fatigue { get; set; } = new();

    public DisplayConfig Display { get; set; } = new();
}

public sealed class CameraConfig
{
    public int Index { get; set; }

    public int Width { get; set; } = 640;

    public int Height { get; set; } = 480;

    public double Fps { get; set; } = 30;
}

public sealed class DetectionConfig
{
    public double MinFaceConfidence { get; set; } = 0.6;

    public int MinFaceSize { get; set; } = 80;

    public int DetectEveryNthFrame { get; set; } = 1;
}

public sealed class CalibrationConfig
{
    public double Seconds { get; set; } = 20;

    public double MaxSeconds { get; set; } = 60;

    public double MinValidRatio { get; set; } = 0.7;

    public double BaselinePercentile { get; set; } = 75;

    public double? BaselineOverride { get; set; }
}

public sealed class BlinkConfig
{
    public double ClosedThresholdRatio { get; set; } = 0.60;

    public double ClosedThresholdMin { get; set; } = 0.08;

    public double ClosedThresholdMax { get; set; } = 0.16;

    public double MinClosedMs { get; set; } = 50;

    public double BlinkMaxMs { get; set; } = 500;

    public double MicrosleepMs { get; set; } = 1200;

    public double MaxGapMs { get; set; } = 500;
}

public sealed class MetricsConfig
{
    public double WindowSeconds { get; set; } = 60;

    public double MinValidSeconds { get; set; } = 30;
}

public sealed class FatigueThresholds
{
    public double PerclosWarn { get; set; } = 0.15;

    public double PerclosCritical { get; set; } = 0.25;

    public double PerclosExitWarn { get; set; } = 0.10;

    public double PerclosExitCritical { get; set; } = 0.15;

    public double BlinkRateWarn { get; set; } = 30;

    public double BlinkRateExit { get; set; } = 24;

    public int LongClosuresWarn { get; set; } = 3;

    public double WarnDwellSeconds { get; set; } = 5;

    public double ExitDwellSeconds { get; set; } = 15;

    public double CriticalDwellSeconds { get; set; } = 5;

    public double CriticalRepeatSeconds { get; set; } = 15;

    public double MicrosleepLookbackSeconds { get; set; } = 60;
}

public sealed class DisplayConfig
{
    public double RefreshHz { get; set; } = 5;

    public string Language { get; set; } = "zh";

    public string Color { get; set; } = "auto";
}
