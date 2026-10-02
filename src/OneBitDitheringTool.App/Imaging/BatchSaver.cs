using OneBitDitheringTool.Core;

namespace OneBitDitheringTool.App.Imaging;

/// <summary>
/// 批量保存的进度。
/// </summary>
/// <param name="Completed">已处理完的图片数（含失败的）。</param>
/// <param name="Total">图片总数。</param>
/// <param name="FileName">正在处理的文件名；全部完成时为空。</param>
public sealed record BatchProgress(int Completed, int Total, string FileName);

/// <summary>
/// 一张图片保存失败的记录。
/// </summary>
/// <param name="Path">输入图片的路径。</param>
/// <param name="Message">失败原因。</param>
public sealed record BatchFailure(string Path, string Message);

/// <summary>
/// 批量保存的结果。
/// </summary>
/// <param name="Saved">成功写出的文件路径。</param>
/// <param name="Failures">失败的图片及原因。</param>
public sealed record BatchResult(IReadOnlyList<string> Saved, IReadOnlyList<BatchFailure> Failures);

/// <summary>
/// 把一批图片用同一份参数抖动，并保存为 PNG。
/// </summary>
public static class BatchSaver
{
    /// <summary>
    /// 逐张处理并保存。单张失败不影响其余图片，失败原因记入结果。
    /// </summary>
    /// <remarks>
    /// 输出文件名取输入的文件名（不含目录与扩展名）加 <c>.png</c>。命名遵循两条规则：
    /// 一是绝不覆盖任何一张输入图片：用户可以把输出文件夹选在原图所在的位置，此时同名的 <c>a.png</c> 若被覆盖，原图就永远丢了；
    /// 二是同一批里重名（不同文件夹下的同名图片）时，后来的自动改名为 <c>名称 (2).png</c>，不会互相覆盖。
    /// 其余已存在的同名文件允许覆盖，这样重新导出时能刷新上次的结果，与原版一致。
    /// 每个文件先写到临时文件再改名，写到一半失败不会留下损坏的 PNG，也不会毁掉已有的旧结果。
    /// </remarks>
    /// <param name="inputs">输入图片路径。</param>
    /// <param name="outputFolder">输出文件夹，不存在时会创建。</param>
    /// <param name="settings">处理参数；预览所用的就是同一份，保证所见即所存。</param>
    /// <param name="progress">进度回调，可为空。</param>
    /// <param name="cancellationToken">用于取消；取消时抛出 <see cref="OperationCanceledException"/>，已写出的文件保留。</param>
    /// <returns>保存结果。</returns>
    /// <exception cref="OperationCanceledException">被取消。</exception>
    /// <exception cref="IOException">输出文件夹无法创建。</exception>
    public static async Task<BatchResult> SaveAllAsync(
        IReadOnlyList<string> inputs,
        string outputFolder,
        ToolSettings settings,
        IProgress<BatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentException.ThrowIfNullOrEmpty(outputFolder);
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(outputFolder);

        var inputPaths = new HashSet<string>(inputs.Select(Path.GetFullPath), PathNames.Comparer);
        var usedTargets = new HashSet<string>(PathNames.Comparer);
        var saved = new List<string>();
        var failures = new List<BatchFailure>();

        for (int i = 0; i < inputs.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string input = inputs[i];
            progress?.Report(new BatchProgress(i, inputs.Count, Path.GetFileName(input)));

            try
            {
                string target = ChooseTarget(outputFolder, input, inputPaths, usedTargets);
                await Task.Run(() => SaveOne(input, target, settings, cancellationToken), cancellationToken).ConfigureAwait(false);
                saved.Add(target);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                // 批处理的边界：一张图的解码、渲染或写盘失败，不该拖垮整批；记下原因，继续处理下一张
                failures.Add(new BatchFailure(input, exception.Message));
            }
        }

        progress?.Report(new BatchProgress(inputs.Count, inputs.Count, string.Empty));
        return new BatchResult(saved, failures);
    }

    private static string ChooseTarget(string folder, string input, HashSet<string> inputPaths, HashSet<string> usedTargets)
    {
        string name = Path.GetFileNameWithoutExtension(input);
        string candidate = Path.GetFullPath(Path.Combine(folder, name + ".png"));
        for (int n = 2; inputPaths.Contains(candidate) || usedTargets.Contains(candidate); n++)
        {
            candidate = Path.GetFullPath(Path.Combine(folder, $"{name} ({n}).png"));
        }

        usedTargets.Add(candidate);
        return candidate;
    }

    private static void SaveOne(string input, string target, ToolSettings settings, CancellationToken cancellationToken)
    {
        RgbaImage image = ImageDecoder.Decode(input);
        OneBitImage result = settings.Render(image, cancellationToken);

        string temp = target + ".tmp";
        try
        {
            using (FileStream stream = File.Create(temp))
            {
                result.WritePng(stream);
            }

            File.Move(temp, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }
}
