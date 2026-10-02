namespace OneBitDitheringTool.Core;

/// <summary>
/// 工具里用户可调的全部参数：缩放、通道权重、亮度、对比度与抖动设置。
/// </summary>
/// <remarks>
/// 预览、输出尺寸标签与批量保存共用这一份参数及其换算规则，三处便不会各算各的而出现不一致。
/// 它是不可变的值对象，按值比较，可直接用来判断「参数有没有变」。默认值与原版工具的初始状态一致。
/// </remarks>
public sealed record ToolSettings
{
    /// <summary>
    /// 缩放比例，范围 (0, 1]。小于 1 时在抖动前先把图缩小到该比例的宽度；只能缩小，不能放大。
    /// </summary>
    public double Scale { get; init; } = 1.0;

    /// <summary>是否使用自定义的 RGB 通道权重来灰度化（原版的 Split Channels）。</summary>
    public bool SplitChannels { get; init; }

    /// <summary>自定义通道权重，仅当 <see cref="SplitChannels"/> 为真时生效。缺省值与原版一致。</summary>
    public ChannelWeights Weights { get; init; } = new(0.30, 0.59, 0.11);

    /// <summary>亮度，范围 [-1, 1]，0 表示不调整。</summary>
    public double Brightness { get; init; }

    /// <summary>对比度，范围 [-1, 1]，0 表示不调整。</summary>
    public double Contrast { get; init; }

    /// <summary>抖动算法及其参数。</summary>
    public DitherSettings Dither { get; init; } = new();

    /// <summary>
    /// 换算出缩放后的目标宽度。
    /// </summary>
    /// <param name="sourceWidth">原图宽度（像素）。</param>
    /// <returns>目标宽度；0 表示不缩放。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sourceWidth"/> 不大于 0，或 <see cref="Scale"/> 不是正的有限数。</exception>
    public int GetTargetWidth(int sourceWidth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceWidth);
        if (!double.IsFinite(Scale) || Scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Scale), Scale, "缩放比例必须是正的有限数。");
        }

        if (Scale >= 1.0)
        {
            return 0;
        }

        // 加一个极小量再取整，抵消浮点误差：0.29 × 100 在 double 里是 28.999999999999996，
        // 直接取整会得到 28，而界面上显示的是 29。原版在这种比例上会少一个像素，这里不重复这个小毛病。
        // 至少保留 1 像素，避免极小比例得到 0（原版会因此悄悄不缩放）
        return Math.Max(1, (int)Math.Floor((sourceWidth * Scale) + 1e-9));
    }

    /// <summary>
    /// 换算出处理后图像的尺寸，与 <see cref="Preprocessor"/> 实际产出的尺寸一致。
    /// </summary>
    /// <param name="sourceWidth">原图宽度（像素）。</param>
    /// <param name="sourceHeight">原图高度（像素）。</param>
    /// <returns>处理后的宽与高。</returns>
    /// <exception cref="ArgumentOutOfRangeException">宽或高不大于 0，或缩放比例无效。</exception>
    public (int Width, int Height) GetOutputSize(int sourceWidth, int sourceHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceHeight);
        int targetWidth = GetTargetWidth(sourceWidth);
        if (targetWidth == 0)
        {
            return (sourceWidth, sourceHeight);
        }

        // 高度的换算与 Preprocessor.ResizeBox 完全相同：按宽高比四舍五入，至少 1 像素
        double height = (double)targetWidth * sourceHeight / sourceWidth;
        return (targetWidth, (int)Math.Max(1.0, Math.Floor(height + 0.5)));
    }

    /// <summary>
    /// 生成预处理参数。
    /// </summary>
    /// <param name="sourceWidth">原图宽度（像素），用于把缩放比例换算成目标宽度。</param>
    /// <returns>预处理参数；值相等的参数产出相同的灰度图，可作缓存键。</returns>
    /// <exception cref="ArgumentOutOfRangeException">宽度不大于 0，或缩放比例无效。</exception>
    public PrepareOptions ToPrepareOptions(int sourceWidth) => new()
    {
        TargetWidth = GetTargetWidth(sourceWidth),
        Weights = SplitChannels ? Weights : null,
        Contrast = Contrast,
        Brightness = Brightness,
    };

    /// <summary>
    /// 对一幅图走完整管线：预处理，再抖动。
    /// </summary>
    /// <param name="source">原图。</param>
    /// <param name="cancellationToken">用于取消耗时的计算。</param>
    /// <returns>1-bit 结果。</returns>
    /// <exception cref="OperationCanceledException">计算被取消。</exception>
    public OneBitImage Render(RgbaImage source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Render(Preprocessor.Prepare(source, ToPrepareOptions(source.Width)), cancellationToken);
    }

    /// <summary>
    /// 对已经预处理好的灰度图抖动。预览时只改抖动参数的话，可以复用上一次的预处理结果，省掉缩放与灰度化。
    /// </summary>
    /// <param name="prepared">预处理之后的灰度图。</param>
    /// <param name="cancellationToken">用于取消耗时的计算。</param>
    /// <returns>1-bit 结果。</returns>
    /// <exception cref="OperationCanceledException">计算被取消。</exception>
    public OneBitImage Render(GrayImage prepared, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        return Dither.CreateDitherer().Dither(prepared, cancellationToken);
    }
}
