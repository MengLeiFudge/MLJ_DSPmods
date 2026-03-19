using System;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.UI.Components;
using FE.UI.View.GetItemRecipe;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ProcessManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.UI.View.ProgressSystem;

public static class Achievements {
    private static RectTransform window;
    private static RectTransform tab;

    private readonly struct AchievementInfo(
        string nameKey,
        string descKey,
        string rewardKey,
        ETier tier,
        Func<bool> condition,
        Action grantReward,
        float successRateBonus = 0f) {
        public readonly string NameKey = nameKey;
        public readonly string DescKey = descKey;
        public readonly string RewardKey = rewardKey;
        public readonly ETier Tier = tier;
        public readonly Func<bool> Condition = condition;
        public readonly Action GrantReward = grantReward;
        public readonly float SuccessRateBonus = successRateBonus;
    }

    private enum ETier {
        Bronze,
        Silver,
        Gold,
        Platinum,
    }

    private static Text txtTitle;
    private static Text txtUnlockedSummary;
    private static Text txtHiddenSummary;
    private static Text txtBonusSummary;

    private static Text[] txtAchievementNames;
    private static Text[] txtAchievementDescs;
    private static Text[] txtAchievementStates;
    private static UIButton[] btnClaims;

    private static readonly AchievementInfo[] achievements = [
        new("成就-初次分馏", "成就描述-初次分馏", "成就奖励-电磁奖券10", ETier.Bronze,
            () => totalFractionSuccesses >= 1,
            () => GrantItems((IFE电磁奖券, 10))),
        new("成就-分馏百次", "成就描述-分馏百次", "成就奖励-电磁奖券30", ETier.Bronze,
            () => totalFractionSuccesses >= 100,
            () => GrantItems((IFE电磁奖券, 30))),
        new("成就-千锤百炼", "成就描述-千锤百炼", "成就奖励-能量奖券20", ETier.Silver,
            () => totalFractionSuccesses >= 1000,
            () => GrantItems((IFE能量奖券, 20)), 0.005f),
        new("成就-万物皆可分馏", "成就描述-万物皆可分馏", "成就奖励-结构奖券30", ETier.Gold,
            () => totalFractionSuccesses >= 10000,
            () => GrantItems((IFE结构奖券, 30)), 0.01f),
        new("成就-分馏之王", "成就描述-分馏之王", "成就奖励-宇宙奖券50", ETier.Platinum,
            () => totalFractionSuccesses >= 100000,
            () => GrantItems((IFE宇宙奖券, 50)), 0.02f),
        new("成就-永不停歇", "成就描述-永不停歇", "成就奖励-宇宙奖券200", ETier.Platinum,
            () => totalFractionSuccesses >= 1000000,
            () => GrantItems((IFE宇宙奖券, 200)), 0.03f),

        new("成就-初次抽奖", "成就描述-初次抽奖", "成就奖励-电磁奖券5", ETier.Bronze,
            () => TicketRaffle.totalDraws >= 1,
            () => GrantItems((IFE电磁奖券, 5))),
        new("成就-幸运星", "成就描述-幸运星", "成就奖励-配方核心1", ETier.Bronze,
            () => TicketRaffle.totalDraws >= 100,
            () => GrantItems((IFE分馏配方核心, 1))),
        new("成就-抽奖达人", "成就描述-抽奖达人", "成就奖励-配方核心3", ETier.Silver,
            () => TicketRaffle.totalDraws >= 500,
            () => GrantItems((IFE分馏配方核心, 3)), 0.005f),
        new("成就-欧皇降临", "成就描述-欧皇降临", "成就奖励-宇宙奖券30", ETier.Gold,
            () => TicketRaffle.totalDraws >= 2000,
            () => GrantItems((IFE宇宙奖券, 30)), 0.01f),
        new("成就-氪金大佬", "成就描述-氪金大佬", "成就奖励-宇宙奖券100", ETier.Platinum,
            () => TicketRaffle.totalDraws >= 10000,
            () => GrantItems((IFE宇宙奖券, 100)), 0.02f),

        new("成就-初级工程师", "成就描述-初级工程师", "成就奖励-增幅芯片2", ETier.Bronze,
            () => GetMaxBuildingLevel() >= 3,
            () => GrantItems((IFE分馏塔增幅芯片, 2))),
        new("成就-高级工程师", "成就描述-高级工程师", "成就奖励-增幅芯片5", ETier.Silver,
            () => GetMaxBuildingLevel() >= 6,
            () => GrantItems((IFE分馏塔增幅芯片, 5)), 0.005f),
        new("成就-特质觉醒", "成就描述-特质觉醒", "成就奖励-增幅芯片10", ETier.Silver,
            () => GetMaxBuildingLevel() >= 6 && HasAnyTraitEnabled(),
            () => GrantItems((IFE分馏塔增幅芯片, 10)), 0.005f),
        new("成就-建筑大师", "成就描述-建筑大师", "成就奖励-增幅芯片20", ETier.Gold,
            () => GetMaxBuildingLevel() >= 10,
            () => GrantItems((IFE分馏塔增幅芯片, 20)), 0.01f),
        new("成就-完美工匠", "成就描述-完美工匠", "成就奖励-宇宙奖券50", ETier.Platinum,
            () => HasLevel12BuildingWithTrait(),
            () => GrantItems((IFE宇宙奖券, 50)), 0.02f),

        new("成就-配方入门", "成就描述-配方入门", "成就奖励-配方核心1", ETier.Bronze,
            () => GetUnlockedRecipeCount() >= 5,
            () => GrantItems((IFE分馏配方核心, 1))),
        new("成就-配方学者", "成就描述-配方学者", "成就奖励-配方核心3", ETier.Silver,
            () => GetUnlockedRecipeCount() >= 30,
            () => GrantItems((IFE分馏配方核心, 3)), 0.005f),
        new("成就-配方专家", "成就描述-配方专家", "成就奖励-配方核心5", ETier.Gold,
            () => GetUnlockedRecipeCount() >= 80,
            () => GrantItems((IFE分馏配方核心, 5)), 0.01f),
        new("成就-万物百科", "成就描述-万物百科", "成就奖励-宇宙奖券80", ETier.Platinum,
            () => GetUnlockedRecipeCount() >= 150,
            () => GrantItems((IFE宇宙奖券, 80)), 0.02f),
        new("成就-满级配方", "成就描述-满级配方", "成就奖励-增幅芯片10", ETier.Gold,
            HasMaxLevelRecipe,
            () => GrantItems((IFE分馏塔增幅芯片, 10)), 0.01f),

        new("成就-符文收集者", "成就描述-符文收集者", "成就奖励-随机精华100", ETier.Bronze,
            () => RuneManager.allRunes.Count >= 5,
            () => GrantRandomEssence(100)),
        new("成就-符文锻造师", "成就描述-符文锻造师", "成就奖励-随机精华300", ETier.Silver,
            () => RuneManager.totalRuneUpgrades >= 50,
            () => GrantRandomEssence(300), 0.005f),
        new("成就-五星奇迹", "成就描述-五星奇迹", "成就奖励-随机精华500", ETier.Silver,
            () => RuneManager.allRunes.Any(rune => rune.star == 5),
            () => GrantRandomEssence(500), 0.005f),
        new("成就-符文满槽", "成就描述-符文满槽", "成就奖励-宇宙奖券20", ETier.Gold,
            () => RuneManager.equippedRuneIds.All(id => id != 0),
            () => GrantItems((IFE宇宙奖券, 20)), 0.01f),
        new("成就-符文满级", "成就描述-符文满级", "成就奖励-宇宙奖券50", ETier.Platinum,
            () => RuneManager.allRunes.Any(rune => rune.level >= 20),
            () => GrantItems((IFE宇宙奖券, 50)), 0.02f),

        new("成就-全面发展", "成就描述-全面发展", "成就奖励-宇宙奖券30", ETier.Gold,
            () => totalFractionSuccesses >= 1000
                  && TicketRaffle.totalDraws >= 100
                  && RuneManager.allRunes.Count >= 3
                  && GetUnlockedRecipeCount() >= 20
                  && GetMaxBuildingLevel() >= 3,
            () => GrantItems((IFE宇宙奖券, 30)), 0.01f),
        new("成就-万物归一", "成就描述-万物归一", "成就奖励-宇宙奖券200", ETier.Platinum,
            () => totalFractionSuccesses >= 50000
                  && GetUnlockedRecipeCount() >= 100
                  && GetMaxBuildingLevel() >= 10
                  && RuneManager.allRunes.Count >= 20
                  && TicketRaffle.totalDraws >= 1000,
            () => GrantItems((IFE宇宙奖券, 200)), 0.03f),
    ];

