using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace VanillaCurveSim;

internal static class ComparisonReporter {
    public static string Write(string solutionDir, SimulationComparisonReport report) {
        string outputDir = Path.Combine(solutionDir, "gamedata", "curve-sim");
        Directory.CreateDirectory(outputDir);

        string jsonPath = Path.Combine(outputDir, "fe-impact-report.json");
        File.WriteAllText(jsonPath, JsonConvert.SerializeObject(report, Formatting.Indented), Encoding.UTF8);

        string markdownPath = Path.Combine(outputDir, "fe-impact-report.md");
        File.WriteAllText(markdownPath, BuildMarkdown(report), Encoding.UTF8);
        return markdownPath;
    }

    private static string BuildMarkdown(SimulationComparisonReport report) {
        var sb = new StringBuilder();
        sb.AppendLine("# FE Impact Comparison");
        sb.AppendLine();
        sb.AppendLine($"生成时间：{report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("说明：baseline 保留原版模拟结果；treatment 为 FE 第一版影响估算，用于比较“分馏带来了多大的影响”。");
        sb.AppendLine("说明：第一版 FE treatment 采用 baseline overlay，不宣称逐 tick 精确仿真。");
        sb.AppendLine();

        foreach (FractionationScenarioResult scenario in report.TreatmentResults) {
            sb.AppendLine($"## {scenario.ScenarioName} vs {scenario.BaselineStrategyName}");
            sb.AppendLine();
            sb.AppendLine($"- baseline 总时长：{FormatDuration(scenario.BaselineTotalSeconds)}");
            sb.AppendLine($"- FE 总时长：{FormatDuration(scenario.TreatmentTotalSeconds)}");
            sb.AppendLine($"- 分馏影响度：{scenario.Metrics.FractionationImpact:0.000}");
            sb.AppendLine($"- 资源净增益倍率：{scenario.Metrics.ResourceGainMultiplier:0.000}");
            sb.AppendLine($"- 能效倍率：{scenario.Metrics.EnergyEfficiencyMultiplier:0.000}");
            sb.AppendLine($"- 抽卡净值/矩阵：{scenario.Metrics.GachaNetValuePerMatrix:0.000}");
            sb.AppendLine($"- 成长净值/积分：{scenario.Metrics.GrowthExchangeNetValue:0.000}");
            sb.AppendLine($"- 综合影响指数：{scenario.Metrics.CompositeImpactIndex:0.000}");
            sb.AppendLine($"- 预设聚焦：{scenario.FinalConfig.Focus}");
            sb.AppendLine($"- 五塔等级：交互 {scenario.FinalConfig.InteractionTowerLevel} / 复制 {scenario.FinalConfig.MineralReplicationTowerLevel} / 点聚 {scenario.FinalConfig.PointAggregateTowerLevel} / 转化 {scenario.FinalConfig.ConversionTowerLevel} / 精馏 {scenario.FinalConfig.RectificationTowerLevel}");
            sb.AppendLine();
            sb.AppendLine("### 关键结论");
            sb.AppendLine();
            foreach (string finding in scenario.Findings) {
                sb.AppendLine($"- {finding}");
            }
            sb.AppendLine();
            sb.AppendLine("### 阶段对照");
            sb.AppendLine();
            sb.AppendLine("| 阶段 | baseline | FE | 压缩率 | 资源倍率 | 能效倍率 | 抽卡净值 |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|");
            foreach (PhaseImpactBreakdown phase in scenario.Phases) {
                sb.AppendLine($"| {phase.PhaseName} | {FormatDuration(phase.BaselineSeconds)} | {FormatDuration(phase.TreatmentSeconds)} | {phase.TimeCompressionRatio:0.000} | {phase.ResourceGainMultiplier:0.000} | {phase.EnergyEfficiencyMultiplier:0.000} | {phase.GachaNetValuePerMatrix:0.000} |");
            }
            sb.AppendLine();
            sb.AppendLine("### 阶段说明");
            sb.AppendLine();
            foreach (PhaseImpactBreakdown phase in scenario.Phases) {
                if (phase.Notes.Count == 0) {
                    continue;
                }
                sb.AppendLine($"- {phase.PhaseName}：{string.Join("；", phase.Notes)}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string FormatDuration(double seconds) {
        TimeSpan span = TimeSpan.FromSeconds(seconds);
        if (span.TotalHours >= 1) {
            return $"{(int)span.TotalHours}h {span.Minutes}m {span.Seconds}s";
        }

        return $"{span.Minutes}m {span.Seconds}s";
    }
}
