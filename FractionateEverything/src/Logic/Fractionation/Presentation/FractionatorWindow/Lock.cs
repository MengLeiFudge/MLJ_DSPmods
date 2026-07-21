using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Fractionation.FracRecipes;
using FE.Logic.Fractionation.Process;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Fractionation.FracRecipes.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Presentation;

/// <summary>
/// 分馏塔窗口单路锁定图标与锁定状态显示逻辑。
/// </summary>
public static partial class FractionatorWindow {
    private static Image lockIconTemplateImage;
    private static UIButton lockIconTemplateButton;

    private static bool IsLockableOutput(BaseRecipe recipe, int itemId) {
        if (recipe == null || itemId == 0 || !recipe.IsMainOutputLockCalibrated) {
            return false;
        }
        return recipe is ConversionRecipe conversionRecipe
            ? conversionRecipe.TryGetLockedOutputPlan(itemId, out _)
            : recipe.SupportsMainOutputLock(itemId);
    }

    private static bool IsLineageTargetOutput(RectificationRecipe recipe, int itemId) {
        return recipe != null && recipe.SupportsLineageTarget(itemId);
    }

    private static void OnSlotRightClick(int itemId) {
        UIFractionatorWindow target = sourceWindow ?? modWindow;
        if (target == null || target.fractionatorId == 0 || target.factory == null) {
            return;
        }
        FractionatorComponent fractionator = target.factorySystem.fractionatorPool[target.fractionatorId];
        if (fractionator.id != target.fractionatorId) {
            return;
        }
        int buildingId = target.factory.entityPool[fractionator.entityId].protoId;
        if (itemId == fractionator.fluidId) {
            return;
        }

        BaseRecipe recipe = GetRecipeForBuilding(buildingId, fractionator.fluidId);
        if (recipe?.OutputAppend.Exists(output => output.OutputID == itemId) == true) {
            ToggleByproductDiscard(target, fractionator, recipe);
            return;
        }

        if (buildingId is IFE交互塔 or IFE转化塔) {
            ToggleLockedOutput(target, fractionator, buildingId, itemId);
            return;
        }

        if (buildingId == IFE解析塔) {
            ToggleAnalysisLineageTarget(target, fractionator, itemId);
        }
    }

    private static void ToggleLockedOutput(UIFractionatorWindow target, FractionatorComponent fractionator,
        int buildingId, int itemId) {
        ERecipe recipeType = buildingId == IFE交互塔 ? ERecipe.BuildingTrain : ERecipe.Conversion;
        if (!TowerRuntimeModifierCache.IsMainOutputLockEnabled(recipeType)) {
            return;
        }

        BaseRecipe recipe = GetRecipe<BaseRecipe>(recipeType, fractionator.fluidId);
        if (!IsLockableOutput(recipe, itemId)) {
            return;
        }

        int currentLockedItemId = fractionator.GetLockedOutput(target.factory);
        if (currentLockedItemId == itemId) {
            fractionator.SetLockedOutputAndSync(target.factory, 0, manual: true);
            UIRealtimeTip.Popup("已清除单路锁定".Translate());
        } else {
            fractionator.SetLockedOutputAndSync(target.factory, itemId, manual: true);
            string itemName = LDB.items.Select(itemId)?.name ?? itemId.ToString();
            UIRealtimeTip.Popup(string.Format("已锁定单路产物：{0}".Translate(), itemName));
        }

        DoModWindowUpdate(target);
    }

    private static void ToggleByproductDiscard(UIFractionatorWindow target, FractionatorComponent fractionator,
        BaseRecipe recipe) {
        if (!TowerRuntimeModifierCache.IsByproductDiscardEnabled(recipe.RecipeType)
            || !recipe.IsByproductDiscardCalibrated) {
            return;
        }

        bool enabled = !fractionator.GetNormalizedByproductDiscard(target.factory);
        fractionator.SetByproductDiscardAndSync(target.factory, enabled, manual: true);
        UIRealtimeTip.Popup((enabled ? "已启用副产物弃置" : "已关闭副产物弃置").Translate());
        DoModWindowUpdate(target);
    }

    private static void ToggleAnalysisLineageTarget(UIFractionatorWindow target, FractionatorComponent fractionator,
        int itemId) {
        if (!TowerRuntimeModifierCache.IsMainOutputLockEnabled(ERecipe.Rectification)) {
            return;
        }

        RectificationRecipe recipe = GetRecipe<RectificationRecipe>(ERecipe.Rectification, fractionator.fluidId);
        if (!IsLineageTargetOutput(recipe, itemId)) {
            return;
        }

        int currentTargetItemId = fractionator.GetLineageTarget(target.factory);
        if (currentTargetItemId == itemId) {
            fractionator.SetLineageTargetAndSync(target.factory, 0, manual: true);
            UIRealtimeTip.Popup("已清除谱系方向".Translate());
        } else {
            fractionator.SetLineageTargetAndSync(target.factory, itemId, manual: true);
            string itemName = LDB.items.Select(itemId)?.name ?? itemId.ToString();
            UIRealtimeTip.Popup(string.Format("已设定谱系方向：{0}".Translate(), itemName));
        }

        DoModWindowUpdate(target);
    }

