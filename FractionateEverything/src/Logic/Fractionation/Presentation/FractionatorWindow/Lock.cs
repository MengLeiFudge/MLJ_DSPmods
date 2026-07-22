using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Fractionation.FracRecipes;
using FE.Logic.Fractionation.Process;
using UnityEngine;
using UnityEngine.UI;
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
        return recipe.SupportsMainOutputLock(itemId);
    }

    private static void OnSlotRightClick(ProductSlot slot, int itemId) {
        UIFractionatorWindow target = sourceWindow ?? modWindow;
        if (slot == null || target == null || target.fractionatorId == 0 || target.factory == null) {
            return;
        }
        FractionatorComponent fractionator = target.factorySystem.fractionatorPool[target.fractionatorId];
        if (fractionator.id != target.fractionatorId || slot.kind == ProductSlotKind.Fluid) {
            return;
        }

        int buildingId = target.factory.entityPool[fractionator.entityId].protoId;
        BaseRecipe recipe = GetRecipeForBuilding(buildingId, fractionator.fluidId);
        if (slot.kind == ProductSlotKind.Side) {
            ToggleByproductDiscard(target, fractionator, recipe);
            return;
        }
        ToggleLockedOutput(target, fractionator, recipe, itemId);
    }

    private static void ToggleLockedOutput(UIFractionatorWindow target, FractionatorComponent fractionator,
        BaseRecipe recipe, int itemId) {
        if (recipe == null || !TowerRuntimeModifierCache.IsMainOutputLockEnabled(recipe.RecipeType)
            || !IsLockableOutput(recipe, itemId)) {
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
        if (recipe == null || !TowerRuntimeModifierCache.IsByproductDiscardEnabled(recipe.RecipeType)
            || recipe.OutputAppend.Count == 0 || !recipe.IsByproductDiscardCalibrated) {
            return;
        }

        bool enabled = !fractionator.GetNormalizedByproductDiscard(target.factory);
        fractionator.SetByproductDiscardAndSync(target.factory, enabled, manual: true);
        UIRealtimeTip.Popup((enabled ? "已启用副产物弃置" : "已关闭副产物弃置").Translate());
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

    private static void UpdateTargetStatusUI(BaseRecipe recipe, int lockedOutputId,
        bool mainOutputLockUnlocked, bool byproductDiscardUnlocked, bool discardByproducts) {
        if (lockStateText != null) {
            lockStateText.gameObject.SetActive(true);
            lockStateText.text = BuildMainOutputLockState(recipe, lockedOutputId, mainOutputLockUnlocked)
                                 + "\r\n"
                                 + BuildByproductDiscardState(recipe, byproductDiscardUnlocked, discardByproducts);
        }
        if (lockHintText != null) {
            lockHintText.gameObject.SetActive(true);
            lockHintText.text = BuildMainOutputLockHint(recipe, lockedOutputId, mainOutputLockUnlocked);
            if (CanToggleByproductDiscard(recipe, byproductDiscardUnlocked)) {
                lockHintText.text = $"{lockHintText.text}{(lockHintText.text.Length > 0 ? "\r\n" : string.Empty)}"
                                    + "右键切换副产物弃置".Translate();
            }
        }
    }

    private static string BuildMainOutputLockState(BaseRecipe recipe, int lockedOutputId, bool unlocked) {
        if (!unlocked) {
            return $"{"主路锁定".Translate()}：{"未解锁".Translate()}";
        }
        if (recipe == null) {
            return $"{"主路锁定".Translate()}：{"等待配方".Translate()}";
        }
        if (!recipe.IsMainOutputLockCalibrated) {
            return $"{"主路锁定".Translate()}：{"未校准".Translate()} "
                   + $"{recipe.TotalSuccessCount}/{recipe.MainOutputLockCalibrationThreshold}";
        }
        if (!recipe.OutputMain.Exists(output => recipe.SupportsMainOutputLock(output.OutputID))) {
            return $"{"主路锁定".Translate()}：{"当前无可选主产物".Translate()}";
        }
        string targetName = lockedOutputId == 0
            ? "未锁定".Translate()
            : LDB.items.Select(lockedOutputId)?.name ?? lockedOutputId.ToString();
        return $"{"主路目标".Translate()}：{targetName}";
    }

    private static string BuildMainOutputLockHint(BaseRecipe recipe, int lockedOutputId, bool unlocked) {
        if (!unlocked || recipe == null || !recipe.IsMainOutputLockCalibrated
            || !recipe.OutputMain.Exists(output => recipe.SupportsMainOutputLock(output.OutputID))) {
            return string.Empty;
        }
        return lockedOutputId == 0 ? "右键设为单锁".Translate() : "右键清除单锁".Translate();
    }

    private static string BuildByproductDiscardState(BaseRecipe recipe, bool unlocked, bool enabled) {
        if (!unlocked) {
            return $"{"副产物弃置".Translate()}：{"未解锁".Translate()}";
        }
        if (recipe == null) {
            return $"{"副产物弃置".Translate()}：{"等待配方".Translate()}";
        }
        if (!recipe.IsByproductDiscardCalibrated) {
            return $"{"副产物弃置".Translate()}：{"未校准".Translate()} "
                   + $"{recipe.TotalSuccessCount}/{recipe.ByproductDiscardCalibrationThreshold}";
        }
        if (recipe.OutputAppend.Count == 0) {
            return $"{"副产物弃置".Translate()}：{"当前无副产物".Translate()}";
        }
        return $"{"副产物弃置".Translate()}：{(enabled ? "弃置" : "保留").Translate()}";
    }

    private static bool CanToggleByproductDiscard(BaseRecipe recipe, bool unlocked) {
        return unlocked && recipe != null && recipe.IsByproductDiscardCalibrated && recipe.OutputAppend.Count > 0;
    }
}
