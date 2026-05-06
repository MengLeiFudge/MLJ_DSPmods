using System;
using System.Collections.Generic;
using System.Reflection;
using FE.UI.Foundation.Window;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FE.UI.MainPanel.Shell.Analysis;

/// <summary>
/// 分析面板风格 FE 主面板窗口壳。
/// </summary>
public partial class AnalysisMainPanelWindow : MyWindow {
    // Analysis 面板内容区标准：黑色底板固定为 1102x787。
    // 所有页面统一保留四边 10px gap，设计基准区为 1082x767，左上角基点为 (10,10)。
    private const float AnalysisBlackAreaWidth = 1102f;
    private const float AnalysisBlackAreaHeight = 787f;
    private const float AnalysisContentGap = 10f;
    private const float AnalysisDesignWidth = AnalysisBlackAreaWidth - AnalysisContentGap * 2f;
    private const float AnalysisDesignHeight = AnalysisBlackAreaHeight - AnalysisContentGap * 2f;

    /// <summary>
    /// 记录原生导航按钮的锚点、尺寸和位置。
    /// </summary>
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

    /// <summary>
    /// 把自定义拖拽事件转发给 Analysis 主窗口。
    /// </summary>
    private sealed class DragWindowForwarder : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler {
        private const float DragThresholdPixels = 5f;

        private RectTransform windowRect;
        private Canvas rootCanvas;
        private AnalysisMainPanelWindow owner;
        private bool pointerDown;
        private bool dragTriggered;

        public void Init(RectTransform window, AnalysisMainPanelWindow host) {
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
    private static readonly HashSet<string> hiddenNavigationLabels = new(StringComparer.Ordinal);
    private static int hiddenNavigationLabelLanguageIndex = -1;
    private int currentPageHiddenNavigationLanguageIndex = -1;
    private readonly Dictionary<int, int> lastSelectedSubpageIndexByTopCategory = [];
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

    public static AnalysisMainPanelWindow CreateInstance(string name, string title = "") {
        UIStatisticsWindow src = UIRoot.instance?.uiGame?.statWindow;
        if (src == null) {
            return MyWindowManager.CreateWindow<AnalysisMainPanelWindow>(name, title);
        }

        GameObject go = Instantiate(src.gameObject, src.transform.parent);
        go.name = name;
        go.SetActive(false);

        AnalysisMainPanelWindow win = go.AddComponent<AnalysisMainPanelWindow>();

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
            Debug.LogError($"Failed to register AnalysisMainPanelWindow to MyWindowManager: {e}");
        }

        if (MyWindowManager.Initialized) {
            win._Init(win.data);
        }
        return win;
    }

    public static void DestroyInstance(AnalysisMainPanelWindow win) {
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

    public override bool _OnInit() {
        if (!base._OnInit()) return false;
        if (windowTrans != null) {
            windowTrans.anchorMin = new Vector2(0.5f, 0.5f);
            windowTrans.anchorMax = new Vector2(0.5f, 0.5f);
            windowTrans.pivot = new Vector2(0.5f, 0.5f);
            windowTrans.anchoredPosition = new Vector2(0f, 0f);
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
            if (currentPageHiddenNavigationLanguageIndex != Localization.CurrentLanguageIndex) {
                HideNestedNavigationInContent(currentPageContent);
                currentPageHiddenNavigationLanguageIndex = Localization.CurrentLanguageIndex;
            }
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
}
