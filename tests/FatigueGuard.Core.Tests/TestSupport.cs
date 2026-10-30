using FatigueGuard.Core;
using FatigueGuard.Core.Config;
using OpenCvSharp;

namespace FatigueGuard.Core.Tests;

internal static class TestSupport
{
    public const double Fps = 30.0;

    public static FaceObservation Frame(long index, double? ear, int faceCount = 1)
    {
        TimeSpan time = TimeSpan.FromSeconds(index / Fps);
        return new FaceObservation(index, time, new Rect(100, 100, 200, 200), faceCount, ear, ear);
    }

    public static FaceObservation FrameAt(double seconds, double? ear, int faceCount = 1)
    {
        long index = (long)Math.Round(seconds * Fps);
        return Frame(index, ear, faceCount);
    }

    public static FatigueConfig BaseConfig()
    {
        var config = new FatigueConfig();
        config.Calibration.Seconds = 0;
        return config;
    }

    public static string RepoRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FatigueGuard.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("找不到仓库根目录（FatigueGuard.sln）。");
    }

    public static string ModelPath(string fileName) => Path.Combine(RepoRoot(), "models", fileName);
}
