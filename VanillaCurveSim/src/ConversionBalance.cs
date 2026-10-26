using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FE.Logic.Fractionation.Recipes;

namespace VanillaCurveSim;

/// <summary>转化配方的期望物料核算，与FE编译同一份配方规格，可独立导出。</summary>
internal static class ConversionBalance {
    public static string Write(string root) {
        Dictionary<int, string> names = File.ReadLines(Path.Combine(root, "gamedata", "Vanilla", "gameData.csv"))
            .Select(line => line.TrimStart('\uFEFF').Split(','))
            .Where(parts => parts.Length > 2 && parts[0].StartsWith("I", StringComparison.Ordinal)
                && int.TryParse(parts[0].Substring(1), out _))
            .ToDictionary(parts => int.Parse(parts[0].Substring(1)), parts => parts[2]);
        string Name(int item) => names.TryGetValue(item, out string value) ? value : $"物品{item}";
        string Number(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
        var report = new StringBuilder("# 转化配方物料核算\r\n\r\n");
        report.AppendLine("来源：与FE运行目录共用ConversionSpec。按1000次尝试、无供养品质或献祭增益核算；成功率8%，失败92%原样回流。每次成功只抽一种候选。工作营养按实际主料支付，待机消耗另计；各单塔期望不能替代完整回环的物料平衡。");
        report.AppendLine();
        report.AppendLine("| 稳定ID | 每成功轮参与物料（括号内为消耗概率） | 1000次尝试净消耗 | 期望产物（回收特质0级→6级） | 工作营养／折合蓝矩阵 |");
        report.AppendLine("| --- | --- | --- | --- | --- |");
        IReadOnlyList<ConversionSpec> recipes = ConversionSpec.CreateAll();
        foreach (ConversionSpec recipe in recipes) {
            double successes = 1000d * ConversionSpec.Success;
            string input = string.Join(" + ", recipe.Inputs.Select(i => $"{Name(i.Item)}×{i.Count}（{Number(i.Consume * 100d)}%）"));
            string used = string.Join(" + ", recipe.Inputs.Select(i => $"{Name(i.Item)}×{Number(i.Count * successes * i.Consume)}"));
            double totalWeight = recipe.Outputs.Sum(o => (double)o.Weight);
            string output = string.Join(" + ", recipe.Outputs.Select(o => $"{Name(o.Item)}×{Number(o.Count * successes * o.Weight / totalWeight * (o.Recovery ? ConversionSpec.RecoveryRatio(0) : 1d))}"
                + (o.Recovery ? $"→{Number(o.Count * successes * o.Weight / totalWeight * ConversionSpec.RecoveryRatio(6))}" : "")));
            double nutrition = recipe.Inputs.Where(i => i.Consume >= 0.9999f).Sum(i => i.Count) * successes;
            report.AppendLine($"| {recipe.Id} | {input} | {used} | {output} | {Number(nutrition)}／{Number(nutrition / 5.25d)} |");
        }
        report.AppendLine();
        report.AppendLine("回收特质由50%提高至125%，只乘向低档拆解的数量。两级链每成功轮消耗4件，升级得到1件高档、降级得到16×回收率件低档，自身候选返还4件。忽略自身的中性循环，两段往返的主体倍率等于回收率；满级提供25%主体余量，仍需支付两座塔的矩阵营养。更多档位含极低概率跨级候选，应按全部候选核算，不能把多种期望产物视为每次同时发放。");
        string path = Path.Combine(root, "VanillaCurveSim", "Reports", "ConversionBalance.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, report.ToString(), new UTF8Encoding(true));
        return path;
    }
}
