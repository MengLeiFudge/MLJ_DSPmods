using System;
using System.Collections.Generic;
using System.Linq;
using CommonAPI.Systems;
using FE.Compatibility.Nebula;
using FE.Logic.Civilization;
using FE.Logic.Fractionation.Recipes;
using FE.UI.Foundation;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static FE.FractionateEverything;
using static FE.Logic.Fractionation.Fractionators.ProcessManager;

namespace FE.Logic.Fractionation.Fractionators.Presentation;

/// <summary>基础塔与高级塔共用固定网格，按真实缓存展示输入、流出与产物。</summary>
public static partial class FractionatorWindow {
    private const float WindowWidth = 530f;
    private const float WindowHeight = 464f;
    private const int VisibleSlots = 4;
    private const float RateDisplayInterval = 0.5f;
    private static RectTransform advancedRoot;
    private static UIFractionatorWindow advancedSlotTemplate;
    private static ProductSlot advancedRecipeSlot;
    private static ProductSlot advancedFoodSlot;
    private static RectTransform digestionMask;
    private static Text advancedFoodTitle;
    private static Text advancedFoodRate;
    private static Text advancedFoodRemaining;
    private static SlotRate foodRateDisplay;
    private static Text advancedDiscardHint;
    private static Text advancedRecipeText;
    private static Image[] advancedFlowArrows;
    private static Image[] advancedProductArrows;
    private static AdvancedSection advancedInputs;
    private static AdvancedSection advancedFlow;
    private static AdvancedSection advancedProducts;
    private static PlanetFactory displayedFactory;
    private static int displayedFractionatorId;
    private static int displayedRecipeId;
    private static Action<RecipeProto> advancedPickerCallback;
    private static AdvancedFractionatorState digestionHoldState;
    private static long digestionHoldVersion;
    private static float digestionHoldStart;
    private static bool foodPressHadReserve;
    private static bool cacheSnapshotReady;
    private static readonly Dictionary<int, int> previousFlow = [];
    private static readonly Dictionary<int, int> previousProducts = [];
    private static readonly Dictionary<int, float> recentProducts = [];
    private static readonly Dictionary<int, int> basicProducts = [];
    private static readonly Dictionary<int, int> basicFlow = [];
    private static readonly Color DigestionMaskColor = new(0f, 0f, 0f, 0.62f);

    /// <summary>四格固定几何；列表只保留本次显示身份，不持有业务库存。</summary>
    private sealed class AdvancedSection {
        internal readonly ProductSlot[] Slots = new ProductSlot[VisibleSlots];
        internal readonly Text[] Rates = new Text[VisibleSlots];
        internal readonly Text[] Satisfaction = new Text[VisibleSlots];
        internal readonly SlotRate[] RateDisplays = new SlotRate[VisibleSlots];
        internal readonly List<int> ItemIds = [];
        internal ProductSlotKind Kind;
    }

    /// <summary>单槽的短时速率显示；仅平滑界面读数，不参与生产或保存。</summary>
    private struct SlotRate {
        private int itemId;
        private double displayedRate;
        internal double DisplayedRate => displayedRate;
        private double weightedRate;
        private float duration;

        /// <summary>按帧时长累计半秒平均值；空槽、物品切换和停机立即更新。</summary>
        /// <param name="id">当前槽位物品，0表示无速率信息。</param>
        /// <param name="perMinute">当前状态换算的每分钟速率。</param>
        /// <returns>整数速率文字，空槽返回空字符串。</returns>
        internal string Update(int id, double perMinute) {
            if (id <= 0) { this = default; return string.Empty; }
            if (id != itemId || perMinute <= 0d || displayedRate <= 0d) {
                this = default;
                itemId = id;
                displayedRate = perMinute;
            } else {
                float elapsed = Time.unscaledDeltaTime;
                weightedRate += perMinute * elapsed;
                duration += elapsed;
                if (duration >= RateDisplayInterval) {
                    displayedRate = weightedRate / duration;
                    weightedRate = 0d;
                    duration = 0f;
                }
            }
            return FormatRate(displayedRate);
        }
    }

