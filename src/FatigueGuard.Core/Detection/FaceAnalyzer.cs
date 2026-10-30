using System.Globalization;
using System.Runtime.InteropServices;
using FatigueGuard.Core.Analysis;
using FatigueGuard.Core.Config;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using OpenCvSharp.Dnn;

namespace FatigueGuard.Core.Detection;

public sealed record FaceDebugInfo(Rect FaceBox, IReadOnlyList<Point2f> Landmarks);

public sealed class FaceAnalyzer : IFaceAnalyzer
{
    public const int MeshInputSize = 192;

    public const int MeshLandmarkCount = 468;

    private const double FaceCropMarginScale = 1.4;

    private const int YunetOutputColumns = 15;

    private const int YunetScoreColumn = 14;

    private readonly string _yunetPath;
    private readonly DetectionConfig _config;
    private readonly bool _debugOverlay;
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly string _landmarkOutputName;
    private readonly string _inputShape;
    private readonly DenseTensor<float> _inputTensor;
    private readonly float[] _landmarkValues = new float[MeshLandmarkCount * 3];
    private readonly float[] _channelBuffer = new float[MeshInputSize * MeshInputSize];
    private readonly Mat _crop = new();
    private readonly Mat _rgb = new();
    private readonly Mat _float = new();
    private readonly Mat _ch0 = new();
    private readonly Mat _ch1 = new();
    private readonly Mat _ch2 = new();
    private readonly Mat _affine = new(2, 3, MatType.CV_64F);
    private readonly Mat _inverseAffine = new(2, 3, MatType.CV_64F);
    private readonly Mat _faces = new();
    private readonly Point2f[] _debugLandmarks = new Point2f[MeshLandmarkCount];

    private FaceDetectorYN? _detector;
    private Size _detectorSize;
    private Rect _cachedBox;
    private int _cachedFaceCount;

    public FaceAnalyzer(
        string yunetModelPath,
        string faceMeshModelPath,
        DetectionConfig config,
        bool enableDebugOverlay = false)
    {
        _yunetPath = yunetModelPath;
        _config = config;
        _debugOverlay = enableDebugOverlay;

        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            IntraOpNumThreads = 2,
            InterOpNumThreads = 1,
        };

        _session = new InferenceSession(faceMeshModelPath, options);

        if (_session.InputMetadata.Count != 1)
        {
            throw new ModelContractException(
                $"FaceMesh 期望 1 个输入，实际 {_session.InputMetadata.Count} 个。");
        }

        KeyValuePair<string, NodeMetadata> input = _session.InputMetadata.First();
        _inputName = input.Key;
        int[] dims = input.Value.Dimensions;
        _inputShape = string.Join(",", dims);
        if (dims.Length != 4 ||
            dims[1] != 3 ||
            dims[2] != MeshInputSize ||
            dims[3] != MeshInputSize)
        {
            throw new ModelContractException(
                $"FaceMesh 输入 {_inputName} 形状为 [{_inputShape}]，期望 [N,3,{MeshInputSize},{MeshInputSize}]。");
        }

        string? landmarkOutput = null;
        string landmarkShape = string.Empty;
        foreach (KeyValuePair<string, NodeMetadata> output in _session.OutputMetadata)
        {
            int[] outputDims = output.Value.Dimensions;
            if (outputDims.Length == 3 && outputDims[1] == MeshLandmarkCount && outputDims[2] == 3)
            {
                landmarkOutput = output.Key;
                landmarkShape = string.Join(",", outputDims);
            }
        }

        if (landmarkOutput is null)
        {
            throw new ModelContractException(
                $"FaceMesh 未找到 [N,{MeshLandmarkCount},3] 的关键点输出。" +
                $"实际输出：{string.Join(", ", _session.OutputMetadata.Keys)}");
        }

