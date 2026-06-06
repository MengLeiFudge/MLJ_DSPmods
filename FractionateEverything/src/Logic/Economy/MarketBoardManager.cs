using System;
using System.Collections.Generic;
using System.IO;
using FE.Logic.Gacha;
using UnityEngine;
using static FE.Logic.Items.ItemManager;
using static FE.Logic.DataCenter.DataCenterInventory;
using static FE.Utils.Utils;
using static FE.Logic.DataCenter.PlayerInventoryAccess;
using Random = System.Random;

namespace FE.Logic.Economy;

/// <summary>
/// 限时市场板。
/// 给普通玩家提供可理解的机会单。
/// </summary>
public static class MarketBoardManager {
    private const int MaxActiveOfferCount = 8;

    /// <summary>
    /// 市场订单分类。历史存档中的其它类型会在导入时丢弃。
    /// </summary>
    public enum MarketOfferType {
        SellToPlayer = 1,
        StageSupply = 2,
    }

    /// <summary>
    /// 市场订单定义。
    /// </summary>
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

    private static readonly Random rng = new(20260404);
    private static readonly List<MarketOffer> activeOffers = [];
    private static int nextOfferId = 1;
    private static long currentExpireTick;
    public static long TotalCompletedOfferCount;

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

        AddItemToModData(offer.OutputItemId, offer.OutputCount, 0, true);
        activeOffers.RemoveAt(index);
        TotalCompletedOfferCount++;
        return true;
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

    private static bool IsSupportedOffer(MarketOffer offer) {
        return offer.OfferType is MarketOfferType.SellToPlayer or MarketOfferType.StageSupply;
    }

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("Offers", br => {
                activeOffers.Clear();
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++) {
                    var offer = new MarketOffer(
                        br.ReadInt32(),
                        (MarketOfferType)br.ReadInt32(),
                        br.ReadInt32(),
                        br.ReadInt32(),
                        br.ReadInt32(),
                        br.ReadInt32(),
                        br.ReadInt32(),
                        br.ReadInt32(),
                        br.ReadInt64(),
                        br.ReadInt32());
                    if (IsSupportedOffer(offer)) {
                        activeOffers.Add(offer);
                    }
                }
            }),
            ("Meta", br => {
                nextOfferId = br.ReadInt32();
                currentExpireTick = br.ReadInt64();
            }),
            ("CompletedOfferStats", br => TotalCompletedOfferCount = Math.Max(0L, br.ReadInt64()))
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
            }),
            ("CompletedOfferStats", bw => bw.Write(TotalCompletedOfferCount))
        );
    }

    public static void IntoOtherSave() {
        Init();
        TotalCompletedOfferCount = 0;
    }
}
