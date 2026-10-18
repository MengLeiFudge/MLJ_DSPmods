using System.Collections.Generic;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Recipes;

/// <summary>转化配方的纯数值规格；运行目录和离线物料核算共同读取，避免两份倍率漂移。</summary>
internal sealed class ConversionSpec(int id, string key, int stage,
    (int Item, int Count, float Consume)[] inputs, (int Item, int Count, bool Recovery)[] outputs) {
    public int Id { get; } = id;
    public string Key { get; } = key;
    public int Stage { get; } = stage;
    public (int Item, int Count, float Consume)[] Inputs { get; } = inputs;
    public (int Item, int Count, bool Recovery)[] Outputs { get; } = outputs;
    public const float Success = 0.08f;

    /// <summary>稳定顺序；首四项保留已有存档身份，新增相邻黑雾转换均有实际辅料损耗。</summary>
    public static IReadOnlyList<ConversionSpec> CreateAll() {
        var result = new List<ConversionSpec> {
            new(30301, "conversion:fuel-hydrogen-deuterium", 2,
                [(I液氢燃料棒, 1, 1f), (I重氢, 10, 0.5f), (I钛合金, 1, 0.02f)],
                [(I氘核燃料棒, 1, false), (I氢, 5, true)]),
            new(30302, "conversion:fuel-deuterium-antimatter", 5,
                [(I氘核燃料棒, 1, 1f), (I反物质, 2, 0.5f), (I湮灭约束球, 1, 0.02f)],
                [(I反物质燃料棒, 1, false), (I重氢, 10, true)]),
            new(30303, "conversion:mining-drill", 3,
                [(I采矿机, 1, 1f), (I处理器, 2, 0.4f), (I量子芯片, 1, 0.02f)],
                [(I大型采矿机, 1, false)]),
            new(30304, "conversion:smelter", 4,
                [(I电弧熔炉, 1, 1f), (I卡西米尔晶体, 2, 0.4f), (I单极磁石, 1, 0.02f)],
                [(I位面熔炉, 1, false)]),
            new(30305, "conversion:silicon-crystallize", 3,
                [(I高纯硅块, 2, 1f), (I高能石墨, 1, 0.3f), (I能量碎片, 1, 0.02f)],
                [(I晶格硅, 2, false)]),
            new(30306, "conversion:silicon-dissolve", 3,
                [(I晶格硅, 2, 1f), (I铁块, 1, 0.3f), (I能量碎片, 1, 0.02f)],
                [(I高纯硅块, 2, false)]),
            // 30307保留为空缺；无战斗入口由元数据额度兑换提供，后续身份不重排。

        };
        int[] chain = [I能量碎片, I黑雾矩阵, I物质重组器, I硅基神经元, I负熵奇点, I核心素];
        int[] lowerCounts = [1, 3, 1, 1, 4];
        int[] upperCounts = [1, 2, 1, 1, 1];
        for (int i = 0; i < chain.Length - 1; i++) {
            int stage = i < 2 ? 3 : i + 2;
            result.Add(new(30308 + i * 2, $"conversion:darkfog-up:{chain[i]}", stage,
                [(chain[i], lowerCounts[i], 1f), (I高纯硅块, 1, 0.02f)],
                [(chain[i + 1], upperCounts[i], false)]));
            result.Add(new(30309 + i * 2, $"conversion:darkfog-down:{chain[i + 1]}", stage,
                [(chain[i + 1], upperCounts[i], 1f), (I高能石墨, 1, 0.02f)],
                [(chain[i], i == 4 ? 3 : lowerCounts[i], false)]));
        }
        return result;
    }
}
