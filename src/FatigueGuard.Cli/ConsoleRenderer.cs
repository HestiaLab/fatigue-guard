using System.Runtime.InteropServices;
using FatigueGuard.Core;
using FatigueGuard.Core.Detection;
using OpenCvSharp;

namespace FatigueGuard.Cli;

internal enum TerminalColor
{
    Default,
    Gray,
    Yellow,
    BoldRed,
    Green,
}

internal static class WindowsConsole
{
    private const int StdOutputHandle = -11;

    private const uint EnableVirtualTerminalProcessing = 0x0004;

    public static bool TryEnableVirtualTerminal()
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        try
        {
            IntPtr handle = GetStdHandle(StdOutputHandle);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            {
                return false;
            }

            if (!GetConsoleMode(handle, out uint mode))
            {
                return false;
            }

            return SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
        }
        catch (Exception)
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
}

public sealed class ConsoleRenderer
{
    private readonly TextWriter _writer;
    private readonly Messages _messages;
    private readonly bool _interactive;
    private readonly bool _quiet;
    private readonly bool _colorEnabled;
    private readonly bool _ansi;
    private readonly TimeSpan _refreshInterval;
    private readonly TerminalPreview? _preview;

    private TimeSpan? _lastStatusTime;
    private int _statusWidth;
    private bool _statusVisible;
    private bool _blockVisible;
    private int _previewRows;

    public ConsoleRenderer(
        Messages messages,
        bool interactive,
        bool quiet,
        bool colorEnabled,
        bool ansi,
        double refreshHz,
        TextWriter? writer = null,
        TerminalPreview? preview = null)
    {
        _writer = writer ?? Console.Out;
        _messages = messages;
        _interactive = interactive;
        _quiet = quiet;
        _colorEnabled = colorEnabled;
        _ansi = ansi;
        _refreshInterval = TimeSpan.FromSeconds(1.0 / refreshHz);
        _preview = preview;
    }

    public void Render(FatigueUpdate update, double fps, Mat? frame = null, FaceDebugInfo? debug = null)
    {
        if (_preview is null)
        {
            RenderPlain(update, fps);
            return;
        }

        if (!_interactive || _quiet)
        {
            foreach (FatigueEvent fatigueEvent in update.Events)
            {
                WriteEventLine(fatigueEvent);
            }

            return;
        }

        bool hadEvents = update.Events.Count > 0;
        if (hadEvents)
        {
            EraseBlock();
            foreach (FatigueEvent fatigueEvent in update.Events)
            {
                WriteEventLine(fatigueEvent);
            }

            _lastStatusTime = null;
        }

        if (frame is null)
        {
            return;
        }

        if (!hadEvents && _lastStatusTime is { } last && update.StreamTime - last < _refreshInterval)
        {
            return;
        }

        _lastStatusTime = update.StreamTime;
        DrawBlock(update, fps, frame, debug);
    }

    public void Finish()
    {
        if (_preview is not null)
        {
            if (_blockVisible)
            {
                EraseBlock();
                _writer.Write('\n');
            }
        }
        else if (_statusVisible)
        {
            EraseStatus();
            _writer.Write('\n');
            _statusVisible = false;
        }

        if (_colorEnabled && _ansi)
        {
            _writer.Write("\x1b[0m");
        }

        _writer.Flush();
    }

    private void RenderPlain(FatigueUpdate update, double fps)
    {
        foreach (FatigueEvent fatigueEvent in update.Events)
        {
            WriteEvent(fatigueEvent);
        }

        if (!_interactive || _quiet)
        {
            return;
        }

        if (_lastStatusTime is { } last && update.StreamTime - last < _refreshInterval)
        {
            return;
        }

        _lastStatusTime = update.StreamTime;
        WriteStatus(update, fps);
    }

    private void WriteEvent(FatigueEvent fatigueEvent)
    {
        if (_interactive && !_quiet)
        {
            EraseStatus();
        }

        WriteEventLine(fatigueEvent);
        _lastStatusTime = null;
    }

    private void WriteEventLine(FatigueEvent fatigueEvent)
    {
        string line = _messages.EventLine(fatigueEvent);
        WriteColored(line, EventColor(fatigueEvent.Kind));
        _writer.Write('\n');
        _writer.Flush();
    }

