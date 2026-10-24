using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FE.Logic.Civilization;
using FE.Logic.DataCenter;
using FE.Logic.Fractionation.Recipes;
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
public static partial class FractionatorWindow {
    #region 状态/初始化

    // ===== 窗口实例 =====
    private static GameObject modWindowGo;

    // ===== 模组窗口组件引用 =====
    private static UIFractionatorWindow modWindow;
    private static UIFractionatorWindow sourceWindow;
    private static RectTransform modRootRect;
    private static RectTransform[] resizableRects;
    private static Vector2[] resizableRectBaseSizes;
    private static Vector2 rootBaseSize;
    private static float currentAddWidth = -1f;

    // 通过路径查找缓存的模组窗口共享组件（比依赖 Instantiate 自动映射更可靠）
    private static Text _modPowerText;
    private static Image _modPowerIcon;

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
        /// 保存产物槽增产点箭头图标数组。
        /// </summary>
        public Image[] incArrows;
        /// <summary>
        /// 保存产物槽显示类型。
        /// </summary>
        public ProductSlotKind kind;
    }

    /// <summary>
    /// 分馏塔产物槽位的主产物、副产物和流体分类。
    /// </summary>
    private enum ProductSlotKind {
        Main,
        Side,
        Fluid,
    }

    private static void ResetWindowInteraction() {
        CancelAdvancedPicker();
        displayedFactory = null;
        displayedFractionatorId = 0;
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
        if (target == null || itemId <= 0) return;
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
        AlignLikeVanillaTopRight(_modPowerText.rectTransform, vanillaWindow.powerText.rectTransform, vanillaWindow);
        AlignLikeVanillaTopRight(_modPowerIcon.rectTransform, vanillaWindow.powerIcon.rectTransform, vanillaWindow);
        AlignLikeVanillaTopRight(modWindow.stateText.rectTransform, vanillaWindow.stateText.rectTransform, vanillaWindow);
    }

    /// <summary>复制原版控件相对窗口右上角的偏移，窗口扩展不改变状态区边距与文字样式。</summary>
    private static void AlignLikeVanillaTopRight(RectTransform target, RectTransform source, UIFractionatorWindow vanillaWindow) {
        RectTransform root = vanillaWindow.GetComponent<RectTransform>();
        Vector3 position = root.InverseTransformPoint(source.position);
        target.SetParent(modWindow.transform, false);
        target.anchorMin = target.anchorMax = Vector2.one;
        target.pivot = source.pivot;
        target.sizeDelta = source.rect.size;
        target.localScale = source.localScale;
        target.localRotation = source.localRotation;
        target.anchoredPosition = new Vector2(position.x - root.rect.xMax, position.y - root.rect.yMax);
    }

    private static void ApplyModLayoutOnce(UIFractionatorWindow window, UIFractionatorWindow vanillaWindow) {
        InitializeWindowResizeContext(window);
        ApplyWindowSizeKeepingTopLeft(WindowWidth - rootBaseSize.x, WindowHeight - rootBaseSize.y);
        window.productProbText.gameObject.SetActive(false);
        window.oriProductProbText.gameObject.SetActive(false);
        foreach (Image image in window.speedArrows) { image.gameObject.SetActive(false); }
        window.productBox.SetActive(false);
        window.oriProductBox.SetActive(false);
        window.sepLine0.gameObject.SetActive(false);
        window.sepLine1.gameObject.SetActive(false);
        window.historyText.gameObject.SetActive(false);
        window.needIcon.gameObject.SetActive(false);
        window.needCountText.gameObject.SetActive(false);
        window.inputTitleText.gameObject.SetActive(false);
        window.speedText.gameObject.SetActive(false);
        window.remindText.gameObject.SetActive(false);
        foreach (Image image in window.needIncs) { image.gameObject.SetActive(false); }
        Transform inputBox = window.needIcon.transform.parent;
        if (inputBox != window.transform && !window.stateText.transform.IsChildOf(inputBox)
            && !window.powerText.transform.IsChildOf(inputBox) && !window.titleText.transform.IsChildOf(inputBox)) {
            inputBox.gameObject.SetActive(false);
        }
        CreateAdvancedLayout(window, vanillaWindow);
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

        currentAddWidth = addWidth;
        currentAddHeight = addHeight;
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

            ResetWindowInteraction();
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
            ResetWindowInteraction();
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
        ResetWindowInteraction();
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
        ResetWindowInteraction();
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
        List<ProductOutputInfo> products = fractionator.GetProducts(__instance.factory);
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
            // 缓存上限随全局堆叠变化，读当前值而不是已放置塔的旧字段。
            int canAdd = BufferCapacity - currentCount;
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
                itemId, removedCount, removedInc, throwTrash: true);
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
            int addedToPackage = player.TryAddItemToPackage(itemId, taken, takenInc, throwTrash: true);
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

    private static void UpdateAdvancedPowerDisplay(UIFractionatorWindow src, AdvancedFractionatorState state) {
        bool fed = state != null && state.DigestingEnergy > 0d;
        AdvancedTowerStatus status = AdvancedFractionatorRuntime.GetProcessingStatus(state);
        string text = (!fed ? "缺少矩阵"
            : status is AdvancedTowerStatus.Running or AdvancedTowerStatus.Stacking ? "FE.Advanced.Feeding" : "待机").Translate();
        src.powerText.text = text;
        src.powerText.color = fed ? src.powerNormalColor : src.powerOffColor;
        src.powerIcon.color = Color.clear;
        if (_modPowerText != null) {
            _modPowerText.text = text;
            _modPowerText.color = src.powerText.color;
        }
        if (_modPowerIcon != null) {
            _modPowerIcon.color = Color.clear;
        }
    }

    private static void UpdateAdvancedStateText(AdvancedFractionatorState state) {
        AdvancedTowerStatus status = AdvancedFractionatorRuntime.GetProcessingStatus(state);
        string text = status switch {
            AdvancedTowerStatus.AwaitingRecipe => "等待配方",
            AdvancedTowerStatus.MissingInput => "缺少原材料",
            AdvancedTowerStatus.FlowOutputBlocked => "FE.Advanced.FlowOutputBlocked",
            AdvancedTowerStatus.ProductOutputBlocked => "FE.Advanced.ProductOutputBlocked",
            AdvancedTowerStatus.Stacking => "运行中 · 等待集装",
            _ => "正常运转",
        };
        modWindow.stateText.text = text.Translate();
        modWindow.stateText.color = status is AdvancedTowerStatus.Running or AdvancedTowerStatus.Stacking
            ? modWindow.workNormalColor
            : status == AdvancedTowerStatus.AwaitingRecipe ? modWindow.idleColor : modWindow.workStoppedColor;
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
        // 与基础塔正面上传的运行条件一致；状态文字和整个输出区域共用此结果。
        bool interactionMode = FractionatorTowerCatalog.IsBasicTower(buildingID)
                               && fractionator.belt0 > 0
                               && !fractionator.isOutput0
                               && fractionator.belt1 <= 0
                               && fractionator.belt2 <= 0
                               && fractionator.GetProducts(src.factory).All(product => product.count <= 0);

        modWindow.titleText.text = building.name;
        UpdateAdvancedControls(src, buildingID, advancedState);

        float consumerRatio;
        if (advanced) {
            consumerRatio = 1f;
        } else {
            PowerConsumerComponent powerConsumer = src.powerSystem.consumerPool[fractionator.pcId];
            int networkId = powerConsumer.networkId;
            PowerNetwork powerNetwork = src.powerSystem.netPool[networkId];
            consumerRatio = powerNetwork != null && networkId > 0 ? (float)powerNetwork.consumerRatio : 0f;
            UpdatePowerDisplay(src, powerConsumer, consumerRatio);
        }

        if (advanced) {
            lock (advancedState) {
                UpdateAdvancedPowerDisplay(src, advancedState);
                WorldviewRecipe advancedRecipe = RecipeManager.GetRecipe(advancedState.SelectedRecipeId);
                UpdateAdvancedStateText(advancedState);
                UpdateAdvancedUI(src, fractionator, advancedRecipe, advancedState);
            }
            return;
        }
        AnalysisOutcome analysis = buildingID == IFE基础解析塔 && !interactionMode
            ? ResearchManager.GetAnalysisOutcome(fractionator.fluidId) : default;
        UpdateModStateText(src, fractionator, building, consumerRatio, analysis, interactionMode);
        BaseRecipe recipe = GetRecipeForBuilding(buildingID, fractionator.fluidId);
        UpdateBasicUI(src, fractionator, recipe, analysis, consumerRatio, interactionMode);
    }

    private static void UpdateModStateText(UIFractionatorWindow src,
        FractionatorComponent fractionator, ItemProto building, float consumerRatio,
        AnalysisOutcome analysis, bool interactionMode) {

        int fluidOutputMax = building.FluidOutputMax();
        int productOutputMax = building.ProductOutputMax();
        List<ProductOutputInfo> products = fractionator.GetProducts(src.factory);
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
        if (building.ID == IFE基础解析塔 && fractionator.fluidId > 0) {
            modWindow.stateText.text = analysis.ModeKey.Translate()
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

    private static void SetArrowGroup(Image[] arrows, bool enabled, Color color) {
        if (arrows == null) return;
        for (int i = 0; i < arrows.Length; i++) {
            Image arrow = arrows[i];
            if (arrow == null) continue;
            arrow.color = color;
            arrow.enabled = enabled;
        }
    }

    #endregion
}
