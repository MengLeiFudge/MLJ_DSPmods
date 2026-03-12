using System;
using System.Collections.Generic;
using System.Linq;
using FE.Logic.Building;
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
/// 分馏塔简洁提示信息窗口及详情窗口的UI修改。
/// </summary>
public static class UIFractionatorWindowPatch {

    private static bool isFirstUpdateUI = true;
    private static float productProbTextBaseY;
    private static float oriProductProbTextBaseY;

    // ===== 简洁提示信息窗口 =====

    /// <summary>
    /// 修改分馏塔简洁提示信息窗口中的速率。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(EntityBriefInfo), nameof(EntityBriefInfo.SetBriefInfo))]
    public static void EntityBriefInfo_SetBriefInfo_Postfix(ref EntityBriefInfo __instance, PlanetFactory _factory,
        int _entityId) {
        if (_factory == null || _entityId == 0)
            return;
        EntityData entityData = _factory.entityPool[_entityId];
        if (entityData.id == 0)
            return;
        if (entityData.fractionatorId > 0) {
            int fractionatorId = entityData.fractionatorId;
            FractionatorComponent fractionator = _factory.factorySystem.fractionatorPool[fractionatorId];
            int fluidId = fractionator.fluidId;
            int productId = fractionator.productId;
            if (fluidId > 0 && productId > 0) {
                PowerConsumerComponent powerConsumer = _factory.powerSystem.consumerPool[fractionator.pcId];
                int networkId = powerConsumer.networkId;
                PowerNetwork powerNetwork = _factory.powerSystem.netPool[networkId];
                float consumerRatio = powerNetwork == null || networkId <= 0
                    ? 0.0f
                    : (float)powerNetwork.consumerRatio;
                double fluidInputCountPerCargo = 1.0;
                if (fractionator.fluidInputCount == 0)
                    fractionator.fluidInputCargoCount = 0.0f;
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
                if (!fractionator.isWorking)
                    speed = 0.0;
                __instance.reading0 = speed;
            }
        }
    }

    // ===== 详情窗口 =====

    // 布局常量
    private const int MaxMainSlots = 4;
    private const int MaxSideSlots = 4;

    // 固定扩展量（开窗时一次性扩展，不随配方切换而变化）
    private const float ExtraW = 150f;   // 横向扩展（3个图标宽度）
    private const float ExtraH = 90f;    // 纵向扩展（为副产物行保留）

    // 图标槽尺寸
    private const float SlotSpacing = 55f;  // 图标间距

    // 颜色
    private static readonly Color ProbColor = new(1f, 0.9f, 0.3f, 1f);       // 金色概率
    private static readonly Color DestroyColor = new(1f, 0.35f, 0.35f, 1f);  // 红色损毁

    // 创建的 UI 元素
    private static readonly ProductSlot[] mainSlots = new ProductSlot[MaxMainSlots];
    private static readonly ProductSlot[] sideSlots = new ProductSlot[MaxSideSlots];

    private static Text mainSectionLabel;
    private static Text appendSectionLabel;
    private static Text fluidSectionLabel;

    // 流体输出右侧信息文字
    private static Text fluidRightText;

    // 原版 oriProductBox 的初始 RectTransform anchoredPosition（_OnInit 时记录）
    private static Vector2 oriProductBoxAnchorPos;

    // 窗口原始宽高
    private static float baseWindowWidth;
    private static float baseWindowHeight;

    // 原版窗口右侧元素的初始 X（用于右移）
    // historyText anchoredPosition.x
    private static float historyTextOrigX;
    private static bool layoutApplied = false;

    private static UIFractionatorWindow currentWindow;

