using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FE.Logic.Recipe;
using FE.Logic.RecipeGrowth;
using UnityEngine;
using FE.Utils;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

/// <summary>
/// 限时市场板。
/// 给普通玩家提供可理解的机会单。
/// </summary>
public static class MarketBoardManager {
    private const int MaxActiveOfferCount = 8;

    public enum MarketOfferType {
        BuyFromPlayer = 0,
        SellToPlayer = 1,
        StageSupply = 2,
        Special = 3,
    }

    public readonly struct MarketOffer(
        int offerId,
        MarketOfferType offerType,
        int inputItemId,
        int inputCount,
        int extraInputItemId,
        int extraInputCount,
        int outputItemId,
        int outputCount,
        long expireTick,
        int refreshVersion) {
        public int OfferId { get; } = offerId;
        public MarketOfferType OfferType { get; } = offerType;
        public int InputItemId { get; } = inputItemId;
        public int InputCount { get; } = inputCount;
        public int ExtraInputItemId { get; } = extraInputItemId;
        public int ExtraInputCount { get; } = extraInputCount;
        public int OutputItemId { get; } = outputItemId;
        public int OutputCount { get; } = outputCount;
        public long ExpireTick { get; } = expireTick;
        public int RefreshVersion { get; } = refreshVersion;
    }

    private static readonly System.Random rng = new(20260404);
    private static readonly List<MarketOffer> activeOffers = [];
    private static int nextOfferId = 1;
    private static long currentExpireTick;

    public static IReadOnlyList<MarketOffer> ActiveOffers => activeOffers;
    public static long CurrentExpireTick => currentExpireTick;

    public static void Init() {
        activeOffers.Clear();
        nextOfferId = 1;
        currentExpireTick = 0L;
        RefreshOffers();
    }

    public static void Tick() {
        long interval = GachaManager.IsSpeedrunMode ? 60L * 60L * 20L : 60L * 60L * 60L;
        if (activeOffers.Count == 0
            || GameMain.gameTick >= currentExpireTick
            || GameMain.gameTick + interval < currentExpireTick) {
            RefreshOffers();
        }
    }

    public static void HandleMarketValueRefreshed() {
        if (activeOffers.Count == 0) {
            RefreshOffers();
        }
    }

    public static bool TryExecuteOffer(int offerId) {
        int index = activeOffers.FindIndex(offer => offer.OfferId == offerId);
        if (index < 0) {
            return false;
        }

        MarketOffer offer = activeOffers[index];
        if (offer.ExpireTick < GameMain.gameTick) {
            return false;
        }

        if (offer.InputItemId > 0 && !TakeItemWithTip(offer.InputItemId, offer.InputCount, out _)) {
            return false;
        }
        if (offer.ExtraInputItemId > 0 && !TakeItemWithTip(offer.ExtraInputItemId, offer.ExtraInputCount, out _)) {
            if (offer.InputItemId > 0) {
                AddItemToModData(offer.InputItemId, offer.InputCount, 0, true);
            }
            return false;
        }

        bool success;
        if (IsDarkFogRecipeBackfillOffer(offer)) {
            success = TryApplyDarkFogRecipeBackfill(offer);
        } else if (DarkFogCombatManager.IsDarkFogOffer(offer)
            && !DarkFogCombatManager.IsEnhancedRewardItem(offer.OutputItemId)) {
            success = TryApplyDarkFogResourceBackfill(offer);
        } else {
            AddItemToModData(offer.OutputItemId, offer.OutputCount, 0, true);
            success = true;
        }

        if (success) {
            activeOffers.RemoveAt(index);
        }
        return success;
    }

    private static bool TryApplyDarkFogResourceBackfill(MarketOffer offer) {
        int appliedRecipeCount = RecipeGrowthExecutor.ApplyDarkFogCatchupByItem(offer.OutputItemId, offer.OutputCount,
            RecipeGrowthManager.BuildContext(manual: true));
        if (appliedRecipeCount > 0) {
            return true;
        }

        RefundOfferCost(offer);
        return false;
    }

