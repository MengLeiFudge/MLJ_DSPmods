using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FE.UI.Components;
using static FE.Utils.Utils;

namespace FE.UI.View.ProgressTask;

public static partial class MainTask {
    private const float RoutePanelWidth = 1082f;
    private const float RoutePanelHeight = 650f;
    private const float DetailPanelHeight = 190f;
    private const float DetailPanelWidth = 420f;
    private const float NodeSize = 44f;
    private const float LineThickness = 4f;

    private static readonly Color RoutePanelColor = new(0f, 0f, 0f, 0.34f);
    private static readonly Color DetailPanelColor = new(0f, 0f, 0f, 0.46f);
    private static readonly Color CenterPanelColor = new(0.08f, 0.1f, 0.14f, 0.88f);
    private static readonly Color LockedNodeColor = new(1f, 1f, 1f, 0.22f);
    private static readonly Color AvailableNodeColor = new(0.62f, 0.8f, 1f, 0.92f);
    private static readonly Color CompletedNodeColor = new(0.42f, 0.73f, 1f, 1f);
    private static readonly Color LockedLineColor = new(1f, 1f, 1f, 0.09f);
    private static readonly Color AvailableLineColor = new(0.42f, 0.73f, 1f, 0.35f);
    private static readonly Color CompletedLineColor = new(0.42f, 0.73f, 1f, 0.88f);
    private static readonly Color SelectedOutlineColor = new(1f, 0.72f, 0.31f, 0.32f);

    private sealed class RouteViewCache {
        public RectTransform Root;
        public Text[] BranchLabels;
        public NodeView[][] NodeViews;
        public Image[][] LinesToNodes;
    }

    private sealed class NodeView {
        public MyImageButton Button;
        public int BranchIndex;
        public int NodeIndex;
    }

    private enum NodeVisualState {
        Locked,
        Available,
        Completed,
    }

    // partial 类静态字段的初始化顺序不稳定，不能在这里提前依赖 RouteMaps。
    private static RouteViewCache[] routeViewsByMode = [];

    private static void BuildMilestonePage(MyConfigWindow wnd) {
        txtModeTitle = wnd.AddText2(0f, 12f, tab, "主线里程碑", 18, "txt-main-task-mode");
        txtModeTitle.supportRichText = true;
        txtModeTitle.color = Orange;

        txtOverallSummary = wnd.AddText2(0f, 42f, tab, "动态刷新", 14, "txt-main-task-overall");
        txtOverallSummary.supportRichText = true;

        txtBranchSummary = wnd.AddText2(260f, 42f, tab, "动态刷新", 14, "txt-main-task-branch");
        txtBranchSummary.supportRichText = true;

        roadmapPanel = CreatePanelRect("main-task-route-panel", tab, 0f, 82f, RoutePanelWidth, RoutePanelHeight, RoutePanelColor);
        centerPanel = CreatePanelRect("main-task-center-panel", roadmapPanel, 441f, 210f, 200f, 140f, CenterPanelColor);

        txtCenterTitle = MyWindow.AddText(24f, 26f, centerPanel, "主线里程碑", 18, "txt-main-task-center-title");
        txtCenterTitle.supportRichText = true;
        txtCenterTitle.color = Orange;

        txtCenterSummary = MyWindow.AddText(24f, 72f, centerPanel, "动态刷新", 14, "txt-main-task-center-summary");
        txtCenterSummary.supportRichText = true;

        detailPanel = CreatePanelRect("main-task-detail-panel", roadmapPanel, 24f, 432f, DetailPanelWidth, DetailPanelHeight, DetailPanelColor);

        txtDetailBranch = MyWindow.AddText(18f, 16f, detailPanel, "动态刷新", 14, "txt-main-task-detail-branch");
        txtDetailBranch.supportRichText = true;
        txtDetailBranch.color = Orange;

        txtDetailName = MyWindow.AddText(18f, 42f, detailPanel, "动态刷新", 18, "txt-main-task-detail-name");
        txtDetailName.supportRichText = true;

        txtDetailDesc = MyWindow.AddText(18f, 72f, detailPanel, "动态刷新", 14, "txt-main-task-detail-desc");
        txtDetailDesc.supportRichText = true;
        txtDetailDesc.rectTransform.sizeDelta = new Vector2(380f, 52f);

        txtDetailCondition = MyWindow.AddText(18f, 106f, detailPanel, "动态刷新", 13, "txt-main-task-detail-condition");
        txtDetailCondition.supportRichText = true;
        txtDetailCondition.rectTransform.sizeDelta = new Vector2(384f, 28f);

        btnDetailRewardIcon = wnd.AddImageButton(18f, 148f, detailPanel, null, "btn-main-task-detail-reward").WithSize(40f, 40f);
        txtDetailReward = MyWindow.AddText(68f, 144f, detailPanel, "动态刷新", 13, "txt-main-task-detail-reward");
        txtDetailReward.supportRichText = true;
        txtDetailReward.rectTransform.sizeDelta = new Vector2(136f, 28f);

        txtDetailState = MyWindow.AddText(216f, 144f, detailPanel, "动态刷新", 13, "txt-main-task-detail-state");
        txtDetailState.supportRichText = true;
        txtDetailState.rectTransform.sizeDelta = new Vector2(176f, 28f);
    }

