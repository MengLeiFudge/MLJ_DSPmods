using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Logic.Civilization.Configuration;
using FE.Logic.Civilization.Protocols;
using FE.Logic.Fractionation.FracRecipes;
using FE.UI.Foundation;
using FE.UI.Foundation.Window;
using FE.UI.Layout;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Layout.GridDsl;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.Civilization;

/// <summary>
/// 选择广域、方向或锚定策略，并通过检索服务恢复文明协议。
/// </summary>
public static class ProtocolRecoveryPage {
    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static Text stageSummaryText;
    private static Text protocolListText;
    private static Text resultText;
    private static UIButton previousButton;
    private static UIButton nextButton;
    private static UIButton strategyButton;
    private static UIButton targetButton;
    private static UIButton retrieveButton;
    private static UIButton batchButton;
    private static UIButton priorityButton;
    private static int selectedStageIndex;
    private static ProtocolRetrievalMode selectedMode;
    private static ERecipe selectedDirectionalRecipeType;
    private static RecipeKey selectedAnchoredRecipeKey;
    private static bool hasSelectedAnchoredRecipe;
    private static string lastResult = "尚未执行检索";

    public static void AddTranslations() {
        Register("文明协议恢复", "Protocol Recovery");
        Register("协议恢复摘要", "Choose broad, directional, or anchored recovery and settle each retrieval independently.",
            "选择广域、方向或锚定检索；每次机会均独立结算。 ");
        Register("执行单次", "Retrieve Once");
        Register("执行默认批量", "Retrieve Batch");
        Register("切换策略", "Cycle Strategy");
        Register("切换检索目标", "Cycle Retrieval Target");
        Register("切换优先协议", "Cycle Priority");
        Register("上一阶段", "Previous Stage");
        Register("下一阶段", "Next Stage");
        Register("广域检索", "Broad Retrieval");
        Register("方向检索", "Directional Retrieval");
        Register("锚定检索", "Anchored Retrieval");
        Register("残片不足", "Insufficient fragments");
        Register("没有可检索协议", "No retrievable protocol");
        Register("没有可用检索机会", "No retrieval opportunity");
        Register("检索请求无效", "Invalid retrieval request");
        Register("无额外目标", "No additional target");
        Register("未识别协议", "Unidentified protocol");
        Register("未发现", "Undiscovered");
        Register("附属", "Auxiliary");
        Register("未具备资格", "Not eligible");
        Register("空协议阶段说明",
            "This stage has no required base protocols. Broad retrieval opportunities are submitted directly to deep analysis.",
            "本阶段没有必需基础协议；广域检索机会将直接投入深层解析。 ");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        tab = trans;
        BuildLayout(wnd, tab,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), Px(154f), 1, Px(PageLayout.FooterHeight)],
                rowGap: PageLayout.Gap,
                children: [
                    Header("文明协议恢复", "协议恢复摘要", pos: (0, 0), objectName: "protocol-recovery-header",
                        onBuilt: refs => header = refs),
                    ContentCard(pos: (1, 0), objectName: "protocol-recovery-control-card", strong: true,
                        rows: [Px(50f), Px(30f), Px(30f)], rowGap: 6f,
                        children: [
                            TextNode("", 13, White, wrap: true, pos: (0, 0),
                                onBuilt: text => {
                                    stageSummaryText = text;
                                    text.supportRichText = true;
                                }, objectName: "protocol-recovery-stage-summary"),
                            Grid(pos: (1, 0), cols: [1, 1, 1, 1], columnGap: 8f,
                                children: [
                                    ButtonNode("上一阶段", PreviousStage, pos: (0, 0),
                                        onBuilt: button => previousButton = button,
                                        objectName: "protocol-recovery-previous"),
                                    ButtonNode("下一阶段", NextStage, pos: (0, 1),
                                        onBuilt: button => nextButton = button,
                                        objectName: "protocol-recovery-next"),
                                    ButtonNode("切换策略", CycleStrategy, pos: (0, 2),
                                        onBuilt: button => strategyButton = button,
                                        objectName: "protocol-recovery-strategy"),
                                    ButtonNode("切换检索目标", CycleTarget, pos: (0, 3),
                                        onBuilt: button => targetButton = button,
                                        objectName: "protocol-recovery-target"),
                                ]),
                            Grid(pos: (2, 0), cols: [1, 1, 1], columnGap: 8f,
                                children: [
                                    ButtonNode("执行单次", Retrieve, pos: (0, 0),
                                        onBuilt: button => retrieveButton = button,
                                        objectName: "protocol-recovery-retrieve"),
                                    ButtonNode("执行默认批量", RetrieveBatch, pos: (0, 1),
                                        onBuilt: button => batchButton = button,
                                        objectName: "protocol-recovery-batch"),
                                    ButtonNode("切换优先协议", CyclePriority, pos: (0, 2),
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
        ProtocolRetrievalStageSnapshot snapshot = ProtocolRetrievalService.GetStageSnapshot(stage.StageKey);
        SynchronizeTargets(stage.StageKey);
        ProtocolRetrievalRequest request = GetRequest(stage.StageKey);
        bool canRetrieve = ProtocolRetrievalService.CanRetrieve(request, out _);

        header.Title.text = "文明协议恢复".Translate().WithColor(Orange);
        header.Summary.text = "协议恢复摘要".Translate().WithColor(White);
        stageSummaryText.text =
            $"{stage.DisplayNameKey.Translate().WithColor(Orange)}  可用检索：{snapshot.AvailableOpportunities}  "
            + $"下次机会：{snapshot.PendingData}/{snapshot.NextOpportunityCost}\n"
            + $"残片：{snapshot.Fragments}  "
            + GetModeCostText()
            + (snapshot.HasRequiredProtocols
                ? $"  失败保底：{snapshot.FailureStreak}/4  新发现保底：{snapshot.DiscoveryStreak}/5  "
                  + $"阶段完成：{(snapshot.StageComplete ? "是" : "否")}"
                : "\n" + "空协议阶段说明".Translate());
        protocolListText.text = BuildProtocolList(snapshot.Protocols);
        resultText.text = lastResult.WithColor(Gray);
        previousButton.button.interactable = selectedStageIndex > 0;
        nextButton.button.interactable = selectedStageIndex < profile.Stages.Count - 1;
        strategyButton.SetText(GetModeText());
        targetButton.SetText(GetTargetText(stage.StageKey));
        targetButton.button.interactable = selectedMode != ProtocolRetrievalMode.Broad
                                         && HasSelectableTarget(stage.StageKey);
        retrieveButton.button.interactable = canRetrieve;
        batchButton.button.interactable = canRetrieve;
        priorityButton.button.interactable = snapshot.HasPriorityCandidate;
    }

    private static string BuildProtocolList(IReadOnlyList<ProtocolRetrievalProtocolSnapshot> protocols) {
        var builder = new StringBuilder();
        int index = 1;
        foreach (ProtocolRetrievalProtocolSnapshot protocol in protocols) {
            string name = protocol.Discovered ? protocol.DisplayName : "未识别协议".Translate();
            string state = protocol.Discovered ? $"{protocol.Completeness}%" : "未发现".Translate();
            builder.Append(index++).Append(". ")
                .Append(protocol.Preferred ? "[优先] " : string.Empty)
                .Append(protocol.CountsTowardStageCompletion ? string.Empty : $"[{"附属".Translate()}] ")
                .Append(protocol.Eligible ? string.Empty : $"[{"未具备资格".Translate()}] ")
                .Append(name).Append("  ").Append(state).Append('\n');
        }
        return builder.Length == 0 ? "空协议阶段说明".Translate() : builder.ToString();
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

    private static void CycleStrategy() {
        selectedMode = selectedMode switch {
            ProtocolRetrievalMode.Broad => ProtocolRetrievalMode.Directional,
            ProtocolRetrievalMode.Directional => ProtocolRetrievalMode.Anchored,
            _ => ProtocolRetrievalMode.Broad,
        };
        lastResult = $"已切换为{GetModeText()}。";
        UpdateUI();
    }

    private static void CycleTarget() {
        MatrixStageDefinition stage = GetSelectedStage();
        if (stage == null) {
            return;
        }
        if (selectedMode == ProtocolRetrievalMode.Directional) {
            List<ERecipe> types = ProtocolRetrievalService.GetDirectionalRecipeTypes(stage.StageKey);
            if (types.Count == 0) {
                lastResult = "没有可检索协议".Translate();
            } else {
                int index = types.IndexOf(selectedDirectionalRecipeType);
                selectedDirectionalRecipeType = types[(index + 1) % types.Count];
                lastResult = $"已选择方向：{selectedDirectionalRecipeType.GetShortName()}。";
            }
        } else if (selectedMode == ProtocolRetrievalMode.Anchored) {
            List<ProtocolRetrievalProtocolSnapshot> candidates = ProtocolRetrievalService.GetAnchoredCandidates(stage.StageKey);
            if (candidates.Count == 0) {
                lastResult = "没有可检索协议".Translate();
            } else {
                int index = FindAnchoredCandidateIndex(candidates);
                ProtocolRetrievalProtocolSnapshot target = candidates[(index + 1) % candidates.Count];
                selectedAnchoredRecipeKey = target.RecipeKey;
                hasSelectedAnchoredRecipe = true;
                lastResult = $"已锚定：{target.DisplayName}。";
            }
        } else {
            lastResult = "广域检索不指定目标。";
        }
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
        if (stage == null) {
            return;
        }
        if (!ProtocolRetrievalService.TryRetrieve(GetRequest(stage.StageKey),
                out ProtocolRetrievalResult result)) {
            lastResult = GetStopReasonText(result.StopReason);
            UpdateUI();
            return;
        }
        lastResult = FormatSingleResult(result);
        UpdateUI();
    }

    private static void RetrieveBatch() {
        MatrixStageDefinition stage = GetSelectedStage();
        if (stage == null) {
            return;
        }
        ProtocolRetrievalBatchResult result = ProtocolRetrievalService.RetrieveBatch(GetRequest(stage.StageKey),
            ProtocolRetrievalService.DefaultBatchCount);
        lastResult = FormatBatchResult(result);
        UpdateUI();
    }

    private static ProtocolRetrievalRequest GetRequest(string stageKey) {
        return selectedMode switch {
            ProtocolRetrievalMode.Directional => ProtocolRetrievalRequest.Directional(stageKey, selectedDirectionalRecipeType),
            ProtocolRetrievalMode.Anchored when hasSelectedAnchoredRecipe =>
                ProtocolRetrievalRequest.Anchored(stageKey, selectedAnchoredRecipeKey),
            ProtocolRetrievalMode.Anchored => new ProtocolRetrievalRequest(stageKey, ProtocolRetrievalMode.Anchored),
            _ => ProtocolRetrievalRequest.Broad(stageKey),
        };
    }

    private static void SynchronizeTargets(string stageKey) {
        List<ERecipe> directionalTypes = ProtocolRetrievalService.GetDirectionalRecipeTypes(stageKey);
        if (directionalTypes.Count > 0 && !directionalTypes.Contains(selectedDirectionalRecipeType)) {
            selectedDirectionalRecipeType = directionalTypes[0];
        }

        List<ProtocolRetrievalProtocolSnapshot> anchoredCandidates = ProtocolRetrievalService.GetAnchoredCandidates(stageKey);
        if (anchoredCandidates.Count == 0) {
            hasSelectedAnchoredRecipe = false;
            return;
        }
        if (FindAnchoredCandidateIndex(anchoredCandidates) < 0) {
            selectedAnchoredRecipeKey = anchoredCandidates[0].RecipeKey;
            hasSelectedAnchoredRecipe = true;
        }
    }

    private static bool HasSelectableTarget(string stageKey) {
        return selectedMode == ProtocolRetrievalMode.Directional
            ? ProtocolRetrievalService.GetDirectionalRecipeTypes(stageKey).Count > 0
            : ProtocolRetrievalService.GetAnchoredCandidates(stageKey).Count > 0;
    }

    private static int FindAnchoredCandidateIndex(IReadOnlyList<ProtocolRetrievalProtocolSnapshot> candidates) {
        if (!hasSelectedAnchoredRecipe) {
            return -1;
        }
        for (int i = 0; i < candidates.Count; i++) {
            if (candidates[i].RecipeKey.Equals(selectedAnchoredRecipeKey)) {
                return i;
            }
        }
        return -1;
    }

    private static string GetModeText() {
        return selectedMode switch {
            ProtocolRetrievalMode.Directional => "方向检索".Translate(),
            ProtocolRetrievalMode.Anchored => "锚定检索".Translate(),
            _ => "广域检索".Translate(),
        };
    }

    private static string GetModeCostText() {
        return selectedMode switch {
            ProtocolRetrievalMode.Directional => $"方向成本：{ProtocolRetrievalService.DirectionalFragmentCost} 残片",
            ProtocolRetrievalMode.Anchored => $"锚定成本：{ProtocolRetrievalService.AnchoredFragmentCost} 残片",
            _ => "广域不额外消耗策略货币",
        };
    }

    private static string GetTargetText(string stageKey) {
        if (selectedMode == ProtocolRetrievalMode.Broad) {
            return "无额外目标".Translate();
        }
        if (selectedMode == ProtocolRetrievalMode.Directional) {
            return $"方向：{selectedDirectionalRecipeType.GetShortName()}";
        }
        foreach (ProtocolRetrievalProtocolSnapshot candidate in ProtocolRetrievalService.GetAnchoredCandidates(stageKey)) {
            if (hasSelectedAnchoredRecipe && candidate.RecipeKey.Equals(selectedAnchoredRecipeKey)) {
                return $"锚定：{candidate.DisplayName}";
            }
        }
        return "无额外目标".Translate();
    }

    private static string FormatSingleResult(ProtocolRetrievalResult result) {
        if (result.Outcome == ProtocolRetrievalOutcome.DeepAnalysis) {
            return result.AwardedTechPoint
                ? "深层解析完成，获得 1 点远古文明科技点。"
                : "深层解析进度增加。";
        }
        if (result.Outcome == ProtocolRetrievalOutcome.Failed) {
            return $"检索未获得有效响应，获得 {result.AwardedFragments} 残片，保底进度已增加。"
                   + "残片可用于方向检索；积累更多残片后可使用成本更高的锚定检索。";
        }
        ProtocolRetrievalStageSnapshot snapshot = ProtocolRetrievalService.GetStageSnapshot(GetSelectedStage().StageKey);
        foreach (ProtocolRetrievalProtocolSnapshot protocol in snapshot.Protocols) {
            if (protocol.RecipeKey.Equals(result.RecipeKey)) {
                return $"{protocol.DisplayName}：{result.PreviousCompleteness}% -> {result.CurrentCompleteness}%";
            }
        }
        return $"协议：{result.PreviousCompleteness}% -> {result.CurrentCompleteness}%";
    }

    private static string FormatBatchResult(ProtocolRetrievalBatchResult result) {
        string summary = $"批量结算：执行 {result.ExecutedCount}，失败 {result.FailedCount}，发现 {result.DiscoveredCount}，"
            + $"推进 {result.ProgressedCount}，完成 {result.CompletedCount}，深层解析 {result.DeepAnalysisCount}";
        if (result.AwardedTechPoints > 0) {
            summary += $"，科技点 +{result.AwardedTechPoints}";
        }
        if (result.SpentFragments > 0 || result.AwardedFragments > 0) {
            summary += $"；残片 -{result.SpentFragments} +{result.AwardedFragments}";
        }
        return result.StopReason == ProtocolRetrievalStopReason.None
            ? summary + "。"
            : summary + $"；停止：{GetStopReasonText(result.StopReason)}";
    }

    private static string GetStopReasonText(ProtocolRetrievalStopReason stopReason) {
        return stopReason switch {
            ProtocolRetrievalStopReason.NoOpportunity => "没有可用检索机会".Translate(),
            ProtocolRetrievalStopReason.NoCandidate => "没有可检索协议".Translate(),
            ProtocolRetrievalStopReason.InsufficientFragments => "残片不足".Translate(),
            ProtocolRetrievalStopReason.AwaitingHost => "请求已发送，等待主机结算。",
            _ => "检索请求无效".Translate(),
        };
    }

    private static MatrixStageDefinition GetSelectedStage() {
        ProgressionProfile profile = ProgressionProfileRegistry.Current;
        return profile == null || selectedStageIndex < 0 || selectedStageIndex >= profile.Stages.Count
            ? null
            : profile.Stages[selectedStageIndex];
    }
}
