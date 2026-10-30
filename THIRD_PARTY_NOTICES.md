# Third-Party Notices

FatigueGuard 使用以下第三方组件与模型。各组件版权归其各自作者所有，使用需遵守对应许可证。

## 运行依赖

### OpenCvSharp4 / OpenCvSharp4.runtime.win

- 项目：https://github.com/shimat/opencvsharp
- 许可证：Apache-2.0
- 版权：Copyright (c) shimat and contributors

### OpenCV

- 项目：https://opencv.org / https://github.com/opencv/opencv
- 许可证：Apache-2.0
- 版权：Copyright (C) OpenCV Foundation and contributors

### FFmpeg（经 OpenCV 视频 I/O 引入）

- 文件：`opencv_videoio_ffmpeg4130_64.dll`（由 `OpenCvSharp4.runtime.win` 提供）
- 项目：https://ffmpeg.org
- 许可证：LGPL-2.1（OpenCvSharp runtime 说明中注明其完整包包含 LGPL v2.1 的 FFmpeg）
- 版权：FFmpeg developers
- 说明：仅上传源码时该二进制由 NuGet 还原；若分发自包含发布产物，请随附 `licenses/LGPL-2.1.txt`，并按 LGPL 要求提供源码获取/重链接方式。

### Microsoft.ML.OnnxRuntime (ONNX Runtime)

- 项目：https://github.com/microsoft/onnxruntime
- 许可证：MIT
- 版权：Copyright (c) Microsoft Corporation

### .NET 8 Runtime（仅自包含发布）

- 项目：https://github.com/dotnet/runtime
- 许可证：MIT
- 说明：自包含单文件发布包含 .NET 运行时及其第三方组件；发布二进制时请附带 .NET 的 LICENSE 与 ThirdPartyNotices。

## 模型

### YuNet 人脸检测模型

- 文件：`models/face_detection_yunet_2023mar.onnx`
- 来源：OpenCV Zoo，https://github.com/opencv/opencv_zoo/tree/main/models/face_detection_yunet
- 许可证：Apache-2.0
- 下载日期：2026-10-30
- SHA256：`8f2383e4dd3cfbb4553ea8718107fc0423210dc964f9f4280604804ed2552fa4`

### MediaPipe Face Mesh 关键点模型（ONNX 导出）

- 文件：`models/face_mesh_Nx3x192x192.onnx`
- 来源：https://github.com/yakhyo/mediapipe-face-mesh-onnx （Apache-2.0）
- 上游模型：Google MediaPipe Face Mesh，https://github.com/google-ai-edge/mediapipe （Apache-2.0）
- 许可证：Apache-2.0
- 下载日期：2026-10-30
- SHA256：`3ca77cf59c18e4da0eccb46695bf604683fa564253e3385892981a5c274fb10f`

## 测试与构建依赖

### xUnit.net

- 项目：https://github.com/xunit/xunit
- 许可证：Apache-2.0
- 版权：Copyright (c) .NET Foundation and contributors

### Microsoft.NET.Test.Sdk

- 项目：https://github.com/microsoft/vstest
- 许可证：MIT
- 版权：Copyright (c) Microsoft Corporation

## 完整许可证文本

再分发本项目及其模型时，请同时保留以下文本：

- `licenses/Apache-2.0.txt`：Apache License 2.0 全文。
- `licenses/MIT.txt`：MIT 许可证全文（Microsoft Corporation）。
- `licenses/LGPL-2.1.txt`：GNU Lesser General Public License 2.1 全文（FFmpeg）。
- `licenses/ONNX-Runtime-ThirdPartyNotices.txt`：ONNX Runtime 1.31.1 的第三方声明。

适用 Apache-2.0 的组件包括 OpenCvSharp4、OpenCV、YuNet、MediaPipe Face Mesh（含 ONNX 导出）与 xUnit；适用 MIT 的组件包括 ONNX Runtime 与 Microsoft.NET.Test.Sdk；FFmpeg（经 OpenCV 视频 I/O 引入）适用 LGPL-2.1。模型权重再分发时请保留本文件与上述许可证文本。
