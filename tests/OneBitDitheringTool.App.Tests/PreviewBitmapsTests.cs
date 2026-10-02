using System.Runtime.InteropServices;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using OneBitDitheringTool.App.Rendering;
using OneBitDitheringTool.Core;

namespace OneBitDitheringTool.App.Tests;

public class PreviewBitmapsTests
{
    [AvaloniaFact]
    public void FromOneBit_ProducesPremultipliedBlackAndWhiteWithAlphaKept()
    {
        // 白不透明、黑不透明、白半透明、黑完全透明
        var image = new OneBitImage(2, 2, [1, 0, 1, 0], [255, 255, 128, 0]);

        using WriteableBitmap bitmap = PreviewBitmaps.FromOneBit(image);

        // 预乘 BGRA：白色的各分量等于 alpha；黑色恒为 0；完全透明整个像素为 0
        Assert.Equal(
            [
                255, 255, 255, 255, 0, 0, 0, 255,
                128, 128, 128, 128, 0, 0, 0, 0,
            ],
            ReadBgra(bitmap));
    }

    [AvaloniaFact]
    public void FromRgba_SwapsChannelOrderAndPremultiplies()
    {
        // 第一个像素不透明：只交换 R、B 的位置；第二个半透明：各分量按 alpha 预乘并四舍五入
        var image = new RgbaImage(2, 1, [10, 20, 30, 255, 200, 100, 50, 128]);

        using WriteableBitmap bitmap = PreviewBitmaps.FromRgba(image);

        // 50×128/255=25.1→25；100×128/255=50.2→50；200×128/255=100.4→100
        Assert.Equal([30, 20, 10, 255, 25, 50, 100, 128], ReadBgra(bitmap));
    }

    [AvaloniaFact]
    public void Bitmaps_HaveTheImageSize()
    {
        using WriteableBitmap bitmap = PreviewBitmaps.FromOneBit(new OneBitImage(7, 3, new byte[21], null));

        Assert.Equal(new Avalonia.PixelSize(7, 3), bitmap.PixelSize);
    }

    /// <summary>
    /// 读出位图的像素数据（BGRA，逐行紧凑排列，去掉每行末尾可能的对齐填充）。
    /// </summary>
    private static byte[] ReadBgra(WriteableBitmap bitmap)
    {
        using ILockedFramebuffer buffer = bitmap.Lock();
        int width = bitmap.PixelSize.Width;
        int height = bitmap.PixelSize.Height;
        var result = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            Marshal.Copy(buffer.Address + (y * buffer.RowBytes), result, y * width * 4, width * 4);
        }

        return result;
    }
}
