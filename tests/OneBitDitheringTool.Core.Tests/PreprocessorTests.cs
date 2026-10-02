namespace OneBitDitheringTool.Core.Tests;

public class PreprocessorTests
{
    /// <summary>
    /// 覆盖缩放（缩小、等宽、放大）、对比度（含 +100% 的阶跃分支）、亮度（含 ±极值）以及组合。
    /// 参数依次为：目标宽度（0 表示不缩放）、对比度、亮度。
    /// </summary>
    public static TheoryData<int, double, double> Cases => new()
    {
        { 0, 0.0, 0.0 },
        { 50, 0.0, 0.0 },
        { 33, 0.0, 0.0 },
        { 97, 0.0, 0.0 },
        { 150, 0.0, 0.0 },
        { 0, 0.35, 0.0 },
        { 0, -0.6, 0.0 },
        { 0, 0.9, 0.0 },
        { 0, 1.0, 0.0 },
        { 0, 0.0, 0.25 },
        { 0, 0.0, -0.4 },
        { 0, 0.0, 1.0 },
        { 41, 0.5, -0.2 },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Prepare_MatchesDidder(int targetWidth, double contrast, double brightness)
    {
        Assert.SkipUnless(DidderOracle.IsAvailable, DidderOracle.SkipReason);

        // 97×61 都是质数，让 Box 缩放的权重不会恰好对齐
        RgbaImage source = TestImages.Plasma(97, 61);

        var flags = new List<string> { "--palette=" + DidderOracle.GrayscalePalette };
        if (targetWidth != 0)
        {
            flags.Add($"--width={targetWidth}");
        }

        if (contrast != 0)
        {
            flags.Add(DidderOracle.Flag("contrast", contrast));
        }

        if (brightness != 0)
        {
            flags.Add(DidderOracle.Flag("brightness", brightness));
        }

        RgbaImage expected = DidderOracle.Run(source, flags, ["random", "0", "0"]);

        GrayImage prepared = Preprocessor.Prepare(source, new PrepareOptions
        {
            TargetWidth = targetWidth,
            Contrast = contrast,
            Brightness = brightness,
        });

        ImageAssert.Equal(expected, prepared.ToRgba(), $"宽度={targetWidth} 对比度={contrast} 亮度={brightness}");
    }

    [Fact]
    public void Prepare_CustomWeights_ProducesWeightedGray()
    {
        // 没有 ImageMagick 可对照，所以直接验算公式：纯红、纯绿、纯蓝像素在权重 (0.5, 0.25, 0.25) 下的灰度
        var pixels = new byte[]
        {
            255, 0, 0, 255,
            0, 255, 0, 255,
            0, 0, 255, 255,
            255, 255, 255, 255,
        };
        var source = new RgbaImage(4, 1, pixels);

        GrayImage gray = Preprocessor.Prepare(source, new PrepareOptions { Weights = new ChannelWeights(0.5, 0.25, 0.25) });

        // 128 = round(255 × 0.5)；64 = round(255 × 0.25)；最后一个像素权重和为 1，保持 255
        Assert.Equal(new byte[] { 128, 64, 64, 255 }, gray.Gray);
        Assert.Null(gray.Alpha);
    }

    [Fact]
    public void Prepare_RejectsNegativeWidth()
    {
        RgbaImage source = TestImages.Plasma(8, 8);
        Assert.Throws<ArgumentOutOfRangeException>(() => Preprocessor.Prepare(source, new PrepareOptions { TargetWidth = -1 }));
    }
}
