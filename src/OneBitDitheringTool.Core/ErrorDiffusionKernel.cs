using System.Collections.Immutable;

namespace OneBitDitheringTool.Core;

/// <summary>
/// 误差扩散核：描述把当前像素的量化误差，按什么比例分给哪些尚未处理的邻居。
/// </summary>
public sealed class ErrorDiffusionKernel
{
    private ErrorDiffusionKernel(string name, int denominator, ImmutableArray<ErrorDiffusionTap> taps)
    {
        Name = name;
        Denominator = denominator;
        Taps = taps;
    }

    /// <summary>核的名称，同原版界面下拉框里的名称。</summary>
    public string Name { get; }

    /// <summary>各邻居权重的公共分母。权重之和不一定等于分母：Atkinson 等核有意只扩散一部分误差。</summary>
    public int Denominator { get; }

    /// <summary>非零权重的邻居，相对当前像素的偏移与权重分子。</summary>
    internal ImmutableArray<ErrorDiffusionTap> Taps { get; }

    /// <summary>
    /// 由教科书里常见的矩阵写法创建核。
    /// </summary>
    /// <remarks>
    /// 矩阵每行是一排像素，首行描述当前像素所在的那一行。约定「当前像素」位于首行第一个非零项的左边一格，
    /// 这样 Floyd–Steinberg 写成 <c>[[0, 0, 7], [3, 5, 1]]</c> 即可，无需另外标出当前像素的位置。
    /// </remarks>
    /// <param name="name">核的名称。</param>
    /// <param name="denominator">公共分母。</param>
    /// <param name="rows">各行权重的分子，各行等长。</param>
    /// <returns>新建的核。</returns>
    internal static ErrorDiffusionKernel Create(string name, int denominator, int[][] rows)
    {
        int current = Array.FindIndex(rows[0], w => w != 0) - 1;
        var taps = ImmutableArray.CreateBuilder<ErrorDiffusionTap>();
        for (int y = 0; y < rows.Length; y++)
        {
            for (int x = 0; x < rows[y].Length; x++)
            {
                if (rows[y][x] != 0)
                {
                    taps.Add(new ErrorDiffusionTap(x - current, y, rows[y][x]));
                }
            }
        }

        return new ErrorDiffusionKernel(name, denominator, taps.ToImmutable());
    }
}

/// <summary>
/// 误差扩散核中的一项：相对当前像素向右偏移 <paramref name="Dx"/>、向下偏移 <paramref name="Dy"/> 的邻居，分得 <paramref name="Weight"/> 份误差。
/// </summary>
/// <param name="Dx">水平偏移（向右为正）。</param>
/// <param name="Dy">垂直偏移（向下为正），不会为负。</param>
/// <param name="Weight">权重分子。</param>
internal readonly record struct ErrorDiffusionTap(int Dx, int Dy, int Weight);
