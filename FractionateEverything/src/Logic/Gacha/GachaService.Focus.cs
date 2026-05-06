using System.Collections.Generic;
using FE.Logic.Fractionation.Growth;
using FE.Logic.Fractionation.Recipes;
using UnityEngine;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;
using static FE.Logic.DataCenter.PlayerInventoryAccess;

namespace FE.Logic.Gacha;

/// <summary>
/// 抽取聚焦选项、权重和加成计算逻辑。
/// </summary>
public static partial class GachaService {
    private static readonly GachaFocusDefinition[] focusDefinitions = [
        new(GachaFocusType.Balanced, "聚焦-平衡发展", "聚焦描述-平衡发展"),
        new(GachaFocusType.MineralExpansion, "聚焦-复制扩张", "聚焦描述-复制扩张"),
        new(GachaFocusType.ConversionLeap, "聚焦-转化跃迁", "聚焦描述-转化跃迁"),
        new(GachaFocusType.LogisticsInteraction, "聚焦-交互物流", "聚焦描述-交互物流"),
        new(GachaFocusType.EmbryoCycle, "聚焦-原胚循环", "聚焦描述-原胚循环"),
        new(GachaFocusType.ProcessOptimization, "聚焦-工艺优化", "聚焦描述-工艺优化"),
        new(GachaFocusType.RectificationEconomy, "聚焦-精馏经济", "聚焦描述-精馏经济"),
    ];

    public static IReadOnlyList<GachaFocusDefinition> FocusDefinitions => focusDefinitions;

    public static string GetFocusName(GachaFocusType focusType) {
        foreach (var focus in focusDefinitions) {
            if (focus.FocusType == focusType) {
                return focus.NameKey.Translate();
            }
        }
        return focusType.ToString();
    }

    public static int GetFocusSwitchFragmentCost(GachaFocusType targetFocus) {
        if (IsSpeedrunMode) {
            return 0;
        }
        return targetFocus == GachaManager.CurrentFocus ? 0 : 120;
    }

    public static bool TryChangeFocus(GachaFocusType targetFocus) {
        if (targetFocus == GachaManager.CurrentFocus) {
            return true;
        }
        int fragmentCost = GetFocusSwitchFragmentCost(targetFocus);
        if (fragmentCost > 0 && !TakeItemWithTip(IFE残片, fragmentCost, out _)) {
            return false;
        }

        GachaManager.SetFocus(targetFocus);
        return true;
    }

    private static GachaGrowthOffer ApplyFocusOfferModifier(GachaGrowthOffer offer) {
        if (!IsFocusedGrowthOffer(offer)) {
            return offer;
        }

        float discountFactor = GetFocusedOfferDiscountFactor();
        int pointCost = Mathf.Max(1, Mathf.RoundToInt(offer.PointCost * discountFactor));
        int fragmentCost = offer.FragmentCost <= 0
            ? 0
            : Mathf.Max(0, Mathf.RoundToInt(offer.FragmentCost * discountFactor));
        int outputCount = offer.OutputCount;

        if (IsCoreGrowthReward(offer)) {
            outputCount += 1;
            if (offer.OutputId == IFE残片) {
                outputCount += 10;
            }
        }

        return new GachaGrowthOffer(pointCost, fragmentCost, offer.OutputId, outputCount, offer.FocusType,
            offer.ExtraCostItemId, offer.ExtraCostCount, offer.OfferKind, offer.RecipeType);
    }

    public static bool IsFocusedGrowthOffer(GachaGrowthOffer offer) {
        return offer.FocusType != GachaFocusType.Balanced && offer.FocusType == GachaManager.CurrentFocus;
    }

    public static float GetFocusedOfferDiscountFactor() {
        return IsSpeedrunMode ? 0.85f : 0.80f;
    }

    public static bool IsCoreGrowthReward(GachaGrowthOffer offer) {
        return offer.OutputId == GetFocusedEmbryoReward()
               || offer.OutputId == IFE分馏塔定向原胚
               || offer.OutputId == IFE残片;
    }

    public static bool IsDarkFogCatchupOffer(GachaGrowthOffer offer) {
        return offer.OfferKind == GachaGrowthOfferKind.DarkFogCatchup;
    }

    public static bool IsDarkFogRecipeGrowthOffer(GachaGrowthOffer offer) {
        return offer.OfferKind == GachaGrowthOfferKind.DarkFogRecipeGrowth;
    }

