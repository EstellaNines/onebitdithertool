namespace OneBitDitheringTool.Core.Tests;

public class RandomDithererTests
{
    // 噪声无法与 didder 逐像素对照（两边的随机数发生器不同），所以改为统计检验。
    // 400×400 共 16 万个像素，白点占比的标准差不超过 0.13%，下面 1.5% 的容差约为 11 个标准差，不会误报
    private const double Tolerance = 0.015;

    /// <summary>
    /// 参数依次为：噪声下限、上限、灰度。覆盖标准对称范围、变窄（对比度升高）、变宽（对比度降低）、整体偏移（偏亮、偏暗）。
    /// </summary>
    public static TheoryData<float, float, byte> StatisticalCases => new()
    {
        { -0.5f, 0.5f, 64 },
        { -0.5f, 0.5f, 128 },
        { -0.5f, 0.5f, 188 },
        { -0.5f, 0.5f, 230 },
        { -0.2f, 0.2f, 140 },
        { -0.2f, 0.2f, 180 },
        { -0.8f, 0.8f, 128 },
        { -0.8f, 0.8f, 220 },
        { -0.2f, 0.5f, 128 },
        { -0.5f, 0.2f, 190 },
    };

    [Theory]
    [MemberData(nameof(StatisticalCases))]
    public void WhiteFraction_FollowsTheNoiseRange(float min, float max, byte gray)
    {
        RgbaImage solid = TestImages.Solid(400, 400, gray);

        OneBitImage result = TestPipeline.Dither(RandomDitherer.Create(min, max, seed: 12345), solid);

        double actual = result.Levels.Count(l => l == 1) / (double)result.Levels.Length;
        Assert.InRange(actual, ExpectedWhiteFraction(min, max, gray) - Tolerance, ExpectedWhiteFraction(min, max, gray) + Tolerance);
    }

    [Fact]
    public void SameSeed_GivesIdenticalResultsAndDifferentSeedsDiffer()
    {
        RgbaImage source = TestImages.Plasma(97, 61);
        GrayImage gray = Preprocessor.Prepare(source, new PrepareOptions());

        OneBitImage first = RandomDitherer.Create(seed: 7).Dither(gray, TestContext.Current.CancellationToken);
        OneBitImage second = RandomDitherer.Create(seed: 7).Dither(gray, TestContext.Current.CancellationToken);
        OneBitImage other = RandomDitherer.Create(seed: 8).Dither(gray, TestContext.Current.CancellationToken);

        // 并行执行也不能影响结果：噪声只取决于种子与像素位置
        Assert.Equal(first.Levels, second.Levels);
        Assert.NotEqual(first.Levels, other.Levels);
    }

    [Fact]
    public void SolidBlackAndWhite_StayUnchangedWithTheStandardRange()
    {
        // 标准范围下噪声幅度恰好覆盖半个满量程：纯黑最多被推到 32767.5 以下，纯白最少被拉到 32767.5，
        // 因此不会出现反色像素
        foreach (byte value in new byte[] { 0, 255 })
        {
            OneBitImage result = TestPipeline.Dither(RandomDitherer.Create(seed: 99), TestImages.Solid(300, 300, value));

            byte expectedLevel = value == 0 ? (byte)0 : (byte)1;
            Assert.True(result.Levels.All(l => l == expectedLevel), $"纯色 {value} 出现了相反颜色的像素");
        }
    }

    [Theory]
    [InlineData(-0.5, 0.5)]
    [InlineData(-0.2, 0.2)]
    [InlineData(-0.8, 0.8)]
    public void WhiteFraction_MatchesDidderStatistically(double min, double max)
    {
        Assert.SkipUnless(DidderOracle.IsAvailable, DidderOracle.SkipReason);

        // 两边的随机数不同，逐像素不可能一致；但同一参数下，整幅图的白点占比应当几乎相同。
        // 这能验证噪声的幅度、偏移与量纲和 didder 的约定一致
        RgbaImage source = TestImages.Plasma(300, 300);
        RgbaImage expected = DidderOracle.Run(
            source,
            TestPipeline.BlackWhiteFlags(1.0),
            ["random", min.ToString("R", System.Globalization.CultureInfo.InvariantCulture), max.ToString("R", System.Globalization.CultureInfo.InvariantCulture)]);
        OneBitImage actual = TestPipeline.Dither(RandomDitherer.Create((float)min, (float)max, seed: 1), source);

        int total = source.Width * source.Height;
        double didderFraction = Enumerable.Range(0, total).Count(i => expected.Pixels[i * 4] == 255) / (double)total;
        double ourFraction = actual.Levels.Count(l => l == 1) / (double)total;

        Assert.InRange(ourFraction, didderFraction - Tolerance, didderFraction + Tolerance);
    }

    [Fact]
    public void TransparentPixels_AreSkippedAndAlphaIsKept()
    {
        RgbaImage source = TestImages.WithAlphaPattern(TestImages.Solid(40, 40, 128));

        OneBitImage result = TestPipeline.Dither(RandomDitherer.Create(seed: 5), source);

        Assert.NotNull(result.Alpha);
        for (int i = 0; i < result.Levels.Length; i++)
        {
            Assert.Equal(source.Pixels[(i * 4) + 3], result.Alpha[i]);
            if (result.Alpha[i] == 0)
            {
                Assert.Equal(0, result.Levels[i]);
            }
        }
    }

    [Fact]
    public void Dither_SupportsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            RandomDitherer.Create().Dither(Preprocessor.Prepare(TestImages.Plasma(64, 64), new PrepareOptions()), cts.Token));
    }

    [Fact]
    public void Create_ValidatesTheRange()
    {
        Assert.Throws<ArgumentException>(() => RandomDitherer.Create(0.3f, -0.3f));
        Assert.Throws<ArgumentOutOfRangeException>(() => RandomDitherer.Create(float.NaN, 0.5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => RandomDitherer.Create(-0.5f, float.PositiveInfinity));
    }

    /// <summary>
    /// 理论上的白点占比：像素变白当且仅当「线性亮度 + 噪声」四舍五入后不小于 32768，
    /// 而噪声在 [min × 65535, max × 65535) 内均匀分布，所以占比是一段被截断在 [0, 1] 的线性函数。
    /// </summary>
    private static double ExpectedWhiteFraction(float min, float max, byte gray)
    {
        double linear = DitherMath.LinearLut[gray];
        double lowNoise = min * 65535.0;
        double highNoise = max * 65535.0;

        // 需要噪声至少为 32767.5 - linear 才能变白
        double needed = 32767.5 - linear;
        return Math.Clamp((highNoise - needed) / (highNoise - lowNoise), 0.0, 1.0);
    }
}
