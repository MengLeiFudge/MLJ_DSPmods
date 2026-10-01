using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using FE.Compatibility.Nebula;
using FE.Logic.Civilization;
using FE.Logic.Fractionation.Recipes;
using FE.UI.Foundation;
using FE.UI.Foundation.Window;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Layout.GridDsl;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>
/// 以进度、解析方向和科技档案三个层次展示生命工业研究状态。
/// </summary>
public static class CivilizationOverviewPage {
    private const float DirectionLineHeight = 26f;

    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static Text progressHeadlineText;
    private static Text exploredMetricText;
    private static Text resolvedMetricText;
    private static Image exploredProgressFill;
    private static Image resolvedProgressFill;
    private static Text directionListText;
    private static RectTransform directionListRoot;
    private static Text directionDetailText;
    private static Text technologySummaryText;
    private static Text technologyArchiveText;
    private static RectTransform technologyArchiveRoot;
    private static Text footerText;
    private static UIButton directionButton;
    private static int selectedDirectionItemId;

    /// <summary>
    /// 注册文明研究页面文本。
    /// </summary>
    public static void AddTranslations() {
        Register("文明研究", "Civilization Research");
        Register("文明研究摘要",
            "Track exploration and analysis progress, carrier-item slots, and the recovered technology archive.",
            "跟踪探索与解析进度、承载物品共享槽，以及已经恢复的科技与特质档案。");
        Register("研究档案", "Research Archive");
        Register("研究已探索", "Explored");
        Register("研究已解析", "Resolved");
        Register("解析方向", "Analysis Directions");
        Register("切换研究方向", "Next Direction");
        Register("解析状态", "Analysis Status");
        Register("等待解析", "Awaiting Analysis");
        Register("等待新探索", "Awaiting Exploration");
        Register("当前进度", "Current Progress");
        Register("方向内容", "Pending Content", "待解析内容");
        Register("仍有未知内容", "Undiscovered content remains");
        Register("方向已完成", "Direction complete");
        Register("研究方向为空", "No research directions are currently available.");
        Register("科技与特质档案", "Technology & Trait Archive");
        Register("档案发现进度", "Found {0}/{1}", "已找到 {0}/{1}");
        Register("未知研究内容", "???", "？？？");
        Register("已解析", "Resolved");
        Register("高级塔收集", "Advanced Towers");
        Register("传承准备", "Legacy Readiness");
        Register("已完成", "Complete");
        Register("未完成", "Incomplete");
        Register("研究页脚",
            "Exploration reveals concrete content. Analyse its carrier item to resolve it; discovered recipes are catalogued on the separate Recipe Gallery page.",
            "探索会发现具体内容；解析其承载物品即可恢复内容，已发现配方统一收录在独立的“配方图鉴”页面。");
        Register("研究客户端说明", "Research state is settled and synchronized by the host.",
            "研究状态由主机结算并同步。");
    }

    /// <summary>
    /// 研究页面没有独立配置。
    /// </summary>
    public static void LoadConfig(ConfigFile configFile) { }

    /// <summary>
    /// 创建进度总览、解析方向和科技档案区域。
    /// </summary>
    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        tab = trans;
        ResearchDirectionSnapshot[] directions = ResearchManager.GetDirectionSnapshots();
        float directionHeight = Math.Max(280f, directions.Length * DirectionLineHeight + 20f);

