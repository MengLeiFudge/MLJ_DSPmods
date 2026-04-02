using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using FE.Utils;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

/// <summary>
/// 残片稳定兑换。
/// 这是保底系统，不参与交易所价格波动。
/// </summary>
public static class FragmentExchangeManager {
    public readonly struct FragmentQuote(int itemId, int fragmentCost, float stageWeight, float safetyPremium) {
        public int ItemId { get; } = itemId;
        public int FragmentCost { get; } = fragmentCost;
        public float StageWeight { get; } = stageWeight;
        public float SafetyPremium { get; } = safetyPremium;
    }

    private static readonly Dictionary<int, FragmentQuote> quotes = [];
    private static int lastRefreshVersion = -1;

    public static void Init() {
        quotes.Clear();
        lastRefreshVersion = -1;
        RefreshQuotes();
    }

    public static void Tick() {
        if (lastRefreshVersion != MarketValueManager.RefreshVersion) {
            RefreshQuotes();
        }
    }

    public static void HandleMarketValueRefreshed() {
        RefreshQuotes();
    }

    public static bool CanExchangeItem(int itemId) {
        if (!MarketValueManager.CanParticipateInEconomy(itemId)) {
            return false;
        }
        return itemId != I沙土 && itemId != IFE残片;
    }

    public static FragmentQuote GetQuote(int itemId) {
        return quotes.TryGetValue(itemId, out FragmentQuote quote)
            ? quote
            : BuildQuote(itemId);
    }

    public static IReadOnlyCollection<int> GetExchangeableItems() {
        return quotes.Keys;
    }

    public static bool TryExchange(int itemId, int count) {
        if (!CanExchangeItem(itemId) || count <= 0) {
            return false;
        }

        FragmentQuote quote = GetQuote(itemId);
        long totalCost = (long)quote.FragmentCost * count;
        if (totalCost <= 0L || totalCost > int.MaxValue) {
            return false;
        }

        if (!TakeItemWithTip(IFE残片, (int)totalCost, out _)) {
            return false;
        }

        AddItemToModData(itemId, count, 0, true);
        return true;
    }

    private static void RefreshQuotes() {
        quotes.Clear();
        for (int itemId = 1; itemId < 12000; itemId++) {
            if (!CanExchangeItem(itemId)) {
                continue;
            }
            quotes[itemId] = BuildQuote(itemId);
        }
        lastRefreshVersion = MarketValueManager.RefreshVersion;
    }

    private static FragmentQuote BuildQuote(int itemId) {
        float baseValue = MarketValueManager.GetBaseValue(itemId);
        float stageWeight = GetStageWeight(itemId);
        float premium = GetSafetyPremium(itemId);
        int fragmentCost = Mathf.Max(1, Mathf.CeilToInt(baseValue * stageWeight * premium));
        return new FragmentQuote(itemId, fragmentCost, stageWeight, premium);
    }

    private static float GetStageWeight(int itemId) {
        int currentStageIndex = GetCurrentProgressStageIndex();
        int itemStageIndex = GetMatrixStageIndex(itemId);
        int delta = itemStageIndex - currentStageIndex;
        return delta switch {
            <= -2 => 0.85f,
            -1 => 0.95f,
            0 => 1.00f,
            1 => 1.25f,
            _ => 1.60f,
        };
    }

    private static float GetSafetyPremium(int itemId) {
        if (MarketValueManager.IsStoreOfValue(itemId)) {
            return 2.20f;
        }
        if (itemId >= IFE交互塔原胚 && itemId <= IFE分馏塔定向原胚) {
            return 2.00f;
        }
        if (itemId >= IFE交互塔 && itemId <= IFE星际物流交互站) {
            return 1.80f;
        }
        return 1.45f;
    }

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("Quotes", br => {
                quotes.Clear();
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++) {
                    int itemId = br.ReadInt32();
                    int fragmentCost = br.ReadInt32();
                    float stageWeight = br.ReadSingle();
                    float safetyPremium = br.ReadSingle();
                    quotes[itemId] = new FragmentQuote(itemId, fragmentCost, stageWeight, safetyPremium);
                }
            }),
            ("RefreshVersion", br => lastRefreshVersion = br.ReadInt32())
        );
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("Quotes", bw => {
                bw.Write(quotes.Count);
                foreach (KeyValuePair<int, FragmentQuote> pair in quotes) {
                    bw.Write(pair.Key);
                    bw.Write(pair.Value.FragmentCost);
                    bw.Write(pair.Value.StageWeight);
                    bw.Write(pair.Value.SafetyPremium);
                }
            }),
            ("RefreshVersion", bw => bw.Write(lastRefreshVersion))
        );
    }

    public static void IntoOtherSave() {
        Init();
    }
}