    private void DrawBlock(FatigueUpdate update, double fps, Mat frame, FaceDebugInfo? debug)
    {
        EraseBlock();
        IReadOnlyList<string> lines = _preview!.Render(frame, debug);
        foreach (string line in lines)
        {
            _writer.Write(line);
            _writer.Write("\r\n");
        }

        WriteColored(_messages.CompactStatusLine(update, fps), StatusColor(update.DisplayState));
        _previewRows = lines.Count;
        _blockVisible = true;
        _statusVisible = false;
        _writer.Flush();
    }

    private void EraseBlock()
    {
        if (!_blockVisible)
        {
            return;
        }

        _writer.Write('\r');
        if (_previewRows > 0)
        {
            _writer.Write($"\x1b[{_previewRows}A");
        }

        _writer.Write("\x1b[0J");
        _blockVisible = false;
        _previewRows = 0;
    }

    private void WriteStatus(FatigueUpdate update, double fps)
    {
        string line = _messages.StatusLine(update, fps);
        int width = Messages.DisplayWidth(line);
        int previous = _statusVisible ? _statusWidth : 0;

        _writer.Write('\r');
        WriteColored(line, StatusColor(update.DisplayState));
        int padding = previous - width;
        if (padding > 0)
        {
            _writer.Write(new string(' ', padding));
        }

        _statusWidth = Math.Max(previous, width);
        _statusVisible = true;
        _writer.Flush();
    }

    private void EraseStatus()
    {
        if (!_statusVisible)
        {
            return;
        }

        _writer.Write('\r');
        _writer.Write(new string(' ', _statusWidth));
        _writer.Write('\r');
        _statusVisible = false;
        _statusWidth = 0;
    }

    private void WriteColored(string text, TerminalColor color)
    {
        if (!_colorEnabled || color == TerminalColor.Default)
        {
            _writer.Write(text);
            return;
        }

        if (_ansi)
        {
            _writer.Write(AnsiCode(color));
            _writer.Write(text);
            _writer.Write("\x1b[0m");
            return;
        }

        ConsoleColor original = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = FallbackColor(color);
            _writer.Write(text);
        }
        finally
        {
            Console.ForegroundColor = original;
        }
    }

    private static TerminalColor EventColor(FatigueEventKind kind) => kind switch
    {
        FatigueEventKind.WarningEntered => TerminalColor.Yellow,
        FatigueEventKind.CriticalEntered => TerminalColor.BoldRed,
        FatigueEventKind.CriticalReminder => TerminalColor.BoldRed,
        FatigueEventKind.WarningDowngraded => TerminalColor.Yellow,
        FatigueEventKind.Recovery => TerminalColor.Green,
        FatigueEventKind.NoFace => TerminalColor.Gray,
        FatigueEventKind.FaceRestored => TerminalColor.Gray,
        FatigueEventKind.QualityLow => TerminalColor.Gray,
        FatigueEventKind.QualityRestored => TerminalColor.Gray,
        _ => TerminalColor.Default,
    };

    private static TerminalColor StatusColor(FatigueDisplayState state) => state switch
    {
        FatigueDisplayState.Warning => TerminalColor.Yellow,
        FatigueDisplayState.Critical => TerminalColor.BoldRed,
        FatigueDisplayState.NoFace => TerminalColor.Gray,
        FatigueDisplayState.QualityLow => TerminalColor.Gray,
        _ => TerminalColor.Default,
    };

    private static string AnsiCode(TerminalColor color) => color switch
    {
        TerminalColor.Gray => "\x1b[90m",
        TerminalColor.Yellow => "\x1b[33m",
        TerminalColor.BoldRed => "\x1b[1;31m",
        TerminalColor.Green => "\x1b[32m",
        _ => string.Empty,
    };

    private static ConsoleColor FallbackColor(TerminalColor color) => color switch
    {
        TerminalColor.Gray => ConsoleColor.DarkGray,
        TerminalColor.Yellow => ConsoleColor.Yellow,
        TerminalColor.BoldRed => ConsoleColor.Red,
        TerminalColor.Green => ConsoleColor.Green,
        _ => ConsoleColor.Gray,
    };
}
