using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ProcessManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;
using Object = UnityEngine.Object;

namespace FE.UI.Patches;

/// <summary>
/// 模组分馏塔独立窗口。
/// 复制原版窗口实例，在创建时一次性修改布局，后续只需控制显示/隐藏。
/// 原版分馏塔直接使用原版窗口，不做任何修改。
/// </summary>
public static class FEFractionatorWindow {
    // ===== 窗口实例 =====
    private static GameObject modWindowGo;

    // ===== 布局常量 =====
    private const int MaxMainSlots = 4;
    private const int MaxSideSlots = 4;
    private const float SlotSpacing = 55f;
    // AddWidth = SlotSpacing * (Mathf.Max(MaxMainSlots, MaxSideSlots) - 1);
    private const float AddWidth = SlotSpacing * 3;
    private const float AddHeight = 70f;

    // ===== 颜色 =====
    private static readonly Color ProbColor = Orange;
    private static readonly Color DestroyColor = Red;

    // ===== 模组窗口组件引用 =====
    private static UIFractionatorWindow modWindow;
    private static UIFractionatorWindow sourceWindow;
    private static bool slotClickBound;
    private static RectTransform modRootRect;
    private static RectTransform[] resizableRects;
    private static Vector2[] resizableRectBaseSizes;
    private static Vector2 rootBaseSize;
    private static float currentAddWidth = -1f;
    private static readonly Dictionary<int, float> widthByFractionatorId = [];

    // ===== 中间区域UI组件 =====

    // 主产物
    private const float MainY = -80;
    private static Text _mainArrowText;
    private static Image[] _mainArrows;
    private static GameObject _mainArrowParent;
    private static readonly ProductSlot[] mainSlots = new ProductSlot[MaxMainSlots];
    // 副产物
    private const float SideY = -160;
    private static Text _sideArrowText;
    private static Image[] _sideArrows;
    private static GameObject _sideArrowParent;
    private static readonly ProductSlot[] sideSlots = new ProductSlot[MaxSideSlots];
    // 流动输出
    private const float FluidY = -240;
    private static Text _fluidArrowText;
    private static Image[] _fluidArrows;
    private static GameObject _fluidArrowParent;
    private static ProductSlot fluidSlot;
    private static Text fluidRightText;

    // 一次性记录的原版元素 localPosition
    private static Vector3 _itemBoxLocalPos;
    private static Vector3 _oriBoxLocalPos;
    private static Vector3 _productProbLocalPos;
    private static Vector3 _speedArrowParentLocalPos;
    private static float _areaHeight;
    private static float _layoutOffsetX;

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

    private class ProductSlot {
        public GameObject go;
        public Image icon;
        public UIButton button;
        public Text countText;
        public Text probText;
        public Image[] incArrows;
    }

    private static void BindSlotClickHandlers() {
        if (slotClickBound) {
            return;
        }
        foreach (var slot in mainSlots) {
            if (slot?.button != null) {
                slot.button.onClick += OnSlotClick;
            }
        }
        foreach (var slot in sideSlots) {
            if (slot?.button != null) {
                slot.button.onClick += OnSlotClick;
            }
        }
        if (fluidSlot?.button != null) {
            fluidSlot.button.onClick += OnSlotClick;
        }
        slotClickBound = true;
    }

    private static void UnbindSlotClickHandlers() {
        if (!slotClickBound) {
            return;
        }
        foreach (var slot in mainSlots) {
            if (slot?.button != null) {
                slot.button.onClick -= OnSlotClick;
            }
        }
        foreach (var slot in sideSlots) {
            if (slot?.button != null) {
                slot.button.onClick -= OnSlotClick;
            }
        }
        if (fluidSlot?.button != null) {
            fluidSlot.button.onClick -= OnSlotClick;
        }
        slotClickBound = false;
    }

    /// <summary>判断是否为模组分馏塔建筑</summary>
    public static bool IsModFractionator(int fractionatorId, PlanetFactory factory) {
        if (fractionatorId == 0 || factory == null) return false;
        FractionatorComponent frac = factory.factorySystem.fractionatorPool[fractionatorId];
        if (frac.id != fractionatorId) return false;
        int buildingId = factory.entityPool[frac.entityId].protoId;
        return buildingId >= IFE交互塔 && buildingId <= IFE回收塔;
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

        // 确保初始隐藏
        modWindowGo.SetActive(false);
    }

