using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using FE.Utils;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

/// <summary>
/// 维护动态市场价值。
/// 注意：这里只生成 MarketValue，绝不改动 BaseValue(itemValue)。
/// </summary>
public static class MarketValueManager {
    public const long RefreshIntervalTicks = 60L * 60L * 10L;
    public const float MultiplierMin = 0.35f;
    public const float MultiplierMax = 3.50f;

    private static readonly System.Random rng = new(20260402);
    private static readonly HashSet<int> storeOfValueItems = [
        I宇宙矩阵, I黑雾矩阵, IFE分馏塔定向原胚
    ];

    public static readonly float[] MarketMultiplier = new float[12000];
    public static readonly float[] MarketValue = new float[12000];
    public static readonly float[] LastTargetMultiplier = new float[12000];
    public static readonly float[] LastCurrentRate = new float[12000];
    public static readonly float[] LastConsumeRate = new float[12000];
    public static readonly long[] LastCenterSnapshot = new long[12000];

    public static long LastRefreshTick { get; private set; }
    public static int RefreshVersion { get; private set; }

    private static bool initialized;

    public static void Init() {
        for (int itemId = 0; itemId < MarketMultiplier.Length; itemId++) {
            MarketMultiplier[itemId] = 1f;
            LastTargetMultiplier[itemId] = 1f;
            MarketValue[itemId] = GetBaseValue(itemId);
            LastCurrentRate[itemId] = 0f;
            LastConsumeRate[itemId] = 0f;
            LastCenterSnapshot[itemId] = 0L;
        }
        LastRefreshTick = 0L;
        RefreshVersion = 0;
        initialized = true;
        Refresh(force: true);
    }

    public static void Tick() {
        if (!initialized) {
            Init();
            return;
        }

        if (GameMain.gameTick - LastRefreshTick < RefreshIntervalTicks) {
            return;
        }

        Refresh(force: false);
    }

    public static void Refresh(bool force) {
        if (!initialized || GameMain.data == null) {
            return;
        }

        int currentStageIndex = GetCurrentProgressStageIndex();
        for (int itemId = 1; itemId < MarketMultiplier.Length; itemId++) {
            float baseValue = GetBaseValue(itemId);
            if (baseValue <= 0f || baseValue >= maxValue || !LDB.items.Exist(itemId)) {
                MarketMultiplier[itemId] = 1f;
                MarketValue[itemId] = 0f;
                continue;
            }

            float currentRate = GetCurrentProductionRate(itemId);
            float consumeRate = GetCurrentConsumeRate(itemId);
            long stock = centerItemCount[itemId];

            float targetMultiplier = CalculateTargetMultiplier(itemId, currentStageIndex, stock, currentRate, consumeRate);
            LastTargetMultiplier[itemId] = targetMultiplier;
            LastCurrentRate[itemId] = currentRate;
            LastConsumeRate[itemId] = consumeRate;
            LastCenterSnapshot[itemId] = stock;

            if (force) {
                MarketMultiplier[itemId] = targetMultiplier;
            } else {
                float blended = MarketMultiplier[itemId] * 0.75f + targetMultiplier * 0.25f;
                MarketMultiplier[itemId] = ClampMultiplier(itemId, blended);
            }
            MarketValue[itemId] = baseValue * MarketMultiplier[itemId];
        }

        LastRefreshTick = GameMain.gameTick;
        RefreshVersion++;
        MarketBoardManager.HandleMarketValueRefreshed();
        ExchangeManager.HandleMarketValueRefreshed();
        FragmentExchangeManager.HandleMarketValueRefreshed();
    }

    public static float GetBaseValue(int itemId) {
        return itemId > 0 && itemId < itemValue.Length ? itemValue[itemId] : 0f;
    }

    public static float GetMultiplier(int itemId) {
        return itemId > 0 && itemId < MarketMultiplier.Length ? MarketMultiplier[itemId] : 1f;
    }

    public static float GetValue(int itemId) {
        return itemId > 0 && itemId < MarketValue.Length ? MarketValue[itemId] : 0f;
    }

    /// <summary>
    /// 返回距离下一次市场刷新还剩多少 tick，用于 UI 倒计时显示。
    /// </summary>
    public static long GetRefreshRemainingTicks() {
        if (!initialized) {
            return RefreshIntervalTicks;
        }

        long elapsedTicks = GameMain.gameTick - LastRefreshTick;
        if (elapsedTicks <= 0L) {
            return RefreshIntervalTicks;
        }

        return Math.Max(0L, RefreshIntervalTicks - elapsedTicks);
    }

    public static int GetRefreshRemainingSeconds() {
        long remainingTicks = GetRefreshRemainingTicks();
        if (remainingTicks <= 0L) {
            return 0;
        }

        return (int)Math.Min(int.MaxValue, (remainingTicks + 59L) / 60L);
    }

    public static float GetCurrentProductionRate(int itemId) {
        if (itemId <= 0 || itemId >= 12000 || GameMain.data?.statistics?.production?.factoryStatPool == null) {
            return 0f;
        }
        float totalRate = 0f;
        FactoryProductionStat[] factoryStatPool = GameMain.data.statistics.production.factoryStatPool;
        int factoryCount = GameMain.data.factoryCount;
        for (int i = 0; i < factoryCount; i++) {
            FactoryProductionStat stat = factoryStatPool[i];
            if (stat == null || stat.productIndices == null || stat.productPool == null) {
                continue;
            }
            int index = stat.productIndices[itemId];
            if (index > 0 && index < stat.productPool.Length && stat.productPool[index] != null) {
                totalRate += stat.productPool[index].refProductSpeed;
            }
        }
        return totalRate;
    }

