using System.Globalization;
using FatigueGuard.Core;

namespace FatigueGuard.Cli;

public sealed class Messages
{
    private readonly bool _zh;

    public Messages(string language)
    {
        _zh = language != "en";
    }

    public static string FormatTime(TimeSpan time)
    {
        long tenths = (long)Math.Floor(time.TotalSeconds * 10.0);
        long minutes = tenths / 600;
        long remainder = tenths % 600;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{minutes:D2}:{remainder / 10:D2}.{remainder % 10}");
    }

    public static int DisplayWidth(string text)
    {
        int width = 0;
        foreach (char c in text)
        {
            width += c >= 0x2E80 ? 2 : 1;
        }

        return width;
    }

    public string StatusLine(FatigueUpdate update, double fps)
    {
        string stateText = StateText(update, compact: false);
        string perclos = update.DataSufficient && update.Perclos.HasValue
            ? Number(update.Perclos, 2)
            : "--";
        string blink = update.DataSufficient && update.BlinkRatePerMinute.HasValue
            ? Number(update.BlinkRatePerMinute.Value, 0) + "/min"
            : "--/min";
        string ear = update.SmoothedEar.HasValue ? Number(update.SmoothedEar, 2) : "--";
        string blinkLabel = _zh ? $"眨眼 {blink}" : $"Blink {blink}";
        string faceLabel = _zh ? $"人脸 {update.FaceCount}" : $"Faces {update.FaceCount}";
        string suffix = update.CalibrationInsufficient
            ? _zh ? "  （校准数据不足）" : "  (insufficient calibration data)"
            : string.Empty;
        string dataInsufficient = !update.DataSufficient && update.DisplayState != FatigueDisplayState.Calibrating
            ? _zh ? "  数据不足" : "  insufficient data"
            : string.Empty;

        int stateWidth = Math.Max(12, DisplayWidth(stateText));
        int padding = stateWidth - DisplayWidth(stateText);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{FormatTime(update.StreamTime)}  {stateText}{new string(' ', padding)}  PERCLOS {perclos}  {blinkLabel}  EAR {ear}  {faceLabel}  {Number(fps, 1)}fps{dataInsufficient}{suffix}");
    }

    public string CompactStatusLine(FatigueUpdate update, double fps)
    {
        string stateText = StateText(update, compact: true);
        string perclos = update.DataSufficient && update.Perclos.HasValue
            ? Number(update.Perclos, 2)
            : "--";
        string blink = update.DataSufficient && update.BlinkRatePerMinute.HasValue
            ? Number(update.BlinkRatePerMinute.Value, 0) + "/min"
            : "--/min";
        string ear = update.SmoothedEar.HasValue ? Number(update.SmoothedEar, 2) : "--";
        string dataInsufficient = !update.DataSufficient && update.DisplayState != FatigueDisplayState.Calibrating
            ? _zh ? "  数据不足" : "  insufficient data"
            : string.Empty;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{FormatTime(update.StreamTime)}  {stateText}  P {perclos}  B {blink}  EAR {ear}  F {update.FaceCount}  {Number(fps, 1)}fps{dataInsufficient}");
    }

    public string EventLine(FatigueEvent fatigueEvent)
    {
        string body = fatigueEvent.Kind switch
        {
            FatigueEventKind.CalibrationStarted => _zh
                ? "开始校准，请保持自然睁眼并面向摄像头"
                : "Calibration started; keep your eyes open and face the camera",
            FatigueEventKind.CalibrationCompleted => _zh
                ? $"校准完成：基线 EAR {Number(fatigueEvent.BaselineEar, 2)}，闭眼阈值 {Number(fatigueEvent.ClosedEarThreshold, 2)}"
                : $"Calibration complete: baseline EAR {Number(fatigueEvent.BaselineEar, 2)}, closed threshold {Number(fatigueEvent.ClosedEarThreshold, 2)}",
            FatigueEventKind.CalibrationInsufficient => _zh
                ? $"校准数据不足，使用保守默认值：基线 EAR {Number(fatigueEvent.BaselineEar, 2)}，闭眼阈值 {Number(fatigueEvent.ClosedEarThreshold, 2)}"
                : $"Insufficient calibration data; using conservative defaults: baseline EAR {Number(fatigueEvent.BaselineEar, 2)}, closed threshold {Number(fatigueEvent.ClosedEarThreshold, 2)}",
            FatigueEventKind.CalibrationSkipped => _zh
                ? $"已跳过校准，使用基线 EAR {Number(fatigueEvent.BaselineEar, 2)}（闭眼阈值 {Number(fatigueEvent.ClosedEarThreshold, 2)}）"
                : $"Calibration skipped; baseline EAR {Number(fatigueEvent.BaselineEar, 2)} (closed threshold {Number(fatigueEvent.ClosedEarThreshold, 2)})",
            FatigueEventKind.WarningEntered => WarningText(fatigueEvent),
            FatigueEventKind.CriticalEntered => CriticalText(fatigueEvent),
            FatigueEventKind.CriticalReminder => _zh
                ? $"严重疲劳：请立即休息（PERCLOS {Number(fatigueEvent.Perclos, 2)}）"
                : $"Severe fatigue: take a break now (PERCLOS {Number(fatigueEvent.Perclos, 2)})",
            FatigueEventKind.WarningDowngraded => _zh
                ? $"疲劳缓解：仍处于预警状态（PERCLOS {Number(fatigueEvent.Perclos, 2)}）"
                : $"Fatigue easing: still in warning range (PERCLOS {Number(fatigueEvent.Perclos, 2)})",
            FatigueEventKind.Recovery => _zh
                ? "恢复：指标已回落至正常范围"
                : "Recovered: metrics are back to normal",
            FatigueEventKind.NoFace => _zh ? "未检测到人脸" : "No face detected",
            FatigueEventKind.FaceRestored => _zh ? "已恢复人脸检测" : "Face detected again",
            FatigueEventKind.QualityLow => _zh ? "画面质量低" : "Low image quality",
            FatigueEventKind.QualityRestored => _zh ? "画面质量已恢复" : "Image quality recovered",
            _ => string.Empty,
        };

        return string.Create(CultureInfo.InvariantCulture, $"{FormatTime(fatigueEvent.Time)}  {body}");
    }

