namespace OneBitDitheringTool.Core;

/// <summary>
/// 抖动前的预处理管线：缩放、灰度化、对比度与亮度。
/// </summary>
/// <remarks>
/// 每一步的公式与取整方式都刻意与 didder 所用的 imaging 库（MIT 许可）保持一致，
/// 目的是让本工具的结果与原版逐像素相同；改动任何一处取整，都会让个别像素的黑白翻转。
/// </remarks>
public static class Preprocessor
{
    /// <summary>
    /// 对源图依次执行：（可选）通道混合 → （可选）Box 缩放 → 灰度化 → 对比度 → 亮度。
    /// </summary>
    /// <param name="source">源图。</param>
    /// <param name="options">预处理参数。</param>
    /// <returns>可直接交给抖动算法的灰度图。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="PrepareOptions.TargetWidth"/> 为负数。</exception>
    public static GrayImage Prepare(RgbaImage source, PrepareOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegative(options.TargetWidth);

        RgbaImage working = source;

        // 原版的 Split Channels 是先让 ImageMagick 按权重混成灰度图，再交给 didder 缩放，
        // 所以自定义权重要在缩放之前生效；默认权重则由后面 didder 自己的灰度化步骤处理
        if (options.Weights is { } weights)
        {
            working = ToGray(working, weights).ToRgba();
        }

        if (options.TargetWidth > 0)
        {
            working = ResizeBox(working, options.TargetWidth);
        }

        GrayImage gray = ToGray(working, ChannelWeights.Default);

        // didder 的次序是先对比度、后亮度；其参数是「百分比」，即滑杆值乘 100
        double contrast = options.Contrast * 100.0;
        if (contrast != 0)
        {
            ApplyLut(gray.Gray, BuildContrastLut(contrast));
        }

        double brightness = options.Brightness * 100.0;
        if (brightness != 0)
        {
            ApplyLut(gray.Gray, BuildBrightnessLut(brightness));
        }

        return gray;
    }

    /// <summary>
    /// 按权重把 RGBA 位图转为灰度图，透明度原样保留。
    /// </summary>
    internal static GrayImage ToGray(RgbaImage source, ChannelWeights weights)
    {
        int count = source.Width * source.Height;
        var gray = new byte[count];
        var alpha = new byte[count];
        bool opaque = true;

        for (int i = 0; i < count; i++)
        {
            int o = i * 4;

            // 运算顺序与 imaging 的 Grayscale 一致：先逐项相乘再从左向右相加，最后四舍五入
            double f = (weights.Red * source.Pixels[o]) + (weights.Green * source.Pixels[o + 1]) + (weights.Blue * source.Pixels[o + 2]);
            gray[i] = Clamp(f);
            alpha[i] = source.Pixels[o + 3];
            opaque &= alpha[i] == 255;
        }

        return new GrayImage(source.Width, source.Height, gray, opaque ? null : alpha);
    }

    /// <summary>
    /// 构建对比度查找表。<paramref name="percentage"/> 取值范围 [-100, 100]。
    /// </summary>
    internal static byte[] BuildContrastLut(double percentage)
    {
        percentage = Math.Min(Math.Max(percentage, -100.0), 100.0);
        double v = (100.0 + percentage) / 100.0;

        var lut = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            if (v is >= 0 and <= 1)
            {
                lut[i] = Clamp((0.5 + (((i / 255.0) - 0.5) * v)) * 255.0);
            }
            else if (v is > 1 and < 2)
            {
                lut[i] = Clamp((0.5 + (((i / 255.0) - 0.5) * (1 / (2.0 - v)))) * 255.0);
            }
            else
            {
                // v 达到 2（对比度 +100%）时曲线退化为阶跃：以 127/128 为界直接二值化
                lut[i] = (byte)((byte)((i / 255.0) + 0.5) * 255);
            }
        }

