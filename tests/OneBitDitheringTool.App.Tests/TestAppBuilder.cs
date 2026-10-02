using Avalonia;
using Avalonia.Headless;
using OneBitDitheringTool.App.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace OneBitDitheringTool.App.Tests;

/// <summary>
/// 界面测试用的应用配置：以无头模式运行真实的 <see cref="App"/>。
/// </summary>
public static class TestAppBuilder
{
    /// <summary>
    /// 构建无头应用。
    /// </summary>
    /// <remarks>
    /// 关闭 <c>UseHeadlessDrawing</c> 并启用 Skia：这样控件会被真正渲染成位图，测试才能截取渲染帧、
    /// 核对预览里显示的像素，而不只是检查控件树。
    /// </remarks>
    /// <returns>配置好的应用构建器。</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
