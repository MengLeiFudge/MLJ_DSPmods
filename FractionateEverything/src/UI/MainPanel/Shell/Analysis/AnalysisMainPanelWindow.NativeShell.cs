using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FE.UI.MainPanel.Shell.Analysis;

public partial class AnalysisMainPanelWindow {
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
}
