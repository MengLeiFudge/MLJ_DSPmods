using System;
using System.Collections.Generic;

namespace VanillaCurveSim;

internal sealed class FeFragmentEconomyEvaluator {
    private const double FractionationFragmentChance = 0.02;

    public static void RunSelfCheck() {
        var evaluator = new FeFragmentEconomyEvaluator();
        FractionationConfigSnapshot config = FeReference.CreateScenarioConfig(SimulationMode.FeConventional, 2);
        FractionationPhaseEstimate fractionation = new() {
            EstimatedFragments = 180,
            ResourceGainMultiplier = 1.15,
            FragmentYieldMultiplier = 1.3,
        };
        var gacha = new GachaPhaseEstimate {
            MatrixSpent = 120,
            OpeningDrawCount = 70,
            ProtoDrawCount = 50,
            DrawNetValuePerMatrix = 0.9,
        };
        var growth = new GrowthExchangeEstimate {
            ConsumedFragments = 80,
            ConsumedPoolPoints = 120,
            GrowthUtility = 30,
        };

        IReadOnlyList<FragmentEconomyEstimate> estimates = evaluator.EvaluatePhase(config, baselinePhaseSeconds: 600,
            currentMatrixRatePerSecond: 0.2, matrixBudget: 120, fractionation, gacha, growth);
        SimulatorSelfCheck.Require(estimates.Count == 2, "残片经济评估应同时输出当前近似和建议平衡政策。");
        SimulatorSelfCheck.Require(estimates[0].TotalFaucet > 0.0, "当前近似政策应有残片来源。");
        SimulatorSelfCheck.Require(estimates[1].SameStageMatrixFeedbackRatio < estimates[0].SameStageMatrixFeedbackRatio,
            "建议平衡政策应降低同阶段矩阵回流风险。");
        SimulatorSelfCheck.Require(estimates[1].TotalMemoryFaucet > 0.0,
            "建议平衡政策应输出阶段性 Memory 来源。");
    }

    public IReadOnlyList<FragmentEconomyEstimate> EvaluatePhase(FractionationConfigSnapshot config,
        double baselinePhaseSeconds, double currentMatrixRatePerSecond, double matrixBudget,
        FractionationPhaseEstimate fractionation, GachaPhaseEstimate gacha, GrowthExchangeEstimate growth) {
        return [
            EvaluatePolicy(FragmentEconomyPolicy.CurrentApproximation, config, baselinePhaseSeconds,
                currentMatrixRatePerSecond, matrixBudget, fractionation, gacha, growth),
            EvaluatePolicy(FragmentEconomyPolicy.ProposedBalanced, config, baselinePhaseSeconds,
                currentMatrixRatePerSecond, matrixBudget, fractionation, gacha, growth),
        ];
    }

