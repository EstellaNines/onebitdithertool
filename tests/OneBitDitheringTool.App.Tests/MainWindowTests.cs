using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using OneBitDitheringTool.App.Controls;
using OneBitDitheringTool.App.Rendering;
using OneBitDitheringTool.Core;

namespace OneBitDitheringTool.App.Tests;

public class MainWindowTests
{
    [AvaloniaFact]
    public async Task Loading_ShowsABlackAndWhitePreviewOfTheSameSize()
    {
        using var file = new TempImageFile(SampleImages.Noisy(64, 48), "sample.png");
        MainWindow window = await OpenAsync(file.Path);

        WriteableBitmap preview = Preview(window);
        Assert.Equal(new PixelSize(64, 48), preview.PixelSize);

        // 每个像素都应是纯黑或纯白（预乘 BGRA 下三个颜色分量相等，且完全不透明）
        byte[] bgra = BitmapReader.ReadBgra(preview);
        for (int i = 0; i < bgra.Length; i += 4)
        {
            Assert.True(bgra[i] == bgra[i + 1] && bgra[i + 1] == bgra[i + 2] && bgra[i] is 0 or 255, $"像素 {i / 4} 不是纯黑或纯白");
            Assert.Equal(255, bgra[i + 3]);
        }

        Assert.Equal("Image 1 of 1", Find<TextBlock>(window, "ImageCountText").Text);
        Assert.Equal("sample.png", Find<TextBlock>(window, "FileNameText").Text);
        Assert.Equal("Size: (64x48)", Find<TextBlock>(window, "SizeText").Text);
        Assert.StartsWith("Done", Find<TextBlock>(window, "StatusText").Text);
        Assert.False(Find<StackPanel>(window, "EmptyHint").IsVisible);
        Assert.Equal("OneBitDitheringTool - sample.png", window.Title);

        window.Close();
    }

    [AvaloniaFact]
    public async Task Preview_IsExactlyWhatCoreComputesForTheCurrentControls()
    {
        RgbaImage source = SampleImages.Noisy(80, 60);
        using var file = new TempImageFile(source);
        MainWindow window = await OpenAsync(file.Path);

        // 依次改动各类控件，每次都核对：预览里的像素必须与 Core 按当前控件取值算出的结果逐字节相同。
        // 这样界面与算法之间只要有任何一环接错（读错控件、换算错、顺序错），这里都会暴露
        Action<MainWindow>[] changes =
        [
            w => Find<ParameterSlider>(w, "StrengthSlider").Value = 0.6,
            w => Find<ParameterSlider>(w, "BrightnessSlider").Value = 0.25,
            w => Find<ParameterSlider>(w, "ContrastSlider").Value = -0.3,
            w => Find<ParameterSlider>(w, "ScaleSlider").Value = 0.5,
            w => Find<ComboBox>(w, "DitherTypeComboBox").SelectedIndex = 2,
            w => Find<ComboBox>(w, "ErrorKernelComboBox").SelectedItem = "Atkinson",
            w => Find<CheckBox>(w, "SerpentineCheckBox").IsChecked = true,
            w => Find<ComboBox>(w, "DitherTypeComboBox").SelectedIndex = 1,
            w => Find<ComboBox>(w, "OrderedMatrixComboBox").SelectedItem = "ClusteredDotDiagonal8x8",
            w => Find<ComboBox>(w, "DitherTypeComboBox").SelectedIndex = 0,
            w => Find<CheckBox>(w, "CustomBayerCheckBox").IsChecked = true,
            w => Find<ComboBox>(w, "BayerWidthComboBox").SelectedItem = "16",
            w => Find<ComboBox>(w, "BayerHeightComboBox").SelectedItem = "4",
            w => Find<CheckBox>(w, "SplitChannelsCheckBox").IsChecked = true,
            w => Find<ParameterSlider>(w, "RedSlider").Value = 0.7,
        ];

        foreach (Action<MainWindow> change in changes)
        {
            change(window);
            await window.WhenIdleAsync();
            AssertPreviewMatchesCore(window, source);
        }

        window.Close();
    }

