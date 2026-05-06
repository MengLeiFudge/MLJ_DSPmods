using System;
using System.Collections.Generic;
using FE.UI.View;
using UnityEngine;
using UnityEngine.UI;

namespace FE.UI.Components;

public partial class MyAnalysisWindow {
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

        // 顶部分类按钮是从原生统计面板的特殊模板克隆出来的，
        // 视觉状态会被重建，但默认不会补齐 UIButton 的音频配置。
        // 这里显式复制一份，让顶部分类切换与左侧分页保持相同的点击音效。
        UIButton audioTemplate = nativeTopCategoryHighlightStyleTemplateButton ?? templateButton;
        if (audioTemplate != null) {
            targetButton.audios = audioTemplate.audios;
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
        EnsureHiddenNavigationLabelCache();
        return hiddenNavigationLabels.Contains(normalized);
    }

    private static void EnsureHiddenNavigationLabelCache() {
        int languageIndex = Localization.CurrentLanguageIndex;
        if (hiddenNavigationLabelLanguageIndex == languageIndex && hiddenNavigationLabels.Count > 0) {
            return;
        }

        hiddenNavigationLabels.Clear();
        foreach (MainWindowPageDefinition page in MainWindowPageRegistry.AllPages) {
            hiddenNavigationLabels.Add(page.SubpageName.Translate());
        }
        foreach (string category in MainWindowPageRegistry.CategoryOrder) {
            hiddenNavigationLabels.Add(category.Translate());
        }
        hiddenNavigationLabelLanguageIndex = languageIndex;
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
}
