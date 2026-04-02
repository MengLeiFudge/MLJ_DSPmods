using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using FE.Utils;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

/// <summary>
/// 股票式交易所。
/// 使用 MarketValue 作为锚点，但价格存在独立波动与玩家交易冲击。
/// </summary>
public static class ExchangeManager {
    public sealed class ExchangeTicker {
        public int ItemId;
        public float LastPrice;
        public float BidPrice;
        public float AskPrice;
        public float DayOpenPrice;
        public float DayHighPrice;
        public float DayLowPrice;
        public long LastTradeTick;
        public int NetPlayerVolume;
        public int RecentPlayerBuyVolume;
        public int RecentPlayerSellVolume;
    }

    private static readonly System.Random rng = new(20260403);
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
                             || GameMain.gameTick - lastRefreshTick >= 3600L;
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

    public static bool TryBuy(int itemId, int count) {
        if (count <= 0 || !tickers.TryGetValue(itemId, out ExchangeTicker ticker)) {
            return false;
        }

        long price = (long)Math.Ceiling(ticker.AskPrice * count);
        if (price <= 0L || price > int.MaxValue) {
            return false;
        }
        if (!TakeItemWithTip(IFE残片, (int)price, out _)) {
            return false;
        }

        AddItemToModData(itemId, count, 0, true);
        ApplyTradeImpact(ticker, count, isBuy: true);
        return true;
    }

    public static bool TrySell(int itemId, int count) {
        if (count <= 0 || !tickers.TryGetValue(itemId, out ExchangeTicker ticker)) {
            return false;
        }
        if (!TakeItemWithTip(itemId, count, out _)) {
            return false;
        }

        long fragments = (long)Math.Floor(ticker.BidPrice * count);
        if (fragments > 0) {
            AddItemToModData(IFE残片, (int)Math.Min(int.MaxValue, fragments), 0, true);
        }
        ApplyTradeImpact(ticker, count, isBuy: false);
        return true;
    }

    private static void RefreshTickers() {
        foreach (ExchangeTicker ticker in tickers.Values) {
            float anchor = Math.Max(1f, MarketValueManager.GetValue(ticker.ItemId));
            float impact = Mathf.Clamp(ticker.NetPlayerVolume * 0.0035f, -0.15f, 0.15f);
            float randomShock = 1f + ((float)rng.NextDouble() * 0.06f - 0.03f);
            float target = anchor * (1f + impact) * randomShock;
            float newMid = ticker.LastPrice * 0.70f + target * 0.30f;
            float minPrice = Math.Max(1f, anchor * 0.50f);
            float maxPrice = Math.Max(minPrice, anchor * 1.50f);
            newMid = Mathf.Clamp(newMid, minPrice, maxPrice);

            ticker.LastPrice = newMid;
            ticker.BidPrice = Math.Max(1f, newMid * 0.96f);
            ticker.AskPrice = Math.Max(ticker.BidPrice, newMid * 1.04f);
            ticker.DayHighPrice = Math.Max(ticker.DayHighPrice, newMid);
            ticker.DayLowPrice = ticker.DayLowPrice <= 0f ? newMid : Math.Min(ticker.DayLowPrice, newMid);
            ticker.NetPlayerVolume = Mathf.RoundToInt(ticker.NetPlayerVolume * 0.60f);
            ticker.RecentPlayerBuyVolume = Mathf.RoundToInt(ticker.RecentPlayerBuyVolume * 0.50f);
            ticker.RecentPlayerSellVolume = Mathf.RoundToInt(ticker.RecentPlayerSellVolume * 0.50f);
        }

        lastRefreshTick = GameMain.gameTick;
        lastRefreshVersion = MarketValueManager.RefreshVersion;
    }

    private static void ApplyTradeImpact(ExchangeTicker ticker, int count, bool isBuy) {
        ticker.LastTradeTick = GameMain.gameTick;
        if (isBuy) {
            ticker.RecentPlayerBuyVolume += count;
            ticker.NetPlayerVolume += count;
        } else {
            ticker.RecentPlayerSellVolume += count;
            ticker.NetPlayerVolume -= count;
        }

        float impactMagnitude = Math.Min(0.12f, 0.01f + 0.02f * (float)Math.Sqrt(count));
        float factor = isBuy ? 1f + impactMagnitude : 1f - impactMagnitude;
        ticker.LastPrice = Math.Max(1f, ticker.LastPrice * factor);
        ticker.BidPrice = Math.Max(1f, ticker.LastPrice * 0.96f);
        ticker.AskPrice = Math.Max(ticker.BidPrice, ticker.LastPrice * 1.04f);
        ticker.DayHighPrice = Math.Max(ticker.DayHighPrice, ticker.LastPrice);
        ticker.DayLowPrice = ticker.DayLowPrice <= 0f ? ticker.LastPrice : Math.Min(ticker.DayLowPrice, ticker.LastPrice);
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
            })
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
            })
        );
    }

    public static void IntoOtherSave() {
        Init();
    }
}
