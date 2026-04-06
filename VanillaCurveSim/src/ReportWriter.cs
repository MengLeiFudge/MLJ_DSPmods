using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace VanillaCurveSim;

internal static class ReportWriter {
    public static string Write(string solutionDir, IReadOnlyList<StrategySimulationResult> results) {
        string outputDir = Path.Combine(solutionDir, "gamedata", "curve-sim");
        Directory.CreateDirectory(outputDir);
        foreach (StrategySimulationResult result in results) {
            result.NormalizeForOutput();
        }

        string jsonPath = Path.Combine(outputDir, "vanilla-strategy-report.json");
        File.WriteAllText(jsonPath, JsonConvert.SerializeObject(results, Formatting.Indented), Encoding.UTF8);

        string markdownPath = Path.Combine(outputDir, "vanilla-strategy-report.md");
        File.WriteAllText(markdownPath, BuildMarkdown(results), Encoding.UTF8);
        return markdownPath;
    }

    private static string BuildMarkdown(IReadOnlyList<StrategySimulationResult> results) {
        var sb = new StringBuilder();
        sb.AppendLine("# Vanilla Curve Simulation");
        sb.AppendLine();
        sb.AppendLine($"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("说明：电力字段为阶段级轻量近似，只统计主体生产建筑，不表示逐秒电网仿真。");
        sb.AppendLine("说明：配方喷涂模式按 Mk.III 与原版最高档机器离线固化，阶段模拟直接复用这张静态表。");
        sb.AppendLine();

        foreach (StrategySimulationResult result in results) {
            sb.AppendLine($"## {FormatStrategyName(result.Strategy)}");
            sb.AppendLine();
            sb.AppendLine($"- 目标科技：{result.GoalTech}");
            sb.AppendLine($"- 总科技数：{result.TechOrder.Count}");
            sb.AppendLine($"- 总 Hash 成本：{result.TotalHashCost}");
            sb.AppendLine($"- 前 12 个科技：{string.Join(" -> ", result.TechOrder.Take(12))}");
            sb.AppendLine($"- 关键产线：{FormatStrategyLineSummary(result)}");
            sb.AppendLine("- 推进规则：当前矩阵层科技全部研究完成后，才进入下一层。");
            sb.AppendLine();
            if (result.Milestones.Count > 0) {
                sb.AppendLine("### 开局里程碑");
                sb.AppendLine();
                sb.AppendLine($"- 数据来源：{FormatStringOrNone(result.MilestoneSource)}");
                sb.AppendLine();
                sb.AppendLine("| 时间点 | 操作 | 补充说明 |");
                sb.AppendLine("|---|---|---|");
                foreach (TimelineMilestone milestone in result.Milestones) {
                    sb.AppendLine($"| {FormatDuration(milestone.Seconds)} | {EscapeTable(milestone.Name)} | {EscapeTable(milestone.Notes)} |");
                }
                sb.AppendLine();
            }
            foreach (PhaseSummary phase in result.PhaseSummaries) {
                sb.AppendLine($"### {FormatPhaseName(phase.Phase)}");
                sb.AppendLine();
                sb.AppendLine($"- 时间：{FormatDuration(phase.StartSeconds)} -> {FormatDuration(phase.PhaseEndSeconds)}（共 {FormatDuration(phase.PhaseEndSeconds - phase.StartSeconds)}）");
                sb.AppendLine($"- 代表科技：{FormatCompactTechs(phase.Techs)}");
                sb.AppendLine($"- 代表产线：{FormatPhaseLineSummary(phase)}");
                sb.AppendLine($"- 矩阵主线：目标 {FormatSingleRatePerMinute(phase.MatrixTargetRatePerSecond)}；实际 {FormatRateMapPerMinute(phase.MatrixRatesPerSecond)}；研究站 {FormatMatrixLabLayout(phase)}");
                sb.AppendLine($"- 阻塞：{FormatDuration(phase.TotalBlockingSeconds)}；主要瓶颈 {FormatBlockingSummary(phase)}");
                sb.AppendLine($"- 电力：{FormatPower(phase.TotalPowerDemandWatts)}；主力 {phase.PrimaryPowerSourceName}；发电建筑 {FormatPowerBuildings(phase.PrimaryPowerSourceName, phase.PrimaryPowerBuildingCount)}；燃料 {FormatFuelRate(phase.FuelName, phase.FuelConsumptionPerSecond)}");
                sb.AppendLine();
                sb.AppendLine("| 时间点 | 操作 | 补充说明 |");
                sb.AppendLine("|---|---|---|");
                foreach (TimelineEvent timelineEvent in phase.TimelineEvents) {
                    sb.AppendLine($"| {FormatDuration(timelineEvent.Seconds)} | {EscapeTable(timelineEvent.Action)} | {EscapeTable(timelineEvent.Notes)} |");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static string FormatStrategyName(PlayerStrategyKind strategy) {
        return strategy switch {
            PlayerStrategyKind.Conventional => "常规",
            PlayerStrategyKind.Speedrun => "速通",
            _ => strategy.ToString(),
        };
    }

    private static string FormatPhaseName(ProgressPhase phase) {
        return phase switch {
            ProgressPhase.Bootstrap or ProgressPhase.Electromagnetic => "电磁矩阵阶段",
            ProgressPhase.Energy => "能量矩阵阶段",
            ProgressPhase.Structure => "结构矩阵阶段",
            ProgressPhase.Information => "信息矩阵阶段",
            ProgressPhase.Gravity => "引力矩阵阶段",
            ProgressPhase.Universe => "宇宙矩阵阶段",
            _ => phase.ToString(),
        };
    }

    private static string FormatDuration(double seconds) {
        TimeSpan span = TimeSpan.FromSeconds(seconds);
        if (span.TotalHours >= 1) {
            return $"{(int)span.TotalHours}h {span.Minutes}m {span.Seconds}s";
        }

        return $"{span.Minutes}m {span.Seconds}s";
    }

    private static string FormatPower(long watts) {
        if (watts <= 0) {
            return "0 W";
        }

        if (watts >= 1_000_000) {
            return $"{watts / 1_000_000d:0.##} MW";
        }

        if (watts >= 1_000) {
            return $"{watts / 1_000d:0.##} kW";
        }

        return $"{watts} W";
    }

    private static string FormatPowerBuildings(string sourceName, int count) {
        return count <= 0 || string.IsNullOrEmpty(sourceName)
            ? "无"
            : $"{sourceName} x{count}";
    }

    private static string FormatFuelRate(string fuelName, double fuelPerSecond) {
        return fuelPerSecond <= 0 || string.IsNullOrEmpty(fuelName)
            ? "无"
            : $"{fuelName} {fuelPerSecond:0.#######}/s";
    }

    private static string FormatRatePerMinute(string itemName, double perMinute) {
        return perMinute <= 0 || string.IsNullOrEmpty(itemName)
            ? "无"
            : $"{itemName} {perMinute:0.##}/min";
    }

    private static string FormatStringOrNone(string value) =>
        string.IsNullOrEmpty(value) ? "无" : value;

    private static string FormatCompactTechs(List<string> techs) =>
        techs.Count == 0
            ? "无"
            : string.Join(" / ", techs.Take(4));

    private static string FormatStrategyLineSummary(StrategySimulationResult result) {
        List<string> lineActions = result.PhaseSummaries
            .SelectMany(phase => phase.TimelineEvents)
            .Where(timelineEvent => timelineEvent.Action.Contains("开始建设 ") || timelineEvent.Action.Contains("开始制作 "))
            .Select(timelineEvent => timelineEvent.Action)
            .Distinct()
            .Take(8)
            .ToList();
        return lineActions.Count == 0
            ? "无"
            : string.Join(" / ", lineActions);
    }

    private static string FormatPhaseLineSummary(PhaseSummary phase) {
        List<string> lineActions = phase.TimelineEvents
            .Where(timelineEvent => timelineEvent.Action.Contains("开始建设 ") || timelineEvent.Action.Contains("开始制作 "))
            .Select(timelineEvent => timelineEvent.Action)
            .Distinct()
            .Take(6)
            .ToList();
        return lineActions.Count == 0
            ? "无"
            : string.Join(" / ", lineActions);
    }

    private static string FormatBlockingSummary(PhaseSummary phase) {
        if (string.IsNullOrEmpty(phase.PrimaryBlockingItemName)) {
            return "无";
        }

        return $"{phase.PrimaryBlockingItemName}（{FormatDuration(phase.PrimaryBlockingSeconds)}，{FormatStringOrNone(phase.PrimaryBlockingReason)}）";
    }

    private static string FormatTopBuildings(Dictionary<string, int> buildings) =>
        buildings.Count == 0
            ? "无"
            : string.Join(" / ", buildings.OrderByDescending(p => p.Value).Take(8).Select(p => $"{p.Key} x{p.Value}"));

    private static string FormatSingleRatePerMinute(double ratePerSecond) =>
        ratePerSecond <= 0
            ? "无"
            : $"{ratePerSecond * 60:0.##} 个/min";

    private static string FormatMatrixLabLayout(PhaseSummary phase) =>
        phase.MatrixLabCounts.Count == 0
            ? "无"
            : string.Join(" / ", phase.MatrixLabCounts.OrderByDescending(p => p.Value).Take(8)
                .Select(p => {
                    int baseCount = phase.MatrixLabBaseCounts.TryGetValue(p.Key, out int mappedBaseCount)
                        ? mappedBaseCount
                        : (int)Math.Ceiling(p.Value / (double)Math.Max(1, phase.LabStackLevel));
                    return $"{p.Key}研究站 {p.Value} 台（底座 {baseCount} 座，{phase.LabStackLevel} 层堆叠）";
                }));

    private static string FormatTopInventory(Dictionary<string, double> inventory) =>
        inventory.Count == 0
            ? "无"
            : string.Join(" / ", inventory.OrderByDescending(p => p.Value).Take(10).Select(p => $"{p.Key} {p.Value:0.##}"));

    private static string FormatRateMapPerMinute(Dictionary<string, double> ratesPerSecond) =>
        ratesPerSecond.Count == 0
            ? "无"
            : string.Join(" / ", ratesPerSecond.OrderByDescending(p => p.Value).Take(10)
                .Select(p => $"{p.Key} {p.Value * 60:0.##} 个/min"));

    private static string FormatCountMap(Dictionary<string, double> counts) =>
        counts.Count == 0
            ? "无"
            : string.Join(" / ", counts.OrderByDescending(p => p.Value).Take(10)
                .Select(p => $"{p.Key} {p.Value:0.##} 个"));

    private static string EscapeTable(string value) =>
        value.Replace("|", "\\|");
}
