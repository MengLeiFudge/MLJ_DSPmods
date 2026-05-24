using System;
using System.Collections.Generic;
using System.Linq;

namespace VanillaCurveSim;

internal sealed class FeScenarioSimulator {
    private readonly FeFractionationEvaluator fractionationEvaluator = new();
    private readonly FeGachaEvaluator gachaEvaluator = new();
    private readonly FeFragmentEconomyEvaluator fragmentEconomyEvaluator = new();

    public static void RunSelfCheck() {
        FeFractionationEvaluator.RunSelfCheck();
        FeGachaEvaluator.RunSelfCheck();
        FeFragmentEconomyEvaluator.RunSelfCheck();
    }

    public IReadOnlyList<FractionationScenarioResult> BuildTreatments(
        IReadOnlyList<StrategySimulationResult> baselineResults) {
        var results = new List<FractionationScenarioResult>(baselineResults.Count);
        foreach (StrategySimulationResult baseline in baselineResults) {
            SimulationMode mode = baseline.Strategy == PlayerStrategyKind.Speedrun
                ? SimulationMode.FeSpeedrun
                : SimulationMode.FeConventional;
            results.Add(BuildTreatment(baseline, mode));
        }
        return results;
    }

    private FractionationScenarioResult BuildTreatment(StrategySimulationResult baseline, SimulationMode mode) {
        var result = new FractionationScenarioResult {
            ScenarioName = mode == SimulationMode.FeSpeedrun ? "FE 速通" : "FE 常规",
            BaselineStrategyName = baseline.Strategy == PlayerStrategyKind.Speedrun ? "速通" : "常规",
            Mode = mode,
        };
        FeWarehouse scenarioWarehouse = FeWarehouse.CreateInitial(mode == SimulationMode.FeSpeedrun);
        var openedStages = new HashSet<int>();

        List<PhaseSummary> orderedPhases = baseline.PhaseSummaries
            .OrderBy(phase => phase.StartSeconds)
            .ToList();
        double weightedResourceGain = 0.0;
        double weightedEnergyEfficiency = 0.0;
        double weightedGachaNetValue = 0.0;
        double weightedGrowthNetValue = 0.0;
        var economyTotals = new Dictionary<FragmentEconomyPolicy, WeightedFragmentEconomyTotals>();
        double totalWeight = 0.0;

        foreach (PhaseSummary phase in orderedPhases) {
            int stageIndex = FeReference.GetStageIndex(phase.Phase);
            FractionationConfigSnapshot config = FeReference.CreateScenarioConfig(mode, stageIndex);
            FractionationPhaseEstimate fractionation = fractionationEvaluator.EvaluatePhase(config);
            if (openedStages.Add(stageIndex)) {
                scenarioWarehouse.AddRecipeSlotsForStage(stageIndex, config.IsSpeedrun);
            }
            scenarioWarehouse.Fragments += fractionation.EstimatedFragments;

            double baselinePhaseSeconds = Math.Max(1.0, phase.PhaseEndSeconds - phase.StartSeconds);
            double currentMatrixRatePerSecond = ResolveCurrentMatrixRate(phase, stageIndex);
            double drawShare = FeReference.GetDrawShare(config);
            double openingShare = FeReference.GetOpeningDrawShare(config);
            double matrixBudget = currentMatrixRatePerSecond * baselinePhaseSeconds * drawShare;
            scenarioWarehouse.AddMatrix(stageIndex, matrixBudget);
            int openingDrawCount = (int)Math.Floor(matrixBudget
                                                   * openingShare
                                                   / Math.Max(1,
                                                       FeReference.GetDrawMatrixCost(isOpeningLinePool: true, 1)));
            int protoDrawCount = (int)Math.Floor(matrixBudget
                                                 * (1.0 - openingShare)
                                                 / Math.Max(1,
                                                     FeReference.GetDrawMatrixCost(isOpeningLinePool: false, 1)));

            gachaEvaluator.RunPhaseSimulation(config, scenarioWarehouse, stageIndex, openingDrawCount, protoDrawCount,
                randomSeed: 97 + stageIndex * 17 + (config.IsSpeedrun ? 1000 : 0),
                out GachaPhaseEstimate gacha, out GrowthExchangeEstimate growth);
            IReadOnlyList<FragmentEconomyEstimate> fragmentEconomies = fragmentEconomyEvaluator.EvaluatePhase(config,
                baselinePhaseSeconds, currentMatrixRatePerSecond, matrixBudget, fractionation, gacha, growth);

            double timeAcceleration = 1.0
                                      + Math.Max(0.0, fractionation.TimeAccelerationBonus)
                                      + Math.Max(0.0, gacha.DrawNetValuePerMatrix * 0.18)
                                      + Math.Max(0.0, growth.NetValuePerPoint * 0.08);
            double treatmentPhaseSeconds = baselinePhaseSeconds / Math.Max(1.0, timeAcceleration);

            var phaseBreakdown = new PhaseImpactBreakdown {
                PhaseName = FormatPhaseName(phase.Phase),
                BaselineSeconds = baselinePhaseSeconds,
                TreatmentSeconds = treatmentPhaseSeconds,
                TimeCompressionRatio = baselinePhaseSeconds / Math.Max(1.0, treatmentPhaseSeconds),
                ResourceGainMultiplier = fractionation.ResourceGainMultiplier,
                EnergyEfficiencyMultiplier = fractionation.EnergyEfficiencyMultiplier,
                GachaNetValuePerMatrix = gacha.DrawNetValuePerMatrix,
                GrowthExchangeNetValue = growth.NetValuePerPoint,
                FragmentYieldMultiplier = fractionation.FragmentYieldMultiplier,
            };
            phaseBreakdown.FragmentEconomies.AddRange(fragmentEconomies);
            phaseBreakdown.Notes.Add($"开线抽数 {gacha.OpeningDrawCount}，原胚抽数 {gacha.ProtoDrawCount}");
            phaseBreakdown.Notes.Add($"成长池积分 {gacha.PoolPointsGenerated:0.##}，成长净值 {growth.GrowthUtility:0.##}");
            if (fractionation.FragmentYieldMultiplier > 1.2) {
                phaseBreakdown.Notes.Add("精馏残片收益已明显高于基线。");
            }
            if (gacha.DrawNetValuePerMatrix > 1.0) {
                phaseBreakdown.Notes.Add("抽卡矩阵净值已超过 1，说明抽卡开始成为正反馈。");
            }
            result.Phases.Add(phaseBreakdown);

            result.BaselineTotalSeconds += baselinePhaseSeconds;
            result.TreatmentTotalSeconds += treatmentPhaseSeconds;
            double weight = baselinePhaseSeconds;
            weightedResourceGain += fractionation.ResourceGainMultiplier * weight;
            weightedEnergyEfficiency += fractionation.EnergyEfficiencyMultiplier * weight;
            weightedGachaNetValue += gacha.DrawNetValuePerMatrix * weight;
            weightedGrowthNetValue += growth.NetValuePerPoint * weight;
            AccumulateEconomyTotals(economyTotals, fragmentEconomies, weight);
            totalWeight += weight;
        }

        if (totalWeight <= 0.0001) {
            totalWeight = 1.0;
        }

        result.FinalConfig = FeReference.CreateScenarioConfig(mode, stageIndex: 5);
        result.Metrics.ResourceGainMultiplier = weightedResourceGain / totalWeight;
        result.Metrics.EnergyEfficiencyMultiplier = weightedEnergyEfficiency / totalWeight;
        result.Metrics.GachaNetValuePerMatrix = weightedGachaNetValue / totalWeight;
        result.Metrics.GrowthExchangeNetValue = weightedGrowthNetValue / totalWeight;
        ApplyEconomyMetrics(result.Metrics, economyTotals, totalWeight);
        result.Metrics.FractionationImpact = result.BaselineTotalSeconds / Math.Max(1.0, result.TreatmentTotalSeconds);
        result.Metrics.CompositeImpactIndex = ComputeCompositeImpactIndex(result.Metrics);

        result.Findings.Add(BuildImpactFinding(result.Metrics.FractionationImpact));
        result.Findings.Add(BuildResourceFinding(result.Metrics.ResourceGainMultiplier,
            result.Metrics.GachaNetValuePerMatrix));
        result.Findings.Add(BuildEnergyFinding(result.Metrics.EnergyEfficiencyMultiplier));
        result.Findings.Add(BuildFragmentEconomyFinding(result.Metrics));
        result.Findings.Add(BuildRectificationFinding(result.Metrics));
        return result;
    }

