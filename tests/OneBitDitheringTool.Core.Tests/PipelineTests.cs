using System.Globalization;

namespace OneBitDitheringTool.Core.Tests;

/// <summary>
/// 整条管线（缩放、对比度、亮度、透明度加抖动）与 didder 的端到端对照。
/// 各步骤已分别验证过，这里确认它们组合起来、尤其是带透明度的缩放之后，结果依然一致。
/// </summary>
public class PipelineTests
{
    // 131×89：缩到 60、33、20 宽时缩放比都不是整数，会让 Box 滤波的边缘权重各不相同
    private static readonly RgbaImage Source = TestImages.Plasma(131, 89);

    /// <summary>
    /// 参数依次为：算法、目标宽度（0 表示不缩放）、对比度、亮度、是否叠加透明度、抖动强度。
    /// </summary>
    public static TheoryData<string, int, double, double, bool, double> Cases => new()
    {
        { "bayer 8x8", 60, 0.3, -0.1, false, 1.0 },
        { "edm FloydSteinberg serpentine", 77, -0.4, 0.2, false, 0.8 },
        { "odm ClusteredDot6x6", 97, 0.0, 0.35, false, 1.0 },
        { "bayer 4x4", 50, 0.0, 0.0, true, 1.0 },
        { "edm Atkinson", 0, 0.5, -0.3, true, 1.0 },
        { "bayer 16x16", 33, -0.7, 0.6, false, -0.6 },
        { "odm ClusteredDotDiagonal8x8", 131, 0.2, 0.0, false, 1.0 },
        { "edm Sierra serpentine", 20, 0.0, 0.0, false, 1.0 },
        { "edm StevenPigeon", 45, 0.15, 0.1, true, 0.7 },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void FullPipeline_MatchesDidder(string algorithm, int targetWidth, double contrast, double brightness, bool withAlpha, double strength)
    {
        Assert.SkipUnless(DidderOracle.IsAvailable, DidderOracle.SkipReason);

        RgbaImage source = withAlpha ? TestImages.WithAlphaPattern(Source) : Source;

        var extra = new List<string>();
        if (targetWidth != 0)
        {
            extra.Add($"--width={targetWidth}");
        }

        if (contrast != 0)
        {
            extra.Add(DidderOracle.Flag("contrast", contrast));
        }

        if (brightness != 0)
        {
            extra.Add(DidderOracle.Flag("brightness", brightness));
        }

        (string[] command, IDitherer ditherer) = Build(algorithm, (float)strength);
        RgbaImage expected = DidderOracle.Run(source, TestPipeline.BlackWhiteFlags(strength, [.. extra]), command);

        OneBitImage actual = TestPipeline.Dither(
            ditherer,
            source,
            new PrepareOptions { TargetWidth = targetWidth, Contrast = contrast, Brightness = brightness });

        ImageAssert.Equal(
            expected,
            actual.ToRgba(),
            string.Create(CultureInfo.InvariantCulture, $"{algorithm} 宽={targetWidth} 对比度={contrast} 亮度={brightness} 透明度={withAlpha} 强度={strength}"));
    }

    /// <summary>
    /// 把「算法描述」翻译成 didder 的子命令参数，以及与之对应的 Core 抖动器。
    /// </summary>
    private static (string[] Command, IDitherer Ditherer) Build(string algorithm, float strength)
    {
        string[] parts = algorithm.Split(' ');
        switch (parts[0])
        {
            case "bayer":
                {
                    string[] size = parts[1].Split('x');
                    return (["bayer", parts[1]], OrderedDitherer.Bayer(int.Parse(size[0], CultureInfo.InvariantCulture), int.Parse(size[1], CultureInfo.InvariantCulture), strength));
                }

            case "odm":
                return (["odm", parts[1]], OrderedDitherer.FromMatrix(OrderedMatrices.Get(parts[1]), strength));

            case "edm":
                {
                    bool serpentine = parts.Length > 2 && parts[2] == "serpentine";
                    string[] command = serpentine ? ["edm", "--serpentine", parts[1]] : ["edm", parts[1]];
                    return (command, ErrorDiffusionDitherer.Create(ErrorDiffusionKernels.Get(parts[1]), strength, serpentine));
                }

            default:
                throw new ArgumentException($"未知算法：{algorithm}", nameof(algorithm));
        }
    }
}