    public static float GetCurrentConsumeRate(int itemId) {
        if (itemId <= 0 || itemId >= 12000 || GameMain.data?.statistics?.production?.factoryStatPool == null) {
            return 0f;
        }
        float totalRate = 0f;
        FactoryProductionStat[] factoryStatPool = GameMain.data.statistics.production.factoryStatPool;
        int factoryCount = GameMain.data.factoryCount;
        for (int i = 0; i < factoryCount; i++) {
            FactoryProductionStat stat = factoryStatPool[i];
            if (stat == null || stat.productIndices == null || stat.productPool == null) {
                continue;
            }
            int index = stat.productIndices[itemId];
            if (index > 0 && index < stat.productPool.Length && stat.productPool[index] != null) {
                totalRate += stat.productPool[index].refConsumeSpeed;
            }
        }
        return totalRate;
    }

    public static IReadOnlyList<int> GetTopMarketItems(int count, bool descending) {
        IEnumerable<int> query = Enumerable.Range(1, MarketMultiplier.Length - 1)
            .Where(CanParticipateInEconomy);
        query = descending
            ? query.OrderByDescending(itemId => MarketMultiplier[itemId])
            : query.OrderBy(itemId => MarketMultiplier[itemId]);
        return query.Take(Math.Max(0, count)).ToArray();
    }

    public static bool CanParticipateInEconomy(int itemId) {
        return itemId > 0
               && itemId < 12000
               && LDB.items.Exist(itemId)
               && GetBaseValue(itemId) > 0f
               && GetBaseValue(itemId) < maxValue
               && itemId != IFE残片
               && itemId != I沙土;
    }

    public static bool IsStoreOfValue(int itemId) {
        return storeOfValueItems.Contains(itemId);
    }

    private static float CalculateTargetMultiplier(int itemId, int currentStageIndex, long stock, float currentRate,
        float consumeRate) {
        float stageFactor = GetStageFactor(itemId, currentStageIndex);
        float stockFactor = GetStockFactor(itemId, stock);
        float throughputFactor = GetThroughputFactor(currentRate, consumeRate);
        float randomShock = 1f + ((float)rng.NextDouble() * 0.16f - 0.08f);
        float target = stageFactor * stockFactor * throughputFactor * randomShock;
        return ClampMultiplier(itemId, target);
    }

    private static float GetStageFactor(int itemId, int currentStageIndex) {
        int itemStageIndex = GetMatrixStageIndex(itemId);
        int delta = itemStageIndex - currentStageIndex;
        float factor = delta switch {
            <= -2 => 0.55f,
            -1 => 0.82f,
            0 => 1.15f,
            1 => 1.32f,
            _ => 1.48f,
        };
        if (IsStoreOfValue(itemId)) {
            factor = Math.Max(factor, 0.90f);
        }
        return factor;
    }

    private static float GetStockFactor(int itemId, long stock) {
        float logValue = (float)Math.Log10(stock + 10.0);
        float factor = 1.25f - logValue * 0.18f;
        if (stock <= 0) {
            factor += 0.08f;
        }
        if (IsStoreOfValue(itemId)) {
            factor = Math.Max(factor, 0.90f);
        }
        return Mathf.Clamp(factor, 0.55f, 1.35f);
    }

    private static float GetThroughputFactor(float currentRate, float consumeRate) {
        float activity = Math.Max(currentRate, consumeRate);
        if (activity <= 0.001f) {
            return 0.95f;
        }
        float factor = 0.85f + (float)Math.Log10(activity + 1f) * 0.12f;
        return Mathf.Clamp(factor, 0.85f, 1.35f);
    }

    private static float ClampMultiplier(int itemId, float value) {
        float min = IsStoreOfValue(itemId) ? 0.80f : MultiplierMin;
        return Mathf.Clamp(value, min, MultiplierMax);
    }

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("Multipliers", br => {
                int count = br.ReadInt32();
                for (int i = 0; i < Math.Min(count, MarketMultiplier.Length); i++) {
                    MarketMultiplier[i] = ClampMultiplier(i, br.ReadSingle());
                    MarketValue[i] = GetBaseValue(i) * MarketMultiplier[i];
                }
                for (int i = MarketMultiplier.Length; i < count; i++) {
                    br.ReadSingle();
                }
            }),
            ("RefreshMeta", br => {
                LastRefreshTick = br.ReadInt64();
                RefreshVersion = br.ReadInt32();
            })
        );
        initialized = true;
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("Multipliers", bw => {
                bw.Write(MarketMultiplier.Length);
                for (int i = 0; i < MarketMultiplier.Length; i++) {
                    bw.Write(MarketMultiplier[i]);
                }
            }),
            ("RefreshMeta", bw => {
                bw.Write(LastRefreshTick);
                bw.Write(RefreshVersion);
            })
        );
    }

    public static void IntoOtherSave() {
        initialized = false;
        Init();
    }
}
