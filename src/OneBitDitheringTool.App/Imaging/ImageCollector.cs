namespace OneBitDitheringTool.App.Imaging;

/// <summary>
/// 把用户拖入的文件与文件夹整理成待处理的图片列表。
/// </summary>
public static class ImageCollector
{
    /// <summary>
    /// 收集图片：文件夹展开为其中（不含子文件夹）受支持的图片，文件则直接收入；其余一律忽略。
    /// </summary>
    /// <remarks>
    /// 文件夹内的文件按文件名排序：目录枚举的顺序在不同系统上并不确定，排序后切图的次序才可预期。
    /// 与原版一致，只扫描文件夹的第一层，不递归子文件夹。
    /// </remarks>
    /// <param name="paths">用户拖入的文件或文件夹路径。</param>
    /// <returns>受支持的图片的完整路径，按出现次序排列，已去重。</returns>
    public static List<string> Collect(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var result = new List<string>();
        var seen = new HashSet<string>(PathNames.Comparer);

        foreach (string path in paths)
        {
            if (Directory.Exists(path))
            {
                IEnumerable<string> files = Directory.EnumerateFiles(path)
                    .Where(ImageDecoder.IsSupported)
                    .OrderBy(file => Path.GetFileName(file), StringComparer.OrdinalIgnoreCase);
                foreach (string file in files)
                {
                    Add(file);
                }
            }
            else if (File.Exists(path) && ImageDecoder.IsSupported(path))
            {
                Add(path);
            }
        }

        return result;

        void Add(string file)
        {
            string full = Path.GetFullPath(file);
            if (seen.Add(full))
            {
                result.Add(full);
            }
        }
    }
}
