using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Logic.Civilization.Analysis;
using FE.Logic.Civilization.Configuration;
using FE.Logic.Civilization.Protocols;
using FE.Logic.Civilization.Technology;
using FE.UI.Foundation.Window;
using FE.UI.Layout;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Layout.GridDsl;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>
/// 汇总六个矩阵阶段的解析数据、检索机会和协议恢复进度。
/// </summary>
public static class CivilizationOverviewPage {
    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static readonly Text[] stageTexts = new Text[6];
    private static Text footerText;

    public static void AddTranslations() {
        Register("文明总览", "Civilization Overview");
        Register("文明总览摘要", "Inspect matrix analysis, retrieval opportunities, and protocol recovery by stage.",
            "查看各矩阵阶段的解析数据、检索机会和协议恢复进度。");
        Register("文明总览页脚", "Completed stages open deep analysis and generate ancient technology points.",
            "完成阶段内全部协议后，该阶段的检索机会将转入深层解析并产出远古文明科技点。");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        tab = trans;
        BuildLayout(wnd, tab,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), 1, Px(PageLayout.FooterHeight)],
                rowGap: PageLayout.Gap,
                children: [
                    Header("文明总览", "文明总览摘要", pos: (0, 0), objectName: "civilization-overview-header",
                        onBuilt: refs => header = refs),
                    ContentCard(pos: (1, 0), objectName: "civilization-overview-stage-card", strong: true,
                        rows: [1, 1, 1, 1, 1, 1], rowGap: 6f,
                        children: BuildStageNodes()),
                    FooterCard(pos: (2, 0), objectName: "civilization-overview-footer",
                        children: [
                            TextNode("", 12, Gray, wrap: true, pos: (0, 0),
                                onBuilt: text => {
                                    footerText = text;
                                    text.supportRichText = true;
                                }, objectName: "civilization-overview-footer-text"),
                        ]),
                ]));
        UpdateUI();
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        header.Title.text = "文明总览".Translate().WithColor(Orange);
        header.Summary.text = "文明总览摘要".Translate().WithColor(White);
        ProgressionProfile profile = ProgressionProfileRegistry.Current;
        if (profile == null) {
            return;
        }

        for (int i = 0; i < stageTexts.Length && i < profile.Stages.Count; i++) {
            MatrixStageDefinition stage = profile.Stages[i];
            AnalysisProgressStore.StageProgress analysis = AnalysisService.GetProgress(stage.StageKey);
            int total = 0;
            int discovered = 0;
            int complete = 0;
            int optionalTotal = 0;
            int optionalComplete = 0;
            foreach (ProtocolDefinition definition in ProtocolCatalog.GetByStage(stage.StageKey)) {
                ProtocolProgressStore.ProtocolProgress progress =
                    ProtocolProgressStore.GetOrCreate(definition.RecipeKey);
                if (definition.CountsTowardStageCompletion) {
                    total++;
                    if (progress.Discovered) discovered++;
                    if (progress.Completeness >= 100) complete++;
                } else {
                    optionalTotal++;
                    if (progress.Completeness >= 100) optionalComplete++;
                }
            }

            long nextCost = AnalysisService.GetNextOpportunityCost(stage.StageKey);
            string stageName = stage.DisplayNameKey.Translate();
            string protocolSummary = total == 0
                ? "协议 无（直接深层解析）"
                : $"协议 {complete}/{total}，已发现 {discovered}/{total}";
            stageTexts[i].text =
                $"{stageName.WithColor(Orange)}  {protocolSummary}\n"
                + $"解析数据 {analysis.PendingData}/{nextCost}，可用检索 {analysis.AvailableOpportunities}"
                + (optionalTotal > 0 ? $"，附属协议 {optionalComplete}/{optionalTotal}" : string.Empty);
        }

        footerText.text =
            $"{"文明总览页脚".Translate()}  当前科技点：{AncientTechTreeState.AvailablePoints}，"
            + $"累计获得：{AncientTechTreeState.TotalPointsEarned}";
    }

    private static LayoutNode[] BuildStageNodes() {
        var nodes = new LayoutNode[stageTexts.Length];
        for (int i = 0; i < nodes.Length; i++) {
            int index = i;
            nodes[i] = TextNode("", 13, White, anchor: TextAnchor.MiddleLeft, wrap: true,
                pos: (i, 0), onBuilt: text => {
                    stageTexts[index] = text;
                    text.supportRichText = true;
                }, objectName: $"civilization-overview-stage-{i}");
        }
        return nodes;
    }
}
