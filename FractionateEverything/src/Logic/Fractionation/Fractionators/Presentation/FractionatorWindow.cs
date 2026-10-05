using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FE.Logic.Civilization;
using FE.Logic.DataCenter;
using FE.Logic.Fractionation.Recipes;
using FE.UI.Controls;
using FE.UI.Foundation;
using FE.UI.MainPanel.Setting;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using static FE.Logic.Fractionation.Fractionators.ProcessManager;
using static FE.Logic.Fractionation.Recipes.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Fractionators.Presentation;

/// <summary>
/// 模组分馏塔独立窗口。
/// 复制原版窗口实例，在创建时一次性修改布局，后续只需控制显示/隐藏。
/// 原版分馏塔直接使用原版窗口，不做任何修改。
/// </summary>
public static class FractionatorWindow {
    #region 状态/初始化

    // ===== 窗口实例 =====
    private static GameObject modWindowGo;

    // ===== 布局常量 =====
    private const int MaxMainSlots = 5;
    private const int MaxSideSlots = 4;
    private const float SlotSpacing = 55f;
    // AddWidth = SlotSpacing * (Mathf.Max(MaxMainSlots, MaxSideSlots) - 1);
    private const float AddWidth = SlotSpacing * 4;
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

    // 通过路径查找缓存的模组窗口共享组件（比依赖 Instantiate 自动映射更可靠）
    private static Text _modPowerText;
    private static Image _modPowerIcon;

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
    private static Text recipeSelectorLabel;
    private static MyComboBox recipeSelector;
    private static List<int> selectableRecipeIds = [];
    private static string selectableRecipeSignature = string.Empty;
    private static bool suppressRecipeSelection;

    // 一次性记录的原版元素 localPosition
    private static Vector3 _itemBoxLocalPos;
    private static Vector3 _oriBoxLocalPos;
    private static Vector3 _speedArrowParentLocalPos;
    private static float _layoutOffsetX;

    private static float currentAddHeight = -1f;

    /// <summary>
    /// 分馏塔窗口单个产物槽位的 UI 引用。
    /// </summary>
    private class ProductSlot {
        /// <summary>
        /// 保存产物槽根对象。
        /// </summary>
        public GameObject go;
        /// <summary>
        /// 保存产物槽物品图标。
        /// </summary>
        public Image icon;
        /// <summary>
        /// 保存产物槽按钮组件。
        /// </summary>
        public UIButton button;
        /// <summary>
        /// 保存产物槽数量文本。
        /// </summary>
        public Text countText;
        /// <summary>
        /// 保存产物槽概率文本。
        /// </summary>
        public Text probText;
        /// <summary>
        /// 保存产物槽增产点箭头图标数组。
        /// </summary>
        public Image[] incArrows;
        /// <summary>
        /// 保存产物槽显示类型。
        /// </summary>
        public ProductSlotKind kind;
        /// <summary>
        /// 保存产物槽左键点击处理器。
        /// </summary>
        public Action<int> clickHandler;
    }

    /// <summary>
    /// 分馏塔产物槽位的主产物、副产物和流体分类。
    /// </summary>
    private enum ProductSlotKind {
        Main,
        Side,
        Fluid,
    }

    private static void BindSlotClickHandlers() {
        if (slotClickBound) {
            return;
        }
        foreach (var slot in mainSlots) {
            if (slot?.button != null) {
                slot.clickHandler = itemId => OnSlotClick(slot, itemId);
                slot.button.onClick += slot.clickHandler;
            }
        }
        foreach (var slot in sideSlots) {
            if (slot?.button != null) {
                slot.clickHandler = itemId => OnSlotClick(slot, itemId);
                slot.button.onClick += slot.clickHandler;
            }
        }
        if (fluidSlot?.button != null) {
            fluidSlot.clickHandler = itemId => OnSlotClick(fluidSlot, itemId);
            fluidSlot.button.onClick += fluidSlot.clickHandler;
        }
        slotClickBound = true;
    }

    private static void UnbindSlotClickHandlers() {
        if (!slotClickBound) {
            return;
        }
        foreach (var slot in mainSlots) {
            if (slot?.button != null && slot.clickHandler != null) {
                slot.button.onClick -= slot.clickHandler;
                slot.clickHandler = null;
            }
        }
        foreach (var slot in sideSlots) {
            if (slot?.button != null && slot.clickHandler != null) {
                slot.button.onClick -= slot.clickHandler;
                slot.clickHandler = null;
            }
        }
        if (fluidSlot?.button != null && fluidSlot.clickHandler != null) {
            fluidSlot.button.onClick -= fluidSlot.clickHandler;
            fluidSlot.clickHandler = null;
        }
        slotClickBound = false;
    }

    /// <summary>判断是否为模组分馏塔建筑</summary>
    public static bool IsModFractionator(int fractionatorId, PlanetFactory factory) {
        if (fractionatorId == 0 || factory == null) return false;
        FractionatorComponent frac = factory.factorySystem.fractionatorPool[fractionatorId];
        if (frac.id != fractionatorId) return false;
        int buildingId = factory.entityPool[frac.entityId].protoId;
        return FractionatorTowerCatalog.IsActiveFractionator(buildingId);
    }

