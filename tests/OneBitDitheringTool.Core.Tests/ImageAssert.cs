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
    public static void Equal(RgbaImage expected, RgbaImage actual, string context)
    {
        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            Assert.Fail($"{context}：尺寸不同，期望 {expected.Width}×{expected.Height}，实际 {actual.Width}×{actual.Height}。");
        }

        int total = expected.Width * expected.Height;
        int mismatches = 0;
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

            mismatches++;
            if (samples.Count < 5)
            {
                samples.Add($"({i % expected.Width},{i / expected.Width}) 期望 [{Format(expected, o)}] 实际 [{Format(actual, o)}]");
            }
        }

        if (mismatches != 0)
        {
            Assert.Fail($"{context}：{mismatches}/{total} 个像素不一致，例如 {string.Join("; ", samples)}");
        }
    }

    private static string Format(RgbaImage image, int offset) =>
        $"{image.Pixels[offset]},{image.Pixels[offset + 1]},{image.Pixels[offset + 2]},{image.Pixels[offset + 3]}";
}
