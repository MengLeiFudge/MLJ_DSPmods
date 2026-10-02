using FE.Logic.DataCenter;
using FE.Logic.Items;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

/// <summary>
/// 提供 FE 物流交互科技翻译与原版配方增强的矩阵阶段判定。
/// </summary>
public static class TechManager {
    /// <summary>
    /// 注册物流交互科技使用的文本。
    /// </summary>
    public static void AddTranslations() {
        Register("T行星内物流交互", "Planetary Subspace Interaction", "行星亚空间交互");
        Register("行星内物流交互描述",
            "Allows planetary logistics interaction stations to exchange items with the fractionation subspace.",
            "允许行星物流交互站与分馏亚空间交换物品。");
        Register("行星内物流交互结果",
            "Planetary logistics interaction stations can now access the fractionation subspace.",
            "行星物流交互站现在可以访问分馏亚空间。");

        Register("T星际物流交互", "Interstellar Subspace Interaction", "星际亚空间交互");
        Register("星际物流交互描述",
            "Allows interstellar logistics interaction stations to exchange items with the fractionation subspace.",
            "允许星际物流交互站与分馏亚空间交换物品。");
        Register("星际物流交互结果",
            "Interstellar logistics interaction stations can now access the fractionation subspace.",
            "星际物流交互站现在可以访问分馏亚空间。");
    }

    /// <summary>
    /// 判断指定矩阵层的有限科技是否全部完成。
    /// </summary>
    /// <param name="matrixId">作为该层顶级研究材料的矩阵物品 ID。</param>
    /// <returns>该层至少存在一项有限科技且均已完成时返回 true。</returns>
    public static bool IsMatrixTierFullyResearched(int matrixId) {
        if (GameMain.history == null) {
            return false;
        }

        bool hasFiniteTech = false;
        foreach (TechProto tech in LDB.techs.dataArray) {
            if (tech == null || !tech.Published || tech.IsObsolete || tech.IsHiddenTech || tech.MaxLevel > 20
                || GrowthTechnologyManager.TryGetTechnologyContentId(tech.ID, out _)
                || ItemManager.GetTechTopMatrixID(tech) != matrixId) {
                continue;
            }
            hasFiniteTech = true;
            if (!GameMain.history.TechUnlocked(tech.ID, true)) {
                return false;
            }
        }
        return hasFiniteTech;
    }

    /// <summary>
    /// 判断指定矩阵层对应的原版配方增强是否已经开放。
    /// </summary>
    /// <param name="matrixId">待判断配方所属的矩阵层。</param>
    /// <returns>下一层矩阵已解锁且该层有限科技全部完成时返回 true。</returns>
    public static bool IsVanillaEnhancementUnlockedForMatrix(int matrixId) {
        int stageIndex = ItemManager.GetMatrixStageIndex(matrixId);
        if (stageIndex < 0 || stageIndex >= ItemManager.MainProgressMatrixIds.Length) {
            return false;
        }

        int requiredIndex = Mathf.Min(stageIndex + 1, ItemManager.MainProgressMatrixIds.Length - 1);
        int requiredMatrixId = ItemManager.MainProgressMatrixIds[requiredIndex];
        return GameMain.history != null
               && GameMain.history.ItemUnlocked(requiredMatrixId)
               && IsMatrixTierFullyResearched(requiredMatrixId);
    }
}

/// <summary>
/// 管理首个遗物建立亚空间通信和一次性启动物资发放。
/// </summary>
public static class CivilizationRecoveryManager {
    private static bool starterKitRecovered;

    /// <summary>
    /// 获取当前存档是否已经通过首个遗物建立亚空间通信。
    /// </summary>
    public static bool HasDataCenterCommunication =>
        starterKitRecovered || ExplorationManager.FirstRelicClaimed;

    /// <summary>
    /// 完成首个遗物回收，直接建立亚空间通信并把启动物资写入亚空间。
    /// </summary>
    /// <returns>本次是否首次完成恢复。</returns>
    public static bool CompleteFirstRelicRecovery() {
        if (starterKitRecovered || GameMain.history == null) {
            return false;
        }

        starterKitRecovered = true;
        GameMain.history.UnlockRecipe(RFE基础矿物塔);
        GameMain.history.UnlockRecipe(RFE基础解析塔);
        ResearchManager.GrantStarterResearchState();
        DataCenterInventory.AddExplorationReward(IFE基础矿物塔, 4);
        DataCenterInventory.AddExplorationReward(IFE基础解析塔, 2);

        if (GrowthTechnologyManager.UsesExperiencedStackProgression) {
            GrantVeteranLogisticsSupply();
        }

        GrowthTechnologyManager.SyncRuntimeEffects();
        CivilizationModule.QueueNotification("首个遗物已恢复：基础塔与亚空间访问已就绪".Translate());
        return true;
    }

    /// <summary>
    /// 切换存档时清除仅用于当前会话的首遗物发奖防重标记。
    /// </summary>
    public static void IntoOtherSave() {
        starterKitRecovered = false;
    }

    /// <summary>
    /// 保留联机生命周期入口；探索认领已经不再使用旧请求节流。
    /// </summary>
    public static void ResetNetworkRequestThrottle() { }

    private static void GrantVeteranLogisticsSupply() {
        int[] logisticsTechs = [T基础物流系统, T改良物流系统, T高效物流系统, T集装物流系统];
        foreach (int techId in logisticsTechs) {
            if (!GameMain.history.TechUnlocked(techId, true)) {
                GameMain.history.UnlockTechUnlimited(techId, true);
            }
        }
        DataCenterInventory.AddExplorationReward(I极速传送带, 1000);
        DataCenterInventory.AddExplorationReward(I集装分拣器, 200);
    }
}
