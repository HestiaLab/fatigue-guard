using FatigueGuard.Core.Detection;

namespace FatigueGuard.Cli;

public sealed class ModelFileLease : IDisposable
{
    private readonly string? _temporaryDirectory;
    private bool _disposed;

    private ModelFileLease(string modelPath, string? temporaryDirectory)
    {
        ModelPath = modelPath;
        _temporaryDirectory = temporaryDirectory;
    }

    public string ModelPath { get; }

    public bool IsTemporary => _temporaryDirectory is not null;

    public static ModelFileLease Materialize(string modelPath)
    {
        if (IsAscii(modelPath))
        {
            return new ModelFileLease(modelPath, null);
        }

        string? root = FindAsciiWritableRoot();
        if (root is null)
        {
            throw new ModelContractException(
                "模型路径包含非 ASCII 字符，且当前系统没有可写的 ASCII 临时目录。" +
                "请将程序移动到纯英文路径后重试。");
        }

        string temporaryDirectory = System.IO.Path.Combine(
            root,
            "FatigueGuard",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);

        string temporaryPath = System.IO.Path.Combine(
            temporaryDirectory,
            System.IO.Path.GetFileName(modelPath));
        File.Copy(modelPath, temporaryPath);

        return new ModelFileLease(temporaryPath, temporaryDirectory);
    }

    public void Dispose()
    {
        if (_disposed || _temporaryDirectory is null)
        {
            return;
        }

        _disposed = true;
        try
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool IsAscii(string value)
    {
        foreach (char c in value)
        {
            if (c > 127)
            {
                return false;
            }
        }

        return true;
    }

    private static string? FindAsciiWritableRoot()
    {
        foreach (string? candidate in CandidateRoots())
        {
            if (string.IsNullOrEmpty(candidate) || !IsAscii(candidate))
            {
                continue;
            }

            try
            {
                Directory.CreateDirectory(System.IO.Path.Combine(candidate, "FatigueGuard"));
                return candidate;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return null;
    }

    private static IEnumerable<string?> CandidateRoots()
    {
        yield return System.IO.Path.GetTempPath();
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        string? systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
        if (!string.IsNullOrEmpty(systemRoot))
        {
            yield return System.IO.Path.Combine(systemRoot, "Temp");
        }
    }
}
