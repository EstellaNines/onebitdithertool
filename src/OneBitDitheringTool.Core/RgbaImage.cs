namespace OneBitDitheringTool.Core;

/// <summary>
/// 8 位 RGBA 位图：像素为「非预乘」格式，按行优先排列，每个像素 4 字节（R、G、B、A）。
/// </summary>
/// <remarks>
/// 选用非预乘格式，是因为 didder 的整条输入管线（Go 的 image.NRGBA）也是非预乘的；
/// 若中途改用预乘格式，半透明像素的灰度在反预乘时会丢失精度，与 didder 的结果就会出现偏差。
/// </remarks>
public sealed class RgbaImage
{
    /// <summary>
    /// 用已有的像素缓冲创建位图，不做复制。
    /// </summary>
    /// <param name="width">宽度（像素），必须大于 0。</param>
    /// <param name="height">高度（像素），必须大于 0。</param>
    /// <param name="pixels">RGBA 像素数据，长度必须恰为 <c>width * height * 4</c>。</param>
    /// <exception cref="ArgumentOutOfRangeException">宽或高不大于 0。</exception>
    /// <exception cref="ArgumentException">缓冲长度与宽高不符。</exception>
    public RgbaImage(int width, int height, byte[] pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(pixels);

        // 用 long 计算，避免超大图在 int 乘法中溢出后恰好与缓冲长度“吻合”
        if (pixels.LongLength != (long)width * height * 4)
        {
            throw new ArgumentException("像素缓冲长度必须等于 宽 × 高 × 4。", nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    /// <summary>宽度（像素）。</summary>
    public int Width { get; }

    /// <summary>高度（像素）。</summary>
    public int Height { get; }

    /// <summary>RGBA 像素缓冲，下标 <c>(y * Width + x) * 4</c> 起依次为 R、G、B、A。</summary>
    public byte[] Pixels { get; }
}
