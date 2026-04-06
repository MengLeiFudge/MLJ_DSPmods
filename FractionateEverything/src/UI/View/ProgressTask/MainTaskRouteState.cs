using System;
using System.IO;
using UnityEngine;
using static FE.Logic.Manager.GachaManager;
using static FE.Utils.Utils;

namespace FE.UI.View.ProgressTask;

public static partial class MainTask {
    private static bool[][][] completedByMode;
    private static bool[][][] rewardedByMode;
    private static int[] selectedBranchByMode = [-1, -1];
    private static int[] selectedNodeByMode = [-1, -1];
    private static int _lastGlobalTickFrame = -1;
    private static readonly string[][] LegacyStageNodeIdsByMode = [
        [
            "normal-tech-data",
            "normal-frac-50",
            "normal-draw-20",
            "normal-mineral",
            "normal-proto",
            "normal-conversion",
            "normal-level-6",
            "normal-rectification",
            "normal-interstellar",
            "normal-darkfog-ground",
            "normal-end",
        ],
        [
            "speed-tech-data",
            "speed-draw-10",
            "speed-mineral",
            "speed-conversion",
            "speed-frac-800",
            "speed-rectification",
            "speed-interstellar",
            "speed-darkfog-signal",
            "speed-end",
        ],
    ];

    private static int GetModeIndex() {
        return IsSpeedrunMode ? 1 : 0;
    }

    private static void EnsureRouteState() {
        completedByMode ??= CreateStateMatrix();
        rewardedByMode ??= CreateStateMatrix();
        ResizeStateMatrix(ref completedByMode);
        ResizeStateMatrix(ref rewardedByMode);
    }

    private static bool[][][] CreateStateMatrix() {
        bool[][][] matrix = new bool[RouteMaps.Length][][];
        for (int modeIndex = 0; modeIndex < RouteMaps.Length; modeIndex++) {
            RouteMap route = GetRouteByModeIndex(modeIndex);
            matrix[modeIndex] = new bool[route.Branches.Length][];
            for (int branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
                matrix[modeIndex][branchIndex] = new bool[route.Branches[branchIndex].Nodes.Length];
            }
        }
        return matrix;
    }

    private static void ResizeStateMatrix(ref bool[][][] matrix) {
        if (matrix == null || matrix.Length != RouteMaps.Length) {
            matrix = CreateStateMatrix();
            return;
        }

        for (int modeIndex = 0; modeIndex < RouteMaps.Length; modeIndex++) {
            RouteMap route = GetRouteByModeIndex(modeIndex);
            if (matrix[modeIndex] == null || matrix[modeIndex].Length != route.Branches.Length) {
                bool[][] oldBranches = matrix[modeIndex];
                matrix[modeIndex] = new bool[route.Branches.Length][];
                for (int branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
                    int nodeCount = route.Branches[branchIndex].Nodes.Length;
                    matrix[modeIndex][branchIndex] = new bool[nodeCount];
                    if (oldBranches == null || branchIndex >= oldBranches.Length || oldBranches[branchIndex] == null) {
                        continue;
                    }
                    Array.Copy(oldBranches[branchIndex], matrix[modeIndex][branchIndex],
                        Math.Min(oldBranches[branchIndex].Length, nodeCount));
                }
                continue;
            }

            for (int branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
                int nodeCount = route.Branches[branchIndex].Nodes.Length;
                if (matrix[modeIndex][branchIndex] != null && matrix[modeIndex][branchIndex].Length == nodeCount) {
                    continue;
                }
                bool[] oldNodes = matrix[modeIndex][branchIndex];
                matrix[modeIndex][branchIndex] = new bool[nodeCount];
                if (oldNodes != null) {
                    Array.Copy(oldNodes, matrix[modeIndex][branchIndex], Math.Min(oldNodes.Length, nodeCount));
                }
            }
        }
    }

    private static void ResetRouteState() {
        completedByMode = CreateStateMatrix();
        rewardedByMode = CreateStateMatrix();
        selectedBranchByMode = [-1, -1];
        selectedNodeByMode = [-1, -1];
        _lastGlobalTickFrame = -1;
    }

