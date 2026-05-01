using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FE.UI.Components;
using static FE.UI.Components.GridDsl;
using static FE.UI.Components.PageLayout;
using static FE.Utils.Utils;

namespace FE.UI.View.ProgressTask;

public static partial class MainTask {
    private const float RoutePanelWidth = 1082f;
    private const float RoutePanelHeight = 650f;
    private const float LeftColumnWidth = 156f;
    private const float StageColumnWidth = 154f;
    private const float StageHeaderHeight = 54f;
    private const float CategoryRowHeight = 56f;
    private const float NodeSize = 30f;
    private const float NodeGap = 6f;
    private const float NodeCellLeftPadding = 10f;
    private const float NodeCellTopPadding = 13f;
    private static readonly Vector2 NodeTipOffset = new(15f, -50f);

    private static readonly Color RoutePanelFillColor = new(20f / 255f, 24f / 255f, 30f / 255f, 0.5f);
    private static readonly Color RoutePanelBorderColor = new(1f, 1f, 1f, 0.10f);
    private static readonly Color LockedNodeColor = new(1f, 1f, 1f, 0.22f);
    private static readonly Color AvailableNodeColor = new(0.62f, 0.8f, 1f, 0.92f);
    private static readonly Color CompletedNodeColor = new(1f, 0.72f, 0.31f, 1f);
    private static readonly Color RowOddColor = new(1f, 1f, 1f, 0.035f);
    private static readonly Color RowEvenColor = new(1f, 1f, 1f, 0.018f);
    private static readonly Color HeaderFillColor = new(0.05f, 0.07f, 0.11f, 0.72f);
    private static readonly Color NodeBgLocked = new(0.08f, 0.10f, 0.14f, 0.55f);
    private static readonly Color NodeBgAvailable = new(0.08f, 0.16f, 0.28f, 0.7f);
    private static readonly Color NodeBgCompleted = new(0.18f, 0.13f, 0.05f, 0.78f);
    private static readonly Color NodeBorderSelected = new(1f, 0.72f, 0.31f, 0.72f);
    private static readonly Color NodeBorderAvailable = new(0.42f, 0.73f, 1f, 0.28f);

    private sealed class RouteViewCache {
        public RectTransform Root;
        public RectTransform ScrollContent;
        public ScrollRect Scroll;
        public Text[] BranchLabels;
        public Text[] StageLabels;
        public NodeView[][] NodeViews;
    }

    private sealed class NodeView {
        public MyImageButton Button;
        public Image Background;
        public Image BackgroundBorder;
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

    private static void BuildMilestonePage(MyWindow wnd) {
        BuildLayout(wnd, tab,
            Grid(
                rows: [Px(70f), Px(RoutePanelHeight)],
                rowGap: 12f,
                children: [
                    Grid(pos: (0, 0), rows: [Px(34f), Px(24f)], cols: [Px(250f), Px(260f), Fr(1)],
                        children: [
                            TextNode("主线里程碑", 20, Orange,
                                onBuilt: text => {
                                    txtModeTitle = text;
                                    text.supportRichText = true;
                                },
                                pos: (0, 0), objectName: "txt-main-task-mode"),
                            TextNode("动态刷新", 13,
                                onBuilt: text => {
                                    txtOverallSummary = text;
                                    text.supportRichText = true;
                                },
                                pos: (1, 0), objectName: "txt-main-task-overall"),
                            TextNode("动态刷新", 13,
                                onBuilt: text => {
                                    txtBranchSummary = text;
                                    text.supportRichText = true;
                                },
                                pos: (1, 1), objectName: "txt-main-task-branch"),
                            TextNode("节点详情-推荐说明", 13, Gray,
                                onBuilt: text => text.supportRichText = true,
                                pos: (1, 2), objectName: "txt-main-task-hint"),
                        ]),
                    Grid(pos: (1, 0), objectName: "main-task-route-panel",
                        onBuilt: root => {
                            roadmapPanel = root;
                            AddRoundedPanel(root, RoutePanelFillColor, RoutePanelBorderColor);
                        }),
                ]));
    }

