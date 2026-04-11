using FE.Logic.Manager;
using FE.Logic.Recipe;
using static FE.Utils.Utils;

namespace FE.Logic.RecipeGrowth;

public static class RecipeGrowthCatchup {
    public static int GetDarkFogTier(int inputId) {
        return inputId switch {
            var id when id == I黑雾矩阵 || id == I能量碎片 => 1,
            var id when id == I物质重组器 || id == I硅基神经元 => 2,
            I负熵奇点 => 3,
            I核心素 => 4,
            _ => 0,
        };
    }

    public static int GetDarkFogStageIndex(EDarkFogCombatStage stage) {
        return stage switch {
            EDarkFogCombatStage.Signal => 1,
            EDarkFogCombatStage.GroundSuppression => 2,
            EDarkFogCombatStage.StellarHunt => 3,
            EDarkFogCombatStage.Singularity => 4,
            _ => 0,
        };
    }

    public static int GetDarkFogCatchupBase(EDarkFogCombatStage stage) {
        return stage switch {
            EDarkFogCombatStage.Signal => 12,
            EDarkFogCombatStage.GroundSuppression => 16,
            EDarkFogCombatStage.StellarHunt => 22,
            EDarkFogCombatStage.Singularity => 30,
            _ => 0,
        };
    }

    public static float GetDarkFogProcessMultiplier(int stageIndex, int recipeTier) {
        int lag = stageIndex - recipeTier;
        return lag switch {
            >= 2 => 2.4f,
            1 => 1.6f,
            _ => 1f,
        };
    }

    public static float GetDarkFogCatchupMultiplier(int stageIndex, int recipeTier) {
        int lag = stageIndex - recipeTier;
        return lag switch {
            >= 2 => 1.8f,
            1 => 1.3f,
            _ => 1f,
        };
    }

    public static int GetAdjustedDarkFogProcessExp(BaseRecipe recipe, int growthExp, RecipeGrowthContext context) {
        int tier = GetDarkFogTier(recipe.InputID);
        int stageIndex = GetDarkFogStageIndex(context.DarkFogStage);
        float multiplier = GetDarkFogProcessMultiplier(stageIndex, tier);
        return UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(growthExp * multiplier));
    }

    public static int GetAdjustedDarkFogCatchupExp(BaseRecipe recipe, int growthExp, RecipeGrowthContext context) {
        int tier = GetDarkFogTier(recipe.InputID);
        int stageIndex = GetDarkFogStageIndex(context.DarkFogStage);
        float multiplier = GetDarkFogCatchupMultiplier(stageIndex, tier);
        return UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(growthExp * multiplier));
    }
}
