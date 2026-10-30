using FatigueGuard.Cli;
using FatigueGuard.Core.Detection;
using OpenCvSharp;

namespace FatigueGuard.Cli.Tests;

public sealed class TerminalPreviewTests
{
    [Fact]
    public void Layout_16By9_UsesWidePreview()
    {
        using var preview = new TerminalPreview(56, 16);
        using var frame = new Mat(new Size(1280, 720), MatType.CV_8UC3, Scalar.All(0));

        preview.Render(frame, null);

        Assert.Equal(56, preview.Columns);
        Assert.Equal(16, preview.LineCount);
    }

    [Fact]
    public void Layout_4By3_FitsHeightBudget()
    {
        using var preview = new TerminalPreview(56, 16);
        using var frame = new Mat(new Size(640, 480), MatType.CV_8UC3, Scalar.All(0));

        preview.Render(frame, null);

        Assert.Equal(43, preview.Columns);
        Assert.Equal(16, preview.LineCount);
    }

    [Fact]
    public void Render_EmitsTrueColorHalfBlocks()
    {
        using var preview = new TerminalPreview(32, 8);
        using var frame = new Mat(new Size(64, 48), MatType.CV_8UC3, new Scalar(10, 20, 30));

        IReadOnlyList<string> lines = preview.Render(frame, null);
        string joined = string.Join("\n", lines);

        Assert.Equal(preview.LineCount, lines.Count);
        Assert.Contains("\u2580", joined);
        Assert.Contains("\x1b[38;2;30;20;10m", joined);
        Assert.Contains("\x1b[48;2;30;20;10m", joined);
        Assert.EndsWith("\x1b[0m", lines[0]);
    }

    [Fact]
    public void Render_MirrorsPreviewByDefault()
    {
        using var frame = new Mat(new Size(64, 48), MatType.CV_8UC3, new Scalar(0, 0, 255));
        Cv2.Rectangle(frame, new Rect(32, 0, 32, 48), new Scalar(255, 0, 0), -1);
        using var mirrored = new TerminalPreview(32, 8, mirror: true);
        using var original = new TerminalPreview(32, 8, mirror: false);

        string mirroredFirst = mirrored.Render(frame, null)[0];
        string originalFirst = original.Render(frame, null)[0];

        Assert.StartsWith("\x1b[38;2;0;0;255m", mirroredFirst);
        Assert.StartsWith("\x1b[38;2;255;0;0m", originalFirst);
    }

    [Fact]
    public void Render_WithDebugInfo_DrawsOverlay()
    {
        using var preview = new TerminalPreview(64, 16);
        using var frame = new Mat(new Size(128, 96), MatType.CV_8UC3, new Scalar(10, 10, 10));
        var landmarks = new Point2f[468];
        for (int i = 0; i < landmarks.Length; i++)
        {
            landmarks[i] = new Point2f(60, 40);
        }

        var debug = new FaceDebugInfo(new Rect(20, 10, 80, 60), landmarks);

        string without = string.Join("\n", preview.Render(frame, null));
        string with = string.Join("\n", preview.Render(frame, debug));

        Assert.NotEqual(without, with);
    }
}
