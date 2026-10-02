using OneBitDitheringTool.Core;

namespace OneBitDitheringTool.App.Tests;

/// <summary>
/// 测试用的临时图片文件：创建时写入磁盘，释放时删除。
/// </summary>
internal sealed class TempImageFile : IDisposable
{
    /// <summary>
    /// 把图像写成临时 PNG 文件。
    /// </summary>
    /// <param name="image">要写入的图像。</param>
    /// <param name="name">文件名（不含目录）；缺省用随机名。</param>
    public TempImageFile(RgbaImage image, string? name = null)
    {
        Directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "obdt-test-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
        Path = System.IO.Path.Combine(Directory, name ?? "image.png");
        using FileStream stream = File.Create(Path);
        PngWriter.WriteRgba(stream, image);
    }

    /// <summary>文件所在的临时目录。</summary>
    public string Directory { get; }

    /// <summary>文件的完整路径。</summary>
    public string Path { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        System.IO.Directory.Delete(Directory, recursive: true);
    }
}
