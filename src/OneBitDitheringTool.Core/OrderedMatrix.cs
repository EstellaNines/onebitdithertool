using System.Collections.Immutable;

namespace OneBitDitheringTool.Core;

/// <summary>
/// 一张有序抖动用的阈值矩阵。
/// </summary>
/// <param name="Name">矩阵名称，同原版界面下拉框里的名称。</param>
/// <param name="Width">矩阵宽度。</param>
/// <param name="Height">矩阵高度。</param>
/// <param name="Max">
/// 阈值的除数。取值恰为 0 到 N-1 各一次的矩阵，其值等于格数 N；
/// 对角矩阵里每个值会出现两次，其值是最大值加 1，即格数的一半。
/// </param>
/// <param name="Values">按行优先排列的阈值，下标 <c>y * Width + x</c>，每个值应小于 <paramref name="Max"/>。</param>
public sealed record OrderedMatrix(string Name, int Width, int Height, int Max, ImmutableArray<int> Values);