    private static void CreateAdvancedLayout(UIFractionatorWindow window, UIFractionatorWindow vanillaWindow) {
        advancedSlotTemplate = vanillaWindow;
        advancedRoot = new GameObject("fe-fractionator-grid", typeof(RectTransform)).GetComponent<RectTransform>();
        RectTransformUtils.NormalizeRectWithTopLeft(advancedRoot, 0f, 40f, window.transform);
        advancedRoot.sizeDelta = new Vector2(WindowWidth, WindowHeight - 40f);
        advancedRecipeSlot = CreateAdvancedSlot(advancedRoot, 72f, 54f);
        advancedRecipeSlot.button.onClick += _ => OpenAdvancedPicker();
        advancedRecipeSlot.button.onRightClick += _ => ClearAdvancedRecipe();
        advancedRecipeText = CreateAdvancedLabel(vanillaWindow.inputTitleText, advancedRoot, new Rect(108f, 30f, 178f, 48f));
        advancedRecipeText.horizontalOverflow = HorizontalWrapMode.Wrap;
        advancedFoodSlot = CreateAdvancedSlot(advancedRoot, 265f, 134f);
        advancedFoodSlot.button.onClick += TakeAdvancedFood;
        CreateDigestionMask(advancedFoodSlot);
        AttachFoodPressHandlers(advancedFoodSlot);
        advancedFoodTitle = CreateAdvancedLabel(vanillaWindow.inputTitleText, advancedRoot, new Rect(222f, 80f, 86f, 26f));
        advancedFoodTitle.text = "FE.Advanced.FoodSlot".Translate();
        advancedFoodTitle.alignment = TextAnchor.MiddleCenter;
        advancedFoodRate = CreateAdvancedLabel(vanillaWindow.speedText, advancedRoot, new Rect(217f, 162f, 96f, 18f));
        advancedFoodRate.alignment = TextAnchor.MiddleCenter;
        advancedFoodRemaining = CreateAdvancedLabel(vanillaWindow.speedText, advancedRoot, new Rect(217f, 180f, 96f, 18f));
        advancedFoodRemaining.alignment = TextAnchor.MiddleCenter;
        advancedDiscardHint = CreateAdvancedLabel(vanillaWindow.speedText, advancedRoot, new Rect(222f, 198f, 86f, 24f));
        advancedDiscardHint.text = "FE.DiscardDigestion".Translate();
        advancedDiscardHint.alignment = TextAnchor.UpperCenter;
        advancedDiscardHint.horizontalOverflow = HorizontalWrapMode.Wrap;
        advancedInputs = CreateAdvancedSection(new Rect(18f, 74f, 208f, 220f), ProductSlotKind.Fluid);
        advancedFlow = CreateAdvancedSection(new Rect(304f, 74f, 208f, 220f), ProductSlotKind.Side);
        advancedProducts = CreateAdvancedSection(new Rect(18f, 266f, 494f, 110f), ProductSlotKind.Main);
        advancedFlowArrows = CreateAdvancedArrows(vanillaWindow, false, new Vector2(265f, -230f));
        advancedProductArrows = CreateAdvancedArrows(vanillaWindow, true, new Vector2(265f, -258f));
    }

