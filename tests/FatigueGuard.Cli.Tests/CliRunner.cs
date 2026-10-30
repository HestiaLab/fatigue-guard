using System.Diagnostics;
using System.Text;
using OpenCvSharp;
using Xunit;

namespace FatigueGuard.Cli.Tests;

public sealed record CliResult(int ExitCode, string StdOut, string StdErr);

public static class CliRunner
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string CliDll { get; } = FindCliDll();

    public static string RepoPath(params string[] parts)
        => Path.Combine(new[] { RepoRoot }.Concat(parts).ToArray());

    public static CliResult Run(params string[] args)
        => RunIn(RepoRoot, args);

    public static CliResult RunIn(string workingDirectory, params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        startInfo.ArgumentList.Add(CliDll);
        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("CLI 进程超时。");
        }

        Task.WaitAll(stdout, stderr);
        return new CliResult(process.ExitCode, stdout.Result, stderr.Result);
    }

    public static string CreateBlankVideo(string path, double seconds = 3.0, int fps = 30, int width = 320, int height = 240)
    {
        using var writer = new VideoWriter(path, FourCC.MJPG, fps, new Size(width, height));
        if (!writer.IsOpened())
        {
            throw new InvalidOperationException("无法创建测试视频。");
        }

        using var frame = new Mat(new Size(width, height), MatType.CV_8UC3, Scalar.All(0));
        int count = (int)(seconds * fps);
        for (int i = 0; i < count; i++)
        {
            writer.Write(frame);
        }

        return path;
    }

    private static string FindRepoRoot()
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

        throw new InvalidOperationException("找不到仓库根目录。");
    }

    private static string FindCliDll()
    {
        string binDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        string configuration = Directory.GetParent(binDirectory)?.Name ?? "Debug";
        string path = Path.Combine(
            RepoRoot,
            "src",
            "FatigueGuard.Cli",
            "bin",
            configuration,
            "net8.0",
            "FatigueGuard.dll");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"找不到 CLI 构建产物：{path}");
        }

        return path;
    }
}

public sealed class MediaFactAttribute : FactAttribute
{
    public MediaFactAttribute()
    {
        string sample = CliRunner.RepoPath("samples", "face_sample.mp4");
        if (!File.Exists(sample))
        {
            Skip = $"SKIPPED: 缺少媒体样本 {sample}（提供方法见 README“媒体测试”一节）。";
        }
    }
}
