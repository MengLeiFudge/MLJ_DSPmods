using System;
using System.Collections.Generic;
using System.Linq;
using CommonAPI.Systems;
using FE.Compatibility.Nebula;
using FE.Logic.Fractionation.Recipes;
using FE.UI.Foundation;
using FE.UI.MainPanel.Theme;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static FE.FractionateEverything;
using static FE.Logic.Fractionation.Fractionators.ProcessManager;

namespace FE.Logic.Fractionation.Fractionators.Presentation;

/// <summary>高级塔窗口：左侧配方、食物与速率；右侧左上流动输入、右上流动输出、下方产物，并用两组三箭头连接。</summary>
public static partial class FractionatorWindow {
    private const float AdvancedWindowWidth = 1104f;
    private const float AdvancedPadding = 24f;
    private const float AdvancedTopOffset = 64f;
    private const float AdvancedInfoWidth = 170f;
    private const float AdvancedGap = 20f;
    /// <summary>输入与流出之间、输入与产物之间留给三箭头的间距。</summary>
    private const float AdvancedArrowGap = 56f;
    private const float AdvancedRowHeight = 132f;
    private const float AdvancedSectionHeader = 32f;
    /// <summary>输入与流出可见网格固定为 2 行×4 列；超出的残料在区域内滚动，不改变窗口尺寸。</summary>
    private const int AdvancedGridColumns = 4;
    private const int AdvancedGridRows = 2;
    private const float AdvancedTopViewport = AdvancedRowHeight * AdvancedGridRows + 8f;
    private const float AdvancedProductViewport = AdvancedRowHeight + 8f;
    private const float AdvancedProductTop = AdvancedSectionHeader + AdvancedTopViewport + AdvancedArrowGap;
    /// <summary>内容区固定高度，任何配方、库存与状态下都不变，避免窗口伸缩闪烁。</summary>
    private const float AdvancedRootHeight = AdvancedProductTop + AdvancedSectionHeader + AdvancedProductViewport;
    private static RectTransform advancedRoot;
    private static UIFractionatorWindow advancedSlotTemplate;
    private static ProductSlot advancedRecipeSlot;
    /// <summary>备用与正在消化的同种矩阵合用的食物格。</summary>
    private static ProductSlot advancedFoodSlot;
    /// <summary>食物格上的燃烧室式遮罩，覆盖当前一件已消耗的比例。</summary>
    private static RectTransform digestionMask;
    private static Text advancedFoodTitle;
    private static Text advancedDiscardHint;
    private static Image[] advancedFlowArrows;
    private static Image[] advancedProductArrows;
    private static AdvancedFractionatorState digestionHoldState;
    private static long digestionHoldVersion;
    private static float digestionHoldStart;
    /// <summary>本次按下食物格时是否有备用；只有有备用的按下才取出，空备用的按下才可长按销毁。</summary>
    private static bool foodPressHadReserve;
    private static Text advancedRecipeText;
    private static Text advancedSpeedText;
    private static Text advancedFoodText;
    private static Text advancedRatioText;
    private static AdvancedSection advancedInputs;
    private static AdvancedSection advancedProducts;
    private static AdvancedSection advancedFlow;
    private static PlanetFactory displayedFactory;
    private static int displayedFractionatorId;
    private static Action<RecipeProto> advancedPickerCallback;

    /// <summary>独立缓存区域的固定尺寸、槽位网格和裁剪视口；只保存界面引用，不保存业务库存。</summary>
    private sealed class AdvancedSection {
        internal RectTransform Root;
        internal RectTransform Content;
        internal ScrollRect Scroll;
        internal Text Title;
        internal float Width;
        internal float Viewport;
        internal int Columns;
        internal ProductSlotKind Kind;
        internal readonly List<ProductSlot> Slots = [];
        internal readonly List<Text> Details = [];
        internal readonly List<int> ItemIds = [];
    }

