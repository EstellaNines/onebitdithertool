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

        ImageAssert.EqualExceptKnownDifferences(
            expected, actual, "bayer 3x5", (x, y) => x % 3 == 2 && y % 5 == 0, requireAtLeastOne: true);
    }

    /// <summary>
    /// 全部 15 张内置矩阵在强度 1 下的对照，另选几张在其他强度下再测。
    /// </summary>
    public static TheoryData<string, double> OrderedMatrixCases
    {
        get
        {
            var data = new TheoryData<string, double>();
            foreach (OrderedMatrix matrix in OrderedMatrices.All)
            {
                data.Add(matrix.Name, 1.0);
            }

            data.Add("ClusteredDot4x4", 0.5);
            data.Add("ClusteredDotDiagonal8x8", -0.8);
            data.Add("ClusteredDot8x8", 0.3);
            data.Add("ClusteredDotSpiral5x5", -1.0);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(OrderedMatrixCases))]
    public void OrderedMatrix_MatchesDidder(string name, double strength)
    {
        Assert.SkipUnless(DidderOracle.IsAvailable, DidderOracle.SkipReason);

        RgbaImage expected = DidderOracle.Run(Source, TestPipeline.BlackWhiteFlags(strength), ["odm", name]);
        RgbaImage actual = TestPipeline.Dither(OrderedDitherer.FromMatrix(OrderedMatrices.Get(name), (float)strength), Source).ToRgba();

        // 两张矩阵修正过上游的笔误，允许在被修正的格子上与 didder 不同，别处必须一致
        Func<int, int, bool> knownDifference = name switch
        {
            "ClusteredDotDiagonal6x6" => KnownDifference6x6,
            "ClusteredDotDiagonal16x16" => KnownDifference16x16,
            _ => (_, _) => false,
        };
        ImageAssert.EqualExceptKnownDifferences(expected, actual, $"odm {name} 强度={strength}", knownDifference, requireAtLeastOne: false);
    }

    [Fact]
    public void Diagonal6x6_FixedTypoCell_ShowsUpOnlyThere()
    {
        Assert.SkipUnless(DidderOracle.IsAvailable, DidderOracle.SkipReason);

        // 修正格的阈值由 8 变为 7，只在线性亮度处于 [32768, 36409) 的像素上才会改变黑白；
        // 取 sRGB 190（线性约 33745）的纯色图，每个矩阵格都会遇到这个亮度
        RgbaImage solid = TestImages.Solid(48, 48, 190);

        RgbaImage expected = DidderOracle.Run(solid, TestPipeline.BlackWhiteFlags(1.0), ["odm", "ClusteredDotDiagonal6x6"]);
        RgbaImage actual = TestPipeline.Dither(OrderedDitherer.FromMatrix(OrderedMatrices.Get("ClusteredDotDiagonal6x6")), solid).ToRgba();

        ImageAssert.EqualExceptKnownDifferences(expected, actual, "odm ClusteredDotDiagonal6x6 纯灰 190", KnownDifference6x6, requireAtLeastOne: true);
    }

    [Theory]
    [InlineData(150)]
    [InlineData(151)]
    public void Diagonal16x16_FixedTypoCells_ShowUpOnlyThere(byte gray)
    {
        Assert.SkipUnless(DidderOracle.IsAvailable, DidderOracle.SkipReason);

        // 修正格的阈值由 87 变为 88，只在线性亮度处于 [19969, 20481) 的像素上才会改变黑白，
        // 对应 sRGB 150 与 151；纯色图让每个矩阵格都遇到这个亮度
        RgbaImage solid = TestImages.Solid(48, 48, gray);

        RgbaImage expected = DidderOracle.Run(solid, TestPipeline.BlackWhiteFlags(1.0), ["odm", "ClusteredDotDiagonal16x16"]);
        RgbaImage actual = TestPipeline.Dither(OrderedDitherer.FromMatrix(OrderedMatrices.Get("ClusteredDotDiagonal16x16")), solid).ToRgba();

        ImageAssert.EqualExceptKnownDifferences(expected, actual, $"odm ClusteredDotDiagonal16x16 纯灰 {gray}", KnownDifference16x16, requireAtLeastOne: true);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(-1.0)]
    public void OrderedMatrices_SolidBlackAndWhite_StayUnchanged(double strength)
    {
        // 与 Bayer 同理：无论哪张矩阵，纯黑都不该抖出白点，纯白都不该抖出黑点
        foreach (OrderedMatrix matrix in OrderedMatrices.All)
        {
            foreach (byte value in new byte[] { 0, 255 })
            {
                RgbaImage solid = TestImages.Solid(70, 70, value);
                OneBitImage actual = TestPipeline.Dither(OrderedDitherer.FromMatrix(matrix, (float)strength), solid);

                byte expectedLevel = value == 0 ? (byte)0 : (byte)1;
                Assert.True(actual.Levels.All(l => l == expectedLevel), $"{matrix.Name} 强度={strength}：纯色 {value} 出现了相反颜色的像素");
            }
        }
    }

    // 6×6 对角矩阵被修正的格子：第 3 行第 5 列
    private static bool KnownDifference6x6(int x, int y) => x % 6 == 5 && y % 6 == 3;

    // 16×16 对角矩阵被修正的两个格子：第 3 行第 8 列，及其镜像（第 11 行第 0 列）
    private static bool KnownDifference16x16(int x, int y) =>
        (x % 16 == 8 && y % 16 == 3) || (x % 16 == 0 && y % 16 == 11);

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
