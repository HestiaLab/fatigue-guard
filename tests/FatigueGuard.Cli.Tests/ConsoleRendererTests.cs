using FatigueGuard.Cli;
using FatigueGuard.Core;
using OpenCvSharp;

namespace FatigueGuard.Cli.Tests;

public sealed class ConsoleRendererTests
{
    private static FatigueUpdate Update(
        TimeSpan time,
        FatigueDisplayState state,
        IReadOnlyList<FatigueEvent>? events = null,
        bool dataSufficient = true)
    {
        return new FatigueUpdate(
            time,
            state,
            events ?? Array.Empty<FatigueEvent>(),
            Perclos: 0.17,
            BlinkRatePerMinute: 31.5,
            SmoothedEar: 0.27,
            DataSufficient: dataSufficient,
            BaselineEar: 0.30,
            ClosedThreshold: 0.16,
            Calibration: null,
            CalibrationInsufficient: false,
            FaceCount: 1,
            QualityValidRatio: 1.0,
            BlinksInWindow: 10,
            LongClosuresInWindow: 0,
            MicrosleepsInLookback: 0,
            ValidSecondsInWindow: 45.0);
    }

    private static ConsoleRenderer CreateRenderer(
        StringWriter writer,
        bool interactive = true,
        bool quiet = false,
        bool colorEnabled = true,
        bool ansi = true)
    {
        return new ConsoleRenderer(
            new Messages("zh"),
            interactive,
            quiet,
            colorEnabled,
            ansi,
            5.0,
            writer);
    }

    [Fact]
    public void InteractiveStatus_UsesCarriageReturnAndColor()
    {
        var writer = new StringWriter();
        ConsoleRenderer renderer = CreateRenderer(writer);

        renderer.Render(Update(TimeSpan.FromSeconds(42.6), FatigueDisplayState.Warning), 29.9);

        string output = writer.ToString();
        Assert.Contains('\r', output);
        Assert.Contains("\x1b[33m", output);
        Assert.Contains("疲劳预警", output);
        Assert.Contains("PERCLOS 0.17", output);
        Assert.Contains("眨眼 32/min", output);
        Assert.Contains("EAR 0.27", output);
        Assert.Contains("人脸 1", output);
        Assert.Contains("29.9fps", output);
    }

    [Fact]
    public void CriticalEvent_IsBoldRedWithTimestamp()
    {
        var writer = new StringWriter();
        ConsoleRenderer renderer = CreateRenderer(writer);
        var critical = new FatigueEvent(
            TimeSpan.FromSeconds(63.2),
            FatigueEventKind.CriticalEntered,
            FatigueTrigger.Microsleep,
            MicrosleepSeconds: 1.4);

        renderer.Render(Update(TimeSpan.FromSeconds(63.2), FatigueDisplayState.Critical, new[] { critical }), 30.0);

        string output = writer.ToString();
        Assert.Contains("\x1b[1;31m01:03.2  严重疲劳：检测到微睡眠 1.4 秒\x1b[0m", output);
    }

    [Fact]
    public void RecoveryEvent_IsGreen()
    {
        var writer = new StringWriter();
        ConsoleRenderer renderer = CreateRenderer(writer);
        var recovery = new FatigueEvent(TimeSpan.FromSeconds(90.8), FatigueEventKind.Recovery);

        renderer.Render(Update(TimeSpan.FromSeconds(90.8), FatigueDisplayState.Ok, new[] { recovery }), 30.0);

        Assert.Contains("\x1b[32m01:30.8  恢复：指标已回落至正常范围\x1b[0m", writer.ToString());
    }

