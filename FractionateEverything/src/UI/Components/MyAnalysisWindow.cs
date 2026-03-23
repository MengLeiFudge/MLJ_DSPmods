using System;
using System.Collections.Generic;
using System.Reflection;
using FE.UI.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FE.UI.Components;

public class MyAnalysisWindow : MyWindow {
    // Analysis 面板内容区标准：黑色底板固定为 1102x787。
    // 所有页面统一保留四边 10px gap，设计基准区为 1082x767，左上角基点为 (10,10)。
    private const float AnalysisBlackAreaWidth = 1102f;
    private const float AnalysisBlackAreaHeight = 787f;
    private const float AnalysisContentGap = 10f;
    private const float AnalysisDesignWidth = AnalysisBlackAreaWidth - AnalysisContentGap * 2f;
    private const float AnalysisDesignHeight = AnalysisBlackAreaHeight - AnalysisContentGap * 2f;

    private const float LegacyProxyContentWidth = AnalysisDesignWidth;
    private const float LegacyProxyContentHeight = AnalysisDesignHeight;

    private readonly struct ButtonPose {
        public readonly Vector2 AnchorMin;
        public readonly Vector2 AnchorMax;
        public readonly Vector2 Pivot;
        public readonly Vector2 AnchoredPosition;
        public readonly Vector2 SizeDelta;
        public readonly Vector3 LocalScale;
        public readonly Quaternion LocalRotation;

        public ButtonPose(RectTransform rect) {
            AnchorMin = rect.anchorMin;
            AnchorMax = rect.anchorMax;
            Pivot = rect.pivot;
            AnchoredPosition = rect.anchoredPosition;
            SizeDelta = rect.sizeDelta;
            LocalScale = rect.localScale;
            LocalRotation = rect.localRotation;
        }

        public void ApplyTo(RectTransform rect) {
            rect.anchorMin = AnchorMin;
            rect.anchorMax = AnchorMax;
            rect.pivot = Pivot;
            rect.anchoredPosition = AnchoredPosition;
            rect.sizeDelta = SizeDelta;
            rect.localScale = LocalScale;
            rect.localRotation = LocalRotation;
        }
    }

    private sealed class DragWindowForwarder : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler {
        private const float DragThresholdPixels = 5f;

        private RectTransform windowRect;
        private Canvas rootCanvas;
        private MyAnalysisWindow owner;
        private bool pointerDown;
        private bool dragTriggered;

        public void Init(RectTransform window, MyAnalysisWindow host) {
            windowRect = window;
            rootCanvas = window?.GetComponentInParent<Canvas>();
            owner = host;
            pointerDown = false;
            dragTriggered = false;
        }

        public void OnPointerDown(PointerEventData eventData) {
            pointerDown = true;
            dragTriggered = false;
        }

        public void OnPointerUp(PointerEventData eventData) {
            pointerDown = false;
            dragTriggered = false;
        }

        public void OnDrag(PointerEventData eventData) {
            if (windowRect == null) {
                return;
            }

            if (pointerDown && !dragTriggered) {
                float sqrDist = (eventData.position - eventData.pressPosition).sqrMagnitude;
                if (sqrDist >= DragThresholdPixels * DragThresholdPixels) {
                    dragTriggered = true;
                    owner?.MarkTopCategoryDragTriggered();
                }
            }

            float scale = rootCanvas != null && rootCanvas.scaleFactor > 0f ? rootCanvas.scaleFactor : 1f;
            windowRect.anchoredPosition += eventData.delta / scale;
        }
    }

    private RectTransform windowTrans;
    private RectTransform contentRootContainer;
    private UIButton switchMainPanelButton;
    private readonly List<UIButton> topCategoryButtons = [];
    private readonly List<UIButton> leftSubpageButtons = [];
    private readonly Dictionary<UIButton, Action<int>> uiButtonClickHandlers = [];
    private Vector2 analysisWindowSize;

    private int selectedTopCategoryIndex = -1;
    private int selectedSubpageIndex = -1;
    private RectTransform currentPageContent;
    private MainWindowPageDefinition currentPageDef;

    private RectTransform nativeVerticalTab;
    private RectTransform nativeHorizontalTab;
    private RectTransform headerCategoryHost;
    private RectTransform contentPanelHost;
    private readonly List<UIButton> nativeHorizontalButtonSlots = [];
    private readonly List<UIButton> nativeVerticalButtonSlots = [];
    private readonly List<UIButton> nativeLeftSubpageButtonSlots = [];
    private readonly List<ButtonPose> nativeHorizontalButtonPoses = [];
    private readonly List<ButtonPose> nativeVerticalButtonPoses = [];
    private readonly List<ButtonPose> nativeLeftSubpageButtonPoses = [];
    private UIButton nativeTopCategoryTemplateButton;
    private UIButton nativeTopCategoryHighlightStyleTemplateButton;
    private ButtonPose nativeTopCategoryTemplatePose;
    private bool hasNativeTopCategoryTemplatePose;
    private float topCategoryStepX = TopCategoryButtonWidth;
    private const float TopCategoryBaseX = 10f;
    private const float TopCategoryBaseY = 0f;
    /// <summary>
    /// 上方主导航按钮的宽度
    /// </summary>
    private const float TopCategoryButtonWidth = 150f;
    private float nativeHorizontalTabOriginalHeight;
    private bool suppressTopCategoryClickOnce;

    public static MyAnalysisWindow CreateInstance(string name, string title = "") {
        UIStatisticsWindow src = UIRoot.instance?.uiGame?.statWindow;
        if (src == null) {
            return MyWindowManager.CreateWindow<MyAnalysisWindow>(name, title);
        }

        GameObject go = Instantiate(src.gameObject, src.transform.parent);
        go.name = name;
        go.SetActive(false);

        MyAnalysisWindow win = go.AddComponent<MyAnalysisWindow>();

        win.SetTitle(title);
        win._Create();

        try {
            var windowsField =
                typeof(MyWindowManager).GetField("Windows", BindingFlags.Static | BindingFlags.NonPublic);
            if (windowsField != null) {
                var windowsList = (List<ManualBehaviour>)windowsField.GetValue(null);
                windowsList.Add(win);
            }
        }
        catch (Exception e) {
            Debug.LogError($"Failed to register MyAnalysisWindow to MyWindowManager: {e}");
        }

        if (MyWindowManager.Initialized) {
            win._Init(win.data);
        }
        return win;
    }