    private static void RefreshMilestonePage() {
        int modeIndex = GetModeIndex();
        EnsureRouteViewCacheCapacity();
        EnsureRouteViewBuilt(modeIndex);

        for (int i = 0; i < routeViewsByMode.Length; i++) {
            if (routeViewsByMode[i]?.Root != null) {
                routeViewsByMode[i].Root.gameObject.SetActive(i == modeIndex);
            }
        }

        RouteMap route = GetRouteByModeIndex(modeIndex);
        int completedNodes = CountCompletedNodes(modeIndex);
        int totalNodes = CountTotalNodes(modeIndex);
        int completedBranches = CountCompletedBranches(modeIndex);

        txtModeTitle.text = route.RouteName.Translate().WithColor(Orange);
        txtOverallSummary.text = string.Format("路线总进度".Translate(), completedNodes, totalNodes).WithColor(Orange);
        txtBranchSummary.text = string.Format("分支完成数".Translate(), completedBranches, route.Branches.Length).WithColor(Blue);
        txtCenterTitle.text = route.CenterTitle.Translate().WithColor(Orange);
        txtCenterSummary.text = $"{completedNodes}/{totalNodes}".WithColor(completedNodes >= totalNodes ? Orange : Blue);

        EnsureSelectedNode(modeIndex);
        RefreshRouteView(modeIndex);
        UpdateDetailPanel(modeIndex);
    }

    private static void EnsureRouteViewCacheCapacity() {
        if (routeViewsByMode == null || routeViewsByMode.Length != RouteMaps.Length) {
            routeViewsByMode = new RouteViewCache[RouteMaps.Length];
        }
    }

    private static void EnsureRouteViewBuilt(int modeIndex) {
        if (routeViewsByMode[modeIndex] != null) {
            return;
        }

        RouteMap route = GetRouteByModeIndex(modeIndex);
        RectTransform root = CreateFillRect($"main-task-route-root-{modeIndex}", roadmapPanel);
        root.gameObject.SetActive(false);

        RouteViewCache cache = new() {
            Root = root,
            BranchLabels = new Text[route.Branches.Length],
            NodeViews = new NodeView[route.Branches.Length][],
            LinesToNodes = new Image[route.Branches.Length][],
        };

        for (int branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
            TaskBranch branch = route.Branches[branchIndex];
            Text branchLabel = MyWindow.AddText(branch.LabelPosition.x, branch.LabelPosition.y, root, branch.Name.Translate(), 14,
                $"txt-main-task-branch-{modeIndex}-{branchIndex}");
            branchLabel.supportRichText = true;
            branchLabel.color = White;
            cache.BranchLabels[branchIndex] = branchLabel;

            cache.NodeViews[branchIndex] = new NodeView[branch.Nodes.Length];
            cache.LinesToNodes[branchIndex] = new Image[branch.Nodes.Length];
            for (int nodeIndex = 0; nodeIndex < branch.Nodes.Length; nodeIndex++) {
                TaskNode node = branch.Nodes[nodeIndex];
                if (nodeIndex == 0) {
                    cache.LinesToNodes[branchIndex][nodeIndex] = CreateLine(root, GetCenterAnchor(node.Position), node.Position);
                }
                else {
                    cache.LinesToNodes[branchIndex][nodeIndex] = CreateLine(root, branch.Nodes[nodeIndex - 1].Position, node.Position);
                }

                MyImageButton nodeButton = MyImageButton.CreateImageButton(node.Position.x, node.Position.y, root, null, NodeSize, NodeSize);
                nodeButton.gameObject.name = $"btn-main-task-node-{modeIndex}-{branchIndex}-{nodeIndex}";
                nodeButton.spriteImage.raycastTarget = true;
                nodeButton.backgroundImage.raycastTarget = false;
                nodeButton.countText.gameObject.SetActive(false);
                nodeButton.backgroundImage.color = Color.clear;

                if (LDB.items.Exist(node.IconItemId)) {
                    nodeButton.spriteImage.sprite = LDB.items.Select(node.IconItemId).iconSprite;
                }
                nodeButton.WithClickEvent(() => SelectNode(modeIndex, branchIndex, nodeIndex), () => SelectNode(modeIndex, branchIndex, nodeIndex));
                AttachHoverSelection(nodeButton, () => SelectNode(modeIndex, branchIndex, nodeIndex));

                cache.NodeViews[branchIndex][nodeIndex] = new NodeView {
                    Button = nodeButton,
                    BranchIndex = branchIndex,
                    NodeIndex = nodeIndex,
                };
            }
        }

        routeViewsByMode[modeIndex] = cache;
    }

