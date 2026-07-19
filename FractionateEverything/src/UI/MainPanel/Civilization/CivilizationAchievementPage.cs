using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Logic.Civilization.Achievements;
using FE.Logic.Fractionation.FracRecipes;
using FE.UI.Foundation.Window;
using FE.UI.Layout;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Layout.GridDsl;
using static FE.Utils.Utils;
using CivilizationAchievementState = FE.Logic.Civilization.Achievements.AchievementState;

namespace FE.UI.MainPanel.Civilization;

/// <summary>
/// 展示单存档自动完成的文明成就及其固定效果。
/// </summary>
public static class CivilizationAchievementPage {
    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static readonly Text[] achievementTexts = new Text[4];
    private static Text footerText;

    public static void AddTranslations() {
        Register("文明成就", "Civilization Achievements");
        Register("文明成就摘要", "Achievements are fixed single-save milestones and do not consume technology points.",
            "成就是单存档固定里程碑，不消耗科技点，完成后自动生效。");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        tab = trans;
        BuildLayout(wnd, tab,
            Grid(rows: [Px(PageLayout.HeaderHeight), 1, Px(PageLayout.FooterHeight)], rowGap: PageLayout.Gap,
                children: [
                    Header("文明成就", "文明成就摘要", pos: (0, 0), objectName: "civilization-achievement-header",
                        onBuilt: refs => header = refs),
                    ContentCard(pos: (1, 0), objectName: "civilization-achievement-card", strong: true,
                        rows: [1, 1, 1, 1], rowGap: 8f, children: BuildAchievementNodes()),
                    FooterCard(pos: (2, 0), objectName: "civilization-achievement-footer", children: [
                        TextNode("", 12, Gray, wrap: true, pos: (0, 0),
                            onBuilt: text => footerText = text,
                            objectName: "civilization-achievement-footer-text"),
                    ]),
                ]));
        UpdateUI();
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }
        header.Title.text = "文明成就".Translate().WithColor(Orange);
        header.Summary.text = "文明成就摘要".Translate().WithColor(White);
        int completeCount = 0;
        for (int i = 0; i < achievementTexts.Length && i < AchievementCatalog.All.Count; i++) {
            AchievementDefinition definition = AchievementCatalog.All[i];
            bool completed = CivilizationAchievementState.IsCompleted(definition.AchievementKey);
            if (completed) completeCount++;
            long current = AchievementService.GetCurrentValue(definition);
            string reward = definition.RewardType == AchievementRewardType.AllRecipeSuccessRate
                ? $"全部配方成功率 +{definition.RewardValue:P1}"
                : $"{definition.RecipeType.GetShortName()}成功率 +{definition.RewardValue:P1}";
            achievementTexts[i].text =
                $"{definition.DisplayNameKey.Translate().WithColor(completed ? Green : Orange)}  "
                + $"{(completed ? "已完成" : $"{current}/{definition.Target}")}\n{reward}";
        }
        footerText.text = $"已完成 {completeCount}/{AchievementCatalog.All.Count}。奖励由运行缓存统一投影到分馏热路径。";
    }

    private static LayoutNode[] BuildAchievementNodes() {
        var nodes = new LayoutNode[achievementTexts.Length];
        for (int i = 0; i < nodes.Length; i++) {
            int index = i;
            nodes[i] = TextNode("", 13, White, anchor: TextAnchor.MiddleLeft, wrap: true, pos: (i, 0),
                onBuilt: text => {
                    achievementTexts[index] = text;
                    text.supportRichText = true;
                }, objectName: $"civilization-achievement-{i}");
        }
        return nodes;
    }
}
