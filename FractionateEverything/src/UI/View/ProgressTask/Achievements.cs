using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.UI.Components;
using FE.UI.View;
using FE.UI.View.DrawGrowth;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.GachaManager;
using static FE.Logic.Manager.ProcessManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Logic.Recipe.ERecipeExtension;
using static FE.Utils.Utils;

namespace FE.UI.View.ProgressTask;

public static class Achievements {
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

    private enum ETier {
        Bronze,
        Silver,
        Gold,
        Platinum,
    }

    private const string ConfigSection = "Achievements";
    private const string ConfigAchievementFlags = "AchievementFlags";
    private const string ConfigPanelOpenCount = "PanelOpenCount";

    private static ConfigEntry<string> achievementFlagsEntry;
    private static ConfigEntry<int> panelOpenCountEntry;
    private static bool configLoaded;
    private static int panelOpenCount;

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
    private const float AchievementRowSpacing = 52f;
    private static int currentPage;
    private static UIButton btnPrevPage;
    private static UIButton btnNextPage;
    private static Text txtPageIndicator;
    private static float listStartY;
    private static float listNameX;
    private static float listNameW;
    private static float listDescX;
    private static float listDescW;
    private static float listRewardX;
    private static float listRewardTextX;
    private static float listRewardTextW;
    private static float listStateX;
    private static float listStateW;
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
    private static bool[] unlocked = new bool[achievements.Length];
    private static bool[] claimed = new bool[achievements.Length];

    private static Dictionary<string, int> BuildAchievementIndexByName() {
        var map = new Dictionary<string, int>(achievements.Length);
        for (int i = 0; i < achievements.Length; i++) {
            map[achievements[i].NameKey] = i;
        }
        return map;
    }

    private static AchievementInfo[] BuildAchievements() {
        var list = new List<AchievementInfo>(72);
        AddProductionAchievements(list);
        AddOpeningAchievements(list);
        AddRecipeAchievements(list);
        AddGrowthAchievements(list);
        AddRecurringAchievements(list);
        AddProtoAchievements(list);
        AddDarkFogAchievements(list);
        AddExplorationAchievements(list);
        AddChallengeAchievements(list);
        return [.. list];
    }

    private static void AddProductionAchievements(List<AchievementInfo> list) {
        var defs = new (string Name, int Target, string RewardKey, ETier Tier, float SuccessBonus, float DestroyBonus, float DoubleBonus)[] {
            ("分馏初学者", 10, "成就奖励-残片200", ETier.Bronze, 0.001f, 0f, 0f),
            ("分馏熟练工", 25, "成就奖励-残片200", ETier.Bronze, 0.001f, 0f, 0f),
            ("分馏执勤员", 50, "成就奖励-残片200", ETier.Bronze, 0.0015f, 0f, 0f),
            ("分馏推进者", 100, "成就奖励-残片300", ETier.Bronze, 0.002f, 0f, 0f),
            ("分馏节拍", 300, "成就奖励-残片300", ETier.Silver, 0.003f, 0f, 0f),
            ("分馏热潮", 600, "成就奖励-残片500", ETier.Silver, 0.004f, 0.001f, 0f),
            ("成就-千锤百炼", 1000, "成就奖励-残片500", ETier.Silver, 0.005f, 0.001f, 0f),
            ("分馏扩张", 3000, "成就奖励-残片800", ETier.Gold, 0.007f, 0.002f, 0.002f),
            ("成就-万物皆可分馏", 10000, "成就奖励-残片1000", ETier.Gold, 0.01f, 0.003f, 0.003f),
            ("分馏高峰", 50000, "成就奖励-残片2000", ETier.Gold, 0.015f, 0.004f, 0.005f),
            ("成就-分馏之王", 200000, "成就奖励-残片2000", ETier.Platinum, 0.02f, 0.006f, 0.008f),
            ("成就-永不停歇", 1000000, "成就奖励-残片2000", ETier.Platinum, 0.03f, 0.01f, 0.02f),
        };

        foreach ((string name, int target, string rewardKey, ETier tier, float successBonus, float destroyBonus, float doubleBonus) in defs) {
            string desc = $"累计完成 {target} 次分馏成功";
            list.Add(new AchievementInfo(
                "成就分类-生产",
                name,
                desc,
                rewardKey,
                tier,
                () => totalFractionSuccesses >= target,
                () => GrantRewardByKey(rewardKey),
                successRateBonus: successBonus,
                destroyReductionBonus: destroyBonus,
                doubleOutputBonus: doubleBonus));
        }
    }

    private static void AddOpeningAchievements(List<AchievementInfo> list) {
        var defs = new (string Name, int Target, string RewardKey, ETier Tier, float DoubleBonus, float LogisticsBonus)[] {
            ("开线初试", 1, "成就奖励-当前阶段矩阵2", ETier.Bronze, 0f, 0f),
            ("开线连发", 5, "成就奖励-当前阶段矩阵2", ETier.Bronze, 0f, 0f),
            ("开线热手", 10, "成就奖励-当前阶段矩阵2", ETier.Bronze, 0.001f, 0f),
            ("开线之门", 20, "成就奖励-当前阶段矩阵4", ETier.Silver, 0.001f, 0f),
            ("开线推进", 50, "成就奖励-当前阶段矩阵4", ETier.Silver, 0.002f, 0f),
            ("成就-开线先锋", 100, "成就奖励-当前阶段矩阵4", ETier.Silver, 0.002f, 0f),
            ("开线大师", 200, "成就奖励-当前阶段矩阵8", ETier.Gold, 0.003f, 0.003f),
            ("成就-开线专家", 500, "成就奖励-当前阶段矩阵8", ETier.Gold, 0.005f, 0.004f),
            ("开线统筹", 1000, "成就奖励-当前阶段矩阵16", ETier.Platinum, 0.008f, 0.006f),
            ("开线传说", 2000, "成就奖励-当前阶段矩阵16", ETier.Platinum, 0.01f, 0.01f),
        };

        foreach ((string name, int target, string rewardKey, ETier tier, float doubleBonus, float logisticsBonus) in defs) {
            string desc = $"累计完成 {target} 次开线抽取";
            list.Add(new AchievementInfo(
                "成就分类-开线",
                name,
                desc,
                rewardKey,
                tier,
                () => TicketRaffle.openingLineDraws >= target,
                () => GrantRewardByKey(rewardKey),
                doubleOutputBonus: doubleBonus,
                logisticsBonus: logisticsBonus));
        }
    }

