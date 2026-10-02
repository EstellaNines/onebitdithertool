using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using OneBitDitheringTool.Core;

namespace OneBitDitheringTool.App.Rendering;

/// <summary>
/// 把 Core 的图像转换成可在界面上显示的位图。
/// </summary>
/// <remarks>
/// 显示用的位图必须是「预乘 alpha」的 BGRA：这是 Avalonia 与 Skia 合成时使用的格式，
/// 若直接塞非预乘数据，半透明像素在界面上会偏亮。结果只用于显示，保存文件走 Core 的 PNG 编码器，不经过这里。
/// </remarks>
internal static class PreviewBitmaps
{
    // 屏幕常用的 96 DPI；图像按像素显示，不依赖具体取值
    private static readonly Vector Dpi = new(96, 96);

    /// <summary>
    /// 把抖动结果转成位图：黑白两色，透明度原样保留。
    /// </summary>
    /// <param name="image">抖动结果。</param>
    /// <returns>新建的位图，调用方负责释放。</returns>
    public static WriteableBitmap FromOneBit(OneBitImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        return Create(image.Width, image.Height, (i, pixel) =>
        {
            byte alpha = image.Alpha is null ? (byte)255 : image.Alpha[i];

            // 白色预乘后每个分量等于 alpha，黑色恒为 0；完全透明时整个像素为 0
            byte color = image.Levels[i] != 0 ? alpha : (byte)0;
            pixel[0] = color;
            pixel[1] = color;
            pixel[2] = color;
            pixel[3] = alpha;
        });
    }

    /// <summary>
    /// 把原图转成位图，用于「Show Original」。
    /// </summary>
    /// <param name="image">原图（非预乘 RGBA）。</param>
    /// <returns>新建的位图，调用方负责释放。</returns>
    public static WriteableBitmap FromRgba(RgbaImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        return Create(image.Width, image.Height, (i, pixel) =>
        {
            int o = i * 4;
            int alpha = image.Pixels[o + 3];

            // 预乘并四舍五入：分量 × alpha ÷ 255；alpha 为 255 时恰好等于原值
            pixel[0] = (byte)(((image.Pixels[o + 2] * alpha) + 127) / 255);
            pixel[1] = (byte)(((image.Pixels[o + 1] * alpha) + 127) / 255);
            pixel[2] = (byte)(((image.Pixels[o] * alpha) + 127) / 255);
            pixel[3] = (byte)alpha;
        });
    }

    private static WriteableBitmap Create(int width, int height, FillPixel fill)
    {
        var bitmap = new WriteableBitmap(new PixelSize(width, height), Dpi, PixelFormat.Bgra8888, AlphaFormat.Premul);
        using ILockedFramebuffer buffer = bitmap.Lock();

        // 逐行填充再复制：行内先写进托管数组，避免对显存逐字节地跨边界写入；
        // 同时兼容帧缓冲每行带对齐填充（RowBytes 大于 宽 × 4）的情况
        var row = new byte[width * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                fill((y * width) + x, row.AsSpan(x * 4, 4));
            }

            Marshal.Copy(row, 0, buffer.Address + (y * buffer.RowBytes), row.Length);
        }

        return bitmap;
    }

    private delegate void FillPixel(int index, Span<byte> bgra);
}
