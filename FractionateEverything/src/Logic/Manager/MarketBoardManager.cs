using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        if (activeOffers.Count == 0 || GameMain.gameTick >= currentExpireTick || GameMain.gameTick + interval < currentExpireTick) {
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

        if (DarkFogCombatManager.IsDarkFogOffer(offer) && !DarkFogCombatManager.IsEnhancedRewardItem(offer.OutputItemId)) {
            RecipeGrowthExecutor.ApplyDarkFogCatchupByItem(offer.OutputItemId, offer.OutputCount,
                RecipeGrowthManager.BuildContext(manual: true));
            return true;
        }

        AddItemToModData(offer.OutputItemId, offer.OutputCount, 0, true);
        return true;
    }

    private static void RefreshOffers() {
        activeOffers.Clear();
        int currentMatrixId = GetCurrentProgressMatrixId();
        IReadOnlyList<int> highDemand = MarketValueManager.GetTopMarketItems(12, descending: true);
        IReadOnlyList<int> lowDemand = MarketValueManager.GetTopMarketItems(12, descending: false);
        var usedItems = new HashSet<int>();
        long interval = GachaManager.IsSpeedrunMode ? 60L * 60L * 20L : 60L * 60L * 60L;
        currentExpireTick = GameMain.gameTick + interval;

        TryAddBuyOffer(highDemand, usedItems);
        TryAddBuyOffer(highDemand, usedItems);
        TryAddSellOffer(lowDemand, usedItems);
        TryAddSellOffer(lowDemand, usedItems);
        activeOffers.Add(new MarketOffer(nextOfferId++, MarketOfferType.StageSupply,
            IFE残片, 120, 0, 0, currentMatrixId, 64, currentExpireTick, MarketValueManager.RefreshVersion));
        activeOffers.Add(CreateSpecialOffer(currentExpireTick));
    }

    private static MarketOffer CreateSpecialOffer(long expireTick) {
        if (!DarkFogCombatManager.IsSpecialOrderUnlocked()) {
            int protoReward = GachaService.GetCurrentDrawMatrixId();
            return new MarketOffer(nextOfferId++, MarketOfferType.Special,
                IFE残片, 180, 0, 0, protoReward, 48, expireTick, MarketValueManager.RefreshVersion);
        }

        EDarkFogCombatStage stage = DarkFogCombatManager.GetCurrentStage();
        int enhancedNodeCount = DarkFogCombatManager.GetEnhancedNodeCount();
        if (DarkFogCombatManager.IsEnhancedLayerEnabled() && stage >= EDarkFogCombatStage.Singularity && enhancedNodeCount >= 2) {
            return new MarketOffer(nextOfferId++, MarketOfferType.Special,
                IFE残片, 720, I黑雾矩阵, 4, IFE分馏塔定向原胚, 1, expireTick, MarketValueManager.RefreshVersion);
        }

        return stage switch {
            EDarkFogCombatStage.GroundSuppression => new MarketOffer(nextOfferId++, MarketOfferType.Special,
                IFE残片, 320, I黑雾矩阵, 2, I物质重组器, 6, expireTick, MarketValueManager.RefreshVersion),
            EDarkFogCombatStage.StellarHunt => new MarketOffer(nextOfferId++, MarketOfferType.Special,
                IFE残片, 480, I黑雾矩阵, 3, I负熵奇点, 2, expireTick, MarketValueManager.RefreshVersion),
            EDarkFogCombatStage.Singularity => new MarketOffer(nextOfferId++, MarketOfferType.Special,
                IFE残片, 600, I黑雾矩阵, 4, I核心素, 1, expireTick, MarketValueManager.RefreshVersion),
            _ => new MarketOffer(nextOfferId++, MarketOfferType.Special,
                IFE残片, 240, I黑雾矩阵, 1, I能量碎片, 24, expireTick, MarketValueManager.RefreshVersion),
        };
    }

    private static void TryAddBuyOffer(IReadOnlyList<int> candidates, HashSet<int> usedItems) {
        int itemId = PickCandidate(candidates, usedItems);
        if (itemId <= 0) {
            return;
        }
        usedItems.Add(itemId);
        int count = GetSuggestedTradeCount(itemId, buyFromPlayer: true);
        int fragments = Mathf.Max(1, Mathf.RoundToInt(MarketValueManager.GetValue(itemId) * count * 1.10f));
        activeOffers.Add(new MarketOffer(nextOfferId++, MarketOfferType.BuyFromPlayer,
            itemId, count, 0, 0, IFE残片, fragments, currentExpireTick, MarketValueManager.RefreshVersion));
    }

    private static void TryAddSellOffer(IReadOnlyList<int> candidates, HashSet<int> usedItems) {
        int itemId = PickCandidate(candidates, usedItems);
        if (itemId <= 0) {
            return;
        }
        usedItems.Add(itemId);
        int count = GetSuggestedTradeCount(itemId, buyFromPlayer: false);
        int fragments = Mathf.Max(1, Mathf.RoundToInt(MarketValueManager.GetValue(itemId) * count * 0.80f));
        activeOffers.Add(new MarketOffer(nextOfferId++, MarketOfferType.SellToPlayer,
            IFE残片, fragments, 0, 0, itemId, count, currentExpireTick, MarketValueManager.RefreshVersion));
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
