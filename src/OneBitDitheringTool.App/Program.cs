using Avalonia;

namespace OneBitDitheringTool.App;

/// <summary>
/// 程序入口。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 启动桌面应用。
    /// </summary>
    /// <remarks>
    /// 在 <see cref="BuildAvaloniaApp"/> 返回之前，不要使用任何 Avalonia 或依赖同步上下文的代码，此时它们尚未初始化。
    /// </remarks>
    /// <param name="args">命令行参数。</param>
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// 构建 Avalonia 应用的配置。可视化设计器与界面测试也会调用它，因此不要删除或改名。
    /// </summary>
    /// <returns>配置好的应用构建器。</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
