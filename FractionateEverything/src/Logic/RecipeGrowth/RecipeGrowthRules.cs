using System;
using FE.Logic.Recipe;
using static FE.Utils.Utils;

namespace FE.Logic.RecipeGrowth;

public static class RecipeGrowthRules {
    private static readonly int[] BuildingTrainThresholds = [12, 20, 34, 56, 90];
    private static readonly int[] DarkFogThresholds = [12, 20, 34, 56, 90];
    private static readonly int[] RectificationThresholds = [14, 24, 42, 72, 120];

    private static readonly RecipeGrowthRule BuildingTrainForwardRule = new(
        RecipeFamily.BuildingTrainForward, RecipeGrowthMode.Hybrid, 5, 0, 2, 2, false, true, false);
    private static readonly RecipeGrowthRule BuildingTrainReverseRule = new(
        RecipeFamily.BuildingTrainReverse, RecipeGrowthMode.ProcessExp, 5, 0, 1, 1, false, true, false);
    private static readonly RecipeGrowthRule MineralCopyDarkFogRule = new(
        RecipeFamily.MineralCopyDarkFog, RecipeGrowthMode.ProcessExp, 5, 0, 0, 1, false, true, false);
    private static readonly RecipeGrowthRule ConversionMaterialDarkFogRule = new(
        RecipeFamily.ConversionMaterialDarkFog, RecipeGrowthMode.ProcessExp, 5, 0, 0, 1, false, true, false);
    private static readonly RecipeGrowthRule ConversionBuildingRule = new(
        RecipeFamily.ConversionBuilding, RecipeGrowthMode.FixedMax, 5, 0, 0, 5, true, false, false);
    private static readonly RecipeGrowthRule PointAggregateRule = new(
        RecipeFamily.PointAggregate, RecipeGrowthMode.FixedMax, 5, 5, 5, 5, true, false, false);
    private static readonly RecipeGrowthRule RectificationRule = new(
        RecipeFamily.Rectification, RecipeGrowthMode.ProcessExpWithPity, 5, 0, 1, 0, false, false, true);

    public static RecipeFamily GetFamily(BaseRecipe recipe) {
        return recipe switch {
            BuildingTrainRecipe => IsEmbryoInput(recipe.InputID)
                ? RecipeFamily.BuildingTrainForward
                : RecipeFamily.BuildingTrainReverse,
            MineralCopyRecipe => IsDarkFogItem(recipe.InputID)
                ? RecipeFamily.MineralCopyDarkFog
                : RecipeFamily.MineralCopyNormal,
            ConversionRecipe => IsBuildingItem(recipe.InputID) ? RecipeFamily.ConversionBuilding :
                IsDarkFogItem(recipe.InputID) ? RecipeFamily.ConversionMaterialDarkFog :
                RecipeFamily.ConversionMaterialNormal,
            PointAggregateRecipe => RecipeFamily.PointAggregate,
            RectificationRecipe => RecipeFamily.Rectification,
            _ => RecipeFamily.Unknown,
        };
    }

    public static RecipeGrowthRule GetRule(BaseRecipe recipe) {
        RecipeFamily family = GetFamily(recipe);
        return family switch {
            RecipeFamily.BuildingTrainForward => BuildingTrainForwardRule,
            RecipeFamily.BuildingTrainReverse => BuildingTrainReverseRule,
            RecipeFamily.MineralCopyNormal => new RecipeGrowthRule(
                family, RecipeGrowthMode.DrawDuplicate, 5, 0, GetStageBaselineLevel(recipe.MatrixID),
                GetDrawUnlockLevel(recipe.MatrixID), false, false, false),
            RecipeFamily.MineralCopyDarkFog => MineralCopyDarkFogRule,
            RecipeFamily.ConversionMaterialNormal => new RecipeGrowthRule(
                family, RecipeGrowthMode.DrawDuplicate, 5, 0, 0, GetDrawUnlockLevel(recipe.MatrixID),
                false, false, false),
            RecipeFamily.ConversionMaterialDarkFog => ConversionMaterialDarkFogRule,
            RecipeFamily.ConversionBuilding => ConversionBuildingRule,
            RecipeFamily.PointAggregate => PointAggregateRule,
            RecipeFamily.Rectification => RectificationRule,
            _ => new RecipeGrowthRule(RecipeFamily.Unknown, RecipeGrowthMode.None, 5, 0, 0, 1, false, false, false),
        };
    }

    public static int GetStageBaselineLevel(int matrixId) {
        return matrixId switch {
            I电磁矩阵 => 3,
            I能量矩阵 => 2,
            I结构矩阵 => 1,
            _ => 0,
        };
    }

    public static int GetDrawUnlockLevel(int matrixId) {
        int baseline = GetStageBaselineLevel(matrixId);
        return baseline > 0 ? baseline : 1;
    }

    public static int ClampLevel(RecipeGrowthRule rule, int level) {
        if (level < 0) {
            level = 0;
        }
        return level > rule.MaxLevel ? rule.MaxLevel : level;
    }

    public static int ConvertLegacyLevelToStored(BaseRecipe recipe, int legacyLevel) {
        RecipeGrowthRule rule = GetRule(recipe);
        if (rule.Family == RecipeFamily.PointAggregate) {
            return rule.MaxLevel;
        }
        if (legacyLevel < 0) {
            return 0;
        }

        // 旧版 Level=0 代表“已解锁的基础态”，新系统中必须至少映射到 Lv1。
        int storedLevel = Math.Max(1, (legacyLevel + 2) / 2);
        return ClampLevel(rule, storedLevel);
    }

    public static int GetEffectiveLegacyLevel(BaseRecipe recipe, int storedLevel) {
        RecipeGrowthRule rule = GetRule(recipe);
        if (rule.Family == RecipeFamily.PointAggregate) {
            return 10;
        }
        if (storedLevel <= 0) {
            return 0;
        }
        return storedLevel switch {
            1 => 2,
            2 => 4,
            3 => 6,
            4 => 8,
            _ => 10,
        };
    }

    public static int GetUpgradeThreshold(RecipeGrowthRule rule, int currentLevel) {
        if (currentLevel < 0 || currentLevel >= rule.MaxLevel) {
            return int.MaxValue;
        }
        return rule.Family switch {
            RecipeFamily.BuildingTrainForward or RecipeFamily.BuildingTrainReverse => BuildingTrainThresholds[
                currentLevel],
            RecipeFamily.MineralCopyDarkFog or RecipeFamily.ConversionMaterialDarkFog => DarkFogThresholds
                [currentLevel],
            RecipeFamily.Rectification => RectificationThresholds[currentLevel],
            _ => int.MaxValue,
        };
    }

    private static bool IsEmbryoInput(int inputId) {
        return inputId >= IFE交互塔原胚 && inputId <= IFE精馏塔原胚 || inputId == IFE分馏塔定向原胚;
    }

    private static bool IsBuildingItem(int inputId) {
        ItemProto item = LDB.items.Select(inputId);
        return item != null && (item.Type == EItemType.Production || item.IsEntity || item.CanBuild);
    }

    private static bool IsDarkFogItem(int inputId) {
        ItemProto item = LDB.items.Select(inputId);
        return inputId == I黑雾矩阵 || item != null && item.Type == EItemType.DarkFog;
    }
}
