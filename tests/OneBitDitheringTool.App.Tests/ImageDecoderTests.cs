using OneBitDitheringTool.App.Imaging;
using OneBitDitheringTool.Core;
using SkiaSharp;

namespace OneBitDitheringTool.App.Tests;

public class ImageDecoderTests
{
    [Theory]
    [InlineData("a.png", true)]
    [InlineData("a.PNG", true)]
    [InlineData("photo.JpG", true)]
    [InlineData("photo.jpeg", true)]
    [InlineData("a.bmp", false)]
    [InlineData("a.gif", false)]
    [InlineData("noextension", false)]
    public void IsSupported_AcceptsOnlyPngJpgJpeg(string path, bool expected)
    {
        Assert.Equal(expected, ImageDecoder.IsSupported(path));
    }

    [Fact]
    public void Decode_Png_KeepsNonPremultipliedColorsExactly()
    {
        // 低透明度像素最容易在「预乘再还原」里丢精度：alpha=3 时 RGB 只剩约 1% 的有效位。
        // 非预乘解码必须原样还原它们
        var pixels = new byte[]
        {
            200, 100, 50, 3,
            201, 99, 51, 17,
            10, 20, 30, 128,
            250, 251, 252, 255,
        };
        var source = new RgbaImage(2, 2, pixels);
        using var stream = new MemoryStream();
        PngWriter.WriteRgba(stream, source);

        RgbaImage decoded = ImageDecoder.Decode(stream.ToArray());

        Assert.Equal(2, decoded.Width);
        Assert.Equal(2, decoded.Height);
        Assert.Equal(pixels, decoded.Pixels);
    }

    [Fact]
    public void Decode_OneBitPng_ExpandsToBlackAndWhite()
    {
        using var stream = new MemoryStream();
        PngWriter.WriteOneBit(stream, 3, 1, [1, 0, 1]);

        RgbaImage decoded = ImageDecoder.Decode(stream.ToArray());

        Assert.Equal([255, 255, 255, 255, 0, 0, 0, 255, 255, 255, 255, 255], decoded.Pixels);
    }

