namespace OneBitDitheringTool.Core.Tests;

public class BayerMatrixTests
{
    /// <summary>
    /// Bisqwit 文章附录 2 中印出的示例矩阵（行优先展开），作为生成算法的测试向量。
    /// 覆盖方阵、横向矩形（宽大于高）与纵向矩形，对应算法里的两个分支。
    /// </summary>
    public static TheoryData<int, int, int[]> PublishedExamples => new()
    {
        { 2, 2, [0, 3, 2, 1] },
        { 4, 4, [0, 12, 3, 15, 8, 4, 11, 7, 2, 14, 1, 13, 10, 6, 9, 5] },
        { 4, 2, [0, 4, 2, 6, 3, 7, 1, 5] },
        { 2, 4, [0, 3, 4, 7, 2, 1, 6, 5] },
        { 8, 2, [0, 8, 4, 12, 2, 10, 6, 14, 3, 11, 7, 15, 1, 9, 5, 13] },
        {
            8, 4,
            [
                0, 16, 8, 24, 2, 18, 10, 26,
                12, 28, 4, 20, 14, 30, 6, 22,
                3, 19, 11, 27, 1, 17, 9, 25,
                15, 31, 7, 23, 13, 29, 5, 21,
            ]
        },
        {
            4, 8,
            [
                0, 12, 3, 15,
                16, 28, 19, 31,
                8, 4, 11, 7,
                24, 20, 27, 23,
                2, 14, 1, 13,
                18, 30, 17, 29,
                10, 6, 9, 5,
                26, 22, 25, 21,
            ]
        },
        { 5, 3, [0, 12, 7, 3, 9, 14, 8, 1, 5, 11, 6, 4, 10, 13, 2] },
        { 3, 3, [0, 5, 2, 3, 8, 7, 6, 1, 4] },
    };

    /// <summary>
    /// 界面会用到的全部尺寸：2 的幂的各种组合（2 到 64），再加三个手工矩阵。
    /// </summary>
    public static TheoryData<int, int> AllSupportedSizes
    {
        get
        {
            var data = new TheoryData<int, int>();
            int[] powers = [2, 4, 8, 16, 32, 64];
            foreach (int w in powers)
            {
                foreach (int h in powers)
                {
                    data.Add(w, h);
                }
            }

            data.Add(3, 3);
            data.Add(5, 3);
            data.Add(3, 5);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(PublishedExamples))]
    public void Generate_MatchesPublishedExamples(int width, int height, int[] expected)
    {
        Assert.Equal(expected, BayerMatrix.Generate(width, height));
    }

    [Theory]
    [MemberData(nameof(AllSupportedSizes))]
    public void Generate_IsAPermutationOfZeroToNMinusOne(int width, int height)
    {
        int[] matrix = BayerMatrix.Generate(width, height);

        // 阈值矩阵必须恰好包含 0..N-1 各一次，否则某些亮度层级会缺失或重复
        Assert.Equal(Enumerable.Range(0, width * height), matrix.Order());
    }

    [Fact]
    public void Generate_ThreeByFiveIsTransposeOfFiveByThree()
    {
        int[] wide = BayerMatrix.Generate(5, 3);
        int[] tall = BayerMatrix.Generate(3, 5);

        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                Assert.Equal(wide[(x * 5) + y], tall[(y * 3) + x]);
            }
        }
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(0, 4)]
    [InlineData(6, 6)]
    [InlineData(3, 4)]
    [InlineData(7, 3)]
    public void Generate_RejectsUnsupportedSizes(int width, int height)
    {
        Assert.False(BayerMatrix.IsSupported(width, height));
        Assert.Throws<ArgumentException>(() => BayerMatrix.Generate(width, height));
    }
}
