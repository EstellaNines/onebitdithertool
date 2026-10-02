namespace OneBitDitheringTool.Core;

/// <summary>
/// 有序抖动：用一张周期性重复的阈值矩阵，逐像素独立判定黑白，因此可以按行并行。
/// </summary>
public sealed class OrderedDitherer : IDitherer
{
    private readonly int _width;
    private readonly int _height;

    // 阈值矩阵预先换算成「加到线性亮度上的偏移量」，抖动时每个像素只需一次加法与一次比较
    private readonly float[] _offsets;

    private OrderedDitherer(int width, int height, float[] offsets)
    {
        _width = width;
        _height = height;
        _offsets = offsets;
    }

    /// <summary>
    /// 创建 Bayer 矩阵有序抖动。
    /// </summary>
    /// <param name="width">矩阵宽度：2 的幂，或配合高度取 3×3、5×3、3×5。</param>
    /// <param name="height">矩阵高度，规则同上。</param>
    /// <param name="strength">
    /// 抖动强度，范围 [-1, 1]。绝对值越小，被抖动的亮度区间越窄、画面对比度越高；
    /// 负值把 Bayer 矩阵偏亮的倾向翻转为偏暗；0 表示不抖动，即以线性亮度 0.5 为界的硬阈值。
    /// 注意：原版 didder 会把 0 当成「未设置」而按 1 处理，这里按数学含义处理。
    /// </param>
    /// <returns>可重复使用、线程安全的抖动器。</returns>
    /// <exception cref="ArgumentException">矩阵尺寸不受支持。</exception>
    /// <exception cref="ArgumentOutOfRangeException">强度不是有限数。</exception>
    public static OrderedDitherer Bayer(int width, int height, float strength = 1f) =>
        FromThresholds(BayerMatrix.Generate(width, height), width, height, width * height, strength);

    /// <summary>
    /// 用内置的聚点矩阵创建有序抖动器。
    /// </summary>
    /// <param name="matrix">阈值矩阵，通常取自 <see cref="OrderedMatrices"/>。</param>
    /// <param name="strength">抖动强度，含义同 <see cref="Bayer"/>。</param>
    /// <returns>可重复使用、线程安全的抖动器。</returns>
    /// <exception cref="ArgumentOutOfRangeException">强度不是有限数。</exception>
    public static OrderedDitherer FromMatrix(OrderedMatrix matrix, float strength = 1f)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        return FromThresholds(matrix.Values.AsSpan(), matrix.Width, matrix.Height, matrix.Max, strength);
    }

    /// <summary>
    /// 由阈值矩阵创建有序抖动器。
    /// </summary>
    /// <param name="matrix">阈值矩阵，行优先，元素取值应在 0 到 <paramref name="max"/> - 1 之间。</param>
    /// <param name="width">矩阵宽度。</param>
    /// <param name="height">矩阵高度。</param>
    /// <param name="max">阈值的除数。通常是格数；对角矩阵里有重复值，则是最大值加 1。</param>
    /// <param name="strength">抖动强度，含义同 <see cref="Bayer"/>。</param>
    internal static OrderedDitherer FromThresholds(ReadOnlySpan<int> matrix, int width, int height, int max, float strength)
    {
        if (!float.IsFinite(strength))
        {
            throw new ArgumentOutOfRangeException(nameof(strength), strength, "抖动强度必须是有限数。");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(max);
        if (matrix.Length != (long)width * height)
        {
            throw new ArgumentException("阈值矩阵长度必须等于 宽 × 高。", nameof(matrix));
        }

        // 下面的浮点运算全部用 float，运算顺序也固定：didder 用 float32 计算，
        // 换成 double 或调整括号都会让个别阈值差一个最低有效位，进而改变极少数像素的黑白
        float scale = 65535f * strength;
        float divisor = max;
        var offsets = new float[matrix.Length];
        for (int i = 0; i < matrix.Length; i++)
        {
            // 0.50000006f 比 0.5 大一个 float 刻度。若用 0.5，矩阵最大格的偏移恰为 scale/2，
            // 纯黑像素（线性值 0）加上 32767.5 后按偶数舍入成 32768，会被误抖成白点；多出的这一点点消除了这个误差
            offsets[i] = scale * (((float)(matrix[i] + 1) / divisor) - 0.50000006f);
        }

        return new OrderedDitherer(width, height, offsets);
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
            int matrixRow = y % _height * _width;
            int matrixColumn = 0;
            for (int x = 0; x < width; x++)
            {
                int i = rowStart + x;

                // 完全透明的像素不参与抖动：它不可见，结果也不会影响邻居（有序抖动逐像素独立）
                if (alpha is null || alpha[i] != 0)
                {
                    float shifted = linear[gray[i]] + _offsets[matrixRow + matrixColumn];
                    levels[i] = DitherMath.RoundClamp(shifted) >= DitherMath.WhiteThreshold ? (byte)1 : (byte)0;
                }

                // 用递增计数代替取模，内层循环里少一次除法
                if (++matrixColumn == _width)
                {
                    matrixColumn = 0;
                }
            }
        });

        return new OneBitImage(width, image.Height, levels, alpha);
    }
}
