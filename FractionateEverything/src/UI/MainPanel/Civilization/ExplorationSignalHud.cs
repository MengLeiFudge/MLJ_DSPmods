using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FE.Logic.Civilization;
using UnityEngine;
using UnityEngine.UI;

namespace FE.UI.MainPanel.Civilization;

/// <summary>
/// 管理左侧探索信号 HUD，以及每名玩家独立保存的四个普通信号固定槽。
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
        rootRect.SetParent(UIRoot.instance.overlayCanvas.transform, false);
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
            row.fontSize = 14;
            row.fontStyle = FontStyle.Normal;
            row.resizeTextForBestFit = true;
            row.resizeTextMinSize = 11;
            row.resizeTextMaxSize = 14;
            row.alignment = TextAnchor.MiddleLeft;
            row.supportRichText = false;
            row.horizontalOverflow = HorizontalWrapMode.Overflow;
            row.verticalOverflow = VerticalWrapMode.Truncate;
            row.raycastTarget = false;
            row.gameObject.SetActive(true);
            row.color = index == 0
                ? new Color(1f, 0.76f, 0.32f, 0.88f)
                : new Color(0.78f, 0.86f, 0.9f, 0.84f);
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
    /// 刷新 HUD 和自动固定规则。
    /// </summary>
    public static void Update() {
        EnsureCreated();
        if (!root) {
            return;
        }
        if (!GameMain.isRunning || GameMain.isPaused || GameMain.isFullscreenPaused
            || GameMain.mainPlayer == null) {
            root.SetActive(false);
            return;
        }
        if (Time.frameCount == lastRefreshFrame || Time.frameCount % 15 != 0) {
            return;
        }
        lastRefreshFrame = Time.frameCount;

        ExplorationMark[] marks = ExplorationManager.GetActiveMarks();
        ReconcilePinnedSlots(marks);
        var marksById = marks.ToDictionary(mark => mark.Id);
        ExplorationMark keySignal = marks
            .Where(ExplorationManager.IsKeySignal)
            .OrderBy(mark => mark.Id)
            .FirstOrDefault();
        SetRow(0, keySignal);
        bool hasVisibleSignal = keySignal != null;
        for (int slot = 0; slot < OrdinarySlotCount; slot++) {
            marksById.TryGetValue(PinnedMarkIds[slot], out ExplorationMark mark);
            SetRow(slot + 1, mark);
            hasVisibleSignal |= mark != null;
        }
        root.SetActive(hasVisibleSignal && UIRoot.instance?.uiGame?.active == true);
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