    private static void ClampSelections() {
        for (int modeIndex = 0; modeIndex < RouteMaps.Length; modeIndex++) {
            RouteMap route = GetRouteByModeIndex(modeIndex);
            if (selectedBranchByMode[modeIndex] < 0 || selectedBranchByMode[modeIndex] >= route.Branches.Length) {
                selectedBranchByMode[modeIndex] = -1;
                selectedNodeByMode[modeIndex] = -1;
                continue;
            }
            int branchIndex = selectedBranchByMode[modeIndex];
            int nodeCount = route.Branches[branchIndex].Nodes.Length;
            if (selectedNodeByMode[modeIndex] < 0 || selectedNodeByMode[modeIndex] >= nodeCount) {
                selectedNodeByMode[modeIndex] = -1;
            }
        }
    }

    public static void Tick() {
        if (Time.frameCount == _lastGlobalTickFrame) {
            return;
        }
        _lastGlobalTickFrame = Time.frameCount;
        if (GameMain.data == null || GameMain.history == null) {
            return;
        }

        RefreshRouteProgress(showPopup: true);
    }

    private static void RefreshRouteProgress(bool showPopup, bool allowRewardGrant = true) {
        EnsureRouteState();
        for (int modeIndex = 0; modeIndex < RouteMaps.Length; modeIndex++) {
            RefreshRouteProgress(modeIndex, showPopup, allowRewardGrant);
        }
    }

    private static void RefreshRouteProgress(int modeIndex, bool showPopup, bool allowRewardGrant) {
        RouteMap route = GetRouteByModeIndex(modeIndex);
        for (int branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
            // 分支内按节点顺序推进；前置节点没完成时，后续节点即使条件已满足也暂不点亮。
            bool branchOpen = true;
            TaskBranch branch = route.Branches[branchIndex];
            for (int nodeIndex = 0; nodeIndex < branch.Nodes.Length; nodeIndex++) {
                if (!branchOpen) {
                    break;
                }

                if (completedByMode[modeIndex][branchIndex][nodeIndex]) {
                    branchOpen = true;
                    continue;
                }

                TaskNode node = branch.Nodes[nodeIndex];
                bool completed = false;
                try {
                    completed = node.IsCompleted();
                }
                catch (Exception ex) {
                    LogWarning($"[MainTask] 节点条件检查失败 {node.Id}: {ex.Message}");
                }

                if (!completed) {
                    branchOpen = false;
                    continue;
                }

                completedByMode[modeIndex][branchIndex][nodeIndex] = true;
                GrantNodeReward(modeIndex, branchIndex, nodeIndex, showPopup, allowRewardGrant);
            }
        }
    }

    private static void GrantNodeReward(int modeIndex, int branchIndex, int nodeIndex, bool showPopup, bool allowRewardGrant) {
        if (rewardedByMode[modeIndex][branchIndex][nodeIndex]) {
            return;
        }

        // 奖励发放与去重绑定在节点状态里，避免读档或窗口刷新时重复发奖。
        TaskNode node = GetRouteByModeIndex(modeIndex).Branches[branchIndex].Nodes[nodeIndex];
        if (!allowRewardGrant) {
            rewardedByMode[modeIndex][branchIndex][nodeIndex] = true;
            return;
        }
        if (node.RewardItemId > 0 && node.RewardCount > 0) {
            AddItemToModData(node.RewardItemId, node.RewardCount, 0, true);
            UIItemup.Up(node.RewardItemId, node.RewardCount);
        }
        rewardedByMode[modeIndex][branchIndex][nodeIndex] = true;

        if (showPopup) {
            UIRealtimeTip.Popup(string.Format("主线里程碑达成提示".Translate(), node.Name), true, 2);
        }
    }

