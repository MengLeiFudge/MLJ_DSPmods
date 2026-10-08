using System;
using System.Collections.Generic;
using CommonAPI.Systems;
using FE.Logic.Fractionation.Recipes;
using FE.UI.Foundation;
using FE.UI.MainPanel.Theme;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using static FE.FractionateEverything;
using static FE.Logic.Fractionation.Fractionators.ProcessManager;

namespace FE.Logic.Fractionation.Fractionators.Presentation;

/// <summary>高级塔配方槽与独立输入、产物、流动输出和营养区域。</summary>
public static partial class FractionatorWindow {
    private const float AdvancedRowHeight = 70f;
    private const float MaximumAdvancedViewportHeight = 390f;
    private static RectTransform advancedRoot;
    private static UIFractionatorWindow advancedSlotTemplate;
    private static ProductSlot advancedRecipeSlot;
    private static ProductSlot advancedFoodSlot;
    private static Text advancedRecipeText;
    private static Text advancedSpeedText;
    private static AdvancedColumn advancedInputs;
    private static AdvancedColumn advancedProducts;
    private static AdvancedColumn advancedFlow;
    private static PlanetFactory displayedFactory;
    private static int displayedFractionatorId;
    private static Action<RecipeProto> advancedPickerCallback;

    /// <summary>固定宽度的双列缓存区；槽位按需要增长，内容在原版风格的裁剪视口中纵向滚动。</summary>
    private sealed class AdvancedColumn {
        internal RectTransform Content;
        internal ScrollRect Scroll;
        internal Text Title;
        internal readonly List<ProductSlot> Slots = [];
        internal readonly List<int> ItemIds = [];
        internal ProductSlotKind Kind;
        internal bool Extractable;
    }

    private static void CreateAdvancedLayout(UIFractionatorWindow window, UIFractionatorWindow vanillaWindow) {
        advancedSlotTemplate = vanillaWindow;
        advancedRoot = new GameObject("fe-advanced-fractionator", typeof(RectTransform)).GetComponent<RectTransform>();
        RectTransformUtils.NormalizeRectWithTopLeft(advancedRoot, 20f, 50f, window.transform);
        float width = rootBaseSize.x + AddWidth - 40f;
        advancedRoot.sizeDelta = new Vector2(width, 250f);
        float columnWidth = (width - 100f) / 3f;
        advancedRecipeSlot = CreateAdvancedSlot(advancedRoot, 26f, 28f);
        advancedRecipeSlot.button.onClick += _ => OpenAdvancedPicker();
        advancedRecipeSlot.button.onRightClick += _ => ClearAdvancedRecipe();
        advancedRecipeText = CreateAdvancedLabel(advancedRoot, 70f, 0f, width - 170f, 42f);
        advancedSpeedText = CreateAdvancedLabel(advancedRoot, 70f, 40f, width - 170f, 30f);
        advancedInputs = CreateAdvancedColumn(0f, columnWidth, ProductSlotKind.Fluid, false);
        advancedProducts = CreateAdvancedColumn(columnWidth, columnWidth, ProductSlotKind.Main, true);
        advancedFlow = CreateAdvancedColumn(columnWidth * 2f, columnWidth, ProductSlotKind.Side, true);
        Text foodTitle = CreateAdvancedLabel(advancedRoot, width - 80f, 80f, 80f, 35f);
        foodTitle.text = "营养食物".Translate();
        advancedFoodSlot = CreateAdvancedSlot(advancedRoot, width - 52f, 140f);
        // 食物用独立暖色框，与三个库存区域的原版白色槽区分。
        Image foodFrame = advancedFoodSlot.go.GetComponent<Image>();
        if (foodFrame != null) { foodFrame.color = new Color(1f, 0.75f, 0.35f, 0.65f); }
        advancedRoot.gameObject.SetActive(false);
    }

