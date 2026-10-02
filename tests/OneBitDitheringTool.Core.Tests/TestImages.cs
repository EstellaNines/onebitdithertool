namespace OneBitDitheringTool.Core.Tests;

/// <summary>
/// 测试用的合成图像。全部由确定性公式生成，不依赖随机数状态，
/// 同一份输入既交给 didder 也交给 Core，结果才有可比性。
/// </summary>
internal static class TestImages
{
    /// <summary>
    /// 生成一幅平滑渐变叠加少量整数噪声的彩色图（完全不透明）。
    /// </summary>
    /// <remarks>
    /// 只有平滑渐变时，缩放与取整的细微差异容易被掩盖；叠加噪声能让每个像素的取整路径都不同，
    /// 从而更容易暴露实现偏差。尺寸应取质数这类「不整齐」的值，避免缩放权重恰好对齐。
    /// </remarks>
    /// <param name="width">宽度（像素）。</param>
    /// <param name="height">高度（像素）。</param>
    /// <param name="seed">变化种子，不同种子给出不同图案。</param>
    /// <returns>新建的位图。</returns>
    public static RgbaImage Plasma(int width, int height, int seed = 1)
    {
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double fx = x / (double)width;
                double fy = y / (double)height;
                double r = 0.5 + (0.5 * Math.Sin((6.0 * fx) + (3.0 * fy) + seed));
                double g = 0.5 + (0.5 * Math.Sin((9.0 * fy) - (4.0 * fx) + (2.0 * seed)));
                double b = 0.5 + (0.5 * Math.Cos((21.0 * fx * fy) + seed));

                int o = ((y * width) + x) * 4;
                pixels[o] = ToByte((r * 223) + Noise(x, y, seed, 0));
                pixels[o + 1] = ToByte((g * 223) + Noise(x, y, seed, 1));
                pixels[o + 2] = ToByte((b * 223) + Noise(x, y, seed, 2));
                pixels[o + 3] = 255;
            }
        }

        return new RgbaImage(width, height, pixels);
    }

    /// <summary>
    /// 生成纯色灰度图（完全不透明）。
    /// </summary>
    /// <remarks>
    /// 整幅纯黑（或纯白）图会让每一个矩阵格都恰好撞上同一个亮度值，
    /// 能检出「最大（或最小）阈值格把纯黑抖成白点、纯白抖成黑点」的舍入偏差；灰阶条每个灰度只占一列，覆盖不到这种情况。
    /// </remarks>
    /// <param name="width">宽度（像素）。</param>
    /// <param name="height">高度（像素）。</param>
    /// <param name="value">灰度值 0 到 255。</param>
    /// <returns>新建的纯色位图。</returns>
    public static RgbaImage Solid(int width, int height, byte value)
    {
        var pixels = new byte[width * height * 4];
        for (int i = 0; i < width * height; i++)
        {
            pixels[i * 4] = value;
            pixels[(i * 4) + 1] = value;
            pixels[(i * 4) + 2] = value;
            pixels[(i * 4) + 3] = 255;
        }

        return new RgbaImage(width, height, pixels);
    }

    /// <summary>
    /// 生成横向 0 到 255 的灰阶条（完全不透明），每个灰度占一列，包含纯黑与纯白两个极端。
    /// </summary>
    /// <remarks>
    /// 纯黑、纯白是抖动算法最容易出错的两端：舍入偏差会让纯黑被抖出白点，或让纯白被抖出黑点。
    /// </remarks>
    /// <param name="height">高度（像素），应大于最大矩阵的高度，才能让矩阵的每一行都用到。</param>
    /// <returns>宽 256 的灰阶条。</returns>
    public static RgbaImage GrayRamp(int height)
    {
        var pixels = new byte[256 * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int v = 0; v < 256; v++)
            {
                int o = ((y * 256) + v) * 4;
                pixels[o] = (byte)v;
                pixels[o + 1] = (byte)v;
                pixels[o + 2] = (byte)v;
                pixels[o + 3] = 255;
            }
        }

        return new RgbaImage(256, height, pixels);
    }

    /// <summary>
    /// 在已有图像上叠加一组循环出现的透明度（0、64、128、200、255），用来覆盖完全透明、半透明与不透明三类像素。
    /// </summary>
    /// <param name="image">原图，不会被修改。</param>
    /// <returns>带透明度的新位图。</returns>
    public static RgbaImage WithAlphaPattern(RgbaImage image)
    {
        var pixels = (byte[])image.Pixels.Clone();
        for (int i = 0; i < image.Width * image.Height; i++)
        {
            int x = i % image.Width;
            int y = i / image.Width;
            pixels[(i * 4) + 3] = (((x * 7) + (y * 3)) % 5) switch
            {
                0 => 0,
                1 => 64,
                2 => 128,
                3 => 200,
                _ => 255,
            };
        }

        return new RgbaImage(image.Width, image.Height, pixels);
    }

    private static int Noise(int x, int y, int seed, int channel)
    {
        // 整数哈希（乘以大奇数后异或移位），取低 5 位得到 0..31 的噪声；unchecked 允许乘法溢出回绕
        unchecked
        {
            uint h = (uint)((x * 73856093) ^ (y * 19349663) ^ (seed * 83492791) ^ (channel * 2654435761));
            h ^= h >> 13;
            h *= 1274126177;
            h ^= h >> 16;
            return (int)(h & 0x1F);
        }
    }

    private static byte ToByte(double value) => (byte)Math.Clamp((int)Math.Round(value), 0, 255);
}
