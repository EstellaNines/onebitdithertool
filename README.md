# OneBitDitheringTool

把任意图片转换成 1-bit（纯黑白）抖动图的桌面工具。基于 .NET 10 与跨平台界面框架 Avalonia 12，**无需另装 didder、ImageMagick 或 LÖVE**。目标平台是 Windows、macOS 与 Linux，但目前只在 Windows 11 上验证过，其余两个平台尚未实测。界面为简体中文。

本项目由 [timheigames/onebitdithertool](https://github.com/timheigames/onebitdithertool)（Lua / LÖVE，调用 didder 与 ImageMagick 命令行）重写而来，功能与操作习惯与原版对齐；抖动算法按 [didder](https://github.com/makew0rld/didder) 的行为实现，并用它的输出做逐像素验证。

## 功能

- **四类抖动算法**
  - Bayer 矩阵：2×2、3×3、3×5、5×3、4×4、8×8、16×16、32×32、64×64，另可自定义宽高（2、4、8、16、32、64 任意组合）
  - 有序抖动矩阵：15 种聚点矩阵（ClusteredDot4x4、ClusteredDotDiagonal8x8、Vertical5x3 等）
  - 误差扩散矩阵：Simple2D、FloydSteinberg、FalseFloydSteinberg、JarvisJudiceNinke、Atkinson、Stucki、Burkes、Sierra、TwoRowSierra、SierraLite、StevenPigeon，可选蛇形扫描
  - 随机噪声：可调噪声范围
- **实时预览**：拖动滑杆即刻重算；「强度」「亮度」「对比度」三个滑杆，「缩放比例」可在抖动前先缩小图片
- **自定义通道权重**（原版的 Split Channels）：抖动前先自定义 R、G、B 的灰度权重
- **批量处理**：一次载入多张图片或整个文件夹，用同一套参数批量输出
- **预览操作**：左键拖动平移，滚轮整数倍缩放（以指针为中心），「显示原图」对比原图，左右方向键切换图片
- **真 1-bit 输出**：不含透明度的图保存为 1 位深的索引 PNG；含透明度的图保存为 8 位 RGBA PNG（1 位深表达不了半透明）

## 运行

需要 [.NET 10 SDK](https://dotnet.microsoft.com/download)。

```
dotnet run --project src/OneBitDitheringTool.App
```

## 发布（导出独立 exe）

下面的命令导出一个**自带 .NET 运行时的单文件 exe**（Windows x64），目标机器无需安装 .NET：

```
dotnet publish src/OneBitDitheringTool.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -p:DebugSymbols=false -p:PublishReferencesDocumentationFiles=false -p:AllowedReferenceRelatedFileExtensions=.allowedextension -o dist
```

产物是 `dist/OneBitDitheringTool.exe`，约 100 MB（其中大部分是运行时）。几点说明：

- 发布后 `dist/` 里还会多出 `libSkiaSharp.pdb`、`libHarfBuzzSharp.pdb` 两个原生库的调试符号（合计约 100 MB），运行用不到，可以直接删除。
- 首次启动会把原生库解压到临时目录，所以第一次会慢一两秒。
- 该 exe 没有代码签名，在别的电脑上首次运行时，Windows SmartScreen 可能提示「未知发布者」。
- 其他平台把 `win-x64` 换成 `osx-arm64`、`linux-x64` 等同理，但尚未实测。
- 没有启用裁剪（`PublishTrimmed`）：Avalonia 依赖反射，裁剪容易引出只在运行时才出现的问题。

## 使用

1. 点击「打开图片…」（可多选），或把图片、文件夹直接拖进窗口。支持 `.png`、`.jpg`、`.jpeg`。拖入文件夹时只扫描第一层，按文件名排序。
2. 调整右侧参数，预览随之更新。点击滑杆的名称可复位为默认值。
3. 载入多张图片后，用工具栏的 `<` `>` 按钮或键盘左右方向键切换。方向键在滑杆或下拉框获得焦点时归它们使用，点一下预览区即可把焦点还给窗口。
4. 点击「全部保存到输出文件夹…」选择输出文件夹，所有图片按同一套参数处理并保存为 `原文件名.png`。

保存时的命名规则：**绝不覆盖任何输入图片**。若输出文件夹就是原图所在的文件夹，同名文件会自动改名为 `名称 (2).png`；同一批里重名的图片也是如此。其余已存在的同名文件会被覆盖，因此重新导出时能刷新上一次的结果。

## 与原版及 didder 的差异

算法行为与 didder 一致，下列各处是**有意的偏离**：

| 项目 | 原版 / didder | 本工具 |
|---|---|---|
| 强度为 0 | didder 把 0 当成「未设置」而按 1 处理 | 按数学含义处理：0 即不抖动，以线性亮度 0.5 为界的硬阈值 |
| Burkes 核 | 原版 Windows 包内置的旧版 dither 系数有误（首行为 8/32、8/32） | 采用修正后的系数（8/32、4/32），与新版 didder 一致 |
| 随机抖动 | 每次以时间为种子，预览与保存的结果不同 | 固定种子，预览与保存完全一致 |
| Bayer 3×5 | dither 库的 3×5 矩阵首行写作 `{0, 14, 16}`，16 超出 0 到 14 的取值范围，应是 6 的笔误 | 采用 5×3 矩阵的转置 `{0, 14, 6}` |
| ClusteredDotDiagonal6x6 | 一个格子取 8，使 8 出现三次、7 只出现一次，破坏了对角矩阵的对称结构 | 该格改为 7 |
| ClusteredDotDiagonal16x16 | 87 出现四次、88 缺失 | 两个镜像格改为 88（依数值规律推断，不像 6×6 那样由结构唯一确定） |
| 输出格式 | 两色像素存成 24 位 RGB 或 32 位 RGBA 的 PNG | 无透明度时存真 1 位深索引 PNG |
| 界面语言 | 英文 | 简体中文（Bayer、FloydSteinberg 等算法专有名称保留英文） |
| 缩放比例取整 | 对宽度向下取整，浮点误差会少一个像素；取整为 0 时悄悄不缩放 | 抵消浮点误差，且至少保留 1 像素 |

已知的局限：

- **JPEG 输入无法保证与 didder 逐像素一致**：JPEG 解码器与 didder 所用的 Go 标准库实现不同，解码出的灰度可能差 1；PNG 输入可以逐像素一致。
- **自定义通道权重（Split Channels）未与 ImageMagick 对照**：原版靠 ImageMagick 混色，本机无法对照，取整可能差一个灰度级。
- 输出只有 PNG，输入只有 PNG、JPG。

## 开发

```
src/OneBitDitheringTool.Core/    抖动算法与 PNG 编码，无界面依赖
src/OneBitDitheringTool.App/     Avalonia 界面
tests/OneBitDitheringTool.Core.Tests/    算法测试，含与 didder 的逐像素对照
tests/OneBitDitheringTool.App.Tests/     界面测试（无头渲染）
```

处理流程：缩放（Box 滤波）→ 灰度化 → 对比度 → 亮度 → sRGB 转线性光 → 抖动 → 1-bit。抖动在线性光空间进行，所以中间调的白点占比等于它的线性亮度（sRGB 中灰 128 约占 22%），而不是直觉里的 50%。

### 运行测试

```
dotnet test
```

界面测试用 Avalonia 的无头模式，无需显示器；它们会核对预览里显示的像素与 Core 算出的结果逐字节相同。

与 didder 的逐像素对照需要一个 didder 可执行文件，它只作为黑盒参照机被调用，不随本仓库分发。未配置时，这部分测试会被自动跳过，其余测试照常运行：

```
git clone https://github.com/makew0rld/didder
cd didder
go build -o didder.exe .
set DIDDER_PATH=C:\path\to\didder.exe        (Windows cmd)
export DIDDER_PATH=/path/to/didder            (macOS / Linux，可执行文件名不带 .exe)
dotnet test
```

随机抖动无法与 didder 逐像素对照（两边的随机数发生器不同），改用统计检验：白点占比与理论值相符，并与 didder 的白点占比比对。

## 协议与致谢

仓库根目录的 [LICENSE](LICENSE) 沿用上游项目的 MIT 协议。第三方来源、各自的协议，以及其中需要特别注意的一个文件（`OrderedMatrices.cs` 按 MPL-2.0 提供），见 [NOTICE](NOTICE)。
