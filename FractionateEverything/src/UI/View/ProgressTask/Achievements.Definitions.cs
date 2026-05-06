using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.Logic.RecipeGrowth;
using FE.UI.Components;
using FE.UI.View;
using FE.UI.View.Archive;
using FE.UI.View.DrawGrowth;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Components.GridDsl;
using static FE.Logic.Manager.GachaManager;
using static FE.Logic.Manager.ProcessManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Logic.Recipe.ERecipeExtension;
using static FE.Utils.Utils;

namespace FE.UI.View.ProgressTask;

public static partial class Achievements {
    private static Dictionary<string, int> BuildAchievementIndexByName() {
        var map = new Dictionary<string, int>(achievements.Length);
        for (int i = 0; i < achievements.Length; i++) {
            map[achievements[i].NameKey] = i;
        }
        return map;
    }

    private static Dictionary<string, AchievementRewardDefinition> BuildRewardDefinitionsByKey() {
        AchievementRewardDefinition[] definitions = [
            new("成就奖励-循环任务自动领取", unlockRecurringAutoClaim: true),
        ];

        var map = new Dictionary<string, AchievementRewardDefinition>(definitions.Length);
        foreach (AchievementRewardDefinition definition in definitions) {
            map[definition.RewardKey] = definition;
        }
        return map;
    }

    private static AchievementInfo[] BuildAchievements() {
        var list = new List<AchievementInfo>(96);
        AddProductionAchievements(list);
        AddOpeningAchievements(list);
        AddRecipeAchievements(list);
        AddGrowthAchievements(list);
        AddRecurringAchievements(list);
        AddDarkFogAchievements(list);
        AddChallengeAchievements(list);
        return [.. list];
    }

    private static void AddProductionAchievements(List<AchievementInfo> list) {
        var totalDefs =
            new (string Name, long Target, string RewardKey, ETier Tier, float SuccessBonus, float DestroyBonus, float
                DoubleBonus)[] {
                    ("分馏星河", 100_000_000L, "成就奖励-残片1000", ETier.Gold, 0.01f, 0.003f, 0.003f),
                    ("分馏星海", 1_000_000_000L, "成就奖励-残片2000", ETier.Platinum, 0.02f, 0.006f, 0.008f),
                    ("分馏宇宙", 10_000_000_000L, "成就奖励-残片2000", ETier.Platinum, 0.03f, 0.01f, 0.02f),
                };

        foreach ((string name, long target, string rewardKey, ETier tier, float successBonus, float destroyBonus,
                     float doubleBonus) in totalDefs) {
            string desc = $"累计完成 {target:N0} 次分馏成功";
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

        var rateDefs =
            new (string Name, long Target, string RewardKey, ETier Tier, float SuccessBonus, float DestroyBonus, float
                DoubleBonus)[] {
                    ("带速成型", 100_000L, "成就奖励-残片1000", ETier.Gold, 0.006f, 0.002f, 0.002f),
                    ("满带洪流", 1_000_000L, "成就奖励-残片2000", ETier.Platinum, 0.012f, 0.004f, 0.006f),
                    ("星河带速", 10_000_000L, "成就奖励-残片2000", ETier.Platinum, 0.02f, 0.008f, 0.012f),
                };

        foreach ((string name, long target, string rewardKey, ETier tier, float successBonus, float destroyBonus,
                     float doubleBonus) in rateDefs) {
            string desc = $"历史峰值分馏速率达到 {target:N0} 次/min";
            list.Add(new AchievementInfo(
                "成就分类-生产",
                name,
                desc,
                rewardKey,
                tier,
                () => peakFractionSuccessesPerMinute >= target,
                () => GrantRewardByKey(rewardKey),
                successRateBonus: successBonus,
                destroyReductionBonus: destroyBonus,
                doubleOutputBonus: doubleBonus));
        }
    }

    private static void AddOpeningAchievements(List<AchievementInfo> list) {
        var defs =
            new (string Name, int Target, string RewardKey, ETier Tier, float DoubleBonus, float LogisticsBonus)[] {
                ("成就-开线先锋", 100, "成就奖励-当前阶段矩阵4", ETier.Silver, 0.002f, 0f),
                ("开线统筹", 1000, "成就奖励-当前阶段矩阵16", ETier.Platinum, 0.008f, 0.006f),
                ("开线传说", 10000, "成就奖励-当前阶段矩阵16", ETier.Platinum, 0.012f, 0.012f),
            };

        foreach ((string name, int target, string rewardKey, ETier tier, float doubleBonus,
                     float logisticsBonus) in defs) {
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
        var defs =
            new (string Name, int Target, string RewardKey, ETier Tier, float SuccessBonus, float DestroyBonus, float
                PowerBonus)[] {
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

        foreach ((string name, int target, string rewardKey, ETier tier, float successBonus, float destroyBonus,
                     float powerBonus) in defs) {
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
        var defs =
            new (string Name, int Target, string RewardKey, ETier Tier, float LogisticsBonus, float DoubleBonus)[] {
                ("任务推进", 10, "成就奖励-当前阶段矩阵2", ETier.Silver, 0.003f, 0f),
                ("成就-任务自动化", 100, "成就奖励-循环任务自动领取", ETier.Gold, 0.008f, 0.004f),
                ("任务永动", 1000, "成就奖励-当前阶段矩阵16", ETier.Platinum, 0.016f, 0.01f),
            };

        foreach ((string name, int target, string rewardKey, ETier tier, float logisticsBonus,
                     float doubleBonus) in defs) {
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

    private static void AddDarkFogAchievements(List<AchievementInfo> list) {
        list.Add(new AchievementInfo(
            "成就分类-黑雾",
            "黑雾信标",
            "将黑雾支线推进到“信号接触”阶段",
            "成就奖励-当前阶段矩阵4",
            ETier.Bronze,
            () => DarkFogCombatManager.GetCurrentStage() >= EDarkFogCombatStage.Signal,
            () => GrantRewardByKey("成就奖励-当前阶段矩阵4"),
            successRateBonus: 0.002f,
            logisticsBonus: 0.002f));
        list.Add(new AchievementInfo(
            "成就分类-黑雾",
            "黑雾压制",
            "将黑雾支线推进到“地面压制”阶段",
            "成就奖励-残片800",
            ETier.Silver,
            () => DarkFogCombatManager.GetCurrentStage() >= EDarkFogCombatStage.GroundSuppression,
            () => GrantRewardByKey("成就奖励-残片800"),
            successRateBonus: 0.003f,
            destroyReductionBonus: 0.002f));
        list.Add(new AchievementInfo(
            "成就分类-黑雾",
            "蜂巢猎场",
            "将黑雾支线推进到“星域围猎”阶段",
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
            "将黑雾支线推进到“奇点收束”阶段",
            "成就奖励-残片2000",
            ETier.Platinum,
            () => DarkFogCombatManager.GetCurrentStage() >= EDarkFogCombatStage.Singularity,
            () => GrantRewardByKey("成就奖励-残片2000"),
            successRateBonus: 0.006f,
            destroyReductionBonus: 0.004f,
            powerStageBonus: 0.012f));
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
}
