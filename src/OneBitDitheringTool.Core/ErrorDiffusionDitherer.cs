namespace OneBitDitheringTool.Core;

/// <summary>
/// 误差扩散抖动：逐像素量化为黑或白，并把量化误差按核的比例分给右侧与下方尚未处理的邻居。
/// </summary>
/// <remarks>
/// 每个像素的结果取决于之前所有像素累积下来的误差，所以只能顺序处理，无法按行并行。
/// </remarks>
public sealed class ErrorDiffusionDitherer : IDitherer
{
    private readonly int[] _dx;
    private readonly int[] _dy;
    private readonly float[] _coefficients;
    private readonly bool _serpentine;

    private ErrorDiffusionDitherer(int[] dx, int[] dy, float[] coefficients, bool serpentine)
    {
        _dx = dx;
        _dy = dy;
        _coefficients = coefficients;
        _serpentine = serpentine;
    }

    /// <summary>
    /// 创建误差扩散抖动器。
    /// </summary>
    /// <param name="kernel">误差扩散核，通常取自 <see cref="ErrorDiffusionKernels"/>。</param>
    /// <param name="strength">
    /// 抖动强度，范围 [-1, 1]，各权重整体乘以该值。小于 1 时传给邻居的误差变少，噪点减少、画面对比度升高；
    /// 0 表示完全不扩散，即以线性亮度 0.5 为界的硬阈值。
    /// 注意：原版 didder 会把 0 当成「未设置」而按 1 处理，这里按数学含义处理。
    /// </param>
    /// <param name="serpentine">
    /// 蛇形扫描：偶数行（从 0 计）改为从右向左处理，核也随之左右镜像。
    /// 可以明显减轻单向扫描产生的斜向纹理。
    /// </param>
    /// <returns>可重复使用、线程安全的抖动器。</returns>
    /// <exception cref="ArgumentOutOfRangeException">强度不是有限数。</exception>
    public static ErrorDiffusionDitherer Create(ErrorDiffusionKernel kernel, float strength = 1f, bool serpentine = false)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        if (!float.IsFinite(strength))
        {
            throw new ArgumentOutOfRangeException(nameof(strength), strength, "抖动强度必须是有限数。");
        }

        int count = kernel.Taps.Length;
        var dx = new int[count];
        var dy = new int[count];
        var coefficients = new float[count];
        for (int i = 0; i < count; i++)
        {
            dx[i] = kernel.Taps[i].Dx;
            dy[i] = kernel.Taps[i].Dy;

            // 先用 float 做一次除法得到权重，再乘强度：didder 的权重与强度都是 float32，
            // 改用 double 或调换顺序，会让个别权重差一个最低有效位，误差累积后可能改变后续大片像素
            coefficients[i] = (float)kernel.Taps[i].Weight / kernel.Denominator;
            if (strength != 1f)
            {
                coefficients[i] *= strength;
            }
        }

        return new ErrorDiffusionDitherer(dx, dy, coefficients, serpentine);
    }

    /// <inheritdoc />
    public OneBitImage Dither(GrayImage image, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        int width = image.Width;
        int height = image.Height;

        // 误差要累加到邻居的线性亮度上，所以单独保存一份 16 位线性值，而不是直接改灰度图
        var linear = new ushort[width * height];
        ushort[] lut = DitherMath.LinearLut;
        for (int i = 0; i < linear.Length; i++)
        {
            linear[i] = lut[image.Gray[i]];
        }

        var levels = new byte[linear.Length];
        int taps = _dx.Length;

        for (int y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool reversed = _serpentine && (y & 1) == 0;

            for (int step = 0; step < width; step++)
            {
                int x = reversed ? width - 1 - step : step;
                int index = (y * width) + x;

                // 完全透明的像素同样参与量化并向邻居扩散误差（与 didder 一致），只是它自己不可见
                ushort old = linear[index];
                bool white = old >= DitherMath.WhiteThreshold;
                levels[index] = white ? (byte)1 : (byte)0;

                int error = old - (white ? 65535 : 0);
                if (error == 0)
                {
                    // 没有误差可扩散；此时 old + 0 × 系数 恒等于 old，跳过不会改变任何结果
                    continue;
                }

                for (int t = 0; t < taps; t++)
                {
                    // 反向扫描时核要左右镜像，否则会把误差撒到已经处理过的像素上
                    int nx = x + (reversed ? -_dx[t] : _dx[t]);
                    int ny = y + _dy[t];
                    if ((uint)nx >= (uint)width || ny >= height)
                    {
                        continue;
                    }

                    int neighbor = (ny * width) + nx;
                    linear[neighbor] = DitherMath.RoundClamp((float)linear[neighbor] + ((float)error * _coefficients[t]));
                }
            }
        }

        return new OneBitImage(width, height, levels, image.Alpha);
    }
}
