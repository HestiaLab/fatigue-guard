# FatigueGuard

本地离线的摄像头疲劳监测命令行工具。它通过摄像头（或离线视频）检测眨眼与闭眼行为，计算 PERCLOS、眨眼率等指标，在终端用颜色提示疲劳状态；严重疲劳时输出红色告警。

- 全程离线：不联网、不上传、不录像、不保存人脸；
- 轻量终端工具：正常时安静显示状态，疲劳时醒目提醒；
- 仅作工作节奏提醒，**不构成医疗或健康诊断建议**。

## 环境要求

- Windows 10/11 x64
- .NET 8 SDK（构建/测试）；运行也可使用自包含发布产物
- 摄像头（实时模式）或离线视频文件
- 模型文件已随仓库提交在 `models/`，无需下载

主要依赖：OpenCvSharp4（Apache-2.0）、ONNX Runtime（MIT）、YuNet（Apache-2.0）、MediaPipe Face Mesh ONNX（Apache-2.0）。详见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) 与 [models/README.md](models/README.md)。

## 安装（MSI）

最终用户可从 Releases 下载 `FatigueGuard-<version>-win-x64.msi` 后双击安装：

- 按用户安装，**不需要管理员权限**；默认目录为 `%LOCALAPPDATA%\Programs\FatigueGuard`，可在向导中修改；
- 向导依次为许可协议（Apache-2.0）、安装目录、安装进度与完成页；安装向导为简体中文（WiX 6 内置文案）；
- 安装内容为 `FatigueGuard.exe`、`models/`、`README.md`、`LICENSE`、`NOTICE`、`THIRD_PARTY_NOTICES.md` 与 `licenses/`；
- 开始菜单创建 `FatigueGuard` 快捷方式，默认参数启动（摄像头 0、无预览）；需要指定摄像头索引、预览或导出指标时，请在终端中运行（见下文“运行”）；
- 卸载：设置 → 应用 → 已安装的应用 → `FatigueGuard` → 卸载，或运行 `msiexec /x FatigueGuard-<version>-win-x64.msi`；
- 升级：直接安装新版本 MSI 即可，安装程序会自动卸载旧版本；
- 安装包为 x64，要求 64 位 Windows 10/11。

安装版与解压版功能完全一致；如果只想临时试用，也可以直接解压 zip 运行。安装包由 `scripts/build-installer.ps1` 构建，具体见“发布”。

## 构建与测试

```bash
dotnet build FatigueGuard.sln -c Release
dotnet test FatigueGuard.sln -c Release
```

CLI 项目会在构建和发布时自动把仓库根目录的 `models/` 复制到输出目录（`bin/<配置>/net8.0/models`），因此在 Visual Studio 中直接 F5 调试、或从 `bin` 目录启动都能找到模型，无需手工复制。

测试不访问摄像头、不访问网络。检测器契约测试会在模型存在时运行（模型随仓库提交）。

## 运行

```bash
# 摄像头实时模式（默认校准 20 秒）
dotnet run --project src/FatigueGuard.Cli -c Release -- --camera 0

# 摄像头 + 终端实时预览
dotnet run --project src/FatigueGuard.Cli -c Release -- --camera 0 --preview

# 离线视频模式（确定性回放，不按实时节奏休眠）
dotnet run --project src/FatigueGuard.Cli -c Release -- --video samples/face_sample.mp4

# 跳过校准（使用保守默认值），运行 60 秒
dotnet run --project src/FatigueGuard.Cli -c Release -- --camera 0 --calibrate 0 --duration 60

# 输出逐秒指标 CSV 并导出调试帧
dotnet run --project src/FatigueGuard.Cli -c Release -- --video samples/face_sample.mp4 --metrics-csv artifacts/metrics.csv --debug-frames artifacts/debug
```

### 命令行参数

