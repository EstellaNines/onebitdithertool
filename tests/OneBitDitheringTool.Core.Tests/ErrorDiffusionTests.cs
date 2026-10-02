namespace OneBitDitheringTool.Core.Tests;

public class ErrorDiffusionTests
{
    // 131×89 都是质数，不会与任何核的宽度恰好对齐
    private static readonly RgbaImage Source = TestImages.Plasma(131, 89);

    /// <summary>
    /// 每个核的权重分子之和。大多数核之和等于分母（误差全部扩散），
    /// Atkinson（6/8）与 Steven Pigeon（12/14）则有意丢弃一部分误差。
    /// </summary>
    public static TheoryData<string, int> WeightSums => new()
    {
        { "Simple2D", 2 },
        { "FloydSteinberg", 16 },
        { "FalseFloydSteinberg", 8 },
        { "JarvisJudiceNinke", 48 },
        { "Atkinson", 6 },
        { "Stucki", 42 },
        { "Burkes", 32 },
        { "Sierra", 32 },
        { "TwoRowSierra", 16 },
        { "SierraLite", 4 },
        { "StevenPigeon", 12 },
    };

    /// <summary>
    /// 全部 11 种核，各自测普通扫描与蛇形扫描，强度 1。
    /// </summary>
    public static TheoryData<string, bool> KernelCases
    {
        get
        {
            var data = new TheoryData<string, bool>();
            foreach (ErrorDiffusionKernel kernel in ErrorDiffusionKernels.All)
            {
                data.Add(kernel.Name, false);
                data.Add(kernel.Name, true);
            }

            return data;
        }
    }

    [Fact]
    public void Catalog_HasElevenKernelsInTheOriginalOrder()
    {
        string[] expected =
        [
            "Simple2D",
            "FloydSteinberg",
            "FalseFloydSteinberg",
            "JarvisJudiceNinke",
            "Atkinson",
            "Stucki",
            "Burkes",
            "Sierra",
            "TwoRowSierra",
            "SierraLite",
            "StevenPigeon",
        ];

        Assert.Equal(expected, ErrorDiffusionKernels.All.Select(k => k.Name));
    }

    [Theory]
    [MemberData(nameof(WeightSums))]
    public void Kernel_WeightsAreWhatTheirDefinitionSays(string name, int expectedSum)
    {
        ErrorDiffusionKernel kernel = ErrorDiffusionKernels.Get(name);

        Assert.Equal(expectedSum, kernel.Taps.Sum(t => t.Weight));

        // 误差只能分给「还没处理」的邻居：下一行及以后的任意位置，或本行右侧
        Assert.All(kernel.Taps, t => Assert.True(t.Dy > 0 || (t.Dy == 0 && t.Dx > 0), $"{name} 的 ({t.Dx},{t.Dy}) 指向了已处理的像素"));
    }

    [Fact]
    public void FloydSteinberg_HasTheClassicLayout()
    {
        ErrorDiffusionTap[] taps = [.. ErrorDiffusionKernels.Get("FloydSteinberg").Taps];

        // 右 7/16；左下 3/16、正下 5/16、右下 1/16
        Assert.Equal(
            [new ErrorDiffusionTap(1, 0, 7), new ErrorDiffusionTap(-1, 1, 3), new ErrorDiffusionTap(0, 1, 5), new ErrorDiffusionTap(1, 1, 1)],
            taps);
    }

    [Theory]
    [MemberData(nameof(KernelCases))]
    public void ErrorDiffusion_MatchesDidder(string kernelName, bool serpentine)
    {
        Assert.SkipUnless(DidderOracle.IsAvailable, DidderOracle.SkipReason);

        RgbaImage expected = DidderOracle.Run(Source, TestPipeline.BlackWhiteFlags(1.0), Command(kernelName, serpentine));
        OneBitImage actual = TestPipeline.Dither(Create(kernelName, 1f, serpentine), Source);

        ImageAssert.Equal(expected, actual.ToRgba(), $"edm {kernelName} 蛇形={serpentine}");
    }

