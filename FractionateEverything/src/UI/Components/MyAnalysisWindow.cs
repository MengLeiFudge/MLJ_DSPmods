using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FE.UI.Components;

public class MyAnalysisWindow : MyWindow {
    private const string CoreOperateCategoryName = "核心操作";
    private const string ItemManageCategoryName = "物品管理";
    private const string ResourceAcquireCategoryName = "资源获取";
    private const string ProgressSystemCategoryName = "进度系统";
    private const string StatisticCategoryName = "统计相关";
    private const string SystemSettingCategoryName = "系统设置";

    private static readonly string[] TopCategoryNames = [
        CoreOperateCategoryName,
        ItemManageCategoryName,
        ResourceAcquireCategoryName,
        ProgressSystemCategoryName,
        StatisticCategoryName,
        SystemSettingCategoryName,
    ];

    private const float TopCategoryHeight = 52f;
    private const float LeftSubpageWidth = 176f;
    private const float ContainerPadding = 24f;
    private const float ContainerGap = 16f;

    private RectTransform windowTrans;
    private RectTransform topCategoryContainer;
    private RectTransform leftSubpageContainer;
    private RectTransform contentRootContainer;
    private readonly List<UIButton> topCategoryButtons = [];
    private Vector2 analysisWindowSize;

    public static MyAnalysisWindow CreateInstance(string name, string title = "") {
        return MyWindowManager.CreateWindow<MyAnalysisWindow>(name, title);
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
        base._OnCreate();
        windowTrans = GetComponent<RectTransform>();
        ApplyStatWindowSize();
        CreateContainerSkeleton();
        gameObject.SetActive(false);
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
    }

    public override void _OnUpdate() {
        base._OnUpdate();
    }

    public override void _OnFree() {
        base._OnFree();
    }

    public override void _OnDestroy() {
        topCategoryButtons.Clear();
        topCategoryContainer = null;
        leftSubpageContainer = null;
        contentRootContainer = null;
        windowTrans = null;
        analysisWindowSize = default;
        base._OnDestroy();
    }

    private void CreateContainerSkeleton() {
        var panelTransform = transform.Find("panel-bg") as RectTransform;
        if (panelTransform == null) return;

        topCategoryContainer = CreateContainerRect(
            "analysis-top-categories",
            panelTransform,
            new(ContainerPadding, -(TitleHeight + ContainerPadding)),
            new(-ContainerPadding, -(TitleHeight + ContainerPadding + TopCategoryHeight)),
            new(0f, 1f),
            new(1f, 1f));
        CreateTopCategoryButtons();

        float bodyTop = TitleHeight + ContainerPadding + TopCategoryHeight + ContainerGap;
        leftSubpageContainer = CreateContainerRect(
            "analysis-left-subpages",
            panelTransform,
            new(ContainerPadding, -bodyTop),
            new(ContainerPadding + LeftSubpageWidth, -ContainerPadding),
            new(0f, 0f),
            new(0f, 1f));

        contentRootContainer = CreateContainerRect(
            "analysis-content-root",
            panelTransform,
            new(ContainerPadding + LeftSubpageWidth + ContainerGap, -bodyTop),
            new(-ContainerPadding, -ContainerPadding),
            new(0f, 0f),
            new(1f, 1f));
    }

    private void CreateTopCategoryButtons() {
        if (topCategoryContainer == null) return;

        topCategoryButtons.Clear();
        float availableWidth = analysisWindowSize.x - ContainerPadding * 2f;
        float buttonWidth = (availableWidth - ContainerGap * (TopCategoryNames.Length - 1)) / TopCategoryNames.Length;

        for (int i = 0; i < TopCategoryNames.Length; i++) {
            UIButton button = AddButton(
                i * (buttonWidth + ContainerGap),
                0f,
                buttonWidth,
                topCategoryContainer,
                TopCategoryNames[i],
                16,
                $"analysis-top-category-{i}");
            topCategoryButtons.Add(button);
        }
    }

    private void ApplyStatWindowSize() {
        if (windowTrans == null) return;

        analysisWindowSize = TryGetStatWindowSize(out Vector2 statWindowSize)
            ? statWindowSize
            : new(WindowWidth, WindowHeight);
        windowTrans.sizeDelta = analysisWindowSize;
    }

    private void RefreshTopCategoryButtonLayout() {
        if (topCategoryButtons.Count == 0) return;

        float availableWidth = analysisWindowSize.x - ContainerPadding * 2f;
        float buttonWidth = (availableWidth - ContainerGap * (TopCategoryNames.Length - 1)) / TopCategoryNames.Length;

        for (int i = 0; i < topCategoryButtons.Count; i++) {
            if (topCategoryButtons[i] == null) continue;

            RectTransform buttonRect = topCategoryButtons[i].transform as RectTransform;
            if (buttonRect == null) continue;

            buttonRect.anchoredPosition = new(i * (buttonWidth + ContainerGap), 0f);
            buttonRect.sizeDelta = new(buttonWidth, buttonRect.sizeDelta.y);
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
        var obj = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        var image = obj.GetComponent<Image>();
        image.color = new(1f, 1f, 1f, 0.04f);
        image.raycastTarget = false;
        return rect;
    }
}
