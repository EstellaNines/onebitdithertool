using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using OneBitDitheringTool.App.Controls;
using OneBitDitheringTool.App.Rendering;
using OneBitDitheringTool.Core;

namespace OneBitDitheringTool.App.Tests;

public class MainWindowInteractionTests
{
    [AvaloniaFact]
    public async Task Navigation_ButtonsAndArrowKeys_CycleThroughTheImages()
    {
        using var a = new TempImageFile(SampleImages.Noisy(40, 30), "a.png");
        using var b = new TempImageFile(SampleImages.Noisy(50, 20), "b.png");
        using var c = new TempImageFile(SampleImages.Noisy(60, 10), "c.png");
        MainWindow window = await OpenAsync(a.Path, b.Path, c.Path);
        AssertShowing(window, "第 1 张 / 共 3 张", "a.png", new PixelSize(40, 30));

        Click(window, "NextButton");
        await window.WhenIdleAsync();
        AssertShowing(window, "第 2 张 / 共 3 张", "b.png", new PixelSize(50, 20));

        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        await window.WhenIdleAsync();
        AssertShowing(window, "第 3 张 / 共 3 张", "c.png", new PixelSize(60, 10));

        // 到尾部后再往后：回到第一张
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        await window.WhenIdleAsync();
        AssertShowing(window, "第 1 张 / 共 3 张", "a.png", new PixelSize(40, 30));

        // 在第一张再往前：绕到最后一张
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        await window.WhenIdleAsync();
        AssertShowing(window, "第 3 张 / 共 3 张", "c.png", new PixelSize(60, 10));

        Click(window, "PreviousButton");
        await window.WhenIdleAsync();
        AssertShowing(window, "第 2 张 / 共 3 张", "b.png", new PixelSize(50, 20));

        window.Close();
    }

    [AvaloniaFact]
    public async Task NavigationButtons_AreEnabledOnlyWithMoreThanOneImage()
    {
        using var a = new TempImageFile(SampleImages.Noisy(20, 20), "a.png");
        using var b = new TempImageFile(SampleImages.Noisy(20, 20), "b.png");
        MainWindow window = await OpenAsync(a.Path);

        Assert.False(Find<Button>(window, "NextButton").IsEnabled);
        Assert.False(Find<Button>(window, "PreviousButton").IsEnabled);

        // 只有一张时按方向键什么也不该发生
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        await window.WhenIdleAsync();
        Assert.Equal("第 1 张 / 共 1 张", Find<TextBlock>(window, "ImageCountText").Text);

        await window.LoadFilesAsync([a.Path, b.Path]);
        await window.WhenIdleAsync();
        Assert.True(Find<Button>(window, "NextButton").IsEnabled);
        Assert.True(Find<Button>(window, "PreviousButton").IsEnabled);

        window.Close();
    }

    [AvaloniaFact]
    public async Task ShowOriginal_SwitchesBetweenTheSourceAndTheResult()
    {
        RgbaImage source = SampleImages.Noisy(40, 30);
        using var file = new TempImageFile(source);
        var window = new MainWindow();
        window.Show();
        Find<ParameterSlider>(window, "ScaleSlider").Value = 0.5;
        await window.LoadFilesAsync([file.Path]);
        await window.WhenIdleAsync();
        Assert.Equal(new PixelSize(20, 15), Preview(window).PixelSize);
        Point before = ImageCenter(window);

        Find<CheckBox>(window, "ShowOriginalCheckBox").IsChecked = true;

        // 显示的是缩放之前的原图，像素与原图的预乘位图逐字节相同；两种尺寸之间切换时，图像中心不动
        WriteableBitmap original = Preview(window);
        Assert.Equal(new PixelSize(40, 30), original.PixelSize);
        using WriteableBitmap expectedOriginal = PreviewBitmaps.FromRgba(source);
        Assert.Equal(BitmapReader.ReadBgra(expectedOriginal), BitmapReader.ReadBgra(original));
        Assert.True(Math.Abs(before.X - ImageCenter(window).X) <= 1 && Math.Abs(before.Y - ImageCenter(window).Y) <= 1);

        // 显示原图期间改参数：后台照常重算，但界面不被打断，仍然显示原图
        Find<ParameterSlider>(window, "StrengthSlider").Value = 0.3;
        await window.WhenIdleAsync();
        Assert.Same(original, Preview(window));

        // 取消勾选后，看到的是按新参数算出的结果
        Find<CheckBox>(window, "ShowOriginalCheckBox").IsChecked = false;
        using WriteableBitmap expectedResult = PreviewBitmaps.FromOneBit(window.ReadSettings().Render(source, TestContext.Current.CancellationToken));
        Assert.Equal(BitmapReader.ReadBgra(expectedResult), BitmapReader.ReadBgra(Preview(window)));

        window.Close();
    }