    private static void AccumulateEconomyTotals(Dictionary<FragmentEconomyPolicy, WeightedFragmentEconomyTotals> totals,
        IReadOnlyList<FragmentEconomyEstimate> estimates, double weight) {
        foreach (FragmentEconomyEstimate estimate in estimates) {
            if (!totals.TryGetValue(estimate.Policy, out WeightedFragmentEconomyTotals total)) {
                total = new WeightedFragmentEconomyTotals();
                totals[estimate.Policy] = total;
            }
            total.NetFragments += estimate.NetFragments * weight;
            total.SinkCoverageRatio += estimate.SinkCoverageRatio * weight;
            total.MatrixFeedbackRatio += estimate.SameStageMatrixFeedbackRatio * weight;
            total.RectificationUtilityScore += estimate.RectificationUtilityScore * weight;
        }
    }

    private static void ApplyEconomyMetrics(FractionationEffectMetrics metrics,
        IReadOnlyDictionary<FragmentEconomyPolicy, WeightedFragmentEconomyTotals> totals, double totalWeight) {
        ApplyOneEconomyMetric(metrics, totals, FragmentEconomyPolicy.CurrentApproximation, totalWeight);
        ApplyOneEconomyMetric(metrics, totals, FragmentEconomyPolicy.ProposedBalanced, totalWeight);
    }