    private static bool[] unlocked = new bool[achievements.Length];
    private static bool[] claimed = new bool[achievements.Length];

    public static void AddTranslations() {
        Register("成就详情", "Achievements");
        Register("成就系统", "Achievement System");
        Register("成就", "Achievement");
        Register("描述", "Description");
        Register("状态", "Status");
        Register("操作", "Action");
        Register("奖励", "Reward");

        Register("已解锁成就", "Unlocked: {0}/{1}", "已解锁：{0}/{1}");
        Register("隐藏未解锁", "Hidden locked: {0}", "隐藏未解锁：{0}");
        Register("成就加成格式", "Achievement bonus: +{0}%", "成就加成：+{0}%");

        Register("已解锁", "Unlocked");
        Register("未解锁", "Locked");
        Register("领取", "Claim");
        Register("已领取", "Claimed");
        Register("未领取", "Unclaimed");
        Register("隐藏成就提示", "???", "???");
        Register("隐藏成就描述", "Hidden achievement", "未解锁");

        Register("成就品阶-青铜", "Bronze", "青铜");
        Register("成就品阶-白银", "Silver", "白银");
        Register("成就品阶-黄金", "Gold", "黄金");
        Register("成就品阶-白金", "Platinum", "白金");

        Register("成就-初次分馏", "First Fractionation");
        Register("成就描述-初次分馏", "Complete 1 successful fractionation", "累计完成 1 次分馏成功");
        Register("成就奖励-电磁奖券10", "Electromagnetic Tickets x10", "电磁奖券 x10");

        Register("成就-分馏百次", "Hundred Distillations");
        Register("成就描述-分馏百次", "Complete 100 successful fractionations", "累计完成 100 次分馏成功");
        Register("成就奖励-电磁奖券30", "Electromagnetic Tickets x30", "电磁奖券 x30");

        Register("成就-千锤百炼", "Tempered Through Trials");
        Register("成就描述-千锤百炼", "Complete 1000 successful fractionations", "累计完成 1000 次分馏成功");
        Register("成就奖励-能量奖券20", "Energy Tickets x20", "能量奖券 x20");

        Register("成就-万物皆可分馏", "Fractionate Everything");
        Register("成就描述-万物皆可分馏", "Complete 10000 successful fractionations", "累计完成 10000 次分馏成功");
        Register("成就奖励-结构奖券30", "Structure Tickets x30", "结构奖券 x30");

        Register("成就-分馏之王", "King of Fractionation");
        Register("成就描述-分馏之王", "Complete 100000 successful fractionations", "累计完成 100000 次分馏成功");
        Register("成就奖励-宇宙奖券50", "Universe Tickets x50", "宇宙奖券 x50");

        Register("成就-永不停歇", "Never Stop");
        Register("成就描述-永不停歇", "Complete 1000000 successful fractionations", "累计完成 1000000 次分馏成功");
        Register("成就奖励-宇宙奖券200", "Universe Tickets x200", "宇宙奖券 x200");

        Register("成就-初次抽奖", "First Draw");
        Register("成就描述-初次抽奖", "Perform 1 raffle draw", "累计完成 1 次奖券抽奖");
        Register("成就奖励-电磁奖券5", "Electromagnetic Tickets x5", "电磁奖券 x5");

        Register("成就-幸运星", "Lucky Star");
        Register("成就描述-幸运星", "Perform 100 raffle draws", "累计完成 100 次奖券抽奖");
        Register("成就奖励-配方核心1", "Fractionation Recipe Core x1", "分馏配方核心 x1");

        Register("成就-抽奖达人", "Draw Expert");
        Register("成就描述-抽奖达人", "Perform 500 raffle draws", "累计完成 500 次奖券抽奖");
        Register("成就奖励-配方核心3", "Fractionation Recipe Core x3", "分馏配方核心 x3");

        Register("成就-欧皇降临", "Blessed by Luck");
        Register("成就描述-欧皇降临", "Perform 2000 raffle draws", "累计完成 2000 次奖券抽奖");
        Register("成就奖励-宇宙奖券30", "Universe Tickets x30", "宇宙奖券 x30");

        Register("成就-氪金大佬", "Whale Supreme");
        Register("成就描述-氪金大佬", "Perform 10000 raffle draws", "累计完成 10000 次奖券抽奖");
        Register("成就奖励-宇宙奖券100", "Universe Tickets x100", "宇宙奖券 x100");

        Register("成就-初级工程师", "Junior Engineer");
        Register("成就描述-初级工程师", "Upgrade any FE building to level 3", "任意万物分馏建筑等级达到 3");
        Register("成就奖励-增幅芯片2", "Fractionator Amplify Chip x2", "分馏塔增幅芯片 x2");

        Register("成就-高级工程师", "Senior Engineer");
        Register("成就描述-高级工程师", "Upgrade any FE building to level 6", "任意万物分馏建筑等级达到 6");
        Register("成就奖励-增幅芯片5", "Fractionator Amplify Chip x5", "分馏塔增幅芯片 x5");

        Register("成就-特质觉醒", "Trait Awakening");
        Register("成就描述-特质觉醒", "Reach level 6 and unlock any building trait", "任意建筑达到 6 级并解锁一个特质");
        Register("成就奖励-增幅芯片10", "Fractionator Amplify Chip x10", "分馏塔增幅芯片 x10");

        Register("成就-建筑大师", "Master Builder");
        Register("成就描述-建筑大师", "Upgrade any FE building to level 10", "任意万物分馏建筑等级达到 10");
        Register("成就奖励-增幅芯片20", "Fractionator Amplify Chip x20", "分馏塔增幅芯片 x20");

        Register("成就-完美工匠", "Perfect Artisan");
        Register("成就描述-完美工匠", "Have one level 12 building with trait enabled", "至少一个建筑等级达到 12 且特质已生效");

        Register("成就-配方入门", "Recipe Beginner");
        Register("成就描述-配方入门", "Unlock 5 fractionation recipes", "累计解锁 5 个分馏配方");

        Register("成就-配方学者", "Recipe Scholar");
        Register("成就描述-配方学者", "Unlock 30 fractionation recipes", "累计解锁 30 个分馏配方");

        Register("成就-配方专家", "Recipe Expert");
        Register("成就描述-配方专家", "Unlock 80 fractionation recipes", "累计解锁 80 个分馏配方");
        Register("成就奖励-配方核心5", "Fractionation Recipe Core x5", "分馏配方核心 x5");

        Register("成就-万物百科", "Everything Encyclopedia");
        Register("成就描述-万物百科", "Unlock 150 fractionation recipes", "累计解锁 150 个分馏配方");
        Register("成就奖励-宇宙奖券80", "Universe Tickets x80", "宇宙奖券 x80");

        Register("成就-满级配方", "Maxed Recipe");
        Register("成就描述-满级配方", "Any fractionation recipe reaches level 10", "任意分馏配方达到 10 级");

        Register("成就-符文收集者", "Rune Collector");
        Register("成就描述-符文收集者", "Own 5 runes", "拥有 5 个符文");
        Register("成就奖励-随机精华100", "Random Essence x100", "随机精华 x100");

        Register("成就-符文锻造师", "Rune Smith");
        Register("成就描述-符文锻造师", "Upgrade runes 50 times", "累计进行 50 次符文升级");
        Register("成就奖励-随机精华300", "Random Essence x300", "随机精华 x300");

        Register("成就-五星奇迹", "Five-Star Miracle");
        Register("成就描述-五星奇迹", "Own at least one 5-star rune", "拥有至少一个 5 星符文");
        Register("成就奖励-随机精华500", "Random Essence x500", "随机精华 x500");

        Register("成就-符文满槽", "Full Rune Slots");
        Register("成就描述-符文满槽", "Equip runes in all 5 slots", "5 个符文槽全部已装备");
        Register("成就奖励-宇宙奖券20", "Universe Tickets x20", "宇宙奖券 x20");

        Register("成就-符文满级", "Rune Grandmaster");
        Register("成就描述-符文满级", "Any rune reaches level 20", "任意符文达到 20 级");

        Register("成就-全面发展", "Balanced Growth");
        Register("成就描述-全面发展", "Meet production, draw, rune, recipe and building milestones", "同时满足分馏、抽奖、符文、配方、建筑的基础里程碑");

        Register("成就-万物归一", "All Into One");
        Register("成就描述-万物归一", "Reach top milestones in every major gameplay branch", "在主要玩法分支中同时达到高阶里程碑");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "成就详情");

