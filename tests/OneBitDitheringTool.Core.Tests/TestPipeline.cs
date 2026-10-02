namespace OneBitDitheringTool.Core.Tests;

/// <summary>
/// 对照测试里反复用到的「走完整管线」与「拼 didder 参数」辅助。
/// </summary>
internal static class TestPipeline
{
    /// <summary>
    /// 对源图依次执行预处理与抖动，等同于应用里「载入图片 → 出结果」的完整流程。
    /// </summary>
    /// <param name="ditherer">抖动算法。</param>
    /// <param name="source">源图。</param>
    /// <param name="options">预处理参数，缺省为不缩放、不调对比度与亮度。</param>
    /// <returns>1-bit 结果。</returns>
    public static OneBitImage Dither(IDitherer ditherer, RgbaImage source, PrepareOptions? options = null) =>
        ditherer.Dither(Preprocessor.Prepare(source, options ?? new PrepareOptions()));

    /// <summary>
    /// 生成原版传给 didder 的全局参数：黑白两色调色板加抖动强度，以及可选的额外参数。
    /// </summary>
    /// <param name="strength">抖动强度。</param>
    /// <param name="extra">额外的全局参数，如 <c>--serpentine</c> 之外的缩放、亮度、对比度。</param>
    /// <returns>全局参数列表。</returns>
    public static List<string> BlackWhiteFlags(double strength, params string[] extra)
    {
        var flags = new List<string> { "--palette=black white", DidderOracle.Flag("strength", strength) };
        flags.AddRange(extra);
        return flags;
    }
}
