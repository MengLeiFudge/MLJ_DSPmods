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
    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        SyncCurrentPageFromSharedState();
        window = trans;
        tab = trans;
        BuildLayout(wnd, tab,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), Px(82f), 1, Px(PageLayout.FooterHeight)],
                rowGap: PageLayout.Gap,
                children: [
                    Header("成就系统", objectName: "achievements-header", pos: (0, 0), onBuilt: refs => {
                        header = refs;
                        txtTitle = refs.Title;
                    }),
                    ContentCard(
                        pos: (1, 0),
                        objectName: "achievements-summary-card",
                        strong: true,
                        rows: [1, 1],
                        cols: [1, 2],
                        rowGap: 4f,
                        columnGap: PageLayout.InnerGap,
                        children: [
                            TextNode("动态刷新", 13, onBuilt: text => txtUnlockedSummary = text,
                                pos: (0, 0), objectName: "txtAchievementUnlockedSummary"),
                            TextNode("动态刷新", 13, onBuilt: text => txtHiddenSummary = text,
                                pos: (1, 0), objectName: "txtAchievementHiddenSummary"),
                            TextNode("动态刷新", 13, anchor: TextAnchor.UpperLeft, wrap: true,
                                onBuilt: text => txtBonusSummary = text,
                                pos: (0, 1), span: (2, 1), objectName: "txtAchievementBonusSummary"),
                        ]),
                    ContentCard(
                        pos: (2, 0),
                        objectName: "achievements-list-card",
                        rows: BuildAchievementListRows(),
                        cols: [Fr(220), Fr(460), Px(42f), Fr(120), Fr(180)],
                        rowGap: 6f,
                        columnGap: 8f,
                        children: BuildAchievementListNodes()),
                    FooterCard(
                        pos: (3, 0),
                        objectName: "achievements-footer-card",
                        cols: [1, 1, 1],
                        columnGap: PageLayout.InnerGap,
                        children: [
                            ButtonNode("上一页", onClick: PrevPage, onBuilt: btn => btnPrevPage = btn,
                                pos: (0, 0), objectName: "achievements-prev-page"),
                            TextNode("", anchor: TextAnchor.MiddleCenter, onBuilt: text => txtPageIndicator = text,
                                pos: (0, 1), objectName: "achievements-page-indicator"),
                            ButtonNode("下一页", onClick: NextPage, onBuilt: btn => btnNextPage = btn,
                                pos: (0, 2), objectName: "achievements-next-page"),
                        ]),
                ]));
    }

    private static IReadOnlyList<LayoutTrack> BuildAchievementListRows() {
        var rows = new List<LayoutTrack> { Px(28f) };
        for (int i = 0; i < RowsPerPage; i++) {
            rows.Add(1);
        }

        return rows;
    }

    private static IReadOnlyList<LayoutNode> BuildAchievementListNodes() {
        txtAchievementNames = new Text[RowsPerPage];
        txtAchievementDescs = new Text[RowsPerPage];
        txtAchievementRewards = new Text[RowsPerPage];
        txtAchievementStates = new Text[RowsPerPage];
        rewardIcons = new MyImageButton[RowsPerPage];

        var nodes = new List<LayoutNode> {
            TextNode("成就", 15, pos: (0, 0), objectName: "txtAchievementHeaderName"),
            TextNode("描述", 15, pos: (0, 1), objectName: "txtAchievementHeaderDesc"),
            TextNode("奖励", 15, pos: (0, 2), span: (1, 2), objectName: "txtAchievementHeaderReward"),
            TextNode("状态", 15, pos: (0, 4), objectName: "txtAchievementHeaderState"),
        };
        for (int i = 0; i < RowsPerPage; i++) {
            int slot = i;
            int row = i + 1;
            nodes.Add(TextNode("动态刷新", 13, wrap: true,
                onBuilt: text => txtAchievementNames[slot] = text,
                pos: (row, 0), objectName: $"txtAchievementName{slot}"));
            nodes.Add(TextNode("动态刷新", 13, anchor: TextAnchor.UpperLeft, wrap: true,
                onBuilt: text => txtAchievementDescs[slot] = text,
                pos: (row, 1), objectName: $"txtAchievementDesc{slot}"));
            nodes.Add(ImageButtonNode(size: 40f, onBuilt: btn => rewardIcons[slot] = btn,
                pos: (row, 2), objectName: $"txtAchievementRewardIcon{slot}"));
            nodes.Add(TextNode("动态刷新", 13, onBuilt: text => txtAchievementRewards[slot] = text,
                pos: (row, 3), objectName: $"txtAchievementReward{slot}"));
            nodes.Add(TextNode("动态刷新", 13, onBuilt: text => txtAchievementStates[slot] = text,
                pos: (row, 4), objectName: $"txtAchievementState{slot}"));
        }

        return nodes;
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
        EnsureBonusSummaryCache();
        int obtainedCount = cachedBonusSummary.ObtainedCount;
        int hiddenLockedCount = achievements.Length - obtainedCount;

        txtTitle.text = "成就系统".Translate().WithColor(Orange);
        txtUnlockedSummary.text =
            string.Format("已获得成就".Translate(), obtainedCount, achievements.Length).WithColor(Orange);
        txtHiddenSummary.text = string.Format("隐藏未解锁".Translate(), hiddenLockedCount).WithColor(Blue);
        txtBonusSummary.text = string.Format("成就加成格式".Translate(),
            cachedBonusSummary.SuccessRateBonus * 100f,
            cachedBonusSummary.DestroyReductionBonus * 100f,
            cachedBonusSummary.DoubleOutputBonus * 100f,
            cachedBonusSummary.EnergyReductionBonus * 100f,
            cachedBonusSummary.LogisticsBonus * 100f,
            cachedBonusSummary.PowerStageBonus * 100f).WithColor(Green);

        int totalPages = Math.Max(1, (achievements.Length + RowsPerPage - 1) / RowsPerPage);
        if (currentPage >= totalPages) {
            currentPage = totalPages - 1;
            SyncCurrentPageToSharedState();
        }

        for (int i = 0; i < RowsPerPage; i++) {
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
            txtAchievementNames[slot].gameObject.SetActive(true);
            txtAchievementDescs[slot].gameObject.SetActive(true);
            txtAchievementRewards[slot].gameObject.SetActive(true);
            txtAchievementStates[slot].gameObject.SetActive(true);

            RefreshAchievementRow(i, slot);
        }

        UpdatePagination(totalPages);
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

    private static void RefreshAchievementRow(int index, int slot) {
        AchievementInfo info = achievements[index];
        Color tierColor = GetTierColor(info.Tier);
        if (!claimed[index]) {
            txtAchievementNames[slot].text =
                $"[{info.CategoryKey.Translate()}] {info.NameKey.Translate().WithColor(tierColor)}";
            txtAchievementDescs[slot].text = GetDisplayedDesc(info);
            rewardIcons[slot].gameObject.SetActive(false);
            txtAchievementRewards[slot].text = GetFunctionalRewardText(info);
            txtAchievementStates[slot].text = "未解锁".Translate().WithColor(Gray);
            return;
        }

        txtAchievementNames[slot].text =
            $"[{info.CategoryKey.Translate()}] {info.NameKey.Translate().WithColor(tierColor)}";
        txtAchievementDescs[slot].text = GetDisplayedDesc(info);
        rewardIcons[slot].gameObject.SetActive(false);
        txtAchievementRewards[slot].text = GetFunctionalRewardText(info);

        txtAchievementStates[slot].text = "已获得".Translate().WithColor(Green);
    }

    private static string GetDisplayedDesc(AchievementInfo info) => info.DescKey.Translate();
}
