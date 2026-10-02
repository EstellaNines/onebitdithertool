namespace OneBitDitheringTool.Core.Tests;

public class ToolSettingsTests
{
    [Fact]
    public void Defaults_MatchTheOriginalTool()
    {
        var settings = new ToolSettings();

        Assert.Equal(1.0, settings.Scale);
        Assert.False(settings.SplitChannels);
        Assert.Equal(new ChannelWeights(0.30, 0.59, 0.11), settings.Weights);
        Assert.Equal(0.0, settings.Brightness);
        Assert.Equal(0.0, settings.Contrast);
        Assert.Equal(new DitherSettings(), settings.Dither);
    }

    [Theory]
    [InlineData(1.0, 100, 0)]
    [InlineData(0.5, 100, 50)]
    [InlineData(0.5, 101, 50)]
    [InlineData(0.01, 1000, 10)]
    [InlineData(0.29, 100, 29)]
    [InlineData(0.07, 100, 7)]
    [InlineData(0.01, 50, 1)]
    public void GetTargetWidth_FloorsAndKeepsAtLeastOnePixel(double scale, int sourceWidth, int expected)
    {
        // 0.29 与 0.07 乘以 100 在 double 里分别是 28.999… 与 7.000…01，用来确认浮点误差不会少算一个像素；
        // 0.01 × 50 = 0.5，取整得 0，但必须至少保留 1 像素
        Assert.Equal(expected, new ToolSettings { Scale = scale }.GetTargetWidth(sourceWidth));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void GetTargetWidth_RejectsInvalidScale(double scale)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ToolSettings { Scale = scale }.GetTargetWidth(100));
    }

    [Theory]
    [InlineData(131, 89, 0.5)]
    [InlineData(131, 89, 0.31)]
    [InlineData(97, 61, 0.07)]
    [InlineData(40, 300, 0.2)]
    [InlineData(300, 40, 0.01)]
    [InlineData(64, 64, 1.0)]
    public void GetOutputSize_AgreesWithWhatThePipelineActuallyProduces(int width, int height, double scale)
    {
        // 界面上显示的尺寸必须与实际产出的尺寸一致；这里用真正的管线核对换算公式
        var settings = new ToolSettings { Scale = scale };

        GrayImage prepared = Preprocessor.Prepare(TestImages.Plasma(width, height), settings.ToPrepareOptions(width));

        Assert.Equal((prepared.Width, prepared.Height), settings.GetOutputSize(width, height));
    }

    [Fact]
    public void ToPrepareOptions_UsesCustomWeightsOnlyWhenSplitChannelsIsOn()
    {
        var weights = new ChannelWeights(0.5, 0.3, 0.2);

        Assert.Null(new ToolSettings { Weights = weights }.ToPrepareOptions(100).Weights);
        Assert.Equal(weights, new ToolSettings { SplitChannels = true, Weights = weights }.ToPrepareOptions(100).Weights);
    }

    [Fact]
    public void ToPrepareOptions_AreComparedByValue()
    {
        // 缓存预处理结果要靠它：只改抖动参数时，预处理参数必须判为相等
        var a = new ToolSettings { Scale = 0.5, Contrast = 0.2 };
        ToolSettings b = a with { Dither = new DitherSettings { Strength = 0.3f } };

        Assert.Equal(a.ToPrepareOptions(100), b.ToPrepareOptions(100));
        Assert.NotEqual(a.ToPrepareOptions(100), (a with { Brightness = 0.1 }).ToPrepareOptions(100));
    }

    [Fact]
    public void Render_MatchesRunningTheStepsByHand()
    {
        RgbaImage source = TestImages.Plasma(97, 61);
        var settings = new ToolSettings
        {
            Scale = 0.6,
            Contrast = 0.2,
            Brightness = -0.1,
            Dither = new DitherSettings { Kind = DitherKind.ErrorDiffusion, ErrorKernelName = "Burkes", Serpentine = true },
        };

        OneBitImage viaSettings = settings.Render(source, TestContext.Current.CancellationToken);

        GrayImage gray = Preprocessor.Prepare(source, new PrepareOptions { TargetWidth = 58, Contrast = 0.2, Brightness = -0.1 });
        OneBitImage byHand = ErrorDiffusionDitherer.Create(ErrorDiffusionKernels.Get("Burkes"), 1f, serpentine: true).Dither(gray, TestContext.Current.CancellationToken);

        Assert.Equal(byHand.Levels, viaSettings.Levels);
    }

    [Fact]
    public void Render_FromAPreparedGrayImage_GivesTheSameResult()
    {
        RgbaImage source = TestImages.Plasma(97, 61);
        var settings = new ToolSettings { Scale = 0.5, Dither = new DitherSettings { Kind = DitherKind.Bayer, BayerWidth = 4, BayerHeight = 4 } };
        GrayImage prepared = Preprocessor.Prepare(source, settings.ToPrepareOptions(source.Width));

        Assert.Equal(
            settings.Render(source, TestContext.Current.CancellationToken).Levels,
            settings.Render(prepared, TestContext.Current.CancellationToken).Levels);
    }
}
