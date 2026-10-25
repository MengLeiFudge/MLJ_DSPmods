using System;
using System.Collections.Generic;
using System.Linq;
using static FE.Utils.Utils;

namespace FE.Logic.Items;

/// <summary>按主题矿脉、气体产率与海洋分布估算自然资源价值，不生成星区或依赖当前存档。</summary>
internal static class MineralValues {
    /// <summary>已有天然来源估值的物品；配方推导不覆盖这些统一基准。</summary>
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
        ApplyFluidValues(values);
    }

    /// <summary>气体按各主题采集产率求和，海洋按出现主题数量估算；分别以氢2、水1作为价格尺度。</summary>
    /// <param name="values">统一估值表；多种天然来源采用较低值。</param>
    private static void ApplyFluidValues(float[] values) {
        Dictionary<int, double> gases = [];
        Dictionary<int, double> oceans = [];
        foreach (ThemeProto theme in LDB.themes.dataArray) {
            if (theme == null) { continue; }
            if (theme.PlanetType == EPlanetType.Gas) {
                for (int i = 0; i < (theme.GasItems?.Length ?? 0); i++) {
                    int id = theme.GasItems[i];
                    double speed = At(theme.GasSpeeds, i);
                    if (id <= 0 || id >= values.Length || !LDB.items.Exist(id)
                        || !(speed > 0d) || double.IsInfinity(speed)) { continue; }
                    gases.TryGetValue(id, out double previous);
                    gases[id] = previous + speed;
                }
            } else if (theme.WaterItemId > 0 && theme.WaterItemId < values.Length && LDB.items.Exist(theme.WaterItemId)) {
                oceans.TryGetValue(theme.WaterItemId, out double previous);
                oceans[theme.WaterItemId] = previous + 1d;
            }
        }
        ApplyFluidPrices(gases, I氢, 2d, values);
        ApplyFluidPrices(oceans, I水, 1d, values);
    }

    /// <summary>同类来源按相对丰度定价；缺少参考物时以该类最常见资源作为基准。</summary>
    /// <param name="weights">资源产率或分布权重。</param>
    /// <param name="referenceId">同类参考物品身份。</param>
    /// <param name="referenceValue">参考物品的基础价值尺度。</param>
    /// <param name="values">统一估值表。</param>
    private static void ApplyFluidPrices(Dictionary<int, double> weights, int referenceId, double referenceValue, float[] values) {
        if (weights.Count == 0) { return; }
        double reference = weights.TryGetValue(referenceId, out double weight) ? weight : weights.Values.Max();
        foreach (var pair in weights) {
            float price = (float)Math.Max(1d, Math.Round(referenceValue * reference / pair.Value, 2, MidpointRounding.AwayFromZero));
            values[pair.Key] = NaturalItems.Contains(pair.Key) ? Math.Min(values[pair.Key], price) : price;
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
