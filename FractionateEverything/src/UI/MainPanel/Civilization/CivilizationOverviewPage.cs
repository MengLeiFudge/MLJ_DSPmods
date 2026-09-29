using System;
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
/// 展示物品方向研究、内容发现和生命工业传承状态。
/// </summary>
public static class CivilizationOverviewPage {
    private const float DirectionLineHeight = 24f;
    private const float ContentLineHeight = 24f;

    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static Text overviewText;
    private static Text directionListText;
    private static RectTransform directionListRoot;
    private static Text directionDetailText;
    private static Text discoveredContentText;
    private static RectTransform discoveredContentRoot;
    private static Text footerText;
    private static UIButton directionButton;
    private static int selectedDirectionItemId;

    /// <summary>
    /// 注册研究页面文本。
    /// </summary>
    public static void AddTranslations() {
        Register("文明研究", "Civilization Research");
        Register("文明研究摘要",
            "Inspect item-direction eligibility, analysis progress, discovered recipes, and living-tower traits.",
            "查看物品方向的解析资格、研究进度、已发现配方与生命塔特质。");
        Register("研究总览", "Research Overview");
        Register("研究方向", "Research Directions");
        Register("方向详情", "Direction Detail");
        Register("已发现内容", "Discovered Content");
        Register("切换研究方向", "Cycle Direction");
        Register("解析资格", "Eligibility");
        Register("已有资格", "Eligible");
        Register("等待探索", "Awaiting Exploration");
        Register("当前进度", "Current Progress");
        Register("方向内容", "Direction Content");
        Register("已发现", "Discovered");
        Register("尚无已发现内容", "No content has been discovered yet.");
        Register("研究方向为空", "No research directions are currently available.");
        Register("研究成果", "Research Results");
        Register("高级塔收集", "Advanced Towers");
        Register("传承准备", "Legacy Readiness");
        Register("已完成", "Complete");
        Register("未完成", "Incomplete");
        Register("研究页脚",
            "Feed eligible items into a Basic Analysis Tower. Each successful analysis advances one lowest-tier undiscovered result in that direction.",
            "将已取得资格的物品送入基础解析塔；每次解析成功会推进该方向当前最低未发现层级中的一项内容。");
        Register("研究客户端说明", "Research state is settled and synchronized by the host.",
            "研究状态由主机结算并同步。");
    }

    /// <summary>
    /// 研究页面没有独立配置。
    /// </summary>
    public static void LoadConfig(ConfigFile configFile) { }

    /// <summary>
    /// 创建研究总览、方向列表和发现内容区域。
    /// </summary>
    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        tab = trans;
        ResearchDirectionSnapshot[] directions = ResearchManager.GetDirectionSnapshots();
        float directionHeight = Math.Max(360f, directions.Length * DirectionLineHeight + 20f);
        int discoveredHeightUnits = ResearchManager.GetUnlockedContentDefinitions()
            .Sum(definition => RecipeManager.GetRecipe(definition.Id) == null ? 1 : 5);
        float contentHeight = Math.Max(260f, Math.Max(1, discoveredHeightUnits) * ContentLineHeight + 20f);

