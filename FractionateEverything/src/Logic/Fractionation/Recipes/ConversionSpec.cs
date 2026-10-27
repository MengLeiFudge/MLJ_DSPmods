using System;
using System.Collections.Generic;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Recipes;

/// <summary>单输入转化的固定系列与候选；同一次成功只抽取一个产物。</summary>
internal sealed class ConversionSpec(int id, int input, int stage, string name,
    (int Item, int Count, bool Recovery, float Weight)[] outputs) {
    public int Id { get; } = id;
    /// <summary>预先注册的完整配方名称键。</summary>
    public string Name { get; } = name;
    public string Key => $"conversion:input:{Inputs[0].Item}";
    public int Stage { get; } = stage;
    public (int Item, int Count, float Consume)[] Inputs { get; } = [(input, 4, 1f)];
    public (int Item, int Count, bool Recovery, float Weight)[] Outputs { get; } = outputs;
    public const float Success = 0.08f;
    public static float RecoveryRatio(int level) => 0.5f + Math.Min(6, Math.Max(0, level)) * 0.125f;

    /// <summary>扩展输入占独立尾号；缺少原型不压缩其他配方身份。</summary>
    public static IReadOnlyList<ConversionSpec> CreateAll(bool genesis = false, bool orbitalRing = false) {
        List<ConversionSpec> result = [];
        AddChain([(1, I传送带, 2, "传送带转化"), (2, I高速传送带, 2, "高速传送带转化"), (3, I极速传送带, 3, "极速传送带转化")]);
        if (genesis) {
            AddChain([(4, I液氢燃料棒, 2, "液氢燃料棒转化"), (22, IGB焦油燃料棒, 2, "焦油燃料棒转化"), (23, IGB铀燃料棒, 3, "创世铀燃料棒转化"),
                (5, I氘核燃料棒, 3, "氘核燃料棒转化"), (24, IGB氦三燃料棒, 4, "氦三燃料棒转化"), (6, I反物质燃料棒, 5, "反物质燃料棒转化")]);
        } else if (orbitalRing) {
            AddChain([(4, IOR化学燃料棒, 2, "化学燃料棒转化"), (26, IOR铀燃料棒, 3, "星环铀燃料棒转化"),
                (5, I氘核燃料棒, 3, "氘核燃料棒转化"), (6, I反物质燃料棒, 5, "反物质燃料棒转化")]);
        } else {
            AddChain([(4, I液氢燃料棒, 2, "液氢燃料棒转化"), (5, I氘核燃料棒, 3, "氘核燃料棒转化"), (6, I反物质燃料棒, 5, "反物质燃料棒转化")]);
        }
        if (genesis) {
            AddChain([(7, IGB基础制造台, 2, "基础制造台转化"), (8, IGB标准制造单元, 3, "标准制造单元转化"),
                (9, IGB高精度装配线, 4, "高精度装配线转化"), (25, IGB物质重组工厂, 5, "物质重组工厂转化")]);
        } else {
            AddChain([(7, I制造台MkI, 2, "制造台MkI转化"), (8, I制造台MkII, 3, "制造台MkII转化"), (9, I制造台MkIII, 4, "制造台MkIII转化")]);
        }
        AddChain([(10, I电弧熔炉, 2, "电弧熔炉转化"), (11, I位面熔炉, 4, "位面熔炉转化")]);
        AddChain([(12, I化工厂, 2, "化工厂转化"), (13, I量子化工厂, 4, "量子化工厂转化")]);
        AddChain([(14, I采矿机, 2, "采矿机转化"), (15, I大型采矿机, 5, "大型采矿机转化")]);
        AddChain([(16, I能量碎片, 3, "能量碎片转化"), (17, I黑雾矩阵, 3, "黑雾矩阵转化"), (18, I物质重组器, 3, "物质重组器转化"),
            (19, I硅基神经元, 4, "硅基神经元转化"), (20, I负熵奇点, 5, "负熵奇点转化"), (21, I核心素, 6, "核心素转化")]);
        return result;

        void AddChain((int Id, int Item, int Stage, string Name)[] items) {
            for (int inputIndex = 0; inputIndex < items.Length; inputIndex++) {
                var candidates = new (int Item, int Count, bool Recovery, float Weight)[items.Length];
                for (int outputIndex = 0; outputIndex < items.Length; outputIndex++) {
                    int distance = Math.Abs(outputIndex - inputIndex);
                    int count = outputIndex <= inputIndex ? (int)Math.Pow(4, inputIndex - outputIndex + 1) : 1;
                    float weight = distance == 0 ? 0.1f : (float)Math.Pow(0.01, distance - 1);
                    candidates[outputIndex] = (items[outputIndex].Item, count, outputIndex < inputIndex, weight);
                }
                result.Add(new ConversionSpec(30300 + items[inputIndex].Id, items[inputIndex].Item, items[inputIndex].Stage, items[inputIndex].Name, candidates));
            }
        }
    }
}