    private static AdvancedColumn CreateAdvancedColumn(float left, float width, ProductSlotKind kind, bool extractable) {
        RectTransform content = PageLayout.CreateScrollableArea(advancedRoot, $"advanced-{kind}-{extractable}",
            left, 115f, width - 6f, 150f, 150f);
        return new AdvancedColumn {
            Content = content, Scroll = content.GetComponentInParent<ScrollRect>(),
            Title = CreateAdvancedLabel(advancedRoot, left, 80f, width - 6f, 35f),
            Kind = kind, Extractable = extractable,
        };
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
                foreach (WorldviewRecipeInput input in recipe.InputArray) { advancedInputs.ItemIds.Add(input.ItemId); }
                foreach (WorldviewRecipeOutput output in recipe.ProductArray) { advancedProducts.ItemIds.Add(output.ItemId); }
            }
            AppendStoredIds(advancedInputs.ItemIds, state.Inputs);
            AppendStoredIds(advancedProducts.ItemIds, state.Products);
            AppendStoredIds(advancedFlow.ItemIds, state.FlowOutputs);
            int maxCount = Math.Max(advancedInputs.ItemIds.Count, Math.Max(advancedProducts.ItemIds.Count, advancedFlow.ItemIds.Count));
            float contentHeight = Math.Max(1, (maxCount + 1) / 2) * AdvancedRowHeight + 10f;
            float viewportHeight = Math.Min(MaximumAdvancedViewportHeight, Math.Max(150f, contentHeight));
            ApplyWindowSizeKeepingTopLeft(AddWidth, Math.Max(AddHeight, 235f + viewportHeight - rootBaseSize.y));
            advancedRoot.sizeDelta = new Vector2(advancedRoot.sizeDelta.x, 115f + viewportHeight);
            UpdateAdvancedColumn(advancedInputs, state.Inputs, state.InputInc, "流动输入", AdvancedFractionatorRuntime.InputCapacity, viewportHeight);
            UpdateAdvancedColumn(advancedProducts, state.Products, null, "配方产物", BufferCapacity, viewportHeight);
            UpdateAdvancedColumn(advancedFlow, state.FlowOutputs, state.FlowOutputInc, "流动输出", BufferCapacity, viewportHeight);
            int foodId = 0;
            int foodCount = 0;
            int foodInc = 0;
            foreach (var pair in state.Foods) {
                if (pair.Value <= 0) { continue; }
                foodId = pair.Key;
                foodCount = pair.Value;
                state.FoodInc.TryGetValue(foodId, out foodInc);
                break;
            }
            RenderAdvancedSlot(advancedFoodSlot, foodId, foodCount, foodInc, ProductSlotKind.Fluid);
            advancedFoodSlot.probText.text = $"{foodCount}/{BufferCapacity}";
            RecipeProto proto = LDB.recipes.Select(AdvancedRecipeCatalog.GetProtoId(state.SelectedRecipeId));
            advancedRecipeSlot.go.SetActive(true);
            advancedRecipeSlot.icon.sprite = proto?.iconSprite ?? UIRoot.instance?.uiGame?.labWindow?.defaultTechIcon;
            advancedRecipeSlot.icon.enabled = advancedRecipeSlot.icon.sprite != null;
            advancedRecipeSlot.countText.text = string.Empty;
            advancedRecipeSlot.probText.text = string.Empty;
            advancedRecipeSlot.button.tips.itemId = proto == null ? 0 : -proto.ID;
            advancedRecipeSlot.button.tips.type = UIButton.ItemTipType.Recipe;
            foreach (Image arrow in advancedRecipeSlot.incArrows) { if (arrow != null) { arrow.enabled = false; } }
            advancedRecipeText.text = (recipe?.DisplayName ?? "未选择配方".Translate()) + "\n" + "点击选择配方（右键清空）".Translate();
            advancedSpeedText.text = string.Format("次分馏每分".Translate(), Math.Round(state.ProcessingRate * 60d))
                + $" · {"成功率".Translate()} {fractionator.produceProb:P1}";
        }
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

    private static void UpdateAdvancedColumn(AdvancedColumn column, Dictionary<int, int> counts,
        Dictionary<int, int> incs, string title, int capacity, float viewportHeight) {
        int total = 0;
        foreach (var pair in counts) { total += pair.Value; }
        column.Title.text = $"{title.Translate()}\n{total}/{capacity}";
        RectTransform scrollRect = column.Scroll.GetComponent<RectTransform>();
        scrollRect.sizeDelta = new Vector2(scrollRect.sizeDelta.x, viewportHeight);
        float height = Math.Max(viewportHeight, ((column.ItemIds.Count + 1) / 2) * AdvancedRowHeight + 10f);
        column.Content.sizeDelta = new Vector2(0f, height);
        for (int i = 0; i < column.ItemIds.Count; i++) {
            if (i == column.Slots.Count) {
                ProductSlot slot = CreateAdvancedSlot(column.Content, 26f + i % 2 * SlotSpacing, 27f + i / 2 * AdvancedRowHeight);
                if (column.Extractable) { slot.button.onClick += itemId => OnSlotClick(slot, itemId); }
                column.Slots.Add(slot);
            }
            int itemId = column.ItemIds[i];
            counts.TryGetValue(itemId, out int count);
            int inc = 0;
            incs?.TryGetValue(itemId, out inc);
            RenderAdvancedSlot(column.Slots[i], itemId, count, inc, column.Kind);
        }
        for (int i = column.ItemIds.Count; i < column.Slots.Count; i++) { column.Slots[i].go.SetActive(false); }
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
