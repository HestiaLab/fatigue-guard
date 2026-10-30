using System.Security.Cryptography;

namespace FatigueGuard.Core.Detection;

public sealed class ModelContractException : Exception
{
    public ModelContractException(string message)
        : base(message)
    {
    }

    public ModelContractException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public static class ModelContract
{
    public const string YunetFileName = "face_detection_yunet_2023mar.onnx";

    public const string FaceMeshFileName = "face_mesh_Nx3x192x192.onnx";

    public const string YunetSha256 = "8f2383e4dd3cfbb4553ea8718107fc0423210dc964f9f4280604804ed2552fa4";

    public const string FaceMeshSha256 = "3ca77cf59c18e4da0eccb46695bf604683fa564253e3385892981a5c274fb10f";

    public static void VerifyFile(string path, string expectedSha256)
    {
        if (!File.Exists(path))
        {
            throw new ModelContractException($"模型文件不存在：{path}");
        }

        string actual;
        try
        {
            using FileStream stream = File.OpenRead(path);
            actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ModelContractException($"无法读取模型文件 {path}: {ex.Message}", ex);
        }

        if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new ModelContractException(
                $"模型文件 SHA256 不匹配：{path}{Environment.NewLine}" +
                $"  期望：{expectedSha256}{Environment.NewLine}" +
                $"  实际：{actual}");
        }
    }
}
