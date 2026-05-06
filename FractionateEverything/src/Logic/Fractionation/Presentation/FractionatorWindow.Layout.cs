using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;
using Object = UnityEngine.Object;

namespace FE.Logic.Fractionation.Presentation;

public static partial class FractionatorWindow {
    private static string GetRelativePath(Transform root, Transform target) {
        if (root == null || target == null) return null;
        if (target == root) return string.Empty;
        var stack = new Stack<string>();
        Transform current = target;
        while (current != null && current != root) {
            stack.Push(current.name);
            current = current.parent;
        }
        if (current != root) return null;
        return string.Join("/", stack.ToArray());
    }

    private static Image[] CloneArrowImagesOrdered(GameObject vanillaParent, GameObject cloneParent,
        UIFractionatorWindow vanillaWindow) {
        Image[] result = new Image[vanillaWindow.speedArrows.Length];
        for (int i = 0; i < vanillaWindow.speedArrows.Length; i++) {
            Image src = vanillaWindow.speedArrows[i];
            if (src == null) continue;
            string path = GetRelativePath(vanillaParent.transform, src.transform);
            if (path == null) continue;
            Transform t = path.Length == 0 ? cloneParent.transform : cloneParent.transform.Find(path);
            if (t != null) result[i] = t.GetComponent<Image>();
        }
        return result;
    }

    private static Text CloneArrowText(GameObject vanillaParent, GameObject cloneParent,
        UIFractionatorWindow vanillaWindow) {
        if (vanillaWindow.productProbText != null) {
            string path = GetRelativePath(vanillaParent.transform, vanillaWindow.productProbText.transform);
            if (path != null) {
                Transform t = path.Length == 0 ? cloneParent.transform : cloneParent.transform.Find(path);
                if (t != null) {
                    Text mapped = t.GetComponent<Text>();
                    if (mapped != null) return mapped;
                }
            }
        }
        Text[] texts = cloneParent.GetComponentsInChildren<Text>(true);
        return texts.FirstOrDefault();
    }

    private static T FindClonedComponent<T>(Transform cloneRoot, Component original, Transform originalRoot)
        where T : Component {
        if (original == null) return null;
        string path = GetRelativePath(originalRoot, original.transform);
        if (path == null) return null;
        Transform found = path.Length == 0 ? cloneRoot : cloneRoot.Find(path);
        return found != null ? found.GetComponent<T>() : null;
    }

