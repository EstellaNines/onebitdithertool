namespace OneBitDitheringTool.Core;

/// <summary>
/// 抖动算法：把灰度图转换成 1-bit 黑白图。
/// </summary>
public interface IDitherer
{
    /// <summary>
    /// 对灰度图执行抖动。
    /// </summary>
    /// <param name="image">预处理之后的灰度图。</param>
    /// <param name="cancellationToken">用于取消耗时的计算；取消时抛出 <see cref="OperationCanceledException"/>。</param>
    /// <returns>1-bit 结果，尺寸与输入相同，透明度原样保留。</returns>
    /// <exception cref="OperationCanceledException">计算被取消。</exception>
    OneBitImage Dither(GrayImage image, CancellationToken cancellationToken = default);
}
