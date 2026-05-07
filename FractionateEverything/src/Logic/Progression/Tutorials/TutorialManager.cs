using System.Collections.Generic;
using FE.UI.MainPanel.ProgressTask;
using UnityEngine;
using static FE.Utils.Utils;
using xiaoye97;

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
    private static readonly HashSet<int> viewedToBottomTutorialIds = [];
    private static readonly Vector3[] viewportWorldCorners = new Vector3[4];
    private static readonly Vector3[] contentWorldCorners = new Vector3[4];

    public static void AddTutorials() {
        // DeterminatorName：解锁时机；DeterminatorParams：解锁参数。
        // TOR_GameSecond: 参数为[伊卡洛斯落地后第几秒]。
        // TOR_TechUnlocked: 参数为[科技ID, 科技研究完成后再等待几秒]。
        // TOR_OnBuild: 参数为[建筑1ID, 建筑2ID, ...]，任一建筑建造都会提示。
        foreach (TutorialRegistration registration in tutorialRegistrations) {
            AddTutorial(registration);
        }
    }

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

    private static void AddTutorial(TutorialRegistration registration) {
        TutorialProto proto = new() {
            ID = registration.Id,
            SID = "",
            Name = $"{registration.BaseName}标题",
            name = $"{registration.BaseName}标题",
            LayoutFileName = $"{FeTutorialLayoutPrefix}{registration.Id}",
            DeterminatorName = registration.DeterminatorName,
            DeterminatorParams = registration.DeterminatorParams,
        };
        LDBTool.PreAddProto(proto);
        proto.Preload();
    }

    private static void TryMarkCurrentTutorialViewedToBottom(UITutorialWindow tutorialWindow) {
        if (tutorialWindow?.tutorialProto == null) {
            return;
        }

        int tutorialId = tutorialWindow.tutorialProto.ID;
        if (!tutorialAchievementIds.Contains(tutorialId) || viewedToBottomTutorialIds.Contains(tutorialId)) {
            return;
        }

        if (!HasReachedTutorialBottom(tutorialWindow)) {
            return;
        }

        if (viewedToBottomTutorialIds.Add(tutorialId)) {
            Achievements.NotifyExternalConditionChanged();
        }
    }

    private static bool HasReachedTutorialBottom(UITutorialWindow tutorialWindow) {
        if (tutorialWindow.scrollViewRect == null || tutorialWindow.contentRect == null) {
            return false;
        }

        float viewportHeight = tutorialWindow.scrollViewRect.rect.height;
        float contentHeight = Mathf.Max(tutorialWindow.contentRect.rect.height, tutorialWindow.contentRect.sizeDelta.y);
        if (contentHeight <= viewportHeight + 1f) {
            return true;
        }

        // 通过内容区与视口的世界坐标关系判断是否滚到底部，不依赖 prefab 是否暴露 ScrollRect 字段。
        tutorialWindow.scrollViewRect.GetWorldCorners(viewportWorldCorners);
        tutorialWindow.contentRect.GetWorldCorners(contentWorldCorners);
        float viewportBottom = viewportWorldCorners[0].y;
        float contentBottom = contentWorldCorners[0].y;
        return contentBottom >= viewportBottom - 1f;
    }
}