        txtAchievementNames = new Text[achievements.Length];
        txtAchievementDescs = new Text[achievements.Length];
        txtAchievementStates = new Text[achievements.Length];
        btnClaims = new UIButton[achievements.Length];

        float x = 0f;
        float y = 18f + 7f;

        txtTitle = wnd.AddText2(x, y, tab, "成就系统", 17, "txtAchievementTitle");
        txtTitle.supportRichText = true;

        txtUnlockedSummary = wnd.AddText2(x + 235f, y, tab, "动态刷新", 14, "txtAchievementUnlockedSummary");
        txtUnlockedSummary.supportRichText = true;

        float achievementRowSpacing = 21f;
        y += 26f;
        txtHiddenSummary = wnd.AddText2(x, y, tab, "动态刷新", 14, "txtAchievementHiddenSummary");
        txtHiddenSummary.supportRichText = true;

        txtBonusSummary = wnd.AddText2(x + 235f, y, tab, "动态刷新", 14, "txtAchievementBonusSummary");
        txtBonusSummary.supportRichText = true;

        y += 30f;

        (float nameX, float nameW) = GetPosition(0, 4);
        (float descX, float descW) = GetPosition(1, 4);
        (float stateX, float stateW) = GetPosition(2, 4);
        (float actionX, float actionW) = GetPosition(3, 4);