    private static void ApplyOneEconomyMetric(FractionationEffectMetrics metrics,
        IReadOnlyDictionary<FragmentEconomyPolicy, WeightedFragmentEconomyTotals> totals,
        FragmentEconomyPolicy policy, double totalWeight) {
        if (!totals.TryGetValue(policy, out WeightedFragmentEconomyTotals total)) {
            return;
        }

        double divisor = Math.Max(1.0, totalWeight);
        if (policy == FragmentEconomyPolicy.CurrentApproximation) {
            metrics.CurrentFragmentNetBalance = total.NetFragments / divisor;
            metrics.CurrentFragmentSinkCoverageRatio = total.SinkCoverageRatio / divisor;
            metrics.CurrentMatrixFeedbackRatio = total.MatrixFeedbackRatio / divisor;
            metrics.CurrentRectificationUtilityScore = total.RectificationUtilityScore / divisor;
            return;
        }

        metrics.ProposedFragmentNetBalance = total.NetFragments / divisor;
        metrics.ProposedFragmentSinkCoverageRatio = total.SinkCoverageRatio / divisor;
        metrics.ProposedMatrixFeedbackRatio = total.MatrixFeedbackRatio / divisor;
        metrics.ProposedRectificationUtilityScore = total.RectificationUtilityScore / divisor;
    }

    private static double ResolveCurrentMatrixRate(PhaseSummary baselinePhase, int stageIndex) {
        string matrixName = stageIndex switch {
            0 => "电磁矩阵",
            1 => "能量矩阵",
            2 => "结构矩阵",
            3 => "信息矩阵",
            4 => "引力矩阵",
            5 => "宇宙矩阵",
            _ => "电磁矩阵",
        };

        if (baselinePhase.MatrixRatesPerSecond.TryGetValue(matrixName, out double rate)) {
            return rate;
        }
        return Math.Max(0.0, baselinePhase.MatrixTargetRatePerSecond);
    }