    [AvaloniaFact]
    public async Task ScaleSlider_ShrinksThePreviewAndTheSizeLabel()
    {
        using var file = new TempImageFile(SampleImages.Noisy(100, 60));
        MainWindow window = await OpenAsync(file.Path);

        Find<ParameterSlider>(window, "ScaleSlider").Value = 0.5;
        await window.WhenIdleAsync();

        Assert.Equal(new PixelSize(50, 30), Preview(window).PixelSize);
        Assert.Equal("Size: (50x30)", Find<TextBlock>(window, "SizeText").Text);

        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(0, true, false, false, false)]
    [InlineData(1, false, true, false, false)]
    [InlineData(2, false, false, true, false)]
    [InlineData(3, false, false, false, true)]
    public void DitherType_ShowsOnlyItsOwnOptions(int index, bool bayer, bool ordered, bool error, bool random)
    {
        var window = new MainWindow();
        window.Show();

        Find<ComboBox>(window, "DitherTypeComboBox").SelectedIndex = index;

        Assert.Equal(bayer, Find<StackPanel>(window, "BayerPanel").IsVisible);
        Assert.Equal(ordered, Find<StackPanel>(window, "OrderedPanel").IsVisible);
        Assert.Equal(error, Find<StackPanel>(window, "ErrorPanel").IsVisible);
        Assert.Equal(random, Find<StackPanel>(window, "RandomPanel").IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void OptionalPanels_FollowTheirCheckBoxes()
    {
        var window = new MainWindow();
        window.Show();

        Assert.False(Find<StackPanel>(window, "ChannelPanel").IsVisible);
        Find<CheckBox>(window, "SplitChannelsCheckBox").IsChecked = true;
        Assert.True(Find<StackPanel>(window, "ChannelPanel").IsVisible);

        Assert.True(Find<ComboBox>(window, "BayerSizeComboBox").IsVisible);
        Find<CheckBox>(window, "CustomBayerCheckBox").IsChecked = true;
        Assert.False(Find<ComboBox>(window, "BayerSizeComboBox").IsVisible);
        Assert.True(Find<StackPanel>(window, "CustomBayerPanel").IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void ReadSettings_DefaultsMatchTheOriginalTool()
    {
        var window = new MainWindow();
        window.Show();

        // 界面刚打开时的参数必须与 ToolSettings 的默认值（即原版初始状态）一致
        Assert.Equal(new ToolSettings(), window.ReadSettings());

        window.Close();
    }

    [AvaloniaFact]
    public void ReadSettings_ReflectsEveryControl()
    {
        var window = new MainWindow();
        window.Show();

        // 每个控件都设成互不相同的非默认值：任何一处读错控件、读串位，都会让下面的整体比较失败
        Find<ParameterSlider>(window, "ScaleSlider").Value = 0.4;
        Find<CheckBox>(window, "SplitChannelsCheckBox").IsChecked = true;
        Find<ParameterSlider>(window, "RedSlider").Value = 0.2;
        Find<ParameterSlider>(window, "GreenSlider").Value = 0.3;
        Find<ParameterSlider>(window, "BlueSlider").Value = 0.5;
        Find<ParameterSlider>(window, "BrightnessSlider").Value = 0.15;
        Find<ParameterSlider>(window, "ContrastSlider").Value = -0.25;
        Find<ParameterSlider>(window, "StrengthSlider").Value = 0.55;
        Find<ComboBox>(window, "DitherTypeComboBox").SelectedIndex = 2;
        Find<CheckBox>(window, "CustomBayerCheckBox").IsChecked = true;
        Find<ComboBox>(window, "BayerWidthComboBox").SelectedItem = "32";
        Find<ComboBox>(window, "BayerHeightComboBox").SelectedItem = "4";
        Find<ComboBox>(window, "OrderedMatrixComboBox").SelectedItem = "Vertical5x3";
        Find<ComboBox>(window, "ErrorKernelComboBox").SelectedItem = "Stucki";
        Find<CheckBox>(window, "SerpentineCheckBox").IsChecked = true;
        Find<ParameterSlider>(window, "RandomMinSlider").Value = -0.3;
        Find<ParameterSlider>(window, "RandomMaxSlider").Value = 0.7;

        var expected = new ToolSettings
        {
            Scale = 0.4,
            SplitChannels = true,
            Weights = new ChannelWeights(0.2, 0.3, 0.5),
            Brightness = 0.15,
            Contrast = -0.25,
            Dither = new DitherSettings
            {
                Kind = DitherKind.ErrorDiffusion,
                Strength = 0.55f,
                BayerWidth = 32,
                BayerHeight = 4,
                OrderedMatrixName = "Vertical5x3",
                ErrorKernelName = "Stucki",
                Serpentine = true,
                RandomMin = -0.3f,
                RandomMax = 0.7f,
            },
        };
        Assert.Equal(expected, window.ReadSettings());

        window.Close();
    }

    [AvaloniaFact]
    public void RandomRange_IsConstrainedSoMinNeverExceedsMax()
    {
        var window = new MainWindow();
        window.Show();
        var min = Find<ParameterSlider>(window, "RandomMinSlider");
        var max = Find<ParameterSlider>(window, "RandomMaxSlider");

        // 与原版一致：被拖动的滑杆停在另一个滑杆的位置，不会把对方推着走
        min.Value = 1.5;
        Assert.Equal(0.5, min.Value);
        Assert.Equal(0.5, max.Value);

        max.Value = -1.0;
        Assert.Equal(0.5, max.Value);
        Assert.Equal(0.5, min.Value);

        // 想把整个范围往下移，需要先降下限，再降上限
        min.Value = -1.0;
        max.Value = -0.5;
        Assert.Equal(-1.0, min.Value);
        Assert.Equal(-0.5, max.Value);

        window.Close();
    }

    [AvaloniaFact]
    public void ClickingASliderName_ResetsItToItsDefault()
    {
        var window = new MainWindow();
        window.Show();
        var strength = Find<ParameterSlider>(window, "StrengthSlider");

        strength.Value = 0.25;
        Assert.Equal(0.25, strength.Value);
        strength.FindControl<Button>("ResetButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(1.0, strength.Value);

        window.Close();
    }

    [AvaloniaFact]
    public async Task ACorruptFile_IsReportedInTheStatusBarAndDoesNotCrash()
    {
        string directory = Path.Combine(Path.GetTempPath(), "obdt-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "broken.png");
            await File.WriteAllBytesAsync(path, [1, 2, 3, 4, 5, 6, 7, 8], TestContext.Current.CancellationToken);

            MainWindow window = await OpenAsync(path);

            Assert.StartsWith("Cannot open broken.png", Find<TextBlock>(window, "StatusText").Text);
            Assert.True(Find<StackPanel>(window, "EmptyHint").IsVisible);
            Assert.Null(Find<Image>(window, "PreviewImage").Source);

            window.Close();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task LoadingASecondImageQuickly_ShowsTheSecondOne()
    {
        using var first = new TempImageFile(SampleImages.Noisy(40, 30), "first.png");
        using var second = new TempImageFile(SampleImages.Noisy(90, 20), "second.png");
        var window = new MainWindow();
        window.Show();

        // 不等第一张解码完就载入第二张：晚到的第一张不能覆盖第二张
        _ = window.LoadFilesAsync([first.Path]);
        await window.LoadFilesAsync([second.Path]);
        await window.WhenIdleAsync();

        Assert.Equal("second.png", Find<TextBlock>(window, "FileNameText").Text);
        Assert.Equal(new PixelSize(90, 20), Preview(window).PixelSize);

        window.Close();
    }

    private static async Task<MainWindow> OpenAsync(string path)
    {
        var window = new MainWindow();
        window.Show();
        await window.LoadFilesAsync([path]);
        await window.WhenIdleAsync();
        return window;
    }

    private static T Find<T>(MainWindow window, string name)
        where T : Control =>
        window.FindControl<T>(name) ?? throw new InvalidOperationException($"找不到控件 {name}。");

    private static WriteableBitmap Preview(MainWindow window) =>
        Assert.IsType<WriteableBitmap>(Find<Image>(window, "PreviewImage").Source);

    private static void AssertPreviewMatchesCore(MainWindow window, RgbaImage source)
    {
        ToolSettings settings = window.ReadSettings();
        OneBitImage expected = settings.Render(source, TestContext.Current.CancellationToken);
        using WriteableBitmap expectedBitmap = PreviewBitmaps.FromOneBit(expected);

        Assert.Equal(BitmapReader.ReadBgra(expectedBitmap), BitmapReader.ReadBgra(Preview(window)));
    }
}
