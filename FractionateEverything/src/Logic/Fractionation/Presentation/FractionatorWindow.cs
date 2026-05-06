using System;
using System.Collections.Generic;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Fractionation.Recipes;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Fractionation.Recipes.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Presentation;

/// <summary>
/// 模组分馏塔独立窗口。
/// 复制原版窗口实例，在创建时一次性修改布局，后续只需控制显示/隐藏。
/// 原版分馏塔直接使用原版窗口，不做任何修改。
/// </summary>
public static partial class FractionatorWindow {
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
    private static Text lockStateText;
    private static Text lockHintText;

    // 一次性记录的原版元素 localPosition
    private static Vector3 _itemBoxLocalPos;
    private static Vector3 _oriBoxLocalPos;
    private static Vector3 _speedArrowParentLocalPos;
    private static float _layoutOffsetX;

    private class ProductSlot {
        public GameObject go;
        public Image icon;
        public UIButton button;
        public Text countText;
        public Text probText;
        public Image lockIcon;
        public Image[] incArrows;
        public ProductSlotKind kind;
        public Action<int> clickHandler;
        public Action<int> rightClickHandler;
    }

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
                slot.rightClickHandler = OnSlotRightClick;
                slot.button.onClick += slot.clickHandler;
                slot.button.onRightClick += slot.rightClickHandler;
            }
        }
        foreach (var slot in sideSlots) {
            if (slot?.button != null) {
                slot.clickHandler = itemId => OnSlotClick(slot, itemId);
                slot.rightClickHandler = OnSlotRightClick;
                slot.button.onClick += slot.clickHandler;
                slot.button.onRightClick += slot.rightClickHandler;
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
            if (slot?.button != null) {
                if (slot.clickHandler != null) {
                    slot.button.onClick -= slot.clickHandler;
                    slot.clickHandler = null;
                }
                if (slot.rightClickHandler != null) {
                    slot.button.onRightClick -= slot.rightClickHandler;
                    slot.rightClickHandler = null;
                }
            }
        }
        foreach (var slot in sideSlots) {
            if (slot?.button != null) {
                if (slot.clickHandler != null) {
                    slot.button.onClick -= slot.clickHandler;
                    slot.clickHandler = null;
                }
                if (slot.rightClickHandler != null) {
                    slot.button.onRightClick -= slot.rightClickHandler;
                    slot.rightClickHandler = null;
                }
            }
        }
        if (fluidSlot?.button != null) {
            if (fluidSlot.clickHandler != null) {
                fluidSlot.button.onClick -= fluidSlot.clickHandler;
                fluidSlot.clickHandler = null;
            }
        }
        slotClickBound = false;
    }

    /// <summary>判断是否为模组分馏塔建筑</summary>
    public static bool IsModFractionator(int fractionatorId, PlanetFactory factory) {
        if (fractionatorId == 0 || factory == null) return false;
        FractionatorComponent frac = factory.factorySystem.fractionatorPool[fractionatorId];
        if (frac.id != fractionatorId) return false;
        int buildingId = factory.entityPool[frac.entityId].protoId;
        return buildingId >= IFE交互塔 && buildingId <= IFE精馏塔;
    }

    private static void OnSlotClick(ProductSlot slot, int itemId) {
        UIFractionatorWindow target = sourceWindow ?? modWindow;
        if (target == null) return;
        HandleProductSlotClick(target, itemId, slot);
    }

}