    private static void AddRecipeAchievements(List<AchievementInfo> list) {
        // 配方线的关键锚点统一落在 60 / 100 / 150，分别对应速通闭环、常规闭环与后期百科完成度。
        var defs = new (string Name, int Target, string RewardKey, ETier Tier, float SuccessBonus, float DestroyBonus, float PowerBonus)[] {
            ("配方启蒙", 1, "成就奖励-配方核心1", ETier.Bronze, 0.001f, 0f, 0f),
            ("配方初识", 3, "成就奖励-配方核心1", ETier.Bronze, 0.001f, 0f, 0f),
            ("成就-配方入门", 5, "成就奖励-配方核心1", ETier.Bronze, 0.002f, 0f, 0f),
            ("配方进修", 10, "成就奖励-配方核心1", ETier.Silver, 0.002f, 0.001f, 0f),
            ("配方拓展", 20, "成就奖励-配方核心3", ETier.Silver, 0.003f, 0.002f, 0f),
            ("成就-配方学者", 30, "成就奖励-配方核心3", ETier.Silver, 0.003f, 0.003f, 0f),
            ("配方总览", 60, "成就奖励-当前阶段矩阵4", ETier.Gold, 0.004f, 0.004f, 0.005f),
            ("成就-配方专家", 100, "成就奖励-当前阶段矩阵8", ETier.Gold, 0.006f, 0.006f, 0.01f),
            ("配方馆长", 120, "成就奖励-残片800", ETier.Platinum, 0.01f, 0.008f, 0.015f),
            ("成就-万物百科", 150, "成就奖励-残片1000", ETier.Platinum, 0.015f, 0.01f, 0.02f),
        };

        foreach ((string name, int target, string rewardKey, ETier tier, float successBonus, float destroyBonus, float powerBonus) in defs) {
            string desc = $"累计解锁 {target} 个分馏配方";
            list.Add(new AchievementInfo(
                "成就分类-配方",
                name,
                desc,
                rewardKey,
                tier,
                () => GetUnlockedRecipeCount() >= target,
                () => GrantRewardByKey(rewardKey),
                successRateBonus: successBonus,
                destroyReductionBonus: destroyBonus,
                powerStageBonus: powerBonus));
        }
    }

    private static void AddGrowthAchievements(List<AchievementInfo> list) {
        var defs = new (string Name, int Target, string RewardKey, ETier Tier, float EnergyBonus, float PowerBonus)[] {
            ("工艺起步", 1, "成就奖励-残片200", ETier.Bronze, 0.01f, 0f),
            ("工艺进阶", 2, "成就奖励-残片300", ETier.Bronze, 0.01f, 0f),
            ("工艺磨合", 3, "成就奖励-残片500", ETier.Silver, 0.015f, 0f),
            ("工艺稳态", 4, "成就奖励-残片500", ETier.Silver, 0.02f, 0f),
            ("成就-工艺优化", 6, "成就奖励-残片800", ETier.Gold, 0.03f, 0.005f),
            ("工艺跃迁", 8, "成就奖励-残片1000", ETier.Gold, 0.04f, 0.01f),
            ("工艺巅峰", 10, "成就奖励-残片1000", ETier.Gold, 0.05f, 0.015f),
            ("成就-工艺大师", 12, "成就奖励-残片2000", ETier.Platinum, 0.08f, 0.02f),
        };

        foreach ((string name, int target, string rewardKey, ETier tier, float energyBonus, float powerBonus) in defs) {
            string desc = $"任意万物分馏建筑等级达到 {target}";
            list.Add(new AchievementInfo(
                "成就分类-成长",
                name,
                desc,
                rewardKey,
                tier,
                () => GetMaxBuildingLevel() >= target,
                () => GrantRewardByKey(rewardKey),
                energyReductionBonus: energyBonus,
                powerStageBonus: powerBonus));
        }
    }

    private static void AddRecurringAchievements(List<AchievementInfo> list) {
        var defs = new (string Name, int Target, string RewardKey, ETier Tier, float LogisticsBonus, float DoubleBonus)[] {
            ("任务热身", 1, "成就奖励-残片200", ETier.Bronze, 0.001f, 0f),
            ("任务连锁", 5, "成就奖励-残片300", ETier.Bronze, 0.002f, 0f),
            ("任务推进", 10, "成就奖励-当前阶段矩阵2", ETier.Silver, 0.003f, 0f),
            ("任务巡航", 20, "成就奖励-配方核心1", ETier.Silver, 0.004f, 0.002f),
            ("任务统筹", 50, "成就奖励-当前阶段矩阵4", ETier.Gold, 0.006f, 0.003f),
            ("成就-任务自动化", 100, "成就奖励-循环任务自动领取", ETier.Gold, 0.008f, 0.004f),
            ("任务驱动", 200, "成就奖励-当前阶段矩阵8", ETier.Platinum, 0.012f, 0.006f),
            ("任务永动", 500, "成就奖励-当前阶段矩阵16", ETier.Platinum, 0.016f, 0.01f),
        };

        foreach ((string name, int target, string rewardKey, ETier tier, float logisticsBonus, float doubleBonus) in defs) {
            string desc = $"累计完成 {target} 次循环任务";
            Action rewardAction = rewardKey == "成就奖励-循环任务自动领取"
                ? RecurringTask.UnlockAutoClaim
                : () => GrantRewardByKey(rewardKey);

            list.Add(new AchievementInfo(
                "成就分类-循环",
                name,
                desc,
                rewardKey,
                tier,
                () => RecurringTask.TotalClaimedCount >= target,
                rewardAction,
                logisticsBonus: logisticsBonus,
                doubleOutputBonus: doubleBonus));
        }
    }

    private static void AddProtoAchievements(List<AchievementInfo> list) {
        var defs = new (string Name, int Target, string RewardKey, ETier Tier, float LogisticsBonus, float PowerBonus)[] {
            ("原胚起点", 1, "成就奖励-定向原胚1", ETier.Bronze, 0.002f, 0f),
            ("原胚整备", 5, "成就奖励-残片300", ETier.Bronze, 0.003f, 0f),
            ("成就-原胚循环", 10, "成就奖励-配方核心1", ETier.Silver, 0.004f, 0f),
            ("原胚调度", 20, "成就奖励-定向原胚1", ETier.Silver, 0.006f, 0.005f),
            ("原胚仓储", 40, "成就奖励-残片800", ETier.Gold, 0.01f, 0.01f),
            ("原胚洪流", 80, "成就奖励-精馏塔原胚3", ETier.Platinum, 0.015f, 0.02f),
        };

        foreach ((string name, int target, string rewardKey, ETier tier, float logisticsBonus, float powerBonus) in defs) {
            string desc = $"仓储中持有 {target} 个分馏塔原胚";
            list.Add(new AchievementInfo(
                "成就分类-原胚",
                name,
                desc,
                rewardKey,
                tier,
                () => GetProtoInventoryCount() >= target,
                () => GrantRewardByKey(rewardKey),
                logisticsBonus: logisticsBonus,
                powerStageBonus: powerBonus));
        }
    }

