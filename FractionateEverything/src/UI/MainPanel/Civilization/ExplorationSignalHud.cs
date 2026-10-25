using FE.Logic.Civilization;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FE.UI.MainPanel.Civilization;

/// <summary>复用原版黑雾监视条目，并从条目向目标绘制蓝色指引线。</summary>
internal static class ExplorationSignalHud {
    private static GameObject root;
    private static RectTransform rootRect;
    private static UIDarkFogMonitorEntry entry;
    private static RectTransform titleRect;
    private static Text title;
    private static LineGizmo guideLine;
    private static GalaxyData lineGalaxy;
    private static float HudWidth => rootRect.rect.width;
    private static float RowHeight => rootRect.rect.height;
    private static readonly Vector3[] PanelCorners = new Vector3[4];
    private static readonly Rect[] PanelBounds = new Rect[5];
    private static int panelCount;
    private static int displayedMarkId;

    /// <summary>复制原版条目预制体，保留视觉组件，不注册黑雾业务事件。</summary>
    public static void EnsureCreated() {
        UIGame uiGame = UIRoot.instance?.uiGame;
        UIDarkFogMonitor monitor = uiGame?.dfMonitor;
        if (root || monitor?.baseEntryPrefab == null || monitor.baseGroupLabel == null
            || UIRoot.instance.overlayCanvas == null) { return; }
        root = new GameObject("fe-exploration-signal-hud", typeof(RectTransform));
        rootRect = (RectTransform)root.transform;
        rootRect.SetParent(monitor.transform.parent, false);
        rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(0f, 1f);
        rootRect.sizeDelta = new Vector2(monitor.contentRect.rect.width,
            monitor.groupLabelHeight + monitor.entryHeight);
        titleRect = Object.Instantiate(monitor.baseGroupLabel, rootRect, false);
        titleRect.anchoredPosition = new Vector2(titleRect.anchoredPosition.x, -1f);
        title = titleRect.GetComponentInChildren<Text>(true);
        entry = Object.Instantiate(monitor.baseEntryPrefab, rootRect, false);
        entry.enabled = false;
        entry.monitor = monitor;
        entry.gameObject.SetActive(true);
        foreach (Localizer localizer in root.GetComponentsInChildren<Localizer>(true)) { localizer.enabled = false; }
        entry.idText.gameObject.SetActive(false);
        entry.levelText.gameObject.SetActive(false);
        entry.locationText.gameObject.SetActive(false);
        entry.expBar.gameObject.SetActive(false);
        entry.alertImage.gameObject.SetActive(false);
        entry.alertToggle.gameObject.SetActive(false);
        entry.iconImage.gameObject.SetActive(false);
        entry.iconTintImage.gameObject.SetActive(false);
        foreach (UIButton button in new[] { entry.entryButton, entry.thrtButton, entry.expButton }) {
            button.onClick -= entry.OnEntryClick;
            button.onClick += _ => { ExplorationDetector.ToggleTracking(displayedMarkId); Update(); };
            button.tips.tipTitle = button.tips.tipText = string.Empty;
            if (button.button != null) { button.button.onClick = new Button.ButtonClickedEvent(); }
        }
        root.SetActive(false);
    }