    [AvaloniaFact]
    public async Task ShowOriginal_StaysOnWhenSwitchingImages()
    {
        RgbaImage first = SampleImages.Noisy(30, 20);
        RgbaImage second = SampleImages.Noisy(45, 25);
        using var a = new TempImageFile(first, "a.png");
        using var b = new TempImageFile(second, "b.png");
        MainWindow window = await OpenAsync(a.Path, b.Path);
        Find<CheckBox>(window, "ShowOriginalCheckBox").IsChecked = true;

        // 勾选着「显示原图」切到下一张：必须立刻显示新图的原图，而不是残留旧图，也不能因旧位图被释放而出错
        Click(window, "NextButton");
        await window.WhenIdleAsync();

        using WriteableBitmap expected = PreviewBitmaps.FromRgba(second);
        Assert.Equal(BitmapReader.ReadBgra(expected), BitmapReader.ReadBgra(Preview(window)));

        window.Close();
    }

    [AvaloniaFact]
    public async Task MouseWheel_ZoomsInWholeStepsAroundThePointer()
    {
        using var file = new TempImageFile(SampleImages.Noisy(64, 48));
        MainWindow window = await OpenAsync(file.Path);
        Size host = Find<Border>(window, "PreviewHost").Bounds.Size;
        Point origin = window.ViewOffset;
        Assert.Equal(new Point(Math.Round((host.Width - 64) / 2), Math.Round((host.Height - 48) / 2)), origin);
        Assert.Equal(1, window.Zoom);

        // 指针指在图像内距左上角 (10, 10) 的位置，放大一档：该点必须仍在指针下，所以图像左上角向左上退 10 个像素
        var pointer = new Point(origin.X + 10, origin.Y + 10);
        window.MouseWheel(pointer, new Vector(0, 1));

        Assert.Equal(2, window.Zoom);
        Assert.Equal(new Point(origin.X - 10, origin.Y - 10), window.ViewOffset);
        Assert.Equal(128, Find<Image>(window, "PreviewImage").Width);
        Assert.Equal("预览放大：2×", Find<TextBlock>(window, "ZoomText").Text);

        // 在同一点缩回去，应当严格还原
        window.MouseWheel(pointer, new Vector(0, -1));
        Assert.Equal(1, window.Zoom);
        Assert.Equal(origin, window.ViewOffset);

        // 不能缩小到 1 倍以下；放大有上限
        window.MouseWheel(pointer, new Vector(0, -1));
        Assert.Equal(1, window.Zoom);
        for (int i = 0; i < 100; i++)
        {
            window.MouseWheel(pointer, new Vector(0, 1));
        }

        Assert.Equal(64, window.Zoom);

        window.Close();
    }

