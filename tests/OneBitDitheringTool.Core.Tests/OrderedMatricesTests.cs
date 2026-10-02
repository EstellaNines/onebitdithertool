namespace OneBitDitheringTool.Core.Tests;

public class OrderedMatricesTests
{
    // 由两个对称方块平铺出 45° 网点的「对角」矩阵：每个值恰好出现两次，且左上块等于右下块、右上块等于左下块
    private static readonly string[] DiagonalTiled =
    [
        "ClusteredDotDiagonal6x6",
        "ClusteredDotDiagonal8x8-2",
        "ClusteredDotDiagonal8x8-3",
        "ClusteredDotDiagonal16x16",
    ];

    public static TheoryData<string> Names
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (OrderedMatrix matrix in OrderedMatrices.All)
            {
                data.Add(matrix.Name);
            }

            return data;
        }
    }

    [Fact]
    public void Catalog_HasFifteenMatricesInTheOriginalOrder()
    {
        string[] expected =
        [
            "Vertical5x3",
            "Horizontal3x5",
            "ClusteredDotVerticalLine",
            "ClusteredDotHorizontalLine",
            "ClusteredDot4x4",
            "ClusteredDotSpiral5x5",
            "ClusteredDot6x6",
            "ClusteredDot6x6-2",
            "ClusteredDot6x6-3",
            "ClusteredDotDiagonal6x6",
            "ClusteredDot8x8",
            "ClusteredDotDiagonal8x8",
            "ClusteredDotDiagonal8x8-2",
            "ClusteredDotDiagonal8x8-3",
            "ClusteredDotDiagonal16x16",
        ];

        Assert.Equal(expected, OrderedMatrices.All.Select(m => m.Name));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Dimensions_AreConsistent(string name)
    {
        OrderedMatrix matrix = OrderedMatrices.Get(name);

        Assert.Equal(matrix.Width * matrix.Height, matrix.Values.Length);
        Assert.True(matrix.Max > 0);

        // 每个阈值都必须小于 Max，否则 (值+1)/Max 会超过 1，阈值偏移越出合理范围
        Assert.All(matrix.Values, v => Assert.InRange(v, 0, matrix.Max - 1));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Values_FollowTheirDesign(string name)
    {
        OrderedMatrix matrix = OrderedMatrices.Get(name);
        var counts = new int[matrix.Max];
        foreach (int v in matrix.Values)
        {
            counts[v]++;
        }

        if (name == "ClusteredDot8x8")
        {
            // 原书取值范围是 0 到 64；dither 作者把 64 改成 63，避免纯黑出现零星白点，
            // 因此 63 出现两次、32 缺失。这是有意为之，不属于笔误
            Assert.Equal(2, counts[63]);
            Assert.Equal(0, counts[32]);
            Assert.All(Enumerable.Range(0, 63).Where(v => v != 32), v => Assert.Equal(1, counts[v]));
        }
        else if (DiagonalTiled.Contains(name))
        {
            Assert.All(counts, c => Assert.Equal(2, c));
            AssertQuadrantsMirror(matrix);
        }
        else
        {
            Assert.All(counts, c => Assert.Equal(1, c));
        }
    }

    [Fact]
    public void Get_IsCaseInsensitiveAndRejectsUnknownNames()
    {
        Assert.Same(OrderedMatrices.Get("ClusteredDot4x4"), OrderedMatrices.Get("clustereddot4X4"));
        Assert.Throws<ArgumentException>(() => OrderedMatrices.Get("NoSuchMatrix"));
    }

    private static void AssertQuadrantsMirror(OrderedMatrix matrix)
    {
        int halfW = matrix.Width / 2;
        int halfH = matrix.Height / 2;
        for (int y = 0; y < halfH; y++)
        {
            for (int x = 0; x < halfW; x++)
            {
                // 左上块 = 右下块，右上块 = 左下块
                Assert.Equal(At(matrix, x, y), At(matrix, x + halfW, y + halfH));
                Assert.Equal(At(matrix, x + halfW, y), At(matrix, x, y + halfH));
            }
        }
    }

    private static int At(OrderedMatrix matrix, int x, int y) => matrix.Values[(y * matrix.Width) + x];
}