    /// <summary>优先排在原版面板下方或右侧，再在屏幕安全区内寻找空位。</summary>
    /// <param name="uiGame">当前原版界面。</param>
    private static void UpdatePosition(UIGame uiGame) {
        RectTransform parent = (RectTransform)rootRect.parent;
        Rect screen = parent.rect;
        Rect safe = Rect.MinMaxRect(screen.xMin + 10f, screen.yMin + 100f,
            screen.xMax - 10f, screen.yMax - 110f);
        panelCount = 0;
        IncludePanelBounds(uiGame.goalPanel?.rect, parent);
        UIDarkFogMonitor monitor = uiGame.dfMonitor;
        IncludePanelBounds(monitor?.contentRect, parent,
            monitor?.scrollRect?.viewport ?? monitor?.entryGroup);
        UIMechaLab lab = uiGame.mechaLab;
        // 与原版每项50像素的目标高度一致；提前为展开预留空间，避免跟随Lerp逐像素下移。
        float labHeight = lab == null ? 0f : lab.minBgSize + 50f * (lab.labItems?.Count ?? 0);
        IncludePanelBounds(lab?.miniLabBg, parent, minimumHeight: labHeight);
        IncludePanelBounds(uiGame.researchQueue?.trans, parent);
        IncludePanelBounds(uiGame.researchQueue?.techTip?.panelRect, parent);

        float bottom = safe.yMax + 10f;
        float right = safe.xMin;
        for (int i = 0; i < panelCount; i++) {
            bottom = Mathf.Min(bottom, PanelBounds[i].yMin);
            right = Mathf.Max(right, PanelBounds[i].xMax);
        }
        float left = monitor?.contentRect != null
            ? GetPanelBounds(monitor.contentRect, parent).xMin : screen.xMin + 22f;
        Vector2 position = new(Mathf.Max(safe.xMin, left), Mathf.Min(safe.yMax, bottom - 10f));
        float scale = GetAvailableScale(position, safe);
        if (scale < 1f) {
            position = new Vector2(right + 10f, safe.yMax);
            scale = GetAvailableScale(position, safe);
        }
        if (scale < 1f) {
            // 空位的左边界和上边界只需考虑安全区边缘及面板边缘；不把越界位置钳回面板上。
            scale = 0f;
            for (int x = -1; x < panelCount; x++) {
                for (int y = -1; y < panelCount; y++) {
                    Vector2 candidate = new(x < 0 ? safe.xMin : PanelBounds[x].xMax + 10f,
                        y < 0 ? safe.yMax : PanelBounds[y].yMin - 10f);
                    float available = GetAvailableScale(candidate, safe);
                    if (available > scale) { position = candidate; scale = available; }
                }
            }
        }
        if (scale <= 0f) { return; }
        Vector2 current = new(screen.xMin + rootRect.anchoredPosition.x,
            screen.yMax + rootRect.anchoredPosition.y);
        // 小幅收缩仍能容纳当前HUD时保留位置，避免面板缓动产生反复跳动。
        if ((position - current).sqrMagnitude <= RowHeight * RowHeight
            && GetAvailableScale(current, safe) >= scale) {
            position = current;
        }
        rootRect.localScale = new Vector3(scale, scale, 1f);
        rootRect.anchoredPosition = new Vector2(position.x - screen.xMin, position.y - screen.yMax);
    }

    /// <summary>计算指定左上角可容纳的最大整体比例，保持信号条完整并与所有面板留出间隔。</summary>
    /// <param name="position">父节点局部坐标下的左上角。</param>
    /// <param name="safe">避开屏幕边缘和建造栏的区域。</param>
    /// <returns>0表示没有空位，1表示能够使用原尺寸。</returns>
    private static float GetAvailableScale(Vector2 position, Rect safe) {
        if (position.x < safe.xMin || position.y > safe.yMax) { return 0f; }
        float height = RowHeight;
        float scale = Mathf.Min(1f, Mathf.Min((safe.xMax - position.x) / HudWidth,
            (position.y - safe.yMin) / height));
        for (int i = 0; i < panelCount; i++) {
            Rect panel = PanelBounds[i];
            if (panel.xMax + 10f <= position.x || panel.yMin - 10f >= position.y) { continue; }
            // HUD向右下方展开；在面板左侧或上方任一方向留出空间即可避免交叠。
            scale = Mathf.Min(scale, Mathf.Max((panel.xMin - 10f - position.x) / HudWidth,
                (position.y - panel.yMax - 10f) / height));
        }
        return Mathf.Max(0f, scale);
    }

