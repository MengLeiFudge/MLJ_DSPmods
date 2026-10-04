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
    /// <summary>HUD所在层的局部矩形；布局每帧跟随原版面板动画。</summary>
    private static RectTransform rootRect;
    /// <summary>复用原版面板四角，避免动态布局每帧分配数组。</summary>
    private static readonly Vector3[] PanelCorners = new Vector3[4];
    private static int lastRefreshFrame = -1;

    /// <summary>
    /// 创建 HUD；重复调用不会创建多个实例。
    /// </summary>
    public static void EnsureCreated() {
        UIGame uiGame = UIRoot.instance?.uiGame;
        if (root || UIRoot.instance?.overlayCanvas == null || uiGame == null) { return; }
        Text source = uiGame.generalTips?.buildCursorTextComp ?? uiGame.generalTips?.promptText
                      ?? uiGame.assemblerWindow?.stateText;
        if (source == null) { return; }

        root = new GameObject("fe-exploration-signal-hud", typeof(RectTransform));
        rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(uiGame.dfMonitor != null
            ? uiGame.dfMonitor.transform.parent : UIRoot.instance.overlayCanvas.transform, false);
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.sizeDelta = new Vector2(HudWidth, RowHeight * RowTexts.Length);

        for (int index = 0; index < RowTexts.Length; index++) {
            // 提示模板可能使用独立描边材质，不能只复制font后换成font.material。
            Text row = Object.Instantiate(source, rootRect, false);
            row.name = $"signal-row-{index + 1}";
            row.enabled = true;
            Localizer localizer = row.GetComponent<Localizer>();
            if (localizer != null) { localizer.enabled = false; }
            RectTransform rowRect = row.rectTransform;
            rowRect.localScale = Vector3.one;
            rowRect.localRotation = Quaternion.identity;
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
            Outline outline = row.GetComponent<Outline>() ?? row.gameObject.AddComponent<Outline>();
            outline.enabled = true;
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

    /// <summary>随可见教学与黑雾列表排在下方；底部不足时放到它们右侧。</summary>
    /// <param name="uiGame">当前原版界面。</param>
    private static void UpdatePosition(UIGame uiGame) {
        RectTransform parent = (RectTransform)rootRect.parent;
        Rect screen = parent.rect;
        float top = screen.yMax - 110f;
        float bottom = top + 10f;
        float right = screen.xMin + 22f;
        IncludePanelBounds(uiGame.goalPanel?.rect, parent, ref bottom, ref right);
        UIDarkFogMonitor monitor = uiGame.dfMonitor;
        IncludePanelBounds(monitor?.contentRect, parent, ref bottom, ref right,
            monitor?.scrollRect?.viewport ?? monitor?.entryGroup);

        float x = screen.xMin + 22f;
        float y = Mathf.Min(top, bottom - 10f);
        // 底部留出原版建造栏区域，不把被遮挡问题转移到屏幕下沿。
        if (y - rootRect.rect.height < screen.yMin + 100f) {
            x = right + 10f;
            y = top;
        }
        x = Mathf.Clamp(x, screen.xMin + 10f, Mathf.Max(screen.xMin + 10f, screen.xMax - HudWidth - 10f));
        rootRect.anchoredPosition = new Vector2(Mathf.Round(x - screen.xMin), Mathf.Round(y - screen.yMax));
    }

    /// <summary>把当前可见面板边界加入避让范围；滚动内容只计视口内部分。</summary>
    /// <param name="panel">面板实际内容矩形。</param>
    /// <param name="parent">HUD父节点，所有边界均转换到其局部坐标。</param>
    /// <param name="bottom">所有面板最低的局部Y坐标。</param>
    /// <param name="right">所有面板最右的局部X坐标。</param>
    /// <param name="viewport">可选的滚动裁剪边界。</param>
    private static void IncludePanelBounds(RectTransform panel, RectTransform parent,
        ref float bottom, ref float right, RectTransform viewport = null) {
        if (panel == null || !panel.gameObject.activeInHierarchy) { return; }
        Rect bounds = GetPanelBounds(panel, parent);
        if (viewport != null) {
            Rect clip = GetPanelBounds(viewport, parent);
            bounds = Rect.MinMaxRect(Mathf.Max(bounds.xMin, clip.xMin), Mathf.Max(bounds.yMin, clip.yMin),
                Mathf.Min(bounds.xMax, clip.xMax), Mathf.Min(bounds.yMax, clip.yMax));
        }
        if (bounds.width <= 0 || bounds.height <= 0 || bounds.xMax <= parent.rect.xMin
            || bounds.xMin >= parent.rect.xMax) { return; }
        bottom = Mathf.Min(bottom, bounds.yMin);
        right = Mathf.Max(right, bounds.xMax);
    }

    /// <summary>读取矩形本身的边界，避免把隐藏或滚出视口的子节点算入布局。</summary>
    /// <param name="panel">目标矩形。</param>
    /// <param name="parent">统一局部坐标系。</param>
    /// <returns>父节点局部坐标下的矩形。</returns>
    private static Rect GetPanelBounds(RectTransform panel, RectTransform parent) {
        panel.GetWorldCorners(PanelCorners);
        Vector3 min = parent.InverseTransformPoint(PanelCorners[0]);
        Vector3 max = min;
        for (int i = 1; i < PanelCorners.Length; i++) {
            Vector3 corner = parent.InverseTransformPoint(PanelCorners[i]);
            min = Vector3.Min(min, corner);
            max = Vector3.Max(max, corner);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
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
        UpdatePosition(uiGame);
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
