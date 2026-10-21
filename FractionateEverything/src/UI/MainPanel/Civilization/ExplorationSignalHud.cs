using FE.Logic.Civilization;
using UnityEngine;
using UnityEngine.UI;

namespace FE.UI.MainPanel.Civilization;

/// <summary>
/// 呈现连续图形信号条，沿用原版HUD隐藏和避让规则。
/// </summary>
internal static class ExplorationSignalHud {
    private const float HudWidth = 180f;
    private const float RowHeight = 24f;
    private static readonly Text[] RowTexts = new Text[1];
    private static GameObject root;
    /// <summary>HUD所在层的局部矩形。</summary>
    private static RectTransform rootRect;
    /// <summary>复用原版面板四角，避免动态布局每帧分配数组。</summary>
    private static readonly Vector3[] PanelCorners = new Vector3[4];
    /// <summary>教学、黑雾、手动研究、研究队列与科技提示的可见边界。</summary>
    private static readonly Rect[] PanelBounds = new Rect[5];
    private static int panelCount;
    private static RectTransform strengthFill;
    private static GameObject strengthBar;
    private static Button navigationButton;
    private static Text navigationText;
    private static int displayedMarkId;

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
        rootRect.sizeDelta = new Vector2(HudWidth, RowHeight * 2f + 14f);

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
        CreateSignalBar();
        Image buttonBackground = new GameObject("signal-navigation", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        buttonBackground.transform.SetParent(rootRect, false);
        buttonBackground.color = new Color(0f, 0f, 0f, 0.65f);
        buttonBackground.rectTransform.anchorMin = buttonBackground.rectTransform.anchorMax
            = buttonBackground.rectTransform.pivot = new Vector2(0f, 1f);
        buttonBackground.rectTransform.anchoredPosition = new Vector2(0f, -RowHeight - 14f);
        buttonBackground.rectTransform.sizeDelta = new Vector2(HudWidth, RowHeight);
        navigationButton = buttonBackground.gameObject.AddComponent<Button>();
        navigationButton.targetGraphic = buttonBackground;
        navigationText = Object.Instantiate(RowTexts[0], buttonBackground.transform, false);
        navigationText.name = "navigation-label";
        navigationText.rectTransform.anchoredPosition = Vector2.zero;
        navigationText.alignment = TextAnchor.MiddleCenter;
        navigationText.horizontalOverflow = HorizontalWrapMode.Wrap;
        navigationText.resizeTextForBestFit = true;
        navigationText.resizeTextMinSize = 10;
        navigationText.resizeTextMaxSize = 16;
        navigationButton.onClick.AddListener(() => {
            ExplorationDetector.ToggleTracking(displayedMarkId);
            Update();
        });
        root.SetActive(false);
    }

    /// <summary>纯图形背景与填充共用固定宽度，条本身不拦截鼠标。</summary>
    private static void CreateSignalBar() {
        Image background = new GameObject("signal-bar", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        background.transform.SetParent(rootRect, false);
        strengthBar = background.gameObject;
        background.color = new Color(0f, 0f, 0f, 0.75f);
        background.raycastTarget = false;
        RectTransform bar = background.rectTransform;
        bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0f, 1f);
        bar.anchoredPosition = new Vector2(0f, -RowHeight);
        bar.sizeDelta = new Vector2(HudWidth, 12f);
        Image fill = new GameObject("signal-strength", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        fill.transform.SetParent(bar, false);
        fill.color = new Color(0.4f, 0.85f, 1f, 0.95f);
        fill.raycastTarget = false;
        strengthFill = fill.rectTransform;
        strengthFill.anchorMin = strengthFill.anchorMax = strengthFill.pivot = new Vector2(0f, 1f);
        strengthFill.anchoredPosition = new Vector2(2f, -2f);
        strengthFill.sizeDelta = new Vector2(0f, 8f);
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
        Vector2 position = new(screen.xMin + 22f, Mathf.Min(safe.yMax, bottom - 10f));
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
        float height = RowHeight * 2f + 14f;
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
    /// 刷新附近信号，首遗物仅显示图形，不输出坐标和百分比。
    /// </summary>
    public static void Update() {
        if (GameMain.isRunning && GameMain.mainPlayer != null) { ExplorationDetector.UpdateNavigation(); }
        EnsureCreated();
        if (!root) {
            ExplorationRouteView.Update(false);
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
        if (!visible || !reading.Visible) { return; }
        UpdatePosition(uiGame);
        RowTexts[0].text = reading.Name;
        strengthFill.sizeDelta = new Vector2((HudWidth - 4f) * reading.Strength, 8f);
        strengthBar.SetActive(reading.ShowStrength);
        displayedMarkId = reading.MarkId;
        navigationButton.gameObject.SetActive(displayedMarkId != 0);
        navigationText.text = (ExplorationDetector.IsTracking(displayedMarkId) ? "取消指引" : "开启指引").Translate();
    }
}
