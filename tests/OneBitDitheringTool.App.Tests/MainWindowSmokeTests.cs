using Avalonia.Headless.XUnit;

namespace OneBitDitheringTool.App.Tests;

public class MainWindowSmokeTests
{
    [AvaloniaFact]
    public void MainWindow_CanBeCreatedAndShown()
    {
        var window = new MainWindow();
        window.Show();

        Assert.Equal("OneBitDitheringTool", window.Title);
        Assert.True(window.IsVisible);
    }
}
