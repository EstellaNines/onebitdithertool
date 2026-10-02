namespace OneBitDitheringTool.Core;

/// <summary>
/// 灰度化时 R、G、B 三个通道的权重。
/// </summary>
/// <param name="Red">红色通道权重。</param>
/// <param name="Green">绿色通道权重。</param>
/// <param name="Blue">蓝色通道权重。</param>
public readonly record struct ChannelWeights(double Red, double Green, double Blue)
{
    /// <summary>
    /// 默认的灰度系数（ITU-R BT.601 亮度公式），与 didder 内部使用的 imaging 库一致。
    /// </summary>
    public static ChannelWeights Default => new(0.299, 0.587, 0.114);
}

/// <summary>
/// 抖动前预处理的参数。
/// </summary>
public sealed record PrepareOptions
{
    /// <summary>
    /// 缩放后的目标宽度（像素）；0 表示不缩放。高度按原图宽高比自动换算。
    /// </summary>
    public int TargetWidth { get; init; }

    /// <summary>
    /// 自定义的 RGB 通道权重（对应原版的 Split Channels）；<see langword="null"/> 表示使用默认灰度公式。
    /// </summary>
    public ChannelWeights? Weights { get; init; }

    /// <summary>
    /// 对比度，取值范围 [-1, 1]，0 表示不调整。
    /// </summary>
    public double Contrast { get; init; }

    /// <summary>
    /// 亮度，取值范围 [-1, 1]，0 表示不调整。
    /// </summary>
    public double Brightness { get; init; }
}
