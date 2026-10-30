# 模型清单与契约

本目录中的模型随仓库提交，程序启动时会校验文件存在性与 SHA256，不匹配则退出（退出码 4）。

| 文件 | 大小 | SHA256 | 许可证 | 来源 | 下载日期 |
|---|---|---|---|---|---|
| `face_detection_yunet_2023mar.onnx` | 232,589 B | `8f2383e4dd3cfbb4553ea8718107fc0423210dc964f9f4280604804ed2552fa4` | Apache-2.0 | [OpenCV Zoo / face_detection_yunet](https://github.com/opencv/opencv_zoo/tree/main/models/face_detection_yunet) | 2026-10-30 |
| `face_mesh_Nx3x192x192.onnx` | 2,466,068 B | `3ca77cf59c18e4da0eccb46695bf604683fa564253e3385892981a5c274fb10f` | Apache-2.0 | [yakhyo/mediapipe-face-mesh-onnx](https://github.com/yakhyo/mediapipe-face-mesh-onnx)（权重来源于 Google MediaPipe Face Mesh，Apache-2.0） | 2026-10-30 |

YuNet 的实际下载地址是 OpenCV Zoo 的 Git LFS 文件：

```
https://media.githubusercontent.com/media/opencv/opencv_zoo/main/models/face_detection_yunet/face_detection_yunet_2023mar.onnx
```

其 LFS 指针中的 `oid sha256` 与上表一致。FaceMesh 的下载地址：

```
https://github.com/yakhyo/mediapipe-face-mesh-onnx/releases/download/weights/face_mesh_Nx3x192x192.onnx
```

## YuNet（人脸检测）

程序通过 OpenCvSharp 的 `FaceDetectorYN` 调用该 ONNX，不直接用 ONNX Runtime 运行它。

ONNX 文件元数据（由 ONNX Runtime 实际读取）：

- 输入：`input`，`float32`，`[1, 3, 640, 640]`（OpenCV DNN 会按实际图像尺寸重排网络输入）
- 输出：
  - `cls_8` `[1, 6400, 1]`、`cls_16` `[1, 1600, 1]`、`cls_32` `[1, 400, 1]`
  - `obj_8` `[1, 6400, 1]`、`obj_16` `[1, 1600, 1]`、`obj_32` `[1, 400, 1]`
  - `bbox_8` `[1, 6400, 4]`、`bbox_16` `[1, 1600, 4]`、`bbox_32` `[1, 400, 4]`
  - `kps_8` `[1, 6400, 10]`、`kps_16` `[1, 1600, 10]`、`kps_32` `[1, 400, 10]`

`FaceDetectorYN.Detect` 的输出是一个 `N x 15` 的 `CV_32F` 矩阵，每行依次为：

```
x, y, w, h, 右眼x, 右眼y, 左眼x, 左眼y, 鼻尖x, 鼻尖y, 右嘴角x, 右嘴角y, 左嘴角x, 左嘴角y, score
```

程序只使用 `x, y, w, h` 与 `score`（第 15 列）。`FaceDetectorYN` 要求输入图像尺寸与创建时一致，因此适配器会在首帧按其实际尺寸惰性创建检测器，尺寸变化时重建。

## Face Mesh（468 点关键点）

ONNX Runtime 实际读取的元数据：

- 输入：`input`，`float32`，`[-1, 3, 192, 192]`，NCHW
- 输出：
  - `landmarks`，`float32`，`[-1, 468, 3]`
  - `score`，`float32`，`[-1, 1]`（原始 logit；本程序不使用该输出）

预处理（与模型导出方的推理脚本一致）：

1. 以检测框中心为基准、取框长边的 1.4 倍作为正方形边长（即每侧外扩 20%），通过仿射变换直接重采样到 192×192，越界区域填 0；
2. BGR → RGB；
3. `float32` 归一化到 `[0, 1]`；
4. 排布为 NCHW。

输出 `landmarks` 的 `x, y, z` 单位为 192×192 裁剪图像素。程序只在裁剪坐标系内计算 EAR（尺度无关），不映射回原图；`--debug-frames` 会用仿射逆变换把关键点画回原始帧。

关键点编号采用 MediaPipe 规范的 468 点顺序。眼睛 6 点轮廓（顺序：p1 外眼角、p2/p3 上睑、p4 内眼角、p5/p6 下睑）：

| 眼睛 | MediaPipe 索引 | 说明 |
|---|---|---|
| 右眼（画面左侧） | 33, 160, 158, 133, 153, 144 | `33` 外眼角，`133` 内眼角 |
| 左眼（画面右侧） | 362, 385, 387, 263, 373, 380 | `362` 外眼角，`263` 内眼角 |

EAR 公式：

```
EAR = (|p2-p6| + |p3-p5|) / (2 * |p1-p4|)
```

单眼有效时使用单眼，双眼有效时取平均；`EAR` 不在 `(0, 0.6)` 内视为无效帧。

## 校验方式

```bash
dotnet run --project src/FatigueGuard.Cli -- --help
dotnet run --project src/FatigueGuard.Cli -- --video samples/face_sample.mp4 --calibrate 0 --verbose
```

`--verbose` 会把 ONNX Runtime 读取到的 FaceMesh 输入/输出名称与形状打印到 stderr。检测器契约测试（`tests/FatigueGuard.Core.Tests/DetectorContractTests.cs`）会断言上述张量形状与关键点索引。

## 许可证提示

YuNet 与 MediaPipe Face Mesh 均为 Apache-2.0。FaceMesh 的 ONNX 由 `yakhyo/mediapipe-face-mesh-onnx`（Apache-2.0）从其 PyTorch 导出重建，权重来自 Google MediaPipe（Apache-2.0）。再分发时请保留本说明与 `THIRD_PARTY_NOTICES.md`。
