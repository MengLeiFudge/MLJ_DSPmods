using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.RecipeManager;
using static FE.Utils.Utils;

namespace FE.UI.Patches;

public static partial class FEFractionatorWindow {
    private static Image lockIconTemplateImage;
    private static UIButton lockIconTemplateButton;

    private static bool IsLockableOutput(ConversionRecipe recipe, int itemId) {
        if (recipe == null || itemId == 0) {
            return false;
        }
        return recipe.TryGetLockedOutputPlan(itemId, out _);
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
        if (buildingId != IFE转化塔 || !ConversionTower.EnableSingleLock || itemId == fractionator.fluidId) {
            return;
        }

        ConversionRecipe recipe = GetRecipe<ConversionRecipe>(ERecipe.Conversion, fractionator.fluidId);
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
                : color * new Color(transition.highlightColorMultiplier, transition.highlightColorMultiplier,
                    transition.highlightColorMultiplier, transition.highlightAlphaMultiplier);
        }

        return Color.white;
    }

    private static void UpdateLockStatusUI(FractionatorComponent fractionator, ConversionRecipe recipe,
        int lockedOutputId,
        bool showLockControls) {
        if (lockStateText != null) {
            lockStateText.gameObject.SetActive(showLockControls);
        }
        if (lockHintText != null) {
            lockHintText.gameObject.SetActive(showLockControls);
        }
        if (!showLockControls) {
            return;
        }

        string lockState = "未锁定".Translate();
        if (lockedOutputId != 0) {
            lockState = null;
            if (recipe != null && recipe.TryGetLockedOutputPlan(lockedOutputId,
                    out ConversionRecipe.LockedOutputPlan lockedPlan)) {
                float extraOutputCount = lockedPlan.ExtraOutputCount;
                if (extraOutputCount < 0.0001f && extraOutputCount > -0.0001f) {
                    extraOutputCount = 0f;
                }
                lockState = $"{"单锁产物数目".Translate()}+{extraOutputCount.FormatP()}";
            }
        }
        if (lockStateText != null) {
            lockStateText.text = lockState ?? $"{"单锁".Translate()}：{"未锁定".Translate()}";
        }
        if (lockHintText != null) {
            lockHintText.text = recipe == null
                ? string.Empty
                : (lockedOutputId == 0 ? "右键设为单锁".Translate() : "右键清除单锁".Translate());
        }
    }
}