    /// <summary>记录当前可见面板边界；滚动内容只计视口内部分。</summary>
    /// <param name="panel">面板实际内容矩形。</param>
    /// <param name="parent">HUD父节点，所有边界均转换到其局部坐标。</param>
    /// <param name="viewport">可选的滚动裁剪边界。</param>
    /// <param name="minimumHeight">面板本地坐标中的展开目标高度，0表示只使用当前高度。</param>
    private static void IncludePanelBounds(RectTransform panel, RectTransform parent,
        RectTransform viewport = null, float minimumHeight = 0f) {
        if (panel == null || !panel.gameObject.activeInHierarchy) { return; }
        Rect bounds = GetPanelBounds(panel, parent);
        if (minimumHeight > panel.rect.height) {
            Vector3 extra = parent.InverseTransformVector(
                panel.TransformVector(Vector3.up * (minimumHeight - panel.rect.height)));
            bounds.yMin -= Mathf.Abs(extra.y);
        }
        Rect clip = viewport == null ? parent.rect : GetPanelBounds(viewport, parent);
        bounds = Rect.MinMaxRect(Mathf.Max(bounds.xMin, Mathf.Max(clip.xMin, parent.rect.xMin)),
            Mathf.Max(bounds.yMin, Mathf.Max(clip.yMin, parent.rect.yMin)),
            Mathf.Min(bounds.xMax, Mathf.Min(clip.xMax, parent.rect.xMax)),
            Mathf.Min(bounds.yMax, Mathf.Min(clip.yMax, parent.rect.yMax)));
        if (bounds.width <= 0 || bounds.height <= 0) { return; }
        PanelBounds[panelCount++] = bounds;
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
    /// 刷新附近信号及百分比；首遗物不公开名称和坐标。
    /// </summary>
    public static void Update() {
        if (GameMain.isRunning && GameMain.mainPlayer != null) { ExplorationDetector.UpdateNavigation(); }
        EnsureCreated();
        if (!root) {
            ExplorationRouteView.Update(false);
            CloseGuideLine();
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
        ExplorationRouteView.Update(visible);
        DetectorReading reading = visible ? ExplorationDetector.Read() : default;
        root.SetActive(visible && reading.Visible);
        if (!visible || !reading.Visible) { CloseGuideLine(); return; }
        displayedMarkId = reading.MarkId;
        bool tracking = ExplorationDetector.TrackingEnabled;
        bool canNavigate = displayedMarkId != 0 || tracking;
        entry.SetColorTheme0(reading.ShowStrength);
        bool hasName = !string.IsNullOrEmpty(reading.Name);
        title.text = reading.Name;
        titleRect.gameObject.SetActive(hasName);
        float titleHeight = hasName ? uiGame.dfMonitor.groupLabelHeight : 0f;
        rootRect.sizeDelta = new Vector2(rootRect.sizeDelta.x, titleHeight + uiGame.dfMonitor.entryHeight);
        entry.rectTrans.anchoredPosition = new Vector2(entry.rectTrans.anchoredPosition.x, -titleHeight);
        float strength = Mathf.Clamp01(reading.Strength);
        entry.threatValueText.text = reading.ShowStrength ? $"{strength * 100f:0.0}%" : string.Empty;
        entry.threatBar.rectTransform.anchoredPosition = new Vector2(-130f + Mathf.Round(strength * 100f),
            entry.threatBar.rectTransform.anchoredPosition.y);
        entry.entryButton.enabled = entry.thrtButton.enabled = entry.expButton.enabled = canNavigate;
        entry.trackImage.gameObject.SetActive(tracking && displayedMarkId != 0);
    }

    /// <summary>与原版监测器同在UI晚更新阶段，使用相机更新后的坐标绘制连线。</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIGame), nameof(UIGame._OnLateUpdate))]
    public static void UIGame_OnLateUpdate_Postfix(UIGame __instance) {
        if (!root || !root.activeInHierarchy || !__instance.active || GameMain.isPaused
            || __instance.hideAllUI0 || __instance.hideAllUI1) {
            CloseGuideLine();
            return;
        }
        UpdatePosition(__instance);
        UpdateGuideLine();
    }

    /// <summary>沿用黑雾条目OpenGuideLine的投影和线条参数，仅替换目标位置与颜色。</summary>
    private static void UpdateGuideLine() {
        if (!ReferenceEquals(lineGalaxy, GameMain.galaxy)) { CloseGuideLine(); lineGalaxy = GameMain.galaxy; }
        if (!ExplorationDetector.TryGetGuidePosition(out VectorLF3 target) || GameCamera.main == null
            || GameCamera.instance.hdScreenshot.preparing) { CloseGuideLine(); return; }
        Vector3 screen = UIRoot.instance.overlayCanvas.worldCamera.WorldToScreenPoint(entry.trackImage.transform.position);
        Vector3 start = GameCamera.main.ScreenPointToRay(screen).GetPoint(4.5f);
        VectorLF3 relative = Maths.QInvRotateLF(GameMain.data.relativeRot, target - GameMain.data.relativePos);
        UniverseSimulator.VirtualMapping(relative.x, relative.y, relative.z, GameCamera.main.transform.position,
            out Vector3 end, out _);
        if (!guideLine) {
            guideLine = LineGizmo.Create(1, start, end);
            guideLine.autoRefresh = false;
            guideLine.multiplier = 1.5f;
            guideLine.alphaMultiplier = 0.4f;
            guideLine.width = 0.15f;
            guideLine.color = new Color(0.3f, 0.65f, 1f, 1f);
            guideLine.spherical = false;
            guideLine.Open();
        } else {
            guideLine.startPoint = start;
            guideLine.endPoint = end;
            guideLine.ManualRefresh();
        }
    }

    private static void CloseGuideLine() {
        if (guideLine) { guideLine.Close(); }
        guideLine = null;
    }
}
