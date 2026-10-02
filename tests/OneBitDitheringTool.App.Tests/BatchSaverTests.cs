using OneBitDitheringTool.App.Imaging;
using OneBitDitheringTool.Core;

namespace OneBitDitheringTool.App.Tests;

public sealed class BatchSaverTests : IDisposable
{
    // PNG 文件头里位深与颜色类型的偏移，见 Core.Tests 的同名常量
    private const int BitDepthOffset = 24;
    private const int ColorTypeOffset = 25;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "obdt-batch-" + Guid.NewGuid().ToString("N"));

    public BatchSaverTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task SaveAll_WritesOneBitPngsThatMatchWhatCoreComputes()
    {
        RgbaImage first = SampleImages.Noisy(60, 40);
        RgbaImage second = SampleImages.Noisy(30, 50);
        string input = NewFolder("in");
        string output = NewFolder("out");
        string a = Write(input, "a.png", first);
        string b = Write(input, "b.png", second);
        var settings = new ToolSettings { Scale = 0.5, Contrast = 0.2, Dither = new DitherSettings { Kind = DitherKind.ErrorDiffusion } };

        BatchResult result = await BatchSaver.SaveAllAsync([a, b], output, settings, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(result.Failures);
        Assert.Equal(["a.png", "b.png"], result.Saved.Select(Path.GetFileName));
        foreach ((string saved, RgbaImage source) in new[] { (result.Saved[0], first), (result.Saved[1], second) })
        {
            byte[] bytes = await File.ReadAllBytesAsync(saved, TestContext.Current.CancellationToken);
            Assert.Equal(1, bytes[BitDepthOffset]);
            Assert.Equal(3, bytes[ColorTypeOffset]);
            Assert.Equal(settings.Render(source, TestContext.Current.CancellationToken).ToRgba().Pixels, ImageDecoder.Decode(bytes).Pixels);
        }
    }

    [Fact]
    public async Task SaveAll_TransparentImagesKeepTheirAlphaAsRgba()
    {
        RgbaImage source = TestImagesWithAlpha();
        string path = Write(NewFolder("in"), "alpha.png", source);
        var settings = new ToolSettings();

        BatchResult result = await BatchSaver.SaveAllAsync([path], NewFolder("out"), settings, cancellationToken: TestContext.Current.CancellationToken);

        byte[] bytes = await File.ReadAllBytesAsync(result.Saved[0], TestContext.Current.CancellationToken);
        Assert.Equal(8, bytes[BitDepthOffset]);
        Assert.Equal(6, bytes[ColorTypeOffset]);
        RgbaImage saved = ImageDecoder.Decode(bytes);
        RgbaImage expected = settings.Render(source, TestContext.Current.CancellationToken).ToRgba();
        for (int i = 0; i < expected.Pixels.Length; i += 4)
        {
            Assert.Equal(expected.Pixels[i + 3], saved.Pixels[i + 3]);
        }
    }

    [Fact]
    public async Task SaveAll_RenamesWhenTwoInputsShareAName()
    {
        string a = Write(NewFolder("in1"), "x.png", SampleImages.Noisy(20, 20));
        string b = Write(NewFolder("in2"), "x.png", SampleImages.Noisy(20, 20));
        string output = NewFolder("out");

        BatchResult result = await BatchSaver.SaveAllAsync([a, b], output, new ToolSettings(), cancellationToken: TestContext.Current.CancellationToken);

        // 不同文件夹下的同名图片，后来的改名，而不是覆盖前一张
        Assert.Equal(["x.png", "x (2).png"], result.Saved.Select(Path.GetFileName));
        Assert.Equal(2, Directory.GetFiles(output).Length);
    }

    [Fact]
    public async Task SaveAll_NeverOverwritesAnInputImage()
    {
        // 输出文件夹选在原图所在的位置：同名的 a.png 若被覆盖，原图就永远丢了
        string folder = NewFolder("same");
        string a = Write(folder, "a.png", SampleImages.Noisy(20, 20));
        byte[] originalBytes = await File.ReadAllBytesAsync(a, TestContext.Current.CancellationToken);

        BatchResult result = await BatchSaver.SaveAllAsync([a], folder, new ToolSettings(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["a (2).png"], result.Saved.Select(Path.GetFileName));
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(a, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveAll_RefreshesAStaleExportOfTheSameName()
    {
        // 上次导出的结果（不是输入）可以被覆盖：重新导出时刷新它，与原版一致
        string output = NewFolder("out");
        string stale = Path.Combine(output, "a.png");
        await File.WriteAllBytesAsync(stale, [1, 2, 3], TestContext.Current.CancellationToken);
        string a = Write(NewFolder("in"), "a.png", SampleImages.Noisy(20, 20));

        BatchResult result = await BatchSaver.SaveAllAsync([a], output, new ToolSettings(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal([stale], result.Saved);
        Assert.Equal(20, ImageDecoder.Decode(stale).Width);
    }

    [Fact]
    public async Task SaveAll_OneBrokenImageDoesNotStopTheRest()
    {
        string input = NewFolder("in");
        string good1 = Write(input, "good1.png", SampleImages.Noisy(20, 20));
        string broken = Path.Combine(input, "broken.png");
        await File.WriteAllBytesAsync(broken, [1, 2, 3, 4, 5, 6, 7, 8], TestContext.Current.CancellationToken);
        string good2 = Write(input, "good2.png", SampleImages.Noisy(20, 20));
        string output = NewFolder("out");

        BatchResult result = await BatchSaver.SaveAllAsync([good1, broken, good2], output, new ToolSettings(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["good1.png", "good2.png"], result.Saved.Select(Path.GetFileName));
        BatchFailure failure = Assert.Single(result.Failures);
        Assert.Equal(broken, failure.Path);
        Assert.False(string.IsNullOrWhiteSpace(failure.Message));

        // 失败的那张不留下任何文件，包括临时文件
        Assert.Equal(["good1.png", "good2.png"], Directory.GetFiles(output).Select(Path.GetFileName).Order());
    }

    [Fact]
    public async Task SaveAll_ReportsProgressAndCreatesTheOutputFolder()
    {
        string input = NewFolder("in");
        string[] inputs = [Write(input, "a.png", SampleImages.Noisy(10, 10)), Write(input, "b.png", SampleImages.Noisy(10, 10)), Write(input, "c.png", SampleImages.Noisy(10, 10))];
        string output = Path.Combine(_root, "does", "not", "exist");
        var reports = new RecordingProgress();

        await BatchSaver.SaveAllAsync(inputs, output, new ToolSettings(), reports, TestContext.Current.CancellationToken);

        Assert.Equal(3, Directory.GetFiles(output).Length);
        Assert.Equal([0, 1, 2, 3], reports.Items.Select(p => p.Completed));
        Assert.All(reports.Items, p => Assert.Equal(3, p.Total));
        Assert.Equal(["a.png", "b.png", "c.png", string.Empty], reports.Items.Select(p => p.FileName));
    }

    [Fact]
    public async Task SaveAll_CanBeCancelledAndLeavesNoTemporaryFiles()
    {
        string a = Write(NewFolder("in"), "a.png", SampleImages.Noisy(20, 20));
        string output = NewFolder("out");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BatchSaver.SaveAllAsync([a], output, new ToolSettings(), cancellationToken: cts.Token));

        Assert.Empty(Directory.GetFiles(output));
    }

    private string NewFolder(string name)
    {
        string path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string Write(string folder, string name, RgbaImage image)
    {
        string path = Path.Combine(folder, name);
        using FileStream stream = File.Create(path);
        PngWriter.WriteRgba(stream, image);
        return path;
    }

    private static RgbaImage TestImagesWithAlpha()
    {
        RgbaImage plain = SampleImages.Noisy(24, 24);
        var pixels = (byte[])plain.Pixels.Clone();
        for (int i = 0; i < 24 * 24; i++)
        {
            pixels[(i * 4) + 3] = (i % 4) switch { 0 => 0, 1 => 90, 2 => 180, _ => 255 };
        }

        return new RgbaImage(24, 24, pixels);
    }

    /// <summary>
    /// 同步记录进度回报：<see cref="Progress{T}"/> 会把回调投递到别的线程，测试里次序不确定，不便断言。
    /// </summary>
    private sealed class RecordingProgress : IProgress<BatchProgress>
    {
        public List<BatchProgress> Items { get; } = [];

        public void Report(BatchProgress value) => Items.Add(value);
    }
}