    private static void EnsureSelectedNode(int modeIndex) {
        ClampSelections();
        if (IsSelectionValid(modeIndex)) {
            return;
        }

        if (TryGetFirstActiveIncompleteNode(modeIndex, out int branchIndex, out int nodeIndex)) {
            selectedBranchByMode[modeIndex] = branchIndex;
            selectedNodeByMode[modeIndex] = nodeIndex;
            return;
        }

        if (TryGetLastCompletedNode(modeIndex, out branchIndex, out nodeIndex)) {
            selectedBranchByMode[modeIndex] = branchIndex;
            selectedNodeByMode[modeIndex] = nodeIndex;
            return;
        }

        selectedBranchByMode[modeIndex] = 0;
        selectedNodeByMode[modeIndex] = 0;
    }

    private static bool IsSelectionValid(int modeIndex) {
        RouteMap route = GetRouteByModeIndex(modeIndex);
        int branchIndex = selectedBranchByMode[modeIndex];
        if (branchIndex < 0 || branchIndex >= route.Branches.Length) {
            return false;
        }
        int nodeIndex = selectedNodeByMode[modeIndex];
        return nodeIndex >= 0 && nodeIndex < route.Branches[branchIndex].Nodes.Length;
    }

    private static bool TryGetFirstActiveIncompleteNode(int modeIndex, out int branchIndex, out int nodeIndex) {
        RouteMap route = GetRouteByModeIndex(modeIndex);
        for (branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
            TaskBranch branch = route.Branches[branchIndex];
            for (nodeIndex = 0; nodeIndex < branch.Nodes.Length; nodeIndex++) {
                if (GetNodeVisualState(modeIndex, branchIndex, nodeIndex) == NodeVisualState.Available) {
                    return true;
                }
            }
        }
        branchIndex = 0;
        nodeIndex = 0;
        return false;
    }

    private static bool TryGetLastCompletedNode(int modeIndex, out int branchIndex, out int nodeIndex) {
        RouteMap route = GetRouteByModeIndex(modeIndex);
        for (branchIndex = route.Branches.Length - 1; branchIndex >= 0; branchIndex--) {
            TaskBranch branch = route.Branches[branchIndex];
            for (nodeIndex = branch.Nodes.Length - 1; nodeIndex >= 0; nodeIndex--) {
                if (completedByMode[modeIndex][branchIndex][nodeIndex]) {
                    return true;
                }
            }
        }
        branchIndex = 0;
        nodeIndex = 0;
        return false;
    }

    private static NodeVisualState GetNodeVisualState(int modeIndex, int branchIndex, int nodeIndex) {
        if (completedByMode[modeIndex][branchIndex][nodeIndex]) {
            return NodeVisualState.Completed;
        }
        return IsNodeUnlocked(modeIndex, branchIndex, nodeIndex) ? NodeVisualState.Available : NodeVisualState.Locked;
    }

    private static bool IsNodeUnlocked(int modeIndex, int branchIndex, int nodeIndex) {
        return nodeIndex <= 0 || completedByMode[modeIndex][branchIndex][nodeIndex - 1];
    }

    private static int CountCompletedNodes(int modeIndex) {
        EnsureRouteState();
        int count = 0;
        for (int branchIndex = 0; branchIndex < completedByMode[modeIndex].Length; branchIndex++) {
            for (int nodeIndex = 0; nodeIndex < completedByMode[modeIndex][branchIndex].Length; nodeIndex++) {
                if (completedByMode[modeIndex][branchIndex][nodeIndex]) {
                    count++;
                }
            }
        }
        return count;
    }

    private static int CountTotalNodes(int modeIndex) {
        RouteMap route = GetRouteByModeIndex(modeIndex);
        int count = 0;
        for (int branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
            count += route.Branches[branchIndex].Nodes.Length;
        }
        return count;
    }

    private static int CountCompletedBranches(int modeIndex) {
        EnsureRouteState();
        int count = 0;
        for (int branchIndex = 0; branchIndex < completedByMode[modeIndex].Length; branchIndex++) {
            bool branchCompleted = true;
            for (int nodeIndex = 0; nodeIndex < completedByMode[modeIndex][branchIndex].Length; nodeIndex++) {
                if (!completedByMode[modeIndex][branchIndex][nodeIndex]) {
                    branchCompleted = false;
                    break;
                }
            }
            if (branchCompleted) {
                count++;
            }
        }
        return count;
    }