    private static bool TryApplyDarkFogRecipeBackfill(MarketOffer offer) {
        BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(ERecipe.Conversion, offer.OutputItemId);
        if (recipe == null || RecipeGrowthQueries.IsMaxed(recipe)) {
            RefundOfferCost(offer);
            return false;
        }

        RecipeGrowthResult result = RecipeGrowthExecutor.ApplyDrawReward(recipe,
                RecipeGrowthManager.BuildContext(manual: true));
        if (result.FragmentReward > 0) {
            AddItemToModData(IFE残片, result.FragmentReward, 0, true);
        }
        return true;
    }

    private static void RefundOfferCost(MarketOffer offer) {
        if (offer.InputItemId > 0 && offer.InputCount > 0) {
            AddItemToModData(offer.InputItemId, offer.InputCount, 0, true);
        }
        if (offer.ExtraInputItemId > 0 && offer.ExtraInputCount > 0) {
            AddItemToModData(offer.ExtraInputItemId, offer.ExtraInputCount, 0, true);
        }
    }

    private static void RefreshOffers() {
        activeOffers.Clear();
        int currentMatrixId = GetCurrentProgressMatrixId();
        IReadOnlyList<int> highDemand = MarketValueManager.GetTopMarketItems(12, descending: true);
        var usedItems = new HashSet<int>();
        long interval = GachaManager.IsSpeedrunMode ? 60L * 60L * 20L : 60L * 60L * 60L;
        currentExpireTick = GameMain.gameTick + interval;

        TryAddShortageSupplyOffer(highDemand, usedItems);
        TryAddShortageSupplyOffer(highDemand, usedItems);
        TryAddShortageSupplyOffer(highDemand, usedItems);
        TryAddStageMatrixSupplyOffer(currentMatrixId);
        AppendDarkFogBackfillOffers(currentExpireTick);
    }

    private static void TryAddShortageSupplyOffer(IReadOnlyList<int> candidates, HashSet<int> usedItems) {
        if (activeOffers.Count >= MaxActiveOfferCount) {
            return;
        }

        int itemId = PickCandidate(candidates, usedItems);
        if (itemId <= 0) {
            return;
        }
        usedItems.Add(itemId);
        int count = GetSuggestedTradeCount(itemId, buyFromPlayer: false);
        int fragments = GetFragmentCost(itemId, count, 0.95f);
        activeOffers.Add(new MarketOffer(nextOfferId++, MarketOfferType.SellToPlayer,
            IFE残片, fragments, 0, 0, itemId, count, currentExpireTick, MarketValueManager.RefreshVersion));
    }

    private static void TryAddStageMatrixSupplyOffer(int matrixId) {
        if (activeOffers.Count >= MaxActiveOfferCount || matrixId <= 0) {
            return;
        }

        int count = GetStageMatrixSupplyCount(matrixId);
        int fragments = GetFragmentCost(matrixId, count, 0.92f);
        activeOffers.Add(new MarketOffer(nextOfferId++, MarketOfferType.StageSupply,
            IFE残片, fragments, 0, 0, matrixId, count, currentExpireTick, MarketValueManager.RefreshVersion));
    }