        return lut;
    }

    /// <summary>
    /// 构建亮度查找表。<paramref name="percentage"/> 取值范围 [-100, 100]。
    /// </summary>
    internal static byte[] BuildBrightnessLut(double percentage)
    {
        percentage = Math.Min(Math.Max(percentage, -100.0), 100.0);
        double shift = 255.0 * percentage / 100.0;

        var lut = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            lut[i] = Clamp(i + shift);
        }

        return lut;
    }

    /// <summary>
    /// 用 Box 滤波（区域平均）把图像缩放到指定宽度，高度按宽高比换算并四舍五入、至少 1 像素。
    /// </summary>
    internal static RgbaImage ResizeBox(RgbaImage source, int targetWidth)
    {
        int dstW = targetWidth;
        double tmpH = (double)dstW * source.Height / source.Width;
        int dstH = (int)Math.Max(1.0, Math.Floor(tmpH + 0.5));

        // 与 imaging 一致：宽高都变时先横向、后纵向，且中间结果按 8 位取整保存。
        // 这个中间取整会影响最终像素值，不能为了省内存改成浮点中间结果
        if (source.Width != dstW && source.Height != dstH)
        {
            return ResizeVertical(ResizeHorizontal(source, dstW), dstH);
        }

        if (source.Width != dstW)
        {
            return ResizeHorizontal(source, dstW);
        }

        if (source.Height != dstH)
        {
            return ResizeVertical(source, dstH);
        }

        return source;
    }

    private static RgbaImage ResizeHorizontal(RgbaImage source, int dstW)
    {
        (int[][] indices, double[][] weights) = ComputeBoxWeights(dstW, source.Width);
        var output = new byte[dstW * source.Height * 4];
        byte[] src = source.Pixels;

        Parallel.For(0, source.Height, y =>
        {
            int srcRow = y * source.Width * 4;
            int dstRow = y * dstW * 4;
            for (int x = 0; x < dstW; x++)
            {
                AccumulatePixel(src, indices[x], weights[x], srcRow, 4, output, dstRow + (x * 4));
            }
        });

        return new RgbaImage(dstW, source.Height, output);
    }

    private static RgbaImage ResizeVertical(RgbaImage source, int dstH)
    {
        (int[][] indices, double[][] weights) = ComputeBoxWeights(dstH, source.Height);
        var output = new byte[source.Width * dstH * 4];
        byte[] src = source.Pixels;
        int srcStride = source.Width * 4;

        Parallel.For(0, source.Width, x =>
        {
            for (int y = 0; y < dstH; y++)
            {
                AccumulatePixel(src, indices[y], weights[y], x * 4, srcStride, output, (y * srcStride) + (x * 4));
            }
        });

        return new RgbaImage(source.Width, dstH, output);
    }

    /// <summary>
    /// 对一个目标像素做带 alpha 加权的平均。
    /// </summary>
    /// <remarks>
    /// 颜色按 alpha 加权累加再除以总权重，等价于先预乘、平均、再反预乘，这样透明像素的颜色不会污染邻近的不透明像素。
    /// 浮点运算的顺序必须与 imaging 完全一致，否则个别像素会在取整时差 1。
    /// </remarks>
    private static void AccumulatePixel(byte[] src, int[] indices, double[] weights, int origin, int step, byte[] dst, int dstOffset)
    {
        double r = 0, g = 0, b = 0, a = 0;
        for (int k = 0; k < indices.Length; k++)
        {
            int i = origin + (indices[k] * step);
            double aw = src[i + 3] * weights[k];
            r += src[i] * aw;
            g += src[i + 1] * aw;
            b += src[i + 2] * aw;
            a += aw;
        }

        // 总权重为 0 说明区域内全是完全透明像素，保持 (0,0,0,0)
        if (a != 0)
        {
            double aInv = 1 / a;
            dst[dstOffset] = Clamp(r * aInv);
            dst[dstOffset + 1] = Clamp(g * aInv);
            dst[dstOffset + 2] = Clamp(b * aInv);
            dst[dstOffset + 3] = Clamp(a);
        }
    }

    /// <summary>
    /// 预先计算 Box 滤波的采样下标与权重：对每个目标像素，列出参与平均的源像素及其归一化权重。
    /// </summary>
    private static (int[][] Indices, double[][] Weights) ComputeBoxWeights(int dstSize, int srcSize)
    {
        const double support = 0.5; // Box 滤波核的半宽

        double du = (double)srcSize / dstSize;
        double scale = du < 1.0 ? 1.0 : du;
        double ru = Math.Ceiling(scale * support);

        var indices = new int[dstSize][];
        var weights = new double[dstSize][];
        var indexBuffer = new List<int>();
        var weightBuffer = new List<double>();

        for (int v = 0; v < dstSize; v++)
        {
            double fu = ((v + 0.5) * du) - 0.5;
            int begin = Math.Max((int)Math.Ceiling(fu - ru), 0);
            int end = Math.Min((int)Math.Floor(fu + ru), srcSize - 1);

            indexBuffer.Clear();
            weightBuffer.Clear();
            double sum = 0;
            for (int u = begin; u <= end; u++)
            {
                // Box 核：到中心距离不超过半个（缩放后的）像素则权重为 1，否则为 0
                double w = Math.Abs((u - fu) / scale) <= support ? 1.0 : 0.0;
                if (w != 0)
                {
                    sum += w;
                    indexBuffer.Add(u);
                    weightBuffer.Add(w);
                }
            }

            if (sum != 0)
            {
                for (int i = 0; i < weightBuffer.Count; i++)
                {
                    weightBuffer[i] /= sum;
                }
            }

            indices[v] = indexBuffer.ToArray();
            weights[v] = weightBuffer.ToArray();
        }

        return (indices, weights);
    }

    private static void ApplyLut(byte[] values, byte[] lut)
    {
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = lut[values[i]];
        }
    }

    /// <summary>
    /// 四舍五入并限制到 [0, 255]。与 imaging 的 clamp 一致：先加 0.5 再向零截断。
    /// </summary>
    private static byte Clamp(double x)
    {
        long v = (long)(x + 0.5);
        if (v > 255)
        {
            return 255;
        }

        return v > 0 ? (byte)v : (byte)0;
    }
}
