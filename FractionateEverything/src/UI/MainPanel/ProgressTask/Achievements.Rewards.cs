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
    public static float GetSuccessRateBonus() {
        EnsureBonusSummaryCache();
        return cachedBonusSummary.SuccessRateBonus;
    }

    public static float GetDestroyReductionBonus() {
        EnsureBonusSummaryCache();
        return cachedBonusSummary.DestroyReductionBonus;
    }

    public static float GetDoubleOutputBonus() {
        EnsureBonusSummaryCache();
        return cachedBonusSummary.DoubleOutputBonus;
    }

    public static float GetEnergyReductionBonus() {
        EnsureBonusSummaryCache();
        return cachedBonusSummary.EnergyReductionBonus;
    }

    public static float GetLogisticsBonus() {
        EnsureBonusSummaryCache();
        return cachedBonusSummary.LogisticsBonus;
    }

    public static float GetPowerStageBonus() {
        EnsureBonusSummaryCache();
        return cachedBonusSummary.PowerStageBonus;
    }

    private static string GetFunctionalRewardText(AchievementInfo info) {
        List<string> rewards = [];
        AddFunctionalRewardText(rewards, "功能奖励-成功", info.SuccessRateBonus, positive: true);
        AddFunctionalRewardText(rewards, "功能奖励-损毁", info.DestroyReductionBonus, positive: false);
        AddFunctionalRewardText(rewards, "功能奖励-翻倍", info.DoubleOutputBonus, positive: true);
        AddFunctionalRewardText(rewards, "功能奖励-能耗", info.EnergyReductionBonus, positive: false);
        AddFunctionalRewardText(rewards, "功能奖励-物流", info.LogisticsBonus, positive: true);
        AddFunctionalRewardText(rewards, "功能奖励-发电", info.PowerStageBonus, positive: true);

        if (TryResolveRewardDefinition(info.RewardKey, out AchievementRewardDefinition definition)
            && definition.UnlockRecurringAutoClaim) {
            rewards.Add("成就奖励-循环任务自动领取".Translate());
        }

        if (rewards.Count == 0) {
            return "无额外功能奖励".Translate().WithColor(Gray);
        }

        if (rewards.Count <= 2) {
            return string.Join(" / ", rewards).WithColor(Blue);
        }

        return $"{string.Join(" / ", rewards.Take(2))} / {"功能加成过多".Translate()}".WithColor(Blue);
    }

    private static void AddFunctionalRewardText(List<string> rewards, string key, float value, bool positive) {
        if (value <= 0f) {
            return;
        }

        float percent = value * 100f;
        rewards.Add(string.Format(key.Translate(), percent.ToString("0.##")));
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

    private static void GrantRewardByKey(string rewardKey) {
        if (!TryResolveRewardDefinition(rewardKey, out AchievementRewardDefinition definition)) {
            return;
        }

        if (definition.UnlockRecurringAutoClaim) {
            RecurringTask.UnlockAutoClaim();
        }
    }

    private static bool TryResolveRewardDefinition(string rewardKey, out AchievementRewardDefinition definition) {
        return rewardDefinitionsByKey.TryGetValue(rewardKey, out definition);
    }

    private static bool IsTechUnlocked(int techId) {
        return GameMain.history != null && GameMain.history.TechUnlocked(techId);
    }

    private static int GetUnlockedRecipeCount() {
        return RecipeGrowthQueries.GetUnlockedCount(RecipeTypes);
    }

    private static int GetMaxBuildingLevel() {
        return Math.Max(InteractionTower.Level, Math.Max(MineralReplicationTower.Level,
            Math.Max(PointAggregateTower.Level, Math.Max(ConversionTower.Level, RectificationTower.Level))));
    }
}