    private static FragmentEconomyEstimate EvaluatePolicy(FragmentEconomyPolicy policy,
        FractionationConfigSnapshot config, double baselinePhaseSeconds, double currentMatrixRatePerSecond,
        double matrixBudget, FractionationPhaseEstimate fractionation, GachaPhaseEstimate gacha,
        GrowthExchangeEstimate growth) {
        int stage = FeReference.ClampStageIndex(config.StageIndex);
        bool proposed = policy == FragmentEconomyPolicy.ProposedBalanced;

        double rawRectification = Math.Max(0.0, fractionation.EstimatedFragments);
        double rawSuccessFaucet = EstimateFractionationSuccessFaucet(config, baselinePhaseSeconds,
            currentMatrixRatePerSecond, fractionation);
        double rawDuplicateFaucet = EstimateGachaDuplicateFaucet(config, gacha);
        double rawPassiveFaucet = EstimatePassiveRewardFaucet(config);

        double rectificationFaucet = proposed ? rawRectification * GetProposedRectificationRoleFactor(stage) : rawRectification;
        double successFaucet = proposed ? rawSuccessFaucet * 0.55 : rawSuccessFaucet;
        double duplicateFaucet = proposed ? rawDuplicateFaucet * 0.12 : rawDuplicateFaucet;
        double passiveFaucet = proposed ? rawPassiveFaucet * 0.25 : rawPassiveFaucet;
        double totalFaucet = rectificationFaucet + successFaucet + duplicateFaucet + passiveFaucet;

        double growthSink = proposed
            ? growth.ConsumedFragments * 1.20 + totalFaucet * GetProposedDirectedGrowthSinkShare(stage)
            : growth.ConsumedFragments;
        double matrixExchangeSink = proposed
            ? totalFaucet * GetProposedMatrixExchangeSinkShare(stage)
            : totalFaucet * GetCurrentMatrixExchangeSinkShare(stage);
        double convenienceSink = proposed
            ? totalFaucet * GetProposedConvenienceSinkShare(stage)
            : totalFaucet * GetCurrentConvenienceSinkShare(stage);
        double totalSink = growthSink + matrixExchangeSink + convenienceSink;
        double netFragments = totalFaucet - totalSink;

        double matrixFromGrowth = EstimateCurrentMatrixFromGrowthSink(policy, stage, growthSink);
        double matrixFromExchange = matrixExchangeSink / Math.Max(1.0, GetFragmentsPerCurrentMatrix(policy, stage));
        double sameStageMatrixFeedback = (matrixFromGrowth + matrixFromExchange) / Math.Max(1.0, matrixBudget);
        double rectificationShare = totalFaucet <= 0.0001 ? 0.0 : rectificationFaucet / totalFaucet;
        double sinkCoverage = totalFaucet <= 0.0001 ? 1.0 : totalSink / totalFaucet;
        double rectificationUtility = ComputeRectificationUtilityScore(stage, rectificationShare,
            sameStageMatrixFeedback, proposed);
        double inflationRisk = ComputeInflationRisk(netFragments, totalFaucet, sinkCoverage);
        double feedbackRisk = ComputeFeedbackRisk(stage, sameStageMatrixFeedback);
        double memoryMilestoneFaucet = EstimateMemoryMilestoneFaucet(config, proposed);
        double memoryAchievementFaucet = EstimateMemoryAchievementFaucet(config, proposed);
        double memoryRectificationFaucet = EstimateMemoryRectificationFaucet(config, fractionation, proposed);

        var estimate = new FragmentEconomyEstimate {
            Policy = policy,
            PolicyName = proposed ? "建议平衡政策" : "当前近似政策",
            RectificationFaucet = rectificationFaucet,
            FractionationSuccessFaucet = successFaucet,
            GachaDuplicateFaucet = duplicateFaucet,
            PassiveRewardFaucet = passiveFaucet,
            TotalFaucet = totalFaucet,
            GrowthSink = growthSink,
            MatrixExchangeSink = matrixExchangeSink,
            ConvenienceSink = convenienceSink,
            TotalSink = totalSink,
            NetFragments = netFragments,
            SinkCoverageRatio = sinkCoverage,
            RectificationFaucetShare = rectificationShare,
            SameStageMatrixFeedbackRatio = sameStageMatrixFeedback,
            RectificationUtilityScore = rectificationUtility,
            InflationRiskScore = inflationRisk,
            PositiveFeedbackRiskScore = feedbackRisk,
            MemoryMilestoneFaucet = memoryMilestoneFaucet,
            MemoryAchievementFaucet = memoryAchievementFaucet,
            MemoryRectificationFaucet = memoryRectificationFaucet,
            TotalMemoryFaucet = memoryMilestoneFaucet + memoryAchievementFaucet + memoryRectificationFaucet,
        };
        AddWarnings(estimate, stage, proposed);
        return estimate;
    }

