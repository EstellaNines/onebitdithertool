using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace OneBitDitheringTool.App.Controls;

/// <summary>
/// 带名称与数值显示的参数滑杆：点击名称复位为默认值，步长 0.01。
/// </summary>
public partial class ParameterSlider : UserControl
{
    // 为 true 时，滑杆的数值变化不对外通知（用于程序内部修改数值）
    private bool _silent;

    /// <summary>
    /// 创建滑杆。
    /// </summary>
    public ParameterSlider()
    {
        InitializeComponent();
    }

    /// <summary>用户拖动滑杆、点击复位，或通过 <see cref="Value"/> 改变数值之后触发。</summary>
    public event EventHandler? ValueChanged;

    /// <summary>滑杆上方显示的名称，同时是复位按钮的文字。</summary>
    public string Header
    {
        get => ResetButton.Content as string ?? string.Empty;
        set => ResetButton.Content = value;
    }

    /// <summary>最小值。</summary>
    public double Minimum
    {
        get => Slider.Minimum;
        set => Slider.Minimum = value;
    }

    /// <summary>最大值。</summary>
    public double Maximum
    {
        get => Slider.Maximum;
        set => Slider.Maximum = value;
    }

    /// <summary>点击名称时复位到的值。</summary>
    public double DefaultValue { get; set; }

    /// <summary>当前值，已按 0.01 的精度取整。</summary>
    public double Value
    {
        get => Math.Round(Slider.Value, 2);
        set => Slider.Value = value;
    }

    /// <summary>
    /// 修改数值，但不触发 <see cref="ValueChanged"/>。
    /// </summary>
    /// <remarks>
    /// 用于程序一次性修改多个滑杆的场合（例如联动约束）：调用方改完后只统一触发一次重新计算，
    /// 避免每改一个滑杆就启动一次渲染。
    /// </remarks>
    /// <param name="value">新的数值。</param>
    public void SetValueWithoutNotify(double value)
    {
        _silent = true;
        try
        {
            Slider.Value = value;
            UpdateValueText();
        }
        finally
        {
            _silent = false;
        }
    }

    private void OnSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        UpdateValueText();
        if (!_silent)
        {
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnResetClick(object? sender, RoutedEventArgs e) => Value = DefaultValue;

    private void UpdateValueText() => ValueText.Text = Value.ToString("0.00", CultureInfo.InvariantCulture);
}