    private static void AddDarkFogAchievements(List<AchievementInfo> list) {
        list.Add(new AchievementInfo(
            "成就分类-黑雾",
            "黑雾信标",
            "进入战斗模式并接通黑雾支线",
            "成就奖励-当前阶段矩阵4",
            ETier.Bronze,
            () => DarkFogCombatManager.GetCurrentStage() >= EDarkFogCombatStage.Signal,
            () => GrantRewardByKey("成就奖励-当前阶段矩阵4"),
            successRateBonus: 0.002f,
            logisticsBonus: 0.002f));
        list.Add(new AchievementInfo(
            "成就分类-黑雾",
            "黑雾压制",
            "将黑雾战斗支线推进到地面压制阶段",
            "成就奖励-残片800",
            ETier.Silver,
            () => DarkFogCombatManager.GetCurrentStage() >= EDarkFogCombatStage.GroundSuppression,
            () => GrantRewardByKey("成就奖励-残片800"),
            successRateBonus: 0.003f,
            destroyReductionBonus: 0.002f));
        list.Add(new AchievementInfo(
            "成就分类-黑雾",
            "蜂巢猎场",
            "将黑雾战斗支线推进到星域围猎阶段",
            "成就奖励-当前阶段矩阵8",
            ETier.Gold,
            () => DarkFogCombatManager.GetCurrentStage() >= EDarkFogCombatStage.StellarHunt,
            () => GrantRewardByKey("成就奖励-当前阶段矩阵8"),
            successRateBonus: 0.004f,
            doubleOutputBonus: 0.003f,
            powerStageBonus: 0.008f));
        list.Add(new AchievementInfo(
            "成就分类-黑雾",
            "奇点收束",
            "将黑雾战斗支线推进到奇点收束阶段",
            "成就奖励-残片2000",
            ETier.Platinum,
            () => DarkFogCombatManager.GetCurrentStage() >= EDarkFogCombatStage.Singularity,
            () => GrantRewardByKey("成就奖励-残片2000"),
            successRateBonus: 0.006f,
            destroyReductionBonus: 0.004f,
            powerStageBonus: 0.012f));
        list.Add(new AchievementInfo(
            "成就分类-黑雾",
            "遗物共振",
            "在黑雾增强层中持有至少 1 个元驱动",
            "成就奖励-配方核心1",
            ETier.Gold,
            () => DarkFogCombatManager.IsEnhancedLayerEnabled() && DarkFogCombatManager.GetRelicCount() >= 1,
            () => GrantRewardByKey("成就奖励-配方核心1"),
            doubleOutputBonus: 0.003f,
            logisticsBonus: 0.003f));
        list.Add(new AchievementInfo(
            "成就分类-黑雾",
            "功勋回路",
            "在黑雾增强层中将功勋等级提升到 4",
            "成就奖励-定向原胚1",
            ETier.Platinum,
            () => DarkFogCombatManager.IsEnhancedLayerEnabled() && DarkFogCombatManager.GetMeritRank() >= 4,
            () => GrantRewardByKey("成就奖励-定向原胚1"),
            logisticsBonus: 0.004f,
            powerStageBonus: 0.015f));
        list.Add(new AchievementInfo(
            "成就分类-黑雾",
            "授权整备",
            "在黑雾增强层中分配至少 8 点技能点",
            "成就奖励-当前阶段矩阵16",
            ETier.Platinum,
            () => DarkFogCombatManager.IsEnhancedLayerEnabled() && DarkFogCombatManager.GetAssignedSkillPointCount() >= 8,
            () => GrantRewardByKey("成就奖励-当前阶段矩阵16"),
            energyReductionBonus: 0.03f,
            powerStageBonus: 0.01f));
    }

    private static void AddExplorationAchievements(List<AchievementInfo> list) {
        list.Add(new AchievementInfo(
            "成就分类-探索",
            "第一次打开面板",
            "第一次打开分馏数据中心面板",
            "成就奖励-残片200",
            ETier.Bronze,
            () => panelOpenCount >= 1,
            () => GrantRewardByKey("成就奖励-残片200"),
            successRateBonus: 0.002f));

        list.Add(new AchievementInfo(
            "成就分类-探索",
            "常规模式试跑",
            "在常规模式下打开面板",
            "成就奖励-当前阶段矩阵2",
            ETier.Bronze,
            () => !IsSpeedrunMode && panelOpenCount >= 1,
            () => GrantRewardByKey("成就奖励-当前阶段矩阵2"),
            powerStageBonus: 0.005f));

        list.Add(new AchievementInfo(
            "成就分类-探索",
            "速通模式试跑",
            "在速通模式下打开面板",
            "成就奖励-当前阶段矩阵2",
            ETier.Bronze,
            () => IsSpeedrunMode && panelOpenCount >= 1,
            () => GrantRewardByKey("成就奖励-当前阶段矩阵2"),
            powerStageBonus: 0.005f));

        list.Add(new AchievementInfo(
            "成就分类-探索",
            "分馏启示",
            "解锁分馏数据中心科技",
            "成就奖励-残片300",
            ETier.Silver,
            () => IsTechUnlocked(TFE分馏数据中心),
            () => GrantRewardByKey("成就奖励-残片300"),
            successRateBonus: 0.003f));

        list.Add(new AchievementInfo(
            "成就分类-探索",
            "矿物新生",
            "解锁矿物复制科技",
            "成就奖励-配方核心1",
            ETier.Silver,
            () => IsTechUnlocked(TFE矿物复制),
            () => GrantRewardByKey("成就奖励-配方核心1"),
            destroyReductionBonus: 0.003f));

        list.Add(new AchievementInfo(
            "成就分类-探索",
            "物品转化",
            "解锁物品转化科技",
            "成就奖励-残片500",
            ETier.Gold,
            () => IsTechUnlocked(TFE物品转化),
            () => GrantRewardByKey("成就奖励-残片500"),
            doubleOutputBonus: 0.005f));

        list.Add(new AchievementInfo(
            "成就分类-探索",
            "成就-精馏开路",
            "解锁物品精馏科技",
            "成就奖励-精馏塔原胚3",
            ETier.Gold,
            () => IsTechUnlocked(TFE物品精馏),
            () => GrantRewardByKey("成就奖励-精馏塔原胚3"),
            powerStageBonus: 0.01f));

        list.Add(new AchievementInfo(
            "成就分类-探索",
            "成就-星际整备",
            "解锁星际物流交互科技",
            "成就奖励-星际物流交互站1",
            ETier.Platinum,
            () => IsTechUnlocked(TFE星际物流交互),
            () => GrantRewardByKey("成就奖励-星际物流交互站1"),
            logisticsBonus: 0.02f));
    }

