using SkiaSharp;

namespace OneBitDitheringTool.Core.Tests;

/// <summary>
/// 测试用的图像读写辅助。解码交给 Skia，与被测的 Core 代码互相独立，
/// 这样 PNG 编码器若有错，不会被「自己写、自己读」的对称错误掩盖。
/// </summary>
internal static class TestImageIo
{
    /// <summary>
    /// 把 PNG 字节解码为非预乘的 8 位 RGBA。
    /// </summary>
    /// <param name="encoded">PNG 文件的全部字节。</param>
    /// <returns>解码得到的位图。</returns>
    /// <exception cref="InvalidDataException">数据不是可解码的图像。</exception>
    public static RgbaImage Decode(byte[] encoded)
    {
        using var codec = SKCodec.Create(new MemoryStream(encoded))
            ?? throw new InvalidDataException("无法识别的图像数据。");

        // 显式要求非预乘输出：默认的预乘解码会让半透明像素的颜色失真，无法与 didder 逐像素比对
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = SKBitmap.Decode(codec, info)
            ?? throw new InvalidDataException("图像解码失败。");

        return new RgbaImage(info.Width, info.Height, bitmap.GetPixelSpan().ToArray());
    }

    /// <summary>
    /// 读取并解码一个 PNG 文件。
    /// </summary>
    /// <param name="path">文件路径。</param>
    /// <returns>解码得到的位图。</returns>
    public static RgbaImage DecodeFile(string path) => Decode(File.ReadAllBytes(path));

    /// <summary>
    /// 用 Core 的编码器把位图编码成 8 位 RGBA 的 PNG 字节。
    /// </summary>
    /// <param name="image">要编码的位图。</param>
    /// <returns>PNG 文件的全部字节。</returns>
    public static byte[] EncodeRgba(RgbaImage image)
    {
        using var stream = new MemoryStream();
        PngWriter.WriteRgba(stream, image);
        return stream.ToArray();
    }
}
