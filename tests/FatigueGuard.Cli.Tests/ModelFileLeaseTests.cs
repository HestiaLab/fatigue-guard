using System.Text;
using FatigueGuard.Cli;

namespace FatigueGuard.Cli.Tests;

public sealed class ModelFileLeaseTests
{
    [Fact]
    public void Materialize_AsciiPath_UsesOriginalPath()
    {
        string root = Path.Combine(Path.GetTempPath(), $"fg-ascii-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string modelPath = Path.Combine(root, "model.onnx");
        File.WriteAllText(modelPath, "ascii");

        try
        {
            using ModelFileLease lease = ModelFileLease.Materialize(modelPath);

            Assert.False(lease.IsTemporary);
            Assert.Equal(modelPath, lease.ModelPath);
            Assert.Equal("ascii", File.ReadAllText(lease.ModelPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Materialize_UnicodePath_CopiesToAsciiPathAndCleansUp()
    {
        string root = Path.Combine(Path.GetTempPath(), $"fg-中文-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string modelPath = Path.Combine(root, "model.onnx");
        File.WriteAllText(modelPath, "模型", new UTF8Encoding(false));
        string? temporaryPath = null;

        try
        {
            using (ModelFileLease lease = ModelFileLease.Materialize(modelPath))
            {
                temporaryPath = lease.ModelPath;

                Assert.True(lease.IsTemporary);
                Assert.True(ContainsOnlyAscii(temporaryPath));
                Assert.Equal("模型", File.ReadAllText(temporaryPath, new UTF8Encoding(false)));
            }

            Assert.False(File.Exists(temporaryPath));
            Assert.True(File.Exists(modelPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static bool ContainsOnlyAscii(string value) => value.All(c => c <= 127);
}