        wnd.AddText2(nameX, y, tab, "成就", 14, "txtAchievementHeaderName");
        wnd.AddText2(descX, y, tab, "描述", 14, "txtAchievementHeaderDesc");
        wnd.AddText2(stateX, y, tab, "状态", 14, "txtAchievementHeaderState");
        wnd.AddText2(actionX, y, tab, "操作", 14, "txtAchievementHeaderAction");

        y += 26f;

        for (int i = 0; i < achievements.Length; i++) {
            int j = i;

            txtAchievementNames[j] = wnd.AddText2(nameX + x, y, tab, "动态刷新", 13, $"txtAchievementName{j}");
            txtAchievementNames[j].supportRichText = true;

            txtAchievementDescs[j] = wnd.AddText2(descX + x, y, tab, "动态刷新", 13, $"txtAchievementDesc{j}");
            txtAchievementDescs[j].supportRichText = true;

            txtAchievementStates[j] = wnd.AddText2(stateX + x, y, tab, "动态刷新", 13, $"txtAchievementState{j}");
            txtAchievementStates[j].supportRichText = true;

            btnClaims[j] = wnd.AddButton(actionX + x, y, actionW, tab, "领取", 13, $"btnAchievementClaim{j}",
                () => ClaimAchievementReward(j));

            y += achievementRowSpacing;
        }
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        for (int i = 0; i < achievements.Length; i++) {
            if (!unlocked[i] && achievements[i].Condition()) {
                unlocked[i] = true;
            }
        }

