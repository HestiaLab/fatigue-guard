using OpenCvSharp;

namespace FatigueGuard.Core;

public readonly record struct FramePacket(long Index, TimeSpan StreamTime, Mat Bgr);

public interface IFrameSource : IDisposable
{
    double Fps { get; }

    bool TryRead(out FramePacket frame);
}

public sealed record FaceObservation(
    long FrameIndex,
    TimeSpan StreamTime,
    Rect FaceBox,
    int FaceCount,
    double? LeftEar,
    double? RightEar);

public interface IFaceAnalyzer : IDisposable
{
    FaceObservation Analyze(FramePacket frame);
}

public enum FatigueDisplayState
{
    Calibrating,
    Ok,
    Warning,
    Critical,
    NoFace,
    QualityLow,
}

public enum FatigueEventKind
{
    CalibrationStarted,
    CalibrationCompleted,
    CalibrationInsufficient,
    CalibrationSkipped,
    WarningEntered,
    CriticalEntered,
    CriticalReminder,
    WarningDowngraded,
    Recovery,
    NoFace,
    FaceRestored,
    QualityLow,
    QualityRestored,
}

public enum FatigueTrigger
{
    None,
    Perclos,
    BlinkRate,
    LongClosures,
    Microsleep,
}

public sealed record FatigueEvent(
    TimeSpan Time,
    FatigueEventKind Kind,
    FatigueTrigger Trigger = FatigueTrigger.None,
    double? Perclos = null,
    double? Threshold = null,
    double? BlinkRate = null,
    int? LongClosures = null,
    double? MicrosleepSeconds = null,
    double? BaselineEar = null,
    double? ClosedEarThreshold = null);

public sealed record CalibrationInfo(
    double ElapsedSeconds,
    double RequiredSeconds,
    double ValidRatio);

public sealed record FatigueUpdate(
    TimeSpan StreamTime,
    FatigueDisplayState DisplayState,
    IReadOnlyList<FatigueEvent> Events,
    double? Perclos,
    double? BlinkRatePerMinute,
    double? SmoothedEar,
    bool DataSufficient,
    double? BaselineEar,
    double? ClosedThreshold,
    CalibrationInfo? Calibration,
    bool CalibrationInsufficient,
    int FaceCount,
    double? QualityValidRatio,
    int BlinksInWindow,
    int LongClosuresInWindow,
    int MicrosleepsInLookback,
    double ValidSecondsInWindow);

public interface IFatigueAnalyzer
{
    FatigueUpdate Process(FaceObservation? obs);
}
