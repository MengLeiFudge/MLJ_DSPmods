using System;
using System.Collections.Generic;
using FE.UI.View;
using UnityEngine;
using UnityEngine.UI;

namespace FE.UI.Components;

public partial class MyAnalysisWindow {
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
        if (changed && selectedTopCategoryIndex >= 0 && selectedSubpageIndex >= 0) {
            lastSelectedSubpageIndexByTopCategory[selectedTopCategoryIndex] = selectedSubpageIndex;
        }
        selectedTopCategoryIndex = index;
        if (changed) {
            selectedSubpageIndex = lastSelectedSubpageIndexByTopCategory.TryGetValue(index, out int rememberedIndex)
                ? rememberedIndex
                : 0;
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
        if (selectedTopCategoryIndex < 0 || selectedTopCategoryIndex >= categories.Count) {
            currentPageDef = null;
            selectedSubpageIndex = -1;
            HideAllPageContent();
            return;
        }

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
                nativeHorizontalTab.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                    nativeHorizontalTabOriginalHeight);
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
        if (selectedTopCategoryIndex >= 0) {
            lastSelectedSubpageIndexByTopCategory[selectedTopCategoryIndex] = index;
        }

        var categories = MainWindow.AnalysisPageCategories;
        if (selectedTopCategoryIndex < 0 || selectedTopCategoryIndex >= categories.Count) return;

        var pages = categories[selectedTopCategoryIndex].Pages;
        if (selectedSubpageIndex < 0 || selectedSubpageIndex >= pages.Count) return;

        UpdateLeftSubpageHighlights();
        ShowPageContent(pages[selectedSubpageIndex]);
    }

    public bool TryGetCurrentPageRoute(out string categoryName, out string subpageName) {
        categoryName = null;
        subpageName = null;
        var categories = MainWindow.AnalysisPageCategories;
        if (selectedTopCategoryIndex < 0 || selectedTopCategoryIndex >= categories.Count) {
            return false;
        }

        IReadOnlyList<MainWindowPageDefinition> pages = categories[selectedTopCategoryIndex].Pages;
        if (selectedSubpageIndex < 0 || selectedSubpageIndex >= pages.Count) {
            return false;
        }

        categoryName = categories[selectedTopCategoryIndex].CategoryName;
        subpageName = pages[selectedSubpageIndex].SubpageName;
        return true;
    }

    public bool JumpToPage(string categoryName, string subpageName) {
        if (string.IsNullOrEmpty(categoryName) || string.IsNullOrEmpty(subpageName)) {
            return false;
        }

        var categories = MainWindow.AnalysisPageCategories;
        for (int i = 0; i < categories.Count; i++) {
            if (categories[i].CategoryName != categoryName) {
                continue;
            }

            OnTopCategoryClick(i);
            var pages = categories[i].Pages;
            for (int pageIndex = 0; pageIndex < pages.Count; pageIndex++) {
                if (pages[pageIndex].SubpageName != subpageName) {
                    continue;
                }

                OnSubpageClick(pageIndex);
                return true;
            }

            return false;
        }

        return false;
    }

    public void JumpToCategory(string categoryName, int internalTabIndex = 0) {
        var categories = MainWindow.AnalysisPageCategories;
        for (int i = 0; i < categories.Count; i++) {
            if (categories[i].CategoryName == categoryName) {
                OnTopCategoryClick(i);
                var pages = categories[i].Pages;
                if (internalTabIndex >= 0 && internalTabIndex < pages.Count) {
                    OnSubpageClick(internalTabIndex);
                }
                return;
            }
        }
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
            RectTransform designRoot = GetOrCreateDesignRoot(currentPageContent);
            pageDef.CreateUI(this, designRoot);

            HideNestedNavigationInContent(currentPageContent);
        } else {
            currentPageContent = existing as RectTransform;
        }

        if (currentPageContent != null) {
            HideNestedNavigationInContent(currentPageContent);
            currentPageHiddenNavigationLanguageIndex = Localization.CurrentLanguageIndex;
            currentPageContent.gameObject.SetActive(true);
        }
    }
}