    [Theory]
    [InlineData("FloydSteinberg", 0.5, false)]
    [InlineData("FloydSteinberg", 0.8, true)]
    [InlineData("Atkinson", 0.3, false)]
    [InlineData("JarvisJudiceNinke", -0.5, true)]
    [InlineData("Sierra", 0.9, false)]
    [InlineData("StevenPigeon", 0.65, true)]
    public void ErrorDiffusion_WithStrength_MatchesDidder(string kernelName, double strength, bool serpentine)
    {
        Assert.SkipUnless(DidderOracle.IsAvailable, DidderOracle.SkipReason);

        RgbaImage expected = DidderOracle.Run(Source, TestPipeline.BlackWhiteFlags(strength), Command(kernelName, serpentine));
        OneBitImage actual = TestPipeline.Dither(Create(kernelName, (float)strength, serpentine), Source);

        ImageAssert.Equal(expected, actual.ToRgba(), $"edm {kernelName} 强度={strength} 蛇形={serpentine}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ErrorDiffusion_TransparentPixels_StillDiffuseAndMatchDidder(bool serpentine)
    {
        Assert.SkipUnless(DidderOracle.IsAvailable, DidderOracle.SkipReason);

        // 完全透明的像素在 didder 里仍会量化并向邻居扩散误差，它们不可见，但会影响周围可见像素的黑白
        RgbaImage source = TestImages.WithAlphaPattern(Source);

        RgbaImage expected = DidderOracle.Run(source, TestPipeline.BlackWhiteFlags(1.0), Command("FloydSteinberg", serpentine));
        OneBitImage actual = TestPipeline.Dither(Create("FloydSteinberg", 1f, serpentine), source);

        Assert.NotNull(actual.Alpha);
        ImageAssert.Equal(expected, actual.ToRgba(), $"edm FloydSteinberg 含透明度 蛇形={serpentine}");
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 40)]
    [InlineData(40, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 17)]
    public void ErrorDiffusion_TinyAndThinImages_MatchDidder(int width, int height)
    {
        Assert.SkipUnless(DidderOracle.IsAvailable, DidderOracle.SkipReason);

        // 宽或高为 1 时，核大部分落在图外；这些边界情形最容易出现越界或漏处理
        RgbaImage source = TestImages.Plasma(width, height);
        foreach (bool serpentine in new[] { false, true })
        {
            RgbaImage expected = DidderOracle.Run(source, TestPipeline.BlackWhiteFlags(1.0), Command("JarvisJudiceNinke", serpentine));
            OneBitImage actual = TestPipeline.Dither(Create("JarvisJudiceNinke", 1f, serpentine), source);

            ImageAssert.Equal(expected, actual.ToRgba(), $"edm JarvisJudiceNinke {width}x{height} 蛇形={serpentine}");
        }
    }

    [Fact]
    public void ErrorDiffusion_GrayRamp_MatchesDidder()
    {
        Assert.SkipUnless(DidderOracle.IsAvailable, DidderOracle.SkipReason);

        RgbaImage ramp = TestImages.GrayRamp(48);

        RgbaImage expected = DidderOracle.Run(ramp, TestPipeline.BlackWhiteFlags(1.0), Command("Stucki", serpentine: true));
        OneBitImage actual = TestPipeline.Dither(Create("Stucki", 1f, serpentine: true), ramp);

        ImageAssert.Equal(expected, actual.ToRgba(), "edm Stucki 灰阶条");
    }

    [Fact]
    public void ErrorDiffusion_SolidBlackAndWhite_StayUnchanged()
    {
        // 纯黑、纯白的量化误差为 0，不会向邻居扩散任何东西，结果必须原样保持
        foreach (ErrorDiffusionKernel kernel in ErrorDiffusionKernels.All)
        {
            foreach (byte value in new byte[] { 0, 255 })
            {
                OneBitImage actual = TestPipeline.Dither(ErrorDiffusionDitherer.Create(kernel), TestImages.Solid(30, 30, value));

                byte expectedLevel = value == 0 ? (byte)0 : (byte)1;
                Assert.True(actual.Levels.All(l => l == expectedLevel), $"{kernel.Name}：纯色 {value} 出现了相反颜色的像素");
            }
        }
    }

    [Fact]
    public void ErrorDiffusion_ZeroStrength_IsAHardThreshold()
    {
        // 强度 0 即不扩散：每个像素只看自己的线性亮度是否达到一半（sRGB 188 起为白）
        RgbaImage ramp = TestImages.GrayRamp(4);

        OneBitImage result = TestPipeline.Dither(ErrorDiffusionDitherer.Create(ErrorDiffusionKernels.Get("FloydSteinberg"), 0f), ramp);

        for (int y = 0; y < 4; y++)
        {
            for (int v = 0; v < 256; v++)
            {
                Assert.Equal(v >= 188 ? 1 : 0, result.Levels[(y * 256) + v]);
            }
        }
    }

    [Fact]
    public void Dither_SupportsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        ErrorDiffusionDitherer ditherer = ErrorDiffusionDitherer.Create(ErrorDiffusionKernels.Get("FloydSteinberg"));

        Assert.ThrowsAny<OperationCanceledException>(() => ditherer.Dither(Preprocessor.Prepare(Source, new PrepareOptions()), cts.Token));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.NegativeInfinity)]
    public void Create_RejectsNonFiniteStrength(float strength)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ErrorDiffusionDitherer.Create(ErrorDiffusionKernels.Get("FloydSteinberg"), strength));
    }

    private static ErrorDiffusionDitherer Create(string kernelName, float strength, bool serpentine) =>
        ErrorDiffusionDitherer.Create(ErrorDiffusionKernels.Get(kernelName), strength, serpentine);

    private static string[] Command(string kernelName, bool serpentine) =>
        serpentine ? ["edm", "--serpentine", kernelName] : ["edm", kernelName];
}