    private class ProductSlot {
        public GameObject go;
        public Image icon;
        public UIButton button;
        public Text countText;
        public Text probText;
        public Image[] incArrows;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow._OnInit))]
    public static void UIFractionatorWindow__OnInit_Postfix(UIFractionatorWindow __instance) {
        RectTransform windowRect = __instance.GetComponent<RectTransform>();
        baseWindowWidth = windowRect.sizeDelta.x;
        baseWindowHeight = windowRect.sizeDelta.y;

        // 记录 oriProductBox 的 anchoredPosition（用于计算新行的相对位置）
        RectTransform oriBoxRT = __instance.oriProductBox.GetComponent<RectTransform>();
        oriProductBoxAnchorPos = oriBoxRT != null ? oriBoxRT.anchoredPosition : Vector2.zero;

        // 记录 historyText 的初始 anchoredPosition.x
        if (__instance.historyText != null) {
            RectTransform htRT = __instance.historyText.GetComponent<RectTransform>();
            historyTextOrigX = htRT != null ? htRT.anchoredPosition.x : 0f;
        }

        // 创建区域标签（复制 inputTitleText，字体大小与"流体输入"一致）
        Text refLabel = __instance.inputTitleText ?? __instance.titleText;

        mainSectionLabel = CreateLabel(__instance, refLabel, "主产物".Translate());
        appendSectionLabel = CreateLabel(__instance, refLabel, "副产物".Translate());
        fluidSectionLabel = CreateLabel(__instance, refLabel, "流体输出".Translate());

        // 创建流体输出右侧信息文字（复制 oriProductProbText）
        if (__instance.oriProductProbText != null) {
            GameObject fluidRightGo = Object.Instantiate(__instance.oriProductProbText.gameObject, __instance.transform);
            fluidRightGo.name = "fluid-right-info";
            fluidRightText = fluidRightGo.GetComponent<Text>();
            fluidRightText.alignment = TextAnchor.UpperLeft;
            fluidRightText.horizontalOverflow = HorizontalWrapMode.Overflow;
            fluidRightText.verticalOverflow = VerticalWrapMode.Overflow;
            fluidRightText.supportRichText = true;
            fluidRightText.gameObject.SetActive(false);
        }

        // 创建产物槽
        for (int i = 0; i < MaxMainSlots; i++) {
            mainSlots[i] = CreateSlot(__instance);
        }
        for (int i = 0; i < MaxSideSlots; i++) {
            sideSlots[i] = CreateSlot(__instance);
        }
    }

    private static Text CreateLabel(UIFractionatorWindow window, Text reference, string text) {
        GameObject go = Object.Instantiate(reference.gameObject, window.transform);
        go.name = "section-label-" + text;
        Text label = go.GetComponent<Text>();
        label.text = text;
        label.fontSize = reference.fontSize; // 与"流体输入"字大小一致
        label.alignment = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.gameObject.SetActive(false);
        return label;
    }

    private static ProductSlot CreateSlot(UIFractionatorWindow window) {
        GameObject go = Object.Instantiate(window.oriProductBox, window.transform);
        go.SetActive(false);

        // 通过索引比较在克隆体中定位对应组件，避免依赖子物体名称
        Image[] origImages = window.oriProductBox.GetComponentsInChildren<Image>(true);
        Image[] cloneImages = go.GetComponentsInChildren<Image>(true);

        int iconIdx = Array.IndexOf(origImages, window.oriProductIcon);
        Image cloneIcon = (iconIdx >= 0 && iconIdx < cloneImages.Length)
            ? cloneImages[iconIdx]
            : go.GetComponentInChildren<Image>(true);

        UIButton cloneButton = cloneIcon != null
            ? cloneIcon.GetComponent<UIButton>() ?? go.GetComponentInChildren<UIButton>(true)
            : go.GetComponentInChildren<UIButton>(true);

        Text[] origTexts = window.oriProductBox.GetComponentsInChildren<Text>(true);
        Text[] cloneTexts = go.GetComponentsInChildren<Text>(true);
        int countTextIdx = Array.IndexOf(origTexts, window.oriProductCountText);
        Text cloneCountText = (countTextIdx >= 0 && countTextIdx < cloneTexts.Length)
            ? cloneTexts[countTextIdx]
            : go.GetComponentInChildren<Text>(true);

        ProductSlot slot = new ProductSlot {
            go = go,
            icon = cloneIcon,
            button = cloneButton,
            countText = cloneCountText,
            incArrows = new Image[3]
        };

        for (int i = 0; i < window.oriProductIncs.Length && i < 3; i++) {
            int incIdx = Array.IndexOf(origImages, window.oriProductIncs[i]);
            if (incIdx >= 0 && incIdx < cloneImages.Length) {
                slot.incArrows[i] = cloneImages[incIdx];
            }
        }

        // 概率文字：在现有 countText 下方创建一个新的 Text
        if (cloneCountText != null) {
            GameObject probGo = Object.Instantiate(cloneCountText.gameObject, go.transform);
            probGo.name = "prob-text";
            slot.probText = probGo.GetComponent<Text>();
            slot.probText.alignment = TextAnchor.MiddleCenter;
            slot.probText.color = ProbColor;
            slot.probText.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform probRect = slot.probText.GetComponent<RectTransform>();
            RectTransform countRect = cloneCountText.GetComponent<RectTransform>();
            if (probRect != null && countRect != null) {
                probRect.anchoredPosition = countRect.anchoredPosition + new Vector2(0, -16f);
            }
        }

        return slot;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow.OnFractionatorIdChange))]
    public static void UIFractionatorWindow_OnFractionatorIdChange_Postfix(UIFractionatorWindow __instance) {
        if (!__instance.active) return;
        if (__instance.fractionatorId == 0 || __instance.factory == null) return;
        FractionatorComponent fractionator = __instance.factorySystem.fractionatorPool[__instance.fractionatorId];
        if (fractionator.id != __instance.fractionatorId) return;

        int buildingId = __instance.factory.entityPool[fractionator.entityId].protoId;
        ItemProto building = LDB.items.Select(buildingId);
        if (building == null) return;

        bool isModBuilding = buildingId >= IFE交互塔 && buildingId <= IFE回收塔;
        if (isModBuilding) {
            int level = building.Level();
            __instance.titleText.text = level > 0
                ? $"{building.name} +{level}"
                : building.name;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow._OnOpen))]
    public static void UIFractionatorWindow__OnOpen_Postfix(UIFractionatorWindow __instance) {
        currentWindow = __instance;
        if (__instance.fractionatorId == 0 || __instance.factory == null) return;
        FractionatorComponent fractionator = __instance.factorySystem.fractionatorPool[__instance.fractionatorId];
        if (fractionator.id != __instance.fractionatorId) return;

        int buildingId = __instance.factory.entityPool[fractionator.entityId].protoId;
        bool isModBuilding = buildingId >= IFE交互塔 && buildingId <= IFE回收塔;

        if (isModBuilding) {
            ApplyModLayout(__instance);
        }

        foreach (var slot in mainSlots) {
            if (slot.button != null)
                slot.button.onClick += OnSlotClick;
        }
        foreach (var slot in sideSlots) {
            if (slot.button != null)
                slot.button.onClick += OnSlotClick;
        }
    }

    private static void ApplyModLayout(UIFractionatorWindow window) {
        if (layoutApplied) return;
        layoutApplied = true;

        // 1. 固定扩展窗口尺寸（横向 + ExtraW，纵向 + ExtraH）
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.sizeDelta = new Vector2(baseWindowWidth + ExtraW, baseWindowHeight + ExtraH);

        // 2. 隐藏原版产物相关 UI
        window.productBox.SetActive(false);
        window.productProbText.gameObject.SetActive(false);
        window.oriProductProbText.gameObject.SetActive(false);
        if (window.historyText != null) window.historyText.gameObject.SetActive(false);

        // 3. 右侧元素右移 ExtraW（historyText 区域，右侧统计已隐藏，无需右移）
        // oriProductBox（流体输出图标）已在原位，无需移动

        // 4. 确保 oriProductBox 自身仍显示（它是流体输出图标）
        window.oriProductBox.SetActive(true);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow._OnClose))]
    public static void UIFractionatorWindow__OnClose_Postfix(UIFractionatorWindow __instance) {
        currentWindow = null;

        // 还原窗口尺寸
        if (layoutApplied) {
            RectTransform windowRect = __instance.GetComponent<RectTransform>();
            windowRect.sizeDelta = new Vector2(baseWindowWidth, baseWindowHeight);
            layoutApplied = false;
        }

        // 还原原版 UI
        __instance.productBox.SetActive(true);
        __instance.productProbText.gameObject.SetActive(true);
        __instance.oriProductProbText.gameObject.SetActive(true);
        if (__instance.historyText != null) __instance.historyText.gameObject.SetActive(true);

        // 隐藏自定义元素
        mainSectionLabel?.gameObject.SetActive(false);
        appendSectionLabel?.gameObject.SetActive(false);
        fluidSectionLabel?.gameObject.SetActive(false);
        fluidRightText?.gameObject.SetActive(false);

        foreach (var slot in mainSlots) {
            slot.go.SetActive(false);
            if (slot.button != null)
                slot.button.onClick -= OnSlotClick;
        }
        foreach (var slot in sideSlots) {
            slot.go.SetActive(false);
            if (slot.button != null)
                slot.button.onClick -= OnSlotClick;
        }
    }

    private static void OnSlotClick(int itemId) {
        if (currentWindow == null) return;
        currentWindow.OnProductUIButtonClick(itemId);
    }

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
            int count = target.count;
            if (VFInput.control || VFInput.shift) {
                int added = player.TryAddItemToPackage(itemId, count, 0, throwTrash: false);
                if (added > 0) UIItemup.Up(itemId, added);
            } else {
                player.SetHandItemId_Unsafe(itemId);
                player.SetHandItemCount_Unsafe(count);
            }
            target.count = 0;
        } else if (player.inhandItemId == itemId && player.inhandItemCount > 0) {
            ItemProto building = LDB.items.Select(buildingId);
            int max = building.ProductOutputMax();
            int canAdd = max - target.count;
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

    /// <summary>
    /// 修改分馏塔详情窗口中的部分内容。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow._OnUpdate))]
    public static void UIFractionatorWindow__OnUpdate_Postfix(ref UIFractionatorWindow __instance) {
        if (isFirstUpdateUI) {
            isFirstUpdateUI = false;
            __instance.titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            __instance.productProbText.horizontalOverflow = HorizontalWrapMode.Overflow;
            __instance.oriProductProbText.horizontalOverflow = HorizontalWrapMode.Overflow;
            __instance.productProbText.verticalOverflow = VerticalWrapMode.Overflow;
            __instance.oriProductProbText.verticalOverflow = VerticalWrapMode.Overflow;
            productProbTextBaseY = __instance.productProbText.transform.localPosition.y;
            oriProductProbTextBaseY = __instance.oriProductProbText.transform.localPosition.y;
            __instance.productProbText.supportRichText = true;
            __instance.oriProductProbText.supportRichText = true;
        }
        if (__instance.fractionatorId == 0 || __instance.factory == null) {
            return;
        }
        FractionatorComponent fractionator =
            __instance.factorySystem.fractionatorPool[__instance.fractionatorId];
        if (fractionator.id != __instance.fractionatorId) {
            return;
        }
        if (fractionator.fluidId == 0) {
            return;
        }
        int buildingID = __instance.factory.entityPool[fractionator.entityId].protoId;

        if (buildingID < IFE交互塔 || buildingID > IFE回收塔) {
            __instance.productProbText.transform.localPosition = new(0, productProbTextBaseY, 0);
            __instance.oriProductProbText.transform.localPosition = new(0, oriProductProbTextBaseY, 0);
            return;
        }

        ItemProto building = LDB.items.Select(buildingID);
        List<ProductOutputInfo> products = fractionator.products(__instance.factory);
        int fluidOutputMax = building.FluidOutputMax();
        int productOutputMax = building.ProductOutputMax();
        int fluidInputIncAvg = fractionator.fluidInputCount > 0
            ? fractionator.fluidInputInc / fractionator.fluidInputCount
            : 0;
        float pointsBonus = (float)MaxTableMilli(fluidInputIncAvg);
        if (!fractionator.isWorking) {
            if (fractionator.fluidId == 0) {
                __instance.stateText.text = "待机".Translate();
                __instance.stateText.color = __instance.idleColor;
            } else if (fractionator.fluidInputCount == 0) {
                __instance.stateText.text = "缺少原材料".Translate();
                __instance.stateText.color = __instance.workStoppedColor;
            } else if (fractionator.fluidOutputCount >= fluidOutputMax) {
                __instance.stateText.text = "原料堆积".Translate();
                __instance.stateText.color = __instance.workStoppedColor;
            } else if (products.Any(p => p.count >= productOutputMax)) {
                if (building.EnableFluidEnhancement()) {
                    __instance.stateText.text = "分馏永动".Translate();
                    __instance.stateText.color = __instance.workStoppedColor;
                } else {
                    __instance.stateText.text = "产物堆积".Translate();
                    __instance.stateText.color = __instance.workStoppedColor;
                }
            } else {
                __instance.stateText.text = "搬运模式".Translate();
                __instance.stateText.color = __instance.workStoppedColor;
            }
        } else {
            if (buildingID == IFE交互塔
                && fractionator.belt0 > 0
                && fractionator.belt1 <= 0
                && fractionator.belt2 <= 0) {
                __instance.stateText.text = "交互模式".Translate();
                __instance.stateText.color = __instance.workNormalColor;
            }
        }

        PowerConsumerComponent powerConsumer = __instance.powerSystem.consumerPool[fractionator.pcId];
        int networkId = powerConsumer.networkId;
        PowerNetwork powerNetwork = __instance.powerSystem.netPool[networkId];
        float consumerRatio = powerNetwork == null || networkId <= 0
            ? 0.0f
            : (float)powerNetwork.consumerRatio;
        double fluidInputCountPerCargo = 1.0;
        if (fractionator.fluidInputCount == 0)
            fractionator.fluidInputCargoCount = 0.0f;
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
        if (!fractionator.isWorking)
            speed = 0.0;
        __instance.speedText.text = string.Format("次分馏每分".Translate(), Math.Round(speed));

        BaseRecipe recipe = null;
        float successBoost = building.SuccessBoost();

        switch (buildingID) {
            case IFE交互塔:
                recipe = GetRecipe<BuildingTrainRecipe>(ERecipe.BuildingTrain, fractionator.fluidId);
                break;
            case IFE矿物复制塔:
                recipe = GetRecipe<MineralCopyRecipe>(ERecipe.MineralCopy, fractionator.fluidId);
                break;
            case IFE点数聚集塔:
                recipe = GetRecipe<PointAggregateRecipe>(ERecipe.PointAggregate, fractionator.fluidId);
                break;
            case IFE转化塔:
                recipe = GetRecipe<ConversionRecipe>(ERecipe.Conversion, fractionator.fluidId);
                break;
            case IFE回收塔:
                recipe = GetRecipe<RecycleRecipe>(ERecipe.Recycle, fractionator.fluidId);
                break;
        }

        float recipeSuccessRatio = 0f;
        float mainOutputBonus = 1f;
        float destroyRatio = 0f;

        if (recipe != null && !recipe.Locked) {
            recipeSuccessRatio = recipe.SuccessRatio * (1 + successBoost) * (1 + pointsBonus);
            mainOutputBonus = 1 + recipe.DoubleOutputRatio;
            destroyRatio = recipe.DestroyRatio;
        }

        UpdateModUI(__instance, fractionator, building, recipe, recipeSuccessRatio, mainOutputBonus, destroyRatio);
    }

    private static void UpdateModUI(UIFractionatorWindow window, FractionatorComponent fractionator,
        ItemProto building, BaseRecipe recipe, float recipeSuccessRatio, float mainOutputBonus, float destroyRatio) {

        List<ProductOutputInfo> products = fractionator.products(window.factory);
        bool sandboxMode = GameMain.sandboxToolsEnabled;

        // ——— 隐藏所有自定义槽位 ———
        foreach (var slot in mainSlots) slot.go.SetActive(false);
        foreach (var slot in sideSlots) slot.go.SetActive(false);

        int mainCount = 0;
        int sideCount = 0;

        if (recipe != null && !recipe.Locked) {
            // 填充主产物槽
            foreach (var output in recipe.OutputMain) {
                if (mainCount >= MaxMainSlots) break;
                var pInfo = products.Find(p => p.itemId == output.OutputID && p.isMainOutput);
                int count = pInfo?.count ?? 0;
                ProductSlot slot = mainSlots[mainCount];
                slot.go.SetActive(true);
                if (slot.button != null) slot.button.data = output.OutputID;
                ItemProto itemProto = LDB.items.Select(output.OutputID);
                if (itemProto != null && slot.icon != null) slot.icon.sprite = itemProto.iconSprite;
                if (slot.countText != null) slot.countText.text = count.ToString();
                if (slot.probText != null) {
                    float ratio = recipeSuccessRatio * output.SuccessRatio * mainOutputBonus;
                    slot.probText.text = output.ShowSuccessRatio || sandboxMode ? ratio.FormatP() : "???";
                    slot.probText.color = ProbColor;
                }
                mainCount++;
            }

            // 填充副产物槽
            foreach (var output in recipe.OutputAppend) {
                if (sideCount >= MaxSideSlots) break;
                var pInfo = products.Find(p => p.itemId == output.OutputID && !p.isMainOutput);
                int count = pInfo?.count ?? 0;
                ProductSlot slot = sideSlots[sideCount];
                slot.go.SetActive(true);
                if (slot.button != null) slot.button.data = output.OutputID;
                ItemProto itemProto = LDB.items.Select(output.OutputID);
                if (itemProto != null && slot.icon != null) slot.icon.sprite = itemProto.iconSprite;
                if (slot.countText != null) slot.countText.text = count.ToString();
                if (slot.probText != null) {
                    float ratio = recipeSuccessRatio * output.SuccessRatio;
                    slot.probText.text = output.ShowSuccessRatio || sandboxMode ? ratio.FormatP() : "???";
                    slot.probText.color = ProbColor;
                }
                sideCount++;
            }
        }

        // ——— 布局：3行从下到上：流体输出行 → 副产物行 → 主产物行 ———
        //
        // oriProductBox 在原版窗口中对应"流体输出（原材料流出）"图标，保持在原位作为"流体输出行"的图标。
        // 我们在其上方依次放置副产物行、主产物行。
        //
        // 使用 RectTransform.anchoredPosition（相对父窗口锚点），确保所有元素在窗口内部。
        // 原版 oriProductBox 的 anchoredPosition 已在 _OnInit 中记录为 oriProductBoxAnchorPos。

        RectTransform oriBoxRT = window.oriProductBox.GetComponent<RectTransform>();
        Vector2 fluidRowPos = oriBoxRT != null ? oriBoxRT.anchoredPosition : oriProductBoxAnchorPos;

        // 标签 Y 偏移：在图标行上方 30f 处
        float labelOffsetY = 32f;
        // 行间距（图标行中心到上一行图标行中心）
        float rowStep = 90f;

        // ——— 流体输出行：标签 ———
        float fluidLabelY = fluidRowPos.y + labelOffsetY;
        // 标签 X：与 oriProductBox 左边对齐，偏移一些
        float fluidLabelX = fluidRowPos.x - 20f;

        SetLabelPos(fluidSectionLabel, fluidLabelX, fluidLabelY);
        fluidSectionLabel.gameObject.SetActive(true);

        // ——— 流体输出行右侧信息文字 ———
        if (fluidRightText != null) {
            // 放在 oriProductBox 右侧
            RectTransform frRT = fluidRightText.GetComponent<RectTransform>();
            if (frRT != null) {
                float infoX = fluidRowPos.x + 60f;  // 图标右侧
                frRT.anchoredPosition = new Vector2(infoX, fluidRowPos.y + 8f);
            }
            fluidRightText.gameObject.SetActive(true);

            // 内容：配方强化等级 + 流动概率 + 损毁概率
            int level = recipe?.Level ?? 0;
            string enhanceStr = level > 0 ? $"+{level}" : "0";
            string flowStr = recipe != null && !recipe.Locked
                ? (1f - recipeSuccessRatio - destroyRatio).FormatP()
                : "---";
            string destroyStr = recipe != null && !recipe.Locked
                ? destroyRatio.FormatP()
                : "---";
            string levelLabel = "配方强化".Translate();
            fluidRightText.text =
                $"{levelLabel} {enhanceStr}\n" +
                $"{flowStr}  <color=#{ColorUtility.ToHtmlStringRGBA(DestroyColor)}>{destroyStr}</color>";
        }

        // ——— 副产物行 ———
        float sideRowY = fluidRowPos.y + rowStep;
        if (sideCount > 0) {
            // 居中排列：N 个图标在固定宽度内居中
            float totalW = sideCount * SlotSpacing;
            float startX = fluidRowPos.x - totalW / 2f + SlotSpacing / 2f;

            for (int i = 0; i < sideCount; i++) {
                SetSlotPos(sideSlots[i], startX + i * SlotSpacing, sideRowY);
            }

            float sideLabelX = startX - SlotSpacing * 0.5f - 5f;
            SetLabelPos(appendSectionLabel, sideLabelX, sideRowY + labelOffsetY);
            appendSectionLabel.gameObject.SetActive(true);
        } else {
            appendSectionLabel.gameObject.SetActive(false);
        }

        // ——— 主产物行 ———
        float mainRowY = fluidRowPos.y + rowStep * 2f;
        if (mainCount > 0) {
            float totalW = mainCount * SlotSpacing;
            float startX = fluidRowPos.x - totalW / 2f + SlotSpacing / 2f;

            for (int i = 0; i < mainCount; i++) {
                SetSlotPos(mainSlots[i], startX + i * SlotSpacing, mainRowY);
            }

            float mainLabelX = startX - SlotSpacing * 0.5f - 5f;
            SetLabelPos(mainSectionLabel, mainLabelX, mainRowY + labelOffsetY);
            mainSectionLabel.gameObject.SetActive(true);
        } else {
            mainSectionLabel.gameObject.SetActive(false);
        }
    }

    private static void SetLabelPos(Text label, float x, float y) {
        if (label == null) return;
        RectTransform rt = label.GetComponent<RectTransform>();
        if (rt != null) rt.anchoredPosition = new Vector2(x, y);
    }

    private static void SetSlotPos(ProductSlot slot, float x, float y) {
        if (slot?.go == null) return;
        RectTransform rt = slot.go.GetComponent<RectTransform>();
        if (rt != null) rt.anchoredPosition = new Vector2(x, y);
    }
}