    private static void AddRoundedPanel(RectTransform root, Color fillColor, Color borderColor) {
        Image fill = root.gameObject.AddComponent<Image>();
        fill.sprite = RoundedSpriteFactory.GetFillSprite();
        fill.type = Image.Type.Sliced;
        fill.color = fillColor;
        fill.raycastTarget = false;

        var borderObj = new GameObject("border", typeof(RectTransform), typeof(Image));
        RectTransform borderRect = borderObj.GetComponent<RectTransform>();
        borderRect.SetParent(root, false);
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;
        borderRect.localScale = Vector3.one;

        Image borderImg = borderObj.GetComponent<Image>();
        borderImg.sprite = RoundedSpriteFactory.GetBorderSprite();
        borderImg.type = Image.Type.Sliced;
        borderImg.color = borderColor;
        borderImg.raycastTarget = false;
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
        txtBranchSummary.text =
            string.Format("分支完成数".Translate(), completedBranches, route.Branches.Length).WithColor(Blue);

        EnsureSelectedNode(modeIndex);
        RefreshRouteView(modeIndex);
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
            StageLabels = new Text[route.Stages.Length],
            NodeViews = new NodeView[route.Branches.Length][],
        };

        RectTransform leftRoot = CreatePanelRect("main-task-left-fixed", root, 10f, 10f, LeftColumnWidth - 10f,
            RoutePanelHeight - 20f, Color.clear);
        AddRowBackground(leftRoot, 0f, StageHeaderHeight, HeaderFillColor);
        Text categoryTitle = MyWindow.AddText(12f, 18f, leftRoot, "类别".Translate(), 14,
            $"txt-main-task-category-title-{modeIndex}");
        categoryTitle.color = Orange;
        categoryTitle.supportRichText = true;

        for (int branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
            float y = StageHeaderHeight + branchIndex * CategoryRowHeight;
            AddRowBackground(leftRoot, 0f, y, branchIndex % 2 == 0 ? RowEvenColor : RowOddColor);
            TaskBranch branch = route.Branches[branchIndex];
            Text branchLabel = MyWindow.AddText(12f, y + 18f, leftRoot, branch.Name.Translate(), 13,
                $"txt-main-task-branch-{modeIndex}-{branchIndex}");
            branchLabel.supportRichText = true;
            branchLabel.color = White;
            cache.BranchLabels[branchIndex] = branchLabel;
        }

        RectTransform viewport = CreateViewport("main-task-scroll-viewport", root, LeftColumnWidth, 10f,
            RoutePanelWidth - LeftColumnWidth - 10f, RoutePanelHeight - 20f);
        float contentWidth = Math.Max(RoutePanelWidth - LeftColumnWidth - 12f, route.Stages.Length * StageColumnWidth);
        RectTransform content = CreateScrollContent("main-task-scroll-content", viewport, contentWidth,
            RoutePanelHeight - 20f);
        cache.ScrollContent = content;

        ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 34f;
        scroll.inertia = true;
        scroll.decelerationRate = 0.135f;
        cache.Scroll = scroll;

        BuildStageHeaders(route, cache, content, modeIndex);
        BuildMatrixRows(route, content);
        BuildNodeViews(route, cache, content, modeIndex);

        routeViewsByMode[modeIndex] = cache;
    }

    private static void BuildStageHeaders(RouteMap route, RouteViewCache cache, RectTransform content, int modeIndex) {
        AddRowBackground(content, 0f, 0f, HeaderFillColor, route.Stages.Length * StageColumnWidth, StageHeaderHeight);
        for (int stageIndex = 0; stageIndex < route.Stages.Length; stageIndex++) {
            StageColumn stage = route.Stages[stageIndex];
            float x = stageIndex * StageColumnWidth + 10f;
            MyImageButton icon = MyImageButton.CreateImageButton(x, 12f, content,
                LDB.items.Exist(stage.IconItemId) ? LDB.items.Select(stage.IconItemId) : null, 28f, 28f);
            icon.backgroundImage.color = Color.clear;
            icon.countText.gameObject.SetActive(false);

            Text label = MyWindow.AddText(x + 36f, 17f, content, stage.Name.Translate(), 13,
                $"txt-main-task-stage-{modeIndex}-{stageIndex}");
            label.supportRichText = true;
            label.color = Orange;
            cache.StageLabels[stageIndex] = label;
        }
    }

    private static void BuildMatrixRows(RouteMap route, RectTransform content) {
        float contentWidth = route.Stages.Length * StageColumnWidth;
        for (int branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
            float y = StageHeaderHeight + branchIndex * CategoryRowHeight;
            AddRowBackground(content, 0f, y, branchIndex % 2 == 0 ? RowEvenColor : RowOddColor, contentWidth,
                CategoryRowHeight);
            for (int stageIndex = 1; stageIndex < route.Stages.Length; stageIndex++) {
                AddColumnSeparator(content, stageIndex * StageColumnWidth, y, CategoryRowHeight);
            }
        }
    }

