namespace OneBitDitheringTool.Core.Tests;

public class OrderedDithererTests
{
    // 131×89 都是质数，且大于最大的 64×64 矩阵，能让矩阵至少完整重复一次以上
    private static readonly RgbaImage Source = TestImages.Plasma(131, 89);

    /// <summary>
    /// 参数依次为：矩阵宽、高、抖动强度。覆盖原版界面提供的所有尺寸、若干长宽不等的自定义尺寸，以及正负与小数强度。
    /// 3×5 不在此列，它与 didder 有一处已知差异，由单独的测试说明。
    /// </summary>
    public static TheoryData<int, int, double> BayerCases => new()
    {
        { 2, 2, 1.0 },
        { 3, 3, 1.0 },
        { 5, 3, 1.0 },
        { 4, 4, 1.0 },
        { 8, 8, 1.0 },
        { 16, 16, 1.0 },
        { 32, 32, 1.0 },
        { 64, 64, 1.0 },
        { 2, 4, 1.0 },
        { 4, 2, 1.0 },
        { 8, 2, 1.0 },
        { 2, 16, 1.0 },
        { 16, 4, 1.0 },
        { 32, 8, 1.0 },
        { 64, 2, 1.0 },
        { 4, 64, 1.0 },
        { 8, 8, 0.5 },
        { 4, 4, -0.7 },
        { 16, 16, 0.2 },
        { 3, 3, 0.8 },
        { 5, 3, -1.0 },
        { 2, 2, 0.35 },
    };

