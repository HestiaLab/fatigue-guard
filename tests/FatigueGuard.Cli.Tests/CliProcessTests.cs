using Xunit;

namespace FatigueGuard.Cli.Tests;

public sealed class CliProcessTests
{
    [Fact]
    public void Help_ExitsZero()
    {
        CliResult result = CliRunner.Run("--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("用法", result.StdOut);
        Assert.Contains("--video", result.StdOut);
        Assert.Contains("--preview", result.StdOut);
        Assert.Contains("--no-mirror", result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }

    [Fact]
    public void Version_ExitsZero()
    {
        CliResult result = CliRunner.Run("--version");

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("FatigueGuard 0.1.1", result.StdOut);
    }

    [Theory]
    [InlineData("--bogus")]
    [InlineData("--camera")]
    [InlineData("--camera", "0", "--video", "x.mp4")]
    [InlineData("--refresh-hz", "0")]
    public void InvalidArguments_ExitTwo(params string[] args)
    {
        CliResult result = CliRunner.Run(args);

        Assert.Equal(2, result.ExitCode);
        Assert.NotEqual(string.Empty, result.StdErr);
    }

    [Fact]
    public void InvalidConfig_ExitsFiveWithFieldName()
    {
        string path = Path.Combine(Path.GetTempPath(), $"fg-invalid-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "metrics": { "windowSeconds": 20, "minValidSeconds": 30 } }""");

        try
        {
            CliResult result = CliRunner.Run("--config", path);

            Assert.Equal(5, result.ExitCode);
            Assert.Contains("metrics.minValidSeconds", result.StdErr);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MissingVideo_ExitsThree()
    {
        CliResult result = CliRunner.Run("--video", Path.Combine(Path.GetTempPath(), "does-not-exist.mp4"));

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("找不到视频", result.StdErr);
    }

    [Fact]
    public void MissingModels_ExitFour()
    {
        // 构建会把 models/ 复制到 CLI 输出目录；临时移走它以覆盖“模型缺失”路径。
        string cliDirectory = Path.GetDirectoryName(CliRunner.CliDll)!;
        string modelsDirectory = Path.Combine(cliDirectory, "models");
        string hiddenDirectory = modelsDirectory + ".hidden-" + Guid.NewGuid().ToString("N");
        string emptyDirectory = Path.Combine(Path.GetTempPath(), $"fg-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(emptyDirectory);
        bool moved = false;

        try
        {
            if (Directory.Exists(modelsDirectory))
            {
                Directory.Move(modelsDirectory, hiddenDirectory);
                moved = true;
            }

            CliResult result = CliRunner.RunIn(emptyDirectory, "--video", "anything.mp4");

            Assert.Equal(4, result.ExitCode);
            Assert.Contains("找不到模型文件", result.StdErr);
        }
        finally
        {
            if (moved && Directory.Exists(hiddenDirectory))
            {
                Directory.Move(hiddenDirectory, modelsDirectory);
            }

            Directory.Delete(emptyDirectory, recursive: true);
        }
    }

    [Fact]
    public void BlankVideo_NoFaceEventWithoutAnsiOrCarriageReturn()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"fg-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            string video = CliRunner.CreateBlankVideo(Path.Combine(directory, "blank.avi"));
            CliResult result = CliRunner.Run(
                "--video", video,
                "--calibrate", "0",
                "--duration", "3",
                "--no-color");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("未检测到人脸", result.StdOut);
            Assert.Contains("已跳过校准", result.StdOut);
            Assert.DoesNotContain("\x1b[", result.StdOut);
            Assert.DoesNotContain('\r', result.StdOut);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void PreviewWithRedirectedOutput_IsDisabledGracefully()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"fg-preview-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            string video = CliRunner.CreateBlankVideo(Path.Combine(directory, "blank.avi"));
            CliResult result = CliRunner.Run(
                "--video", video,
                "--calibrate", "0",
                "--duration", "3",
                "--preview");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("预览已禁用", result.StdErr);
            Assert.DoesNotContain("\x1b[", result.StdOut);
            Assert.DoesNotContain('\r', result.StdOut);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void VideoMode_IsDeterministicAcrossRuns()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"fg-det-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            string video = CliRunner.CreateBlankVideo(Path.Combine(directory, "blank.avi"));
            string csv1 = Path.Combine(directory, "m1.csv");
            string csv2 = Path.Combine(directory, "m2.csv");

            CliResult first = CliRunner.Run("--video", video, "--calibrate", "0", "--duration", "3", "--metrics-csv", csv1);
            CliResult second = CliRunner.Run("--video", video, "--calibrate", "0", "--duration", "3", "--metrics-csv", csv2);

            Assert.Equal(0, first.ExitCode);
            Assert.Equal(0, second.ExitCode);
            Assert.Equal(first.StdOut, second.StdOut);
            Assert.Equal(File.ReadAllText(csv1), File.ReadAllText(csv2));
            Assert.True(File.ReadAllLines(csv1).Length >= 3);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Verbose_PrintsModelMetadataToStderr()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"fg-verbose-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            string video = CliRunner.CreateBlankVideo(Path.Combine(directory, "blank.avi"), seconds: 1);
            CliResult result = CliRunner.Run(
                "--video", video,
                "--calibrate", "0",
                "--duration", "1",
                "--verbose");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("FaceMesh input=input shape=[-1,3,192,192]", result.StdErr);
            Assert.Contains("outputs=landmarks=[-1,468,3]", result.StdErr);
            Assert.Contains("SHA256 校验通过", result.StdErr);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DebugFrames_AreExported()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"fg-debug-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            string video = CliRunner.CreateBlankVideo(Path.Combine(directory, "blank.avi"));
            string frames = Path.Combine(directory, "frames");
            CliResult result = CliRunner.Run(
                "--video", video,
                "--calibrate", "0",
                "--duration", "3",
                "--debug-frames", frames);

            Assert.Equal(0, result.ExitCode);
            string[] files = Directory.GetFiles(frames, "*.png");
            Assert.Equal(3, files.Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void EnglishLanguage_WorksForHelpAndEvents()
    {
        CliResult help = CliRunner.Run("--help", "--lang", "en");
        Assert.Equal(0, help.ExitCode);
        Assert.Contains("Usage:", help.StdOut);

        string directory = Path.Combine(Path.GetTempPath(), $"fg-en-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            string video = CliRunner.CreateBlankVideo(Path.Combine(directory, "blank.avi"));
            CliResult result = CliRunner.Run(
                "--video", video,
                "--calibrate", "0",
                "--duration", "3",
                "--lang", "en",
                "--no-color");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Calibration skipped", result.StdOut);
            Assert.Contains("No face detected", result.StdOut);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [MediaFact]
    [Trait("Category", "Media")]
    public void FaceSample_DetectsMicrosleep()
    {
        string video = CliRunner.RepoPath("samples", "face_sample.mp4");
        string config = CliRunner.RepoPath("samples", "verification-config.json");

        CliResult result = CliRunner.Run(
            "--video", video,
            "--config", config,
            "--no-color");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("校准完成", result.StdOut);
        Assert.Contains("严重疲劳：检测到微睡眠", result.StdOut);
    }
}