    private static void AddChallengeAchievements(List<AchievementInfo> list) {
        list.Add(new AchievementInfo(
            "成就分类-挑战",
            "基础闭环",
            "累计 5000 次分馏成功、解锁 60 个配方并将任意建筑升到 6 级",
            "成就奖励-当前阶段矩阵8",
            ETier.Gold,
            () => totalFractionSuccesses >= 5000 && GetUnlockedRecipeCount() >= 60 && GetMaxBuildingLevel() >= 6,
            () => GrantRewardByKey("成就奖励-当前阶段矩阵8"),
            successRateBonus: 0.01f,
            doubleOutputBonus: 0.005f,
            powerStageBonus: 0.01f));

        list.Add(new AchievementInfo(
            "成就分类-挑战",
            "全域工艺",
            "累计 20000 次分馏成功、解锁 100 个配方并将任意建筑升到 8 级",
            "成就奖励-当前阶段矩阵16",
            ETier.Gold,
            () => totalFractionSuccesses >= 20000 && GetUnlockedRecipeCount() >= 100 && GetMaxBuildingLevel() >= 8,
            () => GrantRewardByKey("成就奖励-当前阶段矩阵16"),
            successRateBonus: 0.015f,
            destroyReductionBonus: 0.005f,
            doubleOutputBonus: 0.01f,
            powerStageBonus: 0.015f));

        list.Add(new AchievementInfo(
            "成就分类-挑战",
            "成就-万物归一",
            "累计 50000 次分馏成功、解锁 100 个配方并将任意建筑升到 10 级",
            "成就奖励-当前阶段矩阵16",
            ETier.Platinum,
            () => totalFractionSuccesses >= 50000 && GetUnlockedRecipeCount() >= 100 && GetMaxBuildingLevel() >= 10,
            () => GrantRewardByKey("成就奖励-当前阶段矩阵16"),
            successRateBonus: 0.02f,
            destroyReductionBonus: 0.01f,
            doubleOutputBonus: 0.015f,
            energyReductionBonus: 0.02f,
            logisticsBonus: 0.02f,
            powerStageBonus: 0.02f));

        list.Add(new AchievementInfo(
            "成就分类-挑战",
            "常规毕业",
            "常规模式下累计 30000 次分馏成功，并解锁 150 个配方与星际物流交互科技",
            "成就奖励-残片2000",
            ETier.Platinum,
            () => !IsSpeedrunMode
                  && totalFractionSuccesses >= 30000
                  && GetUnlockedRecipeCount() >= 150
                  && IsTechUnlocked(TFE星际物流交互),
            () => GrantRewardByKey("成就奖励-残片2000"),
            successRateBonus: 0.02f,
            powerStageBonus: 0.02f));

        list.Add(new AchievementInfo(
            "成就分类-挑战",
            "速通毕业",
            "速通模式下累计 10000 次分馏成功、500 次开线抽取并解锁星际物流交互科技",
            "成就奖励-残片2000",
            ETier.Platinum,
            () => IsSpeedrunMode
                  && totalFractionSuccesses >= 10000
                  && TicketRaffle.openingLineDraws >= 500
                  && IsTechUnlocked(TFE星际物流交互),
            () => GrantRewardByKey("成就奖励-残片2000"),
            successRateBonus: 0.02f,
            doubleOutputBonus: 0.02f,
            logisticsBonus: 0.02f));
    }

    private static void SyncCurrentPageFromSharedState() {
        currentPage = Math.Max(0, MainWindow.SharedPanelState?.AchievementsCurrentPage ?? 0);
    }

    private static void SyncCurrentPageToSharedState() {
        if (MainWindow.SharedPanelState != null) {
            MainWindow.SharedPanelState.AchievementsCurrentPage = currentPage;
        }
    }

    public static void AddTranslations() {
        Register("成就详情", "Achievements");
        Register("成就系统", "Achievement System");
        Register("成就", "Achievement");
        Register("成就分类-生产", "Production", "生产");
        Register("成就分类-开线", "Opening", "开线");
        Register("成就分类-配方", "Recipe", "配方");
        Register("成就分类-成长", "Growth", "成长");
        Register("成就分类-循环", "Recurring", "循环");
        Register("成就分类-原胚", "Proto", "原胚");
        Register("成就分类-黑雾", "Dark Fog", "黑雾");
        Register("成就分类-探索", "Explore", "探索");
        Register("成就分类-挑战", "Challenge", "挑战");
        Register("描述", "Description");
        Register("状态", "Status");
        Register("奖励", "Reward");

        Register("已获得成就", "Obtained: {0}/{1}", "已获得：{0}/{1}");
        Register("隐藏未解锁", "Locked: {0}", "未解锁：{0}");
        Register("成就加成格式", "Success +{0}% / Destroy -{1}% / Double +{2}% / Energy -{3}% / Logistics +{4}% / Power +{5}%", "成功+{0}% / 损毁-{1}% / 翻倍+{2}% / 能耗-{3}% / 物流+{4}% / 发电+{5}%");

        Register("已获得", "Obtained", "已获得");
        Register("未解锁", "Locked");
        Register("上一页", "Prev page");
        Register("下一页", "Next page");
        Register("隐藏成就提示", "???", "???");
        Register("隐藏成就描述", "Hidden achievement", "未解锁");
        Register("成就获得提示", "Achievement unlocked: {0}", "获得成就：{0}");

        Register("成就品阶-青铜", "Bronze", "青铜");
        Register("成就品阶-白银", "Silver", "白银");
        Register("成就品阶-黄金", "Gold", "黄金");
        Register("成就品阶-白金", "Platinum", "白金");

        Register("成就奖励-残片200", "Fragments x200", "残片 x200");
        Register("成就奖励-残片300", "Fragments x300", "残片 x300");
        Register("成就奖励-残片500", "Fragments x500", "残片 x500");
        Register("成就奖励-残片800", "Fragments x800", "残片 x800");
        Register("成就奖励-残片1000", "Fragments x1000", "残片 x1000");
        Register("成就奖励-残片2000", "Fragments x2000", "残片 x2000");
        Register("成就奖励-当前阶段矩阵2", "Current stage matrix x2", "当前阶段矩阵 x2");
        Register("成就奖励-当前阶段矩阵4", "Current stage matrix x4", "当前阶段矩阵 x4");
        Register("成就奖励-当前阶段矩阵8", "Current stage matrix x8", "当前阶段矩阵 x8");
        Register("成就奖励-当前阶段矩阵16", "Current stage matrix x16", "当前阶段矩阵 x16");
        Register("成就奖励-配方核心1", "Fractionation Recipe Core x1", "分馏配方核心 x1");
        Register("成就奖励-配方核心3", "Fractionation Recipe Core x3", "分馏配方核心 x3");
        Register("成就奖励-定向原胚1", "Directional Proto x1", "定向原胚 x1");
        Register("成就奖励-星际物流交互站1", "Interstellar Interaction Station x1", "星际物流交互站 x1");
        Register("成就奖励-精馏塔原胚3", "Rectification Tower Proto x3", "精馏塔原胚 x3");
        Register("成就奖励-循环任务自动领取", "Recurring task auto-claim", "循环任务自动领取");

        Register("成就-任务自动化", "Task Automation");
        Register("成就-千锤百炼", "Tempered Through Trials");
        Register("成就-万物皆可分馏", "Fractionate Everything");
        Register("成就-分馏之王", "King of Fractionation");
        Register("成就-永不停歇", "Never Stop");
        Register("成就-开线先锋", "Opening Pioneer");
        Register("成就-开线专家", "Opening Expert");
        Register("成就-配方入门", "Recipe Beginner");
        Register("成就-配方学者", "Recipe Scholar");
        Register("成就-配方专家", "Recipe Expert");
        Register("成就-万物百科", "Everything Encyclopedia");
        Register("成就-工艺优化", "Craft Optimization");
        Register("成就-工艺大师", "Craft Master");
        Register("成就-原胚循环", "Proto Cycle");
        Register("成就-星际整备", "Interstellar Readiness");
        Register("成就-精馏开路", "Rectification Opening");
        Register("成就-万物归一", "All Into One");
        Register("黑雾信标", "Dark Fog Signal", "黑雾信标");
        Register("黑雾压制", "Ground Suppression", "黑雾压制");
        Register("蜂巢猎场", "Hive Hunt", "蜂巢猎场");
        Register("奇点收束", "Singularity Convergence", "奇点收束");
        Register("遗物共振", "Relic Resonance", "遗物共振");
        Register("功勋回路", "Merit Circuit", "功勋回路");
        Register("授权整备", "Authorization Setup", "授权整备");
    }

