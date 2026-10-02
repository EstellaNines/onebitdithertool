using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using OneBitDitheringTool.App.Imaging;
using OneBitDitheringTool.App.Rendering;
using OneBitDitheringTool.Core;

namespace OneBitDitheringTool.App;

public partial class MainWindow
{
    private static readonly FilePickerFileType ImageFileType = new("图片（png、jpg、jpeg）")
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg"],

        // macOS 的文件对话框按类型标识而不是通配符过滤，缺了这两项会让所有图片都灰掉
        AppleUniformTypeIdentifiers = ["public.png", "public.jpeg"],
        MimeTypes = ["image/png", "image/jpeg"],
    };

    private List<string> _files = [];
    private int _index;
    private RgbaImage? _source;
    private WriteableBitmap? _resultBitmap;

    // 每次发起载入就加一。解码在后台线程进行，用户快速连续切图时多个解码会同时在途，
    // 只有版本号仍是最新的那一个才有资格更新界面，否则旧图会晚到并覆盖新图
    private int _loadVersion;
    private Task _loadTask = Task.CompletedTask;

    // 新载入一批图片后，第一张渲染结果出来时把它居中显示
    private bool _centerOnNextResult;

    /// <summary>
    /// 载入一批图片并显示第一张。
    /// </summary>
    /// <param name="paths">图片文件路径，须为受支持的格式。</param>
    /// <returns>表示载入（含随后发起的渲染请求）的任务。</returns>
    internal Task LoadFilesAsync(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0)
        {
            return Task.CompletedTask;
        }

        _files = [.. paths];
        _index = 0;
        _centerOnNextResult = true;
        UpdateNavigationButtons();
        UpdateSaveButton();
        _loadTask = LoadCurrentAsync();
        return _loadTask;
    }

    /// <summary>
    /// 处理被拖入窗口的文件与文件夹：整理出受支持的图片并载入。
    /// </summary>
    /// <param name="paths">被拖入的文件或文件夹路径。</param>
    /// <returns>表示处理的任务。</returns>
    internal async Task HandleDroppedPathsAsync(IEnumerable<string> paths)
    {
        List<string> images = ImageCollector.Collect(paths);
        if (images.Count == 0)
        {
            SetStatus("拖入的内容里没有受支持的图片（png、jpg、jpeg）。");
            return;
        }

        await LoadFilesAsync(images);
    }

    /// <summary>
    /// 等待载入与渲染全部完成。主要供测试使用。
    /// </summary>
    /// <returns>表示等待的任务。</returns>
    internal async Task WhenIdleAsync()
    {
        // 载入完成时会发起渲染，所以先等载入，再等渲染
        await _loadTask;
        await _coordinator.WhenIdleAsync();
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            IReadOnlyList<IStorageFile> picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "打开图片",
                AllowMultiple = true,
                FileTypeFilter = [ImageFileType],
            });

            string[] paths = [.. picked.Select(file => file.TryGetLocalPath()).OfType<string>().Where(ImageDecoder.IsSupported)];
            if (paths.Length > 0)
            {
                await LoadFilesAsync(paths);
            }
        }
        catch (Exception exception)
        {
            // 这里是界面事件处理的最外层，不能让异常逃出去把程序崩掉：转成状态栏提示即可
            SetStatus($"打开失败：{exception.Message}");
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        try
        {
            // 路径必须在 await 之前取出：拖放数据只在事件处理期间有效，之后可能已被释放
            string[] paths = [.. (e.DataTransfer.TryGetFiles() ?? []).Select(item => item.TryGetLocalPath()).OfType<string>()];
            await HandleDroppedPathsAsync(paths);
        }
        catch (Exception exception)
        {
            SetStatus($"拖入失败：{exception.Message}");
        }
    }

    private void OnPreviousClick(object? sender, RoutedEventArgs e) => Navigate(-1);

    private void OnNextClick(object? sender, RoutedEventArgs e) => Navigate(1);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        // 先让获得焦点的控件处理：滑杆与下拉框自己要用方向键，它们处理过就不该再拿来切图
        base.OnKeyDown(e);
        if (e.Handled || _files.Count < 2)
        {
            return;
        }

        if (e.Key == Key.Right)
        {
            Navigate(1);
            e.Handled = true;
        }
        else if (e.Key == Key.Left)
        {
            Navigate(-1);
            e.Handled = true;
        }
    }

    /// <summary>
    /// 切到前一张或后一张，首尾相接。
    /// </summary>
    /// <param name="delta">-1 为前一张，1 为后一张。</param>
    private void Navigate(int delta)
    {
        if (_files.Count < 2)
        {
            return;
        }

        _index = (_index + delta + _files.Count) % _files.Count;
        _loadTask = LoadCurrentAsync();
    }

    private async Task LoadCurrentAsync()
    {
        int version = ++_loadVersion;
        string path = _files[_index];
        SetStatus($"正在载入 {Path.GetFileName(path)}…");

        RgbaImage image;
        try
        {
            image = await Task.Run(() => ImageDecoder.Decode(path));
        }
        catch (Exception exception)
        {
            // 单张图片损坏、被占用或内存不足都不该让程序崩溃；这是该图片的失败，如实告知即可
            if (version == _loadVersion)
            {
                ClearImage();
                SetStatus($"无法打开 {Path.GetFileName(path)}：{exception.Message}");
            }

            return;
        }

        if (version != _loadVersion)
        {
            return;
        }

        _source = image;
        ReleaseOriginal();
        UpdateInfoText();
        RequestRender();
    }

    /// <summary>
    /// 请求用当前控件的取值重新渲染预览；尚未载入图片时什么也不做。
    /// </summary>
    private void RequestRender()
    {
        if (_source is null)
        {
            return;
        }

        UpdateSizeText();
        SetStatus("正在渲染…");
        _coordinator.Request(_source, ReadSettings());
    }

    private void OnRenderCompleted(RenderOutcome outcome)
    {
        // 渲染期间可能已经换了一张图，此时这份结果属于旧图，直接丢弃
        if (!ReferenceEquals(outcome.Source, _source))
        {
            return;
        }

        if (outcome.Error is not null)
        {
            SetStatus($"出错：{outcome.Error.Message}");
            return;
        }

        ShowResult(outcome.Result!);
        SetStatus($"完成，用时 {outcome.Elapsed.TotalMilliseconds:0} 毫秒");
    }

    private void ShowResult(OneBitImage result)
    {
        WriteableBitmap? previous = _resultBitmap;
        _resultBitmap = PreviewBitmaps.FromOneBit(result);

        // 先让界面换上新位图，再释放旧的：界面还在引用旧位图时释放它，下一次绘制会出错。
        // 若此刻显示的是原图，这次刷新不会动界面，旧结果位图本来就没在用
        RefreshPreview();
        previous?.Dispose();
    }

    private void ClearImage()
    {
        _source = null;
        PreviewImage.Source = null;
        _resultBitmap?.Dispose();
        _resultBitmap = null;
        _originalBitmap?.Dispose();
        _originalBitmap = null;
        EmptyHint.IsVisible = true;
        ImageCountText.Text = ImageCountLabel();
        FileNameText.Text = string.Empty;
        SizeText.Text = string.Empty;
    }

    private void UpdateInfoText()
    {
        string name = Path.GetFileName(_files[_index]);
        ImageCountText.Text = ImageCountLabel();
        FileNameText.Text = name;
        Title = $"OneBitDitheringTool - {name}";
        UpdateSizeText();
    }

    private string ImageCountLabel() => $"第 {_index + 1} 张 / 共 {_files.Count} 张";

    private void UpdateNavigationButtons() => PreviousButton.IsEnabled = NextButton.IsEnabled = _files.Count > 1;

    private void UpdateSizeText()
    {
        if (_source is null)
        {
            SizeText.Text = string.Empty;
            return;
        }

        // 显示处理之后的尺寸（含缩放比例），与实际产出完全一致，换算规则在 ToolSettings 中统一维护
        (int width, int height) = ReadSettings().GetOutputSize(_source.Width, _source.Height);
        SizeText.Text = $"尺寸：{width}×{height}";
    }

    private void SetStatus(string text) => StatusText.Text = text;
}