    private static void SetSlotLocked(ProductSlot slot, bool locked) {
        if (slot?.lockIcon == null) {
            return;
        }
        ApplyLockIconStyle(slot.lockIcon);
        slot.lockIcon.gameObject.SetActive(locked);
    }

    private static Image GetLockIconTemplateImage() {
        if (lockIconTemplateImage != null) {
            return lockIconTemplateImage;
        }

        UIStationStorage[] storages = UIRoot.instance?.uiGame?.stationWindow?.storageUIs;
        if (storages == null) {
            return null;
        }

        foreach (UIStationStorage storage in storages) {
            UIButton keepModeButton = storage?.keepModeButton;
            if (keepModeButton == null) {
                continue;
            }

            Image image = keepModeButton.GetComponent<Image>() ?? keepModeButton.GetComponentInChildren<Image>(true);
            if (image != null && image.sprite != null) {
                lockIconTemplateImage = image;
                lockIconTemplateButton = keepModeButton;
                return lockIconTemplateImage;
            }
        }

        return null;
    }

    private static void ApplyLockIconStyle(Image target) {
        Image template = GetLockIconTemplateImage();
        if (target == null || template == null) {
            return;
        }

        target.sprite = template.sprite;
        target.type = template.type;
        target.material = template.material;
        target.preserveAspect = true;
        target.color = GetLockIconHighlightedColor(template);
    }

    private static Color GetLockIconHighlightedColor(Image template) {
        UIButton.Transition[] transitions = lockIconTemplateButton?.transitions;
        if (transitions == null) {
            return Color.white;
        }

        foreach (UIButton.Transition transition in transitions) {
            if (transition?.target != template) {
                continue;
            }

            Color color = transition.normalColor;
            if (color.r == 0f && color.g == 0f && color.b == 0f && color.a == 0f) {
                color = template.color;
            }

            return transition.highlightColorOverride.a > 0f
                ? transition.highlightColorOverride
                : color
                  * new Color(transition.highlightColorMultiplier, transition.highlightColorMultiplier,
                      transition.highlightColorMultiplier, transition.highlightAlphaMultiplier);
        }

        return Color.white;
    }

    private static void UpdateLockStatusUI(BaseRecipe recipe, int lockedOutputId, bool showLockControls) {
        if (lockStateText != null) {
            lockStateText.gameObject.SetActive(showLockControls);
        }
        if (lockHintText != null) {
            lockHintText.gameObject.SetActive(showLockControls);
        }
        if (!showLockControls) {
            return;
        }

        string targetName = lockedOutputId == 0
            ? "未锁定".Translate()
            : LDB.items.Select(lockedOutputId)?.name ?? lockedOutputId.ToString();
        if (lockStateText != null) {
            lockStateText.text = $"{"主路目标".Translate()}：{targetName}";
        }
        if (lockHintText != null) {
            lockHintText.text = recipe == null
                ? string.Empty
                : (lockedOutputId == 0 ? "右键设为单锁".Translate() : "右键清除单锁".Translate());
        }
    }

    private static void UpdateTargetStatusUI(BaseRecipe lockRecipe, int lockedOutputId, bool showLockControls,
        RectificationRecipe rectificationRecipe, int lineageTargetId, bool showLineageControls,
        bool showDiscardControls, bool discardByproducts) {
        bool showControls = showLockControls || showLineageControls || showDiscardControls;
        if (lockStateText != null) {
            lockStateText.gameObject.SetActive(showControls);
        }
        if (lockHintText != null) {
            lockHintText.gameObject.SetActive(showControls);
        }
        if (!showControls) {
            return;
        }

        if (showLockControls) {
            UpdateLockStatusUI(lockRecipe, lockedOutputId, showLockControls);
        } else if (showLineageControls) {
            string targetName = lineageTargetId == 0
                ? "未锁定".Translate()
                : LDB.items.Select(lineageTargetId)?.name ?? lineageTargetId.ToString();
            if (lockStateText != null) {
                lockStateText.text = $"{"谱系方向".Translate()}：{targetName}";
            }
            if (lockHintText != null) {
                lockHintText.text = rectificationRecipe == null
                    ? string.Empty
                    : (lineageTargetId == 0 ? "右键设为谱系方向".Translate() : "右键清除谱系方向".Translate());
            }
        } else {
            if (lockStateText != null) lockStateText.text = string.Empty;
            if (lockHintText != null) lockHintText.text = string.Empty;
        }

        if (showDiscardControls) {
            string discardState = discardByproducts ? "弃置".Translate() : "保留".Translate();
            if (lockStateText != null) {
                lockStateText.text = $"{lockStateText.text}{(lockStateText.text.Length > 0 ? "\r\n" : string.Empty)}"
                                     + $"{"副产物".Translate()}：{discardState}";
            }
            if (lockHintText != null) {
                lockHintText.text = $"{lockHintText.text}{(lockHintText.text.Length > 0 ? "\r\n" : string.Empty)}"
                                    + "右键切换副产物弃置".Translate();
            }
        }
    }
}