    // ===== 初始化：复制窗口并一次性修改布局 =====

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow._OnInit))]
    public static void CreateModWindowInstance(UIFractionatorWindow __instance) {
        if (modWindowGo != null) return;

        // 复制前记录原版 localPosition
        _itemBoxLocalPos = __instance.productBox.transform.localPosition;
        _oriBoxLocalPos = __instance.oriProductBox.transform.localPosition;
        _speedArrowParentLocalPos = __instance.speedArrows[0].transform.parent.localPosition;

        // 复制窗口
        modWindowGo = Object.Instantiate(__instance.gameObject, __instance.transform.parent);
        modWindowGo.name = "FE-FractionatorWindow";
        modWindow = modWindowGo.GetComponent<UIFractionatorWindow>();

        // 一次性修改布局
        ApplyModLayoutOnce(modWindow, __instance);

        // 缓存共享组件引用（通过路径查找，比 Instantiate 自动映射更可靠)
        CacheSharedComponents(__instance);

        // 确保初始隐藏
        modWindowGo.SetActive(false);
    }

    private static void CacheSharedComponents(UIFractionatorWindow vanillaWindow) {
        _modPowerText =
            FindClonedComponent<Text>(modWindowGo.transform, vanillaWindow.powerText, vanillaWindow.transform);
        _modPowerIcon =
            FindClonedComponent<Image>(modWindowGo.transform, vanillaWindow.powerIcon, vanillaWindow.transform);
    }

    private static void ApplyModLayoutOnce(UIFractionatorWindow window, UIFractionatorWindow vanillaWindow) {
        InitializeWindowResizeContext(window);
        ApplyWindowSizeKeepingTopLeft(AddWidth, AddHeight);

        // 隐藏原版中间区域的全部内容，新的中间区域全使用复制的UI
        // 隐藏概率文字
        window.productProbText.gameObject.SetActive(false);
        window.oriProductProbText.gameObject.SetActive(false);
        // 隐藏箭头
        foreach (Image image in window.speedArrows) {
            if (image == null) continue;
            image.enabled = false;
            image.gameObject.SetActive(false);
        }
        // 隐藏产物box、流动输出box
        window.productBox.SetActive(false);
        window.oriProductBox.SetActive(false);
        // 隐藏分割线
        window.sepLine0.gameObject.SetActive(false);
        window.sepLine1.gameObject.SetActive(false);
        // 隐藏右下区域的分馏数目统计
        window.historyText.gameObject.SetActive(false);

        // 复制箭头、箭头上方文字，1、2、3区域各一份
        GameObject vanillaArrowParent = vanillaWindow.speedArrows[0].transform.parent.gameObject;

        _mainArrowParent = Object.Instantiate(vanillaArrowParent, window.transform);
        _mainArrowParent.name = "produce-main";
        _mainArrowParent.transform.localPosition = new Vector3(
            _speedArrowParentLocalPos.x + _layoutOffsetX, MainY - 8, _speedArrowParentLocalPos.z);
        _mainArrows = CloneArrowImagesOrdered(vanillaArrowParent, _mainArrowParent, vanillaWindow);
        _mainArrowText = CloneArrowText(vanillaArrowParent, _mainArrowParent, vanillaWindow);

        _sideArrowParent = Object.Instantiate(vanillaArrowParent, window.transform);
        _sideArrowParent.name = "produce-side";
        _sideArrowParent.transform.localPosition = new Vector3(
            _speedArrowParentLocalPos.x + _layoutOffsetX, SideY - 8, _speedArrowParentLocalPos.z);
        _sideArrows = CloneArrowImagesOrdered(vanillaArrowParent, _sideArrowParent, vanillaWindow);
        _sideArrowText = CloneArrowText(vanillaArrowParent, _sideArrowParent, vanillaWindow);

        _fluidArrowParent = Object.Instantiate(vanillaArrowParent, window.transform);
        _fluidArrowParent.name = "produce-fluid";
        _fluidArrowParent.transform.localPosition = new Vector3(
            _speedArrowParentLocalPos.x + _layoutOffsetX, FluidY - 8, _speedArrowParentLocalPos.z);
        _fluidArrows = CloneArrowImagesOrdered(vanillaArrowParent, _fluidArrowParent, vanillaWindow);
        _fluidArrowText = CloneArrowText(vanillaArrowParent, _fluidArrowParent, vanillaWindow);

        // 复制主产物槽、副产物槽、流动输出槽
        for (int i = 0; i < MaxMainSlots; i++) {
            Vector3 pos = new Vector3(_itemBoxLocalPos.x + _layoutOffsetX + i * SlotSpacing, MainY, _itemBoxLocalPos.z);
            mainSlots[i] = CreateSlot(window, vanillaWindow, pos);
        }
        for (int i = 0; i < MaxSideSlots; i++) {
            Vector3 pos = new Vector3(_itemBoxLocalPos.x + _layoutOffsetX + i * SlotSpacing, SideY, _oriBoxLocalPos.z);
            sideSlots[i] = CreateSlot(window, vanillaWindow, pos);
        }
        Vector3 fluidPos = new Vector3(_itemBoxLocalPos.x + _layoutOffsetX, FluidY, _itemBoxLocalPos.z);
        fluidSlot = CreateSlot(window, vanillaWindow, fluidPos);

        // 不要分割线了
        // // 复制分割线，主产物-副产物之间、副产物-流动输出之间
        // GameObject newSep12 = Object.Instantiate(window.sepLine1.gameObject, window.transform);
        // newSep12.name = "sep-line-12-extra";
        // newSep12.transform.localPosition = new Vector3(
        //     window.sepLine1.transform.localPosition.x,
        //     (MainY + SideY) / 2,
        //     window.sepLine1.transform.localPosition.z);
        // GameObject newSep23 = Object.Instantiate(window.sepLine1.gameObject, window.transform);
        // newSep23.name = "sep-line-23-extra";
        // newSep23.transform.localPosition = new Vector3(
        //     window.sepLine1.transform.localPosition.x,
        //     (SideY + FluidY) / 2,
        //     window.sepLine1.transform.localPosition.z);

        // 流动输出右侧的提示文字，包括配方强化等级、损毁率
        if (window.oriProductProbText != null) {
            GameObject frGo = Object.Instantiate(window.oriProductProbText.gameObject, window.transform);
            frGo.name = "fluid-right-info";
            frGo.transform.localPosition =
                new Vector3(_itemBoxLocalPos.x + 80f + _layoutOffsetX, FluidY, _itemBoxLocalPos.z);
            fluidRightText = frGo.GetComponent<Text>();
            fluidRightText.alignment = TextAnchor.UpperLeft;
            fluidRightText.horizontalOverflow = HorizontalWrapMode.Overflow;
            fluidRightText.verticalOverflow = VerticalWrapMode.Overflow;
            fluidRightText.supportRichText = true;
            fluidRightText.fontSize = 14;
            frGo.SetActive(false);

            lockStateText = CreateLabel(window, fluidRightText, "单锁".Translate(),
                new Vector3(_itemBoxLocalPos.x + 80f + _layoutOffsetX, FluidY - 38f, _itemBoxLocalPos.z));
            if (lockStateText != null) {
                lockStateText.fontSize = 14;
                lockStateText.alignment = TextAnchor.UpperLeft;
                lockStateText.supportRichText = true;
            }

            lockHintText = CreateLabel(window, fluidRightText, "右键设为单锁".Translate(),
                new Vector3(_itemBoxLocalPos.x + 80f + _layoutOffsetX, FluidY - 56f, _itemBoxLocalPos.z));
            if (lockHintText != null) {
                lockHintText.fontSize = 12;
                lockHintText.alignment = TextAnchor.UpperLeft;
                lockHintText.color = ProbColor;
            }
        }
    }

    private static void InitializeWindowResizeContext(UIFractionatorWindow window) {
        modRootRect = window.GetComponent<RectTransform>();
        if (modRootRect == null) {
            return;
        }

        rootBaseSize = modRootRect.sizeDelta;
        List<RectTransform> resizable = [];
        List<Vector2> baseSizes = [];

        RectTransform[] rects = window.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++) {
            RectTransform rect = rects[i];
            if (rect == null || rect == modRootRect) {
                continue;
            }

            Vector2 size = rect.sizeDelta;
            if (Mathf.Abs(size.x - rootBaseSize.x) < 0.01f && Mathf.Abs(size.y - rootBaseSize.y) < 0.01f) {
                resizable.Add(rect);
                baseSizes.Add(size);
            }
        }

        resizableRects = resizable.ToArray();
        resizableRectBaseSizes = baseSizes.ToArray();
    }

    private static Vector3 GetTopLeftWorld(RectTransform rect) {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return corners[1];
    }

    private static void AlignTopLeft(RectTransform target, RectTransform reference) {
        if (target == null || reference == null) {
            return;
        }

        Vector3 refTopLeft = GetTopLeftWorld(reference);
        Vector3 targetTopLeft = GetTopLeftWorld(target);
        Vector3 delta = refTopLeft - targetTopLeft;
        target.position += delta;
    }

    private static float currentAddHeight = -1f;

    private static void ApplyWindowSizeKeepingTopLeft(float addWidth, float addHeight) {
        if (modRootRect == null) {
            return;
        }

        if (Mathf.Abs(currentAddWidth - addWidth) < 0.01f && Mathf.Abs(currentAddHeight - addHeight) < 0.01f) {
            return;
        }

        Vector3 oldTopLeft = GetTopLeftWorld(modRootRect);

        Vector2 targetRootSize = new(rootBaseSize.x + addWidth, rootBaseSize.y + addHeight);
        modRootRect.sizeDelta = targetRootSize;

        if (resizableRects != null && resizableRectBaseSizes != null) {
            int count = Math.Min(resizableRects.Length, resizableRectBaseSizes.Length);
            for (int i = 0; i < count; i++) {
                RectTransform rect = resizableRects[i];
                if (rect == null) {
                    continue;
                }
                Vector2 baseSize = resizableRectBaseSizes[i];
                rect.sizeDelta = new Vector2(baseSize.x + addWidth, baseSize.y + addHeight);
            }
        }

        Vector3 newTopLeft = GetTopLeftWorld(modRootRect);
        Vector3 delta = oldTopLeft - newTopLeft;
        modRootRect.position += delta;

        _layoutOffsetX = -addWidth * 0.5f;
        currentAddWidth = addWidth;
        currentAddHeight = addHeight;
    }

    private static void RefreshLayoutX() {
        RefreshLayoutX(FluidY);
    }

    private static void RefreshLayoutX(float fluidY) {
        if (_mainArrowParent != null) {
            _mainArrowParent.transform.localPosition = new Vector3(
                _speedArrowParentLocalPos.x + _layoutOffsetX, MainY - 8, _speedArrowParentLocalPos.z);
        }
        if (_sideArrowParent != null) {
            _sideArrowParent.transform.localPosition = new Vector3(
                _speedArrowParentLocalPos.x + _layoutOffsetX, SideY - 8, _speedArrowParentLocalPos.z);
        }
        if (_fluidArrowParent != null) {
            _fluidArrowParent.transform.localPosition = new Vector3(
                _speedArrowParentLocalPos.x + _layoutOffsetX, fluidY - 8, _speedArrowParentLocalPos.z);
        }

        for (int i = 0; i < MaxMainSlots; i++) {
            if (mainSlots[i]?.go != null) {
                mainSlots[i].go.transform.localPosition = new Vector3(
                    _itemBoxLocalPos.x + _layoutOffsetX + i * SlotSpacing, MainY, _itemBoxLocalPos.z);
            }
        }
        for (int i = 0; i < MaxSideSlots; i++) {
            if (sideSlots[i]?.go != null) {
                sideSlots[i].go.transform.localPosition = new Vector3(
                    _itemBoxLocalPos.x + _layoutOffsetX + i * SlotSpacing, SideY, _itemBoxLocalPos.z);
            }
        }
        if (fluidSlot?.go != null) {
            fluidSlot.go.transform.localPosition = new Vector3(
                _itemBoxLocalPos.x + _layoutOffsetX, fluidY, _itemBoxLocalPos.z);
        }
        if (fluidRightText != null) {
            fluidRightText.transform.localPosition = new Vector3(
                _itemBoxLocalPos.x + 80f + _layoutOffsetX, fluidY, _itemBoxLocalPos.z);
        }
        if (lockStateText != null) {
            lockStateText.transform.localPosition = new Vector3(
                _itemBoxLocalPos.x + 80f + _layoutOffsetX, fluidY - 38f, _itemBoxLocalPos.z);
        }
        if (lockHintText != null) {
            lockHintText.transform.localPosition = new Vector3(
                _itemBoxLocalPos.x + 80f + _layoutOffsetX, fluidY - 56f, _itemBoxLocalPos.z);
        }
    }

    private static Text CreateLabel(UIFractionatorWindow window, Text reference, string text, Vector3 localPos) {
        GameObject go = Object.Instantiate(reference.gameObject, window.transform);
        go.name = "section-label-" + text;
        go.transform.localPosition = localPos;
        Text label = go.GetComponent<Text>();
        label.text = text;
        label.fontSize = reference.fontSize;
        label.alignment = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.gameObject.SetActive(false);
        return label;
    }

    private static ProductSlot CreateSlot(UIFractionatorWindow window, UIFractionatorWindow vanillaWindow,
        Vector3 localPos) {
        GameObject go = Object.Instantiate(vanillaWindow.oriProductBox, window.transform);
        go.SetActive(false);
        go.transform.localPosition = localPos;

        Image[] origImages = vanillaWindow.oriProductBox.GetComponentsInChildren<Image>(true);
        Image[] cloneImages = go.GetComponentsInChildren<Image>(true);

        int iconIdx = Array.IndexOf(origImages, vanillaWindow.oriProductIcon);
        Image cloneIcon = (iconIdx >= 0 && iconIdx < cloneImages.Length)
            ? cloneImages[iconIdx]
            : go.GetComponentInChildren<Image>(true);

        UIButton cloneButton = cloneIcon != null
            ? cloneIcon.GetComponent<UIButton>() ?? go.GetComponentInChildren<UIButton>(true)
            : go.GetComponentInChildren<UIButton>(true);

        Text[] origTexts = vanillaWindow.oriProductBox.GetComponentsInChildren<Text>(true);
        Text[] cloneTexts = go.GetComponentsInChildren<Text>(true);
        int countIdx = Array.IndexOf(origTexts, vanillaWindow.oriProductCountText);
        Text cloneCountText = (countIdx >= 0 && countIdx < cloneTexts.Length)
            ? cloneTexts[countIdx]
            : go.GetComponentInChildren<Text>(true);

        ProductSlot slot = new ProductSlot {
            go = go, icon = cloneIcon, button = cloneButton,
            countText = cloneCountText, incArrows = new Image[3]
        };

        for (int i = 0; i < vanillaWindow.oriProductIncs.Length && i < 3; i++) {
            int incIdx = Array.IndexOf(origImages, vanillaWindow.oriProductIncs[i]);
            if (incIdx >= 0 && incIdx < cloneImages.Length)
                slot.incArrows[i] = cloneImages[incIdx];
        }

        // 概率文字（叠加在数量文字下方）
        if (cloneCountText != null) {
            GameObject probGo = Object.Instantiate(cloneCountText.gameObject, go.transform);
            probGo.name = "prob-text";
            slot.probText = probGo.GetComponent<Text>();
            slot.probText.alignment = TextAnchor.MiddleCenter;
            slot.probText.color = ProbColor;
            slot.probText.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform probRect = slot.probText.GetComponent<RectTransform>();
            RectTransform countRect = cloneCountText.GetComponent<RectTransform>();
            if (probRect != null && countRect != null)
                probRect.anchoredPosition = countRect.anchoredPosition + new Vector2(0, -16f);
        }

        if (cloneIcon != null) {
            GameObject lockGo = new("lock-icon");
            lockGo.transform.SetParent(cloneIcon.transform, false);
            slot.lockIcon = lockGo.AddComponent<Image>();
            slot.lockIcon.raycastTarget = false;
            ApplyLockIconStyle(slot.lockIcon);
            RectTransform lockRect = slot.lockIcon.rectTransform;
            lockRect.anchorMin = new Vector2(1f, 1f);
            lockRect.anchorMax = new Vector2(1f, 1f);
            lockRect.pivot = new Vector2(1f, 1f);
            lockRect.anchoredPosition = new Vector2(8f, 6f);
            lockRect.sizeDelta = new Vector2(14f, 14f);
            lockGo.SetActive(false);
        }

        return slot;
    }
}