    public static void DestroyInstance(MyAnalysisWindow win) {
        MyWindowManager.DestroyWindow(win);
    }

    public void OpenWindow() {
        Open();
    }

    public void CloseWindow() {
        Close();
    }

    public override void _OnCreate() {
        windowTrans = GetComponent<RectTransform>();

        UIStatisticsWindow stat = GetComponent<UIStatisticsWindow>();
        if (stat != null) {
            CaptureNativeNavigationButtons(stat);
            if (stat.verticalTab != null) {
                nativeVerticalTab = stat.verticalTab.GetComponent<RectTransform>();
            }
            if (stat.horizontalTab != null) {
                nativeHorizontalTab = stat.horizontalTab.GetComponent<RectTransform>();
                nativeHorizontalTabOriginalHeight = nativeHorizontalTab != null ? nativeHorizontalTab.rect.height : 0f;
            }

            HideNativeElements(stat);
            DestroyImmediate(stat);
        }

        var btn = transform.Find("panel-bg")?.gameObject.GetComponentInChildren<Button>();
        if (btn) {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(CloseWindow);
        }

        CreateContainerSkeleton();
        ConfigureTopCategoryButtons();
        gameObject.SetActive(false);
    }

    private void CaptureNativeNavigationButtons(UIStatisticsWindow stat) {
        nativeHorizontalButtonSlots.Clear();
        nativeVerticalButtonSlots.Clear();
        nativeLeftSubpageButtonSlots.Clear();
        nativeHorizontalButtonPoses.Clear();
        nativeVerticalButtonPoses.Clear();
        nativeLeftSubpageButtonPoses.Clear();

        // 注意：horizontal/vertical 两组都属于左侧标签体系（左上到左下），
        // 区别仅在按钮模板文本排布方向：horizontal 偏横排、vertical 偏竖排。
        // 不要把这两组误解为“上方导航 vs 左侧导航”的关系。
        AddButtonSlot(nativeHorizontalButtonSlots, stat.horMilUIBtn);
        AddButtonSlot(nativeHorizontalButtonSlots, stat.horDashUIBtn);
        AddButtonSlot(nativeHorizontalButtonSlots, stat.horProUIBtn);
        AddButtonSlot(nativeHorizontalButtonSlots, stat.horPowUIBtn);
        AddButtonSlot(nativeHorizontalButtonSlots, stat.horResUIBtn);
        AddButtonSlot(nativeHorizontalButtonSlots, stat.horDysUIBtn);
        AddButtonSlot(nativeHorizontalButtonSlots, stat.horKilUIBtn);
        AddButtonSlot(nativeHorizontalButtonSlots, stat.horAchUIBtn);
        AddButtonSlot(nativeHorizontalButtonSlots, stat.horPrpUIBtn);
        AddButtonSlot(nativeHorizontalButtonSlots, stat.horPrfUIBtn);

        AddButtonSlot(nativeVerticalButtonSlots, stat.verMilUIBtn);
        AddButtonSlot(nativeVerticalButtonSlots, stat.verProUIBtn);
        AddButtonSlot(nativeVerticalButtonSlots, stat.verPowUIBtn);
        AddButtonSlot(nativeVerticalButtonSlots, stat.verResUIBtn);
        AddButtonSlot(nativeVerticalButtonSlots, stat.verDysUIBtn);
        AddButtonSlot(nativeVerticalButtonSlots, stat.verAchUIBtn);
        AddButtonSlot(nativeVerticalButtonSlots, stat.verPrpUIBtn);
        AddButtonSlot(nativeVerticalButtonSlots, stat.verPrfUIBtn);

        AddButtonSlot(nativeLeftSubpageButtonSlots, stat.horMilUIBtn);
        AddButtonSlot(nativeLeftSubpageButtonSlots, stat.horProUIBtn);
        AddButtonSlot(nativeLeftSubpageButtonSlots, stat.horPowUIBtn);
        AddButtonSlot(nativeLeftSubpageButtonSlots, stat.horResUIBtn);
        AddButtonSlot(nativeLeftSubpageButtonSlots, stat.horDysUIBtn);
        AddButtonSlot(nativeLeftSubpageButtonSlots, stat.horKilUIBtn);
        AddButtonSlot(nativeLeftSubpageButtonSlots, stat.horPrfUIBtn);
        AddButtonSlot(nativeLeftSubpageButtonSlots, stat.horAchUIBtn);
        AddButtonSlot(nativeLeftSubpageButtonSlots, stat.horPrpUIBtn);

        CacheButtonPoses(nativeHorizontalButtonSlots, nativeHorizontalButtonPoses);
        CacheButtonPoses(nativeVerticalButtonSlots, nativeVerticalButtonPoses);
        CacheButtonPoses(nativeLeftSubpageButtonSlots, nativeLeftSubpageButtonPoses);

        nativeTopCategoryTemplateButton = stat.horDashUIBtn;
        if (nativeTopCategoryTemplateButton == null && nativeHorizontalButtonSlots.Count > 0) {
            nativeTopCategoryTemplateButton = nativeHorizontalButtonSlots[0];
        }

        // 顶部按钮“颜色状态机”模板：沿用左侧子页按钮可正常高亮的样式配置。
        // 这里只用于 normal / hover / pressed / highlighted 的颜色与缩放逻辑，
        // 不用于顶部按钮的位置与尺寸。
        nativeTopCategoryHighlightStyleTemplateButton = stat.horMilUIBtn;
        if (nativeTopCategoryHighlightStyleTemplateButton == null && nativeLeftSubpageButtonSlots.Count > 0) {
            nativeTopCategoryHighlightStyleTemplateButton = nativeLeftSubpageButtonSlots[0];
        }

        RectTransform templateRect = nativeTopCategoryTemplateButton?.GetComponent<RectTransform>();
        if (templateRect != null) {
            nativeTopCategoryTemplatePose = new ButtonPose(templateRect);
            hasNativeTopCategoryTemplatePose = true;
            topCategoryStepX = TopCategoryButtonWidth;
        } else {
            hasNativeTopCategoryTemplatePose = false;
            topCategoryStepX = TopCategoryButtonWidth;
        }
    }

