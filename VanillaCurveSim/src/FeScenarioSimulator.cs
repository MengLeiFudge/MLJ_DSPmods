using System.Collections.Generic;
using System.Linq;

namespace VanillaCurveSim;

/// <summary>组织现行框架的独立工况；各配方、研究样本和扩容实验不共用虚构的物资余额。</summary>
internal sealed class FeScenarioSimulator {
    public SimulationComparisonReport Run(FeDataSet data, FeSimulationOptions options,
        IReadOnlyList<StrategySimulationResult> baselines) {
        var report = new SimulationComparisonReport { Options = options, SourceSha256 = data.SourceSha256 };
        report.BaselineResults.AddRange(baselines);
        foreach (FeScenarioConfig scenario in options.Scenarios) {
            List<FeOfferingMinute> offering = FeResearchSimulator.RunOffering(scenario, options.DurationSeconds, options.Seed);
            report.Offerings.Add(scenario.Name, offering);
            // 同一配方在不同工况使用相同初始种子，目录按稳定身份排序。
            for (int i = 0; i < data.Recipes.Count; i++) {
                report.Production.Add(FeProductionSimulator.Run(data.Recipes[i], scenario, data,
                    options.DurationSeconds, offering, unchecked(options.Seed + (uint)i)));
            }
        }
        foreach (int stack in options.Scenarios.Select(s => s.Stack).Distinct().OrderBy(s => s)) {
            for (int stage = 0; stage <= 6; stage++) {
                foreach (double value in new[] { 1d, 100d, 10000d }) {
                    report.Research.Add(FeResearchSimulator.RunSamples($"阶段{stage}单项线索",
                        [FeRules.SampleBudgets[stage]], value, stack, options.DurationSeconds, options.Seed));
                }
            }
            report.Research.Add(FeResearchSimulator.RunSamples("黄/紫两项线索共用样本",
                [FeRules.SampleBudgets[3], FeRules.SampleBudgets[4]], 100, stack, options.DurationSeconds, options.Seed));
            for (int stage = 1; stage <= 6; stage++) {
                report.Recovery.Add(FeResearchSimulator.RunRecovery(stage, stack, options.DurationSeconds, options.Seed));
            }
            report.ExpansionByStack.Add(stack, FeResearchSimulator.RunExpansion(stack, options.DurationSeconds, options.Seed));
        }
        return report;
    }
}
