using System.Linq;
using FE.Logic.Civilization;
using UnityEngine;
using UnityEngine.UI;

namespace FE.UI.MainPanel.Civilization;

/// <summary>
/// 显示附近最高探测强度、关键信号与本星球静默回收进度。
/// </summary>
internal static class ExplorationSignalHud {
    private const float HudWidth = 520f;
    private const float RowHeight = 24f;
    private static readonly Text[] RowTexts = new Text[3];
    private static GameObject root;
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

        ExplorationMark[] keySignals = ExplorationManager.GetActiveMarks().Where(ExplorationManager.IsKeySignal).ToArray();
        float strength = ExplorationManager.GetHighestStrength();
        RowTexts[0].text = strength > 0 ? string.Format("附近信号：{0:P0}".Translate(), strength) : "附近暂无可探测信号".Translate();
        RowTexts[0].gameObject.SetActive(true);
        if (keySignals.Length > 0) {
            ExplorationMark first = keySignals[0];
            string text = $"{ExplorationManager.GetSignalName(first)} · {GameMain.galaxy?.PlanetById(first.PlanetId)?.displayName}";
            RowTexts[1].text = keySignals.Length > 1 ? string.Format("{0}  （共 {1} 个关键信号）".Translate(), text, keySignals.Length) : text;
        }
        RowTexts[1].gameObject.SetActive(keySignals.Length > 0);
        PlanetData planet = GameMain.localPlanet;
        bool showSilent = planet != null && ExplorationManager.FirstRelicClaimed;
        if (showSilent) {
            ExplorationManager.GetSilentProgress(planet.id, out int collected, out int total);
            RowTexts[2].text = string.Format("本星球静默遗迹：{0}/{1}".Translate(), collected, total);
        }
        RowTexts[2].gameObject.SetActive(showSilent);
    }
}