    private static void ApplyModLayoutOnce(UIFractionatorWindow window, UIFractionatorWindow vanillaWindow) {
        InitializeWindowResizeContext(window);
        ApplyWindowSizeKeepingTopLeft(AddWidth);

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
        
        // 流动输出右侧的提示文字，包括配方强化等级、成功损毁率
        if (window.oriProductProbText != null) {
            GameObject frGo = Object.Instantiate(window.oriProductProbText.gameObject, window.transform);
            frGo.name = "fluid-right-info";
            frGo.transform.localPosition = new Vector3(_itemBoxLocalPos.x + 80f + _layoutOffsetX, FluidY, _itemBoxLocalPos.z);
            fluidRightText = frGo.GetComponent<Text>();
            fluidRightText.alignment = TextAnchor.UpperLeft;
            fluidRightText.horizontalOverflow = HorizontalWrapMode.Overflow;
            fluidRightText.verticalOverflow = VerticalWrapMode.Overflow;
            fluidRightText.supportRichText = true;
            frGo.SetActive(false);
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

    private static void ApplyWindowSizeKeepingTopLeft(float addWidth) {
        if (modRootRect == null) {
            return;
        }

        if (Mathf.Abs(currentAddWidth - addWidth) < 0.01f) {
            return;
        }

        Vector3 oldTopLeft = GetTopLeftWorld(modRootRect);

        Vector2 targetRootSize = new(rootBaseSize.x + addWidth, rootBaseSize.y + AddHeight);
        modRootRect.sizeDelta = targetRootSize;

        if (resizableRects != null && resizableRectBaseSizes != null) {
            int count = Math.Min(resizableRects.Length, resizableRectBaseSizes.Length);
            for (int i = 0; i < count; i++) {
                RectTransform rect = resizableRects[i];
                if (rect == null) {
                    continue;
                }
                Vector2 baseSize = resizableRectBaseSizes[i];
                rect.sizeDelta = new Vector2(baseSize.x + addWidth, baseSize.y + AddHeight);
            }
        }

        Vector3 newTopLeft = GetTopLeftWorld(modRootRect);
        Vector3 delta = oldTopLeft - newTopLeft;
        modRootRect.position += delta;

        _layoutOffsetX = -addWidth * 0.5f;
        currentAddWidth = addWidth;

        RefreshLayoutX();
    }

    private static void RefreshLayoutX() {
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
                _speedArrowParentLocalPos.x + _layoutOffsetX, FluidY - 8, _speedArrowParentLocalPos.z);
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
                _itemBoxLocalPos.x + _layoutOffsetX, FluidY, _itemBoxLocalPos.z);
        }
        if (fluidRightText != null) {
            fluidRightText.transform.localPosition = new Vector3(
                _itemBoxLocalPos.x + 80f + _layoutOffsetX, FluidY, _itemBoxLocalPos.z);
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

        return slot;
    }

