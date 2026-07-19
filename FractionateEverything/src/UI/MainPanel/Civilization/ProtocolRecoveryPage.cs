using System.Text;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Logic.Civilization.Analysis;
using FE.Logic.Civilization.Configuration;
using FE.Logic.Civilization.Protocols;
using FE.Logic.Fractionation.FracRecipes;
using FE.UI.Foundation.Window;
using FE.UI.Layout;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Layout.GridDsl;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>
/// 消费阶段检索机会，发现、推进或完成基础协议。
/// </summary>
public static class ProtocolRecoveryPage {
    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static Text stageSummaryText;
    private static Text protocolListText;
    private static Text resultText;
    private static UIButton previousButton;
    private static UIButton nextButton;
    private static UIButton retrieveButton;
    private static UIButton priorityButton;
    private static int selectedStageIndex;
    private static string lastResult = "尚未执行检索";

    public static void AddTranslations() {
        Register("文明协议恢复", "Protocol Recovery");
        Register("协议恢复摘要", "Discover protocols randomly, then focus an already discovered protocol.",
            "随机发现新协议；发现后可设置优先目标并持续推进完整度。");
        Register("执行检索", "Retrieve");
        Register("切换优先协议", "Cycle Priority");
        Register("上一阶段", "Previous Stage");
        Register("下一阶段", "Next Stage");
        Register("空协议阶段说明",
            "This stage has no required base protocols. Retrieval opportunities are submitted directly to deep analysis.",
            "本阶段没有必需基础协议，检索机会将直接投入深层解析并推进科技点进度。");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        tab = trans;
        BuildLayout(wnd, tab,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), Px(118f), 1, Px(PageLayout.FooterHeight)],
                rowGap: PageLayout.Gap,
                children: [
                    Header("文明协议恢复", "协议恢复摘要", pos: (0, 0), objectName: "protocol-recovery-header",
                        onBuilt: refs => header = refs),
                    ContentCard(pos: (1, 0), objectName: "protocol-recovery-control-card", strong: true,
                        rows: [1, Px(38f)], rowGap: 8f,
                        children: [
                            TextNode("", 13, White, wrap: true, pos: (0, 0),
                                onBuilt: text => {
                                    stageSummaryText = text;
                                    text.supportRichText = true;
                                }, objectName: "protocol-recovery-stage-summary"),
                            Grid(pos: (1, 0), cols: [1, 1, 1, 1], columnGap: 10f,
                                children: [
                                    ButtonNode("上一阶段", PreviousStage, pos: (0, 0),
                                        onBuilt: button => previousButton = button,
                                        objectName: "protocol-recovery-previous"),
                                    ButtonNode("下一阶段", NextStage, pos: (0, 1),
                                        onBuilt: button => nextButton = button,
                                        objectName: "protocol-recovery-next"),
                                    ButtonNode("执行检索", Retrieve, pos: (0, 2),
                                        onBuilt: button => retrieveButton = button,
                                        objectName: "protocol-recovery-retrieve"),
                                    ButtonNode("切换优先协议", CyclePriority, pos: (0, 3),
                                        onBuilt: button => priorityButton = button,
                                        objectName: "protocol-recovery-priority"),
                                ]),
                        ]),
                    ScrollableContentCard(1400f, pos: (2, 0), objectName: "protocol-recovery-list-card",
                        rows: [1], children: [
                            TextNode("", 13, White, anchor: TextAnchor.UpperLeft, wrap: true, pos: (0, 0),
                                onBuilt: text => {
                                    protocolListText = text;
                                    text.supportRichText = true;
                                }, objectName: "protocol-recovery-list"),
                        ]),
                    FooterCard(pos: (3, 0), objectName: "protocol-recovery-footer", children: [
                        TextNode("", 12, Gray, wrap: true, pos: (0, 0),
                            onBuilt: text => {
                                resultText = text;
                                text.supportRichText = true;
                            }, objectName: "protocol-recovery-result"),
                    ]),
                ]));
        UpdateUI();
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        ProgressionProfile profile = ProgressionProfileRegistry.Current;
        if (profile == null || profile.Stages.Count == 0) {
            return;
        }
        selectedStageIndex = Mathf.Clamp(selectedStageIndex, 0, profile.Stages.Count - 1);
        MatrixStageDefinition stage = profile.Stages[selectedStageIndex];
        AnalysisProgressStore.StageProgress analysis = AnalysisService.GetProgress(stage.StageKey);
        ProtocolProgressStore.StageRetrievalProgress retrieval = ProtocolProgressStore.GetStageProgress(stage.StageKey);

        header.Title.text = "文明协议恢复".Translate().WithColor(Orange);
        header.Summary.text = "协议恢复摘要".Translate().WithColor(White);
        bool hasRequiredProtocols = ProtocolCatalog.HasRequiredProtocols(stage.StageKey);
        stageSummaryText.text =
            $"{stage.DisplayNameKey.Translate().WithColor(Orange)}  可用检索：{analysis.AvailableOpportunities}  "
            + $"下次机会：{analysis.PendingData}/{AnalysisService.GetNextOpportunityCost(stage.StageKey)}\n"
            + (hasRequiredProtocols
                ? $"失败保底：{retrieval.FailureStreak}/4  新发现保底：{retrieval.DiscoveryStreak}/5  "
                  + $"阶段完成：{(ProtocolCatalog.IsStageComplete(stage.StageKey) ? "是" : "否")}"
                : "空协议阶段说明".Translate());
        protocolListText.text = BuildProtocolList(stage.StageKey);
        resultText.text = lastResult.WithColor(Gray);
        previousButton.button.interactable = selectedStageIndex > 0;
        nextButton.button.interactable = selectedStageIndex < profile.Stages.Count - 1;
        retrieveButton.button.interactable = analysis.AvailableOpportunities > 0;
        priorityButton.button.interactable = HasPriorityCandidate(stage.StageKey);
    }

    private static string BuildProtocolList(string stageKey) {
        var builder = new StringBuilder();
        ProtocolProgressStore.StageRetrievalProgress retrieval = ProtocolProgressStore.GetStageProgress(stageKey);
        int index = 1;
        foreach (ProtocolDefinition definition in ProtocolCatalog.GetByStage(stageKey)) {
            ProtocolProgressStore.ProtocolProgress progress = ProtocolProgressStore.GetOrCreate(definition.RecipeKey);
            bool preferred = retrieval.HasPreferredRecipe && retrieval.PreferredRecipe.Equals(definition.RecipeKey);
            BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(definition.RecipeKey.RecipeType,
                definition.RecipeKey.InputId);
            string name = progress.Discovered
                ? recipe?.TypeName ?? $"{definition.RecipeKey.RecipeType}/{definition.RecipeKey.InputId}"
                : "未识别协议";
            string state = progress.Discovered ? $"{progress.Completeness}%" : "未发现";
            builder.Append(index++).Append(". ").Append(preferred ? "[优先] " : string.Empty)
                .Append(definition.CountsTowardStageCompletion ? string.Empty : "[附属] ")
                .Append(name).Append("  ").Append(state).Append('\n');
        }
        return builder.Length == 0 ? "空协议阶段说明".Translate() : builder.ToString();
    }

    private static bool HasPriorityCandidate(string stageKey) {
        foreach (ProtocolDefinition definition in ProtocolCatalog.GetByStage(stageKey)) {
            ProtocolProgressStore.ProtocolProgress progress = ProtocolProgressStore.GetOrCreate(definition.RecipeKey);
            if (progress.Discovered && progress.Completeness < 100
                                    && ProtocolEligibilityService.IsEligible(definition)) {
                return true;
            }
        }
        return false;
    }

    private static void PreviousStage() {
        selectedStageIndex--;
        lastResult = "已切换阶段。";
        UpdateUI();
    }

    private static void NextStage() {
        selectedStageIndex++;
        lastResult = "已切换阶段。";
        UpdateUI();
    }

    private static void CyclePriority() {
        MatrixStageDefinition stage = GetSelectedStage();
        lastResult = stage != null && ProtocolRetrievalService.CyclePreferredProtocol(stage.StageKey)
            ? "已切换优先协议。"
            : "当前没有可设为优先的已发现协议。";
        UpdateUI();
    }

    private static void Retrieve() {
        MatrixStageDefinition stage = GetSelectedStage();
        if (stage == null || !ProtocolRetrievalService.TryRetrieve(stage.StageKey, out ProtocolRetrievalResult result)) {
            lastResult = "本次无法执行检索：当前阶段没有可消费的检索机会。";
            UpdateUI();
            return;
        }

        if (result.Outcome == ProtocolRetrievalOutcome.DeepAnalysis) {
            lastResult = result.AwardedTechPoint
                ? "深层解析完成，获得 1 点远古文明科技点。"
                : "深层解析进度增加。";
        } else if (result.Outcome == ProtocolRetrievalOutcome.Failed) {
            lastResult = "检索未获得有效响应，保底进度已增加。";
        } else {
            BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(result.RecipeKey.RecipeType,
                result.RecipeKey.InputId);
            lastResult = $"{recipe?.TypeName ?? "协议"}：{result.PreviousCompleteness}% -> {result.CurrentCompleteness}%";
        }
        UpdateUI();
    }

    private static MatrixStageDefinition GetSelectedStage() {
        ProgressionProfile profile = ProgressionProfileRegistry.Current;
        return profile == null || selectedStageIndex < 0 || selectedStageIndex >= profile.Stages.Count
            ? null
            : profile.Stages[selectedStageIndex];
    }
}
