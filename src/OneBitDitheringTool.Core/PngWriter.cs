using System.Buffers.Binary;
using System.IO.Compression;

namespace OneBitDitheringTool.Core;

/// <summary>
/// 极简 PNG 编码器，只支持本工具需要的两种输出：1 位深黑白索引图，以及 8 位 RGBA。
/// </summary>
/// <remarks>
/// 自行编码而不引入图像库，是因为主流库（包括 Skia）都不能直接写出 1 位深索引 PNG，
/// 而这正是「真 1-bit」输出所需要的格式；PNG 规范本身很简单，自写的成本很低。
/// 逐行滤波固定使用 None：抖动图的像素分布近似随机，别的滤波器对压缩率的帮助很小。
/// </remarks>
public static class PngWriter
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    // 调色板：下标 0 为黑，下标 1 为白，与 OneBitImage.Levels 的取值一一对应
    private static readonly byte[] BlackWhitePalette = [0, 0, 0, 255, 255, 255];

    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>
    /// 将 RGBA 位图写成 8 位 RGBA 的 PNG。
    /// </summary>
    /// <param name="stream">目标流，调用方负责关闭。</param>
    /// <param name="image">要写出的位图。</param>
    public static void WriteRgba(Stream stream, RgbaImage image)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(image);

        int stride = image.Width * 4;
        var raw = new byte[(stride + 1) * image.Height];
        for (int y = 0; y < image.Height; y++)
        {
            // 每行以滤波类型字节开头，0 表示 None，该字节已由数组零初始化
            Buffer.BlockCopy(image.Pixels, y * stride, raw, (y * (stride + 1)) + 1, stride);
        }

        WritePng(stream, image.Width, image.Height, bitDepth: 8, colorType: 6, palette: null, raw);
    }

    /// <summary>
    /// 将黑白数据写成真 1-bit 的索引 PNG（位深 1，调色板为黑、白两色）。
    /// </summary>
    /// <param name="stream">目标流，调用方负责关闭。</param>
    /// <param name="width">宽度（像素），必须大于 0。</param>
    /// <param name="height">高度（像素），必须大于 0。</param>
    /// <param name="levels">每像素一字节，0 为黑，非 0 为白；长度必须为 <c>width * height</c>。</param>
    /// <exception cref="ArgumentOutOfRangeException">宽或高不大于 0。</exception>
    /// <exception cref="ArgumentException">数据长度与宽高不符。</exception>
    public static void WriteOneBit(Stream stream, int width, int height, ReadOnlySpan<byte> levels)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (levels.Length != (long)width * height)
        {
            throw new ArgumentException("黑白数据长度必须等于 宽 × 高。", nameof(levels));
        }

        // 每行按字节对齐，行尾不足 8 位的部分补 0；位序为高位在前，这是 PNG 规范规定的
        int rowBytes = (width + 7) / 8;
        var raw = new byte[(rowBytes + 1) * height];
        for (int y = 0; y < height; y++)
        {
            int rowStart = (y * (rowBytes + 1)) + 1;
            int source = y * width;
            for (int x = 0; x < width; x++)
            {
                if (levels[source + x] != 0)
                {
                    raw[rowStart + (x >> 3)] |= (byte)(0x80 >> (x & 7));
                }
            }
        }

        WritePng(stream, width, height, bitDepth: 1, colorType: 3, BlackWhitePalette, raw);
    }

    private static void WritePng(Stream stream, int width, int height, byte bitDepth, byte colorType, byte[]? palette, byte[] filteredRows)
    {
        stream.Write(Signature);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(header[4..], (uint)height);
        header[8] = bitDepth;
        header[9] = colorType;

        // 压缩方式、滤波方式、隔行扫描三项均为 0：标准 deflate、自适应滤波框架、不隔行
        WriteChunk(stream, "IHDR"u8, header);

        if (palette is not null)
        {
            WriteChunk(stream, "PLTE"u8, palette);
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(filteredRows);
        }

        WriteChunk(stream, "IDAT"u8, compressed.GetBuffer().AsSpan(0, (int)compressed.Length));
        WriteChunk(stream, "IEND"u8, []);
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> word = stackalloc byte[4];

        BinaryPrimitives.WriteUInt32BigEndian(word, (uint)data.Length);
        stream.Write(word);
        stream.Write(type);
        stream.Write(data);

        // CRC 覆盖「类型 + 数据」，不含长度字段
        uint crc = 0xFFFFFFFF;
        crc = UpdateCrc(crc, type);
        crc = UpdateCrc(crc, data);
        BinaryPrimitives.WriteUInt32BigEndian(word, crc ^ 0xFFFFFFFF);
        stream.Write(word);
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (byte b in bytes)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
            {
                // 0xEDB88320 是 PNG 规范使用的 CRC-32 多项式（反射形式）
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
