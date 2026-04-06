using System;
using System.Collections.Generic;

namespace VanillaCurveSim;

internal static class FeReference {
    private const int MaxIncLevel = 10;
    private static readonly double[] accTableMilli = [0.0, 0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0, 2.25, 2.5];

    private static readonly double[] phaseDrawShareConventional = [0.04, 0.06, 0.08, 0.10, 0.12, 0.14];
    private static readonly double[] phaseDrawShareSpeedrun = [0.08, 0.10, 0.13, 0.16, 0.18, 0.20];
    private static readonly double[] phaseOpeningShareConventional = [0.65, 0.62, 0.58, 0.54, 0.50, 0.48];
    private static readonly double[] phaseOpeningShareSpeedrun = [0.72, 0.68, 0.64, 0.58, 0.54, 0.50];

    // 这是第一版 treatment 预设。它们是可见的、可调的，不伪装成真实存档数据。
    private static readonly int[] conventionalTowerLevels = [2, 4, 6, 8, 10, 12];
    private static readonly int[] speedrunTowerLevels = [1, 3, 5, 7, 9, 10];
    private static readonly int[] conventionalRecipeLevels = [1, 3, 5, 7, 9, 10];
    private static readonly int[] speedrunRecipeLevels = [0, 2, 4, 6, 8, 9];
    private static readonly int[] conventionalSelectedInc = [1, 3, 5, 7, 9, 10];
    private static readonly int[] speedrunSelectedInc = [0, 2, 4, 5, 7, 8];

    private static readonly float[] conventionalSuccessBonus = [0.001f, 0.003f, 0.005f, 0.008f, 0.011f, 0.015f];
    private static readonly float[] speedrunSuccessBonus = [0.001f, 0.002f, 0.004f, 0.006f, 0.008f, 0.010f];
    private static readonly float[] conventionalDestroyBonus = [0.000f, 0.001f, 0.002f, 0.003f, 0.004f, 0.005f];
    private static readonly float[] speedrunDestroyBonus = [0.000f, 0.0005f, 0.001f, 0.002f, 0.0025f, 0.003f];
    private static readonly float[] conventionalDoubleBonus = [0.000f, 0.001f, 0.003f, 0.006f, 0.009f, 0.012f];
    private static readonly float[] speedrunDoubleBonus = [0.000f, 0.001f, 0.002f, 0.004f, 0.006f, 0.008f];
    private static readonly float[] conventionalEnergyBonus = [0.000f, 0.001f, 0.002f, 0.004f, 0.006f, 0.008f];
    private static readonly float[] speedrunEnergyBonus = [0.000f, 0.001f, 0.0015f, 0.003f, 0.004f, 0.005f];

    private static readonly double[] towerWeightsStage0 = [0.32, 0.30, 0.22, 0.10, 0.06];
    private static readonly double[] towerWeightsStage1 = [0.25, 0.28, 0.22, 0.13, 0.12];
    private static readonly double[] towerWeightsStage2 = [0.20, 0.25, 0.20, 0.18, 0.17];
    private static readonly double[] towerWeightsStage3 = [0.15, 0.22, 0.18, 0.23, 0.22];
    private static readonly double[] towerWeightsStage4 = [0.12, 0.20, 0.16, 0.24, 0.28];
    private static readonly double[] towerWeightsStage5 = [0.10, 0.18, 0.14, 0.25, 0.33];

    public static FractionationConfigSnapshot CreateScenarioConfig(SimulationMode mode, int stageIndex) {
        int safeStageIndex = ClampStageIndex(stageIndex);
        bool isSpeedrun = mode == SimulationMode.FeSpeedrun;
        return new FractionationConfigSnapshot {
            ScenarioName = mode == SimulationMode.FeSpeedrun ? "FE 速通" : "FE 常规",
            Mode = mode,
            IsSpeedrun = isSpeedrun,
            Focus = isSpeedrun ? FeGachaFocus.ProcessOptimization : FeGachaFocus.Balanced,
            StageIndex = safeStageIndex,
            RecipeLevel = SelectByStage(isSpeedrun ? speedrunRecipeLevels : conventionalRecipeLevels, safeStageIndex),
            SelectedIncLevel = SelectByStage(isSpeedrun ? speedrunSelectedInc : conventionalSelectedInc, safeStageIndex),
            InteractionTowerLevel = SelectByStage(isSpeedrun ? speedrunTowerLevels : conventionalTowerLevels, safeStageIndex),
            MineralReplicationTowerLevel = SelectByStage(isSpeedrun ? speedrunTowerLevels : conventionalTowerLevels, safeStageIndex),
            PointAggregateTowerLevel = SelectByStage(isSpeedrun ? speedrunTowerLevels : conventionalTowerLevels, safeStageIndex),
            ConversionTowerLevel = SelectByStage(isSpeedrun ? speedrunTowerLevels : conventionalTowerLevels, safeStageIndex),
            RectificationTowerLevel = SelectByStage(isSpeedrun ? speedrunTowerLevels : conventionalTowerLevels, safeStageIndex),
            AchievementSuccessBonus = SelectByStage(isSpeedrun ? speedrunSuccessBonus : conventionalSuccessBonus, safeStageIndex),
            AchievementDestroyReductionBonus = SelectByStage(isSpeedrun ? speedrunDestroyBonus : conventionalDestroyBonus, safeStageIndex),
            AchievementDoubleOutputBonus = SelectByStage(isSpeedrun ? speedrunDoubleBonus : conventionalDoubleBonus, safeStageIndex),
            AchievementEnergyReductionBonus = SelectByStage(isSpeedrun ? speedrunEnergyBonus : conventionalEnergyBonus, safeStageIndex),
        };
    }

