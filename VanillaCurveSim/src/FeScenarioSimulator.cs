using System;
using System.Collections.Generic;
using System.Linq;

namespace VanillaCurveSim;

internal sealed class FeScenarioSimulator {
    private readonly FeFractionationEvaluator fractionationEvaluator = new();
    private readonly FeGachaEvaluator gachaEvaluator = new();

    public static void RunSelfCheck() {
        FeFractionationEvaluator.RunSelfCheck();
        FeGachaEvaluator.RunSelfCheck();
    }

    public IReadOnlyList<FractionationScenarioResult> BuildTreatments(IReadOnlyList<StrategySimulationResult> baselineResults) {
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
            int openingDrawCount = (int)Math.Floor(matrixBudget * openingShare
                / Math.Max(1, FeReference.GetDrawMatrixCost(isOpeningLinePool: true, 1)));
            int protoDrawCount = (int)Math.Floor(matrixBudget * (1.0 - openingShare)
                / Math.Max(1, FeReference.GetDrawMatrixCost(isOpeningLinePool: false, 1)));

            gachaEvaluator.RunPhaseSimulation(config, scenarioWarehouse, stageIndex, openingDrawCount, protoDrawCount,
                randomSeed: 97 + stageIndex * 17 + (config.IsSpeedrun ? 1000 : 0),
                out GachaPhaseEstimate gacha, out GrowthExchangeEstimate growth);

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
        result.Metrics.FractionationImpact = result.BaselineTotalSeconds / Math.Max(1.0, result.TreatmentTotalSeconds);
        result.Metrics.CompositeImpactIndex = ComputeCompositeImpactIndex(result.Metrics);

        result.Findings.Add(BuildImpactFinding(result.Metrics.FractionationImpact));
        result.Findings.Add(BuildResourceFinding(result.Metrics.ResourceGainMultiplier, result.Metrics.GachaNetValuePerMatrix));
        result.Findings.Add(BuildEnergyFinding(result.Metrics.EnergyEfficiencyMultiplier));
        return result;
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
        return 1.0
            + normalizedImpact * 0.45
            + normalizedResource * 0.25
            + normalizedEnergy * 0.15
            + normalizedGacha * 0.15;
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
}
