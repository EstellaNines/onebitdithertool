using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using OneBitDitheringTool.App.Controls;
using OneBitDitheringTool.App.Imaging;
using OneBitDitheringTool.Core;

namespace OneBitDitheringTool.App.Tests;

public class MainWindowSavingTests
{
    [AvaloniaFact]
    public async Task SaveAllButton_IsEnabledOnlyOnceImagesAreLoaded()
    {
        var window = new MainWindow();
        window.Show();
        Assert.False(Find<Button>(window, "SaveAllButton").IsEnabled);

        using var file = new TempImageFile(SampleImages.Noisy(20, 20));
        await window.LoadFilesAsync([file.Path]);
        await window.WhenIdleAsync();

        Assert.True(Find<Button>(window, "SaveAllButton").IsEnabled);

        window.Close();
    }

    [AvaloniaFact]
    public async Task SaveAll_AppliesTheCurrentControlsToEveryImage()
    {
        RgbaImage first = SampleImages.Noisy(60, 40);
        RgbaImage second = SampleImages.Noisy(40, 60);
        using var a = new TempImageFile(first, "first.png");
        using var b = new TempImageFile(second, "second.png");
        string output = Path.Combine(Path.GetTempPath(), "obdt-save-" + Guid.NewGuid().ToString("N"));
        var window = new MainWindow();
        window.Show();
        await window.LoadFilesAsync([a.Path, b.Path]);
        await window.WhenIdleAsync();
        Find<ParameterSlider>(window, "ScaleSlider").Value = 0.5;
        Find<ParameterSlider>(window, "ContrastSlider").Value = 0.3;
        Find<ComboBox>(window, "DitherTypeComboBox").SelectedIndex = 2;
        await window.WhenIdleAsync();

        try
        {
            await window.SaveAllToAsync(output);

            // 保存出来的每一张，都必须与「当前控件取值下 Core 的结果」逐像素相同，也就是与预览所见一致
            ToolSettings settings = window.ReadSettings();
            foreach ((string name, RgbaImage source) in new[] { ("first.png", first), ("second.png", second) })
            {
                RgbaImage saved = ImageDecoder.Decode(Path.Combine(output, name));
                Assert.Equal(settings.Render(source, TestContext.Current.CancellationToken).ToRgba().Pixels, saved.Pixels);
            }

            Assert.Equal($"Saved 2 image(s) to {output}", Find<TextBlock>(window, "StatusText").Text);
            Assert.True(Find<Button>(window, "SaveAllButton").IsEnabled);
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }

            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task SaveAll_ReportsAFailureInTheStatusBarButStillSavesTheRest()
    {
        using var good = new TempImageFile(SampleImages.Noisy(20, 20), "good.png");
        string broken = Path.Combine(good.Directory, "broken.png");
        await File.WriteAllBytesAsync(broken, [1, 2, 3, 4, 5, 6, 7, 8], TestContext.Current.CancellationToken);
        string output = Path.Combine(Path.GetTempPath(), "obdt-save-" + Guid.NewGuid().ToString("N"));
        var window = new MainWindow();
        window.Show();
        await window.LoadFilesAsync([good.Path, broken]);
        await window.WhenIdleAsync();

        try
        {
            await window.SaveAllToAsync(output);

            string status = Find<TextBlock>(window, "StatusText").Text!;
            Assert.StartsWith("Saved 1 of 2 image(s)", status);
            Assert.Contains("Failed: broken.png", status);
            Assert.True(File.Exists(Path.Combine(output, "good.png")));
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }

            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task SaveAll_DoesNothingWithoutImages()
    {
        var window = new MainWindow();
        window.Show();
        string output = Path.Combine(Path.GetTempPath(), "obdt-save-" + Guid.NewGuid().ToString("N"));

        await window.SaveAllToAsync(output);

        Assert.False(Directory.Exists(output));

        window.Close();
    }

    private static T Find<T>(MainWindow window, string name)
        where T : Control =>
        window.FindControl<T>(name) ?? throw new InvalidOperationException($"找不到控件 {name}。");
}
