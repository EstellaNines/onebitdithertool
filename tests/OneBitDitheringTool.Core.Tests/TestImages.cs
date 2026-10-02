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
