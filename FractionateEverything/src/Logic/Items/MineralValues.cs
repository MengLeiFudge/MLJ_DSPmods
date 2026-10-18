using System;
using System.Collections.Generic;
using System.Linq;
using static FE.Utils.Utils;

namespace FE.Logic.Items;

/// <summary>按主题矿脉的期望权重估算相对稀缺性，不生成星区或依赖当前存档。</summary>
internal static class MineralValues {
    /// <summary>有天然矿脉估值的物品；配方推导不覆盖这些统一基准。</summary>
    private static readonly HashSet<int> NaturalItems = [];

    /// <summary>物品是否已按天然资源权重定价。</summary>
    public static bool HasNaturalValue(int itemId) => NaturalItems.Contains(itemId);

    /// <summary>岩石主题等权，普通恒星采用中性稀有指数1；特殊恒星用固定参考占比补充。</summary>
    /// <param name="values">唯一全局估值表。</param>
    public static void Apply(float[] values) {
        NaturalItems.Clear();
        ThemeProto[] themes = LDB.themes.dataArray.Where(theme => theme != null
            && theme.PlanetType != EPlanetType.Gas).ToArray();
        if (themes.Length == 0) { throw new InvalidOperationException("没有可用于矿物估值的岩石主题。"); }
        Dictionary<int, double> weights = [];
        foreach (VeinProto vein in LDB.veins.dataArray) {
            if (vein == null || vein.MiningItem <= 0 || vein.MiningItem >= values.Length) { continue; }
            double weight = 0d;
            foreach (ThemeProto theme in themes) {
                int index = vein.ID - 1;
                double spots = At(theme.VeinSpot, index);
                double density = At(theme.VeinCount, index);
                double richness = At(theme.VeinOpacity, index);
                double baseWeight = spots * density * richness;
                int rare = Array.IndexOf(theme.RareVeins ?? [], vein.ID);
                if (rare >= 0 && theme.RareSettings?.Length >= rare * 4 + 4) {
                    double chance = Probability(theme.RareSettings[rare * 4 + 1]);
                    double repeats = Probability(theme.RareSettings[rare * 4 + 2]);
                    double amount = Probability(theme.RareSettings[rare * 4 + 3]);
                    baseWeight = (1d - chance) * baseWeight
                        + chance * (spots + ExpectedGroups(repeats)) * amount * amount;
                }
                weight += baseWeight / themes.Length;
            }
            // 原版白矮星追加金伯利、分形与光栅；中子星和黑洞追加单极矿。
            // 参考占比白矮星2/64，中子星与黑洞合计2/64，只是跨存档一致的稀缺权重。
            weight += vein.ID switch {
                9 or 10 => 2d / 64d * (1d + ExpectedGroups(0.45d)) * 0.7d,
                12 => 2d / 64d * ExpectedGroups(0.5d) * 0.7d * 0.3d,
                14 => 2d / 64d * ExpectedGroups(0.65d) * 0.7d * 0.3d,
                _ => 0d,
            };
            if (weight > 0d) {
                weights.TryGetValue(vein.MiningItem, out double previous);
                weights[vein.MiningItem] = previous + weight;
            }
        }
        double reference = (weights.TryGetValue(I铁矿, out double iron) ? iron : 0d)
            + (weights.TryGetValue(I铜矿, out double copper) ? copper : 0d);
        reference /= 2d;
        if (reference <= 0d) { throw new InvalidOperationException("主题矿脉缺少铁铜估值基准。"); }
        foreach (var pair in weights) {
            values[pair.Key] = pair.Key is I铁矿 or I铜矿 ? 1f
                : (float)Math.Max(1d, Math.Round(reference / pair.Value, 2, MidpointRounding.AwayFromZero));
            NaturalItems.Add(pair.Key);
        }
    }

    /// <summary>首次矿群加最多11次连续追加的几何分布期望。</summary>
    private static double ExpectedGroups(double chance) {
        double sum = 1d;
        double term = 1d;
        for (int i = 1; i < 12; i++) { term *= chance; sum += term; }
        return sum;
    }

    private static double Probability(float value) => Math.Max(0d, Math.Min(1d, value));
    private static double At(int[] values, int index) => values != null && index >= 0 && index < values.Length
        ? Math.Max(0, values[index]) : 0d;
    private static double At(float[] values, int index) => values != null && index >= 0 && index < values.Length
        ? Math.Max(0f, values[index]) : 0d;
}
