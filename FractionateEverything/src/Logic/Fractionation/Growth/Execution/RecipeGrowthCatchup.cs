using FE.Logic.DarkFog;
using FE.Logic.Fractionation.FracRecipes;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Growth;

/// <summary>
/// 黑雾配方追赶层级和目标等级计算逻辑。
/// </summary>
public static class RecipeGrowthCatchup {
    /// <summary>
    /// 按输入物品推断黑雾配方所在阶层。
    /// </summary>
    public static int GetDarkFogTier(int inputId) {
        return inputId switch {
            var id when id == I黑雾矩阵 || id == I能量碎片 => 1,
            var id when id == I物质重组器 || id == I硅基神经元 => 2,
            I负熵奇点 => 3,
            I核心素 => 4,
            _ => 0,
        };
    }

    /// <summary>
    /// 将黑雾战斗阶段转换为成长计算使用的阶段序号。
    /// </summary>
    public static int GetDarkFogStageIndex(EDarkFogCombatStage stage) {
        return stage switch {
            EDarkFogCombatStage.Signal => 1,
            EDarkFogCombatStage.GroundSuppression => 2,
            EDarkFogCombatStage.StellarHunt => 3,
            EDarkFogCombatStage.Singularity => 4,
            _ => 0,
        };
    }

    /// <summary>
    /// 读取指定黑雾阶段的基础追赶值。
    /// </summary>
    public static int GetDarkFogCatchupBase(EDarkFogCombatStage stage) {
        return stage switch {
            EDarkFogCombatStage.Signal => 12,
            EDarkFogCombatStage.GroundSuppression => 16,
            EDarkFogCombatStage.StellarHunt => 22,
            EDarkFogCombatStage.Singularity => 30,
            _ => 0,
        };
    }

    /// <summary>
    /// 计算黑雾配方加工获得经验的阶段倍率。
    /// </summary>
    public static float GetDarkFogProcessMultiplier(int stageIndex, int recipeTier) {
        int lag = stageIndex - recipeTier;
        return lag switch {
            >= 2 => 2.4f,
            1 => 1.6f,
            _ => 1f,
        };
    }

    /// <summary>
    /// 计算黑雾配方追赶经验的阶段倍率。
    /// </summary>
    public static float GetDarkFogCatchupMultiplier(int stageIndex, int recipeTier) {
        int lag = stageIndex - recipeTier;
        return lag switch {
            >= 2 => 1.8f,
            1 => 1.3f,
            _ => 1f,
        };
    }

    /// <summary>
    /// 计算黑雾配方加工成长的修正经验。
    /// </summary>
    public static int GetAdjustedDarkFogProcessExp(BaseRecipe recipe, int growthExp, RecipeGrowthContext context) {
        int tier = GetDarkFogTier(recipe.InputID);
        int stageIndex = GetDarkFogStageIndex(context.DarkFogStage);
        float multiplier = GetDarkFogProcessMultiplier(stageIndex, tier);
        return Mathf.Max(1, Mathf.RoundToInt(growthExp * multiplier));
    }

    /// <summary>
    /// 计算黑雾配方追赶成长的修正经验。
    /// </summary>
    public static int GetAdjustedDarkFogCatchupExp(BaseRecipe recipe, int growthExp, RecipeGrowthContext context) {
        int tier = GetDarkFogTier(recipe.InputID);
        int stageIndex = GetDarkFogStageIndex(context.DarkFogStage);
        float multiplier = GetDarkFogCatchupMultiplier(stageIndex, tier);
        return Mathf.Max(1, Mathf.RoundToInt(growthExp * multiplier));
    }
}
