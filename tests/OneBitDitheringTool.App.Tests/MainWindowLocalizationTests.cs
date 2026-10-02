using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using OneBitDitheringTool.Core;

namespace OneBitDitheringTool.App.Tests;

/// <summary>
/// 守住「界面全是中文」：以后新增控件或状态栏文字时，若忘了写中文，这里会直接指出是哪一句。
/// </summary>
public class MainWindowLocalizationTests
{
    // 有意保留为拉丁字母的 Bayer 尺寸写法：它们是数据名称，同时也是解析用的键（按 x 切开）
    private static readonly string[] BayerSizes = ["2x2", "3x3", "3x5", "5x3", "4x4", "8x8", "16x16", "32x32", "64x64"];

    [AvaloniaFact]
    public async Task EveryUiStringIsChinese_EvenAfterLoadingAnImageAndSwitchingAlgorithms()
    {
        var window = new MainWindow();
        window.Show();
        var offenders = new SortedSet<string>(StringComparer.Ordinal);
        var everything = new HashSet<string>(StringComparer.Ordinal);

        void Check()
        {
            foreach (string text in CollectUiStrings(window))
            {
                everything.Add(text);
                if (IsLatinOnly(text) && !IsIntentionallyKept(text))
                {
                    offenders.Add(text);
                }
            }
        }

        // 第一遍：刚打开、还没载入图片的窗口，包含所有暂时看不见的面板
        Check();

        // 第二遍：载入图片后，状态栏、尺寸、第几张等运行时文字也会出现；
        // 依次切到四类算法，让各自的面板与下拉框当前显示的内容也被检查到
        using var file = new TempImageFile(SampleImages.Noisy(40, 30), "sample.png");
        await window.LoadFilesAsync([file.Path]);
        await window.WhenIdleAsync();
        var kinds = window.FindControl<ComboBox>("DitherTypeComboBox")!;
        for (int i = 0; i < 4; i++)
        {
            kinds.SelectedIndex = i;
            await window.WhenIdleAsync();
            Check();
        }

        // 防止「遍历落空、什么也没收集到」而白白通过：确认确实见到了几句典型的中文界面文字
        Assert.Contains("打开图片…", everything);
        Assert.Contains("抖动类型", everything);
        Assert.Contains("全部保存到输出文件夹…", everything);
        Assert.True(everything.Any(t => t.StartsWith("完成，用时", StringComparison.Ordinal)), "没有见到状态栏的「完成，用时 …」");

        Assert.True(offenders.Count == 0, "以下界面文字不是中文：" + string.Join(" ｜ ", offenders));

        window.Close();
    }

    /// <summary>
    /// 收集窗口里所有会显示给用户的文字：文本块、按钮与勾选框等的内容、鼠标悬停提示，以及窗口标题。
    /// 同时遍历逻辑树与可视树：暂时隐藏的面板只在逻辑树里，下拉框当前显示的项只在可视树里。
    /// </summary>
    private static IEnumerable<string> CollectUiStrings(MainWindow window)
    {
        IEnumerable<Control> controls = window.GetLogicalDescendants().OfType<Control>()
            .Concat(window.GetVisualDescendants().OfType<Control>())
            .Append(window)
            .Distinct();

        foreach (Control control in controls)
        {
            if (control is TextBlock { Text: { Length: > 0 } text })
            {
                yield return text;
            }

            if (control is ContentControl { Content: string content } && content.Length > 0)
            {
                yield return content;
            }

            if (ToolTip.GetTip(control) is string tip && tip.Length > 0)
            {
                yield return tip;
            }
        }

        if (window.Title is { Length: > 0 } title)
        {
            yield return title;
        }
    }

    private static bool IsLatinOnly(string text) =>
        text.Any(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z') && !text.Any(c => c is >= '\u4e00' and <= '\u9fff');

    /// <summary>
    /// 有意保留的非中文文字：产品名（窗口标题）、算法的专有名称、载入图片后的文件名。
    /// 算法名（FloydSteinberg、ClusteredDot4x4 等）是通行的英文专有名称，翻译了反而没人认得出。
    /// </summary>
    private static bool IsIntentionallyKept(string text) =>
        text.StartsWith("OneBitDitheringTool", StringComparison.Ordinal)
        || text.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
        || BayerSizes.Contains(text)
        || OrderedMatrices.All.Any(m => m.Name == text)
        || ErrorDiffusionKernels.All.Any(k => k.Name == text);
}