    /// <summary>一次性建立固定几何布局；之后只刷新内容，不再改变任何区域的位置或尺寸。</summary>
    private static void CreateAdvancedLayout(UIFractionatorWindow window, UIFractionatorWindow vanillaWindow) {
        advancedSlotTemplate = vanillaWindow;
        advancedRoot = new GameObject("fe-advanced-fractionator", typeof(RectTransform)).GetComponent<RectTransform>();
        RectTransformUtils.NormalizeRectWithTopLeft(advancedRoot, AdvancedPadding, AdvancedTopOffset, window.transform);
        float width = Math.Max(AdvancedWindowWidth, rootBaseSize.x + AddWidth) - AdvancedPadding * 2f;
        advancedRoot.sizeDelta = new Vector2(width, AdvancedRootHeight);
        CreateAdvancedLeftColumn();
        float dataLeft = AdvancedInfoWidth + AdvancedGap;
        float dataWidth = width - dataLeft;
        float sectionWidth = (dataWidth - AdvancedArrowGap) / 2f;
        CreateAdvancedRule(AdvancedInfoWidth + AdvancedGap / 2f, 0f, 1f, AdvancedRootHeight);
        advancedInputs = CreateAdvancedSection(dataLeft, 0f, sectionWidth, AdvancedGridColumns,
            AdvancedTopViewport, ProductSlotKind.Fluid);
        advancedFlow = CreateAdvancedSection(dataLeft + sectionWidth + AdvancedArrowGap, 0f, sectionWidth,
            AdvancedGridColumns, AdvancedTopViewport, ProductSlotKind.Side);
        advancedProducts = CreateAdvancedSection(dataLeft, AdvancedProductTop, dataWidth, AdvancedGridColumns * 2,
            AdvancedProductViewport, ProductSlotKind.Main);
        // 右向箭头：输入 → 流动输出；下向箭头：输入 → 产物。沿用原版三箭头样式。
        GameObject vanillaArrowParent = vanillaWindow.speedArrows[0].transform.parent.gameObject;
        float topCenterY = AdvancedSectionHeader + AdvancedTopViewport / 2f;
        advancedFlowArrows = CreateAdvancedArrows(vanillaArrowParent, vanillaWindow, "advanced-flow-arrows",
            dataLeft + sectionWidth + AdvancedArrowGap / 2f, topCenterY, 0f);
        advancedProductArrows = CreateAdvancedArrows(vanillaArrowParent, vanillaWindow, "advanced-product-arrows",
            dataLeft + sectionWidth / 2f, AdvancedSectionHeader + AdvancedTopViewport + AdvancedArrowGap / 2f, -90f);
        advancedRoot.gameObject.SetActive(false);
    }

    /// <summary>左栏从上到下：配方、供养食物单格、速率与投产比例。</summary>
    private static void CreateAdvancedLeftColumn() {
        const float slotCenter = 28f;
        const float textLeft = 64f;
        const float textWidth = AdvancedInfoWidth - textLeft;
        advancedRecipeSlot = CreateAdvancedSlot(advancedRoot, slotCenter, 30f);
        advancedRecipeSlot.button.onClick += _ => OpenAdvancedPicker();
        advancedRecipeSlot.button.onRightClick += _ => ClearAdvancedRecipe();
        advancedRecipeText = CreateAdvancedLabel(advancedRoot, textLeft, 6f, textWidth, 48f);
        Text pickerHint = CreateAdvancedLabel(advancedRoot, 0f, 62f, AdvancedInfoWidth, 20f);
        pickerHint.text = "点击选择配方（右键清空）".Translate();
        pickerHint.fontSize = 11;
        pickerHint.color = AdvancedMutedColor;
        CreateAdvancedRule(0f, 90f, AdvancedInfoWidth, 1f);
        advancedFoodTitle = CreateAdvancedLabel(advancedRoot, 0f, 98f, AdvancedInfoWidth, 20f);
        advancedFoodTitle.text = "FE.Advanced.FoodSlot".Translate();
        advancedFoodSlot = CreateAdvancedSlot(advancedRoot, slotCenter, 148f);
        advancedFoodSlot.button.onClick += TakeAdvancedFood;
        CreateDigestionMask(advancedFoodSlot);
        advancedFoodText = CreateAdvancedLabel(advancedRoot, textLeft, 124f, textWidth, 34f);
        advancedFoodText.fontSize = 12;
        advancedDiscardHint = CreateAdvancedLabel(advancedRoot, textLeft, 160f, textWidth, 18f);
        advancedDiscardHint.text = "FE.DiscardDigestion".Translate();
        advancedDiscardHint.fontSize = 11;
        advancedDiscardHint.color = AdvancedMutedColor;
        AttachFoodPressHandlers(advancedFoodSlot);
        CreateAdvancedRule(0f, 190f, AdvancedInfoWidth, 1f);
        CreateAdvancedLabel(advancedRoot, 0f, 198f, AdvancedInfoWidth, 22f).text = "FE.Advanced.RecipeRate".Translate();
        advancedSpeedText = CreateAdvancedLabel(advancedRoot, 0f, 224f, AdvancedInfoWidth, 100f);
        advancedSpeedText.fontSize = 13;
        advancedRatioText = CreateAdvancedLabel(advancedRoot, 0f, 334f, AdvancedInfoWidth, AdvancedRootHeight - 334f);
        advancedRatioText.fontSize = 12;
    }

    private static readonly Color AdvancedMutedColor = new(0.72f, 0.78f, 0.82f, 0.62f);
    private static readonly Color AdvancedRuleColor = new(0.62f, 0.78f, 0.9f, 0.16f);
    private static readonly Color AdvancedPanelColor = new(0f, 0f, 0f, 0.2f);
    private static readonly Color AdvancedCellColor = new(1f, 1f, 1f, 0.035f);
    /// <summary>燃烧室遮罩颜色：覆盖已消耗的部分。</summary>
    private static readonly Color DigestionMaskColor = new(0f, 0f, 0f, 0.62f);