    [Theory]
    [MemberData(nameof(BayerCases))]
    public void Bayer_MatchesDidder(int width, int height, double strength)
    {
        Assert.SkipUnless(DidderOracle.IsAvailable, DidderOracle.SkipReason);

        RgbaImage expected = DidderOracle.Run(Source, TestPipeline.BlackWhiteFlags(strength), ["bayer", $"{width}x{height}"]);
        OneBitImage actual = TestPipeline.Dither(OrderedDitherer.Bayer(width, height, (float)strength), Source);

        ImageAssert.Equal(expected, actual.ToRgba(), $"bayer {width}x{height} 强度={strength}");
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 4)]
    [InlineData(8, 8)]
    [InlineData(64, 64)]
    public void Bayer_GrayRamp_KeepsPureBlackAndWhiteAndMatchesDidder(int width, int height)
    {
        // 高度取 64 以上，让每种矩阵的每一行阈值都参与；纯黑列必须全黑、纯白列必须全白
        RgbaImage ramp = TestImages.GrayRamp(64);
        OneBitImage actual = TestPipeline.Dither(OrderedDitherer.Bayer(width, height), ramp);
        for (int y = 0; y < 64; y++)
        {
            Assert.Equal(0, actual.Levels[y * 256]);
            Assert.Equal(1, actual.Levels[(y * 256) + 255]);
        }

        Assert.SkipUnless(DidderOracle.IsAvailable, DidderOracle.SkipReason);
        RgbaImage expected = DidderOracle.Run(ramp, TestPipeline.BlackWhiteFlags(1.0), ["bayer", $"{width}x{height}"]);
        ImageAssert.Equal(expected, actual.ToRgba(), $"bayer {width}x{height} 灰阶条");
    }

    [Theory]
    [InlineData(2, 2, 1.0)]
    [InlineData(2, 2, -1.0)]
    [InlineData(3, 3, 1.0)]
    [InlineData(4, 4, 1.0)]
    [InlineData(4, 4, -1.0)]
    [InlineData(8, 8, 1.0)]
    [InlineData(5, 3, 1.0)]
    [InlineData(16, 16, 1.0)]
    public void Bayer_SolidBlackAndWhite_StayUnchanged(int width, int height, double strength)
    {
        // 纯黑不该出现白点，纯白不该出现黑点：阈值矩阵再极端，也不能把「完全没有光」抖出光来。
        // 这依赖阈值公式里比 0.5 略大的修正量，见 OrderedDitherer.FromThresholds
        foreach (byte value in new byte[] { 0, 255 })
        {
            RgbaImage solid = TestImages.Solid(70, 70, value);
            OneBitImage actual = TestPipeline.Dither(OrderedDitherer.Bayer(width, height, (float)strength), solid);

            byte expectedLevel = value == 0 ? (byte)0 : (byte)1;
            Assert.All(actual.Levels, level => Assert.Equal(expectedLevel, level));
        }
    }

    [Fact]
    public void Bayer_TransparentPixels_MatchDidder()
    {
        Assert.SkipUnless(DidderOracle.IsAvailable, DidderOracle.SkipReason);

        // 含完全透明、半透明、不透明三类像素：透明度必须原样保留，且不影响各像素自己的黑白判定
        RgbaImage source = TestImages.WithAlphaPattern(Source);

        RgbaImage expected = DidderOracle.Run(source, TestPipeline.BlackWhiteFlags(1.0), ["bayer", "8x8"]);
        OneBitImage actual = TestPipeline.Dither(OrderedDitherer.Bayer(8, 8), source);

        Assert.NotNull(actual.Alpha);
        ImageAssert.Equal(expected, actual.ToRgba(), "bayer 8x8 含透明度");
    }

    [Fact]
    public void Bayer3x5_DiffersFromDidderOnlyAtItsTypoCell()
    {
        Assert.SkipUnless(DidderOracle.IsAvailable, DidderOracle.SkipReason);

        // didder 所用 dither 库里的 3×5 矩阵首行是 {0, 14, 16}，而 5×3 矩阵转置后应为 {0, 14, 6}：
        // 16 超出了 0..14 的取值范围（共 15 格），明显是把 6 误写成 16。
        // 本工具采用正确的转置矩阵，因此只允许在矩阵的 (x=2, y=0) 这一格对应的像素上与 didder 不同。
        RgbaImage expected = DidderOracle.Run(Source, TestPipeline.BlackWhiteFlags(1.0), ["bayer", "3x5"]);
        RgbaImage actual = TestPipeline.Dither(OrderedDitherer.Bayer(3, 5), Source).ToRgba();

        Assert.Equal(expected.Width, actual.Width);
        int differing = 0;
        for (int y = 0; y < actual.Height; y++)
        {
            for (int x = 0; x < actual.Width; x++)
            {
                int o = ((y * actual.Width) + x) * 4;
                if (expected.Pixels[o] == actual.Pixels[o])
                {
                    continue;
                }

                Assert.True(x % 3 == 2 && y % 5 == 0, $"在笔误格之外出现差异：({x},{y})");
                differing++;
            }
        }

        // 若一处差异都没有，说明 didder 已修正该笔误，本测试和对应的偏差说明就该删除
        Assert.True(differing > 0, "与 didder 完全一致：上游似乎已修复 3×5 矩阵，请更新测试与文档。");
    }

    [Fact]
    public void Bayer_ZeroStrength_IsAHardThresholdAtLinearHalf()
    {
        // 横向 0..255 灰阶条：强度为 0 时没有任何阈值偏移，结果只取决于线性亮度是否达到一半
        var pixels = new byte[256 * 4];
        for (int v = 0; v < 256; v++)
        {
            pixels[v * 4] = (byte)v;
            pixels[(v * 4) + 1] = (byte)v;
            pixels[(v * 4) + 2] = (byte)v;
            pixels[(v * 4) + 3] = 255;
        }

        OneBitImage result = TestPipeline.Dither(OrderedDitherer.Bayer(8, 8, 0f), new RgbaImage(256, 1, pixels));

        for (int v = 0; v < 256; v++)
        {
            // sRGB 188 是第一个线性亮度达到一半的灰度，见 DitherMathTests
            Assert.Equal(v >= 188 ? 1 : 0, result.Levels[v]);
        }
    }

    [Fact]
    public void Dither_SupportsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            OrderedDitherer.Bayer(8, 8).Dither(Preprocessor.Prepare(Source, new PrepareOptions()), cts.Token));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Bayer_RejectsNonFiniteStrength(float strength)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OrderedDitherer.Bayer(8, 8, strength));
    }
}
