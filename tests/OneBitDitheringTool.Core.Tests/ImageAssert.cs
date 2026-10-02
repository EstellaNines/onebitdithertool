namespace OneBitDitheringTool.Core.Tests;

/// <summary>
/// 图像比对断言：失败时给出不一致像素的数量与若干样例，便于定位。
/// </summary>
internal static class ImageAssert
{
    /// <summary>
    /// 逐像素比较两幅图。完全透明（alpha 为 0）的像素 RGB 不可见，只比较 alpha。
    /// </summary>
    /// <param name="expected">期望的图像（通常来自 didder）。</param>
    /// <param name="actual">实际的图像（来自 Core）。</param>
    /// <param name="context">失败信息里附带的场景说明。</param>
    public static void Equal(RgbaImage expected, RgbaImage actual, string context) =>
        EqualExceptKnownDifferences(expected, actual, context, (_, _) => false, requireAtLeastOne: false);

    /// <summary>
    /// 逐像素比较两幅图，但允许在「已知差异」的位置上不同；其余位置必须完全一致。
    /// 用于有意偏离 didder 的少数场合（例如修正上游矩阵里的笔误），把偏差严格限定在预期的格子上。
    /// </summary>
    /// <param name="expected">期望的图像（通常来自 didder）。</param>
    /// <param name="actual">实际的图像（来自 Core）。</param>
    /// <param name="context">失败信息里附带的场景说明。</param>
    /// <param name="isKnownDifference">给定像素坐标 (x, y)，返回该处是否允许与期望不同。</param>
    /// <param name="requireAtLeastOne">
    /// 为 <see langword="true"/> 时，要求至少出现一处已知差异。
    /// 若一处也没有，说明上游可能已修复，对应的测试与偏差说明就该一并更新。
    /// </param>
    public static void EqualExceptKnownDifferences(
        RgbaImage expected,
        RgbaImage actual,
        string context,
        Func<int, int, bool> isKnownDifference,
        bool requireAtLeastOne)
    {
        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            Assert.Fail($"{context}：尺寸不同，期望 {expected.Width}×{expected.Height}，实际 {actual.Width}×{actual.Height}。");
        }

        int total = expected.Width * expected.Height;
        int unexpected = 0;
        int known = 0;
        var samples = new List<string>();
        for (int i = 0; i < total; i++)
        {
            int o = i * 4;
            byte expectedAlpha = expected.Pixels[o + 3];
            bool same = expectedAlpha == actual.Pixels[o + 3]
                && (expectedAlpha == 0
                    || (expected.Pixels[o] == actual.Pixels[o]
                        && expected.Pixels[o + 1] == actual.Pixels[o + 1]
                        && expected.Pixels[o + 2] == actual.Pixels[o + 2]));
            if (same)
            {
                continue;
            }

            int x = i % expected.Width;
            int y = i / expected.Width;
            if (isKnownDifference(x, y))
            {
                known++;
                continue;
            }

            unexpected++;
            if (samples.Count < 5)
            {
                samples.Add($"({x},{y}) 期望 [{Format(expected, o)}] 实际 [{Format(actual, o)}]");
            }
        }

        if (unexpected != 0)
        {
            Assert.Fail($"{context}：{unexpected}/{total} 个像素不一致，例如 {string.Join("; ", samples)}");
        }

        if (requireAtLeastOne && known == 0)
        {
            Assert.Fail($"{context}：与 didder 完全一致，已知差异没有出现。上游似乎已修复，请更新测试与偏差说明。");
        }
    }

    private static string Format(RgbaImage image, int offset) =>
        $"{image.Pixels[offset]},{image.Pixels[offset + 1]},{image.Pixels[offset + 2]},{image.Pixels[offset + 3]}";
}
