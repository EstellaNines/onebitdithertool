using System.Runtime.InteropServices;
using OneBitDitheringTool.Core;
using SkiaSharp;

namespace OneBitDitheringTool.App.Imaging;

/// <summary>
/// 把图片文件解码为 Core 使用的非预乘 8 位 RGBA。
/// </summary>
/// <remarks>
/// 不用 Avalonia 自带的 <c>Bitmap</c>，是因为它解码出来的是预乘像素：半透明像素的颜色在预乘再还原的往返里会丢精度，
/// 抖动结果就可能与 didder 不同。这里直接让 Skia 输出非预乘像素，并按 EXIF 方向转正（didder 默认也会转正）。
/// </remarks>
public static class ImageDecoder
{
    private static readonly string[] SupportedExtensions = [".png", ".jpg", ".jpeg"];

    /// <summary>
    /// 判断文件扩展名是否属于支持的图片格式（PNG、JPG、JPEG，不区分大小写）。
    /// </summary>
    /// <param name="path">文件路径或文件名。</param>
    /// <returns>支持返回 <see langword="true"/>。</returns>
    public static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 读取并解码一个图片文件。
    /// </summary>
    /// <param name="path">图片文件路径。</param>
    /// <returns>按 EXIF 方向转正后的非预乘 RGBA 位图。</returns>
    /// <exception cref="InvalidDataException">文件不是可识别的图片，或已损坏、不完整。</exception>
    /// <exception cref="IOException">文件无法读取。</exception>
    public static RgbaImage Decode(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Decode(File.ReadAllBytes(path));
    }

    /// <summary>
    /// 解码内存中的图片数据。
    /// </summary>
    /// <param name="encoded">图片文件的全部字节。</param>
    /// <returns>按 EXIF 方向转正后的非预乘 RGBA 位图。</returns>
    /// <exception cref="InvalidDataException">数据不是可识别的图片，或已损坏、不完整。</exception>
    public static RgbaImage Decode(byte[] encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);

        using var codec = SKCodec.Create(new MemoryStream(encoded))
            ?? throw new InvalidDataException("无法识别的图片格式。");

        // 显式要求非预乘的 RGBA：不依赖 Skia 的默认像素格式，Windows 与其他平台上的结果才一致
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var pixels = new byte[checked(info.Width * info.Height * 4)];

        // 直接解码进托管数组（先固定住它），省去一次与图像等大的内存复制
        GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            SKCodecResult result = codec.GetPixels(info, handle.AddrOfPinnedObject());
            if (result != SKCodecResult.Success)
            {
                throw new InvalidDataException($"图片已损坏或不完整（{result}）。");
            }
        }
        finally
        {
            handle.Free();
        }

        // SKEncodedOrigin 的取值 1 到 8 与 EXIF 方向标签的取值一一对应
        return ApplyOrientation(new RgbaImage(info.Width, info.Height, pixels), (int)codec.EncodedOrigin);
    }

    /// <summary>
    /// 按 EXIF 方向标签把图像转正。
    /// </summary>
    /// <remarks>
    /// 标签含义：1 正常；2 水平翻转；3 旋转 180°；4 垂直翻转；5 转置（沿主对角线翻转）；
    /// 6 顺时针旋转 90°；7 转置后再旋转 180°（沿副对角线翻转）；8 逆时针旋转 90°。
    /// 取值 5 到 8 会交换宽高。
    /// </remarks>
    /// <param name="image">原图。</param>
    /// <param name="orientation">EXIF 方向标签，1 到 8；其他值视为 1。</param>
    /// <returns>转正后的位图；方向为 1 时直接返回原对象。</returns>
    internal static RgbaImage ApplyOrientation(RgbaImage image, int orientation)
    {
        if (orientation is < 2 or > 8)
        {
            return image;
        }

        int w = image.Width;
        int h = image.Height;
        bool swap = orientation >= 5;
        int outW = swap ? h : w;
        int outH = swap ? w : h;
        var output = new byte[image.Pixels.Length];

        for (int sy = 0; sy < h; sy++)
        {
            for (int sx = 0; sx < w; sx++)
            {
                // 每种方向给出「源像素 (sx, sy) 落到目标的哪个位置」
                (int dx, int dy) = orientation switch
                {
                    2 => (w - 1 - sx, sy),
                    3 => (w - 1 - sx, h - 1 - sy),
                    4 => (sx, h - 1 - sy),
                    5 => (sy, sx),
                    6 => (h - 1 - sy, sx),
                    7 => (h - 1 - sy, w - 1 - sx),
                    _ => (sy, w - 1 - sx),
                };

                Buffer.BlockCopy(image.Pixels, ((sy * w) + sx) * 4, output, ((dy * outW) + dx) * 4, 4);
            }
        }

        return new RgbaImage(outW, outH, output);
    }
}
