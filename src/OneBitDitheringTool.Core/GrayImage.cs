namespace OneBitDitheringTool.Core;

/// <summary>
/// 8 位灰度图，可附带每像素的透明度。抖动算法的输入。
/// </summary>
public sealed class GrayImage
{
    /// <summary>
    /// 创建灰度图，不复制缓冲。
    /// </summary>
    /// <param name="width">宽度（像素），必须大于 0。</param>
    /// <param name="height">高度（像素），必须大于 0。</param>
    /// <param name="gray">每像素一字节灰度，长度必须为 <c>width * height</c>。</param>
    /// <param name="alpha">
    /// 每像素透明度，长度同上；为 <see langword="null"/> 表示整幅图完全不透明。
    /// 约定：只要存在非 255 的透明度就必须传入数组，保证「alpha 为 null ⇔ 图完全不透明」。
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">宽或高不大于 0。</exception>
    /// <exception cref="ArgumentException">缓冲长度与宽高不符。</exception>
    public GrayImage(int width, int height, byte[] gray, byte[]? alpha)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(gray);

        long count = (long)width * height;
        if (gray.LongLength != count)
        {
            throw new ArgumentException("灰度缓冲长度必须等于 宽 × 高。", nameof(gray));
        }

        if (alpha is not null && alpha.LongLength != count)
        {
            throw new ArgumentException("透明度缓冲长度必须等于 宽 × 高。", nameof(alpha));
        }

        Width = width;
        Height = height;
        Gray = gray;
        Alpha = alpha;
    }

    /// <summary>宽度（像素）。</summary>
    public int Width { get; }

    /// <summary>高度（像素）。</summary>
    public int Height { get; }

    /// <summary>每像素一字节灰度。</summary>
    public byte[] Gray { get; }

    /// <summary>每像素透明度；为 <see langword="null"/> 表示整幅图完全不透明。</summary>
    public byte[]? Alpha { get; }

    /// <summary>
    /// 展开为 R=G=B 的 RGBA 位图，供缩放管线与测试比对复用。
    /// </summary>
    /// <returns>新建的 RGBA 位图。</returns>
    internal RgbaImage ToRgba()
    {
        var pixels = new byte[Gray.Length * 4];
        for (int i = 0; i < Gray.Length; i++)
        {
            int o = i * 4;
            pixels[o] = Gray[i];
            pixels[o + 1] = Gray[i];
            pixels[o + 2] = Gray[i];
            pixels[o + 3] = Alpha is null ? (byte)255 : Alpha[i];
        }

        return new RgbaImage(Width, Height, pixels);
    }
}
