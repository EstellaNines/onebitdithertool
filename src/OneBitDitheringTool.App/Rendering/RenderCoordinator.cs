using System.Diagnostics;
using OneBitDitheringTool.Core;

namespace OneBitDitheringTool.App.Rendering;

/// <summary>
/// 一次渲染的结果，成功时带 <see cref="Result"/>，失败时带 <see cref="Error"/>。
/// </summary>
/// <param name="Source">渲染所用的原图。</param>
/// <param name="Settings">渲染所用的参数。</param>
/// <param name="Result">抖动结果；失败时为 <see langword="null"/>。</param>
/// <param name="Error">失败原因；成功时为 <see langword="null"/>。</param>
/// <param name="Elapsed">计算耗时。</param>
internal sealed record RenderOutcome(RgbaImage Source, ToolSettings Settings, OneBitImage? Result, Exception? Error, TimeSpan Elapsed);

/// <summary>
/// 调度预览的重新计算：去抖、取消过期任务、只发布最新一次的结果，并缓存预处理结果。
/// </summary>
/// <remarks>
/// 滑杆拖动时参数会连续变化。如果每次变化都完整算一遍，计算会堆积、界面会卡顿，旧结果还可能晚于新结果到达而覆盖它。
/// 所以每次请求都先取消上一次，稍等片刻（去抖）确认参数不再变化，才真正开始计算；过时的结果一律不发布。
/// 回调会被投递回创建本对象时所在的同步上下文（界面线程），调用方无需自己切换线程。
/// </remarks>
internal sealed class RenderCoordinator : IDisposable
{
    private readonly TimeSpan _debounce;
    private readonly Action<RenderOutcome> _completed;
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    private readonly object _gate = new();

    private CancellationTokenSource? _current;
    private Task _latest = Task.CompletedTask;

    // 预处理结果的缓存：只改抖动参数时，缩放与灰度化的结果可以直接复用
    private RgbaImage? _cachedSource;
    private PrepareOptions? _cachedOptions;
    private GrayImage? _cachedGray;
    private int _prepareCount;

    /// <summary>
    /// 创建协调器。应在界面线程上创建，这样回调才会回到界面线程。
    /// </summary>
    /// <param name="debounce">去抖时长：请求之后需要这么久没有新请求，才开始计算。</param>
    /// <param name="completed">渲染完成（或失败）时的回调；仅当该次请求没有被更新的请求取代时才会调用。</param>
    public RenderCoordinator(TimeSpan debounce, Action<RenderOutcome> completed)
    {
        ArgumentNullException.ThrowIfNull(completed);
        _debounce = debounce;
        _completed = completed;
    }

    /// <summary>已执行过多少次预处理计算（缓存命中的不计），供测试验证缓存是否生效。</summary>
    internal int PrepareCount => Volatile.Read(ref _prepareCount);

    /// <summary>
    /// 请求用给定参数重新渲染，同时取消尚未完成的上一次请求。
    /// </summary>
    /// <param name="source">原图。</param>
    /// <param name="settings">渲染参数。</param>
    public void Request(RgbaImage source, ToolSettings settings)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(settings);

        lock (_gate)
        {
            _current?.Cancel();
            _current = new CancellationTokenSource();
            _latest = RunAsync(source, settings, _current.Token);
        }
    }

    /// <summary>
    /// 等待当前所有请求处理完毕（含回调已执行）。主要供测试使用。
    /// </summary>
    /// <returns>表示等待的任务。</returns>
    public async Task WhenIdleAsync()
    {
        // 等待期间可能又有新请求，因此要一直等到「最新任务」不再变化为止
        while (true)
        {
            Task latest;
            lock (_gate)
            {
                latest = _latest;
            }

            await latest.ConfigureAwait(false);

            lock (_gate)
            {
                if (ReferenceEquals(latest, _latest))
                {
                    return;
                }
            }
        }
    }

    /// <summary>
    /// 取消尚未完成的请求。
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _current?.Cancel();
            _current?.Dispose();
            _current = null;
        }
    }

    private async Task RunAsync(RgbaImage source, ToolSettings settings, CancellationToken cancellationToken)
    {
        RenderOutcome outcome;
        try
        {
            if (_debounce > TimeSpan.Zero)
            {
                await Task.Delay(_debounce, cancellationToken).ConfigureAwait(false);
            }

            var stopwatch = Stopwatch.StartNew();
            OneBitImage result = await Task.Run(() => Render(source, settings, cancellationToken), cancellationToken).ConfigureAwait(false);
            outcome = new RenderOutcome(source, settings, result, null, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 被更新的请求取代，什么也不用做
            return;
        }
        catch (Exception exception)
        {
            outcome = new RenderOutcome(source, settings, null, exception, TimeSpan.Zero);
        }

        // 发布放在 try 之外：若回调自己出了错，不应被当成「渲染失败」再发布一次，而应如实暴露出来
        await PublishAsync(outcome, cancellationToken).ConfigureAwait(false);
    }

    private OneBitImage Render(RgbaImage source, ToolSettings settings, CancellationToken cancellationToken)
    {
        PrepareOptions options = settings.ToPrepareOptions(source.Width);
        GrayImage gray = GetPrepared(source, options);
        return settings.Render(gray, cancellationToken);
    }

    private GrayImage GetPrepared(RgbaImage source, PrepareOptions options)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_cachedSource, source) && _cachedOptions == options && _cachedGray is not null)
            {
                return _cachedGray;
            }
        }

        // 预处理放在锁外：它可能耗时较长，不能阻塞另一次请求的发起或取消
        GrayImage gray = Preprocessor.Prepare(source, options);
        Interlocked.Increment(ref _prepareCount);

        lock (_gate)
        {
            _cachedSource = source;
            _cachedOptions = options;
            _cachedGray = gray;
        }

        return gray;
    }

    /// <summary>
    /// 把结果投递给回调；若此刻该请求已被取代则丢弃。返回的任务在回调执行完之后才完成。
    /// </summary>
    private Task PublishAsync(RenderOutcome outcome, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        if (_context is null)
        {
            _completed(outcome);
            return Task.CompletedTask;
        }

        var done = new TaskCompletionSource();
        _context.Post(_ =>
        {
            try
            {
                // 排队等待执行期间，可能又来了新请求，此时这次的结果已经过时
                if (!cancellationToken.IsCancellationRequested)
                {
                    _completed(outcome);
                }
            }
            finally
            {
                done.SetResult();
            }
        }, null);
        return done.Task;
    }
}
