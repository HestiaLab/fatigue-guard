using FatigueGuard.Core;
using FatigueGuard.Core.Detection;
using OpenCvSharp;

namespace FatigueGuard.Cli;

public sealed class DebugFrameWriter
{
    private readonly string _directory;
    private long _lastSecond = -1;

    public DebugFrameWriter(string directory)
    {
        _directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(_directory);
    }

    public void MaybeWrite(FatigueUpdate update, Mat frame, FaceDebugInfo? debug)
    {
        long second = (long)Math.Floor(update.StreamTime.TotalSeconds);
        if (second <= _lastSecond)
        {
            return;
        }

        _lastSecond = second;
        if (debug is not null)
        {
            Cv2.Rectangle(frame, debug.FaceBox, new Scalar(0, 255, 0), 1);
            foreach (Point2f point in debug.Landmarks)
            {
                Cv2.Circle(
                    frame,
                    new Point((int)Math.Round(point.X), (int)Math.Round(point.Y)),
                    1,
                    new Scalar(0, 255, 255),
                    -1);
            }

            DrawEye(frame, debug, EyeLandmarks.RightEye, new Scalar(255, 0, 0));
            DrawEye(frame, debug, EyeLandmarks.LeftEye, new Scalar(0, 0, 255));
        }

        Cv2.ImWrite(Path.Combine(_directory, $"frame_{second:D5}.png"), frame);
    }

    private static void DrawEye(Mat frame, FaceDebugInfo debug, int[] indices, Scalar color)
    {
        for (int i = 0; i < indices.Length; i++)
        {
            Point2f a = debug.Landmarks[indices[i]];
            Point2f b = debug.Landmarks[indices[(i + 1) % indices.Length]];
            Cv2.Line(
                frame,
                new Point((int)Math.Round(a.X), (int)Math.Round(a.Y)),
                new Point((int)Math.Round(b.X), (int)Math.Round(b.Y)),
                color,
                1);
        }
    }
}