    private static void RefreshRouteView(int modeIndex) {
        RouteMap route = GetRouteByModeIndex(modeIndex);
        RouteViewCache cache = routeViewsByMode[modeIndex];
        int selectedBranch = selectedBranchByMode[modeIndex];
        int selectedNode = selectedNodeByMode[modeIndex];

        for (int branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
            bool branchCompleted = true;
            for (int nodeIndex = 0; nodeIndex < route.Branches[branchIndex].Nodes.Length; nodeIndex++) {
                if (!completedByMode[modeIndex][branchIndex][nodeIndex]) {
                    branchCompleted = false;
                    break;
                }
            }
            cache.BranchLabels[branchIndex].color = branchCompleted ? Orange : new Color(1f, 1f, 1f, 0.72f);

            for (int nodeIndex = 0; nodeIndex < route.Branches[branchIndex].Nodes.Length; nodeIndex++) {
                TaskNode node = route.Branches[branchIndex].Nodes[nodeIndex];
                NodeView view = cache.NodeViews[branchIndex][nodeIndex];
                Image line = cache.LinesToNodes[branchIndex][nodeIndex];
                NodeVisualState visualState = GetNodeVisualState(modeIndex, branchIndex, nodeIndex);
                UpdateNodeVisual(view.Button, visualState, branchIndex == selectedBranch && nodeIndex == selectedNode);
                UpdateLineVisual(line, modeIndex, branchIndex, nodeIndex);
                UpdateNodeTip(view.Button, node, modeIndex, branchIndex, nodeIndex);
            }
        }
    }

    private static void UpdateNodeVisual(MyImageButton button, NodeVisualState visualState, bool selected) {
        Color iconColor = visualState switch {
            NodeVisualState.Completed => CompletedNodeColor,
            NodeVisualState.Available => AvailableNodeColor,
            _ => LockedNodeColor,
        };
        button.spriteImage.color = iconColor;
        button.backgroundImage.color = selected ? SelectedOutlineColor : Color.clear;
    }

    private static void UpdateLineVisual(Image line, int modeIndex, int branchIndex, int nodeIndex) {
        NodeVisualState state = GetNodeVisualState(modeIndex, branchIndex, nodeIndex);
        line.color = state switch {
            NodeVisualState.Completed => CompletedLineColor,
            NodeVisualState.Available => AvailableLineColor,
            _ => LockedLineColor,
        };
    }

    private static void UpdateNodeTip(MyImageButton button, TaskNode node, int modeIndex, int branchIndex, int nodeIndex) {
        string progressText = GetNodeProgressText(modeIndex, branchIndex, nodeIndex);
        string rewardText = GetRewardText(node);

        button.uiButton.tips.type = UIButton.ItemTipType.Other;
        button.uiButton.tips.itemId = 0;
        button.uiButton.tips.topLevel = true;
        button.uiButton.tips.delay = 0.25f;
        button.uiButton.tips.corner = 2;
        button.uiButton.tips.tipTitle = node.Name.Translate();
        button.uiButton.tips.tipText =
            $"{node.Desc.Translate()}\n\n{"节点详情-条件".Translate()} {progressText}\n{"节点详情-奖励".Translate()} {rewardText}\n{"节点详情-状态".Translate()} {GetNodeStateText(modeIndex, branchIndex, nodeIndex)}";
        button.uiButton.UpdateTip();
    }

    private static void UpdateDetailPanel(int modeIndex) {
        RouteMap route = GetRouteByModeIndex(modeIndex);
        int branchIndex = selectedBranchByMode[modeIndex];
        int nodeIndex = selectedNodeByMode[modeIndex];
        TaskBranch branch = route.Branches[branchIndex];
        TaskNode node = branch.Nodes[nodeIndex];

        txtDetailBranch.text = branch.Name.Translate().WithColor(Orange);
        txtDetailName.text = node.Name.Translate().WithColor(GetDetailTitleColor(modeIndex, branchIndex, nodeIndex));
        txtDetailDesc.text = node.Desc.Translate();
        txtDetailCondition.text = $"{ "节点详情-条件".Translate() } {GetNodeProgressText(modeIndex, branchIndex, nodeIndex)}";
        txtDetailState.text = $"{ "节点详情-状态".Translate() } {GetNodeStateText(modeIndex, branchIndex, nodeIndex)}";

        bool hasReward = node.RewardItemId > 0 && node.RewardCount > 0 && LDB.items.Exist(node.RewardItemId);
        btnDetailRewardIcon.gameObject.SetActive(hasReward);
        if (hasReward) {
            btnDetailRewardIcon.Proto = LDB.items.Select(node.RewardItemId);
            btnDetailRewardIcon.SetCount(node.RewardCount);
        }
        else {
            btnDetailRewardIcon.Proto = null;
            btnDetailRewardIcon.ClearCountText();
        }
        txtDetailReward.text = $"{ "节点详情-奖励".Translate() } {GetRewardText(node)}".WithColor(Blue);
    }