    private static float GetOpeningRecipeFocusMultiplier(BaseRecipe recipe, int currentStageIndex) {
        GachaFocusType focus = GachaManager.CurrentFocus;
        if (focus == GachaFocusType.Balanced) {
            return 1f;
        }

        float mainMultiplier = IsSpeedrunMode ? 1.6f : 1.4f;
        float sideMultiplier = IsSpeedrunMode ? 1.3f : 1.2f;

        if (focus == GachaFocusType.ProcessOptimization && GetMatrixStageIndex(recipe.MatrixID) == currentStageIndex) {
            return sideMultiplier;
        }
        if (focus == GachaFocusType.LogisticsInteraction && IsLogisticsRecipe(recipe.InputID)) {
            return mainMultiplier;
        }
        if (focus == GachaFocusType.EmbryoCycle && !RecipeGrowthQueries.IsUnlocked(recipe)) {
            return sideMultiplier;
        }

        return recipe.RecipeType switch {
            ERecipe.MineralCopy when focus == GachaFocusType.MineralExpansion => mainMultiplier,
            ERecipe.Conversion when focus == GachaFocusType.ConversionLeap => mainMultiplier,
            _ => 1f,
        };
    }

    private static int GetFocusedEmbryoReward() {
        return GachaManager.CurrentFocus switch {
            GachaFocusType.MineralExpansion => IFE矿物复制塔原胚,
            GachaFocusType.ConversionLeap => IFE转化塔原胚,
            GachaFocusType.LogisticsInteraction => IFE交互塔原胚,
            GachaFocusType.EmbryoCycle => IFE分馏塔定向原胚,
            GachaFocusType.ProcessOptimization => IFE点数聚集塔原胚,
            GachaFocusType.RectificationEconomy => IFE精馏塔原胚,
            _ => IFE交互塔原胚,
        };
    }

    private static GachaFocusMatchType GetFocusMatchType(int poolId, int itemId) {
        if (GachaManager.CurrentFocus == GachaFocusType.Balanced) {
            return GachaFocusMatchType.None;
        }
        if (GachaPool.IsRecipePool(poolId)) {
            BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(ERecipe.MineralCopy, itemId)
                                ?? RecipeManager.GetRecipe<BaseRecipe>(ERecipe.Conversion, itemId);
            if (recipe == null) {
                return GachaFocusMatchType.None;
            }
            if (GachaManager.CurrentFocus == GachaFocusType.LogisticsInteraction && IsLogisticsRecipe(recipe.InputID)) {
                return GachaFocusMatchType.Main;
            }
            if (GachaManager.CurrentFocus == GachaFocusType.EmbryoCycle && !RecipeGrowthQueries.IsUnlocked(recipe)) {
                return GachaFocusMatchType.Main;
            }
            if (GachaManager.CurrentFocus == GachaFocusType.ProcessOptimization
                && GetMatrixStageIndex(recipe.MatrixID) == GetCurrentProgressStageIndex()) {
                return GachaFocusMatchType.Main;
            }
            if (GachaManager.CurrentFocus == GachaFocusType.RectificationEconomy
                && RecipeGrowthQueries.IsMaxed(recipe)) {
                return GachaFocusMatchType.Side;
            }
            return recipe.RecipeType switch {
                ERecipe.MineralCopy when GachaManager.CurrentFocus == GachaFocusType.MineralExpansion =>
                    GachaFocusMatchType.Main,
                ERecipe.Conversion when GachaManager.CurrentFocus == GachaFocusType.ConversionLeap =>
                    GachaFocusMatchType.Main,
                _ => GachaFocusMatchType.None,
            };
        }
        if (GachaPool.IsProtoLoopPool(poolId)) {
            if (itemId == GetFocusedEmbryoReward()) {
                return GachaFocusMatchType.Main;
            }
            if (GachaManager.CurrentFocus == GachaFocusType.EmbryoCycle && itemId == IFE分馏塔定向原胚) {
                return GachaFocusMatchType.Side;
            }
        }
        return GachaFocusMatchType.None;
    }

    private static bool IsLogisticsRecipe(int inputId) {
        return inputId switch {
            I配送运输机 or I物流运输机 or I星际物流运输船
                or I物流配送器 or I行星内物流运输站 or I星际物流运输站 or I轨道采集器
                or I传送带 or I高速传送带 or I极速传送带
                or I四向分流器 or I流速监测器 or I自动集装机
                or I分拣器 or I高速分拣器 or I极速分拣器 or I集装分拣器
                or I小型储物仓 or I大型储物仓 or I储液罐 => true,
            _ => false,
        };
    }
}
