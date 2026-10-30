using System.Text;
using FatigueGuard.Core.Detection;
using OpenCvSharp;

namespace FatigueGuard.Cli;

public sealed class TerminalPreview : IDisposable
{
    private const string HalfBlock = "\u2580";

    private readonly int _maxColumns;
    private readonly int _maxRows;
    private readonly bool _mirror;
    private readonly Mat _scaled = new();
    private int _columns;
    private int _pixelRows;
    private bool _disposed;

    public TerminalPreview(int maxColumns, int maxRows, bool mirror = true)
    {
        _maxColumns = Math.Clamp(maxColumns, 16, 120);
        _maxRows = Math.Clamp(maxRows, 4, 32);
        _mirror = mirror;
    }

    public int Columns => _columns;

    public int LineCount => _pixelRows / 2;

    public IReadOnlyList<string> Render(Mat frame, FaceDebugInfo? debug)
    {
        PrepareLayout(frame.Width, frame.Height);
        Cv2.Resize(frame, _scaled, new Size(_columns, _pixelRows), 0, 0, InterpolationFlags.Area);

        if (debug is not null)
        {
            DrawOverlay(debug, frame.Width, frame.Height);
        }

        if (_mirror)
        {
            Cv2.Flip(_scaled, _scaled, FlipMode.Y);
        }

        var lines = new string[LineCount];
        for (int row = 0; row < lines.Length; row++)
        {
            var builder = new StringBuilder((_columns * 8) + 32);
            int previousForeground = -1;
            int previousBackground = -1;
            for (int column = 0; column < _columns; column++)
            {
                Vec3b top = _scaled.At<Vec3b>(row * 2, column);
                Vec3b bottom = _scaled.At<Vec3b>((row * 2) + 1, column);
                int foreground = Pack(top);
                int background = Pack(bottom);
                if (foreground != previousForeground)
                {
                    AppendColor(builder, foreground: true, top);
                    previousForeground = foreground;
                }

                if (background != previousBackground)
                {
                    AppendColor(builder, foreground: false, bottom);
                    previousBackground = background;
                }

                builder.Append(HalfBlock);
            }

            builder.Append("\x1b[0m");
            lines[row] = builder.ToString();
        }

        return lines;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _scaled.Dispose();
    }

    private void PrepareLayout(int frameWidth, int frameHeight)
    {
        if (_columns > 0)
        {
            return;
        }

        if (frameWidth <= 0 || frameHeight <= 0)
        {
            throw new ArgumentException("预览帧尺寸必须大于 0。");
        }

        double aspect = (double)frameWidth / frameHeight;
        int columns = _maxColumns;
        int rows = (int)Math.Round(columns / aspect / 2.0);
        if (rows > _maxRows)
        {
            rows = _maxRows;
            columns = (int)Math.Round(rows * 2.0 * aspect);
        }

        rows = Math.Clamp(rows, 4, _maxRows);
        _columns = Math.Clamp(columns, 16, _maxColumns);
        _pixelRows = rows * 2;
    }

    private void DrawOverlay(FaceDebugInfo debug, int frameWidth, int frameHeight)
    {
        double scaleX = (double)_columns / frameWidth;
        double scaleY = (double)_pixelRows / frameHeight;

        Rect box = ScaleRect(debug.FaceBox, scaleX, scaleY);
        Cv2.Rectangle(_scaled, box, new Scalar(0, 255, 0), 1);
        DrawEye(debug, EyeLandmarks.RightEye, scaleX, scaleY, new Scalar(255, 0, 0));
        DrawEye(debug, EyeLandmarks.LeftEye, scaleX, scaleY, new Scalar(0, 0, 255));
    }

    private void DrawEye(FaceDebugInfo debug, int[] indices, double scaleX, double scaleY, Scalar color)
    {
        for (int i = 0; i < indices.Length; i++)
        {
            Point2f a = debug.Landmarks[indices[i]];
            Point2f b = debug.Landmarks[indices[(i + 1) % indices.Length]];
            Cv2.Line(_scaled, ScalePoint(a, scaleX, scaleY), ScalePoint(b, scaleX, scaleY), color, 1);
        }
    }

    private static Rect ScaleRect(Rect rect, double scaleX, double scaleY) => new(
        (int)Math.Round(rect.X * scaleX),
        (int)Math.Round(rect.Y * scaleY),
        Math.Max(1, (int)Math.Round(rect.Width * scaleX)),
        Math.Max(1, (int)Math.Round(rect.Height * scaleY)));

    private static Point ScalePoint(Point2f point, double scaleX, double scaleY) => new(
        (int)Math.Round(point.X * scaleX),
        (int)Math.Round(point.Y * scaleY));

    private static int Pack(Vec3b pixel) => (pixel.Item2 << 16) | (pixel.Item1 << 8) | pixel.Item0;

    private static void AppendColor(StringBuilder builder, bool foreground, Vec3b pixel)
    {
        builder.Append("\x1b[");
        builder.Append(foreground ? "38" : "48");
        builder.Append(";2;");
        builder.Append(pixel.Item2).Append(';');
        builder.Append(pixel.Item1).Append(';');
        builder.Append(pixel.Item0).Append('m');
    }
}
