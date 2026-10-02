using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace OneBitDitheringTool.App.Tests;

/// <summary>
/// 读出界面位图里的像素，供测试核对「界面上到底显示了什么」。
/// </summary>
internal static class BitmapReader
{
    /// <summary>
    /// 读出位图的像素数据（BGRA，逐行紧凑排列，去掉每行末尾可能的对齐填充）。
    /// </summary>
    /// <param name="bitmap">要读取的位图。</param>
    /// <returns>像素数据，长度为 宽 × 高 × 4。</returns>
    public static byte[] ReadBgra(WriteableBitmap bitmap)
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
