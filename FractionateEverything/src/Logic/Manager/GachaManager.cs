using System;
using System.IO;
using FE.Utils;

namespace FE.Logic.Manager;

public enum GachaFocusType {
    Balanced = 0,
    MineralExpansion = 1,
    ConversionLeap = 2,
    LogisticsInteraction = 3,
    EmbryoCycle = 4,
    ProcessOptimization = 5,
    RectificationEconomy = 6,
}

public static class GachaManager {
    // 每个抽卡池的保底计数器（poolId → 自上次出S后的连续未出S抽数）
    public static readonly int[] PityCount = new int[GachaPool.PoolCount];
    public static readonly int[] PoolPoints = new int[GachaPool.PoolCount];

    public static GachaFocusType CurrentFocus = GachaFocusType.Balanced;

    // 原神风格：1-73 抽 0.6%，74-89 抽每抽额外 +6%，第 90 抽必出
    public const int SoftPityStartDraw = 74;
    public const int HardPityThreshold = 90;
    public const float SoftPityBonusPerDraw = 0.06f;

    private static int ClampPityCount(int value) {
        if (value < 0) {
            return 0;
        }

        int maxPityCount = HardPityThreshold - 1;
        return value > maxPityCount ? maxPityCount : value;
    }

    private static int ClampNonNegative(int value) {
        return value < 0 ? 0 : value;
    }

    private static GachaFocusType NormalizeFocus(int value) {
        return Enum.IsDefined(typeof(GachaFocusType), value)
            ? (GachaFocusType)value
            : GachaFocusType.Balanced;
    }

    /// <summary>
    /// 计算当前这一抽的 S 概率。
    /// 1-73 抽固定基础概率；74-89 抽每抽额外 +6%；90 抽硬保底。
    /// </summary>
    public static float GetCurrentSRate(int poolId, float baseRateS) {
        if (!GachaPool.IsDrawPool(poolId)) {
            return 0f;
        }

        int nextDraw = PityCount[poolId] + 1;
        if (nextDraw >= HardPityThreshold) {
            return 1f;
        }

        if (nextDraw < SoftPityStartDraw) {
            return baseRateS;
        }

        int boostedDrawCount = nextDraw - SoftPityStartDraw + 1;
        float rate = baseRateS + boostedDrawCount * SoftPityBonusPerDraw;
        return rate > 1f ? 1f : rate;
    }

    public static bool IsHardPity(int poolId) {
        return GachaPool.IsDrawPool(poolId) && PityCount[poolId] >= HardPityThreshold - 1;
    }

    public static void RecordDraw(int poolId, bool isS) {
        if (!GachaPool.IsDrawPool(poolId)) {
            return;
        }

        if (isS) {
            PityCount[poolId] = 0;
            return;
        }

        int nextCount = PityCount[poolId] + 1;
        int maxPityCount = HardPityThreshold - 1;
        PityCount[poolId] = nextCount > maxPityCount ? maxPityCount : nextCount;
    }

    public static int GetPoolPoints(int poolId) {
        return GachaPool.IsValidPoolId(poolId) ? PoolPoints[poolId] : 0;
    }

    public static void AddPoolPoints(int poolId, int amount) {
        if (!GachaPool.IsValidPoolId(poolId) || amount <= 0) {
            return;
        }

        PoolPoints[poolId] += amount;
    }

    public static bool TryConsumePoolPoints(int poolId, int amount) {
        if (!GachaPool.IsValidPoolId(poolId) || amount <= 0) {
            return false;
        }

        if (PoolPoints[poolId] < amount) {
            return false;
        }

        PoolPoints[poolId] -= amount;
        return true;
    }

    public static void SetFocus(GachaFocusType focus) {
        CurrentFocus = focus;
        GachaService.InitPools();
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("PityCount", bw => {
                for (int i = 0; i < PityCount.Length; i++) {
                    bw.Write(PityCount[i]);
                }
            }),
            ("PoolPointsByPoolId", bw => {
                for (int i = 0; i < PoolPoints.Length; i++) {
                    bw.Write(PoolPoints[i]);
                }
            }),
            ("Focus", bw => bw.Write((int)CurrentFocus))
        );
    }

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("PityCount", br => {
                for (int i = 0; i < PityCount.Length; i++) {
                    PityCount[i] = ClampPityCount(br.ReadInt32());
                }
            }),
            ("PoolPointsByPoolId", br => {
                for (int i = 0; i < PoolPoints.Length; i++) {
                    PoolPoints[i] = ClampNonNegative(br.ReadInt32());
                }
            }),
            ("Focus", br => CurrentFocus = NormalizeFocus(br.ReadInt32()))
        );
    }

    public static void IntoOtherSave() {
        Array.Clear(PityCount, 0, PityCount.Length);
        Array.Clear(PoolPoints, 0, PoolPoints.Length);
        CurrentFocus = GachaFocusType.Balanced;
    }
}