    private static Color GetDetailTitleColor(int modeIndex, int branchIndex, int nodeIndex) {
        return GetNodeVisualState(modeIndex, branchIndex, nodeIndex) switch {
            NodeVisualState.Completed => Orange,
            NodeVisualState.Available => Blue,
            _ => Gray,
        };
    }

    private static string GetNodeProgressText(int modeIndex, int branchIndex, int nodeIndex) {
        if (!IsNodeUnlocked(modeIndex, branchIndex, nodeIndex)) {
            return "节点详情-前置未完成".Translate();
        }
        if (completedByMode[modeIndex][branchIndex][nodeIndex]) {
            return "节点状态-已完成".Translate();
        }
        try {
            return GetRouteByModeIndex(modeIndex).Branches[branchIndex].Nodes[nodeIndex].ProgressText();
        }
        catch (Exception ex) {
            return $"条件检查失败：{ex.Message}";
        }
    }

    private static string GetNodeStateText(int modeIndex, int branchIndex, int nodeIndex) {
        return GetNodeVisualState(modeIndex, branchIndex, nodeIndex) switch {
            NodeVisualState.Completed => "节点状态-已完成".Translate(),
            NodeVisualState.Available => "节点状态-进行中".Translate(),
            _ => "节点状态-未解锁".Translate(),
        };
    }

    private static string GetRewardText(TaskNode node) {
        if (node.RewardItemId > 0 && LDB.items.Exist(node.RewardItemId)) {
            return $"{LDB.items.Select(node.RewardItemId).name} x{node.RewardCount}";
        }
        return "无".Translate();
    }

    private static void SelectNode(int modeIndex, int branchIndex, int nodeIndex) {
        selectedBranchByMode[modeIndex] = branchIndex;
        selectedNodeByMode[modeIndex] = nodeIndex;
        if (modeIndex == GetModeIndex() && detailPanel != null) {
            RefreshRouteView(modeIndex);
            UpdateDetailPanel(modeIndex);
        }
    }

    private static void AttachHoverSelection(MyImageButton button, Action onHover) {
        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null) {
            trigger = button.gameObject.AddComponent<EventTrigger>();
        }
        EventTrigger.Entry pointerEnter = new() { eventID = EventTriggerType.PointerEnter };
        pointerEnter.callback.AddListener(_ => onHover?.Invoke());
        trigger.triggers.Add(pointerEnter);
    }

    private static Image CreateLine(RectTransform parent, Vector2 startNodePosition, Vector2 endNodePosition) {
        Vector2 startCenter = startNodePosition + new Vector2(NodeSize / 2f, NodeSize / 2f);
        Vector2 endCenter = endNodePosition + new Vector2(NodeSize / 2f, NodeSize / 2f);

        Image line = new GameObject("main-task-route-line").AddComponent<Image>();
        line.color = LockedLineColor;
        line.raycastTarget = false;
        RectTransform rect = line.rectTransform;
        rect.SetParent(parent, false);

        if (Math.Abs(startCenter.x - endCenter.x) >= Math.Abs(startCenter.y - endCenter.y)) {
            NormalizeRectWithTopLeft(line, Math.Min(startCenter.x, endCenter.x), startCenter.y - LineThickness / 2f, parent);
            rect.sizeDelta = new Vector2(Math.Abs(startCenter.x - endCenter.x), LineThickness);
        }
        else {
            NormalizeRectWithTopLeft(line, startCenter.x - LineThickness / 2f, Math.Min(startCenter.y, endCenter.y), parent);
            rect.sizeDelta = new Vector2(LineThickness, Math.Abs(startCenter.y - endCenter.y));
        }
        return line;
    }

    private static Vector2 GetCenterAnchor(Vector2 nodePosition) {
        return new Vector2(519f, 258f);
    }

    private static RectTransform CreatePanelRect(string name, RectTransform parent, float left, float top, float width,
        float height, Color color) {
        Image image = new GameObject(name).AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        NormalizeRectWithTopLeft(image, left, top, parent);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static RectTransform CreateFillRect(string name, RectTransform parent) {
        RectTransform rect = new GameObject(name).AddComponent<RectTransform>();
        NormalizeRectWithMargin(rect, 0f, 0f, 0f, 0f, parent);
        return rect;
    }
}