```
FatigueGuard [options]
  --camera <index>       摄像头索引，默认 0；与 --video 互斥
  --video <path>         离线视频文件（用于无摄像头验证与可重复测试）
  --config <path>        JSON 配置文件
  --calibrate <seconds>  校准时长，默认 20；0 表示跳过校准（用保守默认阈值）
  --duration <seconds>   运行指定时长后正常退出；视频模式默认播放到结尾
  --refresh-hz <n>       状态行刷新频率，默认 5
  --no-color             禁用 ANSI 颜色（另需尊重 NO_COLOR 环境变量）
  --lang <zh|en>         界面语言，默认 zh
  --metrics-csv <path>   逐秒输出指标 CSV（仅数值，不含图像）
  --debug-frames <dir>   导出带框和关键点的 PNG（仅开发调试，默认关闭）
  --quiet                只输出状态事件，不输出实时状态行
  --preview              在终端底部实时预览摄像头画面（ANSI 半块字符，默认关闭）
  --no-mirror            预览不做水平镜像（摄像头源本身已镜像时使用）
  --verbose              诊断日志写 stderr
  --help / --version
```

- 标准输出只放状态行与状态事件；诊断信息与 OpenCV 原生警告写 stderr；
- 输出被重定向（非 TTY）时自动关闭颜色与 `\r` 状态行，预览也会禁用；
- `NO_COLOR` 环境变量存在即禁用颜色；`--no-color` 优先。

### 退出码

| 退出码 | 含义 |
|---|---|
| 0 | 正常结束（视频读完 / 到达 `--duration`） |
| 2 | 参数错误 |
| 3 | 摄像头打开/读取失败，或视频文件无法打开 |
| 4 | 模型缺失 / SHA256 不符 / 张量契约不符 |
| 5 | 配置非法 |
| 6 | 其他运行时错误 |
| 130 | Ctrl+C |

### 状态与事件

状态行只在 TTY 下用 `\r` 原地刷新：

```
00:42.6  正常       PERCLOS 0.04  眨眼 16/min  EAR 0.27  人脸 1  29.9fps
```

状态切换时才输出事件行（新起一行，随后恢复状态行）：

- 黄色：`00:51.2  疲劳预警：PERCLOS 0.17（阈值 0.15，60 秒窗口）`
- 红色加粗：`01:03.2  严重疲劳：检测到微睡眠 1.4 秒`
- 绿色：`01:30.8  恢复：指标已回落至正常范围`
- 灰色：`00:05.0  未检测到人脸`、`画面质量低`

校准期间状态行显示倒计时（如 `校准中 8/20s`）；当有效帧占比不足、校准已超过计划时长仍在延长时显示 `（延长）`。校准完成/数据不足会输出事件行，数据不足时状态行持续标注“校准数据不足”。

### 实时预览

加 `--preview` 会在终端底部用 ANSI 半块字符显示降采样的摄像头画面：人脸框为绿色，右眼轮廓蓝色、左眼红色，下方是紧凑状态行（预览模式下用缩写）：

```
00:42.6  正常  P 0.04  B 16/min  EAR 0.27  F 1  29.9fps
```

- `P` = PERCLOS，`B` = 眨眼率，`F` = 检测到的人脸数；校准倒计时后的 `*` 表示校准因有效帧不足而延长。
- 预览默认水平镜像（自拍视角，人物移动方向与自然方向一致）；如果摄像头源本身已镜像，可用 `--no-mirror` 关闭。镜像只作用于预览，检测与 EAR 不受影响；`--debug-frames` 始终保留原始方向。
- 预览是终端文本渲染，不是 GUI；帧只在内存中处理，不录像、不保存。
- 需要交互式终端 + ANSI/VT，窗口至少 90×12；`--quiet`、`--no-color`、`NO_COLOR`、输出重定向时自动禁用，并在 stderr 说明原因。
- 终端过窄、字体不支持 `▀`、或 VS 调试控制台不接受 ANSI 时，可改用 Windows Terminal 或 `--debug-frames`。
- 视频模式仍按帧号/fps 确定性快速回放（不实时休眠），因此预览主要适合摄像头模式；刷新率跟随 `--refresh-hz`。
- 运行期间调整终端窗口大小可能导致预览错位，建议保持窗口尺寸不变。

### 中文与非 ASCII 路径

OpenCV 的原生 DNN 在 Windows 上按系统 ANSI 代码页（简体中文默认为 GBK）解释路径字节，直接用中文目录加载 ONNX 会失败，典型报错是把路径显示成乱码的 `Can't read ONNX file: C:\Users\...\00_鏀朵欢绠...`。