    private static double ComputeCompositeImpactIndex(FractionationEffectMetrics metrics) {
        double normalizedImpact = metrics.FractionationImpact - 1.0;
        double normalizedResource = metrics.ResourceGainMultiplier - 1.0;
        double normalizedEnergy = metrics.EnergyEfficiencyMultiplier - 1.0;
        double normalizedGacha = metrics.GachaNetValuePerMatrix;
        double normalizedFeedbackRisk = Math.Max(0.0, metrics.CurrentMatrixFeedbackRatio - metrics.ProposedMatrixFeedbackRatio);
        return 1.0
               + normalizedImpact * 0.45
               + normalizedResource * 0.25
               + normalizedEnergy * 0.15
               + normalizedGacha * 0.12
               + normalizedFeedbackRisk * 0.20;
    }

    private static string BuildImpactFinding(double fractionationImpact) {
        if (fractionationImpact >= 1.6) {
            return "分馏影响度已超过 1.60，需要重点复核是否过强。";
        }
        if (fractionationImpact >= 1.3) {
            return "分馏影响度达到明显增强区间。";
        }
        if (fractionationImpact >= 1.1) {
            return "分馏影响度处于轻度增强区间。";
        }
        return "分馏影响度接近基线。";
    }

    private static string BuildResourceFinding(double resourceGainMultiplier, double gachaNetValuePerMatrix) {
        if (resourceGainMultiplier >= 1.5 || gachaNetValuePerMatrix >= 1.1) {
            return "资源净增益已偏高，存在正反馈失控风险。";
        }
        if (resourceGainMultiplier >= 1.25 || gachaNetValuePerMatrix >= 0.9) {
            return "资源净增益明显高于基线，但仍可继续观察。";
        }
        return "资源净增益相对温和。";
    }

    private static string BuildEnergyFinding(double energyEfficiencyMultiplier) {
        if (energyEfficiencyMultiplier >= 1.5) {
            return "单位电力收益已显著高于基线。";
        }
        if (energyEfficiencyMultiplier >= 1.2) {
            return "单位电力收益较基线有稳定提升。";
        }
        return "单位电力收益接近基线。";
    }

    private static string BuildFragmentEconomyFinding(FractionationEffectMetrics metrics) {
        if (metrics.CurrentMatrixFeedbackRatio >= 0.30 || metrics.CurrentFragmentSinkCoverageRatio < 0.75) {
            return
                $"当前残片经济不健康：消耗覆盖 {metrics.CurrentFragmentSinkCoverageRatio:0.00}，同阶段矩阵回流 {metrics.CurrentMatrixFeedbackRatio:0.00}。";
        }
        if (metrics.CurrentMatrixFeedbackRatio >= 0.18 || metrics.CurrentFragmentNetBalance > 50.0) {
            return
                $"当前残片经济需要继续观察：阶段净结余 {metrics.CurrentFragmentNetBalance:0.#}，同阶段矩阵回流 {metrics.CurrentMatrixFeedbackRatio:0.00}。";
        }
        return "当前残片经济在模拟口径下未显示明显通胀。";
    }

    private static string BuildRectificationFinding(FractionationEffectMetrics metrics) {
        if (metrics.CurrentRectificationUtilityScore < 0.75) {
            return
                $"当前精馏塔存在感不足；建议政策可把存在感评分从 {metrics.CurrentRectificationUtilityScore:0.00} 拉到 {metrics.ProposedRectificationUtilityScore:0.00}。";
        }
        return
            $"精馏塔在当前模拟口径下有稳定作用；建议政策存在感评分 {metrics.ProposedRectificationUtilityScore:0.00}。";
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

    private sealed class WeightedFragmentEconomyTotals {
        public double NetFragments { get; set; }
        public double SinkCoverageRatio { get; set; }
        public double MatrixFeedbackRatio { get; set; }
        public double RectificationUtilityScore { get; set; }
    }
}
