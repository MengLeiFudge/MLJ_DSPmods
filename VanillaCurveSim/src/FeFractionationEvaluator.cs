using System;
using System.Collections.Generic;

namespace VanillaCurveSim;

internal sealed class FeFractionationEvaluator {
    public static void RunSelfCheck() {
        SimulatorSelfCheck.Require(FeReference.GetDefaultMaxStackByLevel(0) == 1, "0 级堆叠应为 1。");
        SimulatorSelfCheck.Require(FeReference.GetDefaultMaxStackByLevel(12) == 12, "12 级堆叠应为 12。");

        var evaluator = new FeFractionationEvaluator();
        FractionationPhaseEstimate low = evaluator.EvaluatePhase(FeReference.CreateScenarioConfig(SimulationMode.FeConventional, 0));
        FractionationPhaseEstimate high = evaluator.EvaluatePhase(FeReference.CreateScenarioConfig(SimulationMode.FeConventional, 5));
        SimulatorSelfCheck.Require(high.EnergyEfficiencyMultiplier > low.EnergyEfficiencyMultiplier,
            "高阶段 FE 预设的能效应高于低阶段。");
    }

    public FractionationPhaseEstimate EvaluatePhase(FractionationConfigSnapshot config) {
        TowerEffectEstimate interaction = EvaluateInteractionTower(config);
        TowerEffectEstimate mineral = EvaluateStandardTower("矿物复制塔", config.MineralReplicationTowerLevel,
            config.RecipeLevel, baseSuccessRatio: 0.05, baseOutputCount: 2.0, config,
            trait1Bonus: 0.08, trait2Bonus: 0.12);
        TowerEffectEstimate pointAggregate = EvaluatePointAggregateTower(config);
        TowerEffectEstimate conversion = EvaluateStandardTower("转化塔", config.ConversionTowerLevel,
            config.RecipeLevel, baseSuccessRatio: 0.05, baseOutputCount: 1.0, config,
            trait1Bonus: 0.08, trait2Bonus: 0.06);
        TowerEffectEstimate rectification = EvaluateRectificationTower(config);

        TowerEffectEstimate[] towers = [interaction, mineral, pointAggregate, conversion, rectification];
        double[] weights = FeReference.GetTowerWeights(config.StageIndex);

        double weightedResourceGain = 1.0;
        double weightedEnergyEfficiency = 1.0;
        double timeAccelerationBonus = 0.0;
        for (int i = 0; i < towers.Length; i++) {
            weightedResourceGain += weights[i] * Math.Max(0.0, towers[i].ResourceGainMultiplier - 1.0);
            weightedEnergyEfficiency += weights[i] * Math.Max(0.0, towers[i].EnergyEfficiencyMultiplier - 1.0);
            timeAccelerationBonus += weights[i] * towers[i].TimeAccelerationContribution;
        }

        int fragmentCount = FeReference.GetRectificationFragmentYield(config.StageIndex, config.RectificationTowerLevel);
        double baselineFragmentCount = FeReference.GetRectificationBaseFragmentYield(config.StageIndex);
        return new FractionationPhaseEstimate {
            ResourceGainMultiplier = weightedResourceGain,
            EnergyEfficiencyMultiplier = weightedEnergyEfficiency,
            TimeAccelerationBonus = timeAccelerationBonus,
            FragmentYieldMultiplier = fragmentCount / Math.Max(1.0, baselineFragmentCount),
            EstimatedFragments = fragmentCount * (12.0 + config.StageIndex * 6.0),
            Towers = { interaction, mineral, pointAggregate, conversion, rectification },
        };
    }

    private static TowerEffectEstimate EvaluateInteractionTower(FractionationConfigSnapshot config) {
        double successBoost = config.AchievementSuccessBonus + (FeReference.HasTrait1(config.InteractionTowerLevel) ? 0.05 : 0.0);
        double resonanceBonus = FeReference.HasTrait2(config.InteractionTowerLevel) ? 0.10 : 0.0;
        double throughput = EvaluateGenericRecipeMultiplier(0.05, 1.0, config.InteractionTowerLevel, config.RecipeLevel,
            config, successBoost, resonanceBonus);
        double energyRatio = EffectiveEnergyRatio(config.InteractionTowerLevel, config.AchievementEnergyReductionBonus);
        return new TowerEffectEstimate {
            TowerName = "交互塔",
            TowerLevel = config.InteractionTowerLevel,
            RecipeLevel = config.RecipeLevel,
            ResourceGainMultiplier = throughput,
            EnergyEfficiencyMultiplier = throughput / energyRatio,
            TimeAccelerationContribution = (throughput / energyRatio - 1.0) * 0.16,
            FragmentYieldMultiplier = 1.0,
        };
    }