    private static double EstimateFractionationSuccessFaucet(FractionationConfigSnapshot config,
        double baselinePhaseSeconds, double currentMatrixRatePerSecond, FractionationPhaseEstimate fractionation) {
        double phaseThroughputProxy = Math.Max(1.0, baselinePhaseSeconds * Math.Max(0.02, currentMatrixRatePerSecond));
        double towerActivityFactor = config.IsSpeedrun ? 0.55 : 0.75;
        double resourceFactor = Math.Max(0.8, fractionation.ResourceGainMultiplier);
        return phaseThroughputProxy * towerActivityFactor * resourceFactor * FractionationFragmentChance;
    }

    private static double EstimateGachaDuplicateFaucet(FractionationConfigSnapshot config, GachaPhaseEstimate gacha) {
        double totalDraws = gacha.OpeningDrawCount + gacha.ProtoDrawCount;
        double directFragmentShare = config.IsSpeedrun ? 0.02 : 0.16;
        double overflowRecipeShare = Math.Min(0.24, 0.05 + config.StageIndex * 0.035);
        double duplicateFragmentPerDraw = 15.0 * (directFragmentShare + overflowRecipeShare);
        return totalDraws * duplicateFragmentPerDraw;
    }

    private static double EstimatePassiveRewardFaucet(FractionationConfigSnapshot config) {
        double baseReward = config.IsSpeedrun ? 90.0 : 140.0;
        double stageReward = config.IsSpeedrun ? 45.0 : 70.0;
        return baseReward + stageReward * config.StageIndex;
    }

    private static double GetProposedRectificationRoleFactor(int stage) => stage switch {
        <= 1 => 1.40,
        <= 3 => 1.80,
        _ => 2.50,
    };

    private static double GetCurrentMatrixExchangeSinkShare(int stage) => stage switch {
        <= 1 => 0.22,
        <= 3 => 0.28,
        _ => 0.34,
    };

    private static double GetProposedMatrixExchangeSinkShare(int stage) => stage switch {
        <= 1 => 0.06,
        <= 3 => 0.09,
        _ => 0.12,
    };

    private static double GetCurrentConvenienceSinkShare(int stage) => stage switch {
        <= 1 => 0.04,
        <= 3 => 0.07,
        _ => 0.10,
    };

    private static double GetProposedConvenienceSinkShare(int stage) => stage switch {
        <= 1 => 0.18,
        <= 3 => 0.24,
        _ => 0.30,
    };

    private static double GetProposedDirectedGrowthSinkShare(int stage) => stage switch {
        <= 1 => 0.18,
        <= 3 => 0.22,
        _ => 0.26,
    };

    private static double GetFragmentsPerCurrentMatrix(FragmentEconomyPolicy policy, int stage) {
        if (policy == FragmentEconomyPolicy.CurrentApproximation) {
            return 2.5 + stage * 0.35;
        }

        return stage switch {
            <= 1 => 18.0,
            <= 3 => 28.0,
            _ => 42.0,
        };
    }

    private static double EstimateCurrentMatrixFromGrowthSink(FragmentEconomyPolicy policy, int stage,
        double growthSink) {
        double matrixOfferShare = policy == FragmentEconomyPolicy.CurrentApproximation
            ? Math.Min(0.55, 0.35 + stage * 0.03)
            : Math.Max(0.08, 0.14 - stage * 0.01);
        return growthSink * matrixOfferShare / Math.Max(1.0, GetFragmentsPerCurrentMatrix(policy, stage));
    }

    private static double ComputeRectificationUtilityScore(int stage, double rectificationShare,
        double sameStageMatrixFeedback, bool proposed) {
        double targetShare = stage switch {
            <= 1 => 0.20,
            <= 3 => 0.28,
            _ => 0.40,
        };
        double shareScore = Math.Min(1.25, rectificationShare / targetShare);
        double feedbackPenalty = Math.Max(0.0, sameStageMatrixFeedback - GetFeedbackLimit(stage)) * (proposed ? 1.5 : 2.5);
        double score = shareScore - feedbackPenalty;
        return score < 0.0 ? 0.0 : score;
    }

