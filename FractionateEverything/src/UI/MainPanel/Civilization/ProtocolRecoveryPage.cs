using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using FE.Compatibility.Nebula;
using FE.Logic.Civilization.Protocols;
using FE.Logic.DataCenter;
using FE.Logic.Fractionation.Recipes;
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
/// 选择广域、方向或锚定策略与目标，执行单次/批量检索；查看 100% 协议的 Memory 与实物逐项缺口后正式解锁。
/// 所有检索与解锁命令走 ProtocolRetrievalService / ProtocolUnlockService；UI 不自行扣款或复制解锁规则。
/// </summary>
public static class ProtocolRecoveryPage {
    private const float ListContentBaseHeight = 360f;
    private const float ListLineHeight = 22f;

    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static Text summaryText;
    private static Text protocolListText;
    private static Text unlockDetailText;
    private static Text resultText;
    private static UIButton strategyButton;
    private static UIButton targetButton;
    private static UIButton unlockTargetButton;
    private static UIButton retrieveButton;
    private static UIButton batchButton;
    private static UIButton formalUnlockButton;

    private static ProtocolRetrievalMode selectedMode;
    private static ERecipe selectedDirectionalRecipeType;
    private static RecipeKey selectedAnchoredRecipeKey;
    private static bool hasSelectedAnchoredRecipe;
    private static RecipeKey selectedUnlockRecipeKey;
    private static bool hasSelectedUnlockRecipe;
    private static string lastResult = "尚未执行检索";