    // ===== _OnOpen Postfix：让原版正常执行，然后切换显示 =====
    // 关键原理：
    //   ManualBehaviour._Open() 先设 active=true，再调用 _OnOpen()。
    //   让原版 _OnOpen 执行，它会正确设置 factory/player 等所有字段。
    //   执行完后，在 Postfix 里隐藏 originalWindow（但保持 active=true），
    //   并设置 unsafeGameObjectState=true，让游戏的 _UpdateArray 继续驱动其 _Update，
    //   从而触发我们的 _OnUpdate Prefix，进而更新 modWindow。

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow._OnOpen))]
    public static void OnWindowOpen(UIFractionatorWindow __instance) {
        if (__instance == modWindow) return;// modWindow 自己不处理

        RectTransform sourceRect = __instance.GetComponent<RectTransform>();

        // factory 由原版 _OnOpen 设置
        PlanetFactory factory = __instance.factory;
        if (!IsModFractionator(__instance.fractionatorId, factory)) {
            if (modWindowGo != null && modWindowGo.activeSelf && sourceRect != null && modRootRect != null) {
                AlignTopLeft(sourceRect, modRootRect);
            }

            UnbindSlotClickHandlers();
            if (modWindowGo != null && modWindowGo.activeSelf) {
                modWindowGo.SetActive(false);
            }
            if (modWindow != null) modWindow.active = false;
            __instance.unsafeGameObjectState = false;
            if (!__instance.gameObject.activeSelf) {
                __instance.gameObject.SetActive(true);
            }
            sourceWindow = null;
            return;
        }

        // 模组建筑：
        if (sourceRect != null && modRootRect != null) {
            AlignTopLeft(modRootRect, sourceRect);
        }

        // 1. 隐藏 originalWindow 但保持 active=true，让游戏继续驱动其 _Update
        __instance.gameObject.SetActive(false);
        __instance.unsafeGameObjectState = true;
        sourceWindow = __instance;

        // 2. 显示 modWindow
        modWindowGo.SetActive(true);
        modWindow.active = true;

        // 3. 注册自定义槽位事件
        BindSlotClickHandlers();
    }

    private static void OnSlotClick(int itemId) {
        UIFractionatorWindow target = sourceWindow ?? modWindow;
        if (target == null) return;
        target.OnProductUIButtonClick(itemId);
    }

    private static void SetArrowGroup(Image[] arrows, bool enabled, Color color) {
        if (arrows == null) return;
        for (int i = 0; i < arrows.Length; i++) {
            Image arrow = arrows[i];
            if (arrow == null) continue;
            arrow.color = color;
            ((Behaviour)arrow).enabled = enabled;
        }
    }

    // ===== _OnClose Prefix：清理自定义状态，让原版正常清理字段 =====

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow._OnClose))]
    public static bool OnWindowClose(UIFractionatorWindow __instance) {
        // modWindow 不应通过 _Close() 关闭（只用 SetActive 管理），
        // 但如果发生了也要避免 player NPE
        if (__instance == modWindow) {
            UnbindSlotClickHandlers();
            if (modWindowGo != null && modWindowGo.activeSelf) {
                modWindowGo.SetActive(false);
            }
            modWindow.active = false;

            UIFractionatorWindow src = sourceWindow;
            sourceWindow = null;
            if (src != null && src.active) {
                src._Close();
            }
            return false;
        }

        // 原版窗口关闭时，同步清理 modWindow
        UnbindSlotClickHandlers();
        if (modWindowGo != null && modWindowGo.activeSelf) {
            modWindowGo.SetActive(false);
        }
        if (modWindow != null) modWindow.active = false;
        __instance.unsafeGameObjectState = false;
        if (sourceWindow == __instance) sourceWindow = null;

        return true;// 让原版 _OnClose 正常执行，清理 factory/player/button 等
    }

    // ===== _OnUpdate Prefix：拦截原版调用，驱动 modWindow 更新 =====
    // 关键原理：
    //   原版 originalWindow 的 active=true，游戏继续调用 originalWindow._Update()，
    //   触发 originalWindow._OnUpdate()，我们在 Prefix 里拦截，执行 modWindow 更新，
    //   return false 跳过原版的显示逻辑（避免原版代码重新显示 oriProductBox/productBox 等）。

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow._OnUpdate))]
    public static bool OnWindowUpdate(UIFractionatorWindow __instance) {
        if (__instance == modWindow) return false;// 防御：modWindow 不被游戏驱动

        if (modWindowGo == null) return true;

        RectTransform sourceRect = __instance.GetComponent<RectTransform>();

        bool isModBuilding = IsModFractionator(__instance.fractionatorId, __instance.factory);
        if (isModBuilding) {
            bool enteringModView = !modWindowGo.activeSelf;
            if (sourceRect != null && modRootRect != null) {
                if (enteringModView) {
                    AlignTopLeft(modRootRect, sourceRect);
                } else {
                    AlignTopLeft(sourceRect, modRootRect);
                }
            }
            sourceWindow = __instance;
            __instance.unsafeGameObjectState = true;
            if (__instance.gameObject.activeSelf) {
                __instance.gameObject.SetActive(false);
            }
            if (!modWindowGo.activeSelf) {
                modWindowGo.SetActive(true);
            }
            modWindow.active = true;
            BindSlotClickHandlers();
            DoModWindowUpdate(__instance);
            return false;
        }

        if (modWindowGo.activeSelf) {
            if (sourceRect != null && modRootRect != null) {
                AlignTopLeft(sourceRect, modRootRect);
            }
            modWindowGo.SetActive(false);
        }
        if (modWindow != null) modWindow.active = false;
        UnbindSlotClickHandlers();
        __instance.unsafeGameObjectState = false;
        if (__instance.active && !__instance.gameObject.activeSelf) {
            __instance.gameObject.SetActive(true);
        }
        sourceWindow = null;
        return true;
    }

    private static void DoModWindowUpdate(UIFractionatorWindow src) {
        if (src.fractionatorId == 0 || src.factory == null) {
            if (src.active) src._Close();
            return;
        }

        FractionatorComponent fractionator = src.factorySystem.fractionatorPool[src.fractionatorId];
        if (fractionator.id != src.fractionatorId) {
            if (src.active) src._Close();
            return;
        }

        bool hasFluid = fractionator.fluidId > 0;

        int buildingID = src.factory.entityPool[fractionator.entityId].protoId;
        ItemProto building = LDB.items.Select(buildingID);
        if (building == null) return;

        // 标题
        int level = building.Level();
        modWindow.titleText.text = level > 0 ? $"{building.name} +{level}" : building.name;

        // 电力
        PowerConsumerComponent powerConsumer = src.powerSystem.consumerPool[fractionator.pcId];
        int networkId = powerConsumer.networkId;
        PowerNetwork powerNetwork = src.powerSystem.netPool[networkId];
        float consumerRatio = powerNetwork != null && networkId > 0 ? (float)powerNetwork.consumerRatio : 0f;
        UpdatePowerDisplay(src, powerConsumer, consumerRatio);

        // 输入侧
        if (hasFluid) {
            ItemProto needProto = LDB.items.Select(fractionator.fluidId);
            if (needProto != null) {
                modWindow.needIcon.sprite = needProto.iconSprite;
                ((Behaviour)modWindow.needIcon).enabled = true;
            }
            modWindow.needCountText.text = fractionator.fluidInputCount.ToString();
            ((Behaviour)modWindow.needCountText).enabled = true;
            ((Behaviour)modWindow.inputTitleText).enabled = true;
            ((Behaviour)modWindow.speedText).enabled = true;
            int inputInc = fractionator.fluidInputCount > 0 && fractionator.fluidInputInc > 0
                ? fractionator.fluidInputInc / fractionator.fluidInputCount
                : 0;
            int inputArrowLevel = Cargo.fastIncArrowTable[Math.Min(inputInc, 10)];
            for (int i = 0; i < modWindow.needIncs.Length; i++)
                ((Behaviour)modWindow.needIncs[i]).enabled = (inputArrowLevel == i + 1);
        } else {
            ((Behaviour)modWindow.needIcon).enabled = false;
            for (int i = 0; i < modWindow.needIncs.Length; i++)
                ((Behaviour)modWindow.needIncs[i]).enabled = false;
            ((Behaviour)modWindow.needCountText).enabled = false;
            ((Behaviour)modWindow.inputTitleText).enabled = false;
            ((Behaviour)modWindow.speedText).enabled = false;
        }

        // 速率文字
        double fluidInputCountPerCargo = 1.0;
        if (fractionator.fluidInputCount == 0)
            fractionator.fluidInputCargoCount = 0f;
        else
            fluidInputCountPerCargo = fractionator.fluidInputCargoCount > 1e-4
                ? fractionator.fluidInputCount / (double)fractionator.fluidInputCargoCount
                : 4.0;
        double speed = consumerRatio
                       * (fractionator.fluidInputCargoCount < MaxBeltSpeed
                           ? fractionator.fluidInputCargoCount
                           : MaxBeltSpeed)
                       * fluidInputCountPerCargo
                       * 60.0;
        if (!fractionator.isWorking) speed = 0.0;
        modWindow.speedText.text = string.Format("次分馏每分".Translate(), Math.Round(speed));

        if (modWindow.productProbText != null) {
            ((Behaviour)modWindow.productProbText).enabled = false;
            modWindow.productProbText.gameObject.SetActive(false);
        }
        if (modWindow.oriProductProbText != null) {
            ((Behaviour)modWindow.oriProductProbText).enabled = false;
            modWindow.oriProductProbText.gameObject.SetActive(false);
        }

        if (modWindow.speedArrows != null) {
            for (int i = 0; i < modWindow.speedArrows.Length; i++) {
                if (modWindow.speedArrows[i] == null) continue;
                ((Behaviour)modWindow.speedArrows[i]).enabled = false;
            }
        }

        bool workingNow = hasFluid && fractionator.isWorking;
        byte outputFlags = fractionator.GetCurrentOutputFlags(src.factory);
        bool mainLit = workingNow && (outputFlags & OutputFlagMain) != 0;
        bool sideLit = workingNow && (outputFlags & OutputFlagSide) != 0;
        bool fluidLit = workingNow && ((outputFlags & OutputFlagFluid) != 0 || (!mainLit && !sideLit));

        SetArrowGroup(_mainArrows, hasFluid, mainLit ? modWindow.marqueeOnColor : modWindow.marqueeOffColor);
        SetArrowGroup(_sideArrows, hasFluid, sideLit ? modWindow.marqueeOnColor : modWindow.marqueeOffColor);
        SetArrowGroup(_fluidArrows, hasFluid, fluidLit ? modWindow.marqueeOnColor : modWindow.marqueeOffColor);

        if (modWindow.sepLine0 != null) ((Behaviour)modWindow.sepLine0).enabled = hasFluid;
        if (modWindow.sepLine1 != null) ((Behaviour)modWindow.sepLine1).enabled = hasFluid;
        if (modWindow.remindText != null) ((Behaviour)modWindow.remindText).enabled = !hasFluid;

        // 状态文字
        UpdateModStateText(src, fractionator, building, buildingID, consumerRatio);

        // 配方和产物区
        BaseRecipe recipe = GetRecipeForBuilding(buildingID, fractionator.fluidId);

        float successBoost = building.SuccessBoost();
        int avgInc = fractionator.fluidInputCount > 0 ? fractionator.fluidInputInc / fractionator.fluidInputCount : 0;
        float pointsBonus = (float)MaxTableMilli(avgInc);

        float recipeSuccessRatio = 0f, mainOutputBonus = 1f, destroyRatio = 0f;
        if (recipe != null && !recipe.Locked) {
            recipeSuccessRatio = recipe.SuccessRatio * (1 + successBoost) * (1 + pointsBonus);
            mainOutputBonus = 1 + recipe.DoubleOutputRatio;
            destroyRatio = recipe.DestroyRatio;
        }

        UpdateUIElements(src, fractionator, recipe, recipeSuccessRatio, mainOutputBonus, destroyRatio, hasFluid);
    }

    private static void UpdateModStateText(UIFractionatorWindow src,
        FractionatorComponent fractionator, ItemProto building, int buildingID, float consumerRatio) {

        int fluidOutputMax = building.FluidOutputMax();
        int productOutputMax = building.ProductOutputMax();
        List<ProductOutputInfo> products = fractionator.products(src.factory);

        if (fractionator.isWorking) {
            if (buildingID == IFE交互塔
                && fractionator.belt0 > 0
                && fractionator.belt1 <= 0
                && fractionator.belt2 <= 0) {
                modWindow.stateText.text = "交互模式".Translate();
                modWindow.stateText.color = modWindow.workNormalColor;
            } else if (fractionator.fluidInputCount > 0) {
                if (consumerRatio == 1f) {
                    modWindow.stateText.text = "正常运转".Translate();
                    modWindow.stateText.color = modWindow.workNormalColor;
                } else if (consumerRatio > 0.1f) {
                    modWindow.stateText.text = "电力不足".Translate();
                    modWindow.stateText.color = modWindow.powerLowColor;
                } else {
                    modWindow.stateText.text = "停止运转".Translate();
                    modWindow.stateText.color = modWindow.powerOffColor;
                }
            }
        } else {
            if (fractionator.fluidId == 0) {
                modWindow.stateText.text = "待机".Translate();
                modWindow.stateText.color = modWindow.idleColor;
            } else if (fractionator.fluidOutputCount >= fluidOutputMax) {
                modWindow.stateText.text = "原料堆积".Translate();
                modWindow.stateText.color = modWindow.workStoppedColor;
            } else if (products.Any(p => p.count >= productOutputMax)) {
                modWindow.stateText.text = building.EnableFluidEnhancement()
                    ? "分馏永动".Translate()
                    : "产物堆积".Translate();
                modWindow.stateText.color = modWindow.workStoppedColor;
            } else if (fractionator.fluidInputCount == 0) {
                modWindow.stateText.text = "缺少原材料".Translate();
                modWindow.stateText.color = modWindow.workStoppedColor;
            } else {
                modWindow.stateText.text = "搬运模式".Translate();
                modWindow.stateText.color = modWindow.workStoppedColor;
            }
        }
    }

    private static void UpdatePowerDisplay(UIFractionatorWindow src,
        PowerConsumerComponent powerConsumer, float consumerRatio) {
        if (modWindow.powerText == null || modWindow.powerIcon == null) {
            return;
        }

        src.powerServedSB ??= new StringBuilder("         W     %", 20);

        long powerPerMin = (long)((double)(powerConsumer.requiredEnergy * 60) * consumerRatio + 0.5);
        StringBuilderUtility.WriteKMG(src.powerServedSB, 8, powerPerMin);
        StringBuilderUtility.WriteUInt(src.powerServedSB, 12, 3, (uint)(consumerRatio * 100f));

        if (consumerRatio == 1f) {
            modWindow.powerText.text = src.powerServedSB.ToString();
            modWindow.powerIcon.color = modWindow.powerNormalIconColor;
            modWindow.powerText.color = modWindow.powerNormalColor;
        } else if (consumerRatio > 0.1f) {
            modWindow.powerText.text = src.powerServedSB.ToString();
            modWindow.powerIcon.color = modWindow.powerLowIconColor;
            modWindow.powerText.color = modWindow.powerLowColor;
        } else {
            modWindow.powerText.text = "未供电".Translate();
            modWindow.powerIcon.color = Color.clear;
            modWindow.powerText.color = modWindow.powerOffColor;
        }
    }

    private static BaseRecipe GetRecipeForBuilding(int buildingID, int fluidId) {
        return buildingID switch {
            IFE交互塔 => GetRecipe<BuildingTrainRecipe>(ERecipe.BuildingTrain, fluidId),
            IFE矿物复制塔 => GetRecipe<MineralCopyRecipe>(ERecipe.MineralCopy, fluidId),
            IFE点数聚集塔 => GetRecipe<PointAggregateRecipe>(ERecipe.PointAggregate, fluidId),
            IFE转化塔 => GetRecipe<ConversionRecipe>(ERecipe.Conversion, fluidId),
            IFE回收塔 => GetRecipe<RecycleRecipe>(ERecipe.Recycle, fluidId),
            _ => null
        };
    }

    private static void UpdateUIElements(UIFractionatorWindow src,
        FractionatorComponent fractionator, BaseRecipe recipe,
        float recipeSuccessRatio, float mainOutputBonus, float destroyRatio, bool hasFluid) {

        List<ProductOutputInfo> products = fractionator.products(src.factory);
        bool sandboxMode = GameMain.sandboxToolsEnabled;

        foreach (var slot in mainSlots)
            if (slot != null)
                slot.go.SetActive(false);
        foreach (var slot in sideSlots)
            if (slot != null)
                slot.go.SetActive(false);
        if (fluidSlot != null) fluidSlot.go.SetActive(false);

        int fractionatorId = src.fractionatorId;
        bool hasCachedWidth = widthByFractionatorId.TryGetValue(fractionatorId, out float cachedWidth);

        if (!hasFluid) {
            ApplyWindowSizeKeepingTopLeft(hasCachedWidth ? cachedWidth : 0f);
            if (_mainArrowText != null) _mainArrowText.gameObject.SetActive(false);
            if (_sideArrowText != null) _sideArrowText.gameObject.SetActive(false);
            if (_fluidArrowText != null) _fluidArrowText.gameObject.SetActive(false);
            if (fluidRightText != null) fluidRightText.gameObject.SetActive(false);
            if (modWindow.oriProductBox != null) modWindow.oriProductBox.SetActive(false);
            if (modWindow.oriProductIcon != null) ((Behaviour)modWindow.oriProductIcon).enabled = false;
            if (modWindow.oriProductCountText != null) ((Behaviour)modWindow.oriProductCountText).enabled = false;
            if (modWindow.oriProductIncs != null) {
                for (int i = 0; i < modWindow.oriProductIncs.Length; i++)
                    if (modWindow.oriProductIncs[i] != null)
                        ((Behaviour)modWindow.oriProductIncs[i]).enabled = false;
            }
            return;
        }

        int mainCount = 0, sideCount = 0;
        float mainSuccessSum = 0f;

        if (recipe != null && !recipe.Locked) {
            foreach (var output in recipe.OutputMain) {
                if (mainCount >= MaxMainSlots) break;
                var pInfo = products.Find(p => p.itemId == output.OutputID && p.isMainOutput);
                float ratio = recipeSuccessRatio * output.SuccessRatio;
                FillSlot(mainSlots[mainCount], output, pInfo?.count ?? 0,
                    ratio,
                    output.ShowSuccessRatio || sandboxMode);
                mainSuccessSum += ratio;
                mainCount++;
            }
            foreach (var output in recipe.OutputAppend) {
                if (sideCount >= MaxSideSlots) break;
                var pInfo = products.Find(p => p.itemId == output.OutputID && !p.isMainOutput);
                FillSlot(sideSlots[sideCount], output, pInfo?.count ?? 0,
                    recipeSuccessRatio * output.SuccessRatio,
                    output.ShowSuccessRatio || sandboxMode);
                sideCount++;
            }
        }

        int visibleSlotCount = Mathf.Max(mainCount, sideCount);
        float targetAddWidth = Mathf.Max(0, visibleSlotCount - 1) * SlotSpacing;
        if (visibleSlotCount > 0) {
            widthByFractionatorId[fractionatorId] = targetAddWidth;
        } else if (hasCachedWidth) {
            targetAddWidth = cachedWidth;
        }
        ApplyWindowSizeKeepingTopLeft(targetAddWidth);

        if (_mainArrowText != null) {
            _mainArrowText.gameObject.SetActive(mainCount > 0);
            _mainArrowText.text = "主产物".Translate();
            _mainArrowText.color = ProbColor;
        }
        if (_sideArrowText != null) {
            _sideArrowText.gameObject.SetActive(sideCount > 0);
            _sideArrowText.text = "副产物".Translate();
            _sideArrowText.color = ProbColor;
        }
        if (_fluidArrowText != null) {
            _fluidArrowText.gameObject.SetActive(true);
            _fluidArrowText.text = "流体输出".Translate();
            _fluidArrowText.color = ProbColor;
        }
        if (modWindow.oriProductBox != null) modWindow.oriProductBox.SetActive(false);

        // 流体输出右侧信息
        if (fluidRightText != null) {
            fluidRightText.gameObject.SetActive(true);
            int recipeLevel = recipe?.Level ?? 0;
            string flowStr = recipe != null && !recipe.Locked
                ? (1f - recipeSuccessRatio - destroyRatio).FormatP()
                : "---";
            string destroyStr = recipe != null && !recipe.Locked
                ? destroyRatio.FormatP()
                : "---";
            fluidRightText.text =
                $"{"配方强化".Translate()} {(recipeLevel > 0 ? $"+{recipeLevel}" : "0")}\n"
                + $"{flowStr}  <color=#{ColorUtility.ToHtmlStringRGBA(DestroyColor)}>{destroyStr}</color>";
        }

        if (fractionator.fluidId > 0) {
            float fluidRatio = Mathf.Clamp01(1f - mainSuccessSum);
            FillFluidSlot(fluidSlot, fractionator.fluidId, fractionator.fluidOutputCount, fluidRatio);
            int fluidInc = fractionator.fluidOutputCount > 0 && fractionator.fluidOutputInc > 0
                ? fractionator.fluidOutputInc / fractionator.fluidOutputCount
                : 0;
            int arrowLevel = Cargo.fastIncArrowTable[Math.Min(fluidInc, 10)];
            if (fluidSlot?.incArrows != null) {
                for (int i = 0; i < fluidSlot.incArrows.Length; i++)
                    if (fluidSlot.incArrows[i] != null)
                        ((Behaviour)fluidSlot.incArrows[i]).enabled = (arrowLevel >= i + 1);
            }
        }
    }

    private static void FillFluidSlot(ProductSlot slot, int itemId, int count, float ratio) {
        if (slot == null) return;
        slot.go.SetActive(true);
        if (slot.button != null) slot.button.data = itemId;
        ItemProto itemProto = LDB.items.Select(itemId);
        if (itemProto != null && slot.icon != null) slot.icon.sprite = itemProto.iconSprite;
        if (slot.countText != null) slot.countText.text = count.ToString();
        if (slot.probText != null) {
            slot.probText.text = ratio.FormatP();
            slot.probText.color = ProbColor;
        }
    }

    private static void FillSlot(ProductSlot slot, OutputInfo output, int count, float ratio, bool showRatio) {
        slot.go.SetActive(true);
        if (slot.button != null) slot.button.data = output.OutputID;
        ItemProto itemProto = LDB.items.Select(output.OutputID);
        if (itemProto != null && slot.icon != null) slot.icon.sprite = itemProto.iconSprite;
        if (slot.countText != null) slot.countText.text = count.ToString();
        if (slot.probText != null) {
            slot.probText.text = showRatio ? ratio.FormatP() : "???";
            slot.probText.color = ProbColor;
        }
    }

    // ===== OnProductUIButtonClick 拦截 =====

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow.OnProductUIButtonClick))]
    public static void OnProductUIButtonClick_Postfix(UIFractionatorWindow __instance, int itemId) {
        if (__instance.fractionatorId == 0 || __instance.factory == null) return;
        FractionatorComponent fractionator = __instance.factorySystem.fractionatorPool[__instance.fractionatorId];
        if (fractionator.id != __instance.fractionatorId) return;
        int buildingId = __instance.factory.entityPool[fractionator.entityId].protoId;
        if (buildingId < IFE交互塔 || buildingId > IFE回收塔) return;
        if (itemId == fractionator.productId || itemId == fractionator.fluidId) return;

        List<ProductOutputInfo> products = fractionator.products(__instance.factory);
        ProductOutputInfo target = products.Find(p => p.itemId == itemId);
        if (target == null || target.count == 0) return;

        Player player = __instance.player;
        if (player.inhandItemId == 0 && player.inhandItemCount == 0) {
            if (VFInput.control || VFInput.shift) {
                int added = player.TryAddItemToPackage(itemId, target.count, 0, throwTrash: false);
                if (added > 0) UIItemup.Up(itemId, added);
            } else {
                player.SetHandItemId_Unsafe(itemId);
                player.SetHandItemCount_Unsafe(target.count);
            }
            target.count = 0;
        } else if (player.inhandItemId == itemId && player.inhandItemCount > 0) {
            ItemProto building = LDB.items.Select(buildingId);
            int canAdd = building.ProductOutputMax() - target.count;
            if (canAdd <= 0) {
                UIRealtimeTip.Popup("栏位已满".Translate());
                return;
            }
            int add = Math.Min(player.inhandItemCount, canAdd);
            target.count += add;
            player.AddHandItemCount_Unsafe(-add);
            if (player.inhandItemCount <= 0) {
                player.SetHandItemId_Unsafe(0);
                player.SetHandItemCount_Unsafe(0);
            }
        }
    }
}