    private static void AppendDarkFogBackfillOffers(long expireTick) {
        TryAddDarkFogResourceBackfill(EDarkFogCombatStage.Signal, I能量碎片, I黑雾矩阵, 1, expireTick);
        TryAddDarkFogResourceBackfill(EDarkFogCombatStage.GroundSuppression, I物质重组器, I黑雾矩阵, 2, expireTick);
        TryAddDarkFogResourceBackfill(EDarkFogCombatStage.GroundSuppression, I硅基神经元, I黑雾矩阵, 2, expireTick);
        TryAddDarkFogRecipeBackfill(EDarkFogCombatStage.GroundSuppression, I重组式制造台, I黑雾矩阵, 2,
            expireTick);
        TryAddDarkFogRecipeBackfill(EDarkFogCombatStage.GroundSuppression, I自演化研究站, I黑雾矩阵, 2,
            expireTick);
        TryAddDarkFogResourceBackfill(EDarkFogCombatStage.StellarHunt, I负熵奇点, I黑雾矩阵, 3, expireTick);
        TryAddDarkFogRecipeBackfill(EDarkFogCombatStage.StellarHunt, I负熵熔炉, I黑雾矩阵, 3, expireTick);
        TryAddDarkFogResourceBackfill(EDarkFogCombatStage.Singularity, I核心素, I黑雾矩阵, 4, expireTick);
        TryAddDarkFogRecipeBackfill(EDarkFogCombatStage.Singularity, I奇异湮灭燃料棒, I黑雾矩阵, 4, expireTick);

        if (DarkFogCombatManager.IsEnhancedLayerEnabled()
            && DarkFogCombatManager.GetCurrentStage() >= EDarkFogCombatStage.Singularity
            && DarkFogCombatManager.GetEnhancedNodeCount() >= 2) {
            TryAddEnhancedDarkFogOffer(expireTick);
        }
    }

    private static void TryAddDarkFogResourceBackfill(EDarkFogCombatStage requiredStage, int itemId,
        int extraCostItemId, int extraCostCount, long expireTick) {
        if (activeOffers.Count >= MaxActiveOfferCount
            || DarkFogCombatManager.GetCurrentStage() < requiredStage
            || !HasDarkFogResourceGrowthTarget(itemId)) {
            return;
        }

        int growthExp = RecipeGrowthCatchup.GetDarkFogCatchupBase(requiredStage);
        int fragments = GetDarkFogBackfillFragmentCost(requiredStage, recipeBackfill: false);
        activeOffers.Add(new MarketOffer(nextOfferId++, MarketOfferType.Special,
            IFE残片, fragments, extraCostItemId, extraCostCount, itemId, growthExp, expireTick,
            MarketValueManager.RefreshVersion));
    }

    private static void TryAddDarkFogRecipeBackfill(EDarkFogCombatStage requiredStage, int itemId,
        int extraCostItemId, int extraCostCount, long expireTick) {
        if (activeOffers.Count >= MaxActiveOfferCount
            || DarkFogCombatManager.GetCurrentStage() < requiredStage
            || !HasDarkFogRecipeBackfillTarget(itemId)) {
            return;
        }

        int fragments = GetDarkFogBackfillFragmentCost(requiredStage, recipeBackfill: true);
        activeOffers.Add(new MarketOffer(nextOfferId++, MarketOfferType.Special,
            IFE残片, fragments, extraCostItemId, extraCostCount, itemId, 1, expireTick,
            MarketValueManager.RefreshVersion));
    }

    private static void TryAddEnhancedDarkFogOffer(long expireTick) {
        if (activeOffers.Count >= MaxActiveOfferCount) {
            return;
        }

        activeOffers.Add(new MarketOffer(nextOfferId++, MarketOfferType.Special,
            IFE残片, 720, I黑雾矩阵, 4, IFE分馏塔定向原胚, 1, expireTick, MarketValueManager.RefreshVersion));
    }

    private static bool HasDarkFogResourceGrowthTarget(int itemId) {
        foreach (BaseRecipe recipe in RecipeManager.AllRecipes) {
            RecipeFamily family = RecipeGrowthRules.GetFamily(recipe);
            if (recipe.InputID == itemId
                && family is RecipeFamily.MineralCopyDarkFog or RecipeFamily.ConversionMaterialDarkFog
                && !RecipeGrowthQueries.IsMaxed(recipe)) {
                return true;
            }
        }
        return false;
    }

    private static bool HasDarkFogRecipeBackfillTarget(int itemId) {
        BaseRecipe recipe = RecipeManager.GetRecipe<BaseRecipe>(ERecipe.Conversion, itemId);
        return recipe != null && !RecipeGrowthQueries.IsMaxed(recipe);
    }

    public static bool IsDarkFogRecipeBackfillOffer(MarketOffer offer) {
        return offer.OfferType == MarketOfferType.Special
               && offer.ExtraInputItemId == I黑雾矩阵
               && IsDarkFogRecipeBackfillItem(offer.OutputItemId);
    }

