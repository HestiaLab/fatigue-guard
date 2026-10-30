using System.Globalization;

namespace FatigueGuard.Cli;

public sealed record CliOptions
{
    public bool CameraSpecified { get; init; }

    public int CameraIndex { get; init; }

    public string? VideoPath { get; init; }

    public string? ConfigPath { get; init; }

    public double? CalibrateSeconds { get; init; }

    public double? DurationSeconds { get; init; }

    public double? RefreshHz { get; init; }

    public bool NoColor { get; init; }

    public string? Language { get; init; }

    public string? MetricsCsvPath { get; init; }

    public string? DebugFramesDir { get; init; }

    public bool Quiet { get; init; }

    public bool Verbose { get; init; }

    public bool Preview { get; init; }

    public bool NoMirror { get; init; }

    public bool Help { get; init; }

    public bool Version { get; init; }
}

public sealed record CliParseResult(CliOptions? Options, string? Error);

public static class CliArguments
{
    public static CliParseResult Parse(string[] args)
    {
        string language = PreScanLanguage(args);
        if (args.Any(a => a is "--help" or "-h"))
        {
            return new CliParseResult(new CliOptions { Help = true, Language = language }, null);
        }

        if (args.Any(a => a == "--version"))
        {
            return new CliParseResult(new CliOptions { Version = true, Language = language }, null);
        }

        bool cameraSpecified = false;
        int cameraIndex = 0;
        string? videoPath = null;
        string? configPath = null;
        double? calibrate = null;
        double? duration = null;
        double? refreshHz = null;
        bool noColor = false;
        string? lang = null;
        string? metricsCsv = null;
        string? debugFrames = null;
        bool quiet = false;
        bool verbose = false;
        bool preview = false;
        bool noMirror = false;

        for (int i = 0; i < args.Length; i++)
        {
            string argument = args[i];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                return Error($"无法识别的参数：{argument}");
            }

            string name = argument;
            string? inlineValue = null;
            int equals = argument.IndexOf('=');
            if (equals >= 0)
            {
                name = argument[..equals];
                inlineValue = argument[(equals + 1)..];
            }

            switch (name)
            {
                case "--camera":
                    if (!TakeValue(args, ref i, inlineValue, name, out string cameraValue, out string? error))
                    {
                        return Error(error!);
                    }

                    if (!int.TryParse(cameraValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out cameraIndex) ||
                        cameraIndex < 0)
                    {
                        return Error("--camera 需要大于等于 0 的整数索引。");
                    }

                    cameraSpecified = true;
                    break;

                case "--video":
                    if (!TakeValue(args, ref i, inlineValue, name, out videoPath, out error))
                    {
                        return Error(error!);
                    }

                    break;

                case "--config":
                    if (!TakeValue(args, ref i, inlineValue, name, out configPath, out error))
                    {
                        return Error(error!);
                    }

                    break;

                case "--calibrate":
                    if (!TakeDouble(args, ref i, inlineValue, name, out double calibrateValue, out error))
                    {
                        return Error(error!);
                    }

                    if (calibrateValue < 0.0)
                    {
                        return Error("--calibrate 不能为负数。");
                    }

                    calibrate = calibrateValue;
                    break;

                case "--duration":
                    if (!TakeDouble(args, ref i, inlineValue, name, out double durationValue, out error))
                    {
                        return Error(error!);
                    }

                    if (durationValue <= 0.0)
                    {
                        return Error("--duration 必须大于 0。");
                    }

                    duration = durationValue;
                    break;

                case "--refresh-hz":
                    if (!TakeDouble(args, ref i, inlineValue, name, out double refreshValue, out error))
                    {
                        return Error(error!);
                    }

                    if (refreshValue <= 0.0 || refreshValue > 60.0)
                    {
                        return Error("--refresh-hz 必须位于 (0, 60] 区间。");
                    }

                    refreshHz = refreshValue;
                    break;

                case "--lang":
                    if (!TakeValue(args, ref i, inlineValue, name, out lang, out error))
                    {
                        return Error(error!);
                    }

                    if (lang is not ("zh" or "en"))
                    {
                        return Error("--lang 仅支持 zh 或 en。");
                    }

                    break;

                case "--metrics-csv":
                    if (!TakeValue(args, ref i, inlineValue, name, out metricsCsv, out error))
                    {
                        return Error(error!);
                    }

                    break;

                case "--debug-frames":
                    if (!TakeValue(args, ref i, inlineValue, name, out debugFrames, out error))
                    {
                        return Error(error!);
                    }

                    break;

                case "--no-color":
                    noColor = true;
                    break;

                case "--quiet":
                    quiet = true;
                    break;

                case "--verbose":
                    verbose = true;
                    break;

                case "--preview":
                    preview = true;
                    break;

                case "--no-mirror":
                    noMirror = true;
                    break;

                case "--help":
                case "-h":
                case "--version":
                    break;

                default:
                    return Error($"未知参数：{argument}");
            }
        }

        if (cameraSpecified && videoPath is not null)
        {
            return Error("--camera 与 --video 互斥，只能选择一种帧源。");
        }

