namespace OneBitDitheringTool.Core;

/// <summary>
/// 内置的误差扩散核目录，名称与顺序同原版界面的下拉框。
/// </summary>
/// <remarks>
/// 各核的系数是公开的标准定义（可见于 Wikipedia 的 Error diffusion 词条与 Tanner Helland 的抖动算法综述），
/// 其中 Steven Pigeon 核出自 https://hbfs.wordpress.com/2013/12/31/dithering/ 。
/// </remarks>
public static class ErrorDiffusionKernels
{
    private static readonly ErrorDiffusionKernel[] Kernels =
    [
        ErrorDiffusionKernel.Create("Simple2D", 2, [[0, 1], [1, 0]]),
        ErrorDiffusionKernel.Create("FloydSteinberg", 16, [[0, 0, 7], [3, 5, 1]]),
        ErrorDiffusionKernel.Create("FalseFloydSteinberg", 8, [[0, 3], [3, 2]]),
        ErrorDiffusionKernel.Create("JarvisJudiceNinke", 48, [[0, 0, 0, 7, 5], [3, 5, 7, 5, 3], [1, 3, 5, 3, 1]]),

        // Atkinson 只扩散 6/8 的误差，剩下的 1/4 直接丢弃：画面对比更硬，纯黑纯白区域更干净
        ErrorDiffusionKernel.Create("Atkinson", 8, [[0, 0, 1, 1], [1, 1, 1, 0], [0, 1, 0, 0]]),
        ErrorDiffusionKernel.Create("Stucki", 42, [[0, 0, 0, 8, 4], [2, 4, 8, 4, 2], [1, 2, 4, 2, 1]]),
        ErrorDiffusionKernel.Create("Burkes", 32, [[0, 0, 0, 8, 4], [2, 4, 8, 4, 2]]),
        ErrorDiffusionKernel.Create("Sierra", 32, [[0, 0, 0, 5, 3], [2, 4, 5, 4, 2], [0, 2, 3, 2, 0]]),
        ErrorDiffusionKernel.Create("TwoRowSierra", 16, [[0, 0, 0, 4, 3], [1, 2, 3, 2, 1]]),
        ErrorDiffusionKernel.Create("SierraLite", 4, [[0, 0, 2], [1, 1, 0]]),

        // Steven Pigeon 核同样只扩散 12/14 的误差
        ErrorDiffusionKernel.Create("StevenPigeon", 14, [[0, 0, 0, 2, 1], [0, 2, 2, 2, 0], [1, 0, 1, 0, 1]]),
    ];

    /// <summary>
    /// 全部内置核，顺序同原版界面。
    /// </summary>
    public static IReadOnlyList<ErrorDiffusionKernel> All => Kernels;

    /// <summary>
    /// 按名称取核，名称不区分大小写。
    /// </summary>
    /// <param name="name">核的名称，如 <c>FloydSteinberg</c>。</param>
    /// <returns>对应的核。</returns>
    /// <exception cref="ArgumentException">没有该名称的核。</exception>
    public static ErrorDiffusionKernel Get(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        foreach (ErrorDiffusionKernel kernel in Kernels)
        {
            if (string.Equals(kernel.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return kernel;
            }
        }

        throw new ArgumentException($"没有名为 {name} 的误差扩散核。", nameof(name));
    }
}