    private static AdvancedSection CreateAdvancedSection(Rect bounds, ProductSlotKind kind) {
        RectTransform root = new GameObject($"cache-{kind}", typeof(RectTransform)).GetComponent<RectTransform>();
        RectTransformUtils.NormalizeRectWithTopLeft(root, bounds.x, bounds.y, advancedRoot);
        root.sizeDelta = bounds.size;
        string title = kind switch { ProductSlotKind.Fluid => "流动输入", ProductSlotKind.Side => "流动输出", _ => "配方产物" };
        Text titleText = CreateAdvancedLabel(advancedSlotTemplate.inputTitleText, root, new Rect(12f, 6f, bounds.width - 24f, 26f));
        titleText.text = title.Translate();
        titleText.alignment = TextAnchor.MiddleCenter;
        AdvancedSection section = new() { Kind = kind };
        for (int i = 0; i < VisibleSlots; i++) {
            float x = kind == ProductSlotKind.Main ? bounds.width / 2f + (i - 1.5f) * 112f : 54f + i % 2 * 100f;
            float y = kind == ProductSlotKind.Main ? 60f : 60f + i / 2 * 96f;
            ProductSlot slot = CreateAdvancedSlot(root, x, y);
            section.Slots[i] = slot;
            if (kind == ProductSlotKind.Fluid) { slot.button.onClick += OnAdvancedInputClick; }
            else {
                slot.button.onClick += itemId => {
                    OnSlotClick(slot, itemId);
                    // 手动取空立即补位，不保留刚产出的短暂显示。
                    recentProducts.Remove(itemId);
                };
            }
            Text rate = CreateAdvancedLabel(advancedSlotTemplate.speedText, root, new Rect(x - 48f, y + 28f, 96f, 18f));
            rate.alignment = TextAnchor.MiddleCenter;
            section.Rates[i] = rate;
            Text satisfaction = CreateAdvancedLabel(advancedSlotTemplate.speedText, root, new Rect(x - 48f, y + 46f, 96f, 18f));
            satisfaction.alignment = TextAnchor.MiddleCenter;
            section.Satisfaction[i] = satisfaction;
        }
        return section;
    }

