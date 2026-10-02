namespace OneBitDitheringTool.Core.Tests;

public class DitherSettingsTests
{
    private static readonly GrayImage Gray = Preprocessor.Prepare(TestImages.Plasma(61, 47), new PrepareOptions());

    [Fact]
    public void Defaults_MatchTheOriginalTool()
    {
        var settings = new DitherSettings();

        // 与原版工具启动时的初始状态一致：Bayer 8×8，强度 1，聚点矩阵 ClusteredDot4x4，核 FloydSteinberg，噪声 ±0.5
        Assert.Equal(DitherKind.Bayer, settings.Kind);
        Assert.Equal(1f, settings.Strength);
        Assert.Equal((8, 8), (settings.BayerWidth, settings.BayerHeight));
        Assert.Equal("ClusteredDot4x4", settings.OrderedMatrixName);
        Assert.Equal("FloydSteinberg", settings.ErrorKernelName);
        Assert.False(settings.Serpentine);
        Assert.Equal((-0.5f, 0.5f), (settings.RandomMin, settings.RandomMax));
    }

    [Fact]
    public void CreateDitherer_ProducesTheSameResultAsBuildingItDirectly()
    {
        (DitherSettings Settings, IDitherer Direct)[] cases =
        [
            (new DitherSettings { Kind = DitherKind.Bayer, BayerWidth = 4, BayerHeight = 2, Strength = 0.7f }, OrderedDitherer.Bayer(4, 2, 0.7f)),
            (new DitherSettings { Kind = DitherKind.OrderedMatrix, OrderedMatrixName = "ClusteredDotSpiral5x5", Strength = -0.4f }, OrderedDitherer.FromMatrix(OrderedMatrices.Get("ClusteredDotSpiral5x5"), -0.4f)),
            (new DitherSettings { Kind = DitherKind.ErrorDiffusion, ErrorKernelName = "Atkinson", Serpentine = true, Strength = 0.9f }, ErrorDiffusionDitherer.Create(ErrorDiffusionKernels.Get("Atkinson"), 0.9f, true)),
            (new DitherSettings { Kind = DitherKind.Random, RandomMin = -0.3f, RandomMax = 0.4f, RandomSeed = 77 }, RandomDitherer.Create(-0.3f, 0.4f, 77)),
        ];

        foreach ((DitherSettings settings, IDitherer direct) in cases)
        {
            Assert.Equal(
                direct.Dither(Gray, TestContext.Current.CancellationToken).Levels,
                settings.CreateDitherer().Dither(Gray, TestContext.Current.CancellationToken).Levels);
        }
    }

    [Fact]
    public void Settings_AreComparedByValue()
    {
        // 值相等的设置必须判为相等，界面据此决定要不要重新计算
        var a = new DitherSettings { Kind = DitherKind.ErrorDiffusion, ErrorKernelName = "Stucki", Strength = 0.5f };
        var b = new DitherSettings { Kind = DitherKind.ErrorDiffusion, ErrorKernelName = "Stucki", Strength = 0.5f };

        Assert.Equal(a, b);
        Assert.NotEqual(a, b with { Serpentine = true });
    }

    [Fact]
    public void CreateDitherer_RejectsInvalidSettings()
    {
        Assert.Throws<ArgumentException>(() => new DitherSettings { BayerWidth = 6, BayerHeight = 6 }.CreateDitherer());
        Assert.Throws<ArgumentException>(() => new DitherSettings { Kind = DitherKind.OrderedMatrix, OrderedMatrixName = "nope" }.CreateDitherer());
        Assert.Throws<ArgumentException>(() => new DitherSettings { Kind = DitherKind.ErrorDiffusion, ErrorKernelName = "nope" }.CreateDitherer());
        Assert.Throws<ArgumentException>(() => new DitherSettings { Kind = DitherKind.Random, RandomMin = 1f, RandomMax = -1f }.CreateDitherer());
        Assert.Throws<ArgumentOutOfRangeException>(() => new DitherSettings { Kind = (DitherKind)99 }.CreateDitherer());
    }
}
