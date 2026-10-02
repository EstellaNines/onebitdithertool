namespace OneBitDitheringTool.Core;

/// <summary>
/// 抖动算法的大类，对应原版界面里的 Dither Type 下拉框。
/// </summary>
public enum DitherKind
{
    /// <summary>Bayer 矩阵有序抖动。</summary>
    Bayer,

    /// <summary>聚点（clustered-dot）等内置矩阵的有序抖动。</summary>
    OrderedMatrix,

    /// <summary>误差扩散抖动。</summary>
    ErrorDiffusion,

    /// <summary>随机噪声抖动。</summary>
    Random,
}

/// <summary>
/// 一次抖动所需的全部算法设置。预览与批量保存共用同一份设置，保证「所见即所存」。
/// </summary>
/// <remarks>
/// 这是不可变的值对象：两份设置的各字段都相等时，用它们抖动同一份输入必得到相同结果，
/// 因此可以直接用来判断「设置是否变了」，也可以作为缓存的键。默认值与原版工具的初始状态一致。
/// </remarks>
public sealed record DitherSettings
{
    /// <summary>算法大类，缺省为 Bayer。</summary>
    public DitherKind Kind { get; init; } = DitherKind.Bayer;

    /// <summary>
    /// 抖动强度，范围 [-1, 1]，对 Bayer、有序矩阵与误差扩散有效，含义见 <see cref="OrderedDitherer.Bayer"/>。
    /// </summary>
    public float Strength { get; init; } = 1f;

    /// <summary>Bayer 矩阵宽度，缺省 8。</summary>
    public int BayerWidth { get; init; } = 8;

    /// <summary>Bayer 矩阵高度，缺省 8。</summary>
    public int BayerHeight { get; init; } = 8;

    /// <summary>有序矩阵的名称，见 <see cref="OrderedMatrices"/>，缺省 ClusteredDot4x4。</summary>
    public string OrderedMatrixName { get; init; } = "ClusteredDot4x4";

    /// <summary>误差扩散核的名称，见 <see cref="ErrorDiffusionKernels"/>，缺省 FloydSteinberg。</summary>
    public string ErrorKernelName { get; init; } = "FloydSteinberg";

    /// <summary>误差扩散是否使用蛇形扫描。</summary>
    public bool Serpentine { get; init; }

    /// <summary>随机抖动的噪声下限，缺省 -0.5。</summary>
    public float RandomMin { get; init; } = -0.5f;

    /// <summary>随机抖动的噪声上限，缺省 0.5。</summary>
    public float RandomMax { get; init; } = 0.5f;

    /// <summary>
    /// 随机抖动的种子。固定种子让预览与保存的噪声一致；界面上拖动其他滑杆时噪声图案也不会跳变。
    /// </summary>
    public ulong RandomSeed { get; init; }

    /// <summary>
    /// 按当前设置创建抖动器。
    /// </summary>
    /// <returns>对应算法的抖动器。</returns>
    /// <exception cref="ArgumentException">矩阵尺寸、矩阵名称、核名称或噪声范围无效。</exception>
    /// <exception cref="ArgumentOutOfRangeException">强度或噪声范围不是有限数。</exception>
    public IDitherer CreateDitherer() => Kind switch
    {
        DitherKind.Bayer => OrderedDitherer.Bayer(BayerWidth, BayerHeight, Strength),
        DitherKind.OrderedMatrix => OrderedDitherer.FromMatrix(OrderedMatrices.Get(OrderedMatrixName), Strength),
        DitherKind.ErrorDiffusion => ErrorDiffusionDitherer.Create(ErrorDiffusionKernels.Get(ErrorKernelName), Strength, Serpentine),
        DitherKind.Random => RandomDitherer.Create(RandomMin, RandomMax, RandomSeed),
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "未知的抖动类别。"),
    };
}