    private static double ComputeInflationRisk(double netFragments, double totalFaucet, double sinkCoverage) {
        if (totalFaucet <= 0.0001) {
            return 0.0;
        }
        double surplusRatio = Math.Max(0.0, netFragments / totalFaucet);
        double underSinkRatio = Math.Max(0.0, 0.85 - sinkCoverage);
        return Math.Min(2.0, surplusRatio * 1.4 + underSinkRatio);
    }

    private static double ComputeFeedbackRisk(int stage, double sameStageMatrixFeedback) {
        double limit = GetFeedbackLimit(stage);
        if (limit <= 0.0001) {
            return 0.0;
        }
        return Math.Max(0.0, sameStageMatrixFeedback / limit - 1.0);
    }

    private static double GetFeedbackLimit(int stage) => stage switch {
        <= 1 => 0.15,
        <= 3 => 0.25,
        _ => 0.35,
    };

    private static double EstimateMemoryMilestoneFaucet(FractionationConfigSnapshot config, bool proposed) {
        if (!proposed) {
            return 0.0;
        }

        double baseReward = config.StageIndex switch {
            <= 0 => 0.0,
            1 => 1.0,
            2 => 1.0,
            3 => 2.0,
            4 => 2.0,
            5 => 3.0,
            _ => 4.0,
        };
        return config.IsSpeedrun && baseReward > 0.0 ? Math.Max(1.0, baseReward - 1.0) : baseReward;
    }

    private static double EstimateMemoryAchievementFaucet(FractionationConfigSnapshot config, bool proposed) {
        if (!proposed) {
            return 0.0;
        }

        double stagePressure = config.StageIndex switch {
            <= 1 => 0.15,
            <= 3 => 0.35,
            _ => 0.65,
        };
        return config.IsSpeedrun ? stagePressure * 0.75 : stagePressure;
    }

    private static double EstimateMemoryRectificationFaucet(FractionationConfigSnapshot config,
        FractionationPhaseEstimate fractionation, bool proposed) {
        if (!proposed || config.RectificationTowerLevel < 12) {
            return 0.0;
        }

        double chance = 0.0025;
        if (config.SelectedIncLevel >= 4) {
            chance += 0.0015;
        }
        if (config.SelectedIncLevel >= 8) {
            chance += 0.001;
        }

        double currentMatrixShare = config.StageIndex switch {
            <= 1 => 0.08,
            <= 3 => 0.12,
            _ => 0.16,
        };
        return Math.Max(0.0, fractionation.EstimatedFragments) * currentMatrixShare * chance;
    }

    private static void AddWarnings(FragmentEconomyEstimate estimate, int stage, bool proposed) {
        if (estimate.SinkCoverageRatio < 0.75) {
            estimate.Warnings.Add("残片消耗覆盖不足，库存会持续膨胀。");
        }
        if (estimate.SameStageMatrixFeedbackRatio > GetFeedbackLimit(stage)) {
            estimate.Warnings.Add("同阶段矩阵回流超过阶段阈值，可能跳过原版科技压力。");
        }
        if (estimate.RectificationUtilityScore < 0.75) {
            estimate.Warnings.Add(proposed
                ? "建议政策下精馏塔仍偏弱，需要继续提高落后矩阵精馏或定向成长价值。"
                : "当前政策下精馏塔存在感偏低，残片来源被任务、抽卡或兑换稀释。");
        }
        if (estimate.InflationRiskScore > 0.65) {
            estimate.Warnings.Add("残片通胀风险偏高，需要增加可重复 sink 或降低非精馏 faucet。");
        }
        if (proposed && estimate.TotalMemoryFaucet > 6.0) {
            estimate.Warnings.Add("Memory 阶段来源偏高，珍贵操作可能被快速刷穿。");
        }
    }
}
