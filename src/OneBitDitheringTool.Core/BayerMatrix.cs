using System.Numerics;

namespace OneBitDitheringTool.Core;

/// <summary>
/// 生成 Bayer 阈值矩阵：矩阵中包含 0 到 N-1 的全部整数（N 为格数），排布使得数值相近的格子在空间上尽量分散。
/// </summary>
/// <remarks>
/// 2 的幂尺寸（含长宽不等的矩形）用 Bisqwit 文章附录 2 给出的位运算算法生成；
/// 3×3 与 5×3 是 Joel Yliluoma 手工设计的矩阵，文中提醒它们「观感可能不均匀，很少值得使用」，保留只为与原版功能对齐。
/// 来源：https://bisqwit.iki.fi/story/howto/dither/jy/#Appendix%202ThresholdMatrix
/// </remarks>
internal static class BayerMatrix
{
    private static readonly int[] Matrix3x3 = [0, 5, 2, 3, 8, 7, 6, 1, 4];

    private static readonly int[] Matrix5x3 = [0, 12, 7, 3, 9, 14, 8, 1, 5, 11, 6, 4, 10, 13, 2];

    /// <summary>
    /// 判断该尺寸能否生成 Bayer 矩阵：长宽都是 2 的幂（1×1 除外，它起不到抖动作用），或属于三个手工矩阵之一。
    /// </summary>
    /// <param name="width">矩阵宽度。</param>
    /// <param name="height">矩阵高度。</param>
    /// <returns>支持返回 <see langword="true"/>。</returns>
    public static bool IsSupported(int width, int height)
    {
        if (width <= 0 || height <= 0 || (width == 1 && height == 1))
        {
            return false;
        }

        return (BitOperations.IsPow2(width) && BitOperations.IsPow2(height))
            || (width, height) is (3, 3) or (5, 3) or (3, 5);
    }

    /// <summary>
    /// 生成矩阵，按行优先排列：下标 <c>y * width + x</c>。
    /// </summary>
    /// <param name="width">矩阵宽度。</param>
    /// <param name="height">矩阵高度。</param>
    /// <returns>新建的矩阵，元素取值 0 到 <c>width * height - 1</c>。</returns>
    /// <exception cref="ArgumentException">尺寸不受支持。</exception>
    public static int[] Generate(int width, int height)
    {
        if (!IsSupported(width, height))
        {
            throw new ArgumentException($"不支持的 Bayer 矩阵尺寸 {width}×{height}：长宽须同为 2 的幂（不含 1×1），或为 3×3、5×3、3×5。");
        }

        return (width, height) switch
        {
            (3, 3) => (int[])Matrix3x3.Clone(),
            (5, 3) => (int[])Matrix5x3.Clone(),

            // 文章只给出 5×3，没有 3×5；3×5 取其转置，这是保持同一设计意图的唯一自然做法
            (3, 5) => Transpose(Matrix5x3, 5, 3),
            _ => GenerateByBits(width, height),
        };
    }

    private static int[] Transpose(int[] matrix, int width, int height)
    {
        var result = new int[matrix.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // 原矩阵 (x, y) 处的值，放到转置后矩阵的 (y, x) 处，后者宽度为 height
                result[(x * height) + y] = matrix[(y * width) + x];
            }
        }

        return result;
    }

    private static int[] GenerateByBits(int width, int height)
    {
        int m = BitOperations.Log2((uint)width);
        int l = BitOperations.Log2((uint)height);
        var matrix = new int[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // 思路：取 y 与 (x 异或 y) 两个数，按相反的位序把它们的二进制位交错拼成一个整数。
                // 长宽不等时，两个数的位数不同，需要用 offset 累加器决定每轮从哪一边取多少位
                int v = 0;
                int offset = 0;
                int xmask = m;
                int ymask = l;

                if (m == 0 || (m > l && l != 0))
                {
                    int xc = x ^ ((y << m) >> l);
                    int yc = y;
                    for (int bit = 0; bit < m + l;)
                    {
                        ymask--;
                        v |= ((yc >> ymask) & 1) << bit++;
                        for (offset += m; offset >= l; offset -= l)
                        {
                            xmask--;
                            v |= ((xc >> xmask) & 1) << bit++;
                        }
                    }
                }
                else
                {
                    int xc = x;
                    int yc = y ^ ((x << l) >> m);
                    for (int bit = 0; bit < m + l;)
                    {
                        xmask--;
                        v |= ((xc >> xmask) & 1) << bit++;
                        for (offset += l; offset >= m; offset -= m)
                        {
                            ymask--;
                            v |= ((yc >> ymask) & 1) << bit++;
                        }
                    }
                }

                matrix[(y * width) + x] = v;
            }
        }

        return matrix;
    }
}
