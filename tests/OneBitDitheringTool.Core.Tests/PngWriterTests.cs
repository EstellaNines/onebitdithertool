namespace OneBitDitheringTool.Core.Tests;

public class PngWriterTests
{
    // PNG 文件头固定 8 字节签名 + 4 字节长度 + 4 字节 "IHDR"，其后依次是宽、高各 4 字节，
    // 因此位深在偏移 24、颜色类型在偏移 25
    private const int BitDepthOffset = 24;
    private const int ColorTypeOffset = 25;

    [Fact]
    public void OneBit_RoundTrip_KeepsPixelsAndWritesOneBitIndexedHeader()
    {
        // 宽度取 13（不是 8 的倍数），用来覆盖每行行尾补位的路径
        const int width = 13;
        const int height = 7;
        var levels = new byte[width * height];
        for (int i = 0; i < levels.Length; i++)
        {
            levels[i] = (byte)(((i * 7) ^ (i >> 2)) & 1);
        }

        using var stream = new MemoryStream();
        PngWriter.WriteOneBit(stream, width, height, levels);
        byte[] png = stream.ToArray();

        Assert.Equal(1, png[BitDepthOffset]);
        Assert.Equal(3, png[ColorTypeOffset]);

        RgbaImage decoded = TestImageIo.Decode(png);
        Assert.Equal(width, decoded.Width);
        Assert.Equal(height, decoded.Height);
        for (int i = 0; i < levels.Length; i++)
        {
            byte expected = levels[i] != 0 ? (byte)255 : (byte)0;
            Assert.Equal(expected, decoded.Pixels[i * 4]);
            Assert.Equal(expected, decoded.Pixels[(i * 4) + 1]);
            Assert.Equal(expected, decoded.Pixels[(i * 4) + 2]);
            Assert.Equal(255, decoded.Pixels[(i * 4) + 3]);
        }
    }

    [Fact]
    public void Rgba_RoundTrip_KeepsColorAndAlpha()
    {
        // 覆盖完全不透明、半透明、完全透明三类像素，以及非 4 的倍数的宽度
        const int width = 5;
        const int height = 3;
        var pixels = new byte[width * height * 4];
        for (int i = 0; i < width * height; i++)
        {
            pixels[i * 4] = (byte)(i * 17);
            pixels[(i * 4) + 1] = (byte)(255 - (i * 11));
            pixels[(i * 4) + 2] = (byte)(i * 3);
            pixels[(i * 4) + 3] = (i % 3) switch { 0 => 255, 1 => 128, _ => 0 };
        }

        var image = new RgbaImage(width, height, pixels);
        RgbaImage decoded = TestImageIo.Decode(TestImageIo.EncodeRgba(image));

        Assert.Equal(width, decoded.Width);
        Assert.Equal(height, decoded.Height);

        // 完全透明像素的 RGB 不可见，解码器可以任意处理，所以只比对 alpha 非 0 的像素
        for (int i = 0; i < width * height; i++)
        {
            Assert.Equal(image.Pixels[(i * 4) + 3], decoded.Pixels[(i * 4) + 3]);
            if (image.Pixels[(i * 4) + 3] == 0)
            {
                continue;
            }

            for (int c = 0; c < 3; c++)
            {
                Assert.Equal(image.Pixels[(i * 4) + c], decoded.Pixels[(i * 4) + c]);
            }
        }
    }

    [Fact]
    public void OneBitImage_WritePng_UsesOneBitWhenOpaqueAndRgbaWhenTransparent()
    {
        var levels = new byte[] { 0, 1, 1, 0 };

        using var opaque = new MemoryStream();
        new OneBitImage(2, 2, levels, alpha: null).WritePng(opaque);
        Assert.Equal(1, opaque.ToArray()[BitDepthOffset]);
        Assert.Equal(3, opaque.ToArray()[ColorTypeOffset]);

        using var transparent = new MemoryStream();
        new OneBitImage(2, 2, levels, new byte[] { 255, 128, 0, 255 }).WritePng(transparent);
        Assert.Equal(8, transparent.ToArray()[BitDepthOffset]);
        Assert.Equal(6, transparent.ToArray()[ColorTypeOffset]);
    }

    [Fact]
    public void WriteOneBit_RejectsMismatchedLength()
    {
        using var stream = new MemoryStream();
        Assert.Throws<ArgumentException>(() => PngWriter.WriteOneBit(stream, 3, 3, new byte[8]));
    }
}
