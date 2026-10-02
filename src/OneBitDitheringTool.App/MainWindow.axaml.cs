using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OneBitDitheringTool.App.Rendering;
using OneBitDitheringTool.Core;

namespace OneBitDitheringTool.App;

/// <summary>
/// 主窗口：左侧预览，右侧参数面板。本文件负责界面的搭建与「控件 ⇄ 设置」的转换，
/// 图片载入与渲染结果见 MainWindow.Loading.cs，预览的缩放平移见 MainWindow.View.cs。
/// </summary>
public partial class MainWindow : Window
{
    // 参数停止变化多久之后才重新计算。太短会在拖动滑杆时反复启动计算，太长则反馈迟钝
    private static readonly TimeSpan RenderDebounce = TimeSpan.FromMilliseconds(100);

    // 与原版下拉框的内容和顺序一致
    private static readonly string[] BayerSizes = ["2x2", "3x3", "3x5", "5x3", "4x4", "8x8", "16x16", "32x32", "64x64"];
    private static readonly string[] BayerSideLengths = ["2", "4", "8", "16", "32", "64"];

    private readonly RenderCoordinator _coordinator;

    // 随机范围联动约束期间为 true：此时程序在改滑杆，不应因此各触发一次渲染
    private bool _adjustingRandomRange;

    /// <summary>
    /// 创建主窗口。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        // 必须在界面线程上创建：渲染结果的回调会被投递回创建者所在的线程，回调里要操作控件
        _coordinator = new RenderCoordinator(RenderDebounce, OnRenderCompleted);

        PopulateLists();
        WireParameterEvents();
        UpdatePanels();

        // 拖放事件要在窗口层面接收：用户可能把文件拖到窗口的任何位置，而不只是预览区
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        ShowOriginalCheckBox.IsCheckedChanged += (_, _) => RefreshPreview();