    public string StateLabel(FatigueDisplayState state) => state switch
    {
        FatigueDisplayState.Calibrating => _zh ? "校准中" : "CALIBRATING",
        FatigueDisplayState.Ok => _zh ? "正常" : "OK",
        FatigueDisplayState.Warning => _zh ? "疲劳预警" : "WARNING",
        FatigueDisplayState.Critical => _zh ? "严重疲劳" : "CRITICAL",
        FatigueDisplayState.NoFace => _zh ? "未检测到人脸" : "NO FACE",
        FatigueDisplayState.QualityLow => _zh ? "画面质量低" : "QUALITY LOW",
        _ => string.Empty,
    };

    private string StateText(FatigueUpdate update, bool compact)
    {
        if (update.DisplayState != FatigueDisplayState.Calibrating || update.Calibration is not { } calibration)
        {
            return StateLabel(update.DisplayState);
        }

        string text = _zh
            ? $"校准中 {calibration.ElapsedSeconds:0}/{calibration.RequiredSeconds:0}s"
            : $"CALIBRATING {calibration.ElapsedSeconds:0}/{calibration.RequiredSeconds:0}s";
        if (calibration.ElapsedSeconds > calibration.RequiredSeconds + 0.05)
        {
            text += compact ? "*" : _zh ? "（延长）" : " (extended)";
        }

        return text;
    }

    private string WarningText(FatigueEvent e)
    {
        return e.Trigger switch
        {
            FatigueTrigger.BlinkRate => _zh
                ? $"疲劳预警：眨眼率 {Number(e.BlinkRate, 0)}/min（阈值 {Number(e.Threshold, 0)}/min）"
                : $"Fatigue warning: blink rate {Number(e.BlinkRate, 0)}/min (threshold {Number(e.Threshold, 0)}/min)",
            FatigueTrigger.LongClosures => _zh
                ? $"疲劳预警：60 秒内长闭眼 {e.LongClosures} 次（阈值 {Number(e.Threshold, 0)} 次）"
                : $"Fatigue warning: {e.LongClosures} long closures in 60 s (threshold {Number(e.Threshold, 0)})",
            _ => _zh
                ? $"疲劳预警：PERCLOS {Number(e.Perclos, 2)}（阈值 {Number(e.Threshold, 2)}，60 秒窗口）"
                : $"Fatigue warning: PERCLOS {Number(e.Perclos, 2)} (threshold {Number(e.Threshold, 2)}, 60 s window)",
        };
    }

    private string CriticalText(FatigueEvent e)
    {
        if (e.Trigger == FatigueTrigger.Microsleep)
        {
            if (e.MicrosleepSeconds is { } seconds)
            {
                return _zh
                    ? $"严重疲劳：检测到微睡眠 {Number(seconds, 1)} 秒"
                    : $"Severe fatigue: microsleep detected ({Number(seconds, 1)} s)";
            }

            return _zh ? "严重疲劳：检测到微睡眠" : "Severe fatigue: microsleep detected";
        }

        return _zh
            ? $"严重疲劳：PERCLOS {Number(e.Perclos, 2)}（阈值 {Number(e.Threshold, 2)}，60 秒窗口）"
            : $"Severe fatigue: PERCLOS {Number(e.Perclos, 2)} (threshold {Number(e.Threshold, 2)}, 60 s window)";
    }

    private static string Number(double? value, int digits)
        => value.HasValue ? value.Value.ToString("F" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) : "--";

    private static string Number(double value, int digits)
        => value.ToString("F" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
}
