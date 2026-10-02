using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using OneBitDitheringTool.App.Rendering;

namespace OneBitDitheringTool.App;

public partial class MainWindow
{
    // 放大倍数的上限。再大没有意义：屏幕装不下，还会因位图过大拖慢绘制
    private const int MaxZoom = 64;

    // 预览的整数缩放倍数。只用整数倍，每个图像像素恰好对应整数个屏幕像素，抖动图案才不会产生摩尔纹
    private int _zoom = 1;

    // 图像左上角在预览区内的位置，已取整到屏幕像素
    private Point _offset;

    // 「Show Original」要显示的原图位图，按需创建，换图时释放
    private WriteableBitmap? _originalBitmap;

    private bool _panning;
    private Point _panStart;
    private Point _offsetAtPanStart;

    /// <summary>当前预览的整数放大倍数。</summary>
    internal int Zoom => _zoom;

    /// <summary>图像左上角在预览区内的位置（屏幕像素）。</summary>
    internal Point ViewOffset => _offset;

    /// <summary>
    /// 按「Show Original」的勾选状态，决定预览区显示原图还是抖动结果，并更新显示。
    /// </summary>
    private void RefreshPreview()
    {
        Bitmap? bitmap = ShowOriginalCheckBox.IsChecked == true && _source is not null
            ? GetOriginalBitmap()
            : _resultBitmap;
        if (bitmap is not null)
        {
            ReplacePreview(bitmap);
        }
    }

    private WriteableBitmap GetOriginalBitmap() => _originalBitmap ??= PreviewBitmaps.FromRgba(_source!);

    /// <summary>
    /// 释放旧图的原图位图。若此刻正显示着原图，则先换上新图的原图，再释放旧的。
    /// </summary>
    private void ReleaseOriginal()
    {
        WriteableBitmap? previous = _originalBitmap;
        _originalBitmap = null;

        // 界面还在引用旧位图时释放它，下一次绘制会出错，所以必须先换上新的
        if (ShowOriginalCheckBox.IsChecked == true && _source is not null)
        {
            RefreshPreview();
        }

        previous?.Dispose();
    }

    /// <summary>
    /// 换上新的预览位图。
    /// </summary>
    /// <remarks>
    /// 位图尺寸变化时（例如拖动缩放滑杆，或在原图与结果之间切换），保持图像中心不动，
    /// 否则图像会朝左上角缩进缩出，看着像是在跳。
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

    /// <summary>
    /// 切换放大倍数，并让 <paramref name="anchor"/> 处的图像点保持在原来的屏幕位置。
    /// </summary>
    /// <remarks>
    /// 以鼠标指针为锚点缩放，放大时看的是指针所指的细节，而不是被推向画面一角。
    /// 锚点处的图像坐标为 (锚点 - 偏移) / 旧倍数；缩放后它必须仍落在锚点上，由此解出新偏移。
    /// </remarks>
    /// <param name="anchor">锚点，预览区坐标。</param>
    /// <param name="zoom">新的整数倍数，已限定在 1 到 <see cref="MaxZoom"/>。</param>
    private void ZoomAt(Point anchor, int zoom)
    {
        if (PreviewImage.Source is not Bitmap bitmap)
        {
            return;
        }

        double ratio = (double)zoom / _zoom;
        _offset = new Point(
            Math.Round(anchor.X - ((anchor.X - _offset.X) * ratio)),
            Math.Round(anchor.Y - ((anchor.Y - _offset.Y) * ratio)));
        _zoom = zoom;

        PreviewImage.Width = bitmap.PixelSize.Width * zoom;
        PreviewImage.Height = bitmap.PixelSize.Height * zoom;
        ZoomText.Text = $"Zoom: {zoom}x";
        ApplyView();
    }

    private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (PreviewImage.Source is null || !e.GetCurrentPoint(PreviewHost).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _panning = true;
        _panStart = e.GetPosition(PreviewHost);
        _offsetAtPanStart = _offset;

        // 捕获指针：拖出预览区之外时仍能继续收到移动事件
        e.Pointer.Capture(PreviewHost);
        PreviewHost.Focus();
        e.Handled = true;
    }

    private void OnPreviewPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_panning)
        {
            return;
        }

        Point delta = e.GetPosition(PreviewHost) - _panStart;
        _offset = new Point(Math.Round(_offsetAtPanStart.X + delta.X), Math.Round(_offsetAtPanStart.Y + delta.Y));
        ApplyView();
    }

    private void OnPreviewPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_panning)
        {
            return;
        }

        _panning = false;
        e.Pointer.Capture(null);
    }

    private void OnPreviewWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (PreviewImage.Source is null)
        {
            return;
        }

        int zoom = Math.Clamp(_zoom + Math.Sign(e.Delta.Y), 1, MaxZoom);
        if (zoom != _zoom)
        {
            ZoomAt(e.GetPosition(PreviewHost), zoom);
        }

        e.Handled = true;
    }
}