    public static void LoadConfig(ConfigFile configFile) {
        achievementFlagsEntry = configFile.Bind(ConfigSection, ConfigAchievementFlags, string.Empty,
            "Achievement obtained flags. 1=obtained, 0=locked.");
        panelOpenCountEntry = configFile.Bind(ConfigSection, ConfigPanelOpenCount, 0,
            "How many times FE main panel has been opened.");

        panelOpenCount = Math.Max(0, panelOpenCountEntry.Value);
        LoadAchievementFlags(achievementFlagsEntry.Value);
        configLoaded = true;
        PersistAchievementConfig(forceSave: true);
    }

    public static void NotifyMainPanelOpened() {
        if (!configLoaded) {
            return;
        }

        panelOpenCount++;
        PersistAchievementConfig();
        CheckAndUnlockAchievements(showPopup: true);
    }

    public static void TickAutoUnlock() {
        if (!configLoaded) {
            return;
        }

        int frame = Time.frameCount;
        if (frame < nextAutoCheckFrame) {
            return;
        }
        nextAutoCheckFrame = frame + 60;
        CheckAndUnlockAchievements(showPopup: true);
    }

    private static void ResetSaveState() {
        Array.Clear(unlocked, 0, unlocked.Length);
        Array.Clear(claimed, 0, claimed.Length);
        panelOpenCount = 0;
        currentPage = 0;
        nextAutoCheckFrame = 0;
        SyncCurrentPageToSharedState();
    }

    private static void LoadAchievementFlags(string flags) {
        Array.Clear(unlocked, 0, unlocked.Length);
        Array.Clear(claimed, 0, claimed.Length);

        if (string.IsNullOrEmpty(flags)) {
            return;
        }

        int count = Math.Min(flags.Length, achievements.Length);
        for (int i = 0; i < count; i++) {
            bool obtained = flags[i] == '1';
            unlocked[i] = obtained;
            claimed[i] = obtained;
        }
    }

    private static string BuildAchievementFlags() {
        char[] flags = new char[achievements.Length];
        for (int i = 0; i < achievements.Length; i++) {
            flags[i] = claimed[i] ? '1' : '0';
        }
        return new string(flags);
    }

