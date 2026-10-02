using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FE.Logic.Civilization;
using UnityEngine;
using UnityEngine.UI;

namespace FE.UI.MainPanel.Civilization;

/// <summary>
/// 显示附近最高探测强度、当前主线与本星球静默回收进度。
/// </summary>
internal static class ExplorationSignalHud {
    private const int OrdinarySlotCount = 4;
    private const float HudWidth = 520f;
    private const float RowHeight = 24f;
    private static readonly int[] PinnedMarkIds = new int[OrdinarySlotCount];
    private static readonly Text[] RowTexts = new Text[OrdinarySlotCount + 1];
    private static GameObject root;
    private static int lastObservedMarkId;
    private static int lastRefreshFrame = -1;

    /// <summary>
    /// 创建 HUD；重复调用不会创建多个实例。
    /// </summary>
    public static void EnsureCreated() {
        UIGame uiGame = UIRoot.instance?.uiGame;
        if (root || UIRoot.instance?.overlayCanvas == null || uiGame == null
                 || (uiGame.goalPanel?.uiGoalInfoEntryPrefab?.nameText == null
                     && uiGame.goalPanel?.headerText == null
                     && uiGame.assemblerWindow?.stateText == null)) {
            return;
        }

        root = new GameObject("fe-exploration-signal-hud", typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(uiGame.dfMonitor != null
            ? uiGame.dfMonitor.transform.parent : UIRoot.instance.overlayCanvas.transform, false);
        rootRect.anchorMin = new Vector2(0f, 0.5f);
        rootRect.anchorMax = new Vector2(0f, 0.5f);
        rootRect.pivot = new Vector2(0f, 0.5f);
        rootRect.anchoredPosition = new Vector2(22f, 100f);
        rootRect.sizeDelta = new Vector2(HudWidth, RowHeight * RowTexts.Length);

        UIGoalPanel goalPanel = uiGame.goalPanel;
        Text source = goalPanel?.uiGoalInfoEntryPrefab?.nameText
                      ?? goalPanel?.headerText
                      ?? uiGame.assemblerWindow.stateText;
        for (int index = 0; index < RowTexts.Length; index++) {
            var rowObject = new GameObject($"signal-row-{index + 1}",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
            rowRect.SetParent(rootRect, false);
            Text row = rowObject.GetComponent<Text>();
            row.font = source.font;
            if (row.font != null) {
                row.material = row.font.material;
            }
            row.text = string.Empty;
            row.fontSize = 16;
            row.fontStyle = FontStyle.Normal;
            row.resizeTextForBestFit = false;
            row.alignment = TextAnchor.MiddleLeft;
            row.supportRichText = false;
            row.horizontalOverflow = HorizontalWrapMode.Overflow;
            row.verticalOverflow = VerticalWrapMode.Truncate;
            row.raycastTarget = false;
            row.gameObject.SetActive(true);
            row.color = Color.white;
            Outline outline = rowObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(0f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -index * RowHeight);
            rowRect.sizeDelta = new Vector2(HudWidth, RowHeight);
            RowTexts[index] = row;
        }
        root.SetActive(false);
    }

    /// <summary>
    /// 刷新本地聚合读数，复用逻辑域缓存快照。
    /// </summary>
    public static void Update() {
        EnsureCreated();
        if (!root) {
            return;
        }
        UIGame uiGame = UIRoot.instance?.uiGame;
        Player player = GameMain.mainPlayer;
        // 与原版黑雾监视器共用所在 UI 层，保留无黑雾模式下的探索显示。
        bool visible = uiGame?.active == true && GameMain.isRunning && GameMain.gameTick > 0
            && !GameMain.isPaused && !GameMain.isFullscreenPaused && !VFInput.inFullscreenGUI
            && !uiGame.hideAllUI0 && !uiGame.hideAllUI1
            && player != null && player.isAlive && !player.respawning
            && player.controller.actionBuild.blueprintMode == EBlueprintMode.None
            && !uiGame.techTree.active && !uiGame.dashboard.active && !uiGame.dysonEditor.active
            && !uiGame.starmap.active;
        root.SetActive(visible);
        if (!visible) { return; }
        if (Time.frameCount == lastRefreshFrame || Time.frameCount % 15 != 0) {
            return;
        }
        lastRefreshFrame = Time.frameCount;

        ExplorationMark keySignal = ExplorationManager.GetActiveMarks().FirstOrDefault(ExplorationManager.IsKeySignal);
        float strength = ExplorationManager.GetHighestStrength();
        RowTexts[0].text = strength > 0 ? $"附近信号：{strength:P0}" : "附近暂无可探测信号";
        RowTexts[0].gameObject.SetActive(true);
        RowTexts[1].text = keySignal == null ? ""
            : $"{ExplorationManager.GetSignalName(keySignal)} · {GameMain.galaxy?.PlanetById(keySignal.PlanetId)?.displayName}";
        RowTexts[1].gameObject.SetActive(keySignal != null);
        PlanetData planet = GameMain.localPlanet;
        if (planet != null && ExplorationManager.FirstRelicClaimed) {
            ExplorationManager.GetSilentProgress(planet.id, out int collected, out int total);
            RowTexts[2].text = $"本星球静默遗迹：{collected}/{total}";
        }
        RowTexts[2].gameObject.SetActive(planet != null && ExplorationManager.FirstRelicClaimed);
        for (int i = 3; i < RowTexts.Length; i++) { RowTexts[i].gameObject.SetActive(false); }
    }

    /// <summary>
    /// 固定或取消固定一个普通信号；主线信号始终由专用槽显示。
    /// </summary>
    public static bool TogglePinned(int markId) {
        ExplorationMark[] marks = ExplorationManager.GetActiveMarks();
        ReconcilePinnedSlots(marks);
        int pinnedSlot = Array.IndexOf(PinnedMarkIds, markId);
        if (pinnedSlot >= 0) {
            PinnedMarkIds[pinnedSlot] = 0;
            lastRefreshFrame = -1;
            return true;
        }
        ExplorationMark mark = marks.FirstOrDefault(candidate => candidate.Id == markId);
        int emptySlot = Array.IndexOf(PinnedMarkIds, 0);
        if (mark == null || ExplorationManager.IsKeySignal(mark) || emptySlot < 0) {
            return false;
        }
        PinnedMarkIds[emptySlot] = markId;
        lastObservedMarkId = Math.Max(lastObservedMarkId, markId);
        lastRefreshFrame = -1;
        return true;
    }

    /// <summary>
    /// 判断普通信号是否已固定到 HUD。
    /// </summary>
    public static bool IsPinned(int markId) => Array.IndexOf(PinnedMarkIds, markId) >= 0;

    /// <summary>
    /// 判断 HUD 是否还有普通信号空槽。
    /// </summary>
    public static bool HasAvailableOrdinarySlot => Array.IndexOf(PinnedMarkIds, 0) >= 0;

    /// <summary>
    /// 返回四个固定槽和已观察到的最大 ID，用于 UI 偏好存档。
    /// </summary>
    public static (int[] Slots, int LastObservedMarkId) GetSaveState() {
        ReconcilePinnedSlots(ExplorationManager.GetActiveMarks());
        return ((int[])PinnedMarkIds.Clone(), lastObservedMarkId);
    }

    /// <summary>
    /// 恢复四个固定槽和新信号自动固定游标。
    /// </summary>
    public static void SetSaveState(IReadOnlyList<int> slots, int observedMarkId) {
        Array.Clear(PinnedMarkIds, 0, PinnedMarkIds.Length);
        var seen = new HashSet<int>();
        if (slots != null) {
            for (int index = 0; index < Math.Min(slots.Count, OrdinarySlotCount); index++) {
                int markId = slots[index];
                if (markId > 0 && seen.Add(markId)) {
                    PinnedMarkIds[index] = markId;
                }
            }
        }
        lastObservedMarkId = Math.Max(0, observedMarkId);
        lastRefreshFrame = -1;
    }

    /// <summary>
    /// 切档时清空玩家 UI 偏好。
    /// </summary>
    public static void IntoOtherSave() => SetSaveState(null, 0);

    private static void ReconcilePinnedSlots(ExplorationMark[] marks) {
        var ordinaryIds = new HashSet<int>(marks
            .Where(mark => !ExplorationManager.IsKeySignal(mark))
            .Select(mark => mark.Id));
        for (int slot = 0; slot < OrdinarySlotCount; slot++) {
            if (PinnedMarkIds[slot] > 0 && !ordinaryIds.Contains(PinnedMarkIds[slot])) {
                PinnedMarkIds[slot] = 0;
            }
        }

        int previousMaximum = lastObservedMarkId;
        bool firstObservation = previousMaximum == 0;
        foreach (ExplorationMark mark in marks.OrderBy(mark => mark.Id)) {
            if (ExplorationManager.IsKeySignal(mark) || Array.IndexOf(PinnedMarkIds, mark.Id) >= 0
                || !firstObservation && mark.Id <= previousMaximum) {
                continue;
            }
            int emptySlot = Array.IndexOf(PinnedMarkIds, 0);
            if (emptySlot >= 0) {
                PinnedMarkIds[emptySlot] = mark.Id;
            }
        }
        if (marks.Length > 0) {
            lastObservedMarkId = Math.Max(lastObservedMarkId, marks.Max(mark => mark.Id));
        }
    }

    private static void SetRow(int rowIndex, ExplorationMark mark) {
        Text row = RowTexts[rowIndex];
        if (row == null) {
            return;
        }
        if (mark == null || !ExplorationManager.RelicDetectorUnlocked) {
            row.gameObject.SetActive(false);
            return;
        }

        row.text = BuildRowText(mark);
        row.gameObject.SetActive(true);
    }

    private static string BuildRowText(ExplorationMark mark) {
        string name = ExplorationManager.GetSignalName(mark);
        if (ExplorationManager.IsKeySignal(mark)) {
            string planetName = GameMain.galaxy?.PlanetById(mark.PlanetId)?.displayName;
            if (!string.IsNullOrEmpty(planetName)) {
                name = $"{name}  {planetName}";
            }
        }

        VectorLF3 playerPosition = GameMain.mainPlayer?.uPosition ?? VectorLF3.zero;
        ExplorationManager.TryGetDetectorStrengths(mark, playerPosition,
            out float planetStrength, out float relicStrength);
        string relicText = $"{"遗物".Translate()} {FormatStrength(relicStrength)}";
        return ExplorationManager.PlanetDetectorUnlocked
            ? $"{name}  {"星球".Translate()} {FormatStrength(planetStrength)}  {relicText}"
            : $"{name}  {relicText}";
    }

    private static string FormatStrength(float strength) {
        string format = GrowthTechnologyManager.ExplorationSignalPrecisionPercent < 1f ? "0.0" : "0";
        return $"{(strength * 100f).ToString(format, CultureInfo.InvariantCulture)}%";
    }
}