    [Fact]
    public void RedirectedOutput_HasNoAnsiAndNoCarriageReturn()
    {
        var writer = new StringWriter();
        ConsoleRenderer renderer = CreateRenderer(writer, interactive: false, colorEnabled: false, ansi: false);
        var noFace = new FatigueEvent(TimeSpan.FromSeconds(5.0), FatigueEventKind.NoFace);

        renderer.Render(Update(TimeSpan.FromSeconds(5.0), FatigueDisplayState.NoFace, new[] { noFace }), 30.0);
        renderer.Finish();

        string output = writer.ToString();
        Assert.Equal("00:05.0  未检测到人脸\n", output);
        Assert.DoesNotContain("\x1b[", output);
        Assert.DoesNotContain('\r', output);
    }

    [Fact]
    public void QuietMode_SuppressesStatusLine()
    {
        var writer = new StringWriter();
        ConsoleRenderer renderer = CreateRenderer(writer, quiet: true);

        renderer.Render(Update(TimeSpan.FromSeconds(1.0), FatigueDisplayState.Ok), 30.0);

        string output = writer.ToString();
        Assert.Equal(string.Empty, output);
    }

    [Fact]
    public void ColorDisabled_EmitsPlainText()
    {
        var writer = new StringWriter();
        ConsoleRenderer renderer = CreateRenderer(writer, colorEnabled: false, ansi: false);
        var warning = new FatigueEvent(
            TimeSpan.FromSeconds(51.2),
            FatigueEventKind.WarningEntered,
            FatigueTrigger.Perclos,
            Perclos: 0.17,
            Threshold: 0.15);

        renderer.Render(Update(TimeSpan.FromSeconds(51.2), FatigueDisplayState.Warning, new[] { warning }), 30.0);

        string output = writer.ToString();
        Assert.Contains("00:51.2  疲劳预警：PERCLOS 0.17（阈值 0.15，60 秒窗口）", output);
        Assert.DoesNotContain("\x1b[", output);
    }

    [Fact]
    public void InsufficientData_IsMarkedInStatusLine()
    {
        var writer = new StringWriter();
        ConsoleRenderer renderer = CreateRenderer(writer);

        renderer.Render(Update(TimeSpan.FromSeconds(5.0), FatigueDisplayState.Ok, dataSufficient: false), 30.0);

        Assert.Contains("PERCLOS --", writer.ToString());
        Assert.Contains("数据不足", writer.ToString());
    }

    [Fact]
    public void PreviewMode_DrawsHalfBlocksAndCompactStatus()
    {
        using var preview = new TerminalPreview(40, 8);
        using var frame = new Mat(new Size(80, 60), MatType.CV_8UC3, new Scalar(10, 20, 30));
        var writer = new StringWriter();
        var renderer = new ConsoleRenderer(
            new Messages("zh"),
            interactive: true,
            quiet: false,
            colorEnabled: true,
            ansi: true,
            refreshHz: 5.0,
            writer,
            preview);

        renderer.Render(Update(TimeSpan.FromSeconds(1.0), FatigueDisplayState.Ok), 30.0, frame, null);
        string output = writer.ToString();

        Assert.Contains("\u2580", output);
        Assert.Contains("P 0.17", output);
        Assert.Contains("B 32/min", output);
        Assert.DoesNotContain("PERCLOS 0.17", output);

        renderer.Finish();
        Assert.Contains("\x1b[0m", writer.ToString());
    }

    [Fact]
    public void CalibrationExtension_IsMarkedInStatus()
    {
        var writer = new StringWriter();
        ConsoleRenderer renderer = CreateRenderer(writer, colorEnabled: false, ansi: false);
        FatigueUpdate update = Update(TimeSpan.FromSeconds(38.0), FatigueDisplayState.Calibrating) with
        {
            Calibration = new CalibrationInfo(38.0, 20.0, 0.5),
        };

        renderer.Render(update, 17.9);

        Assert.Contains("38/20s（延长）", writer.ToString());
    }

    [Fact]
    public void NoFaceStatus_IsGray()
    {
        var writer = new StringWriter();
        ConsoleRenderer renderer = CreateRenderer(writer);

        renderer.Render(Update(TimeSpan.FromSeconds(3.0), FatigueDisplayState.NoFace), 30.0);

        Assert.Contains("\x1b[90m", writer.ToString());
    }
}