程序启动校验 SHA256 之后会检查模型路径：如果包含非 ASCII 字符，就把模型副本放进纯 ASCII 的临时目录（依次尝试 `%TEMP%\FatigueGuard\`、`%PROGRAMDATA%\FatigueGuard\`、`%SystemRoot%\Temp\`）并从那里加载；进程退出时删除这次运行的临时子目录，原始 `models/` 文件不会被修改或移动。找不到任何可写的 ASCII 目录时以退出码 4 退出并提示改用纯英文路径。加 `--verbose` 时会在 stderr 打印“模型路径包含非 ASCII 字符，已复制到 ASCII 临时目录加载。”

注意：临时副本在进程退出前会占用与模型等量的磁盘空间（两个模型合计约 2.7 MB）。视频文件、`--config`、`--metrics-csv`、`--debug-frames` 的路径由 .NET 处理，中文路径正常可用。

## 配置

命令行 > 配置文件 > 默认值。示例（即默认值）：

```json
{
  "camera": { "index": 0, "width": 640, "height": 480, "fps": 30 },
  "detection": { "minFaceConfidence": 0.6, "minFaceSize": 80, "detectEveryNthFrame": 1 },
  "calibration": { "seconds": 20, "maxSeconds": 60, "minValidRatio": 0.7, "baselinePercentile": 75, "baselineOverride": null },
  "blink": { "closedThresholdRatio": 0.60, "closedThresholdMin": 0.08, "closedThresholdMax": 0.16,
             "minClosedMs": 50, "blinkMaxMs": 500, "microsleepMs": 1200, "maxGapMs": 500 },
  "metrics": { "windowSeconds": 60, "minValidSeconds": 30 },
  "fatigue": { "perclosWarn": 0.15, "perclosCritical": 0.25, "perclosExitWarn": 0.10, "perclosExitCritical": 0.15,
               "blinkRateWarn": 30, "blinkRateExit": 24, "longClosuresWarn": 3,
               "warnDwellSeconds": 5, "exitDwellSeconds": 15, "criticalDwellSeconds": 5,
               "criticalRepeatSeconds": 15, "microsleepLookbackSeconds": 60 },
  "display": { "refreshHz": 5, "language": "zh", "color": "auto" }
}
```

| 字段 | 说明 |
|---|---|
| `camera.width/height/fps` | 向摄像头请求的参数；设备可能返回其他分辨率，实际值见 `--verbose` |
| `detection.detectEveryNthFrame` | 每隔 N 帧做一次人脸检测；关键点与 EAR 仍逐帧计算 |
| `calibration.baselineOverride` | 显式指定基线，跳过校准；必须位于 `(0, 0.6)` |
| `blink.closedThresholdRatio/Min/Max` | 闭眼阈值 = `clamp(ratio × 基线, Min, Max)` |
| `blink.minClosedMs` | 低于阈值持续多久才记为闭眼段 |
| `blink.blinkMaxMs` | 眨眼 / 长闭眼分界 |
| `blink.microsleepMs` | 微睡眠阈值（达到即立即升级） |
| `blink.maxGapMs` | 有效帧间隔上限；超过则中断闭眼段且不累计该间隔 |
| `metrics.minValidSeconds` | PERCLOS 与眨眼率的最小有效监测时长 |
| `fatigue.*` | 状态机阈值与时间迟滞 |
| `display.color` | `auto` / `on` / `off`；重定向时始终关闭 ANSI |

非法值（负数、阈值倒挂、窗口小于最小有效时长等）会在启动时以退出码 5 退出，并指出字段名。

## 算法与指标

### 处理流程

1. 每帧用 YuNet 检测人脸（取置信度 ≥ 0.6、宽 ≥ 80px 的最大人脸；多人时标注人数并跟踪最大人脸）；
2. 以人脸框中心为基准、框长边 1.4 倍为边长（即每侧外扩 20%）裁剪为正方形，仿射重采样到 192×192；
3. Face Mesh 输出 468 个关键点，在裁剪坐标系内按 33/160/158/133/153/144（右眼）与 362/385/387/263/373/380（左眼）计算 6 点 EAR；
4. 单眼有效时用单眼，双眼有效取平均；`EAR ∈ (0, 0.6)` 之外视为无效帧；
5. 平滑：3 帧中值滤波 → EMA（α=0.6）；
6. 校准（默认 20s）：收集有效帧，基线取平滑 EAR 的 75 百分位；有效帧占比 < 70% 时延长到 60s，仍不足则回退到基线 0.28 / 阈值 0.16，并显式提示；
7. 闭眼段：低于闭眼阈值持续 ≥ 50ms 开始记录，连续 2 帧回到阈值以上结束；50–500ms 为眨眼，500ms–1.2s 为长闭眼，≥ 1.2s 为微睡眠。帧间隔 > 500ms 会中断当前闭眼段；
8. 滚动窗口统计并驱动状态机（2Hz 评估，按时间迟滞）。

### 指标

- **EAR**：眼睛纵横比，越大越睁眼；平滑后的值显示在状态行。
- **PERCLOS60**：60 秒窗口内闭眼累计时长 / 有效监测时长。有效监测时长 = 有人脸且 EAR 有效的帧间隔之和（超过 500ms 的间隔不计）。有效时长 < 30 秒时标记“数据不足”，不据此报警。
- **眨眼率**：60 秒窗口内眨眼数 × (60 / 有效秒数)；有效秒数 < 30 不评估。
- **长闭眼**：持续 500ms–1.2s 的闭眼段次数（60 秒/120 秒窗口分别用于进入/退出判定）。
- **微睡眠**：持续 ≥ 1.2s 的闭眼段；立即升级为严重疲劳，并在严重疲劳期间每 15 秒重复红色提醒。
- **画面质量低**：最近 10 秒有效帧占比 < 50% 时进入该显示状态；暂停状态升级（允许降级）。

### 状态机

| 当前 | 条件 | 新状态 |
|---|---|---|
| CALIBRATING | 校准完成且有效率 ≥ 70%（否则延长/回退） | OK |
| OK | PERCLOS60 ≥ 0.15，或眨眼率 ≥ 30/min，或 60s 内长闭眼 ≥ 3 次；持续 5s | WARNING |
| WARNING | 出现微睡眠（立即），或 PERCLOS60 ≥ 0.25 持续 5s | CRITICAL |
| WARNING | PERCLOS60 ≤ 0.10 且眨眼率 ≤ 24/min 且 120s 内无长闭眼；持续 15s | OK |
| CRITICAL | 120s 内无微睡眠且 PERCLOS60 ≤ 0.15；持续 15s | WARNING |

人脸丢失 > 2 秒时显示 `NO_FACE`，内部状态保持；窗口按“有效时长”规则自然排除该时段。

### 指标 CSV

`--metrics-csv` 逐秒写一行（UTF-8、无 BOM、`\n` 换行、InvariantCulture）：

```
time_s,face_count,ear,ear_baseline,ear_threshold,perclos60,blink_rate_per_min,valid_seconds,
blinks_60,long_closures_60,microsleeps_60,quality_valid_ratio_10s,state
```

`state` 为数值：0=CALIBRATING，1=OK，2=WARNING，3=CRITICAL，4=NO_FACE，5=QUALITY_LOW。空字段表示该时刻无可用数据。

## 确定性

视频模式的流时间严格取 `帧号 / fps`，不按实时节奏休眠。同一视频、同一配置连续运行两次，stdout 与 metrics CSV 逐字节一致（已由 CLI 进程测试验证）。

摄像头模式使用单调 `Stopwatch`（从首帧开始计时），fps 仅用于展示（按实际帧间隔平滑估计）。

## 调参建议

1. 先跑一次并加 `--metrics-csv`，观察 `ear`、`ear_threshold`、`perclos60` 与实际用眼情况；
2. 个体差异大：确保校准期间自然睁眼；若闭眼时 EAR 不够低，可适度提高 `blink.closedThresholdRatio` 或 `blink.closedThresholdMax`（注意别把正常眨眼误判为长闭眼）；
3. 若正常使用就报警，可提高 `fatigue.perclosWarn` / `blinkRateWarn` 或延长 `warnDwellSeconds`；
4. 若报警太迟钝，可降低上述阈值或 `criticalDwellSeconds`；微睡眠阈值 `blink.microsleepMs` 不建议低于 1000ms；
5. 低帧率或掉帧环境可适当提高 `blink.maxGapMs`，但会降低时间分辨率。

**闭眼 1.5 秒能触发微睡眠，但日常眨眼计数很少？** 这通常是眨眼幅度较浅（眼镜、虚拟摄像头、画质偏软）且默认闭眼阈值 0.16 偏低，短暂眨眼经 3 帧中值 + EMA 平滑后不足以形成 ≥50ms 的闭眼段。可先使用随仓库提供的灵敏度配置：

```bash
# 从仓库根目录运行；如果可用摄像头不是索引 0，请替换 --camera 后的数字
dotnet run --project src/FatigueGuard.Cli -c Release -- --camera 0 --preview --config samples/blink-tuning.json
```

`samples/blink-tuning.json` 将 `closedThresholdRatio` 提高到 0.75、`closedThresholdMax` 提高到 0.22、`minClosedMs` 降到 40ms（基线 0.28 时阈值约为 0.21）。连续眨眼 5–10 次观察 `B/min`；如果仍偏低，可把 `closedThresholdMax` 提到 0.23、`minClosedMs` 降到 30；如果出现误报的长闭眼/微睡眠，把阈值调回 0.20 左右。该阈值同时影响 PERCLOS 与长闭眼统计，调参时需一起观察。

## 已知限制

- 强逆光、严重遮挡、快速转头、侧脸或距离过远会导致无效帧；工具显示“画面质量低”/“未检测到人脸”，而不是硬判疲劳；
- 单人假设：多人入镜时跟踪面积最大的人脸，状态行显示人数（如 `人脸 2`）；
- 眼镜反光、深色镜框、口罩、刘海遮挡可能影响关键点稳定性；
- 阈值是启发式默认值，个体差异大；建议先校准，并用 `--metrics-csv` 观察后再调整；
- 摄像头后端优先 MSMF、失败回退 DSHOW；设备可能不支持 640×480@30，实际分辨率/帧率可能不同（`--verbose` 可见）；
- 本工具不防照片/视频欺骗：照片或屏幕上的脸也可能骗过检测；
- 不构成医疗建议，不用于诊断或治疗任何疾病。

## 隐私

- 无任何网络调用（可全局搜索 `HttpClient`/`Socket` 验证）；
- 摄像头帧只在内存中处理，不录像、不截图、不保存人脸模板或生物特征；
- 只有显式指定 `--metrics-csv` 或 `--debug-frames` 时才会写文件：
  - CSV 仅数值指标；
  - 调试 PNG 含画面，仅供开发排查，默认关闭，请勿在敏感环境开启；
- 模型从本地 `models/` 加载；程序不访问网络。

## 模型与许可

模型文件、来源 URL、下载日期、SHA256、张量名称/形状与关键点编号约定见 [models/README.md](models/README.md)。程序启动时校验 SHA256，不匹配则退出码 4。

本项目代码采用 Apache-2.0，见 [LICENSE](LICENSE) 与 [NOTICE](NOTICE)。第三方依赖与模型的许可证见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)，完整许可证文本在 [licenses/](licenses/)。

## 开发与测试

```
FatigueGuard.sln
src/FatigueGuard.Core/      帧源、检测器、EAR、校准、窗口、状态机、配置（无 Console 依赖）
src/FatigueGuard.Cli/       参数解析、终端渲染、组合根、信号处理
tests/FatigueGuard.Core.Tests/
tests/FatigueGuard.Cli.Tests/
installer/                  WiX 6 MSI 安装包（按用户安装，不在解决方案中）
scripts/                    构建脚本（MSI）
models/                     ONNX 模型与契约说明
samples/                    可选的本地媒体样本（大文件已 gitignore）
```

本机专用的 `Properties/launchSettings.json` 不提交；需要固定调试参数时，复制 `src/FatigueGuard.Cli/Properties/launchSettings.example.json` 为 `launchSettings.json` 并按需修改。

仓库包含 `.github/workflows/ci.yml`：在 Windows 上执行 Release 构建与全量测试；媒体测试在没有 `samples/face_sample.mp4` 时会按设计显示 SKIPPED。

- Core 单元测试使用合成 EAR 序列，覆盖 EAR 数学、平滑、校准、闭眼分段、滚动窗口、状态机逐行转换、配置校验与确定性；
- 检测器契约测试加载真实模型，断言输入/输出张量与关键点索引；
- CLI 进程测试覆盖帮助/版本/参数/退出码/无 ANSI/无 `\r`/确定性/调试帧/元数据打印。

### 媒体测试（可选）

自动化测试不依赖真实摄像头。需要真实人脸视频的测试标记为 `Category=Media`，当 `samples/face_sample.mp4` 不存在时会显示明确的 SKIPPED 原因。

提供任意清晰的正脸视频（建议 640×480、30fps、包含眨眼/闭眼片段），保存为 `samples/face_sample.mp4`，然后运行：

```bash
dotnet test tests/FatigueGuard.Cli.Tests -c Release --filter Category=Media
```

仓库中的 `samples/verification-config.json` 仅用于合成样本验证：合成图像的“闭眼”关键点仍保留部分睑裂，默认阈值可能不触发；该配置放宽了闭眼阈值上限，用于验证微睡眠事件链路。真实眨眼请先使用默认配置与个体校准。

## 手动验收清单

在真实摄像头环境建议按以下步骤检查（README 即验收步骤记录）：

1. 启动后校准倒计时正常；完成时事件行显示基线 EAR 与闭眼阈值；
2. 正常用眼约 3 分钟无告警；
3. 闭眼约 1.5 秒 → 1.5 秒内出现红色“严重疲劳”事件行；
4. 遮住摄像头 2 秒 → 显示“未检测到人脸”，无误报红色；
5. 两人入镜 → 跟踪最大人脸，状态行显示 `人脸 2`；
6. 按 Ctrl+C → 颜色复原、状态行清理、摄像头释放、退出码 130；再次启动可正常打开设备；
7. 输出重定向到文件 → 无 ANSI、无 `\r`；
8. 加 `--preview` → 预览正常刷新，人脸框与眼部轮廓可见；`--no-color`/重定向/`--quiet` 时按说明自动禁用。

自动化侧已验证：视频模式确定性、状态机全表转换、颜色/状态行渲染、无 ANSI/无 `\r`、预览渲染与禁用回退、模型元数据与 SHA256、Ctrl+Break 取消路径（与 Ctrl+C 共用 `CancelKeyPress` 处理）退出码 130；MSI 的静默安装/卸载、安装目录、开始菜单快捷方式与 ARP 信息已在本机验证。真实 Ctrl+C 的设备释放、多人场景与预览视觉效果需在实机按上述步骤确认。

安装包另需人工确认：对话框文案与许可协议页显示正常、自定义安装目录生效、覆盖安装升级后快捷方式仍可用。

## 发布

```bash
dotnet publish src/FatigueGuard.Cli -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

