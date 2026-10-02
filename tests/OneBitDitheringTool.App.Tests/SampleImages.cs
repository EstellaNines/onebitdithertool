using OneBitDitheringTool.Core;

namespace OneBitDitheringTool.App.Tests;

/// <summary>
/// 界面测试用的合成图像，由确定性公式生成，不依赖随机数状态。
/// </summary>
internal static class SampleImages
{
    /// <summary>
    /// 生成一幅带少量噪声的彩色渐变图（完全不透明）。
    /// </summary>
    /// <param name="width">宽度（像素）。</param>
    /// <param name="height">高度（像素）。</param>
    /// <returns>新建的位图。</returns>
    public static RgbaImage Noisy(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int o = ((y * width) + x) * 4;
                int noise = ((x * 7) ^ (y * 13)) & 0x1F;
                pixels[o] = (byte)Math.Min(255, (x * 255 / Math.Max(1, width - 1) / 2) + noise);
                pixels[o + 1] = (byte)Math.Min(255, (y * 255 / Math.Max(1, height - 1) / 2) + noise);
                pixels[o + 2] = (byte)Math.Min(255, 96 + noise);
                pixels[o + 3] = 255;
            }
        }

        return new RgbaImage(width, height, pixels);
    }
}
