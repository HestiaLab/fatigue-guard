using System.Diagnostics;
using OpenCvSharp;

namespace FatigueGuard.Core.Sources;

public sealed class CameraFrameSource : IFrameSource
{
    private readonly VideoCapture _capture;
    private readonly Mat _frame = new();
    private readonly Stopwatch _clock = new();
    private long _index;
    private bool _clockStarted;
    private bool _disposed;

    public CameraFrameSource(int index, int width, int height, double fps)
    {
        Fps = fps;
        _capture = Open(index);
        _capture.Set(VideoCaptureProperties.FrameWidth, width);
        _capture.Set(VideoCaptureProperties.FrameHeight, height);
        _capture.Set(VideoCaptureProperties.Fps, fps);
        _capture.Set(VideoCaptureProperties.BufferSize, 1);
        OpenedWidth = (int)_capture.Get(VideoCaptureProperties.FrameWidth);
        OpenedHeight = (int)_capture.Get(VideoCaptureProperties.FrameHeight);
    }

    public double Fps { get; }

    public int OpenedWidth { get; }

    public int OpenedHeight { get; }

    public bool TryRead(out FramePacket frame)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (_capture.Read(_frame) && !_frame.Empty())
            {
                if (!_clockStarted)
                {
                    _clockStarted = true;
                    _clock.Start();
                }

                frame = new FramePacket(_index, _clock.Elapsed, _frame);
                _index++;
                return true;
            }

            Thread.Sleep(30);
        }

        frame = default;
        return false;
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

    private static VideoCapture Open(int index)
    {
        foreach (VideoCaptureAPIs api in new[] { VideoCaptureAPIs.MSMF, VideoCaptureAPIs.DSHOW })
        {
            VideoCapture? capture = null;
            try
            {
                capture = new VideoCapture(index, api);
                if (capture.IsOpened())
                {
                    return capture;
                }
            }
            catch (Exception)
            {
                // 尝试下一个后端。
            }

            capture?.Dispose();
        }

        throw new FrameSourceException(
            FrameSourceKind.Camera,
            "无法打开摄像头。" + Environment.NewLine +
            "排查建议：" + Environment.NewLine +
            "  1. Windows 设置 > 隐私和安全性 > 相机：允许应用访问相机，并允许桌面应用访问相机；" + Environment.NewLine +
            "  2. 关闭可能占用摄像头的程序（Zoom、Teams、浏览器、相机应用等）；" + Environment.NewLine +
            "  3. 确认设备管理器中摄像头驱动正常，或用 --camera 指定其他索引。");
    }
}
