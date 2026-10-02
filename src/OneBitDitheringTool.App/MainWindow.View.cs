using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace OneBitDitheringTool.App;

public partial class MainWindow
{
    // 预览的整数缩放倍数。只用整数倍，每个图像像素恰好对应整数个屏幕像素，抖动图案才不会产生摩尔纹
    private int _zoom = 1;

    // 图像左上角在预览区内的位置，已取整到屏幕像素
    private Point _offset;

    /// <summary>
    /// 换上新的预览位图。
    /// </summary>
    /// <remarks>
    /// 位图尺寸变化时（例如拖动缩放滑杆），保持图像中心不动，否则图像会朝左上角缩进缩出，看着像是在跳。
    /// </remarks>
    /// <param name="bitmap">要显示的位图。</param>
    private void ReplacePreview(Bitmap bitmap)
    {
        double oldWidth = PreviewImage.Width;
        double oldHeight = PreviewImage.Height;

        PreviewImage.Source = bitmap;
        PreviewImage.Width = bitmap.PixelSize.Width * _zoom;
        PreviewImage.Height = bitmap.PixelSize.Height * _zoom;
        EmptyHint.IsVisible = false;

        if (_centerOnNextResult)
        {
            _centerOnNextResult = false;
            CenterImage();
        }
        else if (!double.IsNaN(oldWidth) && !double.IsNaN(oldHeight))
        {
            _offset = new Point(
                Math.Round(_offset.X + ((oldWidth - PreviewImage.Width) / 2)),
                Math.Round(_offset.Y + ((oldHeight - PreviewImage.Height) / 2)));
            ApplyView();
        }
        else
        {
            ApplyView();
        }
    }

    private void CenterImage()
    {
        Size host = PreviewHost.Bounds.Size;
        _offset = new Point(
            Math.Round((host.Width - PreviewImage.Width) / 2),
            Math.Round((host.Height - PreviewImage.Height) / 2));
        ApplyView();
    }

    private void ApplyView()
    {
        Canvas.SetLeft(PreviewImage, _offset.X);
        Canvas.SetTop(PreviewImage, _offset.Y);
    }
}