    private static TowerEffectEstimate EvaluateStandardTower(string towerName, int towerLevel, int recipeLevel,
        double baseSuccessRatio, double baseOutputCount, FractionationConfigSnapshot config, double trait1Bonus,
        double trait2Bonus) {
        double successBoost = config.AchievementSuccessBonus + (FeReference.HasTrait1(towerLevel) ? trait1Bonus : 0.0)
            + (FeReference.HasTrait2(towerLevel) ? trait2Bonus : 0.0);
        double throughput = EvaluateGenericRecipeMultiplier(baseSuccessRatio, baseOutputCount, towerLevel, recipeLevel,
            config, successBoost, 0.0);
        double energyRatio = EffectiveEnergyRatio(towerLevel, config.AchievementEnergyReductionBonus);
        return new TowerEffectEstimate {
            TowerName = towerName,
            TowerLevel = towerLevel,
            RecipeLevel = recipeLevel,
            ResourceGainMultiplier = throughput,
            EnergyEfficiencyMultiplier = throughput / energyRatio,
            TimeAccelerationContribution = (throughput / energyRatio - 1.0) * 0.18,
            FragmentYieldMultiplier = 1.0,
        };
    }

    private static TowerEffectEstimate EvaluatePointAggregateTower(FractionationConfigSnapshot config) {
        int maxInc = FeReference.GetPointAggregateMaxInc(config.PointAggregateTowerLevel);
        double successRatio = Math.Min(1.0, (config.SelectedIncLevel / 10.0) * 0.2 * (1.0 + config.AchievementSuccessBonus));
        double doublePointBonus = FeReference.HasTrait2(config.PointAggregateTowerLevel) ? 1.18 : 1.0;
        double resourceGain = 1.0 + successRatio * (maxInc / 10.0) * doublePointBonus;
        double energyRatio = EffectiveEnergyRatio(config.PointAggregateTowerLevel, config.AchievementEnergyReductionBonus);
        return new TowerEffectEstimate {
            TowerName = "点数聚集塔",
            TowerLevel = config.PointAggregateTowerLevel,
            RecipeLevel = config.RecipeLevel,
            ResourceGainMultiplier = resourceGain,
            EnergyEfficiencyMultiplier = resourceGain / energyRatio,
            TimeAccelerationContribution = (resourceGain / energyRatio - 1.0) * 0.12,
            FragmentYieldMultiplier = 1.0,
        };
    }

    private static TowerEffectEstimate EvaluateRectificationTower(FractionationConfigSnapshot config) {
        int fragmentCount = FeReference.GetRectificationFragmentYield(config.StageIndex, config.RectificationTowerLevel);
        double baselineFragmentCount = FeReference.GetRectificationBaseFragmentYield(config.StageIndex);
        double fragmentMultiplier = fragmentCount / Math.Max(1.0, baselineFragmentCount);
        double energyRatio = EffectiveEnergyRatio(config.RectificationTowerLevel, config.AchievementEnergyReductionBonus);
        return new TowerEffectEstimate {
            TowerName = "精馏塔",
            TowerLevel = config.RectificationTowerLevel,
            RecipeLevel = 0,
            ResourceGainMultiplier = fragmentMultiplier,
            EnergyEfficiencyMultiplier = fragmentMultiplier / energyRatio,
            TimeAccelerationContribution = (fragmentMultiplier / energyRatio - 1.0) * 0.14,
            FragmentYieldMultiplier = fragmentMultiplier,
        };
    }

    private static double EvaluateGenericRecipeMultiplier(double baseSuccessRatio, double baseOutputCount, int towerLevel,
        int recipeLevel, FractionationConfigSnapshot config, double successBoost, double extraMultiplier) {
        double pointsBonus = FeReference.GetPointsBonus(config.SelectedIncLevel, towerLevel);
        double successRatio = baseSuccessRatio * (1.0 + pointsBonus) * (1.0 + successBoost);
        if (successRatio > 1.0) {
            successRatio = 1.0;
        }

        double destroyRatio = Math.Max(0.0, FeReference.GetBaseDestroyRatio(recipeLevel) - config.AchievementDestroyReductionBonus);
        double fracRatio = (1.0 - destroyRatio) * successRatio;
        double remainInputRatio = recipeLevel * 0.08;
        double repeatRatio = fracRatio * remainInputRatio;
        double repeatMultiplier = repeatRatio >= 0.9999 ? 10000.0 : 1.0 / (1.0 - repeatRatio);
        double mainOutputBonus = 1.0 + recipeLevel * 0.05 + config.AchievementDoubleOutputBonus;
        double stackMultiplier = FeReference.GetDefaultMaxStackByLevel(towerLevel);

        double outputMultiplier = fracRatio * repeatMultiplier * mainOutputBonus * baseOutputCount;
        double throughput = outputMultiplier * stackMultiplier * (1.0 + extraMultiplier);
        return Math.Max(1.0, throughput);
    }

    private static double EffectiveEnergyRatio(int towerLevel, float achievementEnergyReductionBonus) {
        double ratio = FeReference.GetDefaultEnergyRatioByLevel(towerLevel) - achievementEnergyReductionBonus;
        return ratio < 0.2 ? 0.2 : ratio;
    }
}