    public static void AddTranslations() {
        Register("文明协议恢复", "Protocol Recovery");
        Register("协议恢复摘要",
            "Choose broad, directional, or anchored recovery; spend Frag, then formally unlock with Memory and required items.",
            "选择广域、方向或锚定检索；消耗残片推进完整度，再用记忆源点与实物正式解锁。");
        Register("执行单次", "Retrieve Once");
        Register("执行默认批量", "Retrieve Batch");
        Register("切换策略", "Cycle Strategy");
        Register("切换检索目标", "Cycle Retrieval Target");
        Register("正式解锁", "Formal Unlock");
        Register("切换解锁目标", "Cycle Unlock Target");
        Register("广域检索", "Broad Retrieval");
        Register("方向检索", "Directional Retrieval");
        Register("锚定检索", "Anchored Retrieval");
        Register("残片不足", "Insufficient Frag");
        Register("记忆源点不足", "Insufficient Memory");
        Register("所需实物不足", "Insufficient required items");
        Register("没有可检索协议", "No retrievable protocol");
        Register("检索请求无效", "Invalid retrieval request");
        Register("无额外目标", "No additional target");
        Register("未识别协议", "Unidentified protocol");
        Register("未发现", "Undiscovered");
        Register("未具备资格", "Not eligible");
        Register("可购买", "Purchase ready");
        Register("已正式解锁", "Formally unlocked");
        Register("资格未满", "Not purchase eligible");
        Register("特殊前置不足", "Prerequisite not met");
        Register("等待主机", "Awaiting host");
        Register("原子解锁成功", "Formal unlock succeeded");
        Register("协议恢复-目录标题", "Protocol Catalog");
        Register("协议恢复-解锁详情标题", "Formal Unlock Detail");
        Register("协议恢复-未选择解锁目标", "No unlock target selected.");
        Register("协议恢复-协议目录为空", "Protocol catalog is empty.");
        Register("协议恢复-无解锁候选", "No protocol has reached 100% completeness yet.");
        Register("协议恢复-未达购买资格", "Below 100% completeness.");
        Register("协议恢复-记忆源点标签", "Memory");
        Register("协议恢复-持有", "held");
        Register("协议恢复-缺口", "short");
        Register("协议恢复-充足", "ready");
        Register("协议恢复-客户端说明", "Client actions are forwarded to and settled by the host.",
            "客户端：检索与正式解锁请求由主机结算。");
        Register("协议恢复-检索次数", "Retrievals", "检索次数");
        Register("协议恢复-基础成本", "Base cost", "基础成本");
        Register("协议恢复-失败保底", "Failure streak", "失败保底");
        Register("协议恢复-深层解析", "Deep analysis", "深层解析");
        Register("协议恢复-解锁状态", "State", "状态");
        Register("协议恢复-成本缺口", "Cost & gaps", "成本与缺口");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        tab = trans;
        float listContentHeight =
            Mathf.Max(ListContentBaseHeight, ProtocolCatalog.All.Count * ListLineHeight + 16f);
        BuildLayout(wnd, tab,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), Px(172f), 1, Px(PageLayout.FooterHeight)],
                rowGap: PageLayout.Gap,
                children: [
                    Header("文明协议恢复", "协议恢复摘要", pos: (0, 0), objectName: "protocol-recovery-header",
                        onBuilt: refs => header = refs),
                    ContentCard(pos: (1, 0), objectName: "protocol-recovery-control-card", strong: true,
                        rows: [1, Px(32f), Px(32f)], rowGap: 8f,
                        children: [
                            TextNode("", 13, White, anchor: TextAnchor.UpperLeft, wrap: true, pos: (0, 0),
                                onBuilt: text => {
                                    summaryText = text;
                                    text.supportRichText = true;
                                }, objectName: "protocol-recovery-summary"),
                            Grid(pos: (1, 0), cols: [1, 1, 1], columnGap: 8f,
                                children: [
                                    ButtonNode("切换策略", CycleStrategy, pos: (0, 0),
                                        onBuilt: button => strategyButton = button,
                                        objectName: "protocol-recovery-strategy"),
                                    ButtonNode("切换检索目标", CycleTarget, pos: (0, 1),
                                        onBuilt: button => targetButton = button,
                                        objectName: "protocol-recovery-target"),
                                    ButtonNode("切换解锁目标", CycleUnlockTarget, pos: (0, 2),
                                        onBuilt: button => unlockTargetButton = button,
                                        objectName: "protocol-recovery-unlock-target"),
                                ]),
                            Grid(pos: (2, 0), cols: [1, 1, 1], columnGap: 8f,
                                children: [
                                    ButtonNode("执行单次", Retrieve, pos: (0, 0),
                                        onBuilt: button => retrieveButton = button,
                                        objectName: "protocol-recovery-retrieve"),
                                    ButtonNode("执行默认批量", RetrieveBatch, pos: (0, 1),
                                        onBuilt: button => batchButton = button,
                                        objectName: "protocol-recovery-batch"),
                                    ButtonNode("正式解锁", FormalUnlock, pos: (0, 2),
                                        onBuilt: button => formalUnlockButton = button,
                                        objectName: "protocol-recovery-formal-unlock"),
                                ]),
                        ]),
                    Grid(pos: (2, 0), cols: [1, 1], columnGap: PageLayout.Gap, objectName: "protocol-recovery-body",
                        children: [
                            ContentCard(pos: (0, 0), objectName: "protocol-list-card", strong: true,
                                rows: [Px(28f), 1], rowGap: 8f,
                                children: [
                                    CardTitleNode("协议恢复-目录标题", pos: (0, 0),
                                        objectName: "protocol-list-title"),
                                    ScrollableContentCard(listContentHeight, pos: (1, 0),
                                        objectName: "protocol-list-scroll",
                                        children: [
                                            TextNode("", 13, White, anchor: TextAnchor.UpperLeft, wrap: true,
                                                pos: (0, 0),
                                                onBuilt: text => {
                                                    protocolListText = text;
                                                    text.supportRichText = true;
                                                }, objectName: "protocol-list-text"),
                                        ]),
                                ]),
                            ContentCard(pos: (0, 1), objectName: "protocol-unlock-detail-card", strong: true,
                                rows: [Px(28f), 1], rowGap: 8f,
                                children: [
                                    CardTitleNode("协议恢复-解锁详情标题", pos: (0, 0),
                                        objectName: "protocol-unlock-detail-title"),
                                    TextNode("", 13, White, anchor: TextAnchor.UpperLeft, wrap: true, pos: (1, 0),
                                        onBuilt: text => {
                                            unlockDetailText = text;
                                            text.supportRichText = true;
                                        }, objectName: "protocol-unlock-detail-text"),
                                ]),
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

        ProtocolRetrievalGlobalSnapshot snapshot = ProtocolRetrievalService.GetGlobalSnapshot();
        SynchronizeTargets();
        ProtocolRetrievalRequest request = GetRequest();
        bool canRetrieve = ProtocolRetrievalService.CanRetrieve(request, out _);
        bool canUnlock = hasSelectedUnlockRecipe && ProtocolUnlockService.CanUnlock(selectedUnlockRecipeKey, out _);

        header.Title.text = "文明协议恢复".Translate().WithColor(Orange);
        header.Summary.text = "协议恢复摘要".Translate().WithColor(White);

        summaryText.text = BuildSummary(snapshot);
        protocolListText.text = BuildProtocolList(snapshot.Protocols);
        unlockDetailText.text = BuildUnlockDetail(snapshot);
        resultText.text = lastResult.WithColor(Gray);

        strategyButton.SetText(GetModeText());
        targetButton.SetText(GetTargetText());
        targetButton.button.interactable = selectedMode != ProtocolRetrievalMode.Broad && HasSelectableTarget();
        unlockTargetButton.SetText("切换解锁目标".Translate());
        unlockTargetButton.button.interactable = GetUnlockCandidates(snapshot).Count > 0;
        retrieveButton.button.interactable = canRetrieve;
        batchButton.button.interactable = canRetrieve;
        formalUnlockButton.button.interactable = canUnlock;
        formalUnlockButton.SetText(SelectedUnlockFormallyUnlocked(snapshot)
            ? "已正式解锁".Translate()
            : "正式解锁".Translate());
    }

    private static string BuildSummary(ProtocolRetrievalGlobalSnapshot snapshot) {
        var builder = new StringBuilder();
        builder.Append($"{"残片".Translate()} {snapshot.Fragments}    ")
            .Append($"{"记忆源点".Translate()} {snapshot.Memory}\n");
        builder.Append(
            $"{"协议恢复-检索次数".Translate()} {snapshot.TotalRetrievalCount}  ·  ")
            .Append($"{"协议恢复-基础成本".Translate()} {snapshot.NextBaseCost}  ·  ")
            .Append(GetModeCostText()).Append("  ·  ")
            .Append($"{"协议恢复-失败保底".Translate()} {snapshot.FailureStreak}/4  ·  ")
            .Append($"{"协议恢复-深层解析".Translate()} {snapshot.DeepAnalysisProgress}/{snapshot.NextDeepPointCost}");
        if (NebulaMultiplayerModAPI.IsClient) {
            builder.Append('\n').Append("协议恢复-客户端说明".Translate());
        }
        return builder.ToString();
    }

    private static string BuildProtocolList(IReadOnlyList<ProtocolRetrievalProtocolSnapshot> protocols) {
        if (protocols.Count == 0) {
            return "协议恢复-协议目录为空".Translate().WithColor(Gray);
        }

        var builder = new StringBuilder();
        for (int i = 0; i < protocols.Count; i++) {
            if (i > 0) {
                builder.Append('\n');
            }
            ProtocolRetrievalProtocolSnapshot protocol = protocols[i];
            string name = protocol.Discovered ? protocol.DisplayName : "未识别协议".Translate();
            string state;
            if (protocol.FormallyUnlocked) {
                state = "已正式解锁".Translate().WithColor(Green);
            } else if (!protocol.Discovered) {
                state = "未发现".Translate().WithColor(Gray);
            } else {
                string completeness = $"{protocol.Completeness}%";
                completeness = protocol.PurchaseEligible
                    ? completeness.WithColor(Orange)
                    : completeness.WithColor(White);
                state = protocol.PurchaseEligible
                    ? $"{completeness} [{"可购买".Translate().WithColor(Orange)}]"
                    : completeness;
            }
            string eligibility = protocol.Eligible
                ? string.Empty
                : $"[{"未具备资格".Translate().WithColor(Red)}] ";
            builder.Append(i + 1).Append(". ").Append(eligibility).Append(name).Append("  ").Append(state);
        }
        return builder.ToString();
    }

    private static string BuildUnlockDetail(ProtocolRetrievalGlobalSnapshot snapshot) {
        if (!hasSelectedUnlockRecipe) {
            return "协议恢复-未选择解锁目标".Translate().WithColor(Gray);
        }

        if (!TryFindProtocol(snapshot, selectedUnlockRecipeKey, out ProtocolRetrievalProtocolSnapshot protocol)) {
            return "协议恢复-未选择解锁目标".Translate().WithColor(Gray);
        }

        ProtocolDefinition definition = ProtocolCatalog.Get(selectedUnlockRecipeKey);
        var builder = new StringBuilder();
        builder.Append(protocol.DisplayName.WithColor(Orange)).Append('\n');
        builder.Append($"{"协议恢复-解锁状态".Translate()} {GetUnlockStateLabel(protocol, definition)}");

        if (definition == null || !protocol.Discovered) {
            return builder.ToString();
        }

        builder.Append("\n\n").Append("协议恢复-成本缺口".Translate().WithColor(White));
        AppendCostLine(builder, "协议恢复-记忆源点标签".Translate(),
            ProtocolUnlockService.GetMemoryCost(definition), IFE记忆源点);
        foreach ((int itemId, long count) in ProtocolUnlockService.GetPhysicalCosts(definition)) {
            AppendCostLine(builder, GetItemName(itemId), count, itemId);
        }
        return builder.ToString();
    }

    private static void AppendCostLine(StringBuilder builder, string label, long cost, int itemId) {
        long held = DataCenterInventory.GetModDataItemCount(itemId);
        bool sufficient = held >= cost;
        string heldPart = held.ToString().WithColor(sufficient ? Green : Gray);
        string status = sufficient
            ? "协议恢复-充足".Translate().WithColor(Green)
            : $"{"协议恢复-缺口".Translate()} {(cost - held).ToString().WithColor(Red)}";
        builder.Append("\n  · ").Append(label).Append("  ")
            .Append(cost.ToString().WithColor(Orange)).Append(" / ")
            .Append(heldPart).Append("  ").Append(status);
    }

    private static string GetUnlockStateLabel(ProtocolRetrievalProtocolSnapshot protocol,
        ProtocolDefinition definition) {
        if (protocol.FormallyUnlocked) {
            return "已正式解锁".Translate().WithColor(Green);
        }
        if (!protocol.Discovered) {
            return "未发现".Translate().WithColor(Gray);
        }
        if (protocol.Completeness < 100) {
            return "协议恢复-未达购买资格".Translate().WithColor(Gray);
        }
        if (definition == null) {
            return "未识别协议".Translate().WithColor(Red);
        }
        ProtocolUnlockService.CanUnlock(definition, out ProtocolUnlockStopReason stopReason);
        return stopReason switch {
            ProtocolUnlockStopReason.PrerequisiteNotMet => "特殊前置不足".Translate().WithColor(Red),
            ProtocolUnlockStopReason.InsufficientMemory => "记忆源点不足".Translate().WithColor(Red),
            ProtocolUnlockStopReason.InsufficientSamples => "所需实物不足".Translate().WithColor(Red),
            ProtocolUnlockStopReason.AlreadyUnlocked => "已正式解锁".Translate().WithColor(Green),
            ProtocolUnlockStopReason.None => NebulaMultiplayerModAPI.IsClient
                ? "等待主机".Translate().WithColor(Blue)
                : "可购买".Translate().WithColor(Orange),
            _ => "资格未满".Translate().WithColor(Red),
        };
    }

    private static bool SelectedUnlockFormallyUnlocked(ProtocolRetrievalGlobalSnapshot snapshot) {
        if (!hasSelectedUnlockRecipe) {
            return false;
        }
        return TryFindProtocol(snapshot, selectedUnlockRecipeKey, out ProtocolRetrievalProtocolSnapshot protocol)
               && protocol.FormallyUnlocked;
    }

    private static bool TryFindProtocol(ProtocolRetrievalGlobalSnapshot snapshot, RecipeKey recipeKey,
        out ProtocolRetrievalProtocolSnapshot protocol) {
        foreach (ProtocolRetrievalProtocolSnapshot candidate in snapshot.Protocols) {
            if (candidate.RecipeKey.Equals(recipeKey)) {
                protocol = candidate;
                return true;
            }
        }
        protocol = default;
        return false;
    }

    private static string GetItemName(int itemId) {
        ItemProto item = LDB.items.Select(itemId);
        return item != null ? item.name : itemId.ToString();
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
        if (selectedMode == ProtocolRetrievalMode.Directional) {
            List<ERecipe> types = ProtocolRetrievalService.GetDirectionalRecipeTypes();
            if (types.Count == 0) {
                lastResult = "没有可检索协议".Translate();
            } else {
                int index = types.IndexOf(selectedDirectionalRecipeType);
                selectedDirectionalRecipeType = types[(index + 1) % types.Count];
                lastResult = $"已选择方向：{selectedDirectionalRecipeType.GetShortName()}。";
            }
        } else if (selectedMode == ProtocolRetrievalMode.Anchored) {
            List<ProtocolRetrievalProtocolSnapshot> candidates = ProtocolRetrievalService.GetAnchoredCandidates();
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

    private static void CycleUnlockTarget() {
        ProtocolRetrievalGlobalSnapshot snapshot = ProtocolRetrievalService.GetGlobalSnapshot();
        List<ProtocolRetrievalProtocolSnapshot> candidates = GetUnlockCandidates(snapshot);
        if (candidates.Count == 0) {
            hasSelectedUnlockRecipe = false;
            lastResult = "协议恢复-无解锁候选".Translate();
            UpdateUI();
            return;
        }
        int index = -1;
        if (hasSelectedUnlockRecipe) {
            for (int i = 0; i < candidates.Count; i++) {
                if (candidates[i].RecipeKey.Equals(selectedUnlockRecipeKey)) {
                    index = i;
                    break;
                }
            }
        }
        ProtocolRetrievalProtocolSnapshot target = candidates[(index + 1) % candidates.Count];
        selectedUnlockRecipeKey = target.RecipeKey;
        hasSelectedUnlockRecipe = true;
        lastResult = $"已选择解锁目标：{target.DisplayName}。";
        UpdateUI();
    }

    private static void Retrieve() {
        if (!ProtocolRetrievalService.TryRetrieve(GetRequest(), out ProtocolRetrievalResult result)) {
            lastResult = GetStopReasonText(result.StopReason);
            UpdateUI();
            return;
        }
        lastResult = FormatSingleResult(result);
        UpdateUI();
    }

    private static void RetrieveBatch() {
        ProtocolRetrievalBatchResult result = ProtocolRetrievalService.RetrieveBatch(GetRequest(),
            ProtocolRetrievalService.DefaultBatchCount);
        lastResult = FormatBatchResult(result);
        UpdateUI();
    }

    private static void FormalUnlock() {
        if (!hasSelectedUnlockRecipe) {
            lastResult = "协议恢复-无解锁候选".Translate();
            UpdateUI();
            return;
        }
        if (!ProtocolUnlockService.TryUnlock(selectedUnlockRecipeKey, out ProtocolUnlockStopReason stopReason)) {
            lastResult = GetUnlockStopReasonText(stopReason);
            UpdateUI();
            return;
        }
        lastResult = NebulaMultiplayerModAPI.IsClient
            ? "等待主机".Translate()
            : "原子解锁成功".Translate();
        UpdateUI();
    }

    private static ProtocolRetrievalRequest GetRequest() {
        return selectedMode switch {
            ProtocolRetrievalMode.Directional => ProtocolRetrievalRequest.Directional(selectedDirectionalRecipeType),
            ProtocolRetrievalMode.Anchored when hasSelectedAnchoredRecipe =>
                ProtocolRetrievalRequest.Anchored(selectedAnchoredRecipeKey),
            ProtocolRetrievalMode.Anchored => new ProtocolRetrievalRequest(ProtocolRetrievalMode.Anchored),
            _ => ProtocolRetrievalRequest.Broad(),
        };
    }

    private static void SynchronizeTargets() {
        ProtocolRetrievalGlobalSnapshot snapshot = ProtocolRetrievalService.GetGlobalSnapshot();

        List<ERecipe> directionalTypes = ProtocolRetrievalService.GetDirectionalRecipeTypes();
        if (directionalTypes.Count > 0 && !directionalTypes.Contains(selectedDirectionalRecipeType)) {
            selectedDirectionalRecipeType = directionalTypes[0];
        }

        List<ProtocolRetrievalProtocolSnapshot> anchoredCandidates = ProtocolRetrievalService.GetAnchoredCandidates();
        if (anchoredCandidates.Count == 0) {
            hasSelectedAnchoredRecipe = false;
        } else if (FindAnchoredCandidateIndex(anchoredCandidates) < 0) {
            selectedAnchoredRecipeKey = anchoredCandidates[0].RecipeKey;
            hasSelectedAnchoredRecipe = true;
        }

        List<ProtocolRetrievalProtocolSnapshot> unlockCandidates = GetUnlockCandidates(snapshot);
        if (unlockCandidates.Count == 0) {
            hasSelectedUnlockRecipe = false;
            return;
        }
        if (hasSelectedUnlockRecipe) {
            foreach (ProtocolRetrievalProtocolSnapshot protocol in unlockCandidates) {
                if (protocol.RecipeKey.Equals(selectedUnlockRecipeKey)) {
                    return;
                }
            }
        }
        selectedUnlockRecipeKey = unlockCandidates[0].RecipeKey;
        hasSelectedUnlockRecipe = true;
    }

    private static List<ProtocolRetrievalProtocolSnapshot> GetUnlockCandidates(
        ProtocolRetrievalGlobalSnapshot snapshot) {
        List<ProtocolRetrievalProtocolSnapshot> candidates = [];
        foreach (ProtocolRetrievalProtocolSnapshot protocol in snapshot.Protocols) {
            if (protocol.PurchaseEligible && !protocol.FormallyUnlocked) {
                candidates.Add(protocol);
            }
        }
        return candidates;
    }

    private static bool HasSelectableTarget() {
        return selectedMode == ProtocolRetrievalMode.Directional
            ? ProtocolRetrievalService.GetDirectionalRecipeTypes().Count > 0
            : ProtocolRetrievalService.GetAnchoredCandidates().Count > 0;
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
        int baseCost = ProtocolRetrievalService.GetCurrentBaseCost();
        int total = ProtocolRetrievalService.GetCurrentTotalCost(selectedMode);
        return selectedMode switch {
            ProtocolRetrievalMode.Directional => $"{"方向检索".Translate()} {total}（base {baseCost}+ceil/4）",
            ProtocolRetrievalMode.Anchored => $"{"锚定检索".Translate()} {total}（2×base）",
            _ => $"{"广域检索".Translate()} {total}",
        };
    }

    private static string GetTargetText() {
        if (selectedMode == ProtocolRetrievalMode.Broad) {
            return "无额外目标".Translate();
        }
        if (selectedMode == ProtocolRetrievalMode.Directional) {
            return $"{"方向检索".Translate()}：{selectedDirectionalRecipeType.GetShortName()}";
        }
        foreach (ProtocolRetrievalProtocolSnapshot candidate in ProtocolRetrievalService.GetAnchoredCandidates()) {
            if (hasSelectedAnchoredRecipe && candidate.RecipeKey.Equals(selectedAnchoredRecipeKey)) {
                return $"{"锚定检索".Translate()}：{candidate.DisplayName}";
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
            return $"检索未获得有效响应，返还 {result.AwardedFragments} 残片（仅 base 20%），保底进度已增加。";
        }
        foreach (ProtocolRetrievalProtocolSnapshot protocol in ProtocolRetrievalService.GetGlobalSnapshot().Protocols) {
            if (protocol.RecipeKey.Equals(result.RecipeKey)) {
                string suffix = result.CurrentCompleteness >= 100 && result.PreviousCompleteness < 100
                    ? "（已取得购买资格）"
                    : string.Empty;
                return $"{protocol.DisplayName}：{result.PreviousCompleteness}% -> {result.CurrentCompleteness}%{suffix}";
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
            ProtocolRetrievalStopReason.NoCandidate => "没有可检索协议".Translate(),
            ProtocolRetrievalStopReason.InsufficientFragments => "残片不足".Translate(),
            ProtocolRetrievalStopReason.AwaitingHost => "等待主机".Translate(),
            _ => "检索请求无效".Translate(),
        };
    }

    private static string GetUnlockStopReasonText(ProtocolUnlockStopReason stopReason) {
        return stopReason switch {
            ProtocolUnlockStopReason.NotPurchaseEligible => "协议恢复-未达购买资格".Translate(),
            ProtocolUnlockStopReason.PrerequisiteNotMet => "特殊前置不足".Translate(),
            ProtocolUnlockStopReason.InsufficientMemory => "记忆源点不足".Translate(),
            ProtocolUnlockStopReason.InsufficientSamples => "所需实物不足".Translate(),
            ProtocolUnlockStopReason.AlreadyUnlocked => "已正式解锁".Translate(),
            ProtocolUnlockStopReason.AwaitingHost => "等待主机".Translate(),
            _ => "检索请求无效".Translate(),
        };
    }
}
