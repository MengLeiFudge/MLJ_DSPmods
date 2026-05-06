using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.Logic.Buildings.Definitions;
using FE.Logic.Manager;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Fractionation.Growth;
using FE.UI.Components;
using FE.UI.MainPanel;
using FE.UI.MainPanel.Archive;
using FE.UI.MainPanel.DrawGrowth;
using UnityEngine;
using UnityEngine.UI;
using FE.Logic.Gacha;
using static FE.UI.Components.GridDsl;
using static FE.Logic.Gacha.GachaManager;
using static FE.Logic.Fractionation.Process.ProcessManager;
using static FE.Logic.Fractionation.Recipes.RecipeManager;
using static FE.Logic.Fractionation.Recipes.ERecipeExtension;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.ProgressTask;

public static partial class Achievements {
    private static RectTransform window;
    private static RectTransform tab;

    private readonly struct AchievementInfo(
        string categoryKey,
        string nameKey,
        string descKey,
        string rewardKey,
        ETier tier,
        Func<bool> condition,
        Action grantReward,
        float successRateBonus = 0f,
        float destroyReductionBonus = 0f,
        float doubleOutputBonus = 0f,
        float energyReductionBonus = 0f,
        float logisticsBonus = 0f,
        float powerStageBonus = 0f) {
        public readonly string CategoryKey = categoryKey;
        public readonly string NameKey = nameKey;
        public readonly string DescKey = descKey;
        public readonly string RewardKey = rewardKey;
        public readonly ETier Tier = tier;
        public readonly Func<bool> Condition = condition;
        public readonly Action GrantReward = grantReward;
        public readonly float SuccessRateBonus = successRateBonus;
        public readonly float DestroyReductionBonus = destroyReductionBonus;
        public readonly float DoubleOutputBonus = doubleOutputBonus;
        public readonly float EnergyReductionBonus = energyReductionBonus;
        public readonly float LogisticsBonus = logisticsBonus;
        public readonly float PowerStageBonus = powerStageBonus;
    }

    private readonly struct AchievementRewardDefinition(
        string rewardKey,
        bool unlockRecurringAutoClaim = false) {
        public readonly string RewardKey = rewardKey;
        public readonly bool UnlockRecurringAutoClaim = unlockRecurringAutoClaim;
    }

    private readonly struct AchievementBonusSummary(
        int obtainedCount,
        float successRateBonus,
        float destroyReductionBonus,
        float doubleOutputBonus,
        float energyReductionBonus,
        float logisticsBonus,
        float powerStageBonus) {
        public readonly int ObtainedCount = obtainedCount;
        public readonly float SuccessRateBonus = successRateBonus;
        public readonly float DestroyReductionBonus = destroyReductionBonus;
        public readonly float DoubleOutputBonus = doubleOutputBonus;
        public readonly float EnergyReductionBonus = energyReductionBonus;
        public readonly float LogisticsBonus = logisticsBonus;
        public readonly float PowerStageBonus = powerStageBonus;
    }

    private enum ETier {
        Bronze,
        Silver,
        Gold,
        Platinum,
    }

    // 成就系统是“全存档共享”的全局进度：
    // 1. Config 是真实持久化来源，切换存档时不会清空；
    // 2. 存档里的成就块只用于兼容旧档导入和把旧进度并入当前全局进度；
    // 3. 因此 Achievements 的已获得状态不应按单个存档隔离。
    private const string ConfigSection = "Achievements";
    private const string ConfigAchievementFlags = "AchievementFlags";
    private const string ConfigPanelOpenCount = "PanelOpenCount";

    private static ConfigEntry<string> achievementFlagsEntry;
    private static ConfigEntry<int> panelOpenCountEntry;
    private static bool configLoaded;
    private static int panelOpenCount;

    private static PageLayout.HeaderRefs header;
    private static Text txtTitle;
    private static Text txtUnlockedSummary;
    private static Text txtHiddenSummary;
    private static Text txtBonusSummary;

    private static Text[] txtAchievementNames;
    private static Text[] txtAchievementDescs;
    private static Text[] txtAchievementRewards;
    private static Text[] txtAchievementStates;
    private static MyImageButton[] rewardIcons;

    private const int RowsPerPage = 8;
    private static int currentPage;
    private static UIButton btnPrevPage;
    private static UIButton btnNextPage;
    private static Text txtPageIndicator;
    private static int nextAutoCheckFrame;

    private static readonly AchievementInfo[] achievements = BuildAchievements();
    private static readonly string[] legacyAchievementNameOrder = [
        "成就-千锤百炼",
        "成就-万物皆可分馏",
        "成就-分馏之王",
        "成就-永不停歇",
        "成就-开线先锋",
        "成就-开线专家",
        "成就-配方入门",
        "成就-配方学者",
        "成就-配方专家",
        "成就-万物百科",
        "成就-工艺优化",
        "成就-工艺大师",
        "成就-任务自动化",
        "成就-原胚循环",
        "成就-星际整备",
        "成就-精馏开路",
        "成就-万物归一",
    ];
    private static readonly Dictionary<string, int> achievementIndexByName = BuildAchievementIndexByName();
    private static readonly Dictionary<string, AchievementRewardDefinition> rewardDefinitionsByKey =
        BuildRewardDefinitionsByKey();
    private static bool[] unlocked = new bool[achievements.Length];
    private static bool[] claimed = new bool[achievements.Length];
    private static bool bonusSummaryDirty = true;
    private static AchievementBonusSummary cachedBonusSummary;

    private static void MarkBonusSummaryDirty() {
        bonusSummaryDirty = true;
        RecipeGrowthQueries.ClearProcessingCache();
    }

    private static void EnsureBonusSummaryCache() {
        if (!bonusSummaryDirty) {
            return;
        }

        int obtainedCount = 0;
        float successRateBonus = 0f;
        float destroyReductionBonus = 0f;
        float doubleOutputBonus = 0f;
        float energyReductionBonus = 0f;
        float logisticsBonus = 0f;
        float powerStageBonus = 0f;
        for (int i = 0; i < achievements.Length; i++) {
            if (!claimed[i]) {
                continue;
            }

            obtainedCount++;
            successRateBonus += achievements[i].SuccessRateBonus;
            destroyReductionBonus += achievements[i].DestroyReductionBonus;
            doubleOutputBonus += achievements[i].DoubleOutputBonus;
            energyReductionBonus += achievements[i].EnergyReductionBonus;
            logisticsBonus += achievements[i].LogisticsBonus;
            powerStageBonus += achievements[i].PowerStageBonus;
        }

        cachedBonusSummary = new AchievementBonusSummary(
            obtainedCount,
            successRateBonus,
            destroyReductionBonus,
            doubleOutputBonus,
            energyReductionBonus,
            logisticsBonus,
            powerStageBonus);
        bonusSummaryDirty = false;
    }
}
