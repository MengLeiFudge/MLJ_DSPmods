using System;
using FE.Logic.Fractionation.State;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Fractionation.Growth;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Fractionation.Process.ProcessManager;
using static FE.Logic.Fractionation.Recipes.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Presentation;

public static partial class FractionatorWindow {
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
        UpdatePowerDisplay(src, powerConsumer, consumerRatio);

        // 输入侧
        if (hasFluid) {
            ItemProto needProto = LDB.items.Select(fractionator.fluidId);
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
                ? fractionator.fluidInputCount / (double)fractionator.fluidInputCargoCount
                : 4.0;
        double speed = consumerRatio
                       * (fractionator.fluidInputCargoCount < MaxBeltSpeed
                           ? fractionator.fluidInputCargoCount
                           : MaxBeltSpeed)
                       * fluidInputCountPerCargo
                       * 60.0;
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
        int lockedOutputId = buildingID == IFE转化塔
            ? fractionator.GetNormalizedLockedOutput(src.factory)
            : 0;

        float successBoost = building.SuccessBoost();
        int avgInc = fractionator.fluidInputCount > 0 ? fractionator.fluidInputInc / fractionator.fluidInputCount : 0;
        float pointsBonus = (float)MaxTableMilli(avgInc);

        float recipeSuccessRatio = 0f, mainOutputBonus = 1f, destroyRatio = 0f;
        if (recipe != null && RecipeGrowthQueries.IsUnlocked(recipe)) {
            recipeSuccessRatio = recipe.SuccessRatio * (1 + successBoost) * (1 + pointsBonus);
            mainOutputBonus = 1 + recipe.DoubleOutputRatio;
            destroyRatio = recipe.DestroyRatio;
        }

        UpdateUIElements(src, fractionator, recipe, recipeSuccessRatio, mainOutputBonus, destroyRatio, hasFluid,
            lockedOutputId);
    }