    public static void Import(BinaryReader r) {
        ResetRouteState();
        EnsureRouteState();

        bool loadedCompletedState = false;
        bool loadedRewardedState = false;
        int legacyCurrentStage = 0;
        bool legacyRewardClaimed = true;
        int[] legacyCurrentStageByMode = new int[RouteMaps.Length];
        bool[] legacyRewardClaimedByMode = [true, true];
        bool loadedLegacyStagesByMode = false;
        bool loadedLegacyRewardsByMode = false;

        // 兼容旧档：旧版只有单线 stage / reward 标记，新版优先读取节点状态矩阵。
        r.ReadBlocks(
            ("CurrentStage", br => legacyCurrentStage = br.ReadInt32()),
            ("RewardClaimed", br => legacyRewardClaimed = br.ReadBoolean()),
            ("CurrentStageByMode", br => {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++) {
                    int value = br.ReadInt32();
                    if (i < legacyCurrentStageByMode.Length) {
                        legacyCurrentStageByMode[i] = value;
                    }
                }
                loadedLegacyStagesByMode = true;
            }),
            ("RewardClaimedByMode", br => {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++) {
                    bool value = br.ReadBoolean();
                    if (i < legacyRewardClaimedByMode.Length) {
                        legacyRewardClaimedByMode[i] = value;
                    }
                }
                loadedLegacyRewardsByMode = true;
            }),
            ("NodeCompletedStates", br => {
                ReadStateMatrix(br, completedByMode);
                loadedCompletedState = true;
            }),
            ("NodeRewardedStates", br => {
                ReadStateMatrix(br, rewardedByMode);
                loadedRewardedState = true;
            })
        );

        bool isLegacyImport = !loadedCompletedState || !loadedRewardedState;
        if (isLegacyImport) {
            ResetRouteState();
            if (!loadedLegacyStagesByMode) {
                legacyCurrentStageByMode[0] = legacyCurrentStage;
            }
            if (!loadedLegacyRewardsByMode) {
                legacyRewardClaimedByMode[0] = legacyRewardClaimed;
            }
            ApplyLegacyProgress(legacyCurrentStageByMode, legacyRewardClaimedByMode);
        }

        // 导入完成后按当前真实游戏状态重算节点完成度：
        // 1. 旧档首迁只重建完成/已发奖状态，避免重复补发旧奖励；
        // 2. 已进入新系统的存档仍会为新增且未发奖节点自动补发奖励。
        EnsureRouteState();
        RefreshRouteProgress(showPopup: false, allowRewardGrant: !isLegacyImport);
        ClampSelections();
    }

    public static void Export(BinaryWriter w) {
        EnsureRouteState();
        BuildLegacyExportState(0, out int legacyCurrentStage, out bool legacyRewardClaimed);
        BuildLegacyExportState(1, out int legacySpeedrunStage, out bool legacySpeedrunRewardClaimed);
        w.WriteBlocks(
            ("CurrentStage", bw => bw.Write(legacyCurrentStage)),
            ("RewardClaimed", bw => bw.Write(legacyRewardClaimed)),
            ("CurrentStageByMode", bw => {
                bw.Write(RouteMaps.Length);
                bw.Write(legacyCurrentStage);
                bw.Write(legacySpeedrunStage);
            }),
            ("RewardClaimedByMode", bw => {
                bw.Write(RouteMaps.Length);
                bw.Write(legacyRewardClaimed);
                bw.Write(legacySpeedrunRewardClaimed);
            }),
            ("NodeCompletedStates", bw => WriteStateMatrix(bw, completedByMode)),
            ("NodeRewardedStates", bw => WriteStateMatrix(bw, rewardedByMode))
        );
    }

    public static void IntoOtherSave() {
        ResetRouteState();
    }

    private static void WriteStateMatrix(BinaryWriter w, bool[][][] matrix) {
        w.Write(matrix.Length);
        for (int modeIndex = 0; modeIndex < matrix.Length; modeIndex++) {
            w.Write(matrix[modeIndex].Length);
            for (int branchIndex = 0; branchIndex < matrix[modeIndex].Length; branchIndex++) {
                w.Write(matrix[modeIndex][branchIndex].Length);
                for (int nodeIndex = 0; nodeIndex < matrix[modeIndex][branchIndex].Length; nodeIndex++) {
                    w.Write(matrix[modeIndex][branchIndex][nodeIndex]);
                }
            }
        }
    }

    private static void ReadStateMatrix(BinaryReader r, bool[][][] matrix) {
        int modeCount = r.ReadInt32();
        for (int modeIndex = 0; modeIndex < modeCount; modeIndex++) {
            int branchCount = r.ReadInt32();
            for (int branchIndex = 0; branchIndex < branchCount; branchIndex++) {
                int nodeCount = r.ReadInt32();
                for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++) {
                    bool value = r.ReadBoolean();
                    if (modeIndex < matrix.Length && branchIndex < matrix[modeIndex].Length
                        && nodeIndex < matrix[modeIndex][branchIndex].Length) {
                        matrix[modeIndex][branchIndex][nodeIndex] = value;
                    }
                }
            }
        }
    }

    private static void ApplyLegacyProgress(int[] currentStageByModeLegacy, bool[] rewardClaimedByModeLegacy) {
        for (int modeIndex = 0; modeIndex < Math.Min(RouteMaps.Length, LegacyStageNodeIdsByMode.Length); modeIndex++) {
            string[] legacyNodeIds = LegacyStageNodeIdsByMode[modeIndex];
            int currentStage = Math.Max(0, currentStageByModeLegacy[modeIndex]);
            bool rewardClaimed = rewardClaimedByModeLegacy[modeIndex];

            int completedStageCount = Math.Min(currentStage, legacyNodeIds.Length);
            for (int i = 0; i < completedStageCount; i++) {
                MarkNodeById(modeIndex, legacyNodeIds[i], rewarded: true);
            }

            if (rewardClaimed && currentStage < legacyNodeIds.Length) {
                MarkNodeById(modeIndex, legacyNodeIds[currentStage], rewarded: true);
            }
        }
    }

    private static void BuildLegacyExportState(int modeIndex, out int currentStage, out bool rewardClaimed) {
        string[] legacyNodeIds = LegacyStageNodeIdsByMode[Math.Max(0, Math.Min(LegacyStageNodeIdsByMode.Length - 1, modeIndex))];
        currentStage = legacyNodeIds.Length;
        rewardClaimed = true;
        for (int i = 0; i < legacyNodeIds.Length; i++) {
            if (TryFindNodeIndex(modeIndex, legacyNodeIds[i], out int branchIndex, out int nodeIndex)
                && completedByMode[modeIndex][branchIndex][nodeIndex]) {
                continue;
            }

            currentStage = i;
            rewardClaimed = false;
            return;
        }
    }

    private static void MarkNodeById(int modeIndex, string nodeId, bool rewarded) {
        if (!TryFindNodeIndex(modeIndex, nodeId, out int branchIndex, out int nodeIndex)) {
            return;
        }
        completedByMode[modeIndex][branchIndex][nodeIndex] = true;
        rewardedByMode[modeIndex][branchIndex][nodeIndex] = rewarded;
    }

    private static bool TryFindNodeIndex(int modeIndex, string nodeId, out int branchIndex, out int nodeIndex) {
        RouteMap route = GetRouteByModeIndex(modeIndex);
        for (branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
            TaskBranch branch = route.Branches[branchIndex];
            for (nodeIndex = 0; nodeIndex < branch.Nodes.Length; nodeIndex++) {
                if (branch.Nodes[nodeIndex].Id == nodeId) {
                    return true;
                }
            }
        }
        branchIndex = -1;
        nodeIndex = -1;
        return false;
    }
}
