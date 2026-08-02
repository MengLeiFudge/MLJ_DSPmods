using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using FE.Logic.Civilization.Achievements;
using FE.Logic.Fractionation.Recipes;
using FE.UI.Foundation.Window;
using FE.UI.Layout;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Layout.GridDsl;
using static FE.Utils.Utils;
using FEAchievementState = FE.Logic.Civilization.Achievements.AchievementState;

namespace FE.UI.MainPanel.ProgressTask;

/// <summary>
/// 切换查看普通成就（单存档里程碑与固定奖励）与秘密成就（仅本机已揭示的纪念项，无生产奖励）。
/// </summary>
public static class AchievementPage {
    private enum View {
        Normal,
        Secret,
    }

    private const float BodyBaseHeight = 360f;
    private const float SecretLineHeight = 64f;

    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static UIButton normalViewButton;
    private static UIButton secretViewButton;
    private static Text bodyTitleText;
    private static Text bodyText;
    private static Text footerText;
    private static View currentView = View.Normal;

    public static void AddTranslations() {
        Register("成就", "Achievements");
        Register("成就摘要", "Achievements are fixed single-save milestones and do not consume technology points.",
            "成就是单存档固定里程碑，不消耗科技点，完成后自动生效。");
        Register("成就-普通视图", "Normal", "普通成就");
        Register("成就-秘密视图", "Secret", "秘密成就");
        Register("成就-已完成", "Completed", "已完成");
        Register("成就-全部配方成功率", "All recipe success rate", "全部配方成功率");
        Register("成就-成功率后缀", " success rate", "成功率");
        Register("成就-秘密空态", "No secret achievement has been revealed on this machine yet.",
            "尚未在本机揭示任何秘密成就。");
        Register("成就-普通空态", "No normal achievement is registered.", "暂无普通成就。");
        Register("成就-秘密页脚", "Secret achievements are kept locally across saves and grant no production rewards.",
            "秘密成就在本机跨存档保留，不提供任何生产奖励。");
        Register("成就-普通页脚", "Rewards are projected onto the fractionation hot path by the runtime cache.",
            "奖励由运行缓存统一投影到分馏热路径。");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        tab = trans;
        float contentHeight =
            Mathf.Max(BodyBaseHeight, SecretAchievementCatalog.GetVisible().Count * SecretLineHeight + 80f);
        BuildLayout(wnd, tab,
            Grid(rows: [Px(PageLayout.HeaderHeight), Px(40f), 1, Px(PageLayout.FooterHeight)],
                rowGap: PageLayout.Gap,
                children: [
                    Header("成就", "成就摘要", pos: (0, 0), objectName: "achievement-header",
                        onBuilt: refs => header = refs),
                    Grid(pos: (1, 0), cols: [1, 1], columnGap: 12f, objectName: "achievement-toggle",
                        children: [
                            ButtonNode("成就-普通视图", SwitchToNormal, pos: (0, 0),
                                onBuilt: button => normalViewButton = button,
                                objectName: "achievement-normal-toggle"),
                            ButtonNode("成就-秘密视图", SwitchToSecret, pos: (0, 1),
                                onBuilt: button => secretViewButton = button,
                                objectName: "achievement-secret-toggle"),
                        ]),
                    ContentCard(pos: (2, 0), objectName: "achievement-card", strong: true,
                        rows: [Px(28f), 1], rowGap: 8f,
                        children: [
                            CardTitleNode("", pos: (0, 0),
                                onBuilt: text => {
                                    bodyTitleText = text;
                                    text.supportRichText = true;
                                }, objectName: "achievement-card-title"),
                            ScrollableContentCard(contentHeight, pos: (1, 0), objectName: "achievement-body-scroll",
                                children: [
                                    TextNode("", 13, White, anchor: TextAnchor.UpperLeft, wrap: true, pos: (0, 0),
                                        onBuilt: text => {
                                            bodyText = text;
                                            text.supportRichText = true;
                                        }, objectName: "achievement-body-text"),
                                ]),
                        ]),
                    FooterCard(pos: (3, 0), objectName: "achievement-footer", children: [
                        TextNode("", 12, Gray, wrap: true, pos: (0, 0),
                            onBuilt: text => footerText = text,
                            objectName: "achievement-footer-text"),
                    ]),
                ]));
        UpdateUI();
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        header.Title.text = "成就".Translate().WithColor(Orange);
        header.Summary.text = "成就摘要".Translate().WithColor(White);
        bodyTitleText.text = (currentView == View.Normal ? "成就-普通视图" : "成就-秘密视图")
            .Translate().WithColor(Orange);
        normalViewButton.button.interactable = currentView != View.Normal;
        secretViewButton.button.interactable = currentView != View.Secret;
        bodyText.text = currentView == View.Normal ? BuildNormalBody() : BuildSecretBody();
        footerText.text = BuildFooter().WithColor(Gray);
    }

    private static string BuildNormalBody() {
        var builder = new StringBuilder();
        for (int i = 0; i < AchievementCatalog.All.Count; i++) {
            AchievementDefinition definition = AchievementCatalog.All[i];
            bool done = FEAchievementState.IsCompleted(definition.AchievementKey);
            long current = AchievementService.GetCurrentValue(definition);
            string name = definition.DisplayNameKey.Translate().WithColor(done ? Green : Orange);
            string progress = done
                ? "成就-已完成".Translate().WithColor(Green)
                : $"{current}/{definition.Target}";
            string reward = FormatReward(definition);
            if (i > 0) {
                builder.Append('\n');
            }
            builder.Append(i + 1).Append(". ").Append(name).Append("  ").Append(progress)
                .Append('\n').Append("   ").Append(reward);
        }
        if (AchievementCatalog.All.Count == 0) {
            builder.Append("成就-普通空态".Translate().WithColor(Gray));
        }
        return builder.ToString();
    }

    private static string BuildSecretBody() {
        IReadOnlyList<SecretAchievementDefinition> visible = SecretAchievementCatalog.GetVisible();
        if (visible.Count == 0) {
            return "成就-秘密空态".Translate().WithColor(Gray);
        }

        var builder = new StringBuilder();
        for (int i = 0; i < visible.Count; i++) {
            SecretAchievementDefinition definition = visible[i];
            if (i > 0) {
                builder.Append('\n');
            }
            builder.Append("• ").Append(definition.DisplayNameKey.Translate().WithColor(Purple))
                .Append('\n').Append("   ").Append(definition.DescriptionKey.Translate().WithColor(White));
        }
        return builder.ToString();
    }

    private static string BuildFooter() {
        if (currentView == View.Secret) {
            return "成就-秘密页脚".Translate();
        }

        int completed = 0;
        foreach (AchievementDefinition definition in AchievementCatalog.All) {
            if (FEAchievementState.IsCompleted(definition.AchievementKey)) {
                completed++;
            }
        }
        int total = AchievementCatalog.All.Count;
        return $"{completed}/{total}  ·  {"成就-普通页脚".Translate()}";
    }

    private static string FormatReward(AchievementDefinition definition) {
        string rate = $" +{definition.RewardValue:P1}";
        if (definition.RewardType == AchievementRewardType.AllRecipeSuccessRate) {
            return "成就-全部配方成功率".Translate() + rate;
        }
        return definition.RecipeType.GetShortName() + "成就-成功率后缀".Translate() + rate;
    }

    private static void SwitchToNormal() {
        currentView = View.Normal;
        UpdateUI();
    }

    private static void SwitchToSecret() {
        currentView = View.Secret;
        UpdateUI();
    }
}