    private static void UpdateModStateText(UIFractionatorWindow src,
        FractionatorComponent fractionator, ItemProto building, int buildingID, float consumerRatio) {

        int fluidOutputMax = building.FluidOutputMax();
        int productOutputMax = building.ProductOutputMax();
        List<ProductOutputInfo> products = fractionator.products(src.factory);

        if (fractionator.isWorking) {
            if (buildingID == IFE交互塔
                && fractionator.belt0 > 0
                && fractionator.belt1 <= 0
                && fractionator.belt2 <= 0) {
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
                    ? "分馏永动".Translate()
                    : "产物堆积".Translate();
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
            IFE交互塔 => GetRecipe<BuildingTrainRecipe>(ERecipe.BuildingTrain, fluidId),
            IFE矿物复制塔 => GetRecipe<MineralCopyRecipe>(ERecipe.MineralCopy, fluidId),
            IFE点数聚集塔 => GetRecipe<PointAggregateRecipe>(ERecipe.PointAggregate, fluidId),
            IFE转化塔 => GetRecipe<ConversionRecipe>(ERecipe.Conversion, fluidId),
            IFE精馏塔 => GetRecipe<RectificationRecipe>(ERecipe.Rectification, fluidId),
            _ => null
        };
    }

    private static void UpdateUIElements(UIFractionatorWindow src,
        FractionatorComponent fractionator, BaseRecipe recipe,
        float recipeSuccessRatio, float mainOutputBonus, float destroyRatio, bool hasFluid, int lockedOutputId) {

        List<ProductOutputInfo> products = fractionator.products(src.factory);
        bool sandboxMode = GameMain.sandboxToolsEnabled;
        ConversionRecipe conversionRecipe = recipe as ConversionRecipe;
        bool showLockControls = src.factory.entityPool[fractionator.entityId].protoId == IFE转化塔
                                && ConversionTower.EnableSingleLock
                                && conversionRecipe != null
                                && conversionRecipe.SupportsLockedOutput;

        foreach (var slot in mainSlots)
            if (slot != null) {
                slot.go.SetActive(false);
                SetSlotLocked(slot, false);
            }
        foreach (var slot in sideSlots)
            if (slot != null) {
                slot.go.SetActive(false);
                SetSlotLocked(slot, false);
            }
        if (fluidSlot != null) fluidSlot.go.SetActive(false);

        int fractionatorId = src.fractionatorId;
        bool hasCachedWidth = widthByFractionatorId.TryGetValue(fractionatorId, out float cachedWidth);

        if (!hasFluid) {
            ApplyWindowSizeKeepingTopLeft(hasCachedWidth ? cachedWidth : 0f, AddHeight);
            if (_mainArrowText != null) _mainArrowText.gameObject.SetActive(false);
            if (_sideArrowText != null) _sideArrowText.gameObject.SetActive(false);
            if (_fluidArrowText != null) _fluidArrowText.gameObject.SetActive(false);
            if (fluidRightText != null) fluidRightText.gameObject.SetActive(false);
            UpdateLockStatusUI(fractionator, recipe as ConversionRecipe, lockedOutputId, showLockControls);
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
        ConversionRecipe.LockedOutputPlan lockedPlan = default;
        bool singleLockActive = showLockControls
                                && lockedOutputId != 0
                                && conversionRecipe != null
                                && conversionRecipe.TryGetLockedOutputPlan(lockedOutputId, out lockedPlan);

        if (recipe != null && RecipeGrowthQueries.IsUnlocked(recipe)) {
            foreach (var output in recipe.OutputMain) {
                if (mainCount >= MaxMainSlots) break;
                var pInfo = products.Find(p => p.itemId == output.OutputID && p.isMainOutput);
                float ratio = singleLockActive
                    ? (output.OutputID == lockedPlan.OutputID ? recipeSuccessRatio : 0f)
                    : recipeSuccessRatio * output.SuccessRatio;
                FillSlot(mainSlots[mainCount], output, pInfo?.count ?? 0,
                    ratio,
                    singleLockActive || output.ShowSuccessRatio || sandboxMode, ProductSlotKind.Main);
                SetSlotLocked(mainSlots[mainCount], singleLockActive && output.OutputID == lockedPlan.OutputID);
                mainSuccessSum += ratio;
                mainCount++;
            }
            foreach (var output in recipe.OutputAppend) {
                if (sideCount >= MaxSideSlots) break;
                var pInfo = products.Find(p => p.itemId == output.OutputID && !p.isMainOutput);
                float ratio = singleLockActive
                    ? (output.OutputID == lockedPlan.OutputID ? recipeSuccessRatio : 0f)
                    : recipeSuccessRatio * output.SuccessRatio;
                FillSlot(sideSlots[sideCount], output, pInfo?.count ?? 0,
                    ratio,
                    singleLockActive || output.ShowSuccessRatio || sandboxMode, ProductSlotKind.Side);
                SetSlotLocked(sideSlots[sideCount], singleLockActive && output.OutputID == lockedPlan.OutputID);
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
        UpdateLockStatusUI(fractionator, conversionRecipe, lockedOutputId, showLockControls);

        // 流体输出右侧信息
        if (fluidRightText != null) {
            fluidRightText.gameObject.SetActive(true);
            if (recipe == null) {
                fluidRightText.text =
                    $"<color=#{ColorUtility.ToHtmlStringRGBA(DestroyColor)}>{"配方不存在".Translate()}</color>";
            } else if (!RecipeGrowthQueries.IsUnlocked(recipe)) {
                fluidRightText.text =
                    $"<color=#{ColorUtility.ToHtmlStringRGBA(DestroyColor)}>{"配方未解锁".Translate()}</color>";
            } else {
                int recipeLevel = RecipeGrowthQueries.GetLevel(recipe);
                bool hasDestroy = destroyRatio > 0f;
                string destroyStr = hasDestroy ? destroyRatio.FormatP() : "";
                fluidRightText.text = recipeLevel > 0
                    ? $"{"配方强化".Translate()} +{recipeLevel}\n<color=#{ColorUtility.ToHtmlStringRGBA(DestroyColor)}>{destroyStr}</color>"
                    : (hasDestroy ? $"<color=#{ColorUtility.ToHtmlStringRGBA(DestroyColor)}>{destroyStr}</color>" : "");
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
}
