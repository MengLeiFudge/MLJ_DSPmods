using System;
using System.Collections.Generic;
using System.Linq;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Fractionation.Growth;
using UnityEngine;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static partial class GachaService {
    public static IReadOnlyList<GachaGrowthOffer> GetGrowthOffers() {
        IReadOnlyList<GachaGrowthOffer> baseOffers = IsSpeedrunMode
            ? BuildSpeedrunGrowthOffers()
            : BuildNormalGrowthOffers();
        List<GachaGrowthOffer> adjusted = new(baseOffers.Count);
        foreach (GachaGrowthOffer offer in baseOffers) {
            adjusted.Add(ApplyFocusOfferModifier(offer));
        }
        return adjusted;
    }

    internal static bool TryExchangeGrowthOffer(GachaGrowthOffer offer, out GachaRewardResolution reward) {
        reward = new GachaRewardResolution(GachaRewardType.None, 0, 0);

        if (offer.PointCost > 0 && !GachaManager.TryConsumePoolPoints(GachaPool.PoolIdGrowth, offer.PointCost)) {
            return false;
        }
        if (offer.FragmentCost > 0 && !TakeItemWithTip(IFE残片, offer.FragmentCost, out _)) {
            if (offer.PointCost > 0) {
                GachaManager.AddPoolPoints(GachaPool.PoolIdGrowth, offer.PointCost);
            }
            return false;
        }
        if (offer.ExtraCostItemId > 0 && !TakeItemWithTip(offer.ExtraCostItemId, offer.ExtraCostCount, out _)) {
            if (offer.PointCost > 0) {
                GachaManager.AddPoolPoints(GachaPool.PoolIdGrowth, offer.PointCost);
            }
            if (offer.FragmentCost > 0) {
                AddItemToModData(IFE残片, offer.FragmentCost, 0, true);
            }
            return false;
        }

        if (IsDarkFogCatchupOffer(offer)) {
            int appliedRecipeCount = RecipeGrowthExecutor.ApplyDarkFogCatchupByItem(
                offer.OutputId,
                offer.OutputCount,
                RecipeGrowthManager.BuildContext(manual: true));
            reward = new GachaRewardResolution(GachaRewardType.ItemGranted, offer.OutputId,
                appliedRecipeCount > 0 ? offer.OutputCount : 0);
            return true;
        }

        if (IsDarkFogRecipeGrowthOffer(offer)) {
            BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(offer.RecipeType, offer.OutputId);
            if (recipe == null) {
                if (offer.PointCost > 0) {
                    GachaManager.AddPoolPoints(GachaPool.PoolIdGrowth, offer.PointCost);
                }
                if (offer.FragmentCost > 0) {
                    AddItemToModData(IFE残片, offer.FragmentCost, 0, true);
                }
                if (offer.ExtraCostItemId > 0) {
                    AddItemToModData(offer.ExtraCostItemId, offer.ExtraCostCount, 0, true);
                }
                return false;
            }

            bool wasLocked = !RecipeGrowthQueries.IsUnlocked(recipe);
            RecipeGrowthResult growthResult = RecipeGrowthExecutor.ApplyDrawReward(recipe,
                RecipeGrowthManager.BuildContext(manual: true));
            if (growthResult.FragmentReward > 0) {
                AddItemToModData(IFE残片, growthResult.FragmentReward, 0, true);
                reward = new GachaRewardResolution(GachaRewardType.DuplicateRecipeFragments, IFE残片,
                    growthResult.FragmentReward);
                return true;
            }

            reward = new GachaRewardResolution(
                wasLocked ? GachaRewardType.RecipeUnlock : GachaRewardType.RecipeUpgrade,
                offer.OutputId,
                RecipeGrowthQueries.GetLevel(recipe));
            return true;
        }

        AddItemToModData(offer.OutputId, offer.OutputCount, 0, true);
        reward = new GachaRewardResolution(GachaRewardType.ItemGranted, offer.OutputId, offer.OutputCount);
        return true;
    }

    private static IReadOnlyList<GachaGrowthOffer> BuildNormalGrowthOffers() {
        var offers = new List<GachaGrowthOffer> {
            new(5, 0, IFE残片, 50),
            new(10, 10, GetCurrentDrawMatrixId(), 4),
            new(20, 15, GetFocusedEmbryoReward(), 1, GachaManager.CurrentFocus),
            new(36, 30, IFE分馏塔定向原胚, 1, GachaFocusType.EmbryoCycle),
        };

        AppendBlackFogOffers(offers);
        return offers;
    }

    private static IReadOnlyList<GachaGrowthOffer> BuildSpeedrunGrowthOffers() {
        var offers = new List<GachaGrowthOffer> {
            new(4, 0, GetCurrentDrawMatrixId(), 6),
            new(8, 6, GetFocusedEmbryoReward(), 1, GachaManager.CurrentFocus),
            new(15, 10, IFE分馏塔定向原胚, 1, GachaFocusType.EmbryoCycle),
        };

        AppendBlackFogOffers(offers, pointBaseOffset: -4, fragmentBaseOffset: -4);
        return offers;
    }

    private static void AppendBlackFogOffers(List<GachaGrowthOffer> offers, int pointBaseOffset = 0,
        int fragmentBaseOffset = 0) {
        if (!DarkFogCombatManager.IsGrowthOfferUnlocked()) {
            return;
        }

        EDarkFogCombatStage stage = DarkFogCombatManager.GetCurrentStage();
        int enhancedNodeCount = DarkFogCombatManager.GetEnhancedNodeCount();

        if (stage >= EDarkFogCombatStage.Signal) {
            offers.Add(new(18 + pointBaseOffset, 12 + fragmentBaseOffset, I能量碎片,
                RecipeGrowthCatchup.GetDarkFogCatchupBase(EDarkFogCombatStage.Signal),
                GachaFocusType.RectificationEconomy, I黑雾矩阵, 1, GachaGrowthOfferKind.DarkFogCatchup));
        }
        if (stage >= EDarkFogCombatStage.GroundSuppression) {
            offers.Add(new(26 + pointBaseOffset, 16 + fragmentBaseOffset, I物质重组器,
                RecipeGrowthCatchup.GetDarkFogCatchupBase(EDarkFogCombatStage.GroundSuppression),
                GachaFocusType.ConversionLeap, I黑雾矩阵, 2, GachaGrowthOfferKind.DarkFogCatchup));
            offers.Add(new(30 + pointBaseOffset, 18 + fragmentBaseOffset, I硅基神经元,
                RecipeGrowthCatchup.GetDarkFogCatchupBase(EDarkFogCombatStage.GroundSuppression),
                GachaFocusType.ProcessOptimization, I黑雾矩阵, 2, GachaGrowthOfferKind.DarkFogCatchup));
            offers.Add(new(26 + pointBaseOffset, 16 + fragmentBaseOffset, I重组式制造台, 1,
                GachaFocusType.ConversionLeap, I黑雾矩阵, 2,
                GachaGrowthOfferKind.DarkFogRecipeGrowth, ERecipe.Conversion));
            offers.Add(new(30 + pointBaseOffset, 18 + fragmentBaseOffset, I自演化研究站, 1,
                GachaFocusType.ConversionLeap, I黑雾矩阵, 2,
                GachaGrowthOfferKind.DarkFogRecipeGrowth, ERecipe.Conversion));
        }
        if (stage >= EDarkFogCombatStage.StellarHunt) {
            offers.Add(new(38 + pointBaseOffset, 24 + fragmentBaseOffset, I负熵奇点,
                RecipeGrowthCatchup.GetDarkFogCatchupBase(EDarkFogCombatStage.StellarHunt),
                GachaFocusType.RectificationEconomy, I黑雾矩阵, 3, GachaGrowthOfferKind.DarkFogCatchup));
            offers.Add(new(38 + pointBaseOffset, 24 + fragmentBaseOffset, I负熵熔炉, 1,
                GachaFocusType.ConversionLeap, I黑雾矩阵, 3,
                GachaGrowthOfferKind.DarkFogRecipeGrowth, ERecipe.Conversion));
        }
        if (stage >= EDarkFogCombatStage.Singularity) {
            offers.Add(new(45 + pointBaseOffset, 30 + fragmentBaseOffset, I核心素,
                RecipeGrowthCatchup.GetDarkFogCatchupBase(EDarkFogCombatStage.Singularity),
                GachaFocusType.EmbryoCycle, I黑雾矩阵, 4, GachaGrowthOfferKind.DarkFogCatchup));
            offers.Add(new(45 + pointBaseOffset, 30 + fragmentBaseOffset, I奇异湮灭燃料棒, 1,
                GachaFocusType.ConversionLeap, I黑雾矩阵, 4,
                GachaGrowthOfferKind.DarkFogRecipeGrowth, ERecipe.Conversion));
        }
        if (DarkFogCombatManager.IsEnhancedLayerEnabled() && stage >= EDarkFogCombatStage.Singularity) {
            if (enhancedNodeCount >= 2) {
                offers.Add(new(48 + pointBaseOffset, 32 + fragmentBaseOffset, IFE分馏塔定向原胚, 1,
                    GachaFocusType.EmbryoCycle, I黑雾矩阵, 4));
            }
        }
    }
}
