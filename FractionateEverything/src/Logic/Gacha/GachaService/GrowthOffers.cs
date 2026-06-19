using System.Collections.Generic;
using FE.Logic.DarkFog;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Fractionation.Growth;
using FE.Logic.Fractionation.FracRecipes;
using static FE.Logic.Items.ItemManager;
using static FE.Logic.DataCenter.DataCenterInventory;
using static FE.Utils.Utils;
using static FE.Logic.DataCenter.PlayerInventoryAccess;

namespace FE.Logic.Gacha;

/// <summary>
/// 成长商店报价生成与购买结算逻辑。
/// </summary>
public static partial class GachaService {
    public static int GetDarkFogGrowthOfferCount() {
        int count = 0;
        foreach (GachaGrowthOffer offer in GetGrowthOffers()) {
            if (offer.ExtraCostItemId == I黑雾矩阵) {
                count++;
            }
        }
        return count;
    }

    public static bool IsEnhancedDarkFogRewardItem(int itemId) {
        return FractionatorTowerCatalog.IsActiveFractionatorProto(itemId);
    }

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

        if (IsEssenceCatalystOffer(offer)
            && RecipeGrowthExecutor.CountEssenceCatalystTargets(offer.ExtraCostItemId, requireMaxed: false) <= 0) {
            return false;
        }

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
            if (TryGetGrowthOfferMaxedFragmentPreview(offer, out int fragmentReward)) {
                AddItemToModData(IFE残片, fragmentReward, 0, true);
                reward = new GachaRewardResolution(GachaRewardType.DuplicateRecipeFragments, IFE残片, fragmentReward);
                return true;
            }

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

        if (IsEssenceCatalystOffer(offer)) {
            int affectedRecipeCount = RecipeGrowthExecutor.ApplyEssenceCatalyst(
                offer.ExtraCostItemId,
                offer.OutputCount,
                RecipeGrowthManager.BuildContext(manual: true));
            if (affectedRecipeCount <= 0) {
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
            reward = new GachaRewardResolution(GachaRewardType.RecipeProgress, offer.OutputId,
                affectedRecipeCount);
            return true;
        }

        AddItemToModData(offer.OutputId, offer.OutputCount, 0, true);
        reward = new GachaRewardResolution(GachaRewardType.ItemGranted, offer.OutputId, offer.OutputCount);
        return true;
    }

    public static bool TryGetGrowthOfferMaxedFragmentPreview(GachaGrowthOffer offer, out int fragmentCount) {
        fragmentCount = 0;
        if (IsDarkFogRecipeGrowthOffer(offer)) {
            BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(offer.RecipeType, offer.OutputId);
            if (recipe == null || !RecipeGrowthQueries.IsMaxed(recipe)) {
                return false;
            }

            fragmentCount = GetDuplicateRecipeFragmentReward();
            return true;
        }

        if (!IsDarkFogCatchupOffer(offer)) {
            return false;
        }

        int affectedRecipeCount = 0;
        foreach (BaseRecipe recipe in RecipeManager.AllRecipes) {
            RecipeFamily family = RecipeGrowthRules.GetFamily(recipe);
            if (recipe.InputID != offer.OutputId
                || family is not RecipeFamily.MineralCopyDarkFog and not RecipeFamily.ConversionDarkFogChain) {
                continue;
            }

            affectedRecipeCount++;
            if (!RecipeGrowthQueries.IsMaxed(recipe)) {
                return false;
            }
        }

        if (affectedRecipeCount <= 0) {
            return false;
        }

        fragmentCount = affectedRecipeCount * GetDuplicateRecipeFragmentReward();
        return true;
    }

    private static int GetDuplicateRecipeFragmentReward() {
        bool rectificationFocus = GachaManager.CurrentFocus == GachaFocusType.RectificationEconomy;
        if (GachaManager.IsSpeedrunMode) {
            return rectificationFocus ? 35 : 25;
        }

        return rectificationFocus ? 20 : 15;
    }

    private static IReadOnlyList<GachaGrowthOffer> BuildNormalGrowthOffers() {
        var offers = new List<GachaGrowthOffer> {
            new(5, 0, IFE残片, 50),
            new(10, 10, GetCurrentDrawMatrixId(), 4),
            new(20, 15, GetFocusedEmbryoReward(), 1, GachaManager.CurrentFocus),
            new(36, 30, GetLeastStockedEmbryoReward(), 1, GachaFocusType.EmbryoCycle),
        };

        AppendEssenceCatalystOffer(offers);
        AppendBlackFogOffers(offers);
        return offers;
    }

    private static IReadOnlyList<GachaGrowthOffer> BuildSpeedrunGrowthOffers() {
        var offers = new List<GachaGrowthOffer> {
            new(4, 0, GetCurrentDrawMatrixId(), 6),
            new(8, 6, GetFocusedEmbryoReward(), 1, GachaManager.CurrentFocus),
            new(15, 10, GetLeastStockedEmbryoReward(), 1, GachaFocusType.EmbryoCycle),
        };

        AppendEssenceCatalystOffer(offers, pointCost: 14, fragmentCost: 8);
        AppendBlackFogOffers(offers, pointBaseOffset: -4, fragmentBaseOffset: -4);
        return offers;
    }

    private static void AppendEssenceCatalystOffer(List<GachaGrowthOffer> offers, int pointCost = 22,
        int fragmentCost = 14) {
        int essenceItemId = GetCurrentCatalystEssenceItemId();
        if (essenceItemId <= 0) {
            return;
        }

        int catalystExp = GetEssenceCatalystGrowthExp(essenceItemId);
        offers.Add(new(pointCost, fragmentCost, essenceItemId, catalystExp, GachaFocusType.RectificationEconomy,
            essenceItemId, 1, GachaGrowthOfferKind.EssenceCatalyst, ERecipe.Rectification));
    }

    private static int GetCurrentCatalystEssenceItemId() {
        return GetMatrixEssenceItemId(GetCurrentProgressStageIndex());
    }

    private static int GetEssenceCatalystGrowthExp(int essenceItemId) {
        int faceValue = GetMatrixEssenceFaceValue(essenceItemId);
        return faceValue <= 0 ? 0 : faceValue * (IsSpeedrunMode ? 4 : 3);
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
                offers.Add(new(48 + pointBaseOffset, 32 + fragmentBaseOffset, GetLeastStockedEmbryoReward(), 1,
                    GachaFocusType.EmbryoCycle, I黑雾矩阵, 4));
            }
        }
    }
}