    public static bool IsDarkFogRecipeBackfillItem(int itemId) {
        return itemId is I重组式制造台 or I自演化研究站 or I负熵熔炉 or I奇异湮灭燃料棒;
    }

    private static bool IsBoardFriendly(int itemId) {
        return MarketValueManager.CanParticipateInEconomy(itemId) && itemId != IFE残片;
    }

    private static int PickCandidate(IReadOnlyList<int> candidates, HashSet<int> usedItems) {
        List<int> filtered = [];
        foreach (int itemId in candidates) {
            if (!IsBoardFriendly(itemId) || usedItems.Contains(itemId)) {
                continue;
            }
            filtered.Add(itemId);
        }
        if (filtered.Count == 0) {
            return 0;
        }
        int poolSize = Math.Min(filtered.Count, 4);
        return filtered[rng.Next(poolSize)];
    }

    private static int GetSuggestedTradeCount(int itemId, bool buyFromPlayer) {
        int stack = Math.Max(1, LDB.items.Select(itemId)?.StackSize ?? 1);
        float baseValue = Math.Max(1f, MarketValueManager.GetBaseValue(itemId));
        int rough = baseValue switch {
            <= 2f => 200,
            <= 8f => 100,
            <= 25f => 40,
            <= 80f => 20,
            _ => 5,
        };
        if (!buyFromPlayer) {
            rough = Math.Max(1, rough / 2);
        }
        return Math.Max(1, Mathf.CeilToInt((float)rough / stack) * stack);
    }

    private static int GetStageMatrixSupplyCount(int matrixId) {
        return GetMatrixStageIndex(matrixId) switch {
            <= 1 => 64,
            2 => 48,
            3 => 32,
            4 => 24,
            5 => 16,
            _ => 8,
        };
    }

    private static int GetFragmentCost(int itemId, int count, float ratio) {
        float value = MarketValueManager.GetValue(itemId);
        if (value <= 0f) {
            value = MarketValueManager.GetBaseValue(itemId);
        }
        return Mathf.Max(1, Mathf.RoundToInt(value * count * ratio));
    }

    private static int GetDarkFogBackfillFragmentCost(EDarkFogCombatStage stage, bool recipeBackfill) {
        int baseCost = stage switch {
            EDarkFogCombatStage.Signal => 28,
            EDarkFogCombatStage.GroundSuppression => 44,
            EDarkFogCombatStage.StellarHunt => 64,
            EDarkFogCombatStage.Singularity => 82,
            _ => 24,
        };
        return recipeBackfill ? baseCost + 12 : baseCost;
    }

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("Offers", br => {
                activeOffers.Clear();
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++) {
                    activeOffers.Add(new MarketOffer(
                        br.ReadInt32(),
                        (MarketOfferType)br.ReadInt32(),
                        br.ReadInt32(),
                        br.ReadInt32(),
                        br.ReadInt32(),
                        br.ReadInt32(),
                        br.ReadInt32(),
                        br.ReadInt32(),
                        br.ReadInt64(),
                        br.ReadInt32()));
                }
            }),
            ("Meta", br => {
                nextOfferId = br.ReadInt32();
                currentExpireTick = br.ReadInt64();
            })
        );
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("Offers", bw => {
                bw.Write(activeOffers.Count);
                foreach (MarketOffer offer in activeOffers) {
                    bw.Write(offer.OfferId);
                    bw.Write((int)offer.OfferType);
                    bw.Write(offer.InputItemId);
                    bw.Write(offer.InputCount);
                    bw.Write(offer.ExtraInputItemId);
                    bw.Write(offer.ExtraInputCount);
                    bw.Write(offer.OutputItemId);
                    bw.Write(offer.OutputCount);
                    bw.Write(offer.ExpireTick);
                    bw.Write(offer.RefreshVersion);
                }
            }),
            ("Meta", bw => {
                bw.Write(nextOfferId);
                bw.Write(currentExpireTick);
            })
        );
    }

    public static void IntoOtherSave() {
        Init();
    }
}
