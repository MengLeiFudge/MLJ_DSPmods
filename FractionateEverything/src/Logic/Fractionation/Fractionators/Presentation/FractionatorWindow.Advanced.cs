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

/// <summary>高级塔以左上输入、右上流出、下方产物展示反应，左侧显示速率，右侧显示供养。</summary>
public static partial class FractionatorWindow {
    private const float AdvancedWindowWidth = 900f;
    private const float AdvancedPadding = 24f;
    private const float AdvancedInfoWidth = 170f;
    private const float AdvancedFoodWidth = 94f;
    private const float AdvancedGap = 20f;
    private const float AdvancedRowHeight = 146f;
    private const float AdvancedSectionHeader = 36f;
    private const float MaximumAdvancedViewportHeight = AdvancedRowHeight * 3f + 8f;
    private static RectTransform advancedRoot;
    private static UIFractionatorWindow advancedSlotTemplate;
    private static ProductSlot advancedRecipeSlot;
    private static ProductSlot advancedFoodSlot;
    private static ProductSlot advancedDigestionSlot;
    private static RectTransform digestionProgressFill;
    private static AdvancedFractionatorState digestionHoldState;
    private static long digestionHoldVersion;
    private static float digestionHoldStart;
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

    /// <summary>独立缓存区域的固定宽度、槽位网格和裁剪视口；只保存界面引用，不保存业务库存。</summary>
    private sealed class AdvancedSection {
        internal RectTransform Root;
        internal RectTransform Content;
        internal ScrollRect Scroll;
        internal Text Title;
        internal float Width;
        internal int Columns;
        internal ProductSlotKind Kind;
        internal readonly List<ProductSlot> Slots = [];
        internal readonly List<Text> Details = [];
        internal readonly List<int> ItemIds = [];
    }

