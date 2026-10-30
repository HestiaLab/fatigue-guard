using OpenCvSharp;

namespace FatigueGuard.Core.Sources;

public sealed class VideoFrameSource : IFrameSource
{
    private readonly VideoCapture _capture = new();
    private readonly Mat _frame = new();
    private long _index;
    private bool _disposed;

    public VideoFrameSource(string path)
    {
        if (!File.Exists(path))
        {
            throw new FrameSourceException(FrameSourceKind.Video, $"找不到视频文件：{path}");
        }

        _capture.Open(path);
        if (!_capture.IsOpened())
        {
            throw new FrameSourceException(FrameSourceKind.Video, $"无法打开视频文件：{path}");
        }

        double fps = _capture.Fps;
        Fps = fps > 1.0 && fps < 240.0 ? fps : 30.0;
    }

    public double Fps { get; }

    public bool TryRead(out FramePacket frame)
    {
        if (!_capture.Read(_frame) || _frame.Empty())
        {
            frame = default;
            return false;
        }

        frame = new FramePacket(_index, TimeSpan.FromSeconds(_index / Fps), _frame);
        _index++;
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _frame.Dispose();
        _capture.Release();
        _capture.Dispose();
    }
}