产物在 `src/FatigueGuard.Cli/bin/Release/net8.0/win-x64/publish/`。发布时会自动把 `models/` 复制到 publish 目录，因此 `FatigueGuard.exe` 可直接运行；模型仍以独立的 `models/` 文件夹随 exe 分发，不会打进单文件可执行程序。如果你手动移动 exe，请连同 `models/` 目录一起移动。

### 构建 MSI

```powershell
pwsh -File scripts/build-installer.ps1
```

脚本会先发布 win-x64 自包含单文件到 `artifacts/installer/staging`（去掉 pdb，补齐文档与 `licenses/`），再从 `src/FatigueGuard.Cli/FatigueGuard.Cli.csproj` 读取 `<Version>` 调用 WiX 6 构建，最终输出 `artifacts/release/FatigueGuard-<version>-win-x64.msi`。首次构建会从 NuGet 还原 `WixToolset.Sdk` 与 `WixToolset.UI.wixext`。

- MSI 项目（`installer/FatigueGuard.Installer.wixproj`）刻意不加入解决方案，避免 `dotnet build FatigueGuard.sln` 在缺少发布产物时失败；
- 安装作用域为 per-user，装在 `%LOCALAPPDATA%\Programs\FatigueGuard`，快捷方式写入当前用户的开始菜单，卸载信息出现在“设置 → 应用”；
- 包内字符串使用代码页 936（简体中文）；`installer/zh-CN.wxl` 声明 zh-CN 区域设置，使向导选用 WiX 6 内置的简体中文文案，ARP 说明与快捷方式描述同为中文；
- 修改版本号只需改 CLI 项目的 `<Version>`：程序、MSI 与文件名会保持一致。

对外分发二进制时请同时附带 `LICENSE`、`NOTICE`、`THIRD_PARTY_NOTICES.md` 与 `licenses/`（其中 FFmpeg 为 LGPL-2.1；自包含发布还包含 .NET 运行时的许可证/第三方声明），不要把 176MB 的 publish 输出提交到仓库。