    private static void AddButtonSlot(List<UIButton> slots, UIButton button) {
        if (button != null) {
            slots.Add(button);
        }
    }

    private static void CacheButtonPoses(List<UIButton> buttons, List<ButtonPose> poses) {
        poses.Clear();
        foreach (var button in buttons) {
            RectTransform rect = button?.GetComponent<RectTransform>();
            if (rect != null) {
                poses.Add(new ButtonPose(rect));
            }
        }
    }

    private void HideNativeElements(UIStatisticsWindow stat) {
        Transform panelBg = stat.transform.Find("panel-bg");
        if (panelBg != null) {
            // 隐藏原生标题和信息文本，我们要用这个区域
            string[] toHide = ["name-text", "inf-text", "tab-line", "tab-line-v", "title-text", "dashboard-text"];
            foreach (var name in toHide) {
                var t = panelBg.Find(name);
                if (t != null) t.gameObject.SetActive(false);
            }

            headerCategoryHost = transform as RectTransform;
            contentPanelHost = ResolveContentPanelHost(stat, panelBg as RectTransform);
        }

        // 隐藏所有原生面板
        bool reuseProductPanelHost = IsUsingProductPanelHost(stat);
        if (stat.productPanel) stat.productPanel.SetActive(reuseProductPanelHost);
        if (stat.powerPanel) stat.powerPanel.SetActive(false);
        if (stat.researchPanel) stat.researchPanel.SetActive(false);
        if (stat.dysonPanel) stat.dysonPanel.SetActive(false);
        if (stat.killPanel) stat.killPanel.SetActive(false);
        if (stat.performancePanel) stat.performancePanel.SetActive(false);

        if (stat.powerDetailPanel) stat.powerDetailPanel.gameObject.SetActive(false);
        if (stat.dysonDetailPanel) stat.dysonDetailPanel.gameObject.SetActive(false);
        if (stat.performancePanelUI) stat.performancePanelUI.gameObject.SetActive(false);
        if (stat.achievementPanelUI) stat.achievementPanelUI.gameObject.SetActive(false);
        if (stat.milestonePanelUI) stat.milestonePanelUI.gameObject.SetActive(false);
        if (stat.propertyPanelUI) stat.propertyPanelUI.gameObject.SetActive(false);

        // 隐藏原生控制组件
        GameObject[] controls = {
            stat.productSortBox?.gameObject, stat.productTimeBox?.gameObject,
            stat.productAstroBox?.gameObject, stat.powerTimeBox?.gameObject,
            stat.powerAstroBox?.gameObject, stat.researchTimeBox?.gameObject,
            stat.researchAstroBox?.gameObject, stat.dysonTimeBox?.gameObject,
            stat.dysonAstroBox?.gameObject, stat.killSortBox?.gameObject,
            stat.killTimeBox?.gameObject, stat.killAstroBox?.gameObject,
            stat.productNameInputField?.gameObject, stat.filterTagGo,
            stat.newFeatureGo, stat.favoriteFilter1?.gameObject,
            stat.favoriteFilter2?.gameObject, stat.favoriteFilter3?.gameObject,
            stat.favoriteFilter4?.gameObject, stat.favoriteFilter5?.gameObject,
            stat.favoriteFilter6?.gameObject, stat.killFavoriteFilter1?.gameObject,
            stat.killFavoriteFilter2?.gameObject, stat.killFavoriteFilter3?.gameObject
        };
        foreach (var c in controls)
            if (c)
                c.SetActive(false);

        if (panelBg != null) {
            Transform productBg = panelBg.Find("product-bg");
            if (productBg != null) {
                DisableAllChildrenGameObjects(productBg);
            }
        }

        if (stat.horizontalTab != null) stat.horizontalTab.SetActive(true);
        if (stat.verticalTab != null) stat.verticalTab.SetActive(false);

    }

    private static void DisableAllChildrenGameObjects(Transform parent) {
        if (parent == null) {
            return;
        }

        for (int i = 0; i < parent.childCount; i++) {
            Transform child = parent.GetChild(i);
            DisableAllChildrenGameObjects(child);
            child.gameObject.SetActive(false);
        }
    }

    private bool IsUsingProductPanelHost(UIStatisticsWindow stat) {
        if (contentPanelHost == null || stat?.productPanel == null) {
            return false;
        }

        Transform product = stat.productPanel.transform;
        return contentPanelHost == product
               || contentPanelHost.IsChildOf(product)
               || product.IsChildOf(contentPanelHost);
    }