    private static void BuildNodeViews(RouteMap route, RouteViewCache cache, RectTransform content, int modeIndex) {
        for (int branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
            TaskBranch branch = route.Branches[branchIndex];
            cache.NodeViews[branchIndex] = new NodeView[branch.Nodes.Length];
            for (int nodeIndex = 0; nodeIndex < branch.Nodes.Length; nodeIndex++) {
                TaskNode node = branch.Nodes[nodeIndex];
                int stageIndex = Math.Max(0, Math.Min(route.Stages.Length - 1, node.StageIndex));
                int cellIndex = CountPreviousNodesInCell(branch, nodeIndex, stageIndex);
                int cellColumn = cellIndex % 3;
                int cellRow = cellIndex / 3;
                float x = stageIndex * StageColumnWidth + NodeCellLeftPadding + cellColumn * (NodeSize + NodeGap);
                float y = StageHeaderHeight + branchIndex * CategoryRowHeight + NodeCellTopPadding
                          + cellRow * (NodeSize + 2f);

                float bgSize = NodeSize + 6f;
                Image nodeBg = new GameObject("node-bg", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                nodeBg.sprite = RoundedSpriteFactory.GetFillSprite();
                nodeBg.type = Image.Type.Sliced;
                nodeBg.color = NodeBgLocked;
                nodeBg.raycastTarget = false;
                NormalizeRectWithTopLeft(nodeBg, x - 3f, y - 3f, content);
                nodeBg.rectTransform.sizeDelta = new Vector2(bgSize, bgSize);

                Image nodeBorder =
                    new GameObject("node-border", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                nodeBorder.sprite = RoundedSpriteFactory.GetBorderSprite();
                nodeBorder.type = Image.Type.Sliced;
                nodeBorder.color = Color.clear;
                nodeBorder.raycastTarget = false;
                NormalizeRectWithTopLeft(nodeBorder, x - 3f, y - 3f, content);
                nodeBorder.rectTransform.sizeDelta = new Vector2(bgSize, bgSize);

                int capturedModeIndex = modeIndex;
                int capturedBranchIndex = branchIndex;
                int capturedNodeIndex = nodeIndex;
                MyImageButton nodeButton = MyImageButton.CreateImageButton(x, y, content,
                    LDB.items.Exist(node.IconItemId) ? LDB.items.Select(node.IconItemId) : null, NodeSize, NodeSize);
                nodeButton.gameObject.name = $"btn-main-task-node-{modeIndex}-{branchIndex}-{nodeIndex}";
                nodeButton.spriteImage.raycastTarget = true;
                nodeButton.backgroundImage.raycastTarget = false;
                nodeButton.countText.gameObject.SetActive(false);
                nodeButton.backgroundImage.color = Color.clear;
                nodeButton.WithClickEvent(() => SelectNode(capturedModeIndex, capturedBranchIndex, capturedNodeIndex),
                    () => SelectNode(capturedModeIndex, capturedBranchIndex, capturedNodeIndex));
                AttachHoverSelection(nodeButton,
                    () => SelectNode(capturedModeIndex, capturedBranchIndex, capturedNodeIndex));

                cache.NodeViews[branchIndex][nodeIndex] = new NodeView {
                    Button = nodeButton,
                    Background = nodeBg,
                    BackgroundBorder = nodeBorder,
                    BranchIndex = branchIndex,
                    NodeIndex = nodeIndex,
                };
            }
        }
    }

    private static int CountPreviousNodesInCell(TaskBranch branch, int nodeIndex, int stageIndex) {
        int count = 0;
        for (int i = 0; i < nodeIndex; i++) {
            if (branch.Nodes[i].StageIndex == stageIndex) {
                count++;
            }
        }
        return count;
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
            cache.BranchLabels[branchIndex].color = branchCompleted ? Orange : White;

            for (int nodeIndex = 0; nodeIndex < route.Branches[branchIndex].Nodes.Length; nodeIndex++) {
                TaskNode node = route.Branches[branchIndex].Nodes[nodeIndex];
                NodeView view = cache.NodeViews[branchIndex][nodeIndex];
                NodeVisualState visualState = GetNodeVisualState(modeIndex, branchIndex, nodeIndex);
                UpdateNodeVisual(view, visualState, branchIndex == selectedBranch && nodeIndex == selectedNode);
                UpdateNodeTip(view.Button, node, modeIndex, branchIndex, nodeIndex);
            }
        }
    }

    private static void UpdateNodeVisual(NodeView view, NodeVisualState visualState, bool selected) {
        Color iconColor = visualState switch {
            NodeVisualState.Completed => CompletedNodeColor,
            NodeVisualState.Available => AvailableNodeColor,
            _ => LockedNodeColor,
        };
        view.Button.spriteImage.color = iconColor;
        view.Button.backgroundImage.color = Color.clear;

        if (view.Background != null) {
            view.Background.color = visualState switch {
                NodeVisualState.Completed => NodeBgCompleted,
                NodeVisualState.Available => NodeBgAvailable,
                _ => NodeBgLocked,
            };
        }

        if (view.BackgroundBorder != null) {
            view.BackgroundBorder.color = selected ? NodeBorderSelected :
                visualState == NodeVisualState.Available ? NodeBorderAvailable : Color.clear;
        }
    }

    private static void UpdateNodeTip(MyImageButton button, TaskNode node, int modeIndex, int branchIndex,
        int nodeIndex) {
        RouteMap route = GetRouteByModeIndex(modeIndex);
        string progressText = GetNodeProgressText(modeIndex, branchIndex, nodeIndex);
        string rewardText = GetRewardText(node);
        string stageName = route.Stages[Math.Max(0, Math.Min(route.Stages.Length - 1, node.StageIndex))].Name.Translate();

        button.uiButton.tips.type = UIButton.ItemTipType.Other;
        button.uiButton.tips.itemId = 0;
        button.uiButton.tips.topLevel = true;
        button.uiButton.tips.delay = 0.25f;
        button.uiButton.tips.corner = 7;
        button.uiButton.tips.offset = NodeTipOffset;
        button.uiButton.tips.tipTitle = node.Name.Translate();
        button.uiButton.tips.tipText =
            $"{node.Desc.Translate()}\n\n{"节点详情-推荐阶段".Translate()} {stageName}\n{"节点详情-条件".Translate()} {progressText}\n{"节点详情-奖励".Translate()} {rewardText}\n{"节点详情-状态".Translate()} {GetNodeStateText(modeIndex, branchIndex, nodeIndex)}\n\n{"节点详情-推荐说明".Translate().WithColor(Gray)}";
        button.uiButton.UpdateTip();
    }

    private static string GetNodeProgressText(int modeIndex, int branchIndex, int nodeIndex) {
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
        if (modeIndex < 0
            || modeIndex >= RouteMaps.Length
            || modeIndex >= selectedBranchByMode.Length
            || modeIndex >= selectedNodeByMode.Length) {
            return;
        }

        RouteMap route = GetRouteByModeIndex(modeIndex);
        if (branchIndex < 0 || branchIndex >= route.Branches.Length) {
            return;
        }
        if (nodeIndex < 0 || nodeIndex >= route.Branches[branchIndex].Nodes.Length) {
            return;
        }

        selectedBranchByMode[modeIndex] = branchIndex;
        selectedNodeByMode[modeIndex] = nodeIndex;
        if (modeIndex == GetModeIndex()) {
            RefreshRouteView(modeIndex);
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

    private static void AddRowBackground(RectTransform parent, float left, float top, Color color,
        float width = LeftColumnWidth, float height = CategoryRowHeight) {
        Image image = new GameObject("row-bg", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        NormalizeRectWithTopLeft(image, left, top, parent);
        image.rectTransform.sizeDelta = new Vector2(width, height);
    }

    private static void AddColumnSeparator(RectTransform parent, float left, float top, float height) {
        Image image = new GameObject("column-separator", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.06f);
        image.raycastTarget = false;
        NormalizeRectWithTopLeft(image, left, top + 6f, parent);
        image.rectTransform.sizeDelta = new Vector2(1f, height - 12f);
    }

    private static RectTransform CreatePanelRect(string name, RectTransform parent, float left, float top, float width,
        float height, Color color) {
        Image image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        NormalizeRectWithTopLeft(image, left, top, parent);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static RectTransform CreateViewport(string name, RectTransform parent, float left, float top, float width,
        float height) {
        var obj = new GameObject(name, typeof(RectTransform), typeof(RectMask2D));
        RectTransform rect = obj.GetComponent<RectTransform>();
        NormalizeRectWithTopLeft(rect, left, top, parent);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static RectTransform CreateScrollContent(string name, RectTransform parent, float width, float height) {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
        return rect;
    }

    private static RectTransform CreateFillRect(string name, RectTransform parent) {
        RectTransform rect = new GameObject(name).AddComponent<RectTransform>();
        NormalizeRectWithMargin(rect, 0f, 0f, 0f, 0f, parent);
        return rect;
    }
}