        _landmarkOutputName = landmarkOutput;
        OutputShape = landmarkShape;
        _inputTensor = new DenseTensor<float>(new[] { 1, 3, MeshInputSize, MeshInputSize });
    }

    public string InputShape => _inputShape;

    public string OutputShape { get; }

    public FaceDebugInfo? LastDebug { get; private set; }

    public FaceObservation Analyze(FramePacket frame)
    {
        Mat bgr = frame.Bgr;
        if (bgr.Type() != MatType.CV_8UC3)
        {
            throw new InvalidOperationException($"帧图像必须是 CV_8UC3，实际为 {bgr.Type()}。");
        }

        FaceDetectorYN detector = EnsureDetector(bgr.Width, bgr.Height);

        Rect box;
        int faceCount;
        bool detectThisFrame =
            _config.DetectEveryNthFrame <= 1 ||
            _cachedBox.Width <= 0 ||
            frame.Index % _config.DetectEveryNthFrame == 0;

        if (detectThisFrame)
        {
            detector.Detect(bgr, _faces);
            faceCount = 0;
            double bestArea = 0.0;
            int bestRow = -1;
            int faceRows = _faces.Rows;
            for (int row = 0; row < faceRows; row++)
            {
                float score = _faces.At<float>(row, YunetScoreColumn);
                if (score < _config.MinFaceConfidence)
                {
                    continue;
                }

                faceCount++;
                float width = _faces.At<float>(row, 2);
                float height = _faces.At<float>(row, 3);
                if (width < _config.MinFaceSize)
                {
                    continue;
                }

                double area = width * height;
                if (area > bestArea)
                {
                    bestArea = area;
                    bestRow = row;
                }
            }

            if (bestRow < 0)
            {
                CacheFace(default, faceCount);
                LastDebug = null;
                return new FaceObservation(frame.Index, frame.StreamTime, default, faceCount, null, null);
            }

            float fx = _faces.At<float>(bestRow, 0);
            float fy = _faces.At<float>(bestRow, 1);
            float fw = _faces.At<float>(bestRow, 2);
            float fh = _faces.At<float>(bestRow, 3);
            box = new Rect(
                (int)Math.Round(fx),
                (int)Math.Round(fy),
                (int)Math.Round(fw),
                (int)Math.Round(fh));
            CacheFace(box, faceCount);
        }
        else
        {
            box = _cachedBox;
            faceCount = _cachedFaceCount;
            if (box.Width <= 0)
            {
                LastDebug = null;
                return new FaceObservation(frame.Index, frame.StreamTime, default, faceCount, null, null);
            }
        }

        double side = Math.Max(box.Width, box.Height) * FaceCropMarginScale;
        double cx = box.X + (box.Width / 2.0);
        double cy = box.Y + (box.Height / 2.0);
        double scale = MeshInputSize / side;
        double half = MeshInputSize / 2.0;

        _affine.Set(0, 0, scale);
        _affine.Set(0, 1, 0.0);
        _affine.Set(0, 2, half - (cx * scale));
        _affine.Set(1, 0, 0.0);
        _affine.Set(1, 1, scale);
        _affine.Set(1, 2, half - (cy * scale));

        Cv2.WarpAffine(
            bgr,
            _crop,
            _affine,
            new Size(MeshInputSize, MeshInputSize),
            InterpolationFlags.Linear,
            BorderTypes.Constant,
            Scalar.All(0));

        Cv2.CvtColor(_crop, _rgb, ColorConversionCodes.BGR2RGB);
        _rgb.ConvertTo(_float, MatType.CV_32FC3, 1.0 / 255.0);
        Cv2.ExtractChannel(_float, _ch0, 0);
        Cv2.ExtractChannel(_float, _ch1, 1);
        Cv2.ExtractChannel(_float, _ch2, 2);

        CopyChannelToTensor(_ch0, 0);
        CopyChannelToTensor(_ch1, 1);
        CopyChannelToTensor(_ch2, 2);

        using (IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(
                   new[] { NamedOnnxValue.CreateFromTensor(_inputName, _inputTensor) },
                   new[] { _landmarkOutputName }))
        {
            Tensor<float> tensor = results[0].AsTensor<float>();
            ReadOnlySpan<float> buffer = tensor is DenseTensor<float> dense
                ? dense.Buffer.Span
                : tensor.ToArray();
            if (buffer.Length < _landmarkValues.Length)
            {
                throw new ModelContractException(
                    $"FaceMesh 输出长度 {buffer.Length} 小于 {_landmarkValues.Length}。");
            }

            buffer[.._landmarkValues.Length].CopyTo(_landmarkValues);
        }

        double? rightEar = EarCalculator.ComputeEyeEar(
            _landmarkValues,
            EyeLandmarks.RightEye[0],
            EyeLandmarks.RightEye[1],
            EyeLandmarks.RightEye[2],
            EyeLandmarks.RightEye[3],
            EyeLandmarks.RightEye[4],
            EyeLandmarks.RightEye[5]);

        double? leftEar = EarCalculator.ComputeEyeEar(
            _landmarkValues,
            EyeLandmarks.LeftEye[0],
            EyeLandmarks.LeftEye[1],
            EyeLandmarks.LeftEye[2],
            EyeLandmarks.LeftEye[3],
            EyeLandmarks.LeftEye[4],
            EyeLandmarks.LeftEye[5]);

        LastDebug = _debugOverlay ? BuildDebugInfo(box) : null;

        return new FaceObservation(frame.Index, frame.StreamTime, box, faceCount, leftEar, rightEar);
    }

    public string DescribeMetadata()
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"FaceMesh input={_inputName} shape=[{_inputShape}] outputs=landmarks=[{OutputShape}]");
    }

    public void Dispose()
    {
        _detector?.Dispose();
        _session.Dispose();
        _crop.Dispose();
        _rgb.Dispose();
        _float.Dispose();
        _ch0.Dispose();
        _ch1.Dispose();
        _ch2.Dispose();
        _affine.Dispose();
        _inverseAffine.Dispose();
        _faces.Dispose();
    }

    private FaceDetectorYN EnsureDetector(int width, int height)
    {
        var size = new Size(width, height);
        if (_detector is not null && _detectorSize == size)
        {
            return _detector;
        }

        _detector?.Dispose();
        _detector = FaceDetectorYN.Create(
            _yunetPath,
            string.Empty,
            size,
            (float)_config.MinFaceConfidence,
            0.3f,
            5000,
            Backend.DEFAULT,
            Target.CPU);
        _detectorSize = size;
        _cachedBox = default;
        _cachedFaceCount = 0;
        return _detector;
    }

    private void CacheFace(Rect box, int faceCount)
    {
        _cachedBox = box;
        _cachedFaceCount = faceCount;
    }

    private void CopyChannelToTensor(Mat channel, int index)
    {
        int pixelCount = MeshInputSize * MeshInputSize;
        Marshal.Copy(channel.Data, _channelBuffer, 0, pixelCount);
        Span<float> target = _inputTensor.Buffer.Span.Slice(index * pixelCount, pixelCount);
        _channelBuffer.AsSpan(0, pixelCount).CopyTo(target);
    }

    private FaceDebugInfo BuildDebugInfo(Rect box)
    {
        Cv2.InvertAffineTransform(_affine, _inverseAffine);
        double a = _inverseAffine.At<double>(0, 0);
        double b = _inverseAffine.At<double>(0, 1);
        double c = _inverseAffine.At<double>(0, 2);
        double d = _inverseAffine.At<double>(1, 0);
        double e = _inverseAffine.At<double>(1, 1);
        double f = _inverseAffine.At<double>(1, 2);

        for (int i = 0; i < MeshLandmarkCount; i++)
        {
            double x = _landmarkValues[i * 3];
            double y = _landmarkValues[(i * 3) + 1];
            _debugLandmarks[i] = new Point2f(
                (float)((a * x) + (b * y) + c),
                (float)((d * x) + (e * y) + f));
        }

        return new FaceDebugInfo(box, _debugLandmarks);
    }
}
