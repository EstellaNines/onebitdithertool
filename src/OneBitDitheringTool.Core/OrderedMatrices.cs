// 本文件中的矩阵数据取自 makeworld-the-better-one/dither 的 ordered_ditherers.go
// （https://github.com/makeworld-the-better-one/dither ，提交 6055917）。
// 该项目以 Mozilla Public License 2.0 授权，因此本文件同样适用 MPL-2.0：
//
//   This Source Code Form is subject to the terms of the Mozilla Public
//   License, v. 2.0. If a copy of the MPL was not distributed with this
//   file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
// 与原文件的差异：转写为 C#；矩阵改为行优先的一维数组；依结构修正了三处疑似笔误（见各处注释）。
// 仓库中其余文件不受 MPL-2.0 约束，详见根目录的 NOTICE。

using System.Collections.Immutable;

namespace OneBitDitheringTool.Core;

/// <summary>
/// 聚点（clustered-dot）有序抖动矩阵的内置目录，名称与顺序同原版界面的下拉框。
/// </summary>
public static class OrderedMatrices
{
    private static readonly OrderedMatrix[] Matrices =
    [
        // 出处：libcaca 研究文第二部分（http://caca.zoy.org/study/part2.html），文中称其「产生富有艺术感的竖线纹理」。
        new OrderedMatrix("Vertical5x3", 5, 3, 15,
        [
            9, 3, 0, 6, 12,
            10, 4, 1, 7, 13,
            11, 5, 2, 8, 14,
        ]),
        // 出处：dither 作者对 Vertical5x3 旋转得到的版本。
        new OrderedMatrix("Horizontal3x5", 3, 5, 15,
        [
            9, 10, 11,
            3, 4, 5,
            0, 1, 2,
            6, 7, 8,
            12, 13, 14,
        ]),
        // 出处：dither 作者对 ClusteredDotHorizontalLine 旋转得到的版本。
        new OrderedMatrix("ClusteredDotVerticalLine", 6, 6, 36,
        [
            35, 23, 11, 5, 17, 29,
            33, 21, 9, 3, 15, 27,
            31, 19, 7, 1, 13, 25,
            30, 18, 6, 0, 12, 24,
            32, 20, 8, 2, 14, 26,
            34, 22, 10, 4, 16, 28,
        ]),
        // 出处：Ulichney《Digital Halftoning》图 5.13，网点沿水平线聚集。
        new OrderedMatrix("ClusteredDotHorizontalLine", 6, 6, 36,
        [
            35, 33, 31, 30, 32, 34,
            23, 21, 19, 18, 20, 22,
            11, 9, 7, 6, 8, 10,
            5, 3, 1, 0, 2, 4,
            17, 15, 13, 12, 14, 16,
            29, 27, 25, 24, 26, 28,
        ]),
        // 出处：libcaca 研究文第二部分（http://caca.zoy.org/study/part2.html）；非对角，网点排成方格。
        new OrderedMatrix("ClusteredDot4x4", 4, 4, 16,
        [
            12, 5, 6, 13,
            4, 0, 1, 7,
            11, 3, 2, 8,
            15, 10, 9, 14,
        ]),
        // 出处：Ulichney《Digital Halftoning》图 5.13；深色区域沿螺旋增长，而不是深浅网点交替。
        new OrderedMatrix("ClusteredDotSpiral5x5", 5, 5, 25,
        [
            20, 21, 22, 23, 24,
            19, 6, 7, 8, 9,
            18, 5, 0, 1, 10,
            17, 4, 3, 2, 11,
            16, 15, 14, 13, 12,
        ]),
        // 出处：Ulichney《Digital Halftoning》图 5.9；原值整体减 1 使取值从 0 开始。
        new OrderedMatrix("ClusteredDot6x6", 6, 6, 36,
        [
            34, 29, 17, 21, 30, 35,
            28, 14, 9, 16, 20, 31,
            13, 8, 4, 5, 15, 19,
            12, 3, 0, 1, 10, 18,
            27, 7, 2, 6, 23, 24,
            33, 26, 11, 22, 25, 32,
        ]),
        // 出处：https://archive.is/71e9G 中的「central white point」。
        new OrderedMatrix("ClusteredDot6x6-2", 6, 6, 36,
        [
            34, 25, 21, 17, 29, 33,
            30, 13, 9, 5, 12, 24,
            18, 6, 1, 0, 8, 20,
            22, 10, 2, 3, 4, 16,
            26, 14, 7, 11, 15, 28,
            35, 31, 19, 23, 27, 32,
        ]),
        // 出处：https://archive.is/71e9G 中的「balanced centered point」。
        new OrderedMatrix("ClusteredDot6x6-3", 6, 6, 36,
        [
            30, 22, 16, 21, 33, 35,
            24, 11, 7, 9, 26, 28,
            13, 5, 0, 2, 14, 19,
            15, 3, 1, 4, 12, 18,
            27, 8, 6, 10, 25, 29,
            32, 20, 17, 23, 31, 34,
        ]),
        // 出处：Ulichney《Digital Halftoning》图 5.4「M = 3」；原为 45° 对角矩阵，已转成矩形，取值减 1。
        new OrderedMatrix("ClusteredDotDiagonal6x6", 6, 6, 18,
        [
            8, 6, 7, 9, 11, 10,
            5, 0, 1, 12, 17, 16,
            4, 3, 2, 13, 14, 15,
            // 修正：第 3 行第 5 列原为 8，现为 7。对角矩阵由两个对称方块平铺而成，每个值应恰好出现两次；此处的 8 使 8 出现三次、7 只出现一次，与左上方块不一致，应为 7。
            9, 11, 10, 8, 6, 7,
            12, 17, 16, 5, 0, 1,
            13, 14, 15, 4, 3, 2,
        ]),
        // 出处：Lau & Arce《Modern Digital Halftoning》第 2 版图 1.5。原书取值范围是 0 到 64 而不是 0 到 63，dither 作者把 64 改成 63，避免纯黑出现零星白点（因此 63 出现两次、32 缺失，属有意为之）。
        new OrderedMatrix("ClusteredDot8x8", 8, 8, 64,
        [
            3, 9, 17, 27, 25, 15, 7, 1,
            11, 29, 38, 46, 44, 36, 23, 5,
            19, 40, 52, 58, 56, 50, 34, 13,
            31, 48, 60, 63, 62, 54, 42, 21,
            30, 47, 59, 63, 61, 53, 41, 20,
            18, 39, 51, 57, 55, 49, 33, 12,
            10, 28, 37, 45, 43, 35, 22, 4,
            2, 8, 16, 26, 24, 14, 6, 0,
        ]),
        // 出处：libcaca 研究文第二部分（http://caca.zoy.org/study/part2.html），文中称其「模仿报纸的网点技术」。
        new OrderedMatrix("ClusteredDotDiagonal8x8", 8, 8, 64,
        [
            24, 10, 12, 26, 35, 47, 49, 37,
            8, 0, 2, 14, 45, 59, 61, 51,
            22, 6, 4, 16, 43, 57, 63, 53,
            30, 20, 18, 28, 33, 41, 55, 39,
            34, 46, 48, 36, 25, 11, 13, 27,
            44, 58, 60, 50, 9, 1, 3, 15,
            42, 56, 62, 52, 23, 7, 5, 17,
            32, 40, 54, 38, 31, 21, 19, 29,
        ]),
        // 出处：Ulichney《Digital Halftoning》图 5.4「M = 4」；与 ClusteredDotDiagonal8x8 近似但灰阶更少。
        new OrderedMatrix("ClusteredDotDiagonal8x8-2", 8, 8, 32,
        [
            13, 11, 12, 15, 18, 20, 19, 16,
            4, 3, 2, 9, 27, 28, 29, 22,
            5, 0, 1, 10, 26, 31, 30, 21,
            8, 6, 7, 14, 23, 25, 24, 17,
            18, 20, 19, 16, 13, 11, 12, 15,
            27, 28, 29, 22, 4, 3, 2, 9,
            26, 31, 30, 21, 5, 0, 1, 10,
            23, 25, 24, 17, 8, 6, 7, 14,
        ]),
        // 出处：https://archive.is/71e9G 中的「diagonal ordered matrix with balanced centered points」。
        new OrderedMatrix("ClusteredDotDiagonal8x8-3", 8, 8, 32,
        [
            13, 9, 5, 12, 18, 22, 26, 19,
            6, 1, 0, 8, 25, 30, 31, 23,
            10, 2, 3, 4, 21, 29, 28, 27,
            14, 7, 11, 15, 17, 24, 20, 16,
            18, 22, 26, 19, 13, 9, 5, 12,
            25, 30, 31, 23, 6, 1, 0, 8,
            21, 29, 28, 27, 10, 2, 3, 4,
            17, 24, 20, 16, 14, 7, 11, 15,
        ]),
        // 出处：Ulichney《Digital Halftoning》图 5.4「M = 8」；原为 45° 对角矩阵，取值减 1。
        new OrderedMatrix("ClusteredDotDiagonal16x16", 16, 16, 128,
        [
            63, 58, 50, 40, 41, 51, 59, 60, 64, 69, 77, 87, 86, 76, 68, 67,
            57, 33, 27, 18, 19, 28, 34, 52, 70, 94, 100, 109, 108, 99, 93, 75,
            49, 26, 13, 11, 12, 15, 29, 44, 78, 101, 114, 116, 115, 112, 98, 83,
            // 修正：第 3 行第 8 列原为 87，现为 88。原表中 87 出现四次而 88 缺失。对照方块边缘相对格子的差值规律（恒为 ±4），此格应为 88；其镜像格（第 11 行第 0 列）同理。这一处是依数值规律的推断，不像 6×6 那样由结构唯一确定。
            39, 17, 4, 3, 2, 9, 20, 42, 88, 110, 123, 124, 125, 118, 107, 85,
            38, 16, 5, 0, 1, 10, 21, 43, 89, 111, 122, 127, 126, 117, 106, 84,
            48, 25, 8, 6, 7, 14, 30, 45, 79, 102, 119, 121, 120, 113, 97, 82,
            56, 32, 24, 23, 22, 31, 35, 53, 71, 95, 103, 104, 105, 96, 92, 74,
            62, 55, 47, 37, 36, 46, 54, 61, 65, 72, 80, 90, 91, 81, 73, 66,
            64, 69, 77, 87, 86, 76, 68, 67, 63, 58, 50, 40, 41, 51, 59, 60,
            70, 94, 100, 109, 108, 99, 93, 75, 57, 33, 27, 18, 19, 28, 34, 52,
            78, 101, 114, 116, 115, 112, 98, 83, 49, 26, 13, 11, 12, 15, 29, 44,
            // 修正：第 11 行第 0 列原为 87，现为 88。同上，是第 3 行第 8 列的镜像副本。
            88, 110, 123, 124, 125, 118, 107, 85, 39, 17, 4, 3, 2, 9, 20, 42,
            89, 111, 122, 127, 126, 117, 106, 84, 38, 16, 5, 0, 1, 10, 21, 43,
            79, 102, 119, 121, 120, 113, 97, 82, 48, 25, 8, 6, 7, 14, 30, 45,
            71, 95, 103, 104, 105, 96, 92, 74, 56, 32, 24, 23, 22, 31, 35, 53,
            65, 72, 80, 90, 91, 81, 73, 66, 62, 55, 47, 37, 36, 46, 54, 61,
        ]),
    ];

    /// <summary>
    /// 全部内置矩阵，顺序同原版界面。
    /// </summary>
    public static IReadOnlyList<OrderedMatrix> All => Matrices;

    /// <summary>
    /// 按名称取矩阵，名称不区分大小写。
    /// </summary>
    /// <param name="name">矩阵名称，如 <c>ClusteredDot4x4</c>。</param>
    /// <returns>对应的矩阵。</returns>
    /// <exception cref="ArgumentException">没有该名称的矩阵。</exception>
    public static OrderedMatrix Get(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        foreach (OrderedMatrix matrix in Matrices)
        {
            if (string.Equals(matrix.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return matrix;
            }
        }

        throw new ArgumentException($"没有名为 {name} 的有序抖动矩阵。", nameof(name));
    }
}
