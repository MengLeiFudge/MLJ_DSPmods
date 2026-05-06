using System;
using System.Linq;
using CommonAPI.Systems;
using FE.Compatibility;
using FE.Logic.Recipe;
using FE.Logic.RecipeGrowth;
using HarmonyLib;
using UnityEngine;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

/// <summary>
/// 添加科技后，需要Preload、Preload2。
/// Preload2会初始化unlockRecipeArray，之后LDBTool添加就不会报空指针异常。
/// </summary>
public static partial class TechManager {
    public static bool IsMatrixTierFullyResearched(int matrixId) {
        if (GameMain.history == null) {
            return false;
        }

        bool hasFiniteTech = false;
        foreach (TechProto tech in LDB.techs.dataArray) {
            if (tech == null || !tech.Published || tech.IsObsolete || tech.IsHiddenTech) {
                continue;
            }
            if (tech.MaxLevel > 20) {
                continue;
            }
            if (ItemManager.GetTechTopMatrixID(tech) != matrixId) {
                continue;
            }

            hasFiniteTech = true;
            if (!GameMain.history.TechUnlocked(tech.ID, true)) {
                return false;
            }
        }

        return hasFiniteTech;
    }

    public static float GetMatrixTierResearchProgress(int matrixId) {
        if (GameMain.history == null) {
            return 0f;
        }

        int total = 0;
        int unlocked = 0;
        foreach (TechProto tech in LDB.techs.dataArray) {
            if (tech == null || !tech.Published || tech.IsObsolete || tech.IsHiddenTech) {
                continue;
            }
            if (tech.MaxLevel > 20) {
                continue;
            }
            if (ItemManager.GetTechTopMatrixID(tech) != matrixId) {
                continue;
            }

            total++;
            if (GameMain.history.TechUnlocked(tech.ID, true)) {
                unlocked++;
            }
        }

        if (total <= 0) {
            return 0f;
        }
        return unlocked / (float)total;
    }

    /// <summary>
    /// 原版配方增强采用“落后一层”的阶段开放规则：
    /// 只有下一层矩阵已解锁，且该层有限科技全部研究完成时，才开放低一层增强。
    /// </summary>
    public static bool IsVanillaEnhancementUnlockedForMatrix(int matrixId) {
        int stageIndex = ItemManager.GetMatrixStageIndex(matrixId);
        if (stageIndex < 0 || stageIndex >= ItemManager.MainProgressMatrixIds.Length) {
            return false;
        }

        int requiredIndex = Mathf.Min(stageIndex + 1, ItemManager.MainProgressMatrixIds.Length - 1);
        int requiredMatrixId = ItemManager.MainProgressMatrixIds[requiredIndex];
        if (GameMain.history == null || !GameMain.history.ItemUnlocked(requiredMatrixId)) {
            return false;
        }
        return IsMatrixTierFullyResearched(requiredMatrixId);
    }

    public static int GetHighestUnlockedVanillaEnhancementMatrix() {
        for (int i = ItemManager.MainProgressMatrixIds.Length - 1; i >= 0; i--) {
            int matrixId = ItemManager.MainProgressMatrixIds[i];
            if (IsVanillaEnhancementUnlockedForMatrix(matrixId)) {
                return matrixId;
            }
        }

        return 0;
    }
}
