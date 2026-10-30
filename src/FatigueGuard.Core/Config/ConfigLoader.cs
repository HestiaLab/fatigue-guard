using System.Text.Json;

namespace FatigueGuard.Core.Config;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static FatigueConfig Load(string path)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ConfigValidationException(new[] { $"无法读取配置文件 {path}: {ex.Message}" });
        }

        FatigueConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<FatigueConfig>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new ConfigValidationException(new[] { $"配置文件不是合法 JSON: {ex.Message}" });
        }

        if (config is null)
        {
            throw new ConfigValidationException(new[] { "配置文件内容为空。" });
        }

        return config;
    }

    public static void ValidateOrThrow(FatigueConfig config)
    {
        IReadOnlyList<string> errors = Validate(config);
        if (errors.Count > 0)
        {
            throw new ConfigValidationException(errors);
        }
    }

    public static IReadOnlyList<string> Validate(FatigueConfig config)
    {
        var errors = new List<string>();

        if (config.Camera is null)
        {
            errors.Add("camera: 配置节不能为 null。");
        }
        else
        {
            Check(config.Camera.Index < 0, errors, "camera.index: 必须大于等于 0。");
            Check(config.Camera.Width <= 0, errors, "camera.width: 必须大于 0。");
            Check(config.Camera.Height <= 0, errors, "camera.height: 必须大于 0。");
            Check(!IsFinitePositive(config.Camera.Fps), errors, "camera.fps: 必须为大于 0 的有限数值。");
        }

        if (config.Detection is null)
        {
            errors.Add("detection: 配置节不能为 null。");
        }
        else
        {
            Check(
                !(config.Detection.MinFaceConfidence > 0.0 && config.Detection.MinFaceConfidence <= 1.0),
                errors,
                "detection.minFaceConfidence: 必须位于 (0, 1] 区间。");
            Check(config.Detection.MinFaceSize <= 0, errors, "detection.minFaceSize: 必须大于 0。");
            Check(config.Detection.DetectEveryNthFrame < 1, errors, "detection.detectEveryNthFrame: 必须大于等于 1。");
        }

        if (config.Calibration is null)
        {
            errors.Add("calibration: 配置节不能为 null。");
        }
        else
        {
            CalibrationConfig c = config.Calibration;
            Check(c.Seconds < 0.0, errors, "calibration.seconds: 不能为负数。");
            Check(c.MaxSeconds < 0.0, errors, "calibration.maxSeconds: 不能为负数。");
            Check(
                c.Seconds > 0.0 && c.MaxSeconds < c.Seconds,
                errors,
                "calibration.maxSeconds: 不能小于 calibration.seconds。");
            Check(
                !(c.MinValidRatio > 0.0 && c.MinValidRatio <= 1.0),
                errors,
                "calibration.minValidRatio: 必须位于 (0, 1] 区间。");
            Check(
                !(c.BaselinePercentile > 0.0 && c.BaselinePercentile < 100.0),
                errors,
                "calibration.baselinePercentile: 必须位于 (0, 100) 区间。");
            Check(
                c.BaselineOverride is { } b && !(b > 0.0 && b < 0.6),
                errors,
                "calibration.baselineOverride: 必须位于 (0, 0.6) 区间或为 null。");
        }

        if (config.Blink is null)
        {
            errors.Add("blink: 配置节不能为 null。");
        }
        else
        {
            BlinkConfig b = config.Blink;
            Check(!IsFinitePositive(b.ClosedThresholdRatio), errors, "blink.closedThresholdRatio: 必须大于 0。");
            Check(b.ClosedThresholdMin <= 0.0, errors, "blink.closedThresholdMin: 必须大于 0。");
            Check(
                b.ClosedThresholdMax <= b.ClosedThresholdMin,
                errors,
                "blink.closedThresholdMax: 必须大于 blink.closedThresholdMin。");
            Check(b.MinClosedMs < 0.0, errors, "blink.minClosedMs: 不能为负数。");
            Check(b.BlinkMaxMs <= b.MinClosedMs, errors, "blink.blinkMaxMs: 必须大于 blink.minClosedMs。");
            Check(b.MicrosleepMs <= b.BlinkMaxMs, errors, "blink.microsleepMs: 必须大于 blink.blinkMaxMs。");
            Check(!IsFinitePositive(b.MaxGapMs), errors, "blink.maxGapMs: 必须大于 0。");
        }

        if (config.Metrics is null)
        {
            errors.Add("metrics: 配置节不能为 null。");
        }
        else
        {
            MetricsConfig m = config.Metrics;
            Check(!IsFinitePositive(m.WindowSeconds), errors, "metrics.windowSeconds: 必须大于 0。");
            Check(m.MinValidSeconds < 0.0, errors, "metrics.minValidSeconds: 不能为负数。");
            Check(
                m.MinValidSeconds > m.WindowSeconds,
                errors,
                "metrics.minValidSeconds: 不能大于 metrics.windowSeconds。");
        }

        if (config.Fatigue is null)
        {
            errors.Add("fatigue: 配置节不能为 null。");
        }
        else
        {
            FatigueThresholds f = config.Fatigue;
            Check(!IsFinitePositive(f.PerclosWarn), errors, "fatigue.perclosWarn: 必须大于 0。");
            Check(
                f.PerclosCritical < f.PerclosWarn,
                errors,
                "fatigue.perclosCritical: 不能小于 fatigue.perclosWarn（阈值倒挂）。");
            Check(
                f.PerclosExitWarn > f.PerclosWarn,
                errors,
                "fatigue.perclosExitWarn: 不能大于 fatigue.perclosWarn（阈值倒挂）。");
            Check(f.PerclosExitWarn < 0.0, errors, "fatigue.perclosExitWarn: 不能为负数。");
            Check(
                f.PerclosExitCritical > f.PerclosCritical,
                errors,
                "fatigue.perclosExitCritical: 不能大于 fatigue.perclosCritical（阈值倒挂）。");
            Check(f.PerclosExitCritical < 0.0, errors, "fatigue.perclosExitCritical: 不能为负数。");
            Check(!IsFinitePositive(f.BlinkRateWarn), errors, "fatigue.blinkRateWarn: 必须大于 0。");
            Check(
                f.BlinkRateExit > f.BlinkRateWarn,
                errors,
                "fatigue.blinkRateExit: 不能大于 fatigue.blinkRateWarn（阈值倒挂）。");
            Check(f.BlinkRateExit < 0.0, errors, "fatigue.blinkRateExit: 不能为负数。");
            Check(f.LongClosuresWarn < 1, errors, "fatigue.longClosuresWarn: 必须大于等于 1。");
            Check(!IsFinitePositive(f.WarnDwellSeconds), errors, "fatigue.warnDwellSeconds: 必须大于 0。");
            Check(!IsFinitePositive(f.ExitDwellSeconds), errors, "fatigue.exitDwellSeconds: 必须大于 0。");
            Check(!IsFinitePositive(f.CriticalDwellSeconds), errors, "fatigue.criticalDwellSeconds: 必须大于 0。");
            Check(!IsFinitePositive(f.CriticalRepeatSeconds), errors, "fatigue.criticalRepeatSeconds: 必须大于 0。");
            Check(
                !IsFinitePositive(f.MicrosleepLookbackSeconds),
                errors,
                "fatigue.microsleepLookbackSeconds: 必须大于 0。");
        }

        if (config.Display is null)
        {
            errors.Add("display: 配置节不能为 null。");
        }
        else
        {
            DisplayConfig d = config.Display;
            Check(
                !(d.RefreshHz > 0.0 && d.RefreshHz <= 60.0),
                errors,
                "display.refreshHz: 必须位于 (0, 60] 区间。");
            Check(
                d.Language is not ("zh" or "en"),
                errors,
                "display.language: 仅支持 zh 或 en。");
            Check(
                d.Color is not ("auto" or "on" or "off"),
                errors,
                "display.color: 仅支持 auto、on 或 off。");
        }

        return errors;
    }

    private static bool IsFinitePositive(double value) => value > 0.0 && double.IsFinite(value);

    private static void Check(bool condition, List<string> errors, string message)
    {
        if (condition)
        {
            errors.Add(message);
        }
    }
}
