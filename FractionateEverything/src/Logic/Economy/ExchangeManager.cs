using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static FE.Utils.Utils;
using Random = System.Random;

namespace FE.Logic.Economy;

/// <summary>
/// 市场指数。
/// 使用 MarketValue 作为锚点，保留价格波动与外部流量，玩家不再在这里直接买卖生产物资。
/// </summary>
public static class ExchangeManager {
    private const long PriceRefreshIntervalTicks = 15L * 60L;
    private const float FlowCarryRatio = 0.55f;
    private const float FlowImpactPerUnit = 0.0035f;
    private const float MaxFlowImpact = 0.18f;
    private const float MeanReversionStrength = 0.25f;
    private const float RandomShockRange = 0.055f;

    /// <summary>
    /// 市场指数行情状态。
    /// </summary>
    public sealed class ExchangeTicker {
        public int ItemId;
        public float LastPrice;
        public float BidPrice;
        public float AskPrice;
        public float DayOpenPrice;
        public float DayHighPrice;
        public float DayLowPrice;
        public long LastTradeTick;
        // 历史字段名保留给存档读写；实际语义是外部市场净流量。
        public int NetPlayerVolume;
        public int NetMarketVolume {
            get => NetPlayerVolume;
            set => NetPlayerVolume = value;
        }
        public int RecentPlayerBuyVolume;
        public int RecentPlayerSellVolume;
        public float ChangePercent => DayOpenPrice > 0f ? (LastPrice - DayOpenPrice) / DayOpenPrice * 100f : 0f;
    }

    private static readonly Random rng = new(20260403);
    private static readonly int[] listedItems = [
        I铁矿, I铜矿, I石矿, I煤矿, I硅石, I钛石, I氢, I重氢,
        I铁块, I铜块, I钢材, I石材, I高能石墨, I玻璃,
        I磁线圈, I电路板, I处理器, I粒子容器, I卡西米尔晶体, I位面过滤器, I量子芯片, I框架材料, I引力透镜,
        I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵,
        I黑雾矩阵, I能量碎片, I物质重组器, I硅基神经元
    ];

    private static readonly Dictionary<int, ExchangeTicker> tickers = [];
    private static long lastRefreshTick;
    private static int lastRefreshVersion = -1;
    public static long TotalObservationCount;

    public static IReadOnlyList<int> ListedItems => listedItems;

    public static void Init() {
        tickers.Clear();
        foreach (int itemId in listedItems) {
            if (!LDB.items.Exist(itemId) || !MarketValueManager.CanParticipateInEconomy(itemId)) {
                continue;
            }
            float mid = Math.Max(1f, MarketValueManager.GetValue(itemId));
            tickers[itemId] = new ExchangeTicker {
                ItemId = itemId,
                LastPrice = mid,
                BidPrice = mid * 0.96f,
                AskPrice = mid * 1.04f,
                DayOpenPrice = mid,
                DayHighPrice = mid,
                DayLowPrice = mid,
                LastTradeTick = 0L,
            };
        }
        lastRefreshTick = 0L;
        lastRefreshVersion = MarketValueManager.RefreshVersion;
    }

    public static void Tick() {
        if (tickers.Count == 0) {
            Init();
            return;
        }

        bool shouldRefresh = MarketValueManager.RefreshVersion != lastRefreshVersion
                             || GameMain.gameTick - lastRefreshTick >= PriceRefreshIntervalTicks;
        if (!shouldRefresh) {
            return;
        }
        RefreshTickers();
    }

    public static void HandleMarketValueRefreshed() {
        RefreshTickers();
    }

    public static ExchangeTicker GetTicker(int itemId) {
        return tickers.TryGetValue(itemId, out ExchangeTicker ticker) ? ticker : null;
    }

    public static bool IsListed(int itemId) {
        return tickers.ContainsKey(itemId);
    }

    public static bool RecordObservation(int itemId) {
        if (!tickers.TryGetValue(itemId, out ExchangeTicker ticker)) {
            return false;
        }
        ticker.LastTradeTick = GameMain.gameTick;
        TotalObservationCount++;
        return true;
    }

