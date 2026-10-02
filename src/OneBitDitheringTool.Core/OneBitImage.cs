namespace OneBitDitheringTool.Core;

/// <summary>
/// 抖动后的 1-bit 结果：每个像素非黑即白，可附带原图的透明度。
/// </summary>
public sealed class OneBitImage
{
    /// <summary>
    /// 创建 1-bit 结果。
    /// </summary>
    /// <param name="width">宽度（像素），必须大于 0。</param>
    /// <param name="height">高度（像素），必须大于 0。</param>
    /// <param name="levels">每像素一字节：0 为黑，其余为白；长度必须为 <c>width * height</c>。</param>
    /// <param name="alpha">
    /// 每像素透明度，长度同上；为 <see langword="null"/> 表示整幅图完全不透明。
    /// 约定：只要存在非 255 的透明度就必须传入数组，保证「alpha 为 null ⇔ 图完全不透明」。
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">宽或高不大于 0。</exception>
    /// <exception cref="ArgumentException">缓冲长度与宽高不符。</exception>
    public OneBitImage(int width, int height, byte[] levels, byte[]? alpha)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(levels);

        long count = (long)width * height;
        if (levels.LongLength != count)
        {
            throw new ArgumentException("黑白缓冲长度必须等于 宽 × 高。", nameof(levels));
        }

        if (alpha is not null && alpha.LongLength != count)
        {
            throw new ArgumentException("透明度缓冲长度必须等于 宽 × 高。", nameof(alpha));
        }

        Width = width;
        Height = height;
        Levels = levels;
        Alpha = alpha;
    }

    /// <summary>宽度（像素）。</summary>
    public int Width { get; }

    /// <summary>高度（像素）。</summary>
    public int Height { get; }

    /// <summary>每像素一字节：0 为黑，非 0 为白。</summary>
    public byte[] Levels { get; }

    /// <summary>每像素透明度；为 <see langword="null"/> 表示整幅图完全不透明。</summary>
    public byte[]? Alpha { get; }

    /// <summary>
    /// 展开为 8 位 RGBA 位图，用于预览显示与测试比对。
    /// </summary>
    /// <remarks>
    /// 完全透明（alpha 为 0）的像素统一输出 (0,0,0,0)：这类像素的 RGB 不可见，没有保留的意义，
    /// 与 didder 在误差扩散模式下的输出一致。
    /// </remarks>
    /// <returns>新建的 RGBA 位图。</returns>
    public RgbaImage ToRgba()
    {
        var pixels = new byte[Levels.Length * 4];
        for (int i = 0; i < Levels.Length; i++)
        {
            byte a = Alpha is null ? (byte)255 : Alpha[i];
            if (a == 0)
            {
                continue;
            }

            byte v = Levels[i] != 0 ? (byte)255 : (byte)0;
            int o = i * 4;
            pixels[o] = v;
            pixels[o + 1] = v;
            pixels[o + 2] = v;
            pixels[o + 3] = a;
        }

        return new RgbaImage(Width, Height, pixels);
    }

    /// <summary>
    /// 将结果写成 PNG：完全不透明时写真 1-bit 索引图，含透明度时写 8 位 RGBA。
    /// </summary>
    /// <remarks>
    /// 1 位深索引图无法表达半透明，因此含透明度的图只能退回 RGBA，这与原版工具的输出格式一致。
    /// </remarks>
    /// <param name="stream">目标流，调用方负责关闭。</param>
    public void WritePng(Stream stream)
    {
        if (Alpha is null)
        {
            PngWriter.WriteOneBit(stream, Width, Height, Levels);
        }
        else
        {
            PngWriter.WriteRgba(stream, ToRgba());
        }
    }
}