    /// <summary>建立固定尺寸的库存区域：底板、标题、可见网格底格与裁剪滚动区。</summary>
    private static AdvancedSection CreateAdvancedSection(float left, float top, float width, int columns,
        float viewport, ProductSlotKind kind) {
        RectTransform root = new GameObject($"advanced-{kind}", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        RectTransformUtils.NormalizeRectWithTopLeft(root, left, top, advancedRoot);
        root.sizeDelta = new Vector2(width, AdvancedSectionHeader + viewport);
        Image panel = root.GetComponent<Image>();
        panel.color = AdvancedPanelColor;
        panel.raycastTarget = false;
        RectTransform content = PageLayout.CreateScrollableArea(root, "slots", 8f, AdvancedSectionHeader,
            width - 16f, viewport, viewport);
        float cellWidth = (width - 16f - 13f) / columns;
        int visibleRows = Math.Max(1, (int)(viewport / AdvancedRowHeight));
        for (int i = 0; i < columns * visibleRows; i++) {
            Image cell = new GameObject("cell", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            RectTransformUtils.NormalizeRectWithTopLeft(cell, i % columns * cellWidth + 3f,
                i / columns * AdvancedRowHeight + 3f, content).sizeDelta = new Vector2(cellWidth - 6f, AdvancedRowHeight - 6f);
            cell.color = AdvancedCellColor;
            cell.raycastTarget = false;
        }
        Text title = CreateAdvancedLabel(root, 10f, 6f, width - 20f, 24f);
        title.fontSize = 14;
        return new AdvancedSection {
            Root = root, Content = content, Scroll = content.parent.parent.GetComponent<ScrollRect>(),
            Title = title, Width = width, Viewport = viewport, Columns = columns, Kind = kind,
        };
    }

    /// <summary>细分隔线，只做视觉分区，不接收点击。</summary>
    private static void CreateAdvancedRule(float left, float top, float width, float height) {
        Image rule = new GameObject("rule", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        RectTransformUtils.NormalizeRectWithTopLeft(rule, left, top, advancedRoot).sizeDelta = new Vector2(width, height);
        rule.color = AdvancedRuleColor;
        rule.raycastTarget = false;
    }

    /// <summary>复制原版三箭头并按中心点放置；旋转 -90° 即为向下。原版箭头上的概率文字隐藏。</summary>
    private static Image[] CreateAdvancedArrows(GameObject vanillaArrowParent, UIFractionatorWindow vanillaWindow,
        string name, float centerX, float centerY, float rotation) {
        GameObject clone = UnityEngine.Object.Instantiate(vanillaArrowParent, advancedRoot);
        clone.name = name;
        if (clone.transform is RectTransform rect) {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(centerX, -centerY);
        }
        clone.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        foreach (Text text in clone.GetComponentsInChildren<Text>(true)) { text.gameObject.SetActive(false); }
        Image[] arrows = CloneArrowImagesOrdered(vanillaArrowParent, clone, vanillaWindow);
        foreach (Image arrow in arrows) {
            if (arrow == null) { continue; }
            arrow.gameObject.SetActive(true);
            arrow.raycastTarget = false;
        }
        clone.SetActive(true);
        return arrows;
    }

    /// <summary>在食物图标上叠加从顶部向下覆盖的遮罩，表示当前一件已消耗的比例。</summary>
    private static void CreateDigestionMask(ProductSlot slot) {
        Transform parent = slot.icon != null ? slot.icon.transform : slot.go.transform;
        Image mask = new GameObject("digestion-mask", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        digestionMask = mask.GetComponent<RectTransform>();
        digestionMask.SetParent(parent, false);
        digestionMask.pivot = new Vector2(0.5f, 1f);
        digestionMask.offsetMin = digestionMask.offsetMax = Vector2.zero;
        SetDigestionMask(0f);
        mask.color = DigestionMaskColor;
        mask.raycastTarget = false;
    }

    /// <param name="consumed">当前一件已消耗比例，0 为未点燃或刚点燃，1 为即将耗尽。</param>
    private static void SetDigestionMask(float consumed) {
        if (digestionMask == null) { return; }
        consumed = Mathf.Clamp01(consumed);
        digestionMask.anchorMin = new Vector2(0f, 1f - consumed);
        digestionMask.anchorMax = Vector2.one;
        digestionMask.gameObject.SetActive(consumed > 0.001f);
    }

    /// <summary>按下时记录是否有备用：有备用只取出，空备用才开始长按销毁。</summary>
    private static void AttachFoodPressHandlers(ProductSlot slot) {
        EventTrigger trigger = slot.go.AddComponent<EventTrigger>();
        EventTrigger.Entry down = new() { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener(data => {
            if (data is PointerEventData pointer && pointer.button == PointerEventData.InputButton.Left) { OnFoodPointerDown(); }
        });
        trigger.triggers.Add(down);
        foreach (EventTriggerType type in new[] { EventTriggerType.PointerUp, EventTriggerType.PointerExit }) {
            EventTrigger.Entry cancel = new() { eventID = type };
            cancel.callback.AddListener(_ => CancelDigestionDiscard());
            trigger.triggers.Add(cancel);
        }
    }

    private static int CountReserveFood(AdvancedFractionatorState state) {
        int total = 0;
        foreach (var pair in state.Foods) { if (pair.Value > 0) { total += pair.Value; } }
        return total;
    }

    private static Text CreateAdvancedLabel(RectTransform parent, float left, float top, float width, float height) {
        Text label = CreateTopLabel(modWindow, modWindow.stateText, string.Empty, left, top, width);
        RectTransform rect = RectTransformUtils.NormalizeRectWithTopLeft(label, left, top, parent);
        rect.sizeDelta = new Vector2(width, height);
        label.alignment = TextAnchor.UpperLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.color = ProbColor;
        label.raycastTarget = false;
        label.material = null;
        label.enabled = true;
        label.gameObject.SetActive(true);
        return label;
    }

    private static ProductSlot CreateAdvancedSlot(RectTransform parent, float centerX, float centerY) {
        ProductSlot slot = CreateSlot(modWindow, advancedSlotTemplate, Vector3.zero);
        RectTransform rect = slot.go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(centerX, -centerY);
        slot.button.onClick -= advancedSlotTemplate.OnProductUIButtonClick;
        slot.button.tips.type = UIButton.ItemTipType.Item;
        if (slot.button.tips.corner == 0) { slot.button.tips.corner = 8; }
        foreach (Text text in slot.go.GetComponentsInChildren<Text>(true)) { text.material = null; }
        return slot;
    }

    private static void UpdateAdvancedControls(UIFractionatorWindow src, int buildingId, AdvancedFractionatorState state) {
        bool visible = FractionatorTowerCatalog.IsAdvancedTower(buildingId) && state != null;
        advancedRoot.gameObject.SetActive(visible);
        if (!visible || !ReferenceEquals(displayedFactory, src.factory) || displayedFractionatorId != src.fractionatorId) {
            CancelAdvancedPicker();
            PageLayout.ScrollToTop(advancedInputs.Content);
            PageLayout.ScrollToTop(advancedProducts.Content);
            PageLayout.ScrollToTop(advancedFlow.Content);
        }
        displayedFactory = visible ? src.factory : null;
        displayedFractionatorId = visible ? src.fractionatorId : 0;
    }

    private static void OpenAdvancedPicker() {
        UIFractionatorWindow window = sourceWindow;
        if (window?.factory == null || window.fractionatorId <= 0) { return; }
        CancelAdvancedPicker();
        PlanetFactory factory = window.factory;
        int id = window.fractionatorId;
        FractionatorComponent fractionator = factory.factorySystem.fractionatorPool[id];
        int entityId = fractionator.entityId;
        int buildingId = factory.entityPool[entityId].protoId;
        ERecipe type = FractionatorTowerCatalog.GetRecipeType(buildingId);
        AdvancedRecipeCatalog.SyncUnlocks();
        advancedPickerCallback = proto => {
            advancedPickerCallback = null;
            if (proto == null || sourceWindow == null || !sourceWindow.active
                || !ReferenceEquals(sourceWindow.factory, factory) || sourceWindow.fractionatorId != id
                || id >= factory.factorySystem.fractionatorPool.Length) { return; }
            FractionatorComponent current = factory.factorySystem.fractionatorPool[id];
            if (current.id != id || current.entityId != entityId || factory.entityPool[entityId].protoId != buildingId) { return; }
            WorldviewRecipe selected = AdvancedRecipeCatalog.GetRecipe(proto.ID);
            if (selected?.RecipeType != type) { return; }
            AdvancedRecipeSelectionSync.SetSelectedRecipeAndSync(factory, entityId, selected.PersistentId, manual: true);
        };
        UIRecipePickerExtension.Popup(modRootRect.anchoredPosition + new Vector2(-300f, -135f),
            advancedPickerCallback, proto => AdvancedRecipeCatalog.GetRecipe(proto.ID)?.RecipeType == type);
        UIRecipePicker picker = UIRoot.instance?.uiGame?.recipePicker;
        if (picker != null && picker.active && picker.onReturn == advancedPickerCallback) {
            picker.OnTypeButtonClick(tab分馏);
        }
    }

    private static void ClearAdvancedRecipe() {
        UIFractionatorWindow window = sourceWindow;
        if (window?.factory == null || window.fractionatorId <= 0) { return; }
        CancelAdvancedPicker();
        FractionatorComponent fractionator = window.factorySystem.fractionatorPool[window.fractionatorId];
        if (fractionator.id == window.fractionatorId) {
            AdvancedRecipeSelectionSync.SetSelectedRecipeAndSync(window.factory, fractionator.entityId, 0, manual: true);
        }
    }

    /// <summary>只关闭本窗口打开的选择器，取消时先移除回调，避免建筑切换后写入旧目标。</summary>
    private static void CancelAdvancedPicker() {
        UIRecipePicker picker = UIRoot.instance?.uiGame?.recipePicker;
        if (advancedPickerCallback != null && picker != null && picker.onReturn == advancedPickerCallback) {
            picker.onReturn = null;
            UIRecipePicker.Close();
        }
        advancedPickerCallback = null;
        CancelDigestionDiscard();
    }

    /// <summary>空手从真实输入取物，手持同物品时补料；操作只针对仍在显示的主机实例。</summary>
    private static void OnAdvancedInputClick(int itemId) {
        UIFractionatorWindow window = sourceWindow;
        if (window?.factory == null || window.player == null || !window.active || itemId <= 0
            || window.factory != displayedFactory || window.fractionatorId != displayedFractionatorId) { return; }
        if (NebulaMultiplayerModAPI.IsClient) {
            UIRealtimeTip.Popup("高级塔客户端手动取出受限".Translate());
            return;
        }
        Player player = window.player;
        if (player.inhandItemCount <= 0) { player.SetHandItems(0, 0); }
        if (player.inhandItemCount > 0) {
            if (player.inhandItemId != itemId) { return; }
            int added = AdvancedFractionatorRuntime.AddInput(window.factory, window.fractionatorId, itemId,
                player.inhandItemCount, player.inhandItemInc, out int inc);
            if (added <= 0) {
                UIRealtimeTip.Popup("栏位已满".Translate());
                return;
            }
            int remaining = player.inhandItemCount - added;
            player.SetHandItems(remaining > 0 ? itemId : 0, remaining, remaining > 0 ? player.inhandItemInc - inc : 0);
            return;
        }
        int taken = AdvancedFractionatorRuntime.TakeInput(window.factory, window.fractionatorId, itemId, int.MaxValue, out int takenInc);
        TransferAdvancedItems(player, itemId, taken, takenInc);
    }

    /// <summary>食物格点击只取出备用；按下时备用为空的这次点击属于长按销毁流程，不取物。</summary>
    private static void TakeAdvancedFood(int itemId) {
        if (!foodPressHadReserve) { return; }
        UIFractionatorWindow window = sourceWindow;
        if (window?.factory == null || window.player == null || !window.active
            || window.factory != displayedFactory || window.fractionatorId != displayedFractionatorId) { return; }
        if (NebulaMultiplayerModAPI.IsClient) {
            UIRealtimeTip.Popup("高级塔客户端手动取出受限".Translate());
            return;
        }
        Player player = window.player;
        if (player.inhandItemId > 0 || player.inhandItemCount > 0) { return; }
        int taken = AdvancedFractionatorRuntime.TakeFood(window.factory, window.fractionatorId, itemId, out int inc);
        TransferAdvancedItems(player, itemId, taken, inc);
    }

    /// <summary>快速转移沿用背包、亚空间及丢出路径；普通点击保持原增产点到手持。</summary>
    private static void TransferAdvancedItems(Player player, int itemId, int count, int inc) {
        if (count <= 0) { return; }
        if (VFInput.control || VFInput.shift) {
            int added = player.TryAddItemToPackage(itemId, count, inc, throwTrash: true);
            if (added > 0) { UIItemup.Up(itemId, added); }
        } else {
            player.SetHandItems(itemId, count, inc);
        }
    }

    /// <summary>食物格按下：有备用时本次按下只用于取出；空备用且正在消化时才开始一秒长按销毁。</summary>
    private static void OnFoodPointerDown() {
        CancelDigestionDiscard();
        foodPressHadReserve = true;
        if (NebulaMultiplayerModAPI.IsClient) { return; }
        if (sourceWindow?.active != true || sourceWindow.factory != displayedFactory
            || sourceWindow.fractionatorId != displayedFractionatorId) { return; }
        AdvancedFractionatorState state = AdvancedFractionatorRuntime.GetState(displayedFactory, displayedFractionatorId);
        if (state == null) { return; }
        lock (state) {
            foodPressHadReserve = CountReserveFood(state) > 0;
            if (foodPressHadReserve || state.DigestingEnergy <= 0d) { return; }
            digestionHoldState = state;
            digestionHoldVersion = state.DigestionVersion;
            digestionHoldStart = Time.unscaledTime;
        }
    }

    private static void CancelDigestionDiscard() => digestionHoldState = null;

    /// <summary>由可见窗口更新驱动；松键、失焦、换件、补入备用或实例失效均取消，不重复销毁后续食物。</summary>
    private static void UpdateDigestionDiscard(AdvancedFractionatorState state) {
        if (digestionHoldState == null) { return; }
        if (!Input.GetMouseButton(0) || !Application.isFocused || !ReferenceEquals(state, digestionHoldState)
            || state.DigestionVersion != digestionHoldVersion || sourceWindow?.active != true
            || CountReserveFood(state) > 0 || state.DigestingEnergy <= 0d) {
            CancelDigestionDiscard();
            return;
        }
        if (Time.unscaledTime - digestionHoldStart < 1f) { return; }
        AdvancedFractionatorRuntime.DiscardDigestion(displayedFactory, displayedFractionatorId, state, digestionHoldVersion);
        CancelDigestionDiscard();
    }

    private static void UpdateAdvancedUI(UIFractionatorWindow src, FractionatorComponent fractionator,
        WorldviewRecipe recipe, AdvancedFractionatorState state) {
        foreach (ProductSlot slot in mainSlots) { slot?.go.SetActive(false); }
        foreach (ProductSlot slot in sideSlots) { slot?.go.SetActive(false); }
        fluidSlot?.go.SetActive(false);
        fluidRightText?.gameObject.SetActive(false);
        _mainArrowText?.gameObject.SetActive(false);
        _sideArrowText?.gameObject.SetActive(false);
        _fluidArrowText?.gameObject.SetActive(false);
        SetArrowGroup(_mainArrows, false, modWindow.marqueeOffColor);
        SetArrowGroup(_sideArrows, false, modWindow.marqueeOffColor);
        SetArrowGroup(_fluidArrows, false, modWindow.marqueeOffColor);
        modWindow.needIcon.enabled = false;
        modWindow.needCountText.enabled = false;
        modWindow.inputTitleText.enabled = false;
        modWindow.speedText.enabled = false;
        modWindow.remindText.enabled = false;
        foreach (Image arrow in modWindow.needIncs) { arrow.enabled = false; }
        lock (state) {
            advancedInputs.ItemIds.Clear();
            advancedProducts.ItemIds.Clear();
            advancedFlow.ItemIds.Clear();
            if (recipe != null) {
                foreach (WorldviewRecipeInput input in recipe.InputArray) {
                    advancedInputs.ItemIds.Add(input.ItemId);
                    advancedFlow.ItemIds.Add(input.ItemId);
                }
                foreach (WorldviewRecipeOutput output in recipe.ProductArray) { advancedProducts.ItemIds.Add(output.ItemId); }
            }
            AppendStoredIds(advancedInputs.ItemIds, state.Inputs);
            AppendStoredIds(advancedProducts.ItemIds, state.Products);
            AppendStoredIds(advancedFlow.ItemIds, state.FlowOutputs);
            // 窗口尺寸恒定；尺寸未变时 ApplyWindowSizeKeepingTopLeft 直接返回。
            ApplyWindowSizeKeepingTopLeft(advancedRoot.sizeDelta.x + AdvancedPadding * 2f - rootBaseSize.x,
                AdvancedTopOffset + AdvancedRootHeight + AdvancedPadding - rootBaseSize.y);
            int buildingId = src.factory.entityPool[fractionator.entityId].protoId;
            UpdateAdvancedSection(advancedInputs, state, recipe, buildingId);
            UpdateAdvancedSection(advancedFlow, state, recipe, buildingId);
            UpdateAdvancedSection(advancedProducts, state, recipe, buildingId);
            bool working = state.ProcessingRate > 0d;
            Color arrowColor = working ? modWindow.marqueeOnColor : modWindow.marqueeOffColor;
            SetArrowGroup(advancedFlowArrows, true, arrowColor);
            SetArrowGroup(advancedProductArrows, true, arrowColor);
            UpdateAdvancedFood(state, recipe, buildingId);
            RecipeProto proto = LDB.recipes.Select(AdvancedRecipeCatalog.GetProtoId(state.SelectedRecipeId));
            advancedRecipeSlot.go.SetActive(true);
            advancedRecipeSlot.icon.sprite = proto?.iconSprite ?? UIRoot.instance?.uiGame?.labWindow?.defaultTechIcon;
            advancedRecipeSlot.icon.enabled = advancedRecipeSlot.icon.sprite != null;
            advancedRecipeSlot.countText.text = string.Empty;
            advancedRecipeSlot.probText.text = string.Empty;
            advancedRecipeSlot.button.tips.itemId = proto == null ? 0 : -proto.ID;
            advancedRecipeSlot.button.tips.type = UIButton.ItemTipType.Recipe;
            foreach (Image arrow in advancedRecipeSlot.incArrows) { if (arrow != null) { arrow.enabled = false; } }
            advancedRecipeText.text = recipe?.DisplayName ?? "未选择配方".Translate();
            double rounds = state.ProcessingRate * 60d;
            advancedSpeedText.text = string.Format("FE.Advanced.RateSummary".Translate(), rounds,
                fractionator.produceProb, AdvancedFractionatorRuntime.GetFoodSpeed(state), rounds * fractionator.produceProb);
            advancedRatioText.text = recipe == null ? string.Empty : string.Format("FE.Advanced.RecipeRatio".Translate(),
                string.Join(" : ", recipe.InputArray.Select(input => input.Count.ToString())),
                string.Join(" : ", recipe.ProductArray.Select(output => AdvancedFractionatorRuntime.GetOutputPerSuccess(recipe, output).ToString("0.##"))));
        }
    }

    /// <summary>食物单格：图标取正在消化的矩阵（无则取备用），右下角只计与图标同种的备用，遮罩表示当前一件已消耗比例。</summary>
    /// <remarks>旧档可能残留与消化物异种、待后端排出的备用；它不计入格内数字，但仍阻止长按销毁，与后端空备用校验一致。</remarks>
    private static void UpdateAdvancedFood(AdvancedFractionatorState state, WorldviewRecipe recipe, int buildingId) {
        bool digesting = state.DigestingEnergy > 0d;
        int shownId = digesting ? state.DigestingItemId : 0;
        if (!digesting) {
            foreach (var pair in state.Foods) {
                if (pair.Value > 0) { shownId = pair.Key; break; }
            }
        }
        int reserveCount = shownId > 0 && state.Foods.TryGetValue(shownId, out int count) ? Math.Max(0, count) : 0;
        int reserveInc = reserveCount > 0 && state.FoodInc.TryGetValue(shownId, out int inc) ? inc : 0;
        int totalReserve = CountReserveFood(state);
        // button.data 由此设为 shownId，点击取物与格内数字是同一种矩阵。
        RenderAdvancedSlot(advancedFoodSlot, shownId, reserveCount, reserveInc, ProductSlotKind.Fluid);
        advancedFoodSlot.countText.text = reserveCount > 0 ? reserveCount.ToString() : string.Empty;
        advancedFoodSlot.probText.text = string.Empty;
        advancedFoodSlot.button.tips.itemId = shownId;
        advancedFoodSlot.button.tips.itemCount = reserveCount > 0 ? reserveCount : digesting ? 1 : 0;
        SetDigestionMask(digesting ? 1f - AdvancedFractionatorRuntime.GetDigestionProgress(state) : 0f);
        var foodRates = AdvancedFractionatorRuntime.GetFoodRates(state, recipe, buildingId);
        advancedFoodText.text = digesting
            ? string.Format("FE.Advanced.FoodRates".Translate(), foodRates.ConsumptionPerMinute, foodRates.Share)
            : string.Empty;
        advancedDiscardHint.gameObject.SetActive(digesting && totalReserve <= 0 && !NebulaMultiplayerModAPI.IsClient);
        UpdateDigestionDiscard(state);
    }

    /// <summary>原版没有高级塔类型名称，展示配方以所属塔名替代“未知”制造来源。</summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RecipeProto), nameof(RecipeProto.madeFromString), MethodType.Getter)]
    private static void RecipeProto_MadeFromString_Postfix(RecipeProto __instance, ref string __result) {
        WorldviewRecipe recipe = AdvancedRecipeCatalog.GetRecipe(__instance.ID);
        if (recipe == null) { return; }
        __result = (recipe.RecipeType switch {
            ERecipe.AdvancedMineral => "高级矿物塔",
            ERecipe.AdvancedInteraction => "培养塔",
            _ => "转化塔",
        }).Translate();
    }

    private static void AppendStoredIds(List<int> ids, Dictionary<int, int> counts) {
        foreach (var pair in counts) {
            if (pair.Value > 0 && !ids.Contains(pair.Key)) { ids.Add(pair.Key); }
        }
    }

    /// <summary>库存区域高度按实际行数计算，空区域仍保留一行。</summary>
    private static float GetSectionContentHeight(AdvancedSection section) =>
        Math.Max(1, (section.ItemIds.Count + section.Columns - 1) / section.Columns) * AdvancedRowHeight + 8f;

    /// <summary>在区域内部排列槽位和四行信息，读真实库存及运行器规则。</summary>
    private static void UpdateAdvancedSection(AdvancedSection section, AdvancedFractionatorState state,
        WorldviewRecipe recipe, int buildingId) {
        string title = section.Kind switch { ProductSlotKind.Fluid => "流动输入", ProductSlotKind.Main => "配方产物", _ => "流动输出" };
        section.Title.text = title.Translate();
        // 区域与视口尺寸在创建时固定，这里只调整可滚动内容高度。
        section.Content.sizeDelta = new Vector2(0f, Math.Max(section.Viewport, GetSectionContentHeight(section)));
        // 滚动区两边各8px，右侧还预留13px滚动条；槽位和文字均落在裁剪区内部。
        float cellWidth = (section.Width - 16f - 13f) / section.Columns;
        Dictionary<int, int> counts = section.Kind switch {
            ProductSlotKind.Fluid => state.Inputs, ProductSlotKind.Main => state.Products, _ => state.FlowOutputs,
        };
        Dictionary<int, int> incs = section.Kind switch {
            ProductSlotKind.Fluid => state.InputInc, ProductSlotKind.Side => state.FlowOutputInc, _ => null,
        };
        for (int i = 0; i < section.ItemIds.Count; i++) {
            if (i == section.Slots.Count) {
                float left = i % section.Columns * cellWidth;
                float rowTop = i / section.Columns * AdvancedRowHeight;
                ProductSlot slot = CreateAdvancedSlot(section.Content, left + cellWidth / 2f, rowTop + 29f);
                if (section.Kind == ProductSlotKind.Fluid) { slot.button.onClick += OnAdvancedInputClick; }
                else { slot.button.onClick += itemId => OnSlotClick(slot, itemId); }
                section.Slots.Add(slot);
                Text detail = CreateAdvancedLabel(section.Content, left + 3f, rowTop + 60f, cellWidth - 6f, AdvancedRowHeight - 66f);
                detail.fontSize = 12;
                detail.resizeTextForBestFit = true;
                detail.resizeTextMinSize = 10;
                detail.resizeTextMaxSize = 12;
                detail.alignment = TextAnchor.UpperCenter;
                section.Details.Add(detail);
            }
            int itemId = section.ItemIds[i];
            counts.TryGetValue(itemId, out int count);
            int inc = 0;
            incs?.TryGetValue(itemId, out inc);
            RenderAdvancedSlot(section.Slots[i], itemId, count, inc, section.Kind);
            Text details = section.Details[i];
            details.gameObject.SetActive(true);
            details.text = GetAdvancedSlotDetails(section.Kind, state, recipe, buildingId, itemId, count);
        }
        for (int i = section.ItemIds.Count; i < section.Slots.Count; i++) {
            section.Slots[i].go.SetActive(false);
            section.Details[i].gameObject.SetActive(false);
        }
    }

    /// <summary>每槽数量与概率从业务定义派生，预计速率以当前反应轮数计算。</summary>
    private static string GetAdvancedSlotDetails(ProductSlotKind kind, AdvancedFractionatorState state,
        WorldviewRecipe recipe, int buildingId, int itemId, int count) {
        double rounds = state.ProcessingRate * 60d;
        float success = recipe == null ? 0f : AdvancedFractionatorRuntime.GetSuccessProbability(recipe,
            FE.Logic.Civilization.ResearchManager.GetAdvancedTowerSacrificeBoost(buildingId));
        if (kind == ProductSlotKind.Fluid && recipe != null && recipe.TryGetInput(itemId, out WorldviewRecipeInput input)) {
            int capacity = AdvancedFractionatorRuntime.GetInputCapacity(recipe, itemId);
            float consume = AdvancedFractionatorRuntime.GetInputConsumeProbability(input, buildingId);
            string rule = input.ConsumeProbability < 0.9999f
                ? string.Format("FE.Advanced.CatalystRule".Translate(), input.Count, consume)
                : string.Format("FE.Advanced.InputRule".Translate(), input.Count, success);
            return $"{count}/{capacity}\n{rule}\n"
                + string.Format((input.ConsumeProbability < 0.9999f ? "FE.Advanced.CatalystRates" : "FE.Advanced.InputRates").Translate(),
                    rounds * input.Count, rounds * input.Count * success * consume);
        }
        if (kind == ProductSlotKind.Main && recipe != null) {
            WorldviewRecipeOutput output = recipe.ProductArray.FirstOrDefault(candidate => candidate.ItemId == itemId);
            if (output != null) {
                float perSuccess = AdvancedFractionatorRuntime.GetOutputPerSuccess(recipe, output);
                int capacity = AdvancedFractionatorRuntime.GetProductCapacity(recipe, output);
                return $"{count}/{capacity}\n" + string.Format("FE.Advanced.ProductRule".Translate(), perSuccess, success,
                    rounds * success * perSuccess);
            }
        }
        if (kind == ProductSlotKind.Side && recipe != null && recipe.TryGetInput(itemId, out WorldviewRecipeInput flow)) {
            if (flow.ConsumeProbability >= 0.9999f) {
                return $"{count}/{OutputCapacity}\n" + string.Format("FE.Advanced.FlowRule".Translate(), 1f - success,
                    rounds * flow.Count * (1f - success));
            }
            return $"{count}/{OutputCapacity}\n" + "FE.Advanced.ExcessCatalyst".Translate();
        }
        // 产物与流动输出共用 5×当前集装层数的准入阈值。
        int fallbackCapacity = kind == ProductSlotKind.Fluid ? AdvancedFractionatorRuntime.GetInputCapacity(recipe, itemId) : OutputCapacity;
        return $"{count}/{fallbackCapacity}\n" + (kind == ProductSlotKind.Main ? "FE.Advanced.RemainingProduct" : "FE.Advanced.PassThrough").Translate();
    }

    private static void RenderAdvancedSlot(ProductSlot slot, int itemId, int count, int inc, ProductSlotKind kind) {
        FillAdvancedSlot(slot, itemId, count, string.Empty, kind);
        slot.icon.sprite = LDB.items.Select(itemId)?.iconSprite;
        slot.icon.enabled = slot.icon.sprite != null;
        slot.button.tips.itemId = itemId;
        slot.button.tips.itemCount = count;
        slot.button.tips.itemInc = inc;
        int level = count > 0 ? Cargo.fastIncArrowTable[Math.Min(10, Math.Max(0, inc / count))] : 0;
        for (int i = 0; i < slot.incArrows.Length; i++) {
            if (slot.incArrows[i] != null) { slot.incArrows[i].enabled = level >= i + 1; }
        }
    }
}