    private static void PersistAchievementConfig(bool forceSave = false) {
        if (!configLoaded || achievementFlagsEntry == null || panelOpenCountEntry == null) {
            return;
        }

        string flags = BuildAchievementFlags();
        bool changed = forceSave
                       || achievementFlagsEntry.Value != flags
                       || panelOpenCountEntry.Value != panelOpenCount;
        if (!changed) {
            return;
        }

        achievementFlagsEntry.Value = flags;
        panelOpenCountEntry.Value = panelOpenCount;
        global::FE.FractionateEverything.SaveConfig();
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        SyncCurrentPageFromSharedState();
        window = trans;
        tab = wnd.AddTab(trans, "成就详情");

        txtAchievementNames = new Text[achievements.Length];
        txtAchievementDescs = new Text[achievements.Length];
        txtAchievementRewards = new Text[achievements.Length];
        txtAchievementStates = new Text[achievements.Length];
        rewardIcons = new MyImageButton[achievements.Length];

        float x = 0f;
        float y = 18f + 7f;

        txtTitle = wnd.AddText2(x, y, tab, "成就系统", 17, "txtAchievementTitle");
        txtTitle.supportRichText = true;

        txtUnlockedSummary = wnd.AddText2(x + 235f, y, tab, "动态刷新", 14, "txtAchievementUnlockedSummary");
        txtUnlockedSummary.supportRichText = true;

        y += 26f;
        txtHiddenSummary = wnd.AddText2(x, y, tab, "动态刷新", 14, "txtAchievementHiddenSummary");
        txtHiddenSummary.supportRichText = true;

        txtBonusSummary = wnd.AddText2(x + 235f, y, tab, "动态刷新", 14, "txtAchievementBonusSummary");
        txtBonusSummary.supportRichText = true;

        y += 30f;

        listNameX = 0f;
        listNameW = 220f;
        listDescX = 220f;
        listDescW = 460f;
        listRewardX = 690f;
        listRewardTextX = 722f;
        listRewardTextW = 120f;
        listStateX = 860f;
        listStateW = 180f;

        wnd.AddText2(listNameX, y, tab, "成就", 14, "txtAchievementHeaderName");
        wnd.AddText2(listDescX, y, tab, "描述", 14, "txtAchievementHeaderDesc");
        wnd.AddText2(listRewardX, y, tab, "奖励", 14, "txtAchievementHeaderReward");
        wnd.AddText2(listStateX, y, tab, "状态", 14, "txtAchievementHeaderState");

        y += 26f;
        listStartY = y;

        for (int i = 0; i < achievements.Length; i++) {
            int j = i;

            txtAchievementNames[j] = wnd.AddText2(listNameX + x, y, tab, "动态刷新", 13, $"txtAchievementName{j}");
            txtAchievementNames[j].supportRichText = true;
            txtAchievementNames[j].rectTransform.sizeDelta = new Vector2(listNameW, 40f);

            txtAchievementDescs[j] = wnd.AddText2(listDescX + x, y, tab, "动态刷新", 13, $"txtAchievementDesc{j}");
            txtAchievementDescs[j].supportRichText = true;
            txtAchievementDescs[j].alignment = TextAnchor.UpperLeft;
            txtAchievementDescs[j].rectTransform.sizeDelta = new Vector2(listDescW, 40f);

            rewardIcons[j] = wnd.AddImageButton(listRewardX + x, y, tab, null).WithSize(40f, 40f);
            txtAchievementRewards[j] = wnd.AddText2(listRewardTextX + x, y, tab, "动态刷新", 13,
                $"txtAchievementReward{j}");
            txtAchievementRewards[j].supportRichText = true;
            txtAchievementRewards[j].rectTransform.sizeDelta = new Vector2(listRewardTextW, 32f);

            txtAchievementStates[j] = wnd.AddText2(listStateX + x, y, tab, "动态刷新", 13, $"txtAchievementState{j}");
            txtAchievementStates[j].supportRichText = true;
            txtAchievementStates[j].rectTransform.sizeDelta = new Vector2(listStateW, 32f);

            y += AchievementRowSpacing;
        }

        float paginationY = listStartY + AchievementRowSpacing * RowsPerPage + 8f;
        btnPrevPage = wnd.AddButton(GetPosition(0, 3).Item1, paginationY, tab, "上一页", onClick: PrevPage);
        txtPageIndicator = wnd.AddText2(GetPosition(1, 3).Item1, paginationY + 6f, tab, "");
        txtPageIndicator.alignment = TextAnchor.MiddleCenter;
        txtPageIndicator.rectTransform.sizeDelta = new(200f, txtPageIndicator.rectTransform.sizeDelta.y);
        btnNextPage = wnd.AddButton(GetPosition(2, 3).Item1, paginationY, tab, "下一页", onClick: NextPage);
    }

    private static bool IsPageVisible() {
        if (MainWindow.OpenedMainPanelType == FEMainPanelType.None) return false;
        if (MainWindow.OpenedMainPanelType == FEMainPanelType.Analysis) {
            return tab != null && tab.gameObject.activeInHierarchy;
        }
        return tab != null && tab.gameObject.activeSelf;
    }

    public static void UpdateUI() {
        if (!IsPageVisible()) {
            return;
        }

        CheckAndUnlockAchievements(showPopup: false);

        int obtainedCount = claimed.Count(v => v);
        int hiddenLockedCount = achievements.Length - obtainedCount;

        txtUnlockedSummary.text = string.Format("已获得成就".Translate(), obtainedCount, achievements.Length).WithColor(Orange);
        txtHiddenSummary.text = string.Format("隐藏未解锁".Translate(), hiddenLockedCount).WithColor(Blue);
        txtBonusSummary.text = string.Format("成就加成格式".Translate(),
            GetSuccessRateBonus() * 100f,
            GetDestroyReductionBonus() * 100f,
            GetDoubleOutputBonus() * 100f,
            GetEnergyReductionBonus() * 100f,
            GetLogisticsBonus() * 100f,
            GetPowerStageBonus() * 100f).WithColor(Green);

        int totalPages = Math.Max(1, (achievements.Length + RowsPerPage - 1) / RowsPerPage);
        if (currentPage >= totalPages) {
            currentPage = totalPages - 1;
            SyncCurrentPageToSharedState();
        }

        for (int i = 0; i < achievements.Length; i++) {
            txtAchievementNames[i].gameObject.SetActive(false);
            txtAchievementDescs[i].gameObject.SetActive(false);
            txtAchievementRewards[i].gameObject.SetActive(false);
            txtAchievementStates[i].gameObject.SetActive(false);
            rewardIcons[i].gameObject.SetActive(false);
        }

        int start = currentPage * RowsPerPage;
        int end = Math.Min(start + RowsPerPage, achievements.Length);
        for (int i = start; i < end; i++) {
            int slot = i - start;
            float rowY = listStartY + slot * AchievementRowSpacing;
            float rowTop = rowY - 10f;

            txtAchievementNames[i].SetPosition(listNameX, rowY);
            NormalizeRectWithTopLeft(txtAchievementDescs[i], listDescX, rowTop);
            NormalizeRectWithMidLeft(rewardIcons[i], listRewardX, rowY);
            txtAchievementRewards[i].SetPosition(listRewardTextX, rowY);
            txtAchievementStates[i].SetPosition(listStateX, rowY);

            txtAchievementNames[i].gameObject.SetActive(true);
            txtAchievementDescs[i].gameObject.SetActive(true);
            txtAchievementRewards[i].gameObject.SetActive(true);
            txtAchievementStates[i].gameObject.SetActive(true);

            RefreshAchievementRow(i);
        }

        UpdatePagination(totalPages);
    }

    private static bool CheckAndUnlockAchievements(bool showPopup) {
        bool changed = false;
        for (int i = 0; i < achievements.Length; i++) {
            if (claimed[i]) {
                continue;
            }

            if (!IsConditionSatisfied(achievements[i].Condition)) {
                continue;
            }

            UnlockAchievement(i, showPopup);
            changed = true;
        }

        if (changed) {
            PersistAchievementConfig();
        }

        return changed;
    }

    private static bool IsConditionSatisfied(Func<bool> condition) {
        try {
            return condition();
        }
        catch (Exception ex) {
            LogWarning($"[Achievement] Condition check failed: {ex.Message}");
            return false;
        }
    }

    private static void UnlockAchievement(int index, bool showPopup) {
        unlocked[index] = true;
        claimed[index] = true;
        achievements[index].GrantReward?.Invoke();

        if (showPopup) {
            string message = string.Format("成就获得提示".Translate(), achievements[index].NameKey.Translate());
            UIRealtimeTip.Popup(message, true, 2);
        }
    }

