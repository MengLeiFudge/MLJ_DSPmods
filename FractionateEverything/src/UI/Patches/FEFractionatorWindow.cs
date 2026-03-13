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
    private static GameObject modWindowGo;// 模组窗口GameObject

    // ===== 布局常量 =====
    private const int MaxMainSlots = 4;
    private const int MaxSideSlots = 4;
    private const float ExtraW = 150f;// 横向扩展
    private const float ExtraH = 90f;// 纵向扩展
    private const float SlotSpacing = 55f;

    // ===== 颜色 =====
    private static readonly Color ProbColor = new(1f, 0.9f, 0.3f, 1f);
    private static readonly Color DestroyColor = new(1f, 0.35f, 0.35f, 1f);

    // ===== 模组窗口组件引用 =====
    private static UIFractionatorWindow modWindow;
    private static float baseWindowWidth;
    private static float baseWindowHeight;
    private static Vector2 oriProductBoxAnchorPos;

    // 自定义UI元素
    private static readonly ProductSlot[] mainSlots = new ProductSlot[MaxMainSlots];
    private static readonly ProductSlot[] sideSlots = new ProductSlot[MaxSideSlots];
    private static Text mainSectionLabel;
    private static Text appendSectionLabel;
    private static Text fluidSectionLabel;
    private static Text fluidRightText;

    private class ProductSlot {
        public GameObject go;
        public Image icon;
        public UIButton button;
        public Text countText;
        public Text probText;
        public Image[] incArrows;
    }

    /// <summary>
    /// 判断是否为模组分馏塔建筑
    /// </summary>
    public static bool IsModFractionator(int fractionatorId, PlanetFactory factory) {
        if (fractionatorId == 0 || factory == null) return false;
        FractionatorComponent frac = factory.factorySystem.fractionatorPool[fractionatorId];
        if (frac.id != fractionatorId) return false;
        int buildingId = factory.entityPool[frac.entityId].protoId;
        return buildingId >= IFE交互塔 && buildingId <= IFE回收塔;
    }

    /// <summary>
    /// 在原版窗口初始化时，复制一份作为模组窗口
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow._OnInit))]
    public static void CreateModWindowInstance(UIFractionatorWindow __instance) {
        if (modWindowGo != null) return;// 已创建

        // 记录原版窗口尺寸
        RectTransform vanillaRect = __instance.GetComponent<RectTransform>();
        baseWindowWidth = vanillaRect.sizeDelta.x;
        baseWindowHeight = vanillaRect.sizeDelta.y;

        // 记录 oriProductBox 位置
        RectTransform oriBoxRT = __instance.oriProductBox.GetComponent<RectTransform>();
        oriProductBoxAnchorPos = oriBoxRT != null ? oriBoxRT.anchoredPosition : Vector2.zero;

        // 复制窗口
        modWindowGo = Object.Instantiate(__instance.gameObject, __instance.transform.parent);
        modWindowGo.name = "FE-FractionatorWindow";
        modWindow = modWindowGo.GetComponent<UIFractionatorWindow>();

        // 立即应用模组布局（一次性，不需要还原）
        ApplyModLayoutOnce(modWindow);

        // 隐藏备用
        modWindowGo.SetActive(false);
    }

    /// <summary>
    /// 一次性应用模组窗口布局，后续不再修改位置
    /// </summary>
    private static void ApplyModLayoutOnce(UIFractionatorWindow window) {
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.sizeDelta = new Vector2(baseWindowWidth + ExtraW, baseWindowHeight + ExtraH);

        // 隐藏原版UI元素
        window.productBox.SetActive(false);
        window.productProbText.gameObject.SetActive(false);
        window.oriProductProbText.gameObject.SetActive(false);
        if (window.historyText != null) window.historyText.gameObject.SetActive(false);

        // oriProductBox 保留显示
        window.oriProductBox.SetActive(true);

        // 创建自定义元素
        Text refLabel = window.inputTitleText ?? window.titleText;

        mainSectionLabel = CreateLabel(window, refLabel, "主产物".Translate());
        appendSectionLabel = CreateLabel(window, refLabel, "副产物".Translate());
        fluidSectionLabel = CreateLabel(window, refLabel, "流体输出".Translate());

        // 流体输出右侧信息
        if (window.oriProductProbText != null) {
            GameObject fluidRightGo = Object.Instantiate(window.oriProductProbText.gameObject, window.transform);
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
            mainSlots[i] = CreateSlot(window);
        }
        for (int i = 0; i < MaxSideSlots; i++) {
            sideSlots[i] = CreateSlot(window);
        }
    }

    private static Text CreateLabel(UIFractionatorWindow window, Text reference, string text) {
        GameObject go = Object.Instantiate(reference.gameObject, window.transform);
        go.name = "section-label-" + text;
        Text label = go.GetComponent<Text>();
        label.text = text;
        label.fontSize = reference.fontSize;
        label.alignment = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.gameObject.SetActive(false);
        return label;
    }

    private static ProductSlot CreateSlot(UIFractionatorWindow window) {
        GameObject go = Object.Instantiate(window.oriProductBox, window.transform);
        go.SetActive(false);

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

        // 概率文字
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

    // ===== 窗口打开拦截 =====

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow._OnOpen))]
    public static bool OnWindowOpen(UIFractionatorWindow __instance) {
        if (!IsModFractionator(__instance.fractionatorId, __instance.factory)) {
            // 原版建筑：确保模组窗口关闭
            if (modWindowGo != null && modWindowGo.activeSelf) {
                modWindowGo.SetActive(false);
            }
            return true;// 使用原版窗口
        }

        // 模组建筑：关闭原版窗口，打开模组窗口
        __instance._Close();

        // 设置模组窗口的fractionatorId
        modWindow.fractionatorId = __instance.fractionatorId;
        modWindowGo.SetActive(true);

        // 注册按钮事件
        foreach (var slot in mainSlots) {
            if (slot.button != null) slot.button.onClick += OnSlotClick;
        }
        foreach (var slot in sideSlots) {
            if (slot.button != null) slot.button.onClick += OnSlotClick;
        }

        return false;// 跳过原版打开逻辑
    }

    private static void OnSlotClick(int itemId) {
        if (modWindow == null || !modWindow.active) return;
        modWindow.OnProductUIButtonClick(itemId);
    }

    // ===== 窗口关闭拦截 =====

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow._OnClose))]
    public static bool OnWindowClose(UIFractionatorWindow __instance) {
        if (__instance == modWindow) {
            // 模组窗口关闭
            modWindowGo.SetActive(false);

            // 注销按钮事件
            foreach (var slot in mainSlots) {
                if (slot.button != null) slot.button.onClick -= OnSlotClick;
            }
            foreach (var slot in sideSlots) {
                if (slot.button != null) slot.button.onClick -= OnSlotClick;
            }

            return false;// 跳过原版关闭逻辑
        }

        // 原版窗口正常关闭，不做任何修改
        return true;
    }

    // ===== 更新逻辑 =====

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow._OnUpdate))]
    public static void OnWindowUpdate(UIFractionatorWindow __instance) {
        // 只处理模组窗口的更新
        if (__instance != modWindow || !modWindow.active) return;

        if (modWindow.fractionatorId == 0 || modWindow.factory == null) return;

        FractionatorComponent fractionator = modWindow.factorySystem.fractionatorPool[modWindow.fractionatorId];
        if (fractionator.id != modWindow.fractionatorId || fractionator.fluidId == 0) return;

        int buildingID = modWindow.factory.entityPool[fractionator.entityId].protoId;
        ItemProto building = LDB.items.Select(buildingID);
        if (building == null) return;

        // 更新标题
        int level = building.Level();
        modWindow.titleText.text = level > 0 ? $"{building.name} +{level}" : building.name;

        // 更新状态文本
        UpdateStateText(modWindow, fractionator, building, buildingID);

        // 更新速率
        UpdateSpeedText(modWindow, fractionator);

        // 获取配方
        BaseRecipe recipe = GetRecipeForBuilding(buildingID, fractionator.fluidId);

        float successBoost = building.SuccessBoost();
        int fluidInputIncAvg = fractionator.fluidInputCount > 0
            ? fractionator.fluidInputInc / fractionator.fluidInputCount
            : 0;
        float pointsBonus = (float)MaxTableMilli(fluidInputIncAvg);

        float recipeSuccessRatio = 0f;
        float mainOutputBonus = 1f;
        float destroyRatio = 0f;

        if (recipe != null && !recipe.Locked) {
            recipeSuccessRatio = recipe.SuccessRatio * (1 + successBoost) * (1 + pointsBonus);
            mainOutputBonus = 1 + recipe.DoubleOutputRatio;
            destroyRatio = recipe.DestroyRatio;
        }

        // 更新UI（只控制显示/隐藏和数据，不调整位置）
        UpdateUIElements(modWindow, fractionator, building, recipe,
            recipeSuccessRatio, mainOutputBonus, destroyRatio);
    }

    private static void UpdateStateText(UIFractionatorWindow window,
        FractionatorComponent fractionator, ItemProto building, int buildingID) {

        int fluidOutputMax = building.FluidOutputMax();
        int productOutputMax = building.ProductOutputMax();
        List<ProductOutputInfo> products = fractionator.products(window.factory);

        if (!fractionator.isWorking) {
            if (fractionator.fluidId == 0) {
                window.stateText.text = "待机".Translate();
                window.stateText.color = window.idleColor;
            } else if (fractionator.fluidInputCount == 0) {
                window.stateText.text = "缺少原材料".Translate();
                window.stateText.color = window.workStoppedColor;
            } else if (fractionator.fluidOutputCount >= fluidOutputMax) {
                window.stateText.text = "原料堆积".Translate();
                window.stateText.color = window.workStoppedColor;
            } else if (products.Any(p => p.count >= productOutputMax)) {
                if (building.EnableFluidEnhancement()) {
                    window.stateText.text = "分馏永动".Translate();
                } else {
                    window.stateText.text = "产物堆积".Translate();
                }
                window.stateText.color = window.workStoppedColor;
            } else {
                window.stateText.text = "搬运模式".Translate();
                window.stateText.color = window.workStoppedColor;
            }
        } else {
            if (buildingID == IFE交互塔
                && fractionator.belt0 > 0
                && fractionator.belt1 <= 0
                && fractionator.belt2 <= 0) {
                window.stateText.text = "交互模式".Translate();
                window.stateText.color = window.workNormalColor;
            }
        }
    }

    private static void UpdateSpeedText(UIFractionatorWindow window, FractionatorComponent fractionator) {
        PowerConsumerComponent powerConsumer = window.powerSystem.consumerPool[fractionator.pcId];
        int networkId = powerConsumer.networkId;
        PowerNetwork powerNetwork = window.powerSystem.netPool[networkId];
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

        if (!fractionator.isWorking) speed = 0.0;
        window.speedText.text = string.Format("次分馏每分".Translate(), Math.Round(speed));
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

    private static void UpdateUIElements(UIFractionatorWindow window,
        FractionatorComponent fractionator, ItemProto building, BaseRecipe recipe,
        float recipeSuccessRatio, float mainOutputBonus, float destroyRatio) {

        List<ProductOutputInfo> products = fractionator.products(window.factory);
        bool sandboxMode = GameMain.sandboxToolsEnabled;

        // 隐藏所有槽位
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

        // 布局位置
        RectTransform oriBoxRT = window.oriProductBox.GetComponent<RectTransform>();
        Vector2 fluidRowPos = oriBoxRT != null ? oriBoxRT.anchoredPosition : oriProductBoxAnchorPos;

        float labelOffsetY = 32f;
        float rowStep = 90f;

        // 流体输出行标签
        float fluidLabelY = fluidRowPos.y + labelOffsetY;
        float fluidLabelX = fluidRowPos.x - 20f;
        SetLabelPos(fluidSectionLabel, fluidLabelX, fluidLabelY);
        fluidSectionLabel.gameObject.SetActive(true);

        // 流体输出右侧信息
        if (fluidRightText != null) {
            RectTransform frRT = fluidRightText.GetComponent<RectTransform>();
            if (frRT != null) {
                float infoX = fluidRowPos.x + 60f;
                frRT.anchoredPosition = new Vector2(infoX, fluidRowPos.y + 8f);
            }
            fluidRightText.gameObject.SetActive(true);

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
                $"{levelLabel} {enhanceStr}\n"
                + $"{flowStr}  <color=#{ColorUtility.ToHtmlStringRGBA(DestroyColor)}>{destroyStr}</color>";
        }

        // 副产物行
        float sideRowY = fluidRowPos.y + rowStep;
        if (sideCount > 0) {
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

        // 主产物行
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

    // ===== OnFractionatorIdChange =====

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIFractionatorWindow), nameof(UIFractionatorWindow.OnFractionatorIdChange))]
    public static void OnFractionatorIdChange(UIFractionatorWindow __instance) {
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
