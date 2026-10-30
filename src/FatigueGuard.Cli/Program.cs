using System.Globalization;
using System.Text;
using FatigueGuard.Core;
using FatigueGuard.Core.Analysis;
using FatigueGuard.Core.Config;
using FatigueGuard.Core.Detection;
using FatigueGuard.Core.Sources;
using OpenCvSharp;

namespace FatigueGuard.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (IOException)
        {
            // 无控制台时保持默认编码。
        }

        try
        {
            CliParseResult parse = CliArguments.Parse(args);
            if (parse.Error is not null)
            {
                Console.Error.WriteLine(parse.Error);
                Console.Error.WriteLine("使用 --help 查看用法。");
                return 2;
            }

            CliOptions options = parse.Options!;
            if (options.Help)
            {
                Console.Out.Write(CliArguments.HelpText(options.Language ?? "zh"));
                return 0;
            }

            if (options.Version)
            {
                Console.Out.WriteLine("FatigueGuard " + Version);
                return 0;
            }

            return Run(options);
        }
        catch (ConfigValidationException ex)
        {
            Console.Error.WriteLine("配置错误：");
            foreach (string error in ex.Errors)
            {
                Console.Error.WriteLine("  " + error);
            }

            return 5;
        }
        catch (ModelContractException ex)
        {
            Console.Error.WriteLine("模型错误：" + ex.Message);
            return 4;
        }
        catch (FrameSourceException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 3;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("运行时错误：" + ex.Message);
            return 6;
        }
    }

    private static string Version =>
        typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    private static int Run(CliOptions options)
    {
        FatigueConfig config = options.ConfigPath is null
            ? new FatigueConfig()
            : ConfigLoader.Load(options.ConfigPath);
        ApplyCliOverrides(config, options);
        ConfigLoader.ValidateOrThrow(config);

        bool verbose = options.Verbose;
        if (verbose)
        {
            Log($"配置：refreshHz={config.Display.RefreshHz}, lang={config.Display.Language}, " +
                $"calibration={config.Calibration.Seconds}s, window={config.Metrics.WindowSeconds}s");
        }

        string yunetPath = ResolveModel(ModelContract.YunetFileName);
        string faceMeshPath = ResolveModel(ModelContract.FaceMeshFileName);
        ModelContract.VerifyFile(yunetPath, ModelContract.YunetSha256);
        ModelContract.VerifyFile(faceMeshPath, ModelContract.FaceMeshSha256);
        if (verbose)
        {
            Log($"YuNet: {yunetPath} (SHA256 校验通过)");
            Log($"FaceMesh: {faceMeshPath} (SHA256 校验通过)");
        }

        using ModelFileLease yunetModel = ModelFileLease.Materialize(yunetPath);
        using ModelFileLease faceMeshModel = ModelFileLease.Materialize(faceMeshPath);
        if (verbose && (yunetModel.IsTemporary || faceMeshModel.IsTemporary))
        {
            Log("模型路径包含非 ASCII 字符，已复制到 ASCII 临时目录加载。");
        }

        using FaceAnalyzer faceAnalyzer = CreateFaceAnalyzer(
            yunetModel.ModelPath,
            faceMeshModel.ModelPath,
            config,
            options);
        if (verbose)
        {
            Log(faceAnalyzer.DescribeMetadata());
        }

        var fatigueAnalyzer = new FatigueAnalyzer(config, config.Calibration.Seconds <= 0.0);

        bool isCamera = options.VideoPath is null;
        using IFrameSource source = isCamera
            ? new CameraFrameSource(config.Camera.Index, config.Camera.Width, config.Camera.Height, config.Camera.Fps)
            : new VideoFrameSource(options.VideoPath!);
        if (verbose)
        {
            Log(isCamera
                ? $"帧源：摄像头 index={config.Camera.Index}, 实际分辨率={((CameraFrameSource)source).OpenedWidth}x{((CameraFrameSource)source).OpenedHeight}"
                : $"帧源：视频 {options.VideoPath}, fps={source.Fps.ToString("0.###", CultureInfo.InvariantCulture)}");
        }

        bool interactive = !Console.IsOutputRedirected;
        bool noColor = options.NoColor ||
                       Environment.GetEnvironmentVariable("NO_COLOR") is not null ||
                       config.Display.Color == "off";
        bool colorRequested = interactive && !noColor && config.Display.Color != "off";
        bool ansi = colorRequested && WindowsConsole.TryEnableVirtualTerminal();
        using TerminalPreview? preview = CreatePreview(options, interactive, colorRequested, ansi, verbose);
        var messages = new Messages(config.Display.Language);
        var renderer = new ConsoleRenderer(
            messages,
            interactive,
            options.Quiet,
            colorRequested,
            ansi,
            config.Display.RefreshHz,
            preview: preview);

        using MetricsCsvWriter? csv = options.MetricsCsvPath is null
            ? null
            : new MetricsCsvWriter(options.MetricsCsvPath);
        DebugFrameWriter? debugFrames = options.DebugFramesDir is null
            ? null
            : new DebugFrameWriter(options.DebugFramesDir);

        if (verbose)
        {
            Log($"输出：tty={interactive}, color={colorRequested}, ansi={ansi}, quiet={options.Quiet}, preview={preview is not null}");
        }

        return RunLoop(
            options,
            source,
            faceAnalyzer,
            fatigueAnalyzer,
            renderer,
            csv,
            debugFrames,
            isCamera);
    }

    private static int RunLoop(
        CliOptions options,
        IFrameSource source,
        FaceAnalyzer faceAnalyzer,
        FatigueAnalyzer fatigueAnalyzer,
        ConsoleRenderer renderer,
        MetricsCsvWriter? csv,
        DebugFrameWriter? debugFrames,
        bool isCamera)
    {
        bool cancelled = false;
        ConsoleCancelEventHandler handler = (_, e) =>
        {
            if (cancelled)
            {
                return;
            }

            e.Cancel = true;
            cancelled = true;
        };

        Console.CancelKeyPress += handler;
        double displayFps = source.Fps;
        TimeSpan? previousFrameTime = null;

        try
        {
            while (!cancelled)
            {
                if (!source.TryRead(out FramePacket frame))
                {
                    if (isCamera)
                    {
                        Console.Error.WriteLine(
                            "摄像头读取失败。请检查设备是否被拔出、被其他程序占用，或 Windows 隐私设置是否允许桌面应用访问相机。");
                        return 3;
                    }

                    break;
                }

                if (options.DurationSeconds is { } duration &&
                    frame.StreamTime.TotalSeconds >= duration)
                {
                    break;
                }

                FaceObservation observation = faceAnalyzer.Analyze(frame);
                FatigueUpdate update = fatigueAnalyzer.Process(observation);

                if (isCamera && previousFrameTime is { } previous)
                {
                    double dt = (frame.StreamTime - previous).TotalSeconds;
                    if (dt > 1e-6)
                    {
                        displayFps = (displayFps * 0.9) + ((1.0 / dt) * 0.1);
                    }
                }

                previousFrameTime = frame.StreamTime;

                renderer.Render(update, displayFps, frame.Bgr, faceAnalyzer.LastDebug);
                csv?.MaybeWrite(update);
                debugFrames?.MaybeWrite(update, frame.Bgr, faceAnalyzer.LastDebug);
            }

            return cancelled ? 130 : 0;
        }
        finally
        {
            Console.CancelKeyPress -= handler;
            renderer.Finish();
        }
    }

    private static FaceAnalyzer CreateFaceAnalyzer(
        string yunetPath,
        string faceMeshPath,
        FatigueConfig config,
        CliOptions options)
    {
        try
        {
            return new FaceAnalyzer(
                yunetPath,
                faceMeshPath,
                config.Detection,
                enableDebugOverlay: options.DebugFramesDir is not null || options.Preview);
        }
        catch (ModelContractException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ModelContractException($"加载模型失败：{ex.Message}", ex);
        }
    }

    private static TerminalPreview? CreatePreview(
        CliOptions options,
        bool interactive,
        bool colorRequested,
        bool ansi,
        bool verbose)
    {
        if (!options.Preview)
        {
            return null;
        }

        string? reason = null;
        if (!interactive)
        {
            reason = "输出被重定向";
        }
        else if (options.Quiet)
        {
            reason = "--quiet 与 --preview 不兼容";
        }
        else if (!colorRequested || !ansi)
        {
            reason = "终端不支持 ANSI 颜色（--no-color/NO_COLOR 或 VT 不可用）";
        }

        if (reason is not null)
        {
            Console.Error.WriteLine("预览已禁用：" + reason);
            return null;
        }

        try
        {
            int windowWidth = Console.WindowWidth;
            int windowHeight = Console.WindowHeight;
            if (windowWidth < 90 || windowHeight < 12)
            {
                Console.Error.WriteLine(
                    $"预览已禁用：终端窗口过小（当前 {windowWidth}x{windowHeight}，至少需要 90x12）。");
                return null;
            }

            int maxColumns = Math.Min(56, windowWidth - 4);
            int maxRows = Math.Min(16, windowHeight - 4);
            if (verbose)
            {
                Log($"预览：最多 {maxColumns} 列、{maxRows} 行，刷新跟随 --refresh-hz");
            }

            return new TerminalPreview(maxColumns, maxRows, mirror: !options.NoMirror);
        }
        catch (Exception ex) when (ex is IOException or PlatformNotSupportedException)
        {
            Console.Error.WriteLine("预览已禁用：无法读取终端尺寸（" + ex.Message + "）。");
            return null;
        }
    }

    private static string ResolveModel(string fileName)
    {
        string? path = ModelLocator.Find(fileName);
        if (path is not null)
        {
            return path;
        }

        throw new ModelContractException(
            $"找不到模型文件 models/{fileName}。" + Environment.NewLine +
            "请从仓库根目录运行，或把 models/ 目录放在可执行文件旁。" + Environment.NewLine +
            "搜索目录：" + string.Join("；", ModelLocator.Roots()));
    }

    private static void ApplyCliOverrides(FatigueConfig config, CliOptions options)
    {
        if (options.CalibrateSeconds is { } calibrate)
        {
            config.Calibration.Seconds = calibrate;
            if (calibrate > config.Calibration.MaxSeconds)
            {
                config.Calibration.MaxSeconds = calibrate;
            }
        }

        if (options.RefreshHz is { } refreshHz)
        {
            config.Display.RefreshHz = refreshHz;
        }

        if (options.Language is { } language)
        {
            config.Display.Language = language;
        }

        if (options.NoColor)
        {
            config.Display.Color = "off";
        }

        if (options.CameraSpecified)
        {
            config.Camera.Index = options.CameraIndex;
        }
    }

    private static void Log(string message) => Console.Error.WriteLine("[verbose] " + message);
}