    private static void PrevPage() {
        if (currentPage <= 0) {
            return;
        }
        currentPage--;
        SyncCurrentPageToSharedState();
        UpdateUI();
    }

    private static void NextPage() {
        int totalPages = Math.Max(1, (achievements.Length + RowsPerPage - 1) / RowsPerPage);
        if (currentPage >= totalPages - 1) {
            return;
        }
        currentPage++;
        SyncCurrentPageToSharedState();
        UpdateUI();
    }

    private static void UpdatePagination(int totalPages) {
        txtPageIndicator.text = $"{(currentPage + 1)}/{totalPages}";
        btnPrevPage.button.interactable = currentPage > 0;
        btnNextPage.button.interactable = currentPage < totalPages - 1;
    }

    public static float GetSuccessRateBonus() {
        float bonus = 0f;
        for (int i = 0; i < achievements.Length; i++) {
            if (claimed[i]) {
                bonus += achievements[i].SuccessRateBonus;
            }
        }
        return bonus;
    }

    public static float GetDestroyReductionBonus() {
        float bonus = 0f;
        for (int i = 0; i < achievements.Length; i++) {
            if (claimed[i]) {
                bonus += achievements[i].DestroyReductionBonus;
            }
        }
        return bonus;
    }

    public static float GetDoubleOutputBonus() {
        float bonus = 0f;
        for (int i = 0; i < achievements.Length; i++) {
            if (claimed[i]) {
                bonus += achievements[i].DoubleOutputBonus;
            }
        }
        return bonus;
    }

    public static float GetEnergyReductionBonus() {
        float bonus = 0f;
        for (int i = 0; i < achievements.Length; i++) {
            if (claimed[i]) {
                bonus += achievements[i].EnergyReductionBonus;
            }
        }
        return bonus;
    }

    public static float GetLogisticsBonus() {
        float bonus = 0f;
        for (int i = 0; i < achievements.Length; i++) {
            if (claimed[i]) {
                bonus += achievements[i].LogisticsBonus;
            }
        }
        return bonus;
    }

    public static float GetPowerStageBonus() {
        float bonus = 0f;
        for (int i = 0; i < achievements.Length; i++) {
            if (claimed[i]) {
                bonus += achievements[i].PowerStageBonus;
            }
        }
        return bonus;
    }

    private static void RefreshAchievementRow(int index) {
        if (!claimed[index]) {
            txtAchievementNames[index].text = "隐藏成就提示".Translate().WithColor(Gray);
            txtAchievementDescs[index].text = "隐藏成就描述".Translate().WithColor(Gray);
            txtAchievementRewards[index].text = "";
            txtAchievementStates[index].text = "未解锁".Translate().WithColor(Gray);
            rewardIcons[index].gameObject.SetActive(false);
            return;
        }

        AchievementInfo info = achievements[index];
        string tierTag = GetTierTag(info.Tier);
        Color tierColor = GetTierColor(info.Tier);
        bool hasRewardIcon = TryGetRewardIconInfo(info.RewardKey, out int rewardItemId, out int rewardCount);
        rewardIcons[index].gameObject.SetActive(hasRewardIcon);
        rewardIcons[index].Proto = hasRewardIcon ? LDB.items.Select(rewardItemId) : null;
        string rewardText = hasRewardIcon
            ? $"x{rewardCount}".WithColor(Blue)
            : info.RewardKey.Translate().WithColor(Blue);

        txtAchievementNames[index].text =
            $"{tierTag.WithColor(tierColor)} [{info.CategoryKey.Translate()}] {info.NameKey.Translate()}";
        txtAchievementDescs[index].text = info.DescKey.Translate();
        txtAchievementRewards[index].text = rewardText;
        rewardIcons[index].gameObject.SetActive(hasRewardIcon);
        if (hasRewardIcon) {
            rewardIcons[index].SetCount(rewardCount);
            txtAchievementRewards[index].text = "";
        }
        else {
            rewardIcons[index].ClearCountText();
        }

        txtAchievementStates[index].text = "已获得".Translate().WithColor(Green);
    }

    private static string GetTierTag(ETier tier) {
        return tier switch {
            ETier.Bronze => "[铜]",
            ETier.Silver => "[银]",
            ETier.Gold => "[金]",
            ETier.Platinum => "[铂]",
            _ => "[?]",
        };
    }

    private static Color GetTierColor(ETier tier) {
        return tier switch {
            ETier.Bronze => Orange,
            ETier.Silver => White,
            ETier.Gold => Gold,
            ETier.Platinum => Blue,
            _ => White,
        };
    }

    private static bool TryGetRewardIconInfo(string rewardKey, out int itemId, out int count) {
        switch (rewardKey) {
            case "成就奖励-残片200":
                itemId = IFE残片;
                count = 200;
                return true;
            case "成就奖励-残片300":
                itemId = IFE残片;
                count = 300;
                return true;
            case "成就奖励-残片500":
                itemId = IFE残片;
                count = 500;
                return true;
            case "成就奖励-残片800":
                itemId = IFE残片;
                count = 800;
                return true;
            case "成就奖励-残片1000":
                itemId = IFE残片;
                count = 1000;
                return true;
            case "成就奖励-残片2000":
                itemId = IFE残片;
                count = 2000;
                return true;
            case "成就奖励-当前阶段矩阵2":
                itemId = GetCurrentStageMatrixId();
                count = 2;
                return true;
            case "成就奖励-当前阶段矩阵4":
                itemId = GetCurrentStageMatrixId();
                count = 4;
                return true;
            case "成就奖励-当前阶段矩阵8":
                itemId = GetCurrentStageMatrixId();
                count = 8;
                return true;
            case "成就奖励-当前阶段矩阵16":
                itemId = GetCurrentStageMatrixId();
                count = 16;
                return true;
            case "成就奖励-配方核心1":
                itemId = IFE分馏配方核心;
                count = 1;
                return true;
            case "成就奖励-配方核心3":
                itemId = IFE分馏配方核心;
                count = 3;
                return true;
            case "成就奖励-定向原胚1":
                itemId = IFE分馏塔定向原胚;
                count = 1;
                return true;
            case "成就奖励-星际物流交互站1":
                itemId = IFE星际物流交互站;
                count = 1;
                return true;
            case "成就奖励-精馏塔原胚3":
                itemId = IFE精馏塔原胚;
                count = 3;
                return true;
            default:
                itemId = 0;
                count = 0;
                return false;
        }
    }