        int unlockedCount = unlocked.Count(v => v);
        int hiddenLockedCount = achievements.Length - unlockedCount;
        float successRateBonusPct = GetSuccessRateBonus() * 100f;

        txtUnlockedSummary.text = string.Format("已解锁成就".Translate(), unlockedCount, achievements.Length).WithColor(Orange);
        txtHiddenSummary.text = string.Format("隐藏未解锁".Translate(), hiddenLockedCount).WithColor(Blue);
        txtBonusSummary.text = string.Format("成就加成格式".Translate(), successRateBonusPct.ToString("0.##")).WithColor(Green);

        for (int i = 0; i < achievements.Length; i++) {
            RefreshAchievementRow(i);
        }
    }

    public static float GetSuccessRateBonus() {
        float bonus = 0f;
        for (int i = 0; i < achievements.Length; i++) {
            if (unlocked[i]) {
                bonus += achievements[i].SuccessRateBonus;
            }
        }
        return bonus;
    }

    private static void RefreshAchievementRow(int index) {
        if (!unlocked[index]) {
            txtAchievementNames[index].text = "隐藏成就提示".Translate().WithColor(new Color(0.65f, 0.65f, 0.65f, 1f));
            txtAchievementDescs[index].text = "隐藏成就描述".Translate().WithColor(new Color(0.65f, 0.65f, 0.65f, 1f));
            txtAchievementStates[index].text = "未解锁".Translate().WithColor(new Color(0.65f, 0.65f, 0.65f, 1f));
            btnClaims[index].button.interactable = false;
            btnClaims[index].SetText("隐藏成就提示");
            return;
        }

        AchievementInfo info = achievements[index];
        string tierTag = GetTierTag(info.Tier);
        Color tierColor = GetTierColor(info.Tier);
        string rewardText = info.RewardKey.Translate().WithColor(Blue);

        txtAchievementNames[index].text = $"{tierTag.WithColor(tierColor)} {info.NameKey.Translate()}";
        txtAchievementDescs[index].text = $"{info.DescKey.Translate()}  |  {"奖励".Translate()}：{rewardText}";

        bool alreadyClaimed = claimed[index];
        txtAchievementStates[index].text = alreadyClaimed
            ? "已领取".Translate().WithColor(Green)
            : "未领取".Translate().WithColor(Orange);

        bool canClaim = unlocked[index] && !alreadyClaimed;
        btnClaims[index].button.interactable = canClaim;
        btnClaims[index].SetText(canClaim ? "领取" : "已领取");
    }

    private static void ClaimAchievementReward(int index) {
        if (index < 0 || index >= achievements.Length) {
            return;
        }
        if (!unlocked[index] || claimed[index]) {
            return;
        }

        achievements[index].GrantReward?.Invoke();
        claimed[index] = true;
        RefreshAchievementRow(index);
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
            ETier.Bronze => new Color(0.80f, 0.50f, 0.20f),
            ETier.Silver => new Color(0.75f, 0.75f, 0.80f),
            ETier.Gold => new Color(1.00f, 0.84f, 0.00f),
            ETier.Platinum => new Color(0.90f, 0.90f, 1.00f),
            _ => Color.white,
        };
    }

    private static void GrantItems(params (int itemId, int count)[] rewards) {
        foreach ((int itemId, int count) in rewards) {
            AddItemToModData(itemId, count, 0, true);
            UIItemup.Up(itemId, count);
        }
    }

    private static void GrantRandomEssence(int count) {
        int essenceId = GetRandomEssenceId();
        AddItemToModData(essenceId, count, 0, true);
        UIItemup.Up(essenceId, count);
    }

    private static int GetRandomEssenceId() {
        int[] essenceIds = [IFE速度精华, IFE产能精华, IFE节能精华, IFE增产精华];
        return essenceIds[GetRandInt(0, essenceIds.Length)];
    }

    private static int GetUnlockedRecipeCount() {
        return Enum.GetValues(typeof(ERecipe)).Cast<ERecipe>()
            .SelectMany(type => GetRecipesByType(type))
            .Count(recipe => recipe.Unlocked);
    }

    private static bool HasMaxLevelRecipe() {
        return Enum.GetValues(typeof(ERecipe)).Cast<ERecipe>()
            .SelectMany(type => GetRecipesByType(type))
            .Any(recipe => recipe.IsMaxLevel);
    }

    private static int GetMaxBuildingLevel() {
        return Math.Max(InteractionTower.Level, Math.Max(MineralReplicationTower.Level,
            Math.Max(PointAggregateTower.Level, Math.Max(ConversionTower.Level, RecycleTower.Level))));
    }

    private static bool HasAnyTraitEnabled() {
        return InteractionTower.EnableSacrificeTrait
               || MineralReplicationTower.EnableMassEnergyFission
               || PointAggregateTower.EnableVoidSpray
               || ConversionTower.EnableCausalTracing;
    }

    private static bool HasLevel12BuildingWithTrait() {
        return (InteractionTower.Level >= 12 && InteractionTower.EnableSacrificeTrait)
               || (MineralReplicationTower.Level >= 12 && MineralReplicationTower.EnableMassEnergyFission)
               || (PointAggregateTower.Level >= 12 && PointAggregateTower.EnableVoidSpray)
               || (ConversionTower.Level >= 12 && ConversionTower.EnableCausalTracing);
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        Array.Clear(unlocked, 0, unlocked.Length);
        Array.Clear(claimed, 0, claimed.Length);

        r.ReadBlocks(
            ("UnlockedFlags", br => {
                int count = br.ReadInt32();
                for (int i = 0; i < Math.Min(count, achievements.Length); i++) {
                    unlocked[i] = br.ReadBoolean();
                }
                for (int i = achievements.Length; i < count; i++) {
                    br.ReadBoolean();
                }
            }),
            ("ClaimedFlags", br => {
                int count = br.ReadInt32();
                for (int i = 0; i < Math.Min(count, achievements.Length); i++) {
                    claimed[i] = br.ReadBoolean();
                }
                for (int i = achievements.Length; i < count; i++) {
                    br.ReadBoolean();
                }
            })
        );

        for (int i = 0; i < achievements.Length; i++) {
            if (claimed[i] && !unlocked[i]) {
                claimed[i] = false;
            }
        }
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("UnlockedFlags", bw => {
                bw.Write(achievements.Length);
                foreach (bool value in unlocked) {
                    bw.Write(value);
                }
            }),
            ("ClaimedFlags", bw => {
                bw.Write(achievements.Length);
                foreach (bool value in claimed) {
                    bw.Write(value);
                }
            })
        );
    }

    public static void IntoOtherSave() {
        Array.Clear(unlocked, 0, unlocked.Length);
        Array.Clear(claimed, 0, claimed.Length);
    }

    #endregion
}
