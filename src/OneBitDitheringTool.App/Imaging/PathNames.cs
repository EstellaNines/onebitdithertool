namespace OneBitDitheringTool.App.Imaging;

/// <summary>
/// 文件路径比较的平台约定。
/// </summary>
internal static class PathNames
{
    /// <summary>
    /// 比较路径是否相同时使用的比较器。
    /// </summary>
    /// <remarks>
    /// Windows 与 macOS 的文件系统默认不区分大小写，大小写不同的两个路径指向同一个文件；Linux 则区分。
    /// </remarks>
    public static StringComparer Comparer { get; } =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