    private RectTransform ResolveContentPanelHost(UIStatisticsWindow stat, RectTransform panelBg) {
        if (panelBg == null) {
            return null;
        }

        RectTransform nativeShell = TryResolveNativeContentShell(stat, panelBg);

        if (nativeShell != null) {
            nativeShell.SetParent(panelBg, false);
            nativeShell.gameObject.SetActive(true);

            if (stat.scrollContentRect != null) stat.scrollContentRect.gameObject.SetActive(false);
            if (stat.killScrollContentRect != null) stat.killScrollContentRect.gameObject.SetActive(false);
            if (stat.scrollVbarRect != null) stat.scrollVbarRect.gameObject.SetActive(false);
            if (stat.killScrollVbarRect != null) stat.killScrollVbarRect.gameObject.SetActive(false);
            if (stat.productEntry != null) stat.productEntry.gameObject.SetActive(false);
            if (stat.killEntry != null) stat.killEntry.gameObject.SetActive(false);
            if (stat.powerEntry != null) stat.powerEntry.gameObject.SetActive(false);
            if (stat.researchEntry != null) stat.researchEntry.gameObject.SetActive(false);
            if (stat.dysonEntry1 != null) stat.dysonEntry1.gameObject.SetActive(false);
            if (stat.dysonEntry2 != null) stat.dysonEntry2.gameObject.SetActive(false);
            if (stat.dysonEntry3 != null) stat.dysonEntry3.gameObject.SetActive(false);
            if (stat.scrollRect != null) stat.scrollRect.enabled = false;
            if (stat.killScrollRect != null) stat.killScrollRect.enabled = false;

            return nativeShell;
        }

        RectTransform fallback = CreateContainerRect(
            "analysis-content-panel-fallback",
            panelBg,
            new Vector2(236f, 60f),
            new Vector2(-36f, -120f),
            new Vector2(0f, 0f),
            new Vector2(1f, 1f));
        var image = fallback.gameObject.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.62f);
        return fallback;
    }

    private static RectTransform TryResolveNativeContentShell(UIStatisticsWindow stat, RectTransform panelBg) {
        if (stat?.productPanel != null) {
            var productRect = stat.productPanel.GetComponent<RectTransform>();
            if (productRect != null) {
                return productRect;
            }
        }

        RectTransform viewport = stat?.scrollViewportRect;
        if (viewport == null) {
            return null;
        }

        RectTransform fallback = viewport.parent as RectTransform;
        RectTransform current = fallback;
        while (current != null && current != panelBg) {
            if (current.GetComponent<Image>() != null) {
                return current;
            }

            current = current.parent as RectTransform;
        }

        return fallback ?? viewport;
    }


    public override bool _OnInit() {
        if (!base._OnInit()) return false;
        if (windowTrans != null) {
            windowTrans.anchoredPosition = new(0f, 0f);
        }
        return true;
    }

    public override void _OnOpen() {
        base._OnOpen();
        ApplyStatWindowSize();
        RefreshTopCategoryButtonLayout();
        RefreshSwitchMainPanelButtonLabel();
        if (selectedTopCategoryIndex == -1 && topCategoryButtons.Count > 0) {
            OnTopCategoryClick(0);
        } else {
            RefreshLeftSubpages();
        }
    }

    public override void _OnUpdate() {
        base._OnUpdate();
        UpdateTopCategoryHighlights();
        RefreshSwitchMainPanelButtonLabel();
        if (currentPageDef != null && currentPageContent != null && currentPageContent.gameObject.activeSelf) {
            currentPageDef.UpdateUI();
        }
    }

    public override void _OnFree() {
        base._OnFree();
    }

    public override void _OnDestroy() {
        topCategoryButtons.Clear();
        leftSubpageButtons.Clear();
        foreach (var pair in uiButtonClickHandlers) {
            if (pair.Key != null) {
                pair.Key.onClick -= pair.Value;
            }
        }
        uiButtonClickHandlers.Clear();
        nativeHorizontalButtonSlots.Clear();
        nativeVerticalButtonSlots.Clear();
        nativeLeftSubpageButtonSlots.Clear();
        nativeHorizontalButtonPoses.Clear();
        nativeVerticalButtonPoses.Clear();
        nativeLeftSubpageButtonPoses.Clear();
        nativeTopCategoryTemplateButton = null;
        nativeTopCategoryHighlightStyleTemplateButton = null;
        hasNativeTopCategoryTemplatePose = false;
        topCategoryStepX = TopCategoryButtonWidth;
        contentRootContainer = null;
        switchMainPanelButton = null;
        windowTrans = null;
        analysisWindowSize = default;
        currentPageContent = null;
        currentPageDef = null;
        base._OnDestroy();
    }

    private void CreateContainerSkeleton() {
        RectTransform host = contentPanelHost;
        if (host == null) {
            host = transform.Find("panel-bg") as RectTransform;
        }
        if (host == null) return;

        contentRootContainer = CreateContainerRect(
            "analysis-content-root",
            host,
            Vector2.zero,
            Vector2.zero,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f));

        // 统一中间黑色区标准尺寸，后续页面都基于这个区域计算布局。
        contentRootContainer.anchorMin = new Vector2(0f, 1f);
        contentRootContainer.anchorMax = new Vector2(0f, 1f);
        contentRootContainer.pivot = new Vector2(0f, 1f);
        contentRootContainer.sizeDelta = new Vector2(AnalysisBlackAreaWidth, AnalysisBlackAreaHeight);
        contentRootContainer.anchoredPosition = Vector2.zero;

        contentRootContainer.SetAsLastSibling();
    }

    private void ConfigureTopCategoryButtons() {
        topCategoryButtons.Clear();
        var categories = MainWindow.AnalysisPageCategories;
        if (categories.Count == 0) return;

        foreach (UIButton slot in nativeHorizontalButtonSlots) {
            if (slot != null) {
                slot.gameObject.SetActive(false);
            }
        }

        EnsureTopCategoryButtonCapacity(categories.Count);

        if (topCategoryButtons.Count == 0) {
            return;
        }

        int visibleCount = Math.Min(categories.Count, topCategoryButtons.Count);

        for (int i = 0; i < visibleCount; i++) {
            int index = i;
            UIButton button = topCategoryButtons[i];
            SetButtonVisible(button, true);
            RestoreImageTransitionTargets(button);
            ApplyTopCategoryTemplateStyle(button);
            NormalizeTopCategoryTextTransitions(button);
            if (button.button != null) {
                button.button.enabled = true;
                button.button.interactable = true;
            }
            BindButtonClick(button, () => OnTopCategoryClick(index));
            SetButtonLabelKeepStyle(button, categories[i].CategoryName);
            RestoreTopCategoryButtonPose(button, i);
            AttachDragForwarding(button);
        }

        for (int i = visibleCount; i < topCategoryButtons.Count; i++) {
            SetButtonVisible(topCategoryButtons[i], false);
        }

        if (selectedTopCategoryIndex < 0 || selectedTopCategoryIndex >= visibleCount) {
            selectedTopCategoryIndex = 0;
        }

        UpdateTopCategoryHighlights();
    }

    private void EnsureTopCategoryButtonCapacity(int requiredCount) {
        UIButton template = nativeTopCategoryTemplateButton;
        if (template == null || headerCategoryHost == null) {
            return;
        }

        Transform[] allTransforms = transform.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++) {
            Transform child = allTransforms[i];
            if (child != null && child.name.StartsWith("analysis-top-category-clone-", StringComparison.Ordinal)) {
                Destroy(child.gameObject);
            }
        }

        for (int i = 0; i < requiredCount; i++) {
            UIButton button = Instantiate(template, headerCategoryHost);
            button.name = $"analysis-top-category-clone-{i}";

            topCategoryButtons.Add(button);
        }

        foreach (UIButton slot in nativeHorizontalButtonSlots) {
            if (slot != null && !topCategoryButtons.Contains(slot)) {
                slot.gameObject.SetActive(false);
            }
        }
    }

    private void OnTopCategoryClick(int index) {
        if (suppressTopCategoryClickOnce) {
            suppressTopCategoryClickOnce = false;
            return;
        }

        var categories = MainWindow.AnalysisPageCategories;
        if (index < 0 || index >= categories.Count) {
            return;
        }

        bool changed = selectedTopCategoryIndex != index;
        selectedTopCategoryIndex = index;
        if (changed) {
            selectedSubpageIndex = -1;
        }

        RefreshLeftSubpages();
        UpdateTopCategoryHighlights();
    }

    private void RefreshLeftSubpages() {
        leftSubpageButtons.Clear();

        // 当前实现约定：左侧子页显示使用 horizontal-tab 这组（横排模板）槽位。
        // vertical-tab 在此流程中整体关闭，防止出现双层标签重叠。
        if (nativeHorizontalTab != null) {
            nativeHorizontalTab.gameObject.SetActive(true);
        }

        if (nativeVerticalTab != null) {
            nativeVerticalTab.gameObject.SetActive(false);
        }

        HideButtons(nativeVerticalButtonSlots);
        HideButtons(nativeLeftSubpageButtonSlots);

        var categories = MainWindow.AnalysisPageCategories;
        if (selectedTopCategoryIndex < 0 || selectedTopCategoryIndex >= categories.Count) return;

        var pages = categories[selectedTopCategoryIndex].Pages;
        EnsureNavigationButtonCapacity(
            leftSubpageButtons,
            nativeLeftSubpageButtonSlots,
            pages.Count,
            nativeHorizontalTab,
            "analysis-left-subpage-clone");

        int visibleCount = Math.Min(pages.Count, leftSubpageButtons.Count);

        for (int i = 0; i < visibleCount; i++) {
            int index = i;
            UIButton button = leftSubpageButtons[i];
            SetButtonVisible(button, true);
            if (button.button != null) {
                button.button.interactable = true;
            }
            EnsureButtonRaycast(button);
            BindButtonClick(button, () => OnSubpageClick(index));
            SetButtonLabelKeepStyle(button, pages[i].SubpageName);
            RestoreLeftSubpageButtonPose(button, i);
        }

        for (int i = visibleCount; i < leftSubpageButtons.Count; i++) {
            SetButtonVisible(leftSubpageButtons[i], false);
        }

        if (visibleCount > 0) {
            if (selectedSubpageIndex < 0 || selectedSubpageIndex >= visibleCount) {
                selectedSubpageIndex = 0;
            }
            OnSubpageClick(selectedSubpageIndex);
        } else {
            currentPageDef = null;
            HideAllPageContent();
        }

        AdjustHorizontalTabHeightByVisibleButtons(visibleCount);

        UpdateLeftSubpageHighlights();
    }

    private void AdjustHorizontalTabHeightByVisibleButtons(int visibleCount) {
        if (nativeHorizontalTab == null) {
            return;
        }

        if (visibleCount <= 0 || leftSubpageButtons.Count == 0) {
            if (nativeHorizontalTabOriginalHeight > 0f) {
                nativeHorizontalTab.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nativeHorizontalTabOriginalHeight);
            }
            return;
        }

        bool hasBound = false;
        float top = 0f;
        float bottom = 0f;

        for (int i = 0; i < visibleCount && i < leftSubpageButtons.Count; i++) {
            UIButton button = leftSubpageButtons[i];
            if (button == null || !button.gameObject.activeSelf) {
                continue;
            }

            RectTransform rect = button.GetComponent<RectTransform>();
            if (rect == null) {
                continue;
            }

            float h = rect.rect.height;
            float t = rect.anchoredPosition.y + (1f - rect.pivot.y) * h;
            float b = rect.anchoredPosition.y - rect.pivot.y * h;

            if (!hasBound) {
                top = t;
                bottom = b;
                hasBound = true;
            } else {
                if (t > top) {
                    top = t;
                }

                if (b < bottom) {
                    bottom = b;
                }
            }
        }

        if (!hasBound) {
            return;
        }

        float targetHeight = Mathf.Max(1f, top - bottom);
        nativeHorizontalTab.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
    }

    private void OnSubpageClick(int index) {
        selectedSubpageIndex = index;

        var categories = MainWindow.AnalysisPageCategories;
        if (selectedTopCategoryIndex < 0 || selectedTopCategoryIndex >= categories.Count) return;

        var pages = categories[selectedTopCategoryIndex].Pages;
        if (selectedSubpageIndex < 0 || selectedSubpageIndex >= pages.Count) return;

        UpdateLeftSubpageHighlights();
        ShowPageContent(pages[selectedSubpageIndex]);
    }

    private void HideAllPageContent() {
        if (contentRootContainer == null) {
            return;
        }

        for (int i = 0; i < contentRootContainer.childCount; i++) {
            Transform child = contentRootContainer.GetChild(i);
            if (child != null) {
                child.gameObject.SetActive(false);
            }
        }

        if (currentPageContent != null) {
            currentPageContent.gameObject.SetActive(false);
        }
    }

    private void ShowPageContent(MainWindowPageDefinition pageDef) {
        if (contentRootContainer == null) return;

        if (currentPageContent != null) {
            currentPageContent.gameObject.SetActive(false);
        }

        currentPageDef = pageDef;
        string contentName = $"analysis-content-{pageDef.CategoryName}-{pageDef.SubpageName}";
        Transform existing = contentRootContainer.Find(contentName);
        if (existing == null) {
            currentPageContent = CreateContainerRect(contentName, contentRootContainer, Vector2.zero, Vector2.zero,
                Vector2.zero, Vector2.one);

            if (pageDef.CreateUIInAnalysis != null) {
                RectTransform designRoot = GetOrCreateDesignRoot(currentPageContent);
                pageDef.CreateUIInAnalysis(this, designRoot);
            } else {
                GameObject proxyGo = new GameObject($"{contentName}-proxy", typeof(MyConfigWindow));
                proxyGo.transform.SetParent(currentPageContent, false);
                MyConfigWindow configWindowProxy = proxyGo.GetComponent<MyConfigWindow>();

                RectTransform designRoot = GetOrCreateDesignRoot(currentPageContent);
                RectTransform legacyProxyHost = CreateLegacyProxyHost(designRoot);
                pageDef.CreateUI(configWindowProxy, legacyProxyHost);
                configWindowProxy.SetCurrentTab(0);
            }

            HideNestedNavigationInContent(currentPageContent);
        } else {
            currentPageContent = existing as RectTransform;
        }

        if (currentPageContent != null) {
            HideNestedNavigationInContent(currentPageContent);
            currentPageContent.gameObject.SetActive(true);
        }
    }

    private static RectTransform CreateLegacyProxyHost(RectTransform parent) {
        float legacyLeftNavSpan = Margin + TabWidth + Spacing;
        float legacyTotalWidth = LegacyProxyContentWidth + legacyLeftNavSpan;
        float legacyTotalHeight = LegacyProxyContentHeight + TitleHeight;

        RectTransform host = CreateContainerRect(
            "analysis-legacy-proxy-host",
            parent,
            Vector2.zero,
            Vector2.zero,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f));

        host.pivot = new Vector2(0f, 1f);
        host.sizeDelta = new Vector2(legacyTotalWidth, legacyTotalHeight);
        host.anchoredPosition = new Vector2(-legacyLeftNavSpan, TitleHeight);
        return host;
    }

    private static RectTransform GetOrCreateDesignRoot(RectTransform pageRoot) {
        if (pageRoot == null) {
            return null;
        }

        Transform existing = pageRoot.Find("analysis-design-root");
        if (existing is RectTransform existingRect) {
            return existingRect;
        }

        return CreateContainerRect(
            "analysis-design-root",
            pageRoot,
            new Vector2(AnalysisContentGap, AnalysisContentGap),
            new Vector2(-AnalysisContentGap, -AnalysisContentGap),
            Vector2.zero,
            Vector2.one);
    }

    private void ApplyStatWindowSize() {
        if (windowTrans == null) return;

        analysisWindowSize = TryGetStatWindowSize(out Vector2 statWindowSize)
            ? statWindowSize
            : new(WindowWidth, WindowHeight);
        windowTrans.sizeDelta = analysisWindowSize;
    }

    private void RefreshTopCategoryButtonLayout() {
        ConfigureTopCategoryButtons();
    }

    private void EnsureNavigationButtonCapacity(List<UIButton> activeButtons, List<UIButton> buttonSlots,
        int requiredCount,
        RectTransform slotParent, string clonePrefix) {
        activeButtons.Clear();
        foreach (var slot in buttonSlots) {
            if (slot != null) {
                if (slotParent != null && slot.transform.parent != slotParent) {
                    slot.transform.SetParent(slotParent, false);
                }
                activeButtons.Add(slot);
            }
        }

        if (requiredCount <= activeButtons.Count || slotParent == null) {
            return;
        }

        UIButton template = activeButtons.Count > 0 ? activeButtons[0] : null;
        while (activeButtons.Count < requiredCount && template != null) {
            UIButton clone = Instantiate(template, slotParent);
            clone.name = $"{clonePrefix}-{activeButtons.Count}";
            buttonSlots.Add(clone);
            activeButtons.Add(clone);
        }
    }

    private void RestoreTopCategoryButtonPose(UIButton button, int index) {
        if (button == null) {
            return;
        }

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null) {
            return;
        }

        if (!hasNativeTopCategoryTemplatePose) {
            return;
        }

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(TopCategoryButtonWidth, nativeTopCategoryTemplatePose.SizeDelta.y);
        rect.localScale = nativeTopCategoryTemplatePose.LocalScale;
        rect.localRotation = nativeTopCategoryTemplatePose.LocalRotation;
        rect.anchoredPosition = new Vector2(TopCategoryBaseX + topCategoryStepX * index, TopCategoryBaseY);
    }

    private void AttachDragForwarding(UIButton button) {
        if (button == null || windowTrans == null) {
            return;
        }

        DragWindowForwarder forwarder = button.GetComponent<DragWindowForwarder>();
        if (forwarder == null) {
            forwarder = button.gameObject.AddComponent<DragWindowForwarder>();
        }

        forwarder.Init(windowTrans, this);
    }

    private void MarkTopCategoryDragTriggered() {
        suppressTopCategoryClickOnce = true;
    }

    private void ApplyTopCategoryTemplateStyle(UIButton targetButton) {
        UIButton templateButton = nativeTopCategoryTemplateButton;
        if (templateButton == null || targetButton == null || targetButton == templateButton) {
            return;
        }

        if (templateButton.button != null && targetButton.button != null) {
            targetButton.button.transition = templateButton.button.transition;
            targetButton.button.colors = templateButton.button.colors;
            targetButton.button.spriteState = templateButton.button.spriteState;
            targetButton.button.navigation = templateButton.button.navigation;
        }

        ApplyTopCategoryBackgroundTransitionStyle(targetButton);

        Image targetImage = targetButton.GetComponent<Image>();
        Image templateImage = templateButton.GetComponent<Image>();
        if (targetImage != null && templateImage != null) {
            targetImage.sprite = templateImage.sprite;
            targetImage.type = templateImage.type;
            targetImage.material = templateImage.material;
            targetImage.color = templateImage.color;
        }

    }

    private static UIButton.Transition FindFirstImageTransition(UIButton button) {
        if (button?.transitions == null) {
            return null;
        }

        for (int i = 0; i < button.transitions.Length; i++) {
            UIButton.Transition transition = button.transitions[i];
            if (transition?.target is Image) {
                return transition;
            }
        }

        return null;
    }

    private static void CopyTransitionParams(UIButton.Transition source, UIButton.Transition target) {
        if (source == null || target == null) {
            return;
        }

        target.damp = source.damp;
        target.mouseoverSize = source.mouseoverSize;
        target.pressedSize = source.pressedSize;
        target.normalColor = source.normalColor;
        target.mouseoverColor = source.mouseoverColor;
        target.pressedColor = source.pressedColor;
        target.disabledColor = source.disabledColor;
        target.alphaOnly = source.alphaOnly;
        target.highlightSizeMultiplier = source.highlightSizeMultiplier;
        target.highlightColorMultiplier = source.highlightColorMultiplier;
        target.highlightAlphaMultiplier = source.highlightAlphaMultiplier;
        target.highlightColorOverride = source.highlightColorOverride;
    }

    private static void NormalizeTopCategoryTextTransitions(UIButton button) {
        if (button?.transitions == null) {
            return;
        }

        for (int i = 0; i < button.transitions.Length; i++) {
            UIButton.Transition transition = button.transitions[i];
            if (transition?.target is Text) {
                transition.highlightSizeMultiplier = 1f;
                transition.highlightColorMultiplier = 1f;
                transition.highlightAlphaMultiplier = 1f;
                transition.highlightColorOverride = transition.mouseoverColor;
            }
        }
    }

    private void ApplyTopCategoryBackgroundTransitionStyle(UIButton targetButton) {
        UIButton sourceButton = nativeTopCategoryHighlightStyleTemplateButton;
        if (sourceButton == null || targetButton == null) {
            return;
        }

        UIButton.Transition sourceBackgroundTransition = FindFirstImageTransition(sourceButton);
        if (sourceBackgroundTransition == null) {
            return;
        }

        UIButton.Transition[] targetTransitions = targetButton.transitions ?? Array.Empty<UIButton.Transition>();
        bool copied = false;
        for (int i = 0; i < targetTransitions.Length; i++) {
            UIButton.Transition transition = targetTransitions[i];
            if (transition?.target is Image) {
                CopyTransitionParams(sourceBackgroundTransition, transition);
                copied = true;
            }
        }

        if (copied) {
            return;
        }

        Image targetImage = targetButton.GetComponent<Image>() ?? targetButton.GetComponentInChildren<Image>(true);
        if (targetImage == null) {
            return;
        }

        UIButton.Transition newTransition = new UIButton.Transition {
            target = targetImage,
        };
        CopyTransitionParams(sourceBackgroundTransition, newTransition);

        UIButton.Transition[] expanded = new UIButton.Transition[targetTransitions.Length + 1];
        Array.Copy(targetTransitions, expanded, targetTransitions.Length);
        expanded[expanded.Length - 1] = newTransition;
        targetButton.transitions = expanded;
    }

    private static void RestoreImageTransitionTargets(UIButton button) {
        if (button?.transitions == null) {
            return;
        }

        Image localImage = button.GetComponent<Image>() ?? button.GetComponentInChildren<Image>(true);
        if (localImage == null) {
            return;
        }

        for (int i = 0; i < button.transitions.Length; i++) {
            UIButton.Transition transition = button.transitions[i];
            if (transition?.target == null || transition.target is not Image) {
                continue;
            }

            Transform targetTransform = transition.target.transform;
            if (targetTransform != button.transform && !targetTransform.IsChildOf(button.transform)) {
                transition.target = localImage;
            }
        }
    }

    private void RestoreLeftSubpageButtonPose(UIButton button, int index) {
        if (button == null) {
            return;
        }

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect != null) {
            if (index < nativeLeftSubpageButtonPoses.Count) {
                nativeLeftSubpageButtonPoses[index].ApplyTo(rect);
            } else {
                ApplyExtrapolatedPose(rect, index, nativeLeftSubpageButtonPoses, new Vector2(0f, -36f));
            }
        }

        LayoutElement staleLayout = button.GetComponent<LayoutElement>();
        if (staleLayout != null) {
            Destroy(staleLayout);
        }
    }

    private static void EnsureButtonRaycast(UIButton button) {
        if (button == null) {
            return;
        }

        if (button.button != null) {
            button.button.interactable = true;
            button.button.enabled = true;
        }

        Image image = button.GetComponent<Image>();
        if (image != null) {
            image.raycastTarget = true;
        }
    }

    private static void ApplyExtrapolatedPose(RectTransform rect, int index, List<ButtonPose> poses,
        Vector2 fallbackStep) {
        if (rect == null || poses.Count == 0) {
            return;
        }

        ButtonPose template = poses[0];
        template.ApplyTo(rect);

        Vector2 step = fallbackStep;
        if (poses.Count >= 2) {
            step = poses[1].AnchoredPosition - poses[0].AnchoredPosition;
        }

        rect.anchoredPosition = template.AnchoredPosition + step * index;
    }

    private static void HideNestedNavigationInContent(RectTransform pageRoot) {
        if (pageRoot == null) {
            return;
        }

        HideNestedNavigationInContentRecursive(pageRoot);
    }

    private static void HideNestedNavigationInContentRecursive(Transform node) {
        for (int i = 0; i < node.childCount; i++) {
            Transform child = node.GetChild(i);
            if (child == null) {
                continue;
            }

            // 隐藏所有可能的导航元素：Tab 按钮、分组标签、背景线等
            string name = child.name.ToLower();
            if (name.StartsWith("tab-btn-")
                || name.StartsWith("tabl-group-label")
                || name.Contains("tab-line")
                || name.Contains("navigation")
                || name.Contains("tab-group")) {
                child.gameObject.SetActive(false);
            }

            UIButton uiButton = child.GetComponent<UIButton>();
            if (uiButton != null && ShouldHideNestedNavigationButton(uiButton)) {
                child.gameObject.SetActive(false);
            }

            HideNestedNavigationInContentRecursive(child);
        }
    }

    private static bool ShouldHideNestedNavigationButton(UIButton button) {
        Transform textNode = button.transform.Find("button-text")
                             ?? button.transform.Find("Text")
                             ?? button.GetComponentInChildren<Text>(true)?.transform;
        if (textNode == null) {
            return false;
        }

        Text text = textNode.GetComponent<Text>();
        if (text == null || string.IsNullOrWhiteSpace(text.text)) {
            return false;
        }

        string normalized = text.text.Trim();
        foreach (MainWindowPageDefinition page in MainWindowPageRegistry.AllPages) {
            if (normalized == page.SubpageName.Translate()) {
                return true;
            }
        }

        foreach (string category in MainWindowPageRegistry.CategoryOrder) {
            if (normalized == category.Translate()) {
                return true;
            }
        }

        return false;
    }

    private void BindButtonClick(UIButton button, Action onClick) {
        if (button == null) {
            return;
        }

        if (uiButtonClickHandlers.TryGetValue(button, out Action<int> oldHandler)) {
            button.onClick -= oldHandler;
            uiButtonClickHandlers.Remove(button);
        }

        Action<int> newHandler = _ => onClick?.Invoke();
        button.onClick += newHandler;
        uiButtonClickHandlers[button] = newHandler;

        if (button.button != null) {
            button.button.onClick.RemoveAllListeners();
            if (onClick != null) {
                button.button.onClick.AddListener(() => onClick());
            }
        }
    }

    private static void SetButtonLabel(UIButton button, string label, int fontSize) {
        if (button == null) {
            return;
        }

        Transform buttonText = button.transform.Find("button-text")
                               ?? button.transform.Find("Text")
                               ?? button.GetComponentInChildren<Text>(true)?.transform;
        if (buttonText == null) {
            return;
        }

        var localizer = buttonText.GetComponent<Localizer>();
        if (localizer != null) {
            localizer.stringKey = label;
            localizer.translation = label.Translate();
        }

        var text = buttonText.GetComponent<Text>();
        if (text != null) {
            text.text = label.Translate();
            text.fontSize = fontSize;
        }
    }

    private static void SetButtonLabelKeepStyle(UIButton button, string label) {
        if (button == null) {
            return;
        }

        Transform buttonText = button.transform.Find("button-text")
                               ?? button.transform.Find("Text")
                               ?? button.GetComponentInChildren<Text>(true)?.transform;
        if (buttonText == null) {
            return;
        }

        string translated = label.Translate();
        Localizer localizer = buttonText.GetComponent<Localizer>();
        if (localizer != null) {
            localizer.stringKey = label;
            localizer.translation = translated;
        }

        Text text = buttonText.GetComponent<Text>();
        if (text != null) {
            text.text = translated;
        }
    }

    private static void SetButtonVisible(UIButton button, bool visible) {
        if (button != null) {
            button.gameObject.SetActive(visible);
        }
    }

    private static void HideButtons(List<UIButton> buttons) {
        for (int i = 0; i < buttons.Count; i++) {
            UIButton button = buttons[i];
            if (button != null) {
                button.gameObject.SetActive(false);
            }
        }
    }

    private void UpdateTopCategoryHighlights() {
        for (int i = 0; i < topCategoryButtons.Count; i++) {
            UIButton button = topCategoryButtons[i];
            if (button == null || !button.gameObject.activeSelf) {
                continue;
            }

            // 顶部按钮选中态对齐左侧按钮逻辑：选中使用 highlighted=true。
            // 这里仅迁移“颜色状态机”逻辑，不涉及顶部尺寸与位置。
            button.highlighted = i == selectedTopCategoryIndex;
        }
    }

    private void UpdateLeftSubpageHighlights() {
        for (int i = 0; i < leftSubpageButtons.Count; i++) {
            UIButton button = leftSubpageButtons[i];
            if (button == null || !button.gameObject.activeSelf) {
                continue;
            }

            button.highlighted = i == selectedSubpageIndex;
        }
    }

    private void RefreshSwitchMainPanelButtonLabel() {
        Transform buttonText = switchMainPanelButton?.transform.Find("button-text");
        if (buttonText == null) {
            return;
        }

        string label = MainWindow.GetSwitchMainPanelButtonLabel(FEMainPanelType.Analysis);
        var localizer = buttonText.GetComponent<Localizer>();
        if (localizer != null) {
            localizer.stringKey = label;
            localizer.translation = label.Translate();
        }

        var text = buttonText.GetComponent<Text>();
        if (text != null) {
            text.text = label.Translate();
        }
    }

    private static bool TryGetStatWindowSize(out Vector2 size) {
        size = default;
        UIGame uiGame = UIRoot.instance?.uiGame;
        if (uiGame?.statWindow == null) {
            return false;
        }

        // analysis 窗口尺寸对齐原生 P 面板，来源是 UIRoot.instance.uiGame.statWindow 的根 RectTransform。
        RectTransform statWindowRect = uiGame.statWindow.GetComponent<RectTransform>();
        if (statWindowRect == null) {
            return false;
        }

        size = statWindowRect.sizeDelta;
        return size.x > 0f && size.y > 0f;
    }

    private static RectTransform CreateContainerRect(string objectName, RectTransform parent, Vector2 offsetMin,
        Vector2 offsetMax, Vector2 anchorMin, Vector2 anchorMax) {
        var obj = new GameObject(objectName, typeof(RectTransform));
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }
}