    private static void GrantRewardByKey(string rewardKey) {
        switch (rewardKey) {
            case "成就奖励-残片200":
                GrantItems((IFE残片, 200));
                break;
            case "成就奖励-残片300":
                GrantItems((IFE残片, 300));
                break;
            case "成就奖励-残片500":
                GrantItems((IFE残片, 500));
                break;
            case "成就奖励-残片800":
                GrantItems((IFE残片, 800));
                break;
            case "成就奖励-残片1000":
                GrantItems((IFE残片, 1000));
                break;
            case "成就奖励-残片2000":
                GrantItems((IFE残片, 2000));
                break;
            case "成就奖励-当前阶段矩阵2":
                GrantItems((GetCurrentStageMatrixId(), 2));
                break;
            case "成就奖励-当前阶段矩阵4":
                GrantItems((GetCurrentStageMatrixId(), 4));
                break;
            case "成就奖励-当前阶段矩阵8":
                GrantItems((GetCurrentStageMatrixId(), 8));
                break;
            case "成就奖励-当前阶段矩阵16":
                GrantItems((GetCurrentStageMatrixId(), 16));
                break;
            case "成就奖励-配方核心1":
                GrantItems((IFE分馏配方核心, 1));
                break;
            case "成就奖励-配方核心3":
                GrantItems((IFE分馏配方核心, 3));
                break;
            case "成就奖励-定向原胚1":
                GrantItems((IFE分馏塔定向原胚, 1));
                break;
            case "成就奖励-星际物流交互站1":
                GrantItems((IFE星际物流交互站, 1));
                break;
            case "成就奖励-精馏塔原胚3":
                GrantItems((IFE精馏塔原胚, 3));
                break;
            case "成就奖励-循环任务自动领取":
                RecurringTask.UnlockAutoClaim();
                break;
        }
    }

    private static void GrantItems(params (int itemId, int count)[] rewards) {
        foreach ((int itemId, int count) in rewards) {
            AddItemToModData(itemId, count, 0, true);
            UIItemup.Up(itemId, count);
        }
    }

    private static bool IsTechUnlocked(int techId) {
        return GameMain.history != null && GameMain.history.TechUnlocked(techId);
    }

    private static int GetUnlockedRecipeCount() {
        return RecipeTypes
            .SelectMany(type => GetRecipesByType(type))
            .Count(recipe => recipe.Unlocked);
    }

    private static int GetMaxBuildingLevel() {
        return Math.Max(InteractionTower.Level, Math.Max(MineralReplicationTower.Level,
            Math.Max(PointAggregateTower.Level, Math.Max(ConversionTower.Level, RectificationTower.Level))));
    }

    private static int GetProtoInventoryCount() {
        return (int)(GetItemTotalCount(IFE交互塔原胚)
                     + GetItemTotalCount(IFE矿物复制塔原胚)
                     + GetItemTotalCount(IFE点数聚集塔原胚)
                     + GetItemTotalCount(IFE转化塔原胚)
                     + GetItemTotalCount(IFE精馏塔原胚)
                     + GetItemTotalCount(IFE分馏塔定向原胚));
    }

    private static int GetCurrentStageMatrixId() {
        return GameMain.history != null && GameMain.history.TechUnlocked(T宇宙矩阵)
            ? I宇宙矩阵
            : GameMain.history != null && GameMain.history.TechUnlocked(T引力矩阵)
                ? I引力矩阵
                : GameMain.history != null && GameMain.history.TechUnlocked(T信息矩阵)
                    ? I信息矩阵
                    : GameMain.history != null && GameMain.history.TechUnlocked(T结构矩阵)
                        ? I结构矩阵
                        : GameMain.history != null && GameMain.history.TechUnlocked(T能量矩阵)
                            ? I能量矩阵
                            : I电磁矩阵;
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        SyncCurrentPageFromSharedState();

        if (!configLoaded || r.BaseStream.Length <= 0) {
            return;
        }

        ResetSaveState();

        bool migrated = false;
        bool[] oldUnlocked = [];
        bool[] oldClaimed = [];
        bool[] saveClaimed = [];

        r.ReadBlocks(
            ("PanelOpenCountV2", br => {
                panelOpenCount = Math.Max(0, br.ReadInt32());
            }),
            ("ClaimedFlagsV2", br => {
                saveClaimed = ReadLegacyFlags(br);
            }),
            ("UnlockedFlags", br => {
                oldUnlocked = ReadLegacyFlags(br);
            }),
            ("ClaimedFlags", br => {
                oldClaimed = ReadLegacyFlags(br);
            })
        );

        int saveCount = Math.Min(saveClaimed.Length, achievements.Length);
        for (int i = 0; i < saveCount; i++) {
            if (!saveClaimed[i] || claimed[i]) {
                continue;
            }
            unlocked[i] = true;
            claimed[i] = true;
            migrated = true;
        }

        int oldCount = Math.Max(oldUnlocked.Length, oldClaimed.Length);
        for (int oldIndex = 0; oldIndex < oldCount && oldIndex < legacyAchievementNameOrder.Length; oldIndex++) {
            bool wasUnlocked = oldIndex < oldUnlocked.Length && oldUnlocked[oldIndex];
            bool wasClaimed = oldIndex < oldClaimed.Length && oldClaimed[oldIndex];
            if (!wasUnlocked && !wasClaimed) {
                continue;
            }

            string legacyName = legacyAchievementNameOrder[oldIndex];
            if (!achievementIndexByName.TryGetValue(legacyName, out int newIndex)) {
                continue;
            }

            if (claimed[newIndex]) {
                continue;
            }

            if (wasClaimed) {
                unlocked[newIndex] = true;
                claimed[newIndex] = true;
            }
            else {
                UnlockAchievement(newIndex, showPopup: false);
            }
            migrated = true;
        }

        if (migrated) {
            PersistAchievementConfig();
        }
    }

    private static bool[] ReadLegacyFlags(BinaryReader br) {
        int count = br.ReadInt32();
        bool[] flags = new bool[count];
        for (int i = 0; i < count; i++) {
            flags[i] = br.ReadBoolean();
        }
        return flags;
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("PanelOpenCountV2", bw => bw.Write(panelOpenCount)),
            ("ClaimedFlagsV2", bw => {
                bw.Write(achievements.Length);
                for (int i = 0; i < achievements.Length; i++) {
                    bw.Write(claimed[i]);
                }
            })
        );
    }

    public static void IntoOtherSave() {
        ResetSaveState();
        PersistAchievementConfig(forceSave: true);
    }

    public static bool IsAchievementClaimed(string nameKey) {
        for (int i = 0; i < achievements.Length; i++) {
            if (achievements[i].NameKey == nameKey) {
                return claimed[i];
            }
        }
        return false;
    }

    #endregion
}