        Closed += (_, _) =>
        {
            // 窗口关闭时取消还在进行的批量保存，免得后台线程继续写文件
            _closing.Cancel();
            _closing.Dispose();
            _coordinator.Dispose();
            _resultBitmap?.Dispose();
            _originalBitmap?.Dispose();
        };
    }

    private void PopulateLists()
    {
        BayerSizeComboBox.ItemsSource = BayerSizes;
        BayerSizeComboBox.SelectedItem = "8x8";
        BayerWidthComboBox.ItemsSource = BayerSideLengths;
        BayerWidthComboBox.SelectedItem = "8";
        BayerHeightComboBox.ItemsSource = BayerSideLengths;
        BayerHeightComboBox.SelectedItem = "8";

        OrderedMatrixComboBox.ItemsSource = OrderedMatrices.All.Select(m => m.Name).ToArray();
        OrderedMatrixComboBox.SelectedItem = new DitherSettings().OrderedMatrixName;
        ErrorKernelComboBox.ItemsSource = ErrorDiffusionKernels.All.Select(k => k.Name).ToArray();
        ErrorKernelComboBox.SelectedItem = new DitherSettings().ErrorKernelName;
    }

    private void WireParameterEvents()
    {
        foreach (Controls.ParameterSlider slider in new[]
        {
            ScaleSlider, RedSlider, GreenSlider, BlueSlider, StrengthSlider, BrightnessSlider, ContrastSlider,
        })
        {
            slider.ValueChanged += (_, _) => RequestRender();
        }

        // 随机范围的两个滑杆要先做联动约束，不能直接触发渲染
        RandomMinSlider.ValueChanged += (_, _) => OnRandomRangeChanged(minChanged: true);
        RandomMaxSlider.ValueChanged += (_, _) => OnRandomRangeChanged(minChanged: false);

        SplitChannelsCheckBox.IsCheckedChanged += (_, _) => OnStructureChanged();
        CustomBayerCheckBox.IsCheckedChanged += (_, _) => OnStructureChanged();
        SerpentineCheckBox.IsCheckedChanged += (_, _) => RequestRender();

        DitherTypeComboBox.SelectionChanged += (_, _) => OnStructureChanged();
        foreach (ComboBox comboBox in new[]
        {
            BayerSizeComboBox, BayerWidthComboBox, BayerHeightComboBox, OrderedMatrixComboBox, ErrorKernelComboBox,
        })
        {
            comboBox.SelectionChanged += (_, _) => RequestRender();
        }
    }

    /// <summary>
    /// 勾选框或算法类别变化：先更新哪些面板可见，再重新渲染。
    /// </summary>
    private void OnStructureChanged()
    {
        UpdatePanels();
        RequestRender();
    }

    private void UpdatePanels()
    {
        var kind = (DitherKind)Math.Max(0, DitherTypeComboBox.SelectedIndex);
        BayerPanel.IsVisible = kind == DitherKind.Bayer;
        OrderedPanel.IsVisible = kind == DitherKind.OrderedMatrix;
        ErrorPanel.IsVisible = kind == DitherKind.ErrorDiffusion;
        RandomPanel.IsVisible = kind == DitherKind.Random;

        bool customBayer = CustomBayerCheckBox.IsChecked == true;
        BayerSizeComboBox.IsVisible = !customBayer;
        CustomBayerPanel.IsVisible = customBayer;

        ChannelPanel.IsVisible = SplitChannelsCheckBox.IsChecked == true;
    }

    private void OnRandomRangeChanged(bool minChanged)
    {
        if (_adjustingRandomRange)
        {
            return;
        }

        // 下限不能超过上限（反之亦然）：与原版一致，被拖的那个滑杆停在另一个滑杆的位置
        _adjustingRandomRange = true;
        try
        {
            if (RandomMinSlider.Value > RandomMaxSlider.Value)
            {
                if (minChanged)
                {
                    RandomMinSlider.SetValueWithoutNotify(RandomMaxSlider.Value);
                }
                else
                {
                    RandomMaxSlider.SetValueWithoutNotify(RandomMinSlider.Value);
                }
            }
        }
        finally
        {
            _adjustingRandomRange = false;
        }

        RequestRender();
    }

    private void OnResetChannelsClick(object? sender, RoutedEventArgs e)
    {
        // 三个滑杆一起复位，只触发一次渲染
        RedSlider.SetValueWithoutNotify(RedSlider.DefaultValue);
        GreenSlider.SetValueWithoutNotify(GreenSlider.DefaultValue);
        BlueSlider.SetValueWithoutNotify(BlueSlider.DefaultValue);
        RequestRender();
    }

    private void OnResetRandomClick(object? sender, RoutedEventArgs e)
    {
        RandomMinSlider.SetValueWithoutNotify(RandomMinSlider.DefaultValue);
        RandomMaxSlider.SetValueWithoutNotify(RandomMaxSlider.DefaultValue);
        RequestRender();
    }

    /// <summary>
    /// 把控件当前的取值汇总成完整的工具参数。
    /// </summary>
    /// <returns>当前参数；预览与批量保存都以它为准，保证所见即所存。</returns>
    internal ToolSettings ReadSettings()
    {
        (int bayerWidth, int bayerHeight) = CustomBayerCheckBox.IsChecked == true
            ? (ParseSide(BayerWidthComboBox), ParseSide(BayerHeightComboBox))
            : ParseSize((string)BayerSizeComboBox.SelectedItem!);

        return new ToolSettings
        {
            Scale = ScaleSlider.Value,
            SplitChannels = SplitChannelsCheckBox.IsChecked == true,
            Weights = new ChannelWeights(RedSlider.Value, GreenSlider.Value, BlueSlider.Value),
            Brightness = BrightnessSlider.Value,
            Contrast = ContrastSlider.Value,
            Dither = new DitherSettings
            {
                Kind = (DitherKind)Math.Max(0, DitherTypeComboBox.SelectedIndex),
                Strength = (float)StrengthSlider.Value,
                BayerWidth = bayerWidth,
                BayerHeight = bayerHeight,
                OrderedMatrixName = (string)OrderedMatrixComboBox.SelectedItem!,
                ErrorKernelName = (string)ErrorKernelComboBox.SelectedItem!,
                Serpentine = SerpentineCheckBox.IsChecked == true,
                RandomMin = (float)RandomMinSlider.Value,
                RandomMax = (float)RandomMaxSlider.Value,
            },
        };
    }

    private static (int Width, int Height) ParseSize(string size)
    {
        string[] parts = size.Split('x');
        return (int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    private static int ParseSide(ComboBox comboBox) =>
        int.Parse((string)comboBox.SelectedItem!, CultureInfo.InvariantCulture);
}
