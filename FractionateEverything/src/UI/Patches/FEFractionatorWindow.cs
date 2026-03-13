using System;
using System.Collections.Generic;
using System.Linq;
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
    private const int ExtraSlotCountForWidth = 3;
    private const float HeightScale = 4f / 3f;
    private const float MainSideExtraYOffset = -30f;
    private const float LabelArrowNudgeX = 16f;
    private const float FluidLabelRightTextYOffsetFactor = 2.5f;

    // ===== 颜色 =====
    private static readonly Color ProbColor = Gold;
    private static readonly Color DestroyColor = Red;

    // ===== 模组窗口组件引用 =====
    private static UIFractionatorWindow modWindow;
    private static UIFractionatorWindow sourceWindow;
    private static bool slotClickBound;

    // 自定义UI元素
    private static readonly ProductSlot[] mainSlots = new ProductSlot[MaxMainSlots];
    private static readonly ProductSlot[] sideSlots = new ProductSlot[MaxSideSlots];
    private static ProductSlot fluidSlot;
    private static Text mainSectionLabel;
    private static Text appendSectionLabel;
    private static Text fluidSectionLabel;
    private static Text fluidRightText;
    private static Text _mainArrowText;
    private static Text _sideArrowText;
    private static Text _fluidArrowText;
    private static Image[] _mainArrows;
    // 克隆的额外箭头组（Area2 副产物、Area3 流体输出）
    private static Image[] _sideArrows;
    private static Image[] _fluidArrows;

    // 一次性记录的原版元素 localPosition
    private static Vector3 _productBoxLocalPos;
    private static Vector3 _oriBoxLocalPos;
    private static Vector3 _productProbLocalPos;
    private static Vector3 _speedArrowParentLocalPos;
    private static float   _areaHeight;
    private static float   _layoutOffsetX;
    private static float   _layoutOffsetY;

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
        if (slotClickBound) return;
        foreach (var slot in mainSlots)
            if (slot?.button != null) slot.button.onClick += OnSlotClick;
        foreach (var slot in sideSlots)
            if (slot?.button != null) slot.button.onClick += OnSlotClick;
        if (fluidSlot?.button != null) fluidSlot.button.onClick += OnSlotClick;
        slotClickBound = true;
    }

    private static void UnbindSlotClickHandlers() {
        if (!slotClickBound) return;
        foreach (var slot in mainSlots)
            if (slot?.button != null) slot.button.onClick -= OnSlotClick;
        foreach (var slot in sideSlots)
            if (slot?.button != null) slot.button.onClick -= OnSlotClick;
        if (fluidSlot?.button != null) fluidSlot.button.onClick -= OnSlotClick;
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
        _productBoxLocalPos = __instance.productBox.transform.localPosition;
        _oriBoxLocalPos = __instance.oriProductBox.transform.localPosition;
        _productProbLocalPos = __instance.productProbText.transform.localPosition;
        _speedArrowParentLocalPos = __instance.speedArrows[0].transform.parent.localPosition;
        _areaHeight = _productBoxLocalPos.y - _oriBoxLocalPos.y;

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
        ResizeWindow(window);

        // 隐藏原版中间区域
        window.productBox.SetActive(false);
        window.productProbText.gameObject.SetActive(false);
        window.oriProductProbText.gameObject.SetActive(false);
        if (window.historyText != null) window.historyText.gameObject.SetActive(false);

        // 先隐藏 oriProductBox，稍后移至 Area3
        window.oriProductBox.SetActive(false);

        float area1Y = _productBoxLocalPos.y + _layoutOffsetY + MainSideExtraYOffset;
        float area2Y = _oriBoxLocalPos.y + _layoutOffsetY + MainSideExtraYOffset;
        float area3Y = _oriBoxLocalPos.y - _areaHeight;
        float area3ContentY = area3Y + _layoutOffsetY * FluidLabelRightTextYOffsetFactor;
        float labelOffsetY = _productProbLocalPos.y - _productBoxLocalPos.y;

        float mainLabelX = _speedArrowParentLocalPos.x + _layoutOffsetX + LabelArrowNudgeX;
        float sideLabelX = _speedArrowParentLocalPos.x + _layoutOffsetX + LabelArrowNudgeX;
        float fluidLabelX = _speedArrowParentLocalPos.x + _layoutOffsetX + LabelArrowNudgeX;
        Vector3 mainLabelPos = new(mainLabelX, area1Y + labelOffsetY, _productProbLocalPos.z);
        Vector3 sideLabelPos = new(sideLabelX, area2Y + labelOffsetY, _productProbLocalPos.z);
        Vector3 fluidLabelPos = new(fluidLabelX, area3ContentY + labelOffsetY, _productProbLocalPos.z);

        window.oriProductBox.SetActive(false);

        // fluidRightText
        if (window.oriProductProbText != null) {
            GameObject frGo = Object.Instantiate(window.oriProductProbText.gameObject, window.transform);
            frGo.name = "fluid-right-info";
            float fluidTextY = area3Y + 8f + _layoutOffsetY * FluidLabelRightTextYOffsetFactor;
            frGo.transform.localPosition = new Vector3(_oriBoxLocalPos.x + 80f + _layoutOffsetX, fluidTextY, _oriBoxLocalPos.z);
            fluidRightText = frGo.GetComponent<Text>();
            fluidRightText.alignment = TextAnchor.UpperLeft;
            fluidRightText.horizontalOverflow = HorizontalWrapMode.Overflow;
            fluidRightText.verticalOverflow = VerticalWrapMode.Overflow;
            fluidRightText.supportRichText = true;
            frGo.SetActive(false);
        }

        // Area2 箭头克隆（从 vanillaWindow 克隆，放到 window 下）
        GameObject vanillaArrowParent = vanillaWindow.speedArrows[0].transform.parent.gameObject;

        GameObject mainArrowParent = Object.Instantiate(vanillaArrowParent, window.transform);
        mainArrowParent.name = "produce-main";
        mainArrowParent.transform.localPosition = new Vector3(
            _speedArrowParentLocalPos.x + _layoutOffsetX, area1Y, _speedArrowParentLocalPos.z);
        _mainArrows = CloneArrowImagesOrdered(vanillaArrowParent, mainArrowParent, vanillaWindow);
        _mainArrowText = CloneArrowText(vanillaArrowParent, mainArrowParent, vanillaWindow);

        GameObject sideArrowParent = Object.Instantiate(vanillaArrowParent, window.transform);
        sideArrowParent.name = "produce-side";
        sideArrowParent.transform.localPosition = new Vector3(
            _speedArrowParentLocalPos.x + _layoutOffsetX, area2Y, _speedArrowParentLocalPos.z);
        _sideArrows = CloneArrowImagesOrdered(vanillaArrowParent, sideArrowParent, vanillaWindow);
        _sideArrowText = CloneArrowText(vanillaArrowParent, sideArrowParent, vanillaWindow);

        // Area3 箭头克隆
        GameObject fluidArrowParent = Object.Instantiate(vanillaArrowParent, window.transform);
        fluidArrowParent.name = "produce-fluid";
        fluidArrowParent.transform.localPosition = new Vector3(
            _speedArrowParentLocalPos.x + _layoutOffsetX, area3ContentY, _speedArrowParentLocalPos.z);
        _fluidArrows = CloneArrowImagesOrdered(vanillaArrowParent, fluidArrowParent, vanillaWindow);
        _fluidArrowText = CloneArrowText(vanillaArrowParent, fluidArrowParent, vanillaWindow);

        if (window.speedArrows != null) {
            for (int i = 0; i < window.speedArrows.Length; i++) {
                if (window.speedArrows[i] == null) continue;
                ((Behaviour)window.speedArrows[i]).enabled = false;
                window.speedArrows[i].gameObject.SetActive(false);
            }
        }

        // 克隆分割线（Area2/Area3 之间）
        if (window.sepLine0 != null) {
            float sep23Y = window.sepLine0.transform.localPosition.y - _areaHeight;
            GameObject newSep0 = Object.Instantiate(window.sepLine0.gameObject, window.transform);
            newSep0.name = "sep-line-0-extra";
            newSep0.transform.localPosition = new Vector3(
                window.sepLine0.transform.localPosition.x, sep23Y, window.sepLine0.transform.localPosition.z);

            if (window.sepLine1 != null) {
                GameObject newSep1 = Object.Instantiate(window.sepLine1.gameObject, window.transform);
                newSep1.name = "sep-line-1-extra";
                newSep1.transform.localPosition = new Vector3(
                    window.sepLine1.transform.localPosition.x, sep23Y, window.sepLine1.transform.localPosition.z);
            }
        }

        Text refLabel = window.productProbText ?? window.titleText;
        mainSectionLabel   = CreateLabel(window, refLabel, "主产物".Translate(), mainLabelPos);
        appendSectionLabel = CreateLabel(window, refLabel, "副产物".Translate(), sideLabelPos);
        fluidSectionLabel  = CreateLabel(window, refLabel, "流体输出".Translate(), fluidLabelPos);

        // 创建产物槽（固定位置）
        for (int i = 0; i < MaxMainSlots; i++) {
            Vector3 pos = new Vector3(_productBoxLocalPos.x + _layoutOffsetX + i * SlotSpacing, area1Y, _productBoxLocalPos.z);
            mainSlots[i] = CreateSlot(window, vanillaWindow, pos);
        }
        for (int i = 0; i < MaxSideSlots; i++) {
            Vector3 pos = new Vector3(_oriBoxLocalPos.x + _layoutOffsetX + i * SlotSpacing, area2Y, _oriBoxLocalPos.z);
            sideSlots[i] = CreateSlot(window, vanillaWindow, pos);
        }

        Vector3 fluidPos = new Vector3(_oriBoxLocalPos.x + _layoutOffsetX, area3ContentY, _oriBoxLocalPos.z);
        fluidSlot = CreateSlot(window, vanillaWindow, fluidPos);
    }

    private static void ResizeWindow(UIFractionatorWindow window) {
        RectTransform rootRect = window.GetComponent<RectTransform>();
        if (rootRect == null) return;

        Vector2 rootSize = rootRect.sizeDelta;
        float addWidth = SlotSpacing * ExtraSlotCountForWidth;
        float addHeight = rootSize.y * (HeightScale - 1f);
        _layoutOffsetX = -addWidth * 0.5f;
        _layoutOffsetY = -addHeight * 0.5f;

        rootRect.sizeDelta = new Vector2(rootSize.x + addWidth, rootSize.y + addHeight);

        RectTransform[] rects = window.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++) {
            RectTransform rect = rects[i];
            if (rect == null || rect == rootRect) continue;
            Vector2 size = rect.sizeDelta;
            if (Mathf.Abs(size.x - rootSize.x) < 0.01f && Mathf.Abs(size.y - rootSize.y) < 0.01f) {
                rect.sizeDelta = new Vector2(size.x + addWidth, size.y + addHeight);
            }
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

    private static ProductSlot CreateSlot(UIFractionatorWindow window, UIFractionatorWindow vanillaWindow, Vector3 localPos) {
        GameObject go = Object.Instantiate(vanillaWindow.oriProductBox, window.transform);
        go.SetActive(false);
        go.transform.localPosition = localPos;

        Image[] origImages  = vanillaWindow.oriProductBox.GetComponentsInChildren<Image>(true);
        Image[] cloneImages = go.GetComponentsInChildren<Image>(true);

        int iconIdx = Array.IndexOf(origImages, vanillaWindow.oriProductIcon);
        Image cloneIcon = (iconIdx >= 0 && iconIdx < cloneImages.Length)
            ? cloneImages[iconIdx] : go.GetComponentInChildren<Image>(true);

        UIButton cloneButton = cloneIcon != null
            ? cloneIcon.GetComponent<UIButton>() ?? go.GetComponentInChildren<UIButton>(true)
            : go.GetComponentInChildren<UIButton>(true);

        Text[] origTexts    = vanillaWindow.oriProductBox.GetComponentsInChildren<Text>(true);
        Text[] cloneTexts   = go.GetComponentsInChildren<Text>(true);
        int countIdx = Array.IndexOf(origTexts, vanillaWindow.oriProductCountText);
        Text cloneCountText = (countIdx >= 0 && countIdx < cloneTexts.Length)
            ? cloneTexts[countIdx] : go.GetComponentInChildren<Text>(true);

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
            RectTransform probRect  = slot.probText.GetComponent<RectTransform>();
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
        if (__instance == modWindow) return; // modWindow 自己不处理

        // factory 由原版 _OnOpen 设置
        PlanetFactory factory = __instance.factory;
        if (!IsModFractionator(__instance.fractionatorId, factory)) {
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

        return true; // 让原版 _OnClose 正常执行，清理 factory/player/button 等
    }

    // ===== _OnUpdate Prefix：拦截原版调用，驱动 modWindow 更新 =====
    // 关键原理：
    //   原版 originalWindow 的 active=true，游戏继续调用 originalWindow._Update()，
    //   触发 originalWindow._OnUpdate()，我们在 Prefix 里拦截，执行 modWindow 更新，
    //   return false 跳过原版的显示逻辑（避免原版代码重新显示 oriProductBox/productBox 等）。

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow._OnUpdate))]
    public static bool OnWindowUpdate(UIFractionatorWindow __instance) {
        if (__instance == modWindow) return false; // 防御：modWindow 不被游戏驱动

        if (modWindowGo == null) return true;

        bool isModBuilding = IsModFractionator(__instance.fractionatorId, __instance.factory);
        if (isModBuilding) {
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

        // 输入侧
        if (hasFluid) {
            ItemProto needProto = LDB.items.Select(fractionator.fluidId);
            if (needProto != null) { modWindow.needIcon.sprite = needProto.iconSprite; ((Behaviour)modWindow.needIcon).enabled = true; }
            modWindow.needCountText.text = fractionator.fluidInputCount.ToString();
            ((Behaviour)modWindow.needCountText).enabled = true;
            ((Behaviour)modWindow.inputTitleText).enabled = true;
            ((Behaviour)modWindow.speedText).enabled = true;
            int inputInc = fractionator.fluidInputCount > 0 && fractionator.fluidInputInc > 0
                ? fractionator.fluidInputInc / fractionator.fluidInputCount : 0;
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
                ? fractionator.fluidInputCount / (double)fractionator.fluidInputCargoCount : 4.0;
        double speed = consumerRatio
            * (fractionator.fluidInputCargoCount < MaxBeltSpeed ? fractionator.fluidInputCargoCount : MaxBeltSpeed)
            * fluidInputCountPerCargo * 60.0;
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
                && fractionator.belt0 > 0 && fractionator.belt1 <= 0 && fractionator.belt2 <= 0) {
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
                    ? "分馏永动".Translate() : "产物堆积".Translate();
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

    private static BaseRecipe GetRecipeForBuilding(int buildingID, int fluidId) {
        return buildingID switch {
            IFE交互塔    => GetRecipe<BuildingTrainRecipe>(ERecipe.BuildingTrain, fluidId),
            IFE矿物复制塔 => GetRecipe<MineralCopyRecipe>(ERecipe.MineralCopy, fluidId),
            IFE点数聚集塔 => GetRecipe<PointAggregateRecipe>(ERecipe.PointAggregate, fluidId),
            IFE转化塔    => GetRecipe<ConversionRecipe>(ERecipe.Conversion, fluidId),
            IFE回收塔    => GetRecipe<RecycleRecipe>(ERecipe.Recycle, fluidId),
            _ => null
        };
    }

    private static void UpdateUIElements(UIFractionatorWindow src,
        FractionatorComponent fractionator, BaseRecipe recipe,
        float recipeSuccessRatio, float mainOutputBonus, float destroyRatio, bool hasFluid) {

        List<ProductOutputInfo> products = fractionator.products(src.factory);
        bool sandboxMode = GameMain.sandboxToolsEnabled;

        foreach (var slot in mainSlots) if (slot != null) slot.go.SetActive(false);
        foreach (var slot in sideSlots) if (slot != null) slot.go.SetActive(false);
        if (fluidSlot != null) fluidSlot.go.SetActive(false);

        if (!hasFluid) {
            mainSectionLabel?.gameObject.SetActive(false);
            appendSectionLabel?.gameObject.SetActive(false);
            fluidSectionLabel?.gameObject.SetActive(false);
            if (_mainArrowText != null) _mainArrowText.gameObject.SetActive(false);
            if (_sideArrowText != null) _sideArrowText.gameObject.SetActive(false);
            if (_fluidArrowText != null) _fluidArrowText.gameObject.SetActive(false);
            if (fluidRightText != null) fluidRightText.gameObject.SetActive(false);
            if (modWindow.oriProductBox != null) modWindow.oriProductBox.SetActive(false);
            if (modWindow.oriProductIcon != null) ((Behaviour)modWindow.oriProductIcon).enabled = false;
            if (modWindow.oriProductCountText != null) ((Behaviour)modWindow.oriProductCountText).enabled = false;
            if (modWindow.oriProductIncs != null) {
                for (int i = 0; i < modWindow.oriProductIncs.Length; i++)
                    if (modWindow.oriProductIncs[i] != null) ((Behaviour)modWindow.oriProductIncs[i]).enabled = false;
            }
            return;
        }

        int mainCount = 0, sideCount = 0;
        float mainSuccessSum = 0f;

        if (recipe != null && !recipe.Locked) {
            foreach (var output in recipe.OutputMain) {
                if (mainCount >= MaxMainSlots) break;
                var pInfo = products.Find(p => p.itemId == output.OutputID && p.isMainOutput);
                float ratio = recipeSuccessRatio * output.SuccessRatio * mainOutputBonus;
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

        mainSectionLabel?.gameObject.SetActive(false);
        appendSectionLabel?.gameObject.SetActive(false);
        fluidSectionLabel?.gameObject.SetActive(false);

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
                ? (1f - recipeSuccessRatio - destroyRatio).FormatP() : "---";
            string destroyStr = recipe != null && !recipe.Locked
                ? destroyRatio.FormatP() : "---";
            fluidRightText.text =
                $"{"配方强化".Translate()} {(recipeLevel > 0 ? $"+{recipeLevel}" : "0")}\n"
                + $"{flowStr}  <color=#{ColorUtility.ToHtmlStringRGBA(DestroyColor)}>{destroyStr}</color>";
        }

        if (fractionator.fluidId > 0) {
            float fluidRatio = Mathf.Clamp01(1f - mainSuccessSum);
            FillFluidSlot(fluidSlot, fractionator.fluidId, fractionator.fluidOutputCount, fluidRatio);
            int fluidInc = fractionator.fluidOutputCount > 0 && fractionator.fluidOutputInc > 0
                ? fractionator.fluidOutputInc / fractionator.fluidOutputCount : 0;
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
            if (canAdd <= 0) { UIRealtimeTip.Popup("栏位已满".Translate()); return; }
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
