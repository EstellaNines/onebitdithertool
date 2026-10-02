namespace OneBitDitheringTool.Core;

/// <summary>
/// 随机噪声抖动：给每个像素的线性亮度加一个均匀分布的随机偏移，再以一半亮度为界判定黑白。
/// </summary>
/// <remarks>
/// 噪声由「像素坐标 + 种子」经哈希得到，而不是顺序取随机数。这样结果只取决于种子，与线程调度和处理顺序无关：
/// 既能按行并行，也保证同一种子下预览与保存的结果完全一致（原版 didder 每次以时间为种子，预览与保存并不相同）。
/// </remarks>
public sealed class RandomDitherer : IDitherer
{
    private readonly float _min;
    private readonly float _max;
    private readonly ulong _seed;

    private RandomDitherer(float min, float max, ulong seed)
    {
        _min = min;
        _max = max;
        _seed = seed;
    }

    /// <summary>
    /// 创建随机噪声抖动器。
    /// </summary>
    /// <param name="min">噪声下限，以满量程为 1，通常取 -0.5。</param>
    /// <param name="max">噪声上限（不含），通常取 0.5。</param>
    /// <param name="seed">随机种子；相同种子对同一幅图总给出相同结果。</param>
    /// <returns>可重复使用、线程安全的抖动器。</returns>
    /// <remarks>
    /// 取 -0.5 与 0.5 时，一个像素变白的概率恰好等于它的线性亮度，色调还原最自然。
    /// 幅度更小则亮度到黑白概率的响应曲线更陡，对比度更高；幅度更大则曲线更平，对比度更低。
    /// 上下限不对称会整体偏亮或偏暗，例如 -0.2 与 0.5 会让画面变亮。
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">上下限不是有限数。</exception>
    /// <exception cref="ArgumentException">下限大于上限。</exception>
    public static RandomDitherer Create(float min = -0.5f, float max = 0.5f, ulong seed = 0)
    {
        if (!float.IsFinite(min))
        {
            throw new ArgumentOutOfRangeException(nameof(min), min, "噪声下限必须是有限数。");
        }

        if (!float.IsFinite(max))
        {
            throw new ArgumentOutOfRangeException(nameof(max), max, "噪声上限必须是有限数。");
        }

        if (min > max)
        {
            throw new ArgumentException("噪声下限不能大于上限。", nameof(min));
        }

        return new RandomDitherer(min, max, seed);
    }

    /// <inheritdoc />
    public OneBitImage Dither(GrayImage image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        int width = image.Width;
        var levels = new byte[width * image.Height];
        byte[] gray = image.Gray;
        byte[]? alpha = image.Alpha;
        ushort[] linear = DitherMath.LinearLut;

        Parallel.For(0, image.Height, new ParallelOptions { CancellationToken = cancellationToken }, y =>
        {
            int rowStart = y * width;
            for (int x = 0; x < width; x++)
            {
                int i = rowStart + x;

                // 完全透明的像素不可见，也不影响邻居（逐像素独立），直接跳过
                if (alpha is not null && alpha[i] == 0)
                {
                    continue;
                }

                // 表达式结构与 didder 一致：满量程 × (噪声 × 幅度 + 下限)，全程 float
                float noise = 65535f * ((UnitNoise(_seed, (ulong)i) * (_max - _min)) + _min);
                float shifted = linear[gray[i]] + noise;
                levels[i] = DitherMath.RoundClamp(shifted) >= DitherMath.WhiteThreshold ? (byte)1 : (byte)0;
            }
        });

        return new OneBitImage(width, image.Height, levels, alpha);
    }

    /// <summary>
    /// 由种子与像素序号确定性地生成 [0, 1) 内的均匀随机数，精度 24 位（与 float 的尾数位数相同）。
    /// </summary>
    /// <remarks>
    /// 使用 SplitMix64 的混合函数：对相邻的输入也能给出彼此无关的输出，且每次调用只需几次乘法与异或。
    /// </remarks>
    private static float UnitNoise(ulong seed, ulong index)
    {
        unchecked
        {
            // 0x9E3779B97F4A7C15 是黄金比例对应的 64 位奇数，让相邻序号在输入端就相隔很远
            ulong z = seed + ((index + 1) * 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;

            // 取高 24 位：float 只有 24 位尾数，再多的位数无法表示
            return (z >> 40) / 16777216f;
        }
    }
}
