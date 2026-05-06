using System;
using System.Linq;
using CommonAPI.Systems;
using FE.Compatibility;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Fractionation.Growth;
using HarmonyLib;
using UnityEngine;
using static FE.Logic.Fractionation.Recipes.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

/// <summary>
/// 添加科技后，需要Preload、Preload2。
/// Preload2会初始化unlockRecipeArray，之后LDBTool添加就不会报空指针异常。
/// </summary>
public static partial class TechManager {
    private static bool pendingLoadTimeRecipeBaselineApply;

    public static void RequestLoadTimeRecipeBaselineApply() {
        pendingLoadTimeRecipeBaselineApply = true;
    }

    public static bool TryApplyLoadTimeRecipeBaselines() {
        if (!pendingLoadTimeRecipeBaselineApply) {
            return true;
        }

        if (GameMain.history == null || !AreFracRecipesReady) {
            return false;
        }

        if (GameMain.history.TechUnlocked(TFE分馏塔原胚, true)) {
            EnsureBuildingTrainRecipeBaseline();
        }
        if (GameMain.history.TechUnlocked(TFE矿物复制, true)) {
            EnsureInitialMineralCopyRecipeBaseline();
        }
        if (GameMain.history.TechUnlocked(TFE物品精馏, true)) {
            EnsureRectificationRecipeBaseline();
        }
        EnsureGuaranteedConversionRecipeBaselines();
        pendingLoadTimeRecipeBaselineApply = false;
        return true;
    }

    private static void EnsureBuildingTrainRecipeBaseline() {
        foreach (BaseRecipe recipe in GetRecipesByType(ERecipe.BuildingTrain)) {
            EnsureRecipeInitialLevel(recipe);
        }
    }

    private static void EnsureInitialMineralCopyRecipeBaseline() {
        foreach (BaseRecipe recipe in GetRecipesByType(ERecipe.MineralCopy)) {
            int itemID = recipe.InputID;
            ItemProto item = LDB.items.Select(itemID);
            if (item == null) {
                continue;
            }
            if (LDB.veins.dataArray.Any(vein => vein.MiningItem == itemID) || item.Type == EItemType.Resource) {
                if (itemID < I可燃冰 || itemID > I单极磁石) {
                    EnsureRecipeInitialLevel(recipe);
                }
            }
        }
    }

    private static void EnsureRectificationRecipeBaseline() {
        foreach (BaseRecipe recipe in GetRecipesByType(ERecipe.Rectification)) {
            EnsureRecipeInitialLevel(recipe);
        }
    }

    /// <summary>
    /// 部分配方不在开线池里，或被归进黑雾阶段后没有自然获取入口。
    /// 当其对应科技已解锁时，补一个最低档位保底，避免旧档和高进度档里永久缺口。
    /// </summary>
    private static void EnsureGuaranteedConversionRecipeBaselines() {
        (int itemId, int techId)[] targets = [
            (I增产剂MkIII, T增产剂MkIII),
            (I战场分析基站, T战场分析基站),
            (I信号塔, T信号塔),
            (I干扰塔, T干扰塔),
            (I行星护盾发生器, T行星防御系统),
        ];

        foreach ((int itemId, int techId) in targets) {
            if (!GameMain.history.TechUnlocked(techId, true)) {
                continue;
            }

            BaseRecipe recipe = GetRecipe<BaseRecipe>(ERecipe.Conversion, itemId);
            if (recipe != null) {
                EnsureRecipeInitialLevel(recipe);
            }
        }
    }

    /// <summary>
    /// 初始保底只补低于基线的配方，不覆盖玩家后续自己抽到或培养出的更高等级。
    /// </summary>
    private static void EnsureRecipeInitialLevel(BaseRecipe recipe) {
        RecipeGrowthExecutor.EnsureUnlockedByTech(recipe, RecipeGrowthManager.BuildContext());
    }

    /// <summary>
    /// 当分馏塔上传至数据中心时，将解锁标记置为true。
    /// </summary>
}