    /// <summary>按窗口内边距建立三区域；食物与消化物保留独立点击语义。</summary>
    private static void CreateAdvancedLayout(UIFractionatorWindow window, UIFractionatorWindow vanillaWindow) {
        advancedSlotTemplate = vanillaWindow;
        advancedRoot = new GameObject("fe-advanced-fractionator", typeof(RectTransform)).GetComponent<RectTransform>();
        RectTransformUtils.NormalizeRectWithTopLeft(advancedRoot, AdvancedPadding, 64f, window.transform);
        float width = Math.Max(AdvancedWindowWidth, rootBaseSize.x + AddWidth) - AdvancedPadding * 2f;
        advancedRoot.sizeDelta = new Vector2(width, 450f);
        advancedRecipeSlot = CreateAdvancedSlot(advancedRoot, 28f, 28f);
        advancedRecipeSlot.button.onClick += _ => OpenAdvancedPicker();
        advancedRecipeSlot.button.onRightClick += _ => ClearAdvancedRecipe();
        advancedRecipeText = CreateAdvancedLabel(advancedRoot, 0f, 66f, AdvancedInfoWidth, 66f);
        Text pickerHint = CreateAdvancedLabel(advancedRoot, 0f, 134f, AdvancedInfoWidth, 38f);
        pickerHint.text = "点击选择配方（右键清空）".Translate();
        pickerHint.fontSize = 12;
        CreateAdvancedLabel(advancedRoot, 0f, 184f, AdvancedInfoWidth, 28f).text = "FE.Advanced.RecipeRate".Translate();
        advancedSpeedText = CreateAdvancedLabel(advancedRoot, 0f, 214f, AdvancedInfoWidth, 110f);
        advancedRatioText = CreateAdvancedLabel(advancedRoot, 0f, 340f, AdvancedInfoWidth, 110f);
        advancedRatioText.fontSize = 12;
        float dataLeft = AdvancedInfoWidth + AdvancedGap;
        float dataWidth = width - dataLeft - AdvancedFoodWidth - AdvancedGap;
        float sectionWidth = (dataWidth - AdvancedGap) / 2f;
        advancedInputs = CreateAdvancedSection(dataLeft, sectionWidth, 2, ProductSlotKind.Fluid);
        advancedFlow = CreateAdvancedSection(dataLeft + sectionWidth + AdvancedGap, sectionWidth, 2, ProductSlotKind.Side);
        advancedProducts = CreateAdvancedSection(dataLeft, dataWidth, 4, ProductSlotKind.Main);
        float foodLeft = width - AdvancedFoodWidth;
        Text foodTitle = CreateAdvancedLabel(advancedRoot, foodLeft, 0f, AdvancedFoodWidth, 32f);
        foodTitle.text = "FE.ReserveFood".Translate();
        foodTitle.alignment = TextAnchor.UpperCenter;
        advancedFoodSlot = CreateAdvancedSlot(advancedRoot, foodLeft + AdvancedFoodWidth / 2f, 62f);
        advancedFoodSlot.button.onClick += TakeAdvancedFood;
        Text digestionTitle = CreateAdvancedLabel(advancedRoot, foodLeft, 150f, AdvancedFoodWidth, 32f);
        digestionTitle.text = "FE.Digestion".Translate();
        digestionTitle.alignment = TextAnchor.UpperCenter;
        advancedDigestionSlot = CreateAdvancedSlot(advancedRoot, foodLeft + AdvancedFoodWidth / 2f, 214f);
        Text discardHint = CreateAdvancedLabel(advancedRoot, foodLeft, 264f, AdvancedFoodWidth, 36f);
        discardHint.text = "FE.DiscardDigestion".Translate();
        discardHint.fontSize = 12;
        discardHint.alignment = TextAnchor.UpperCenter;
        advancedFoodText = CreateAdvancedLabel(advancedRoot, foodLeft, 312f, AdvancedFoodWidth, 110f);
        advancedFoodText.fontSize = 12;
        advancedFoodText.alignment = TextAnchor.UpperCenter;
        Image progressTrack = new GameObject("digestion-progress", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        RectTransformUtils.NormalizeRectWithTopLeft(progressTrack, foodLeft + 19f, 246f, advancedRoot).sizeDelta = new Vector2(56f, 6f);
        progressTrack.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        progressTrack.raycastTarget = false;
        Image fill = new GameObject("remaining", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        digestionProgressFill = RectTransformUtils.NormalizeRectWithTopLeft(fill, 0f, 0f, progressTrack.transform);
        fill.color = new Color(1f, 0.75f, 0.35f);
        fill.raycastTarget = false;
        EventTrigger trigger = advancedDigestionSlot.go.AddComponent<EventTrigger>();
        EventTrigger.Entry down = new() { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener(data => {
            if (data is PointerEventData pointer && pointer.button == PointerEventData.InputButton.Left) { BeginDigestionDiscard(); }
        });
        trigger.triggers.Add(down);
        foreach (EventTriggerType type in new[] { EventTriggerType.PointerUp, EventTriggerType.PointerExit }) {
            EventTrigger.Entry cancel = new() { eventID = type };
            cancel.callback.AddListener(_ => CancelDigestionDiscard());
            trigger.triggers.Add(cancel);
        }
        Image foodFrame = advancedFoodSlot.go.GetComponent<Image>();
        if (foodFrame != null) { foodFrame.color = new Color(1f, 0.75f, 0.35f, 0.65f); }
        advancedRoot.gameObject.SetActive(false);
    }

    /// <summary>建立带内边距的库存网格，标题和滚动条均留在区域内部。</summary>
    private static AdvancedSection CreateAdvancedSection(float left, float width, int columns, ProductSlotKind kind) {
        RectTransform root = new GameObject($"advanced-{kind}", typeof(RectTransform)).GetComponent<RectTransform>();
        RectTransformUtils.NormalizeRectWithTopLeft(root, left, 0f, advancedRoot);
        root.sizeDelta = new Vector2(width, 200f);
        RectTransform content = PageLayout.CreateScrollableArea(root, "slots", 8f, AdvancedSectionHeader,
            width - 16f, 160f, 160f);
        return new AdvancedSection {
            Root = root, Content = content, Scroll = content.parent.parent.GetComponent<ScrollRect>(),
            Title = CreateAdvancedLabel(root, 8f, 0f, width - 16f, 30f),
            Width = width, Columns = columns, Kind = kind,
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

    /// <summary>备用食物与反应库存分开取物，先扣真实库存，再转移到手持或背包。</summary>
    private static void TakeAdvancedFood(int itemId) {
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

    /// <summary>按下时固定实例和当前件代次，持续按住一秒才允许销毁。</summary>
    private static void BeginDigestionDiscard() {
        CancelDigestionDiscard();
        if (NebulaMultiplayerModAPI.IsClient) {
            UIRealtimeTip.Popup("高级塔客户端手动取出受限".Translate());
            return;
        }
        if (sourceWindow?.active != true || sourceWindow.factory != displayedFactory
            || sourceWindow.fractionatorId != displayedFractionatorId) { return; }
        AdvancedFractionatorState state = AdvancedFractionatorRuntime.GetState(displayedFactory, displayedFractionatorId);
        if (state == null) { return; }
        lock (state) {
            if (state.DigestingEnergy <= 0d) { return; }
            digestionHoldState = state;
            digestionHoldVersion = state.DigestionVersion;
            digestionHoldStart = Time.unscaledTime;
        }
    }

    private static void CancelDigestionDiscard() => digestionHoldState = null;

    /// <summary>由可见窗口更新驱动；松键、失焦、换件或实例失效均取消，不重复销毁后续食物。</summary>
    private static void UpdateDigestionDiscard(AdvancedFractionatorState state) {
        if (digestionHoldState == null) { return; }
        if (!Input.GetMouseButton(0) || !Application.isFocused || !ReferenceEquals(state, digestionHoldState)
            || state.DigestionVersion != digestionHoldVersion || sourceWindow?.active != true) {
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
            float topHeight = Math.Max(160f, Math.Min(MaximumAdvancedViewportHeight,
                Math.Max(GetSectionContentHeight(advancedInputs), GetSectionContentHeight(advancedFlow))));
            float productHeight = Math.Max(AdvancedRowHeight + 8f, Math.Min(AdvancedRowHeight * 2f + 8f, GetSectionContentHeight(advancedProducts)));
            float productTop = AdvancedSectionHeader + topHeight + AdvancedGap;
            float height = Math.Max(450f, productTop + AdvancedSectionHeader + productHeight + 12f);
            ApplyWindowSizeKeepingTopLeft(advancedRoot.sizeDelta.x + AdvancedPadding * 2f - rootBaseSize.x,
                64f + height + AdvancedPadding - rootBaseSize.y);
            advancedRoot.sizeDelta = new Vector2(advancedRoot.sizeDelta.x, height);
            int buildingId = src.factory.entityPool[fractionator.entityId].protoId;
            UpdateAdvancedSection(advancedInputs, state, recipe, buildingId, topHeight, 0f);
            UpdateAdvancedSection(advancedFlow, state, recipe, buildingId, topHeight, 0f);
            UpdateAdvancedSection(advancedProducts, state, recipe, buildingId, productHeight, productTop);
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
            advancedFoodSlot.probText.text = $"{foodCount}/{AdvancedFractionatorRuntime.FoodCapacity}";
            var foodRates = AdvancedFractionatorRuntime.GetFoodRates(state, recipe, buildingId);
            advancedFoodText.text = state.DigestingEnergy > 0d
                ? string.Format("FE.Advanced.FoodRates".Translate(), foodRates.ConsumptionPerMinute, foodRates.Share)
                : string.Empty;
            UpdateDigestionDiscard(state);
            bool digesting = state.DigestingEnergy > 0d;
            RenderAdvancedSlot(advancedDigestionSlot, state.DigestingItemId, digesting ? 1 : 0,
                state.DigestingInc, ProductSlotKind.Fluid);
            advancedDigestionSlot.countText.text = string.Empty;
            digestionProgressFill.sizeDelta = new Vector2(56f * AdvancedFractionatorRuntime.GetDigestionProgress(state), 6f);
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
        WorldviewRecipe recipe, int buildingId, float viewportHeight, float top) {
        string title = section.Kind switch { ProductSlotKind.Fluid => "流动输入", ProductSlotKind.Main => "配方产物", _ => "流动输出" };
        section.Title.text = title.Translate();
        section.Root.anchoredPosition = new Vector2(section.Root.anchoredPosition.x, -top);
        section.Root.sizeDelta = new Vector2(section.Width, AdvancedSectionHeader + viewportHeight);
        RectTransform scrollRect = section.Scroll.GetComponent<RectTransform>();
        scrollRect.sizeDelta = new Vector2(scrollRect.sizeDelta.x, viewportHeight);
        section.Content.sizeDelta = new Vector2(0f, Math.Max(viewportHeight, GetSectionContentHeight(section)));
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
                Text detail = CreateAdvancedLabel(section.Content, left + 3f, rowTop + 62f, cellWidth - 6f, 80f);
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
                return $"{count}/{BufferCapacity}\n" + string.Format("FE.Advanced.ProductRule".Translate(), perSuccess, success,
                    rounds * success * perSuccess);
            }
        }
        if (kind == ProductSlotKind.Side && recipe != null && recipe.TryGetInput(itemId, out WorldviewRecipeInput flow)) {
            if (flow.ConsumeProbability >= 0.9999f) {
                return $"{count}/{BufferCapacity}\n" + string.Format("FE.Advanced.FlowRule".Translate(), 1f - success,
                    rounds * flow.Count * (1f - success));
            }
            return $"{count}/{BufferCapacity}\n" + "FE.Advanced.ExcessCatalyst".Translate();
        }
        int fallbackCapacity = kind == ProductSlotKind.Fluid ? AdvancedFractionatorRuntime.GetInputCapacity(recipe, itemId) : BufferCapacity;
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
