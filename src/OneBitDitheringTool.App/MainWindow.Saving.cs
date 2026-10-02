using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using OneBitDitheringTool.App.Imaging;
using OneBitDitheringTool.Core;

namespace OneBitDitheringTool.App;

public partial class MainWindow
{
    // 窗口关闭时取消，用来中止还在后台进行的批量保存
    private readonly CancellationTokenSource _closing = new();

    private bool _saving;

    private async void OnSaveAllClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose the output folder",
                AllowMultiple = false,
            });

            string? folder = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            if (folder is not null)
            {
                await SaveAllToAsync(folder);
            }
        }
        catch (Exception exception)
        {
            // 界面事件处理的最外层：转成状态栏提示，不让异常把程序崩掉
            SetStatus($"Save failed: {exception.Message}");
        }
    }

    /// <summary>
    /// 用当前参数处理已载入的全部图片，保存到指定文件夹。
    /// </summary>
    /// <param name="folder">输出文件夹。</param>
    /// <returns>表示保存的任务；保存中或尚未载入图片时立即返回。</returns>
    internal async Task SaveAllToAsync(string folder)
    {
        if (_saving || _files.Count == 0)
        {
            return;
        }

        _saving = true;
        UpdateSaveButton();

        // 取点击瞬间的参数快照：保存期间用户再拖动滑杆，也不会让这一批的前后几张用上不同的参数
        ToolSettings settings = ReadSettings();
        string[] inputs = [.. _files];

        // 进度回调只在保存途中显示进度；全部完成的那次回报交给下面的汇总信息，免得被它盖掉
        var progress = new Progress<BatchProgress>(p =>
        {
            if (p.Completed < p.Total)
            {
                SetStatus($"Saving {p.Completed + 1} of {p.Total}: {p.FileName}");
            }
        });

        try
        {
            BatchResult result = await BatchSaver.SaveAllAsync(inputs, folder, settings, progress, _closing.Token);
            SetStatus(Summarize(result, inputs.Length, folder));
        }
        catch (OperationCanceledException)
        {
            // 窗口已关闭，没有地方可以显示结果
        }
        catch (Exception exception)
        {
            SetStatus($"Save failed: {exception.Message}");
        }
        finally
        {
            _saving = false;
            UpdateSaveButton();
        }
    }

    private void UpdateSaveButton() => SaveAllButton.IsEnabled = !_saving && _files.Count > 0;

    private static string Summarize(BatchResult result, int total, string folder)
    {
        if (result.Failures.Count == 0)
        {
            return $"Saved {result.Saved.Count} image(s) to {folder}";
        }

        BatchFailure first = result.Failures[0];
        string more = result.Failures.Count > 1 ? $" (+{result.Failures.Count - 1} more)" : string.Empty;
        return $"Saved {result.Saved.Count} of {total} image(s) to {folder}. Failed: {Path.GetFileName(first.Path)}: {first.Message}{more}";
    }
}
