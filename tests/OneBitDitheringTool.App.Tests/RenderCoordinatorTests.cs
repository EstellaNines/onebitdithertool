using System.Collections.Concurrent;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using OneBitDitheringTool.App.Rendering;
using OneBitDitheringTool.Core;

namespace OneBitDitheringTool.App.Tests;

public class RenderCoordinatorTests
{
    [Fact]
    public async Task Request_PublishesTheSameResultAsRenderingDirectly()
    {
        RgbaImage source = SampleImages.Noisy(64, 48);
        var settings = new ToolSettings { Scale = 0.5, Dither = new DitherSettings { Kind = DitherKind.ErrorDiffusion } };
        var outcomes = new ConcurrentQueue<RenderOutcome>();
        using var coordinator = new RenderCoordinator(TimeSpan.Zero, outcomes.Enqueue);

        coordinator.Request(source, settings);
        await coordinator.WhenIdleAsync();

        RenderOutcome outcome = Assert.Single(outcomes);
        Assert.Null(outcome.Error);
        Assert.Same(source, outcome.Source);
        Assert.Equal(settings, outcome.Settings);
        Assert.Equal(settings.Render(source, TestContext.Current.CancellationToken).Levels, outcome.Result!.Levels);
    }

    [Fact]
    public async Task RapidRequests_PublishOnlyTheLastOne()
    {
        // 模拟拖动滑杆：参数连续变化。去抖期内的请求都应被后来的取代，只有最后一次的结果会发布
        RgbaImage source = SampleImages.Noisy(64, 48);
        var outcomes = new ConcurrentQueue<RenderOutcome>();
        using var coordinator = new RenderCoordinator(TimeSpan.FromMilliseconds(200), outcomes.Enqueue);

        for (int i = 1; i <= 5; i++)
        {
            coordinator.Request(source, new ToolSettings { Dither = new DitherSettings { Strength = i / 5f } });
        }

        await coordinator.WhenIdleAsync();

        RenderOutcome outcome = Assert.Single(outcomes);
        Assert.Equal(1f, outcome.Settings.Dither.Strength);
    }

    [Fact]
    public async Task Failure_IsReportedAsAnErrorAndTheCoordinatorKeepsWorking()
    {
        RgbaImage source = SampleImages.Noisy(32, 32);
        var outcomes = new ConcurrentQueue<RenderOutcome>();
        using var coordinator = new RenderCoordinator(TimeSpan.Zero, outcomes.Enqueue);

        // Bayer 矩阵的边长必须是 2 的幂，6×6 非法
        coordinator.Request(source, new ToolSettings { Dither = new DitherSettings { BayerWidth = 6, BayerHeight = 6 } });
        await coordinator.WhenIdleAsync();
        coordinator.Request(source, new ToolSettings());
        await coordinator.WhenIdleAsync();

        RenderOutcome[] all = [.. outcomes];
        Assert.Equal(2, all.Length);
        Assert.IsType<ArgumentException>(all[0].Error);
        Assert.Null(all[0].Result);
        Assert.Null(all[1].Error);
        Assert.NotNull(all[1].Result);
    }

    [Fact]
    public async Task Preprocessing_IsReusedWhenOnlyTheDitherSettingsChange()
    {
        RgbaImage source = SampleImages.Noisy(64, 48);
        using var coordinator = new RenderCoordinator(TimeSpan.Zero, _ => { });

        async Task RenderAsync(RgbaImage image, ToolSettings settings)
        {
            coordinator.Request(image, settings);
            await coordinator.WhenIdleAsync();
        }

        var baseline = new ToolSettings { Scale = 0.5 };
        await RenderAsync(source, baseline);
        Assert.Equal(1, coordinator.PrepareCount);

        // 只改抖动参数：缩放、灰度化、对比度与亮度的结果都不变，应直接复用
        await RenderAsync(source, baseline with { Dither = new DitherSettings { Strength = 0.4f } });
        await RenderAsync(source, baseline with { Dither = new DitherSettings { Kind = DitherKind.Random } });
        Assert.Equal(1, coordinator.PrepareCount);

        // 改了预处理参数，或换了一幅图，就必须重新算
        await RenderAsync(source, baseline with { Contrast = 0.2 });
        Assert.Equal(2, coordinator.PrepareCount);
        await RenderAsync(SampleImages.Noisy(64, 48), baseline with { Contrast = 0.2 });
        Assert.Equal(3, coordinator.PrepareCount);
    }

    [Fact]
    public async Task Dispose_CancelsPendingWork()
    {
        var outcomes = new ConcurrentQueue<RenderOutcome>();
        var coordinator = new RenderCoordinator(TimeSpan.FromMilliseconds(300), outcomes.Enqueue);

        coordinator.Request(SampleImages.Noisy(32, 32), new ToolSettings());
        coordinator.Dispose();
        await coordinator.WhenIdleAsync();

        Assert.Empty(outcomes);
    }

    [AvaloniaFact]
    public async Task Callback_RunsOnTheThreadThatCreatedTheCoordinator()
    {
        // 在界面线程上创建时，回调必须回到界面线程：回调里要操作控件，其他线程不允许
        bool onUiThread = false;
        using var coordinator = new RenderCoordinator(TimeSpan.Zero, _ => onUiThread = Dispatcher.UIThread.CheckAccess());

        coordinator.Request(SampleImages.Noisy(32, 32), new ToolSettings());
        await coordinator.WhenIdleAsync();

        Assert.True(onUiThread);
    }
}