    private static void RefreshTickers() {
        foreach (ExchangeTicker ticker in tickers.Values) {
            float anchor = Math.Max(1f, MarketValueManager.GetValue(ticker.ItemId));
            int externalFlow = CalculateExternalOrderFlow(ticker, anchor);
            float netFlow = ticker.NetMarketVolume * FlowCarryRatio + externalFlow;
            float flowImpact = Mathf.Clamp(netFlow * FlowImpactPerUnit, -MaxFlowImpact, MaxFlowImpact);
            float anchorGap = Mathf.Clamp((anchor - ticker.LastPrice) / anchor, -0.40f, 0.40f);
            float meanReversion = anchorGap * MeanReversionStrength;
            float randomShock = (float)rng.NextDouble() * RandomShockRange * 2f - RandomShockRange;
            float target = anchor * (1f + flowImpact + meanReversion + randomShock);
            float newMid = ticker.LastPrice * 0.58f + target * 0.42f;
            float minPrice = Math.Max(1f, anchor * 0.50f);
            float maxPrice = Math.Max(minPrice, anchor * 1.50f);
            newMid = Mathf.Clamp(newMid, minPrice, maxPrice);

            ticker.LastPrice = newMid;
            ticker.BidPrice = Math.Max(1f, newMid * 0.96f);
            ticker.AskPrice = Math.Max(ticker.BidPrice, newMid * 1.04f);
            ticker.DayHighPrice = Math.Max(ticker.DayHighPrice, newMid);
            ticker.DayLowPrice = ticker.DayLowPrice <= 0f ? newMid : Math.Min(ticker.DayLowPrice, newMid);
            ticker.NetMarketVolume = Mathf.RoundToInt(netFlow);
            ticker.RecentPlayerBuyVolume = Mathf.RoundToInt(ticker.RecentPlayerBuyVolume * 0.50f);
            ticker.RecentPlayerSellVolume = Mathf.RoundToInt(ticker.RecentPlayerSellVolume * 0.50f);
        }

        lastRefreshTick = GameMain.gameTick;
        lastRefreshVersion = MarketValueManager.RefreshVersion;
    }

    private static int CalculateExternalOrderFlow(ExchangeTicker ticker, float anchor) {
        float baseValue = Math.Max(1f, MarketValueManager.GetBaseValue(ticker.ItemId));
        int liquidity = Mathf.Clamp(Mathf.RoundToInt(4f + (float)Math.Sqrt(baseValue) * 1.8f), 4, 60);
        float anchorGap = Mathf.Clamp((anchor - ticker.LastPrice) / anchor, -0.45f, 0.45f);
        float momentum = Mathf.Clamp(ticker.ChangePercent / 100f, -0.30f, 0.30f);
        float randomPressure = (float)rng.NextDouble() * 2f - 1f;
        float direction = Mathf.Clamp(randomPressure * 0.70f + anchorGap * 0.85f + momentum * 0.25f, -1f, 1f);
        int flow = Mathf.RoundToInt(liquidity * direction);
        if (flow == 0) {
            flow = rng.Next(0, 2) == 0 ? -1 : 1;
        }
        return flow;
    }

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("Tickers", br => {
                tickers.Clear();
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++) {
                    var ticker = new ExchangeTicker {
                        ItemId = br.ReadInt32(),
                        LastPrice = br.ReadSingle(),
                        BidPrice = br.ReadSingle(),
                        AskPrice = br.ReadSingle(),
                        DayOpenPrice = br.ReadSingle(),
                        DayHighPrice = br.ReadSingle(),
                        DayLowPrice = br.ReadSingle(),
                        LastTradeTick = br.ReadInt64(),
                        NetPlayerVolume = br.ReadInt32(),
                        RecentPlayerBuyVolume = br.ReadInt32(),
                        RecentPlayerSellVolume = br.ReadInt32(),
                    };
                    tickers[ticker.ItemId] = ticker;
                }
            }),
            ("RefreshMeta", br => {
                lastRefreshTick = br.ReadInt64();
                lastRefreshVersion = br.ReadInt32();
            }),
            ("TradeStats", br => TotalObservationCount = Math.Max(0L, br.ReadInt64()))
        );
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("Tickers", bw => {
                bw.Write(tickers.Count);
                foreach (ExchangeTicker ticker in tickers.Values.OrderBy(t => t.ItemId)) {
                    bw.Write(ticker.ItemId);
                    bw.Write(ticker.LastPrice);
                    bw.Write(ticker.BidPrice);
                    bw.Write(ticker.AskPrice);
                    bw.Write(ticker.DayOpenPrice);
                    bw.Write(ticker.DayHighPrice);
                    bw.Write(ticker.DayLowPrice);
                    bw.Write(ticker.LastTradeTick);
                    bw.Write(ticker.NetPlayerVolume);
                    bw.Write(ticker.RecentPlayerBuyVolume);
                    bw.Write(ticker.RecentPlayerSellVolume);
                }
            }),
            ("RefreshMeta", bw => {
                bw.Write(lastRefreshTick);
                bw.Write(lastRefreshVersion);
            }),
            ("TradeStats", bw => bw.Write(TotalObservationCount))
        );
    }

    public static void IntoOtherSave() {
        Init();
        TotalObservationCount = 0;
    }
}
