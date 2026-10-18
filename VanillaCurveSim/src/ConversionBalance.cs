using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FE.Logic.Fractionation.Recipes;

namespace VanillaCurveSim;

/// <summary>转化配方的期望物料核算，仅运行显式请求的配方表，不启动旧模拟器自检或游戏。</summary>
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
        report.AppendLine("来源：与FE运行目录编译同一份ConversionSpec，名称读取原版导出表。按1000次尝试、无献祭增益、辅料充足核算期望；基础成功率8%，主料失败92%原样回流。高级塔电耗为0，食物另列，不包含带速、待机与供料短缺。原型是否在模组组合中可得，由运行时来源闭包决定。");
        report.AppendLine();
        report.AppendLine("| 稳定ID | 每成功轮参与物料（括号内为消耗概率） | 1000次尝试净消耗 | 期望产物（回收特质0级→6级） | 工作营养／折合蓝矩阵 |");
        report.AppendLine("| --- | --- | --- | --- | --- |");
        IReadOnlyList<ConversionSpec> recipes = ConversionSpec.CreateAll();
        foreach (ConversionSpec recipe in recipes) {
            double successes = 1000d * ConversionSpec.Success;
            string input = string.Join(" + ", recipe.Inputs.Select(i => $"{Name(i.Item)}×{i.Count}（{Number(i.Consume * 100d)}%）"));
            string used = string.Join(" + ", recipe.Inputs.Select(i => $"{Name(i.Item)}×{Number(i.Count * successes * i.Consume)}"));
            string output = string.Join(" + ", recipe.Outputs.Select(o => $"{Name(o.Item)}×{Number(o.Count * successes * (o.Recovery ? 0.5d : 1d))}"
                + (o.Recovery ? $"→{Number(o.Count * successes * 0.8d)}" : "")));
            double nutrition = recipe.Inputs.Where(i => i.Consume >= 0.9999f).Sum(i => i.Count) * successes;
            report.AppendLine($"| {recipe.Id} | {input} | {used} | {output} | {Number(nutrition)}／{Number(nutrition / 5.25d)} |");
        }
        report.AppendLine();
        report.AppendLine("## 可逆链净流");
        report.AppendLine();
        report.AppendLine("以下按一轮正向、再把全部主要产物逆向处理，扣除两边主料后显示剩余；负数代表净投入。辅料及食物另付。单位为期望件，不代表每批确定扣除小数件。");
        report.AppendLine();
        report.AppendLine("| 正向／逆向 | 主体净流 | 辅料净投入 | 工作营养 |");
        report.AppendLine("| --- | --- | --- | --- |");
        foreach (ConversionSpec forward in recipes) {
            ConversionSpec reverse = recipes.FirstOrDefault(candidate => candidate.Id > forward.Id
                && candidate.Inputs[0].Item == forward.Outputs[0].Item
                && candidate.Outputs[0].Item == forward.Inputs[0].Item);
            if (reverse == null) { continue; }
            double turns = forward.Outputs[0].Count / (double)reverse.Inputs[0].Count;
            double net = reverse.Outputs[0].Count * turns - forward.Inputs[0].Count;
            var auxiliary = forward.Inputs.Skip(1).Select(i => (i.Item, Count: i.Count * (double)i.Consume))
                .Concat(reverse.Inputs.Skip(1).Select(i => (i.Item, Count: i.Count * (double)i.Consume * turns)))
                .GroupBy(i => i.Item).Select(group => $"{Name(group.Key)}×{Number(group.Sum(i => i.Count))}");
            double nutrition = forward.Inputs[0].Count + reverse.Inputs[0].Count * turns;
            report.AppendLine($"| {forward.Id}／{reverse.Id} | {Name(forward.Inputs[0].Item)} {Number(net)} | {string.Join(" + ", auxiliary)} | {Number(nutrition)} |");
        }
        report.AppendLine();
        report.AppendLine("燃料回喂：30301与30302各成功一次，净消耗液氢燃料棒1、反物质1、钛合金0.02、湮灭约束球0.02；0级回收时重氢净0、氢净+2.5，6级时重氢净+3、氢净+4；产出反物质燃料棒1，消耗工作营养2。回流不能免除燃料棒、反物质与营养投入。设备升级不返还辅料；硅材料与黑雾相邻往返不增加主体总量，并持续消耗催化剂与营养。黑雾入口30307把工业原料转换为能量碎片，不要求已有黑雾材料。");
        string path = Path.Combine(root, "VanillaCurveSim", "Reports", "ConversionBalance.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, report.ToString(), new UTF8Encoding(true));
        return path;
    }
}
