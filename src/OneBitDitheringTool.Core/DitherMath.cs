namespace OneBitDitheringTool.Core;

/// <summary>
/// 各抖动算法共用的数值工具。
/// </summary>
internal static class DitherMath
{
    /// <summary>
    /// 线性光亮度达到该值（含）即判为白，否则为黑。
    /// </summary>
    /// <remarks>
    /// 调色板只有黑、白两色、输入又是灰度时，「找最近的调色板颜色」就退化为比较线性亮度与中点的大小：
    /// 32768 是 16 位范围 [0, 65535] 的中点向上取整（中点 32767.5 不是整数）。
    /// 这个等价关系已由与 didder 输出的逐像素对照测试验证。
    /// </remarks>
    public const ushort WhiteThreshold = 32768;

    /// <summary>
    /// 8 位 sRGB 灰度到 16 位线性光的换算表。
    /// </summary>
    /// <remarks>
    /// 抖动必须在线性光空间里做：人眼感知的是光量，而 sRGB 值是做过 γ 编码的；
    /// 若直接在 sRGB 值上抖动，中间调会明显偏亮。线性值用 16 位而不是 8 位存储，
    /// 是因为暗部的线性值很小，8 位无法区分相邻的暗灰。
    /// </remarks>
    public static readonly ushort[] LinearLut = BuildLinearLut();

    /// <summary>
    /// 限制到 [0, 65535] 并四舍五入，恰好在两整数中间时取偶数（银行家舍入）。
    /// </summary>
    /// <remarks>
    /// 取偶而不是「五入」，是为了避免舍入方向带来系统性的偏亮，这与 didder 的行为一致。
    /// </remarks>
    /// <param name="value">待取整的值。</param>
    /// <returns>取整后的 16 位值。</returns>
    public static ushort RoundClamp(float value)
    {
        if (value < 0)
        {
            return 0;
        }

        if (value > 65535)
        {
            return 65535;
        }

        return (ushort)Math.Round((double)value, MidpointRounding.ToEven);
    }

    private static ushort[] BuildLinearLut()
    {
        var lut = new ushort[256];
        for (int i = 0; i < lut.Length; i++)
        {
            double v = i / 255.0;

            // sRGB 标准传递函数的逆变换：暗部走线性段，其余走 2.4 次幂段
            double linear = v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
            lut[i] = (ushort)Math.Round(linear * 65535.0, MidpointRounding.ToEven);
        }

        return lut;
    }
}
