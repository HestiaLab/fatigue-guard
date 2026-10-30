# samples

- `verification-config.json`：仅用于合成人脸样本的验证配置（放宽闭眼阈值上限）。真实使用请用默认配置并完成个体校准。
- `blink-tuning.json`：针对浅眨眼（眼镜、虚拟摄像头、低画质）的灵敏度配置示例：`closedThresholdRatio=0.75`、`closedThresholdMax=0.22`、`minClosedMs=40`。若仍漏检可继续提高上限/降低最小时长；若误报则回调。
- `face_sample.mp4`：可选的本地人脸视频样本，**不随仓库提交**（`.gitignore` 排除）。提供方法见仓库根目录 `README.md` 的“媒体测试（可选）”一节。

自动化测试在样本缺失时会显示明确的 SKIPPED 原因，不会静默跳过。