    public static int GetStageIndex(ProgressPhase phase) {
        return phase switch {
            ProgressPhase.Bootstrap => 0,
            ProgressPhase.Electromagnetic => 0,
            ProgressPhase.Energy => 1,
            ProgressPhase.Structure => 2,
            ProgressPhase.Information => 3,
            ProgressPhase.Gravity => 4,
            ProgressPhase.Universe => 5,
            _ => 0,
        };
    }

    public static int GetDefaultMaxStackByLevel(int level) => level switch {
        < 6 => 1,
        < 9 => 4,
        < 12 => 8,
        _ => 12,
    };

    public static double GetDefaultEnergyRatioByLevel(int level) => level switch {
        < 1 => 1.0,
        < 4 => 0.95,
        < 7 => 0.85,
        < 10 => 0.7,
        _ => 0.5,
    };

    public static double GetDefaultPlrRatioByLevel(int level) => level switch {
        < 2 => 1.0,
        < 5 => 1.1,
        < 8 => 1.3,
        < 11 => 1.6,
        _ => 1.8,
    };

    public static double GetPointsBonus(int selectedIncLevel, int towerLevel) {
        int safeIncLevel = Math.Max(0, Math.Min(MaxIncLevel, selectedIncLevel));
        return accTableMilli[safeIncLevel] * GetDefaultPlrRatioByLevel(towerLevel);
    }

    public static double GetBaseDestroyRatio(int recipeLevel) => recipeLevel switch {
        < 7 => 0.04,
        < 8 => 0.03,
        < 9 => 0.02,
        < 10 => 0.01,
        _ => 0.0,
    };

    public static int GetPointAggregateMaxInc(int towerLevel) {
        return Math.Min(towerLevel + 4, MaxIncLevel);
    }

    public static bool HasFluidEnhancement(int towerLevel) => towerLevel >= 3;
    public static bool HasTrait1(int towerLevel) => towerLevel >= 6;
    public static bool HasTrait2(int towerLevel) => towerLevel >= 12;

    public static int GetRectificationBaseFragmentYield(int stageIndex) => stageIndex switch {
        0 => 2,
        1 => 4,
        2 => 8,
        3 => 10,
        4 => 16,
        5 => 32,
        _ => 1,
    };

    public static int GetRectificationFragmentYield(int stageIndex, int towerLevel) {
        double value = GetRectificationBaseFragmentYield(stageIndex) * GetDefaultPlrRatioByLevel(towerLevel);
        return Math.Max(1, (int)Math.Round(value, MidpointRounding.AwayFromZero));
    }

    public static int GetDrawMatrixCost(bool isOpeningLinePool, int drawCount) {
        if (drawCount <= 0) {
            return 0;
        }
        return drawCount;
    }

    public static double GetDrawShare(FractionationConfigSnapshot config) {
        return SelectByStage(config.IsSpeedrun ? phaseDrawShareSpeedrun : phaseDrawShareConventional, config.StageIndex);
    }

    public static double GetOpeningDrawShare(FractionationConfigSnapshot config) {
        return SelectByStage(config.IsSpeedrun ? phaseOpeningShareSpeedrun : phaseOpeningShareConventional, config.StageIndex);
    }

    public static double[] GetTowerWeights(int stageIndex) {
        return ClampStageIndex(stageIndex) switch {
            0 => towerWeightsStage0,
            1 => towerWeightsStage1,
            2 => towerWeightsStage2,
            3 => towerWeightsStage3,
            4 => towerWeightsStage4,
            _ => towerWeightsStage5,
        };
    }

    public static double GetFocusUtilityFactor(FeGachaFocus focus) {
        return focus == FeGachaFocus.Balanced ? 1.0 : 1.12;
    }

    public static double GetFocusedOfferDiscountFactor(bool isSpeedrun) {
        return isSpeedrun ? 0.85 : 0.80;
    }

    public static int ClampStageIndex(int stageIndex) {
        if (stageIndex < 0) {
            return 0;
        }
        return stageIndex > 5 ? 5 : stageIndex;
    }

    private static int SelectByStage(IReadOnlyList<int> values, int stageIndex) {
        return values[ClampStageIndex(stageIndex)];
    }

    private static float SelectByStage(IReadOnlyList<float> values, int stageIndex) {
        return values[ClampStageIndex(stageIndex)];
    }

    private static double SelectByStage(IReadOnlyList<double> values, int stageIndex) {
        return values[ClampStageIndex(stageIndex)];
    }
}