        return new CliParseResult(
            new CliOptions
            {
                CameraSpecified = cameraSpecified,
                CameraIndex = cameraIndex,
                VideoPath = videoPath,
                ConfigPath = configPath,
                CalibrateSeconds = calibrate,
                DurationSeconds = duration,
                RefreshHz = refreshHz,
                NoColor = noColor,
                Language = lang,
                MetricsCsvPath = metricsCsv,
                DebugFramesDir = debugFrames,
                Quiet = quiet,
                Verbose = verbose,
                Preview = preview,
                NoMirror = noMirror,
            },
            null);
    }

    public static string HelpText(string language)
    {
        if (language == "en")
        {
            return
                "FatigueGuard - offline webcam fatigue monitor (no network, no recording)\n" +
                "\n" +
                "Usage: FatigueGuard [options]\n" +
                "\n" +
                "  --camera <index>        Camera index, default 0; mutually exclusive with --video\n" +
                "  --video <path>          Offline video file (deterministic replay for testing)\n" +
                "  --config <path>         JSON configuration file\n" +
                "  --calibrate <seconds>   Calibration duration, default 20; 0 skips calibration\n" +
                "  --duration <seconds>    Stop after the given stream time; video otherwise plays to the end\n" +
                "  --refresh-hz <n>        Status line refresh rate, default 5\n" +
                "  --no-color              Disable ANSI colors (NO_COLOR is also honored)\n" +
                "  --lang <zh|en>          UI language, default zh\n" +
                "  --metrics-csv <path>    Write per-second numeric metrics CSV\n" +
                "  --debug-frames <dir>    Export annotated PNG frames (development only)\n" +
                "  --quiet                 Only print status events, no live status line\n" +
                "  --preview               Live terminal preview of the camera (ANSI half blocks, off by default)\n" +
                "  --no-mirror             Do not mirror the preview horizontally (source is already mirrored)\n" +
                "  --verbose               Diagnostic logs to stderr\n" +
                "  --help                  Show this help\n" +
                "  --version               Show version\n" +
                "\n" +
                "Exit codes: 0 success, 2 argument error, 3 camera/video failure, 4 model missing or contract mismatch,\n" +
                "            5 invalid configuration, 6 runtime error, 130 Ctrl+C\n" +
                "\n" +
                "Models are loaded from ./models or the models/ directory next to the executable.\n";
        }

        return
            "FatigueGuard - 本地离线摄像头疲劳监测（不联网、不录像、不保存画面）\n" +
            "\n" +
            "用法：FatigueGuard [options]\n" +
            "\n" +
            "  --camera <index>        摄像头索引，默认 0；与 --video 互斥\n" +
            "  --video <path>          离线视频文件（用于确定性回放与测试）\n" +
            "  --config <path>         JSON 配置文件\n" +
            "  --calibrate <seconds>   校准时长，默认 20；0 表示跳过校准\n" +
            "  --duration <seconds>    运行指定流时间后退出；视频模式默认播放到结尾\n" +
            "  --refresh-hz <n>        状态行刷新频率，默认 5\n" +
            "  --no-color              禁用 ANSI 颜色（同时尊重 NO_COLOR 环境变量）\n" +
            "  --lang <zh|en>          界面语言，默认 zh\n" +
            "  --metrics-csv <path>    逐秒输出指标 CSV（仅数值）\n" +
            "  --debug-frames <dir>    导出带框和关键点的 PNG（仅开发调试）\n" +
            "  --quiet                 只输出状态事件，不输出实时状态行\n" +
            "  --preview               在终端底部实时预览摄像头画面（ANSI 半块字符，默认关闭）\n" +
            "  --no-mirror             预览不做水平镜像（摄像头源本身已镜像时使用）\n" +
            "  --verbose               诊断日志写 stderr\n" +
            "  --help                  显示帮助\n" +
            "  --version               显示版本\n" +
            "\n" +
            "退出码：0 正常；2 参数错误；3 摄像头/视频失败；4 模型缺失或契约不符；5 配置非法；6 运行时错误；130 Ctrl+C\n" +
            "\n" +
            "模型从 ./models 或可执行文件旁的 models/ 目录加载。\n";
    }

    private static string PreScanLanguage(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--lang" && i + 1 < args.Length)
            {
                return args[i + 1] == "en" ? "en" : "zh";
            }

            if (args[i].StartsWith("--lang=", StringComparison.Ordinal))
            {
                return args[i]["--lang=".Length..] == "en" ? "en" : "zh";
            }
        }

        return "zh";
    }

    private static CliParseResult Error(string message) => new(null, message);

    private static bool TakeValue(
        string[] args,
        ref int index,
        string? inlineValue,
        string name,
        out string value,
        out string? error)
    {
        if (inlineValue is not null)
        {
            if (inlineValue.Length == 0)
            {
                value = string.Empty;
                error = $"{name} 需要一个非空值。";
                return false;
            }

            value = inlineValue;
            error = null;
            return true;
        }

        if (index + 1 >= args.Length)
        {
            value = string.Empty;
            error = $"{name} 缺少参数值。";
            return false;
        }

        index++;
        value = args[index];
        error = value.Length == 0 ? $"{name} 需要一个非空值。" : null;
        return error is null;
    }

    private static bool TakeDouble(
        string[] args,
        ref int index,
        string? inlineValue,
        string name,
        out double value,
        out string? error)
    {
        if (!TakeValue(args, ref index, inlineValue, name, out string text, out error))
        {
            value = 0.0;
            return false;
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
            !double.IsFinite(value))
        {
            error = $"{name} 需要有限数值，实际为 \"{text}\"。";
            return false;
        }

        error = null;
        return true;
    }
}