    [Fact]
    public void Decode_RejectsGarbageAndTruncatedData()
    {
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));

        using var stream = new MemoryStream();
        PngWriter.WriteRgba(stream, new RgbaImage(40, 40, new byte[40 * 40 * 4]));
        byte[] png = stream.ToArray();
        Assert.Throws<InvalidDataException>(() => ImageDecoder.Decode(png[..(png.Length / 2)]));
    }

    [Fact]
    public void Decode_ReadsFilesFromDisk()
    {
        string path = Path.Combine(Path.GetTempPath(), "obdt-decoder-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            using (FileStream file = File.Create(path))
            {
                PngWriter.WriteRgba(file, new RgbaImage(1, 1, [9, 8, 7, 255]));
            }

            Assert.Equal([9, 8, 7, 255], ImageDecoder.Decode(path).Pixels);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// 一幅 3×2 的小图，六个像素的红色分量依次为 10 到 60，用来辨认每个像素被转到了哪里：
    /// <code>
    /// 10 20 30
    /// 40 50 60
    /// </code>
    /// 参数依次为：EXIF 方向、期望宽、期望高、期望的红色分量（行优先）。
    /// </summary>
    public static TheoryData<int, int, int, int[]> Orientations => new()
    {
        { 1, 3, 2, [10, 20, 30, 40, 50, 60] },
        { 2, 3, 2, [30, 20, 10, 60, 50, 40] },
        { 3, 3, 2, [60, 50, 40, 30, 20, 10] },
        { 4, 3, 2, [40, 50, 60, 10, 20, 30] },
        { 5, 2, 3, [10, 40, 20, 50, 30, 60] },
        { 6, 2, 3, [40, 10, 50, 20, 60, 30] },
        { 7, 2, 3, [60, 30, 50, 20, 40, 10] },
        { 8, 2, 3, [30, 60, 20, 50, 10, 40] },
    };

    [Theory]
    [MemberData(nameof(Orientations))]
    public void ApplyOrientation_MovesEveryPixelToItsExifPosition(int orientation, int width, int height, int[] expectedRed)
    {
        var pixels = new byte[3 * 2 * 4];
        for (int i = 0; i < 6; i++)
        {
            pixels[i * 4] = (byte)((i + 1) * 10);
            pixels[(i * 4) + 3] = 255;
        }

        RgbaImage result = ImageDecoder.ApplyOrientation(new RgbaImage(3, 2, pixels), orientation);

        Assert.Equal(width, result.Width);
        Assert.Equal(height, result.Height);
        Assert.Equal(expectedRed, Enumerable.Range(0, 6).Select(i => (int)result.Pixels[i * 4]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(-3)]
    public void ApplyOrientation_TreatsUnknownValuesAsNormal(int orientation)
    {
        var image = new RgbaImage(1, 1, [1, 2, 3, 4]);

        Assert.Same(image, ImageDecoder.ApplyOrientation(image, orientation));
    }

    [Fact]
    public void Decode_Jpeg_HonorsTheExifOrientationTag()
    {
        // 24×16 的四色方块图：左上红、右上绿、左下蓝、右下黄。方向 6（顺时针转 90°）后应为 16×24，
        // 原来的左下角（蓝）转到左上角。JPEG 有损，所以颜色只比对主导通道
        byte[] jpeg = EncodeQuadrantJpeg(24, 16);
        byte[] rotated = WithExifOrientation(jpeg, 6);

        RgbaImage plain = ImageDecoder.Decode(jpeg);
        RgbaImage fixedUp = ImageDecoder.Decode(rotated);

        Assert.Equal((24, 16), (plain.Width, plain.Height));
        Assert.Equal((16, 24), (fixedUp.Width, fixedUp.Height));
        AssertDominant(plain, 2, 2, red: true);
        AssertDominant(fixedUp, 2, 2, blue: true);
        AssertDominant(fixedUp, 13, 2, red: true);
    }

    private static byte[] EncodeQuadrantJpeg(int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool right = x >= width / 2;
                bool bottom = y >= height / 2;
                bitmap.SetPixel(x, y, (right, bottom) switch
                {
                    (false, false) => new SKColor(255, 0, 0),
                    (true, false) => new SKColor(0, 255, 0),
                    (false, true) => new SKColor(0, 0, 255),
                    _ => new SKColor(255, 255, 0),
                });
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
        return data.ToArray();
    }

    /// <summary>
    /// 在 JPEG 的文件头（SOI）之后插入一个只含「方向」标签的 EXIF 段。
    /// </summary>
    private static byte[] WithExifOrientation(byte[] jpeg, int orientation)
    {
        byte[] exif =
        [
            0xFF, 0xE1, 0x00, 0x22,                                     // APP1 段标记与长度（含长度字段本身共 34 字节）
            0x45, 0x78, 0x69, 0x66, 0x00, 0x00,                         // "Exif\0\0"
            0x4D, 0x4D, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08,             // TIFF 头：大端，IFD0 位于偏移 8
            0x00, 0x01,                                                 // IFD0 有 1 个条目
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01,             // 标签 0x0112（方向），类型 SHORT，个数 1
            0x00, (byte)orientation, 0x00, 0x00,                        // 方向值（高字节在前）与 2 字节填充
            0x00, 0x00, 0x00, 0x00,                                     // 没有下一个 IFD
        ];

        return [.. jpeg[..2], .. exif, .. jpeg[2..]];
    }

    private static void AssertDominant(RgbaImage image, int x, int y, bool red = false, bool blue = false)
    {
        int o = ((y * image.Width) + x) * 4;
        byte r = image.Pixels[o];
        byte b = image.Pixels[o + 2];
        if (red)
        {
            Assert.True(r > 200 && b < 80, $"({x},{y}) 应为红色，实际 R={r} B={b}");
        }

        if (blue)
        {
            Assert.True(b > 200 && r < 80, $"({x},{y}) 应为蓝色，实际 R={r} B={b}");
        }
    }
}