    private static void OnSlotClick(ProductSlot slot, int itemId) {
        UIFractionatorWindow target = sourceWindow ?? modWindow;
        if (target == null) return;
        HandleProductSlotClick(target, itemId, slot);
    }

    #endregion

    #region 布局

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

    /// <summary>
    /// 创建并初始化模组分馏塔窗口实例。
    /// </summary>
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

        // 流动输出右侧的提示文字，包括当前配方状态与损毁率
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
        }

        recipeSelectorLabel = CreateTopLabel(window, window.stateText, "配方选择".Translate(), 24f, 47f, 104f);
        MyComboBox.InitBaseObject();
        recipeSelector = MyComboBox.CreateComboBox(126f, 43f, window.GetComponent<RectTransform>())
            .WithFontSize(14)
            .WithSize(300f, 30f)
            .WithItems("未选择配方")
            .WithIndex(0)
            .WithOnSelChanged(OnAdvancedRecipeSelected);
        recipeSelector.gameObject.SetActive(false);
    }

    private static Text CreateTopLabel(UIFractionatorWindow window, Text reference, string text,
        float left, float top, float width) {
        GameObject go = Object.Instantiate(reference.gameObject, window.transform);
        go.name = $"fe-label-{left:0}-{top:0}";
        Text label = go.GetComponent<Text>();
        label.text = text;
        label.fontSize = 14;
        label.alignment = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.supportRichText = true;
        RectTransform rect = RectTransformUtils.NormalizeRectWithTopLeft(label, left, top, window.transform);
        rect.sizeDelta = new Vector2(width, 30f);
        go.SetActive(false);
        return label;
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

    #endregion

    #region 生命周期

    // ===== _OnOpen Postfix：让原版正常执行，然后切换显示 =====
    // 关键原理：
    //   ManualBehaviour._Open() 先设 active=true，再调用 _OnOpen()。
    //   让原版 _OnOpen 执行，它会正确设置 factory/player 等所有字段。
    //   执行完后，在 Postfix 里隐藏 originalWindow（但保持 active=true），
    //   并设置 unsafeGameObjectState=true，让游戏的 _UpdateArray 继续驱动其 _Update，
    //   从而触发我们的 _OnUpdate Prefix，进而更新 modWindow。

    /// <summary>
    /// 在分馏塔窗口打开时切换到模组窗口状态。
    /// </summary>
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

    // ===== _OnClose Prefix：清理自定义状态，让原版正常清理字段 =====

    /// <summary>
    /// 在分馏塔窗口关闭时清理模组窗口状态。
    /// </summary>
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

    /// <summary>
    /// 在分馏塔窗口更新时刷新模组产物槽显示。
    /// </summary>
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

    #endregion

    #region 库存交互

    // ===== OnProductUIButtonClick 拦截 =====

    private static ProductOutputInfo FindProductByItemId(List<ProductOutputInfo> products, int itemId) {
        if (products == null) {
            return null;
        }
        for (int i = 0; i < products.Count; i++) {
            ProductOutputInfo product = products[i];
            if (product.itemId == itemId) {
                return product;
            }
        }
        return null;
    }

    private static ProductOutputInfo FindProductBySlot(List<ProductOutputInfo> products, ProductSlot slot,
        int itemId) {
        if (products == null || slot == null || slot.kind == ProductSlotKind.Fluid) {
            return null;
        }
        bool isMainOutput = slot.kind == ProductSlotKind.Main;
        for (int i = 0; i < products.Count; i++) {
            ProductOutputInfo product = products[i];
            if (product.itemId == itemId && product.isMainOutput == isMainOutput) {
                return product;
            }
        }
        return null;
    }

    private static bool IsFluidSlot(FractionatorComponent fractionator, ProductSlot slot, int itemId) {
        return slot != null ? slot.kind == ProductSlotKind.Fluid : itemId == fractionator.fluidId;
    }

    private static int GetModSlotCount(FractionatorComponent fractionator, List<ProductOutputInfo> products,
        int itemId, ProductSlot slot = null) {
        if (slot != null) {
            if (slot.kind == ProductSlotKind.Fluid) {
                return itemId == fractionator.fluidId ? fractionator.fluidOutputCount : 0;
            }
            return FindProductBySlot(products, slot, itemId)?.count ?? 0;
        }
        if (itemId == fractionator.productId) {
            return fractionator.productOutputCount;
        }
        if (itemId == fractionator.fluidId) {
            return fractionator.fluidOutputCount;
        }
        ProductOutputInfo product = FindProductByItemId(products, itemId);
        return product?.count ?? 0;
    }

    private static void SetModSlotCount(ref FractionatorComponent fractionator, List<ProductOutputInfo> products,
        int itemId,
        int count, ProductSlot slot = null) {
        if (slot != null) {
            if (slot.kind == ProductSlotKind.Fluid) {
                if (itemId == fractionator.fluidId) {
                    fractionator.fluidOutputCount = count;
                    if (count <= 0) {
                        fractionator.fluidOutputInc = 0;
                    }
                }
                return;
            }
            ProductOutputInfo slotProduct = FindProductBySlot(products, slot, itemId);
            if (slotProduct != null) {
                slotProduct.count = count;
                if (slotProduct.itemId == fractionator.productId && slotProduct.isMainOutput) {
                    fractionator.productOutputCount = count;
                }
            }
            return;
        }
        if (itemId == fractionator.productId) {
            fractionator.productOutputCount = count;
        }
        if (itemId == fractionator.fluidId) {
            fractionator.fluidOutputCount = count;
            if (count <= 0) {
                fractionator.fluidOutputInc = 0;
            }
        } else {
            ProductOutputInfo product = FindProductByItemId(products, itemId);
            if (product != null) {
                product.count = count;
            }
        }
    }

    private static int GetModSlotMax(FractionatorComponent fractionator, int itemId, ProductSlot slot = null) {
        // 缓存上限随全局堆叠变化，已放置塔的原版字段可能是旧值，所以读当前上限。
        return IsFluidSlot(fractionator, slot, itemId) ? BaseFracFluidOutputMax : BaseFracProductOutputMax;
    }

    /// <summary>
    /// 处理模组分馏塔产物槽按钮点击。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow.OnProductUIButtonClick))]
    public static bool OnProductUIButtonClick_Prefix(UIFractionatorWindow __instance, int itemId) {
        return HandleProductSlotClick(__instance, itemId, null);
    }

    private static bool HandleProductSlotClick(UIFractionatorWindow __instance, int itemId, ProductSlot slot) {
        if (__instance.fractionatorId == 0 || __instance.factory == null || __instance.player == null) {
            return true;
        }
        ref FractionatorComponent fractionator =
            ref __instance.factorySystem.fractionatorPool[__instance.fractionatorId];
        if (fractionator.id != __instance.fractionatorId
            || !IsModFractionator(__instance.fractionatorId, __instance.factory)) {
            return true;
        }

        int buildingId = __instance.factory.entityPool[fractionator.entityId].protoId;
        if (FractionatorTowerCatalog.IsAdvancedTower(buildingId)) {
            return HandleAdvancedOutputSlotClick(__instance, itemId, slot);
        }

        // 额外主产物/副产物复用了原版回调；如果不在这里完全接管，
        // 原版会把“非第一主产物”错误走到流体输出分支，导致错取和无限取。
        // 自定义槽位需要额外带上槽位身份，避免主路目标只靠 itemId 反查到错误缓存。
        Player player = __instance.player;
        List<ProductOutputInfo> products = fractionator.products(__instance.factory);
        int currentCount = GetModSlotCount(fractionator, products, itemId, slot);
        bool isFluidSlot = IsFluidSlot(fractionator, slot, itemId);

        if (player.inhandItemId > 0 && player.inhandItemCount == 0) {
            player.SetHandItems(0, 0);
            return false;
        }

        if (player.inhandItemId > 0 && player.inhandItemCount > 0) {
            if (player.inhandItemId != itemId) {
                return false;
            }
            int canAdd = GetModSlotMax(fractionator, itemId, slot) - currentCount;
            if (canAdd < 0) {
                canAdd = 0;
            }
            int add = Math.Min(player.inhandItemCount, canAdd);
            if (add <= 0) {
                UIRealtimeTip.Popup("栏位已满".Translate());
                return false;
            }
            int handCount = player.inhandItemCount;
            int handInc = player.inhandItemInc;
            int takeInc = isFluidSlot ? split_inc(ref handCount, ref handInc, add) : 0;

            SetModSlotCount(ref fractionator, products, itemId, currentCount + add, slot);
            fractionator.GetExtraState(__instance.factory).InvalidateFullProductCache();
            player.AddHandItemCount_Unsafe(-add);
            if (isFluidSlot) {
                player.SetHandItemInc_Unsafe(player.inhandItemInc - takeInc);
                fractionator.fluidOutputInc += takeInc;
            }
            if (player.inhandItemCount <= 0) {
                player.SetHandItemId_Unsafe(0);
                player.SetHandItemCount_Unsafe(0);
                player.SetHandItemInc_Unsafe(0);
            }
            return false;
        }

        if (player.inhandItemId != 0 || player.inhandItemCount != 0 || currentCount == 0) {
            return false;
        }

        int currentInc = isFluidSlot ? fractionator.fluidOutputInc : 0;
        int removedCount = currentCount;
        int removedInc = currentInc;
        bool quickMove = VFInput.control || VFInput.shift;
        if (!quickMove) {
            player.SetHandItemId_Unsafe(itemId);
            player.SetHandItemCount_Unsafe(currentCount);
            player.SetHandItemInc_Unsafe(currentInc);
        }
        SetModSlotCount(ref fractionator, products, itemId, 0, slot);
        fractionator.GetExtraState(__instance.factory).InvalidateFullProductCache();
        if (isFluidSlot) {
            fractionator.fluidOutputInc = Math.Max(0, fractionator.fluidOutputInc - removedInc);
        }
        if (quickMove) {
            int addedToPackage = player.TryAddItemToPackage(
                itemId, removedCount, removedInc, throwTrash: false);
            if (addedToPackage > 0) {
                UIItemup.Up(itemId, addedToPackage);
            }
        }
        return false;
    }

    private static bool HandleAdvancedOutputSlotClick(UIFractionatorWindow window, int itemId, ProductSlot slot) {
        if (slot == null || itemId <= 0 || window?.player == null || window.factory == null) {
            return false;
        }
        if (FE.Compatibility.Nebula.NebulaMultiplayerModAPI.IsClient) {
            UIRealtimeTip.Popup("高级塔客户端手动取出受限".Translate());
            return false;
        }

        Player player = window.player;
        if (player.inhandItemId > 0 || player.inhandItemCount > 0) {
            return false;
        }
        AdvancedFractionatorState state = AdvancedFractionatorRuntime.GetState(window.factory, window.fractionatorId);
        if (state == null) {
            return false;
        }
        bool flowOutput = slot.kind != ProductSlotKind.Main;
        int count;
        lock (state) {
            Dictionary<int, int> source = flowOutput ? state.FlowOutputs : state.Products;
            count = source.TryGetValue(itemId, out int stored) ? stored : 0;
        }
        if (count <= 0) {
            return false;
        }

        bool quickMove = VFInput.control || VFInput.shift;
        int taken = AdvancedFractionatorRuntime.TakeOutput(
            window.factory, window.fractionatorId, itemId, count, flowOutput, out int takenInc);
        if (taken <= 0) {
            return false;
        }
        if (quickMove) {
            int addedToPackage = player.TryAddItemToPackage(itemId, taken, takenInc, throwTrash: false);
            if (addedToPackage > 0) {
                UIItemup.Up(itemId, addedToPackage);
            }
        } else {
            player.SetHandItems(itemId, taken, takenInc);
        }
        return false;
    }

    #endregion

    #region 渲染更新

    private static void OnAdvancedRecipeSelected(int index) {
        if (suppressRecipeSelection || sourceWindow?.factory == null || sourceWindow.fractionatorId <= 0
            || index < 0 || index >= selectableRecipeIds.Count) {
            return;
        }
        FractionatorComponent fractionator =
            sourceWindow.factorySystem.fractionatorPool[sourceWindow.fractionatorId];
        if (fractionator.id != sourceWindow.fractionatorId) {
            return;
        }
        int recipeId = selectableRecipeIds[index];
        AdvancedRecipeSelectionSync.SetSelectedRecipeAndSync(
            sourceWindow.factory, fractionator.entityId, recipeId, manual: true);
    }

    private static void UpdateAdvancedControls(UIFractionatorWindow src, int buildingId,
        AdvancedFractionatorState state) {
        bool advanced = FractionatorTowerCatalog.IsAdvancedTower(buildingId);
        recipeSelectorLabel?.gameObject.SetActive(advanced);
        recipeSelector?.gameObject.SetActive(advanced);
        if (!advanced || recipeSelector == null || state == null) {
            return;
        }

        List<WorldviewRecipe> recipes = GetUnlockedRecipesForTower(buildingId);
        string signature = $"{buildingId}:{string.Join(",", recipes.Select(recipe => recipe.PersistentId))}";
        if (signature != selectableRecipeSignature) {
            selectableRecipeSignature = signature;
            selectableRecipeIds = [0, .. recipes.Select(recipe => recipe.PersistentId)];
            string[] names = ["未选择配方", .. recipes.Select(recipe => recipe.DisplayName)];
            suppressRecipeSelection = true;
            recipeSelector.SetItems(names);
            suppressRecipeSelection = false;
        }

        int selectedIndex = selectableRecipeIds.IndexOf(state.SelectedRecipeId);
        if (selectedIndex < 0) {
            selectedIndex = 0;
        }
        suppressRecipeSelection = true;
        recipeSelector.SetIndex(selectedIndex);
        suppressRecipeSelection = false;
    }

    private static void UpdateAdvancedPowerDisplay(UIFractionatorWindow src, AdvancedFractionatorState state) {
        string text = string.Format("当前营养：{0} / {1}".Translate(),
            state?.Nutrition.ToString("0.0") ?? "0.0", AdvancedFractionatorRuntime.MaximumNutrition.ToString("0"));
        src.powerText.text = text;
        src.powerText.color = state != null && state.Nutrition > 0f ? src.powerNormalColor : src.powerOffColor;
        src.powerIcon.color = Color.clear;
        if (_modPowerText != null) {
            _modPowerText.text = text;
            _modPowerText.color = src.powerText.color;
        }
        if (_modPowerIcon != null) {
            _modPowerIcon.color = Color.clear;
        }
    }

    private static void UpdateAdvancedStateText(AdvancedFractionatorState state, WorldviewRecipe recipe,
        FractionatorComponent fractionator) {
        AdvancedTowerStatus status = AdvancedFractionatorRuntime.GetOperatingStatus(state);
        string text = status switch {
            AdvancedTowerStatus.AwaitingNourishment => "等待供养",
            AdvancedTowerStatus.MissingMatrix => "缺少矩阵",
            AdvancedTowerStatus.AwaitingRecipe => "等待配方",
            AdvancedTowerStatus.MissingInput => "缺少原材料",
            AdvancedTowerStatus.OutputBlocked => "输出堵塞",
            AdvancedTowerStatus.Stacking => "运行中 · 等待集装",
            _ => "正常运转",
        };
        modWindow.stateText.text = text.Translate();
        modWindow.stateText.color = status is AdvancedTowerStatus.Running or AdvancedTowerStatus.Stacking
            ? modWindow.workNormalColor
            : status == AdvancedTowerStatus.AwaitingRecipe ? modWindow.idleColor : modWindow.workStoppedColor;
    }

    private static void UpdateAdvancedUI(UIFractionatorWindow src, FractionatorComponent fractionator,
        WorldviewRecipe recipe, AdvancedFractionatorState state) {
        foreach (ProductSlot slot in mainSlots) {
            slot?.go?.SetActive(false);
        }
        foreach (ProductSlot slot in sideSlots) {
            slot?.go?.SetActive(false);
        }
        fluidSlot?.go.SetActive(false);
        fluidRightText?.gameObject.SetActive(false);

        int productCount = 0;
        int flowCount = 0;
        lock (state) {
            if (recipe != null) {
                foreach (WorldviewRecipeOutput output in recipe.Products) {
                    if (productCount >= MaxMainSlots) {
                        break;
                    }
                    int count = state.Products.TryGetValue(output.ItemId, out int stored) ? stored : 0;
                    FillAdvancedSlot(mainSlots[productCount], output.ItemId, count,
                        fractionator.produceProb.FormatP(), ProductSlotKind.Main);
                    productCount++;
                }
            }
            foreach (KeyValuePair<int, int> output in state.FlowOutputs.OrderBy(pair => pair.Key)) {
                if (flowCount >= MaxSideSlots || output.Value <= 0) {
                    break;
                }
                FillAdvancedSlot(sideSlots[flowCount], output.Key, output.Value, string.Empty,
                    ProductSlotKind.Side);
                flowCount++;
            }
        }

        if (_mainArrowText != null) {
            _mainArrowText.gameObject.SetActive(productCount > 0);
            _mainArrowText.text = "配方产物".Translate();
            _mainArrowText.color = ProbColor;
        }
        if (_sideArrowText != null) {
            _sideArrowText.gameObject.SetActive(flowCount > 0);
            _sideArrowText.text = "流动输出".Translate();
            _sideArrowText.color = ProbColor;
        }
        _fluidArrowText?.gameObject.SetActive(false);
        SetArrowGroup(_mainArrows, productCount > 0,
            fractionator.fractionSuccess ? modWindow.marqueeOnColor : modWindow.marqueeOffColor);
        SetArrowGroup(_sideArrows, flowCount > 0, modWindow.marqueeOffColor);
        SetArrowGroup(_fluidArrows, false, modWindow.marqueeOffColor);

        int visibleCount = Math.Max(productCount, flowCount);
        float width = Math.Max(AddWidth, Math.Max(0, visibleCount - 1) * SlotSpacing);
        ApplyWindowSizeKeepingTopLeft(width, flowCount > 0 ? AddHeight : AddHeight - 60f);
        RefreshLayoutX(flowCount > 0 ? FluidY : SideY);
    }

    private static void FillAdvancedSlot(ProductSlot slot, int itemId, int count, string probability,
        ProductSlotKind kind) {
        if (slot == null) {
            return;
        }
        slot.go.SetActive(true);
        slot.kind = kind;
        if (slot.button != null) {
            slot.button.data = itemId;
        }
        ItemProto item = LDB.items.Select(itemId);
        if (slot.icon != null && item != null) {
            slot.icon.sprite = item.iconSprite;
        }
        if (slot.countText != null) {
            slot.countText.text = count.ToString();
        }
        if (slot.probText != null) {
            slot.probText.text = probability;
            slot.probText.color = ProbColor;
        }
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

        int buildingID = src.factory.entityPool[fractionator.entityId].protoId;
        ItemProto building = LDB.items.Select(buildingID);
        if (building == null) {
            return;
        }
        bool advanced = FractionatorTowerCatalog.IsAdvancedTower(buildingID);
        AdvancedFractionatorState advancedState = advanced
            ? AdvancedFractionatorRuntime.GetOrCreateState(src.factory, src.fractionatorId)
            : null;
        WorldviewRecipe advancedRecipe = advancedState == null
            ? null
            : RecipeManager.GetRecipe(advancedState.SelectedRecipeId);
        bool hasFluid = advanced ? advancedRecipe != null : fractionator.fluidId > 0;

        modWindow.titleText.text = building.name;
        UpdateAdvancedControls(src, buildingID, advancedState);

        float consumerRatio;
        if (advanced) {
            consumerRatio = 1f;
            UpdateAdvancedPowerDisplay(src, advancedState);
        } else {
            PowerConsumerComponent powerConsumer = src.powerSystem.consumerPool[fractionator.pcId];
            int networkId = powerConsumer.networkId;
            PowerNetwork powerNetwork = src.powerSystem.netPool[networkId];
            consumerRatio = powerNetwork != null && networkId > 0 ? (float)powerNetwork.consumerRatio : 0f;
            UpdatePowerDisplay(src, powerConsumer, consumerRatio);
        }

        int inputItemId = advancedRecipe?.PrimaryInputId ?? fractionator.fluidId;
        if (hasFluid && inputItemId > 0) {
            ItemProto needProto = LDB.items.Select(inputItemId);
            if (needProto != null) {
                modWindow.needIcon.sprite = needProto.iconSprite;
                ((Behaviour)modWindow.needIcon).enabled = true;
            }
            modWindow.needCountText.text = fractionator.fluidInputCount.ToString();
            ((Behaviour)modWindow.needCountText).enabled = true;
            modWindow.inputTitleText.text = "流动输入".Translate();
            ((Behaviour)modWindow.inputTitleText).enabled = true;
            ((Behaviour)modWindow.speedText).enabled = true;
            int inputInc = fractionator.fluidInputCount > 0 && fractionator.fluidInputInc > 0
                ? fractionator.fluidInputInc / fractionator.fluidInputCount
                : 0;
            int inputArrowLevel = Cargo.fastIncArrowTable[Math.Min(inputInc, 10)];
            for (int i = 0; i < modWindow.needIncs.Length; i++) {
                ((Behaviour)modWindow.needIncs[i]).enabled = inputArrowLevel == i + 1;
            }
        } else {
            ((Behaviour)modWindow.needIcon).enabled = false;
            for (int i = 0; i < modWindow.needIncs.Length; i++) {
                ((Behaviour)modWindow.needIncs[i]).enabled = false;
            }
            ((Behaviour)modWindow.needCountText).enabled = false;
            ((Behaviour)modWindow.inputTitleText).enabled = false;
            ((Behaviour)modWindow.speedText).enabled = false;
        }

        if (advanced) {
            modWindow.speedText.text = fractionator.isWorking ? "生命反应中".Translate() : string.Empty;
        } else {
            double fluidInputCountPerCargo = 1.0;
            if (fractionator.fluidInputCount == 0) {
                fractionator.fluidInputCargoCount = 0f;
            } else {
                fluidInputCountPerCargo = fractionator.fluidInputCargoCount > 1e-4
                    ? fractionator.fluidInputCount / (double)fractionator.fluidInputCargoCount
                    : 4.0;
            }
            double speed = consumerRatio
                           * (fractionator.fluidInputCargoCount < MaxBeltSpeed
                               ? fractionator.fluidInputCargoCount
                               : MaxBeltSpeed)
                           * fluidInputCountPerCargo
                           * 60.0;
            if (!fractionator.isWorking) {
                speed = 0.0;
            }
            modWindow.speedText.text = string.Format("次分馏每分".Translate(), Math.Round(speed));
        }

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
                if (modWindow.speedArrows[i] != null) {
                    ((Behaviour)modWindow.speedArrows[i]).enabled = false;
                }
            }
        }

        if (modWindow.sepLine0 != null) ((Behaviour)modWindow.sepLine0).enabled = hasFluid;
        if (modWindow.sepLine1 != null) ((Behaviour)modWindow.sepLine1).enabled = hasFluid;
        if (modWindow.remindText != null) ((Behaviour)modWindow.remindText).enabled = !hasFluid;

        if (advanced) {
            UpdateAdvancedStateText(advancedState, advancedRecipe, fractionator);
            UpdateAdvancedUI(src, fractionator, advancedRecipe, advancedState);
            return;
        }

        bool workingNow = hasFluid && fractionator.isWorking;
        byte outputFlags = fractionator.GetCurrentOutputFlags(src.factory);
        bool mainLit = workingNow && (outputFlags & OutputFlagMain) != 0;
        bool sideLit = workingNow && (outputFlags & OutputFlagSide) != 0;
        bool fluidLit = workingNow && ((outputFlags & OutputFlagFluid) != 0 || (!mainLit && !sideLit));
        SetArrowGroup(_mainArrows, hasFluid, mainLit ? modWindow.marqueeOnColor : modWindow.marqueeOffColor);
        SetArrowGroup(_sideArrows, hasFluid, sideLit ? modWindow.marqueeOnColor : modWindow.marqueeOffColor);
        SetArrowGroup(_fluidArrows, hasFluid, fluidLit ? modWindow.marqueeOnColor : modWindow.marqueeOffColor);

        UpdateModStateText(src, fractionator, building, buildingID, consumerRatio);
        BaseRecipe recipe = GetRecipeForBuilding(buildingID, fractionator.fluidId);
        int avgInc = fractionator.fluidInputCount > 0 ? fractionator.fluidInputInc / fractionator.fluidInputCount : 0;
        float pointsBonus = (float)MaxTableMilli(avgInc);
        float recipeSuccessRatio = 0f;
        float destroyRatio = 0f;
        if (recipe != null && RecipeAvailabilityStore.IsAvailable(recipe)) {
            recipeSuccessRatio = recipe.SuccessRatio * (1 + pointsBonus);
            destroyRatio = recipe.DestroyRatio;
        }
        UpdateUIElements(src, fractionator, recipe, recipeSuccessRatio, destroyRatio, hasFluid);
    }

    private static void UpdateModStateText(UIFractionatorWindow src,
        FractionatorComponent fractionator, ItemProto building, int buildingID, float consumerRatio) {

        int fluidOutputMax = building.FluidOutputMax();
        int productOutputMax = building.ProductOutputMax();
        List<ProductOutputInfo> products = fractionator.products(src.factory);
        bool interactionMode = FractionatorTowerCatalog.IsBasicTower(buildingID)
                               && fractionator.belt0 > 0
                               && !fractionator.isOutput0
                               && fractionator.belt1 <= 0
                               && fractionator.belt2 <= 0
                               && products.All(product => product.count <= 0);
        if (interactionMode) {
            int rearItemId = src.factory.cargoTraffic.GetItemIdAtRear(fractionator.belt0);
            int rearStack = GetRearCargoStack(src.factory.cargoTraffic, fractionator.belt0);
            bool blocked = rearItemId > 0 && rearStack > 0
                           && !DataCenterUploadRouter.CanUploadStack(rearItemId, rearStack);
            modWindow.stateText.text = (blocked ? "栏位已满" : "交互模式").Translate();
            modWindow.stateText.color = blocked
                ? modWindow.workStoppedColor
                : fractionator.isWorking ? modWindow.workNormalColor : modWindow.idleColor;
            return;
        }

        if (fractionator.isWorking) {
            if (fractionator.fluidInputCount > 0) {
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
                modWindow.stateText.text = "产物堆积".Translate();
                modWindow.stateText.color = modWindow.workStoppedColor;
            } else if (fractionator.fluidInputCount == 0) {
                modWindow.stateText.text = "缺少原材料".Translate();
                modWindow.stateText.color = modWindow.workStoppedColor;
            } else {
                modWindow.stateText.text = "搬运模式".Translate();
                modWindow.stateText.color = modWindow.workStoppedColor;
            }
        }
        if (buildingID == IFE基础解析塔 && fractionator.fluidId > 0) {
            modWindow.stateText.text = ResearchManager.GetAnalysisMode(fractionator.fluidId).Translate()
                + " · " + modWindow.stateText.text;
        }
    }

    private static void UpdatePowerDisplay(UIFractionatorWindow src,
        PowerConsumerComponent powerConsumer, float consumerRatio) {
        src.powerServedSB ??= new StringBuilder("         W     %", 20);

        long powerPerMin = (long)((double)(powerConsumer.requiredEnergy * 60) * (double)consumerRatio + 0.5);
        StringBuilderUtility.WriteKMG(src.powerServedSB, 8, powerPerMin);
        StringBuilderUtility.WriteUInt(src.powerServedSB, 12, 3, (uint)(consumerRatio * 100f));

        if (consumerRatio == 1f) {
            src.powerText.text = src.powerServedSB.ToString();
            src.powerIcon.color = src.powerNormalIconColor;
            src.powerText.color = src.powerNormalColor;
        } else if (consumerRatio > 0.1f) {
            src.powerText.text = src.powerServedSB.ToString();
            src.powerIcon.color = src.powerLowIconColor;
            src.powerText.color = src.powerLowColor;
        } else {
            src.powerText.text = "未供电".Translate();
            src.powerIcon.color = Color.clear;
            src.powerText.color = src.powerOffColor;
        }

        if (_modPowerText != null) {
            _modPowerText.text = src.powerText.text;
            _modPowerText.color = src.powerText.color;
        }
        if (_modPowerIcon != null) {
            _modPowerIcon.color = src.powerIcon.color;
        }
    }

    private static BaseRecipe GetRecipeForBuilding(int buildingID, int fluidId) {
        return buildingID switch {
            IFE基础矿物塔 => GetRecipe<WorldviewRecipe>(ERecipe.BasicMineral, fluidId),
            IFE基础解析塔 => GetRecipe<WorldviewRecipe>(ERecipe.BasicAnalysis, fluidId),
            _ => null
        };
    }

    private static void UpdateUIElements(UIFractionatorWindow src,
        FractionatorComponent fractionator, BaseRecipe recipe,
        float recipeSuccessRatio, float destroyRatio, bool hasFluid) {

        List<ProductOutputInfo> products = fractionator.products(src.factory);
        bool showCompleteRecipeInformation = GameMain.sandboxToolsEnabled
                                             || Miscellaneous.ShowFractionateRecipeDetails;

        foreach (var slot in mainSlots)
            slot?.go?.SetActive(false);
        foreach (var slot in sideSlots)
            slot?.go?.SetActive(false);
        if (fluidSlot != null) fluidSlot.go.SetActive(false);

        int fractionatorId = src.fractionatorId;
        bool hasCachedWidth = widthByFractionatorId.TryGetValue(fractionatorId, out float cachedWidth);

        if (!hasFluid) {
            ApplyWindowSizeKeepingTopLeft(hasCachedWidth ? cachedWidth : 0f, AddHeight);
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

        if (recipe != null && RecipeAvailabilityStore.IsAvailable(recipe)) {
            foreach (var output in recipe.OutputMain) {
                if (mainCount >= MaxMainSlots) break;
                var pInfo = products.Find(p => p.itemId == output.OutputID && p.isMainOutput);
                float ratio = recipeSuccessRatio * output.SuccessRatio;
                FillSlot(mainSlots[mainCount], output, pInfo?.count ?? 0,
                    ratio, output.ShowSuccessRatio || showCompleteRecipeInformation,
                    ProductSlotKind.Main);
                mainSuccessSum += ratio;
                mainCount++;
            }
            foreach (var output in recipe.OutputAppend) {
                if (sideCount >= MaxSideSlots) break;
                var pInfo = products.Find(p => p.itemId == output.OutputID && !p.isMainOutput);
                float ratio = recipeSuccessRatio * output.SuccessRatio;
                FillSlot(sideSlots[sideCount], output, pInfo?.count ?? 0,
                    ratio, output.ShowSuccessRatio || showCompleteRecipeInformation,
                    ProductSlotKind.Side);
                sideCount++;
            }
        }

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
            _fluidArrowText.text = "流动输出".Translate();
            _fluidArrowText.color = ProbColor;
        }
        if (modWindow.oriProductBox != null) modWindow.oriProductBox.SetActive(false);

        // 流体输出右侧信息
        if (fluidRightText != null) {
            fluidRightText.gameObject.SetActive(true);
            if (recipe == null) {
                fluidRightText.text =
                    $"<color=#{ColorUtility.ToHtmlStringRGBA(DestroyColor)}>{"配方不存在".Translate()}</color>";
            } else if (!RecipeAvailabilityStore.IsAvailable(recipe)) {
                fluidRightText.text =
                    $"<color=#{ColorUtility.ToHtmlStringRGBA(DestroyColor)}>{"配方未解锁".Translate()}</color>";
            } else {
                bool hasDestroy = destroyRatio > 0f;
                string destroyStr = hasDestroy ? destroyRatio.FormatP() : "";
                fluidRightText.text = hasDestroy
                    ? $"<color=#{ColorUtility.ToHtmlStringRGBA(DestroyColor)}>{destroyStr}</color>"
                    : "";
            }
        }

        // 动态布局： 无副产物时流动输出上移， 窗口高度缩小
        bool hasSideProducts = sideCount > 0;
        float fluidY = hasSideProducts ? FluidY : SideY;
        float actualAddHeight = hasSideProducts ? AddHeight : AddHeight - 60f;

        int visibleSlotCount = Mathf.Max(mainCount, sideCount);
        float targetAddWidth = Mathf.Max(0, visibleSlotCount - 1) * SlotSpacing;

        if (visibleSlotCount > 0) {
            widthByFractionatorId[fractionatorId] = targetAddWidth;
        } else if (hasCachedWidth) {
            targetAddWidth = cachedWidth;
        }

        ApplyWindowSizeKeepingTopLeft(targetAddWidth, actualAddHeight);
        RefreshLayoutX(fluidY);

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
        slot.kind = ProductSlotKind.Fluid;
        if (slot.button != null) slot.button.data = itemId;
        ItemProto itemProto = LDB.items.Select(itemId);
        if (itemProto != null && slot.icon != null) slot.icon.sprite = itemProto.iconSprite;
        if (slot.countText != null) slot.countText.text = count.ToString();
        if (slot.probText != null) {
            slot.probText.text = ratio.FormatP();
            slot.probText.color = ProbColor;
        }
    }

    private static void FillSlot(ProductSlot slot, OutputInfo output, int count, float ratio, bool showRatio,
        ProductSlotKind kind) {
        slot.go.SetActive(true);
        slot.kind = kind;
        if (slot.button != null) slot.button.data = output.OutputID;
        ItemProto itemProto = LDB.items.Select(output.OutputID);
        if (itemProto != null && slot.icon != null) slot.icon.sprite = itemProto.iconSprite;
        if (slot.countText != null) slot.countText.text = count.ToString();
        if (slot.probText != null) {
            slot.probText.text = showRatio ? ratio.FormatP() : "???";
            slot.probText.color = ProbColor;
        }
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

    #endregion
}
