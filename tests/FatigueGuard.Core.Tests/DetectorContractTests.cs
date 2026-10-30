using FatigueGuard.Core;
using FatigueGuard.Core.Config;
using FatigueGuard.Core.Detection;
using OpenCvSharp;

namespace FatigueGuard.Core.Tests;

public sealed class DetectorContractTests
{
    [Fact]
    public void ModelFiles_ExistAndMatchPinnedHashes()
    {
        string yunet = TestSupport.ModelPath(ModelContract.YunetFileName);
        string mesh = TestSupport.ModelPath(ModelContract.FaceMeshFileName);

        Assert.True(File.Exists(yunet), yunet);
        Assert.True(File.Exists(mesh), mesh);
        ModelContract.VerifyFile(yunet, ModelContract.YunetSha256);
        ModelContract.VerifyFile(mesh, ModelContract.FaceMeshSha256);
    }

    [Fact]
    public void HashMismatch_ThrowsModelContractException()
    {
        string path = Path.Combine(Path.GetTempPath(), $"fg-{Guid.NewGuid():N}.onnx");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });

        try
        {
            Assert.Throws<ModelContractException>(() => ModelContract.VerifyFile(path, ModelContract.FaceMeshSha256));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FaceMeshMetadata_MatchesAdapterAssumptions()
    {
        using var analyzer = new FaceAnalyzer(
            TestSupport.ModelPath(ModelContract.YunetFileName),
            TestSupport.ModelPath(ModelContract.FaceMeshFileName),
            new DetectionConfig());

        Assert.Equal("-1,3,192,192", analyzer.InputShape);
        Assert.Equal("-1,468,3", analyzer.OutputShape);
    }

    [Fact]
    public void BlankFrame_ProducesNoFaceObservation()
    {
        using var analyzer = new FaceAnalyzer(
            TestSupport.ModelPath(ModelContract.YunetFileName),
            TestSupport.ModelPath(ModelContract.FaceMeshFileName),
            new DetectionConfig());

        using var frame = new Mat(new Size(640, 480), MatType.CV_8UC3, Scalar.All(0));
        FaceObservation observation = analyzer.Analyze(new FramePacket(0, TimeSpan.Zero, frame));

        Assert.Equal(0, observation.FaceCount);
        Assert.Null(observation.LeftEar);
        Assert.Null(observation.RightEar);
        Assert.Null(analyzer.LastDebug);
    }

    [Fact]
    public void EyeLandmarkIndices_MatchSpecification()
    {
        Assert.Equal(new[] { 33, 160, 158, 133, 153, 144 }, EyeLandmarks.RightEye);
        Assert.Equal(new[] { 362, 385, 387, 263, 373, 380 }, EyeLandmarks.LeftEye);
    }
}
