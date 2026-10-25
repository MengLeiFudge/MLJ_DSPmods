using System.Collections.Generic;
using FE.Logic.Civilization;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FE.UI.MainPanel.Civilization;

/// <summary>按信号槽复用原版黑雾监视条目，每条独立显示定位图标与蓝色指引线。</summary>
internal static class ExplorationSignalHud {
    private static GameObject root;
    private static RectTransform rootRect;
    private static readonly List<SignalRow> Rows = [];
    private static readonly List<DetectorReading> Readings = [];
    private static int activeRows;
    private static GalaxyData lineGalaxy;
    private static float HudWidth => rootRect.rect.width;
    private static float HudHeight => rootRect.rect.height;
    private static readonly Vector3[] PanelCorners = new Vector3[4];
    private static readonly Rect[] PanelBounds = new Rect[5];
    private static int panelCount;

    /// <summary>创建共用布局根节点；条目按实际需要增加并复用，不注册黑雾业务事件。</summary>
    public static void EnsureCreated() {
        UIGame uiGame = UIRoot.instance?.uiGame;
        UIDarkFogMonitor monitor = uiGame?.dfMonitor;
        if (root || monitor?.baseEntryPrefab == null || monitor.baseGroupLabel == null
            || UIRoot.instance.overlayCanvas == null) { return; }
        CloseGuideLines();
        Rows.Clear();
        activeRows = 0;
        root = new GameObject("fe-exploration-signal-hud", typeof(RectTransform));
        rootRect = (RectTransform)root.transform;
        rootRect.SetParent(monitor.transform.parent, false);
        rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(0f, 1f);
        rootRect.sizeDelta = new Vector2(monitor.contentRect.rect.width, 0f);
        Camera.onPreCull -= OnCameraPreCull;
        Camera.onPreCull += OnCameraPreCull;
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
        float left = monitor?.baseGroupLabel != null
            ? GetPanelBounds(monitor.baseGroupLabel, parent).xMin - GetPanelBounds(Rows[0].TitleRect, rootRect).xMin
            : screen.xMin + 22f;
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
        // 只保留纵向缓动余量，横向必须跟随原版标题，不能因距离小而锁在旧的左侧位置。
        if (Mathf.Abs(position.x - current.x) < 0.5f
            && Mathf.Abs(position.y - current.y) <= monitor.groupLabelHeight + monitor.entryHeight
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
        float height = HudHeight;
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

    /// <summary>刷新主线、独立任务和质量槽，按全部可见条目的实际高度参与避让。</summary>
    public static void Update() {
        if (GameMain.isRunning && GameMain.mainPlayer != null) { ExplorationDetector.UpdateNavigation(); }
        EnsureCreated();
        if (!root) {
            ExplorationRouteView.Update(false);
            CloseGuideLines();
            return;
        }
        if (!ReferenceEquals(lineGalaxy, GameMain.galaxy)) {
            HideRows();
            lineGalaxy = GameMain.galaxy;
        }
        UIGame uiGame = UIRoot.instance?.uiGame;
        Player player = GameMain.mainPlayer;
        // 与原版黑雾监视器共用所在UI层，保留无黑雾模式下的探索显示。
        bool visible = uiGame?.active == true && GameMain.isRunning && GameMain.gameTick > 0
            && !GameMain.isPaused && !GameMain.isFullscreenPaused && !VFInput.inFullscreenGUI
            && !uiGame.hideAllUI0 && !uiGame.hideAllUI1
            && player != null && player.isAlive && !player.respawning
            && player.controller.actionBuild.blueprintMode == EBlueprintMode.None
            && !uiGame.techTree.active && !uiGame.dashboard.active && !uiGame.dysonEditor.active
            && !uiGame.starmap.active;
        ExplorationRouteView.Update(visible);
        if (!visible) {
            root.SetActive(false);
            HideRows();
            return;
        }
        ExplorationDetector.Read(Readings);
        root.SetActive(Readings.Count > 0);
        UIDarkFogMonitor monitor = uiGame.dfMonitor;
        while (Rows.Count < Readings.Count) { Rows.Add(new SignalRow(monitor, rootRect)); }
        float height = 0f;
        activeRows = Readings.Count;
        for (int i = 0; i < Rows.Count; i++) {
            if (i < activeRows) { height += Rows[i].SetReading(Readings[i], height, monitor); }
            else { Rows[i].Hide(); }
        }
        rootRect.sizeDelta = new Vector2(rootRect.sizeDelta.x, height);
    }

    /// <summary>原版监测器完成布局后，为整组探索条目计算避让位置。</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIGame), nameof(UIGame._OnLateUpdate))]
    public static void UIGame_OnLateUpdate_Postfix(UIGame __instance) {
        if (!root || !root.activeInHierarchy || !__instance.active || GameMain.isPaused
            || __instance.hideAllUI0 || __instance.hideAllUI1) {
            CloseGuideLines();
            return;
        }
        UpdatePosition(__instance);
        UpdateTrackIconLayout(__instance.dfMonitor);
        for (int i = 0; i < activeRows; i++) { Rows[i].UpdateGuideVisibility(); }
    }

    /// <summary>所有条目保留原版图标相对条框的位置，扣除本HUD隐藏的地点名宽度。</summary>
    private static void UpdateTrackIconLayout(UIDarkFogMonitor monitor) {
        UIDarkFogMonitorEntry template = monitor.baseEntryPrefab;
        foreach (UIDarkFogMonitorEntry candidate in monitor.baseEntries) {
            if (candidate != null && candidate.gameObject.activeInHierarchy) { template = candidate; break; }
        }
        RectTransform frame = template.frameImage.rectTransform;
        Vector3 center = template.trackImage.rectTransform.TransformPoint(template.trackImage.rectTransform.rect.center);
        Vector3 local = frame.InverseTransformPoint(center);
        float locationWidth = template.locationText.rectTransform.rect.width;
        Vector2 position = new(local.x - frame.rect.xMax - locationWidth, local.y - frame.rect.center.y);
        Vector2 size = template.trackImage.rectTransform.rect.size;
        for (int i = 0; i < activeRows; i++) { Rows[i].UpdateTrackIconLayout(position, size); }
    }

    /// <summary>在主相机实际渲染前投影每条蓝线，使起点跟随各自图标的最终屏幕位置。</summary>
    private static void OnCameraPreCull(Camera camera) {
        if (camera != GameCamera.main) { return; }
        UIGame uiGame = UIRoot.instance?.uiGame;
        if (!root || !root.activeInHierarchy || uiGame?.active != true || GameMain.isPaused
            || uiGame.hideAllUI0 || uiGame.hideAllUI1 || GameCamera.instance.hdScreenshot.preparing) {
            CloseGuideLines();
            return;
        }
        for (int i = 0; i < activeRows; i++) {
            if (Rows[i].UpdateGuideVisibility()) { Rows[i].UpdateGuideLine(); }
        }
    }

    /// <summary>关闭所有可见或回收到池中的条目连线。</summary>
    private static void CloseGuideLines() {
        foreach (SignalRow row in Rows) { row.CloseGuideLine(); }
    }

    /// <summary>界面隐藏或切档时回收条目，清除悬停预览但保留逻辑域的固定选择。</summary>
    private static void HideRows() {
        foreach (SignalRow row in Rows) { row.Hide(); }
        activeRows = 0;
    }

    /// <summary>一条信号的原版控件、悬停抑制和连线，复用时不携带前一目标的预览状态。</summary>
    private sealed class SignalRow {
        public readonly RectTransform TitleRect;
        private readonly UIDarkFogMonitorEntry entry;
        private readonly Text title;
        private readonly Color trackIconColor;
        private LineGizmo guideLine;
        private int markId;
        private bool suppressHoverGuide;

        /// <summary>复制原版视觉组件，将交互绑定到本条当前目标。</summary>
        public SignalRow(UIDarkFogMonitor monitor, RectTransform parent) {
            TitleRect = Object.Instantiate(monitor.baseGroupLabel, parent, false);
            title = TitleRect.GetComponentInChildren<Text>(true);
            entry = Object.Instantiate(monitor.baseEntryPrefab, parent, false);
            entry.enabled = false;
            entry.monitor = monitor;
            foreach (Localizer localizer in TitleRect.GetComponentsInChildren<Localizer>(true)) { localizer.enabled = false; }
            foreach (Localizer localizer in entry.GetComponentsInChildren<Localizer>(true)) { localizer.enabled = false; }
            entry.idText.gameObject.SetActive(false);
            entry.levelText.gameObject.SetActive(false);
            entry.locationText.gameObject.SetActive(false);
            entry.expBar.gameObject.SetActive(false);
            entry.alertImage.gameObject.SetActive(false);
            entry.alertToggle.gameObject.SetActive(false);
            // 条框带有Mask；定位图标必须挂在条目根节点，才能显示在条框外侧。
            entry.locationText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
            entry.trackImage.rectTransform.SetParent(entry.rectTrans, false);
            entry.trackImage.rectTransform.anchorMin = entry.trackImage.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            entry.trackImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            entry.trackImage.rectTransform.localScale = Vector3.one;
            entry.trackImage.enabled = true;
            entry.trackImage.raycastTarget = false;
            entry.trackImage.gameObject.SetActive(false);
            entry.iconImage.gameObject.SetActive(false);
            entry.iconTintImage.gameObject.SetActive(false);
            trackIconColor = entry.trackImage.color;
            foreach (UIButton button in new[] { entry.entryButton, entry.thrtButton, entry.expButton }) {
                button.onClick -= entry.OnEntryClick;
                button.onClick += _ => {
                    ExplorationDetector.ToggleTracking(markId);
                    suppressHoverGuide = !ExplorationDetector.IsTracking(markId);
                    Update();
                };
                // 图标由本条固定/悬停状态控制，不跟随按钮普通态淡出。
                foreach (UIButton.Transition transition in button.transitions ?? []) {
                    if (transition.target == entry.trackImage && transition.mouseoverColor.a > trackIconColor.a) {
                        trackIconColor = transition.mouseoverColor;
                    }
                }
                if (button.transitions != null) {
                    button.transitions = System.Array.FindAll(button.transitions, transition => transition.target != entry.trackImage);
                }
                button.tips.tipTitle = button.tips.tipText = string.Empty;
                if (button.button != null) { button.button.onClick = new Button.ButtonClickedEvent(); }
            }
            if (trackIconColor.a <= 0f) { trackIconColor = monitor.frameColor0; }
            Hide();
        }

        /// <summary>刷新内容及纵向位置，返回本条标题和信号行占用的总高度。</summary>
        public float SetReading(DetectorReading reading, float offset, UIDarkFogMonitor monitor) {
            if (markId != reading.MarkId) {
                entry.gameObject.SetActive(false);
                suppressHoverGuide = false;
                CloseGuideLine();
            }
            markId = reading.MarkId;
            entry.gameObject.SetActive(true);
            entry.SetColorTheme0(reading.ShowStrength);
            bool hasName = !string.IsNullOrEmpty(reading.Name);
            title.text = reading.Name;
            TitleRect.gameObject.SetActive(hasName);
            TitleRect.anchoredPosition = new Vector2(TitleRect.anchoredPosition.x, -offset - 1f);
            entry.levelText.text = reading.Level > 0 ? reading.Level.ToString() : string.Empty;
            entry.levelText.gameObject.SetActive(reading.Level > 0);
            float titleHeight = hasName ? monitor.groupLabelHeight : 0f;
            entry.rectTrans.anchoredPosition = new Vector2(entry.rectTrans.anchoredPosition.x, -offset - titleHeight);
            float strength = Mathf.Clamp01(reading.Strength);
            // 未满的强度不能四舍五入显示100%，避免把接近误当成已经到达。
            float percent = strength >= 1f ? 100f : Mathf.Min(99.9f, Mathf.Floor(strength * 1000f) / 10f);
            entry.threatValueText.text = reading.ShowStrength ? $"{percent:0.0}%" : string.Empty;
            entry.threatBar.rectTransform.anchoredPosition = new Vector2(-130f + Mathf.Round(strength * 100f),
                entry.threatBar.rectTransform.anchoredPosition.y);
            entry.entryButton.enabled = entry.thrtButton.enabled = entry.expButton.enabled = markId != 0 || reading.Level > 0;
            return titleHeight + monitor.entryHeight;
        }

        /// <summary>对齐本条定位图标，并保持独立保存的可见主题色。</summary>
        public void UpdateTrackIconLayout(Vector2 position, Vector2 size) {
            entry.trackImage.rectTransform.anchoredPosition = position;
            entry.trackImage.rectTransform.sizeDelta = size;
            entry.trackImage.color = trackIconColor;
        }

        /// <summary>图标与蓝线共用本条可见条件，取消后移出本条才恢复悬停预览。</summary>
        public bool UpdateGuideVisibility() {
            bool hovering = entry.entryButton._isPointerEnter || entry.thrtButton._isPointerEnter
                || entry.expButton._isPointerEnter;
            if (!hovering) { suppressHoverGuide = false; }
            bool visible = markId != 0 && (ExplorationDetector.IsTracking(markId) || hovering && !suppressHoverGuide);
            entry.trackImage.gameObject.SetActive(visible);
            if (!visible) { CloseGuideLine(); }
            return visible;
        }

        /// <summary>沿用黑雾条目的线条参数，从本条定位图标中心投影到自己的目标。</summary>
        public void UpdateGuideLine() {
            if (!ExplorationDetector.TryGetGuidePosition(markId, out VectorLF3 target)) { CloseGuideLine(); return; }
            entry.rectTrans.ForceUpdateRectTransforms();
            RectTransform track = entry.trackImage.rectTransform;
            Vector3 center = track.TransformPoint(track.rect.center);
            Vector3 screen = UIRoot.instance.overlayCanvas.worldCamera.WorldToScreenPoint(center);
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

        /// <summary>条目暂时不用时隐藏控件并清除前一目标的瞬时状态。</summary>
        public void Hide() {
            TitleRect.gameObject.SetActive(false);
            entry.gameObject.SetActive(false);
            markId = 0;
            suppressHoverGuide = false;
            CloseGuideLine();
        }

        /// <summary>释放本条连线，不影响其他信号。</summary>
        public void CloseGuideLine() {
            if (guideLine) { guideLine.Close(); }
            guideLine = null;
        }
    }
}
