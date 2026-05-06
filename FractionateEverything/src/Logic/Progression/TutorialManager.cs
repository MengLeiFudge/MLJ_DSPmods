using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Reflection.Emit;
using FE.Compatibility.Mods;
using FE.UI.MainPanel.ProgressTask;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using xiaoye97;
using static FE.Utils.Utils;

namespace FE.Logic.Progression;
/// <summary>
/// FE 教程系统的状态、解锁和阅读奖励管理。
/// </summary>
public static partial class TutorialManager {
    /// <summary>
    /// 教程阅读奖励使用的成就档位。
    /// </summary>
    public enum TutorialAchievementTier {
        Bronze,
        Silver,
        Gold,
        Platinum,
    }
    /// <summary>
    /// 教程成就定义。
    /// </summary>
    public readonly struct TutorialAchievementDefinition(
        int tutorialId,
        string nameKey,
        string nameEn,
        string nameCn,
        string descKey,
        string descEn,
        string descCn,
        string rewardKey,
        TutorialAchievementTier tier) {
        public readonly int TutorialId = tutorialId;
        public readonly string NameKey = nameKey;
        public readonly string NameEn = nameEn;
        public readonly string NameCn = nameCn;
        public readonly string DescKey = descKey;
        public readonly string DescEn = descEn;
        public readonly string DescCn = descCn;
        public readonly string RewardKey = rewardKey;
        public readonly TutorialAchievementTier Tier = tier;
    }
    /// <summary>
    /// 教程注册项定义。
    /// </summary>
    private readonly struct TutorialRegistration(
        int id,
        string baseName,
        string englishTitle,
        string determinatorName,
        long[] determinatorParams,
        bool enableAchievement = true,
        string achievementRewardKey = DefaultTutorialAchievementRewardKey,
        TutorialAchievementTier achievementTier = DefaultTutorialAchievementTier,
        string achievementNameKey = null,
        string achievementNameEn = null,
        string achievementNameCn = null,
        string achievementDescKey = null,
        string achievementDescEn = null,
        string achievementDescCn = null) {
        public readonly int Id = id;
        public readonly string BaseName = baseName;
        public readonly string EnglishTitle = englishTitle;
        public readonly string DeterminatorName = determinatorName;
        public readonly long[] DeterminatorParams = determinatorParams;
        public readonly bool EnableAchievement = enableAchievement;
        public readonly string AchievementRewardKey = achievementRewardKey;
        public readonly TutorialAchievementTier AchievementTier = achievementTier;
        public readonly string AchievementNameKey = achievementNameKey ?? $"成就-指引通读-{baseName}";
        public readonly string AchievementNameEn = achievementNameEn ?? $"Guide Complete: {englishTitle}";
        public readonly string AchievementNameCn = achievementNameCn ?? $"指引通读：{baseName}";
        public readonly string AchievementDescKey = achievementDescKey ?? $"成就描述-指引通读-{baseName}";
        public readonly string AchievementDescEn =
            achievementDescEn ?? $"Open the [G]-key guide, read '{englishTitle}', and scroll to the bottom.";
        public readonly string AchievementDescCn =
            achievementDescCn ?? $"在 G 键指引中浏览《{baseName}》并看到底部";
    }

    private const int FirstTutorialId = 201;
    private const string FeTutorialLayoutPrefix = "tutorial-fe-";
    private const string DefaultTutorialAchievementRewardKey = "成就奖励-残片200";
    private const TutorialAchievementTier DefaultTutorialAchievementTier = TutorialAchievementTier.Bronze;

    private static readonly TutorialRegistration[] tutorialRegistrations = BuildTutorialRegistrations();
    private static readonly TutorialAchievementDefinition[] tutorialAchievementDefinitions =
        BuildTutorialAchievementDefinitions();
    private static readonly HashSet<int> tutorialAchievementIds = BuildTutorialAchievementIds();


    private static TutorialRegistration[] BuildTutorialRegistrations() {
        int nextId = FirstTutorialId;
        return [
            new(nextId++, "万物分馏简介", "Fractionate Everything", "TOR_GameSecond", [10]),
            new(nextId++, "分馏数据中心", "Fractionation data centre", "TOR_TechUnlocked", [TFE分馏数据中心, 4]),
            new(nextId++, "分馏塔使用指南", "Fractionator guidelines", "TOR_OnBuild", [IFE交互塔, IFE矿物复制塔, IFE点数聚集塔, IFE转化塔]),
            new(nextId++, "物流交互站使用指南", "Interaction station guidelines", "TOR_OnBuild", [IFE行星内物流交互站, IFE星际物流交互站]),
        ];
    }

    private static TutorialAchievementDefinition[] BuildTutorialAchievementDefinitions() {
        var definitions = new List<TutorialAchievementDefinition>(tutorialRegistrations.Length);
        foreach (TutorialRegistration registration in tutorialRegistrations) {
            if (!registration.EnableAchievement) {
                continue;
            }

            definitions.Add(new TutorialAchievementDefinition(
                registration.Id,
                registration.AchievementNameKey,
                registration.AchievementNameEn,
                registration.AchievementNameCn,
                registration.AchievementDescKey,
                registration.AchievementDescEn,
                registration.AchievementDescCn,
                registration.AchievementRewardKey,
                registration.AchievementTier));
        }
        return [.. definitions];
    }

    private static HashSet<int> BuildTutorialAchievementIds() {
        var ids = new HashSet<int>();
        foreach (TutorialAchievementDefinition definition in tutorialAchievementDefinitions) {
            ids.Add(definition.TutorialId);
        }
        return ids;
    }

    public static IReadOnlyList<TutorialAchievementDefinition> GetTutorialAchievementDefinitions() {
        return tutorialAchievementDefinitions;
    }

    public static bool HasViewedTutorialToBottom(int tutorialId) {
        return viewedToBottomTutorialIds.Contains(tutorialId);
    }
}
