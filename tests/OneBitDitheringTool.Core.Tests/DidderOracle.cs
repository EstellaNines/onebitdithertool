using System.Diagnostics;
using System.Globalization;

namespace OneBitDitheringTool.Core.Tests;

/// <summary>
/// 把 didder 命令行当作「黑盒参照机」运行，用它的输出校验 Core 的结果。
/// </summary>
/// <remarks>
/// didder 是 GPL-3.0 项目，这里只以独立进程调用其可执行文件，不复制、不链接其代码，也不随仓库分发；
/// 可执行文件路径由环境变量 <see cref="EnvironmentVariable"/> 指定，未设置时依赖它的测试会被跳过。
/// 获取方式：克隆 didder 源码后执行 <c>go build</c>，再把环境变量指向生成的可执行文件。
/// </remarks>
internal static class DidderOracle
{
    /// <summary>指定 didder 可执行文件路径的环境变量名。</summary>
    public const string EnvironmentVariable = "DIDDER_PATH";

    /// <summary>未配置参照机时的跳过原因。</summary>
    public const string SkipReason = "未设置环境变量 DIDDER_PATH，跳过与 didder 的对照测试。";

    /// <summary>
    /// 256 级灰阶调色板参数。配合噪声幅度为 0 的 <c>random 0 0</c> 模式使用，
    /// didder 会原样输出「预处理之后、尚未二值化」的灰度图，从而能单独校验预处理步骤。
    /// </summary>
    public static string GrayscalePalette { get; } = string.Join(' ', Enumerable.Range(0, 256));

    /// <summary>参照机是否可用。</summary>
    public static bool IsAvailable => ExecutablePath is { Length: > 0 } path && File.Exists(path);

    private static string? ExecutablePath => Environment.GetEnvironmentVariable(EnvironmentVariable);

    /// <summary>
    /// 把数值格式化成 didder 命令行里的 <c>--名称=值</c> 形式，使用不随区域设置变化的 "R" 格式，
    /// 保证 didder 解析出的 double 与测试里的 double 完全相同。
    /// </summary>
    /// <param name="name">参数名（不含前导连字符）。</param>
    /// <param name="value">参数值。</param>
    /// <returns>形如 <c>--contrast=0.35</c> 的参数。</returns>
    public static string Flag(string name, double value) =>
        $"--{name}={value.ToString("R", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// 调用 didder 处理一幅图并返回其输出。
    /// </summary>
    /// <param name="input">输入图。</param>
    /// <param name="globalFlags">全局参数，如 <c>--palette=...</c>、<c>--strength=...</c>，必须写在子命令之前。</param>
    /// <param name="command">子命令及其参数，如 <c>bayer 8x8</c>。</param>
    /// <returns>didder 输出的图像（解码为非预乘 RGBA）。</returns>
    /// <exception cref="InvalidOperationException">didder 以非零状态退出。</exception>
    public static RgbaImage Run(RgbaImage input, IEnumerable<string> globalFlags, IEnumerable<string> command)
    {
        string directory = Path.Combine(Path.GetTempPath(), "obdt-oracle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string inputPath = Path.Combine(directory, "in.png");
            string outputPath = Path.Combine(directory, "out.png");
            File.WriteAllBytes(inputPath, TestImageIo.EncodeRgba(input));

            var startInfo = new ProcessStartInfo(ExecutablePath!)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (string flag in globalFlags)
            {
                startInfo.ArgumentList.Add(flag);
            }

            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(outputPath);
            foreach (string part in command)
            {
                startInfo.ArgumentList.Add(part);
            }

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("无法启动 didder 进程。");

            // 两个输出流都被重定向时，必须并发读取，否则一方缓冲区写满会让子进程阻塞
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            string standardOutput = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"didder 退出码 {process.ExitCode}。参数：{string.Join(' ', startInfo.ArgumentList.Select(a => a.Length > 60 ? a[..60] + "…" : a))}\n{standardOutput}\n{standardError.Result}");
            }

            return TestImageIo.DecodeFile(outputPath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
