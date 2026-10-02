using OneBitDitheringTool.App.Imaging;

namespace OneBitDitheringTool.App.Tests;

public sealed class ImageCollectorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "obdt-collect-" + Guid.NewGuid().ToString("N"));

    public ImageCollectorTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "frames", "nested"));
        foreach (string name in new[] { "frames/b.png", "frames/A.JPG", "frames/c.jpeg", "frames/notes.txt", "frames/d.bmp", "frames/nested/deep.png", "solo.png", "solo.gif" })
        {
            File.WriteAllBytes(Path.Combine(_root, name), []);
        }
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Folder_ExpandsToItsSupportedImagesInNameOrderWithoutRecursing()
    {
        List<string> result = ImageCollector.Collect([Path.Combine(_root, "frames")]);

        // 只收 png/jpg/jpeg；按文件名排序（不区分大小写）；不进入子文件夹 nested
        Assert.Equal(["A.JPG", "b.png", "c.jpeg"], result.Select(Path.GetFileName));
    }

    [Fact]
    public void Files_AreKeptIfSupportedAndOthersAreIgnored()
    {
        List<string> result = ImageCollector.Collect(
        [
            Path.Combine(_root, "solo.png"),
            Path.Combine(_root, "solo.gif"),
            Path.Combine(_root, "missing.png"),
        ]);

        Assert.Equal(["solo.png"], result.Select(Path.GetFileName));
    }

    [Fact]
    public void FilesAndFolders_CanBeMixedAndDuplicatesAreRemoved()
    {
        // 文件夹里的 b.png 又被单独拖入一次：只应出现一次，并保持首次出现的位置
        List<string> result = ImageCollector.Collect(
        [
            Path.Combine(_root, "solo.png"),
            Path.Combine(_root, "frames"),
            Path.Combine(_root, "frames", "b.png"),
        ]);

        Assert.Equal(["solo.png", "A.JPG", "b.png", "c.jpeg"], result.Select(Path.GetFileName));
    }

    [Fact]
    public void NothingSupported_GivesAnEmptyList()
    {
        Assert.Empty(ImageCollector.Collect([Path.Combine(_root, "solo.gif"), Path.Combine(_root, "nowhere")]));
        Assert.Empty(ImageCollector.Collect([]));
    }
}