        BuildLayout(wnd, tab,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), Px(112f), 1, Px(PageLayout.FooterHeight)],
                rowGap: PageLayout.Gap,
                children: [
                    Header("文明研究", "文明研究摘要", pos: (0, 0), objectName: "research-header",
                        onBuilt: refs => header = refs),
                    ContentCard(pos: (1, 0), objectName: "research-progress-card", strong: true,
                        rows: [1, 1], cols: [Px(250f), 1], rowGap: 8f, columnGap: 24f,
                        children: [
                            TextNode("", 18, White, anchor: TextAnchor.MiddleLeft, wrap: true,
                                pos: (0, 0), span: (2, 1), onBuilt: text => {
                                    progressHeadlineText = text;
                                    text.supportRichText = true;
                                }, objectName: "research-progress-headline"),
                            Node(pos: (0, 1), objectName: "research-explored-progress",
                                build: (_, root) => BuildProgressMetric(root, out exploredMetricText,
                                    out exploredProgressFill)),
                            Node(pos: (1, 1), objectName: "research-resolved-progress",
                                build: (_, root) => BuildProgressMetric(root, out resolvedMetricText,
                                    out resolvedProgressFill, new Color(0.45f, 0.85f, 0.48f, 0.92f))),
                        ]),
                    Grid(pos: (2, 0), cols: [Fr(1.08f), 1], columnGap: PageLayout.Gap,
                        objectName: "research-body", children: [
                            ContentCard(pos: (0, 0), objectName: "research-direction-card", strong: true,
                                rows: [Px(28f), Px(72f), Px(36f), 1], rowGap: 8f, children: [
                                    CardTitleNode("解析方向", pos: (0, 0),
                                        objectName: "research-direction-title"),
                                    TextNode("", 13, White, anchor: TextAnchor.UpperLeft, wrap: true,
                                        pos: (1, 0), onBuilt: text => {
                                            directionDetailText = text;
                                            text.supportRichText = true;
                                        }, objectName: "research-direction-detail"),
                                    ButtonNode("切换研究方向", CycleDirection, pos: (2, 0),
                                        onBuilt: button => directionButton = button,
                                        objectName: "research-cycle-direction"),
                                    ScrollableArea(directionHeight, pos: (3, 0),
                                        objectName: "research-direction-scroll",
                                        onBuilt: root => directionListRoot = root, children: [
                                            TextNode("", 13, White, anchor: TextAnchor.UpperLeft, wrap: true,
                                                pos: (0, 0), onBuilt: text => {
                                                    directionListText = text;
                                                    text.supportRichText = true;
                                                }, objectName: "research-direction-list"),
                                        ]),
                                ]),
                            ContentCard(pos: (0, 1), objectName: "research-technology-card",
                                rows: [Px(28f), Px(28f), 1], rowGap: 8f, children: [
                                    CardTitleNode("科技与特质档案", pos: (0, 0),
                                        objectName: "research-technology-title"),
                                    TextNode("", 13, Gray, anchor: TextAnchor.MiddleLeft,
                                        pos: (1, 0), onBuilt: text => {
                                            technologySummaryText = text;
                                            text.supportRichText = true;
                                        }, objectName: "research-technology-summary"),
                                    ScrollableArea(360f, pos: (2, 0),
                                        objectName: "research-technology-scroll",
                                        onBuilt: root => technologyArchiveRoot = root, children: [
                                            TextNode("", 13, White, anchor: TextAnchor.UpperLeft, wrap: true,
                                                pos: (0, 0), onBuilt: text => {
                                                    technologyArchiveText = text;
                                                    text.supportRichText = true;
                                                }, objectName: "research-technology-list"),
                                        ]),
                                ]),
                        ]),
                    FooterCard(pos: (3, 0), objectName: "research-footer", children: [
                        TextNode("", 12, Gray, wrap: true, pos: (0, 0), onBuilt: text => {
                            footerText = text;
                            text.supportRichText = true;
                        }, objectName: "research-footer-text"),
                    ]),
                ]));
        UpdateUI();
    }

    /// <summary>
    /// 刷新研究快照，不在页面层执行任何研究结算。
    /// </summary>
    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        ResearchContentDefinition[] catalog = ResearchManager.GetCatalogContentDefinitions();
        ResearchContentDefinition[] known = ResearchManager.GetKnownContentDefinitions();
        ResearchContentDefinition[] resolved = ResearchManager.GetUnlockedContentDefinitions();
        var knownIds = new HashSet<string>(known.Select(definition => definition.Id));
        var resolvedIds = new HashSet<string>(resolved.Select(definition => definition.Id));
        ResearchDirectionSnapshot[] directions = ResearchManager.GetDirectionSnapshots();
        SynchronizeSelectedDirection(directions);

        header.Title.text = "文明研究".Translate().WithColor(Orange);
        header.Summary.text = "文明研究摘要".Translate().WithColor(White);
        UpdateProgress(catalog.Length, known.Length, resolved.Length);

        directionDetailText.text = BuildDirectionDetail(directions);
        directionListText.text = BuildDirectionList(directions);
        PageLayout.ResizeScrollableText(directionListRoot, directionListText);
        directionButton.button.interactable = directions.Length > 1;
        directionButton.SetText("切换研究方向".Translate());

        ResearchContentDefinition[] technologyDefinitions = catalog
            .Where(definition => RecipeManager.GetRecipe(definition.Id) == null)
            .ToArray();
        int knownTechnologyCount = technologyDefinitions.Count(definition => knownIds.Contains(definition.Id));
        technologySummaryText.text = string.Format("档案发现进度".Translate(),
            knownTechnologyCount, technologyDefinitions.Length).WithColor(Gray);
        technologyArchiveText.text = BuildTechnologyArchive(technologyDefinitions, knownIds, resolvedIds);
        PageLayout.ResizeScrollableText(technologyArchiveRoot, technologyArchiveText);

        string footer = "研究页脚".Translate();
        if (NebulaMultiplayerModAPI.IsClient) {
            footer += "  ·  " + "研究客户端说明".Translate();
        }
        footerText.text = footer.WithColor(Gray);
    }

    private static void BuildProgressMetric(RectTransform root, out Text label, out Image fill,
        Color? fillColor = null) {
        label = PageLayout.AddCenteredText(root, string.Empty, 13, White, TextAnchor.MiddleLeft,
            0f, 0f, root.sizeDelta.x, 20f, $"{root.name}-label");
        label.supportRichText = true;
        fill = PageLayout.CreateProgressBar(root, $"{root.name}-bar",
            0f, Math.Max(22f, root.sizeDelta.y - 10f), root.sizeDelta.x, 8f, fillColor);
    }

    private static void UpdateProgress(int total, int known, int resolved) {
        string towers = (ResearchManager.HasAcquiredAllAdvancedTowers ? "已完成" : "未完成").Translate();
        string legacy = (PermanentProfileStore.HasPlayedFractionation ? "已完成" : "未完成").Translate();
        progressHeadlineText.text =
            $"{"研究档案".Translate().WithColor(Orange)}  {known}/{total}\n"
            + $"{"高级塔收集".Translate()}：{towers}  ·  {"传承准备".Translate()}：{legacy}";
        exploredMetricText.text = $"{"研究已探索".Translate()}  {known}/{total}";
        resolvedMetricText.text = $"{"研究已解析".Translate()}  {resolved}/{total}";
        PageLayout.SetProgress(exploredProgressFill, total > 0 ? known / (float)total : 0f);
        PageLayout.SetProgress(resolvedProgressFill, total > 0 ? resolved / (float)total : 0f);
    }

    private static string BuildDirectionList(ResearchDirectionSnapshot[] directions) {
        if (directions.Length == 0) {
            return "研究方向为空".Translate().WithColor(Gray);
        }

        var builder = new StringBuilder();
        foreach (ResearchDirectionSnapshot direction in directions) {
            bool selected = direction.ItemId == selectedDirectionItemId;
            string marker = selected ? ">".WithColor(Orange) : " ";
            string status = GetAnalysisStatus(direction).Translate()
                .WithColor(direction.HasPendingContent ? Green
                    : direction.HasRemainingContent ? Gray : Blue);
            builder.Append(marker).Append(' ')
                .Append(GetItemName(direction.ItemId))
                .Append("  ").Append(direction.Progress / 100f).Append('%')
                .Append("  ").Append(status);
            if (direction.HasPendingContent) {
                builder.Append("  ×").Append(direction.PendingContentCount);
            }
            builder.Append('\n');
        }
        return builder.ToString();
    }

    private static string BuildDirectionDetail(ResearchDirectionSnapshot[] directions) {
        ResearchDirectionSnapshot direction = directions.FirstOrDefault(candidate =>
            candidate.ItemId == selectedDirectionItemId);
        if (direction == null) {
            return "研究方向为空".Translate().WithColor(Gray);
        }

        string status = GetAnalysisStatus(direction).Translate()
            .WithColor(direction.HasPendingContent ? Green
                : direction.HasRemainingContent ? Gray : Blue);
        string remaining = (direction.HasRemainingContent ? "仍有未知内容" : "方向已完成")
            .Translate().WithColor(direction.HasRemainingContent ? Gray : Blue);
        return $"{GetItemName(direction.ItemId).WithColor(Orange)}    "
               + $"{"解析状态".Translate()}：{status}\n"
               + $"{"当前进度".Translate()}：{direction.Progress / 100f:0.##}%    "
               + $"{"方向内容".Translate()}：{direction.PendingContentCount}    {remaining}";
    }

    private static string BuildTechnologyArchive(IEnumerable<ResearchContentDefinition> definitions,
        ISet<string> knownIds, ISet<string> resolvedIds) {
        var builder = new StringBuilder();
        foreach (ResearchContentDefinition definition in definitions) {
            builder.Append('T').Append(definition.Tier).Append("  ");
            if (!knownIds.Contains(definition.Id)) {
                builder.Append("未知研究内容".Translate().WithColor(Gray));
            } else {
                bool resolved = resolvedIds.Contains(definition.Id);
                builder.Append(definition.DisplayName.WithColor(Orange)).Append("  ·  ")
                    .Append((resolved ? "已解析" : "等待解析").Translate()
                        .WithColor(resolved ? Green : Blue));
            }
            builder.Append('\n').Append('\n');
        }
        return builder.ToString();
    }

    private static string GetAnalysisStatus(ResearchDirectionSnapshot direction) {
        if (direction.HasPendingContent) {
            return "等待解析";
        }
        return direction.HasRemainingContent ? "等待新探索" : "方向已完成";
    }

    private static void CycleDirection() {
        ResearchDirectionSnapshot[] directions = ResearchManager.GetDirectionSnapshots();
        if (directions.Length == 0) {
            return;
        }
        int index = Array.FindIndex(directions, direction => direction.ItemId == selectedDirectionItemId);
        selectedDirectionItemId = directions[(index + 1) % directions.Length].ItemId;
        UpdateUI();
    }

    private static void SynchronizeSelectedDirection(ResearchDirectionSnapshot[] directions) {
        if (directions.Length == 0) {
            selectedDirectionItemId = 0;
            return;
        }
        if (directions.All(direction => direction.ItemId != selectedDirectionItemId)) {
            selectedDirectionItemId = directions.FirstOrDefault(direction => direction.HasPendingContent)?.ItemId
                                      ?? directions[0].ItemId;
        }
    }

    private static string GetItemName(int itemId) => LDB.items.Select(itemId)?.name ?? itemId.ToString();
}