        BuildLayout(wnd, tab,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), Px(112f), 1, Px(PageLayout.FooterHeight)],
                rowGap: PageLayout.Gap,
                children: [
                    Header("文明研究", "文明研究摘要", pos: (0, 0), objectName: "research-header",
                        onBuilt: refs => header = refs),
                    ContentCard(pos: (1, 0), objectName: "research-overview-card", strong: true,
                        cols: [1, Px(210f)], columnGap: PageLayout.InnerGap,
                        children: [
                            TextNode("", 13, White, anchor: TextAnchor.MiddleLeft, wrap: true, pos: (0, 0),
                                onBuilt: text => {
                                    overviewText = text;
                                    text.supportRichText = true;
                                }, objectName: "research-overview-text"),
                            ButtonNode("切换研究方向", CycleDirection, pos: (0, 1),
                                onBuilt: button => directionButton = button,
                                objectName: "research-cycle-direction"),
                        ]),
                    Grid(pos: (2, 0), cols: [1, 1], columnGap: PageLayout.Gap,
                        objectName: "research-body", children: [
                            ContentCard(pos: (0, 0), objectName: "research-direction-card", strong: true,
                                rows: [Px(28f), 1], rowGap: 8f, children: [
                                    CardTitleNode("研究方向", pos: (0, 0), objectName: "research-direction-title"),
                                    ScrollableContentCard(directionHeight, pos: (1, 0),
                                        objectName: "research-direction-scroll",
                                        onBuilt: root => directionListRoot = root, children: [
                                            TextNode("", 13, White, anchor: TextAnchor.UpperLeft, wrap: true,
                                                pos: (0, 0), onBuilt: text => {
                                                    directionListText = text;
                                                    text.supportRichText = true;
                                                }, objectName: "research-direction-list"),
                                        ]),
                                ]),
                            Grid(pos: (0, 1), rows: [Px(184f), 1], rowGap: PageLayout.Gap,
                                objectName: "research-detail-column", children: [
                                    ContentCard(pos: (0, 0), objectName: "research-direction-detail-card",
                                        rows: [Px(28f), 1], rowGap: 8f, children: [
                                            CardTitleNode("方向详情", pos: (0, 0),
                                                objectName: "research-direction-detail-title"),
                                            TextNode("", 13, White, anchor: TextAnchor.UpperLeft, wrap: true,
                                                pos: (1, 0), onBuilt: text => {
                                                    directionDetailText = text;
                                                    text.supportRichText = true;
                                                }, objectName: "research-direction-detail"),
                                        ]),
                                    ContentCard(pos: (1, 0), objectName: "research-content-card",
                                        rows: [Px(28f), 1], rowGap: 8f, children: [
                                            CardTitleNode("已发现内容", pos: (0, 0),
                                                objectName: "research-content-title"),
                                            ScrollableContentCard(contentHeight, pos: (1, 0),
                                                objectName: "research-content-scroll",
                                                onBuilt: root => discoveredContentRoot = root, children: [
                                                    TextNode("", 13, White, anchor: TextAnchor.UpperLeft,
                                                        wrap: true, pos: (0, 0), onBuilt: text => {
                                                            discoveredContentText = text;
                                                            text.supportRichText = true;
                                                        }, objectName: "research-content-list"),
                                                ]),
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

        ResearchDirectionSnapshot[] directions = ResearchManager.GetDirectionSnapshots();
        SynchronizeSelectedDirection(directions);
        header.Title.text = "文明研究".Translate().WithColor(Orange);
        header.Summary.text = "文明研究摘要".Translate().WithColor(White);
        overviewText.text = BuildOverviewText();
        directionListText.text = BuildDirectionList(directions);
        ResizeScrollableText(directionListRoot, directionListText);
        directionDetailText.text = BuildDirectionDetail(directions);
        discoveredContentText.text = BuildDiscoveredContent();
        ResizeScrollableText(discoveredContentRoot, discoveredContentText);
        directionButton.button.interactable = directions.Length > 1;
        directionButton.SetText("切换研究方向".Translate());

        string footer = "研究页脚".Translate();
        if (NebulaMultiplayerModAPI.IsClient) {
            footer += "  ·  " + "研究客户端说明".Translate();
        }
        footerText.text = footer.WithColor(Gray);
    }

    private static string BuildOverviewText() {
        string towers = (ResearchManager.HasAcquiredAllAdvancedTowers ? "已完成" : "未完成").Translate();
        string completion = (PermanentProfileStore.HasPlayedFractionation ? "已完成" : "未完成").Translate();
        return $"{"研究成果".Translate()}：{ResearchManager.DiscoveredContentCount.ToString().WithColor(Blue)}    "
               + $"{"高级塔收集".Translate()}：{towers}\n"
               + $"{"传承准备".Translate()}：{completion}";
    }

    private static string BuildDirectionList(ResearchDirectionSnapshot[] directions) {
        if (directions.Length == 0) {
            return "研究方向为空".Translate().WithColor(Gray);
        }

        var builder = new StringBuilder();
        foreach (ResearchDirectionSnapshot direction in directions) {
            bool selected = direction.ItemId == selectedDirectionItemId;
            string marker = selected ? ">".WithColor(Orange) : " ";
            string eligibility = (direction.Eligible ? "已有资格" : "等待探索").Translate()
                .WithColor(direction.Eligible ? Green : Gray);
            builder.Append(marker).Append(' ')
                .Append(GetItemName(direction.ItemId))
                .Append("  ").Append(eligibility)
                .Append("  ").Append(direction.DiscoveredCount).Append('/').Append(direction.TotalCount)
                .Append('\n');
        }
        return builder.ToString();
    }

    private static string BuildDirectionDetail(ResearchDirectionSnapshot[] directions) {
        ResearchDirectionSnapshot direction = directions.FirstOrDefault(candidate =>
            candidate.ItemId == selectedDirectionItemId);
        if (direction == null) {
            return "研究方向为空".Translate().WithColor(Gray);
        }

        string eligibility = (direction.Eligible ? "已有资格" : "等待探索").Translate()
            .WithColor(direction.Eligible ? Green : Gray);
        float percentage = direction.Progress / 100f;
        return $"{GetItemName(direction.ItemId).WithColor(Orange)}\n\n"
               + $"{"解析资格".Translate()}：{eligibility}\n"
               + $"{"当前进度".Translate()}：{percentage:0.##}%\n"
               + $"{"方向内容".Translate()}：{direction.DiscoveredCount}/{direction.TotalCount}";
    }

    private static string BuildDiscoveredContent() {
        ResearchContentDefinition[] definitions = ResearchManager.GetUnlockedContentDefinitions();
        if (definitions.Length == 0) {
            return "尚无已发现内容".Translate().WithColor(Gray);
        }

        var builder = new StringBuilder();
        foreach (ResearchContentDefinition definition in definitions) {
            WorldviewRecipe recipe = RecipeManager.GetRecipe(definition.Id);
            builder.Append("T").Append(definition.Tier).Append("  ")
                .Append(definition.DisplayName.WithColor(Orange)).Append('\n');
            if (recipe == null) {
                builder.Append('\n');
                continue;
            }

            builder.Append("稳定配方ID".Translate()).Append("：").Append(recipe.StableId)
                .Append("  ·  ").Append("运行塔型".Translate()).Append("：")
                .Append(GetItemName(recipe.RecipeType.GetSpriteItemId())).Append('\n');
            builder.Append("成功率".Translate()).Append("：")
                .Append(recipe.CycleSuccessProbability.ToString("P0")).Append('\n');
            builder.Append("配方输入".Translate()).Append("：")
                .Append(string.Join(" + ", recipe.Inputs.Select(input => {
                    string consumption = input.ConsumeProbability < 0.9999f
                        ? $" ({input.ConsumeProbability:P0} {"每次成功消耗概率".Translate()})"
                        : string.Empty;
                    return $"{input.Count} × {GetItemName(input.ItemId)}{consumption}";
                }))).Append('\n');
            builder.Append("配方输出".Translate()).Append("：")
                .Append(string.Join(" + ", recipe.Products.Select(output => {
                    string recovery = output.ScalesWithRecovery
                        ? $" ({"按回收率结算".Translate()})"
                        : string.Empty;
                    return $"{output.Count:0.##} × {GetItemName(output.ItemId)}{recovery}";
                }))).Append("\n\n");
        }
        return builder.ToString();
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
            selectedDirectionItemId = directions.FirstOrDefault(direction => direction.Eligible)?.ItemId
                                      ?? directions[0].ItemId;
        }
    }

    private static void ResizeScrollableText(RectTransform contentRoot, Text text) {
        if (contentRoot == null || text == null) {
            return;
        }
        RectTransform viewport = contentRoot.parent as RectTransform;
        float height = Math.Max(viewport?.rect.height ?? 0f, text.preferredHeight + 20f);
        contentRoot.sizeDelta = new Vector2(contentRoot.sizeDelta.x, height);
        if (text.rectTransform.parent is RectTransform textContainer) {
            float textHeight = Math.Max(0f, height - 20f);
            textContainer.sizeDelta = new Vector2(textContainer.sizeDelta.x, textHeight);
            text.rectTransform.sizeDelta = new Vector2(text.rectTransform.sizeDelta.x, textHeight);
        }
    }

    private static string GetItemName(int itemId) => LDB.items.Select(itemId)?.name ?? itemId.ToString();
}