    [AvaloniaFact]
    public async Task DraggingWithTheLeftButton_PansTheImage()
    {
        using var file = new TempImageFile(SampleImages.Noisy(64, 48));
        MainWindow window = await OpenAsync(file.Path);
        Point origin = window.ViewOffset;
        var start = new Point(origin.X + 20, origin.Y + 20);

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(new Point(start.X + 30, start.Y + 20));
        window.MouseUp(new Point(start.X + 30, start.Y + 20), MouseButton.Left);

        Assert.Equal(new Point(origin.X + 30, origin.Y + 20), window.ViewOffset);

        // 松开之后再移动鼠标，不应继续拖动
        window.MouseMove(new Point(start.X + 80, start.Y + 80));
        Assert.Equal(new Point(origin.X + 30, origin.Y + 20), window.ViewOffset);

        window.Close();
    }

    [AvaloniaFact]
    public async Task ZoomAndPan_AreKeptWhenSwitchingImages()
    {
        using var a = new TempImageFile(SampleImages.Noisy(64, 48), "a.png");
        using var b = new TempImageFile(SampleImages.Noisy(64, 48), "b.png");
        MainWindow window = await OpenAsync(a.Path, b.Path);
        window.MouseWheel(new Point(window.ViewOffset.X + 5, window.ViewOffset.Y + 5), new Vector(0, 1));
        Point offset = window.ViewOffset;

        Click(window, "NextButton");
        await window.WhenIdleAsync();

        // 与原版一致：连续看一组图时，放大倍数与位置保持不变，方便逐张对比同一处细节
        Assert.Equal(2, window.Zoom);
        Assert.Equal(offset, window.ViewOffset);

        window.Close();
    }

    [AvaloniaFact]
    public async Task DroppedFolder_LoadsAllItsImagesInNameOrder()
    {
        using var first = new TempImageFile(SampleImages.Noisy(20, 20), "b.png");
        string folder = first.Directory;
        await File.WriteAllBytesAsync(Path.Combine(folder, "a.png"), await File.ReadAllBytesAsync(first.Path, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(folder, "readme.txt"), "not an image", TestContext.Current.CancellationToken);
        var window = new MainWindow();
        window.Show();

        await window.HandleDroppedPathsAsync([folder]);
        await window.WhenIdleAsync();

        AssertShowing(window, "第 1 张 / 共 2 张", "a.png", new PixelSize(20, 20));

        window.Close();
    }

    [AvaloniaFact]
    public async Task DroppingNothingSupported_ExplainsInTheStatusBarAndKeepsTheCurrentImage()
    {
        using var file = new TempImageFile(SampleImages.Noisy(30, 30), "keep.png");
        MainWindow window = await OpenAsync(file.Path);

        await window.HandleDroppedPathsAsync([Path.Combine(file.Directory, "notes.txt"), Path.Combine(file.Directory, "missing.png")]);

        Assert.StartsWith("拖入的内容里没有受支持的图片", Find<TextBlock>(window, "StatusText").Text);
        AssertShowing(window, "第 1 张 / 共 1 张", "keep.png", new PixelSize(30, 30));

        window.Close();
    }

    private static async Task<MainWindow> OpenAsync(params string[] paths)
    {
        var window = new MainWindow();
        window.Show();
        await window.LoadFilesAsync(paths);
        await window.WhenIdleAsync();
        return window;
    }

    private static T Find<T>(MainWindow window, string name)
        where T : Control =>
        window.FindControl<T>(name) ?? throw new InvalidOperationException($"找不到控件 {name}。");

    private static WriteableBitmap Preview(MainWindow window) =>
        Assert.IsType<WriteableBitmap>(Find<Image>(window, "PreviewImage").Source);

    private static void Click(MainWindow window, string buttonName) =>
        Find<Button>(window, buttonName).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static Point ImageCenter(MainWindow window) =>
        new(window.ViewOffset.X + (Find<Image>(window, "PreviewImage").Width / 2), window.ViewOffset.Y + (Find<Image>(window, "PreviewImage").Height / 2));

    private static void AssertShowing(MainWindow window, string count, string fileName, PixelSize size)
    {
        Assert.Equal(count, Find<TextBlock>(window, "ImageCountText").Text);
        Assert.Equal(fileName, Find<TextBlock>(window, "FileNameText").Text);
        Assert.Equal(size, Preview(window).PixelSize);
    }
}