    /// <summary>原版数组前半为产物箭头、后半为回流箭头，单行朝右；产物分支整体转-90°指向下方产物区。</summary>
    private static Image[] CreateAdvancedArrows(UIFractionatorWindow vanillaWindow, bool product, Vector2 center) {
        int split = (int)(vanillaWindow.speedArrows.Length * 0.501f);
        int start = product ? 0 : split;
        int end = product ? split : vanillaWindow.speedArrows.Length;
        Image[] arrows = new Image[end - start];
        Vector3 sourceCenter = Vector3.zero;
        for (int i = start; i < end; i++) {
            sourceCenter += vanillaWindow.transform.InverseTransformPoint(vanillaWindow.speedArrows[i].transform.position);
        }
        sourceCenter /= arrows.Length;
        Quaternion turn = Quaternion.Euler(0f, 0f, product ? -90f : 0f);
        for (int i = start; i < end; i++) {
            Image source = vanillaWindow.speedArrows[i];
            Image arrow = UnityEngine.Object.Instantiate(source, advancedRoot);
            RectTransform rect = arrow.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            Vector3 offset = turn * (vanillaWindow.transform.InverseTransformPoint(source.transform.position) - sourceCenter);
            rect.anchoredPosition = center + new Vector2(offset.x, offset.y);
            rect.localRotation = turn * Quaternion.Inverse(vanillaWindow.transform.rotation) * source.transform.rotation;
            arrow.raycastTarget = false;
            arrow.gameObject.SetActive(true);
            arrows[i - start] = arrow;
        }
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

    /// <summary>直接克隆原版对应文字，保留字号、字体材质、颜色与描边；仅调整位置和对齐。</summary>
    /// <param name="template">原版区域标题或速率文字。</param>
    /// <param name="parent">目标布局节点。</param>
    /// <param name="bounds">相对父节点左上角的文字矩形。</param>
    /// <returns>由本窗口更新内容的文字组件。</returns>
    private static Text CreateAdvancedLabel(Text template, RectTransform parent, Rect bounds) {
        Text label = UnityEngine.Object.Instantiate(template, parent, false);
        label.name = "fractionator-label";
        Localizer localizer = label.GetComponent<Localizer>();
        if (localizer != null) { localizer.enabled = false; }
        label.text = string.Empty;
        RectTransform rect = RectTransformUtils.NormalizeRectWithTopLeft(label, bounds.x, bounds.y, parent);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.sizeDelta = bounds.size;
        label.alignment = TextAnchor.MiddleLeft;
        label.resizeTextForBestFit = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
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
        Color countColor = slot.countText.color;
        countColor.a = Math.Min(countColor.a, 0.4f);
        slot.countText.color = countColor;
        if (slot.button.tips.corner == 0) { slot.button.tips.corner = 8; }
        return slot;
    }

    private static void UpdateAdvancedControls(UIFractionatorWindow src, int buildingId, AdvancedFractionatorState state) {
        bool advanced = FractionatorTowerCatalog.IsAdvancedTower(buildingId);
        int recipeId = state?.SelectedRecipeId ?? src.factorySystem.fractionatorPool[src.fractionatorId].fluidId;
        if (!ReferenceEquals(displayedFactory, src.factory) || displayedFractionatorId != src.fractionatorId
            || displayedRecipeId != recipeId) {
            CancelAdvancedPicker();
            previousFlow.Clear();
            previousProducts.Clear();
            recentProducts.Clear();
            cacheSnapshotReady = false;
            Array.Clear(advancedInputs.RateDisplays, 0, VisibleSlots);
            Array.Clear(advancedProducts.RateDisplays, 0, VisibleSlots);
            foodRateDisplay = default;
        }
        displayedFactory = src.factory;
        displayedFractionatorId = src.fractionatorId;
        displayedRecipeId = recipeId;
        advancedRoot.gameObject.SetActive(true);
        advancedFoodSlot.go.SetActive(advanced);
        advancedFoodTitle.gameObject.SetActive(advanced);
        advancedFoodRate.gameObject.SetActive(advanced);
        advancedFoodRemaining.gameObject.SetActive(advanced);
        advancedDiscardHint.gameObject.SetActive(false);
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
        if (!FractionatorTowerCatalog.IsAdvancedTower(buildingId)) { return; }
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
        if (fractionator.id == window.fractionatorId
            && FractionatorTowerCatalog.IsAdvancedTower(window.factory.entityPool[fractionator.entityId].protoId)) {
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
        if (!FractionatorTowerCatalog.IsAdvancedTower(window.factory.entityPool[
            window.factorySystem.fractionatorPool[window.fractionatorId].entityId].protoId)) { return; }
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
        // 调用方持有实例锁；输入与流出共用同一顺序，残料放在配方项之后。
        advancedInputs.ItemIds.Clear();
        advancedFlow.ItemIds.Clear();
        advancedProducts.ItemIds.Clear();
        if (recipe != null) {
            foreach (WorldviewRecipeInput input in recipe.InputArray) { advancedInputs.ItemIds.Add(input.ItemId); }
        }
        AppendStoredIds(advancedInputs.ItemIds, state.Inputs);
        AppendStoredIds(advancedInputs.ItemIds, state.FlowOutputs);
        advancedFlow.ItemIds.AddRange(advancedInputs.ItemIds);
        UpdateCacheActivity(state.FlowOutputs, state.Products);
        // 缓存可在连续收发后数量不变，此时沿用原版成功/流回分支的运行指示。
        if (fractionator.isWorking && (state.ProcessingRate > 0d || state.InputRates.Values.Any(rate => rate > 0d))) {
            SetArrowGroup(fractionator.fractionSuccess ? advancedProductArrows : advancedFlowArrows,
                true, modWindow.marqueeOnColor);
        }
        if (recipe != null) {
            foreach (WorldviewRecipeOutput output in recipe.ProductArray) {
                if (IsVisibleProduct(output.ItemId, state.Products)) { advancedProducts.ItemIds.Add(output.ItemId); }
            }
        }
        AppendStoredIds(advancedProducts.ItemIds, state.Products);
        int buildingId = src.factory.entityPool[fractionator.entityId].protoId;
        UpdateAdvancedSection(advancedInputs, state, recipe, buildingId);
        UpdateAdvancedSection(advancedFlow, state, recipe, buildingId);
        UpdateAdvancedSection(advancedProducts, state, recipe, buildingId);
        UpdateAdvancedFood(state, recipe, buildingId);
        RecipeProto proto = recipe == null ? null : LDB.recipes.Select(AdvancedRecipeCatalog.GetProtoId(recipe.PersistentId));
        RenderRecipe(proto, recipe?.DisplayName ?? "未选择配方".Translate());
    }

    private static void RenderRecipe(RecipeProto proto, string title) {
        RenderAdvancedSlot(advancedRecipeSlot, 0, 0, 0, ProductSlotKind.Main);
        advancedRecipeSlot.icon.sprite = proto?.iconSprite ?? UIRoot.instance?.uiGame?.labWindow?.defaultTechIcon;
        advancedRecipeSlot.icon.enabled = advancedRecipeSlot.icon.sprite != null;
        advancedRecipeSlot.button.tips.itemId = proto == null ? 0 : -proto.ID;
        advancedRecipeSlot.button.tips.type = UIButton.ItemTipType.Recipe;
        advancedRecipeText.text = title;
    }

    /// <summary>食物单格：图标取正在消化的矩阵（无则取备用），右下角只计与图标同种的备用，遮罩表示当前一件已消耗比例。</summary>
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
        advancedFoodSlot.button.tips.itemId = shownId;
        advancedFoodSlot.button.tips.itemCount = reserveCount > 0 ? reserveCount : digesting ? 1 : 0;
        float remaining = digesting ? AdvancedFractionatorRuntime.GetDigestionProgress(state) : 0f;
        SetDigestionMask(digesting ? 1f - remaining : 0f);
        double foodRate = AdvancedFractionatorRuntime.GetFoodRates(state, recipe, buildingId).ConsumptionPerMinute;
        advancedFoodRate.text = foodRateDisplay.Update(shownId, foodRate);
        advancedFoodRemaining.text = $"{Math.Ceiling(remaining * 100d)}%";
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

    private static void UpdateAdvancedSection(AdvancedSection section, AdvancedFractionatorState state,
        WorldviewRecipe recipe, int buildingId) {
        Dictionary<int, int> counts = section.Kind switch {
            ProductSlotKind.Fluid => state.Inputs, ProductSlotKind.Main => state.Products, _ => state.FlowOutputs,
        };
        Dictionary<int, int> incs = section.Kind switch {
            ProductSlotKind.Fluid => state.InputInc, ProductSlotKind.Side => state.FlowOutputInc, _ => null,
        };
        double rounds = state.ProcessingRate * 60d;
        float success = recipe == null ? 0f : AdvancedFractionatorRuntime.GetSuccessProbability(recipe,
            ResearchManager.GetAdvancedTowerSacrificeBoost(buildingId));
        for (int i = 0; i < VisibleSlots; i++) {
            int itemId = i < section.ItemIds.Count ? section.ItemIds[i] : 0;
            counts.TryGetValue(itemId, out int count);
            int inc = 0;
            incs?.TryGetValue(itemId, out inc);
            RenderAdvancedSlot(section.Slots[i], itemId, count, inc, section.Kind);
            double perMinute = 0d;
            double fullInputRate = 0d;
            int rateItemId = 0;
            section.Satisfaction[i].text = string.Empty;
            if (itemId > 0 && recipe != null && section.Kind == ProductSlotKind.Fluid
                && recipe.TryGetInput(itemId, out WorldviewRecipeInput input)) {
                rateItemId = itemId;
                bool catalyst = input.ConsumeProbability < 0.1f;
                if (input.ConsumeProbability >= 0.9999f) {
                    state.InputRates.TryGetValue(itemId, out double rate);
                    perMinute = rate * 60d;
                } else {
                    perMinute = rounds * input.Count * success
                        * AdvancedFractionatorRuntime.GetInputConsumeProbability(input, buildingId);
                }
                if (!catalyst) {
                    fullInputRate = AdvancedFractionatorRuntime.GetInputFullBeltRate(recipe, input, buildingId) * 60d;
                }
            } else if (itemId > 0 && recipe != null && section.Kind == ProductSlotKind.Main) {
                rateItemId = itemId;
                WorldviewRecipeOutput output = recipe.ProductArray.FirstOrDefault(candidate => candidate.ItemId == itemId);
                perMinute = output == null ? 0d : rounds * success * AdvancedFractionatorRuntime.GetOutputPerSuccess(recipe, output);
            }
            section.Rates[i].text = section.RateDisplays[i].Update(rateItemId, perMinute);
            if (fullInputRate > 0d) {
                SetSatisfaction(section.Satisfaction[i], section.RateDisplays[i].DisplayedRate / fullInputRate);
            }
        }
    }

    /// <summary>满足度按各运行器提供的满速基准显示；颜色只改RGB，保留原版文字透明度。</summary>
    private static void SetSatisfaction(Text text, double value) {
        float fraction = (float)Math.Max(0d, Math.Min(1d, value));
        text.text = fraction >= 1f ? "100%" : $"{Math.Floor(fraction * 100d):0}%";
        Color normal = advancedSlotTemplate.speedText.color;
        text.color = new Color(normal.r, normal.g * fraction, normal.b * fraction, normal.a);
    }

    private static string FormatRate(double value) => Math.Round(value).ToString("0") + "每分钟".Translate();

    private static bool IsVisibleProduct(int itemId, Dictionary<int, int> counts) =>
        counts.TryGetValue(itemId, out int count) && count > 0
        || recentProducts.TryGetValue(itemId, out float time) && Time.unscaledTime - time < 0.25f;

    /// <summary>仅在窗口可见时比较缓存；初次打开与切配方不视为产出。短暂保留刚增长的产物，避免立即送走时闪空。</summary>
    private static void UpdateCacheActivity(Dictionary<int, int> flow, Dictionary<int, int> products) {
        bool flowChanged = CacheChanged(flow, previousFlow);
        bool productChanged = CacheChanged(products, previousProducts);
        foreach (var pair in products) {
            previousProducts.TryGetValue(pair.Key, out int before);
            if (cacheSnapshotReady && pair.Value > before) { recentProducts[pair.Key] = Time.unscaledTime; }
        }
        SetArrowGroup(advancedFlowArrows, true, cacheSnapshotReady && flowChanged ? modWindow.marqueeOnColor : modWindow.marqueeOffColor);
        SetArrowGroup(advancedProductArrows, true, cacheSnapshotReady && productChanged ? modWindow.marqueeOnColor : modWindow.marqueeOffColor);
        previousFlow.Clear();
        foreach (var pair in flow) { previousFlow[pair.Key] = pair.Value; }
        previousProducts.Clear();
        foreach (var pair in products) { previousProducts[pair.Key] = pair.Value; }
        cacheSnapshotReady = true;
    }

    private static bool CacheChanged(Dictionary<int, int> current, Dictionary<int, int> previous) {
        foreach (var pair in current) {
            previous.TryGetValue(pair.Key, out int count);
            if (count != pair.Value) { return true; }
        }
        foreach (var pair in previous) {
            if (pair.Value > 0 && !current.ContainsKey(pair.Key)) { return true; }
        }
        return false;
    }

    private static void UpdateBasicUI(UIFractionatorWindow src, FractionatorComponent fractionator,
        BaseRecipe recipe, AnalysisOutcome analysis, float power, bool interactionMode) {
        basicProducts.Clear();
        basicFlow.Clear();
        List<ProductOutputInfo> products = fractionator.GetProducts(src.factory);
        foreach (ProductOutputInfo product in products) {
            if (product.count > 0 && !basicProducts.ContainsKey(product.itemId)) { basicProducts.Add(product.itemId, product.count); }
        }
        if (fractionator.fluidId > 0) { basicFlow[fractionator.fluidId] = fractionator.fluidOutputCount; }
        UpdateCacheActivity(basicFlow, basicProducts);
        byte flags = fractionator.GetCurrentOutputFlags(src.factory);
        if (fractionator.isWorking && power >= 0.1f) {
            if ((flags & OutputFlagMain) != 0) { SetArrowGroup(advancedProductArrows, true, modWindow.marqueeOnColor); }
            if ((flags & OutputFlagFluid) != 0) { SetArrowGroup(advancedFlowArrows, true, modWindow.marqueeOnColor); }
        }
        advancedProducts.ItemIds.Clear();
        if (recipe != null) {
            foreach (OutputInfo output in recipe.OutputMain) {
                if (IsVisibleProduct(output.OutputID, basicProducts)) { advancedProducts.ItemIds.Add(output.OutputID); }
            }
        }
        AppendStoredIds(advancedProducts.ItemIds, basicProducts);
        double rate = fractionator.isWorking && power >= 0.1f && !interactionMode ? GetBasicProcessingRate(fractionator) * power : 0d;
        // 满足度为当前缓存可支撑的加工速度占同堆叠满速的比例，与供电无关。
        double stack = fractionator.fluidInputCargoCount > 0.0001f
            ? fractionator.fluidInputCount / (double)fractionator.fluidInputCargoCount : 4d;
        double fullRate = MaxBeltSpeed * stack;
        bool available = recipe != null && RecipeAvailabilityStore.IsAvailable(recipe);
        int avgInc = fractionator.fluidInputCount > 0 ? fractionator.fluidInputInc / fractionator.fluidInputCount : 0;
        float success = available ? Math.Min(1f, recipe.SuccessRatio * (1f + (float)MaxTableMilli(avgInc))) : 0f;
        for (int i = 0; i < VisibleSlots; i++) {
            int inputId = i == 0 ? fractionator.fluidId : 0;
            RenderAdvancedSlot(advancedInputs.Slots[i], inputId, i == 0 ? fractionator.fluidInputCount : 0,
                i == 0 ? fractionator.fluidInputInc : 0, ProductSlotKind.Fluid);
            RenderAdvancedSlot(advancedFlow.Slots[i], inputId, i == 0 ? fractionator.fluidOutputCount : 0,
                i == 0 ? fractionator.fluidOutputInc : 0, ProductSlotKind.Fluid);
            advancedInputs.Rates[i].text = advancedInputs.RateDisplays[i].Update(inputId, rate * 60d);
            advancedInputs.Satisfaction[i].text = string.Empty;
            if (inputId > 0) { SetSatisfaction(advancedInputs.Satisfaction[i], fullRate > 0d ? GetBasicProcessingRate(fractionator) / fullRate : 0d); }
            advancedFlow.Rates[i].text = advancedFlow.Satisfaction[i].text = string.Empty;
            int productId = i < advancedProducts.ItemIds.Count ? advancedProducts.ItemIds[i] : 0;
            basicProducts.TryGetValue(productId, out int count);
            ProductOutputInfo cached = products.Find(product => product.itemId == productId && product.count > 0);
            RenderAdvancedSlot(advancedProducts.Slots[i], productId, count, 0,
                cached?.isMainOutput == false ? ProductSlotKind.Side : ProductSlotKind.Main);
            advancedProducts.Satisfaction[i].text = string.Empty;
            OutputInfo output = recipe?.OutputMain.FirstOrDefault(candidate => candidate.OutputID == productId);
            double ratio = !available || output == null ? 0d : recipe.RecipeType == ERecipe.BasicAnalysis
                ? ResearchManager.GetAnalysisProductRatio(fractionator.fluidId, productId, analysis) * output.OutputCount
                : success * output.SuccessRatio * output.OutputCount;
            advancedProducts.Rates[i].text = advancedProducts.RateDisplays[i].Update(productId, rate * 60d * ratio);
        }
        // 基础塔配方由输入决定，只读显示；没有实体产物的解析结果继续由原版状态区说明。
        RenderRecipe(null, (recipe as WorldviewRecipe)?.DisplayName ?? (interactionMode ? "交互模式" : "未选择配方").Translate());
        advancedRecipeSlot.icon.sprite = fractionator.fluidId > 0 ? LDB.items.Select(fractionator.fluidId)?.iconSprite : null;
        advancedRecipeSlot.icon.enabled = advancedRecipeSlot.icon.sprite != null;
        advancedRecipeSlot.button.tips.type = UIButton.ItemTipType.Item;
        advancedRecipeSlot.button.tips.itemId = fractionator.fluidId;
    }

    private static void RenderAdvancedSlot(ProductSlot slot, int itemId, int count, int inc, ProductSlotKind kind) {
        slot.go.SetActive(true);
        slot.kind = kind;
        slot.button.data = itemId;
        slot.icon.sprite = itemId > 0 ? LDB.items.Select(itemId)?.iconSprite : null;
        slot.icon.enabled = slot.icon.sprite != null;
        slot.countText.text = itemId > 0 ? count.ToString() : string.Empty;
        slot.button.tips.itemId = itemId;
        slot.button.tips.type = UIButton.ItemTipType.Item;
        slot.button.tips.itemCount = count;
        slot.button.tips.itemInc = inc;
        int level = count > 0 ? Cargo.fastIncArrowTable[Math.Min(10, Math.Max(0, inc / count))] : 0;
        for (int i = 0; i < slot.incArrows.Length; i++) {
            if (slot.incArrows[i] != null) { slot.incArrows[i].enabled = level >= i + 1; }
        }
    }
}
