using System;
using System.IO;
using FE.Utils;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static class GachaManager {
    // 每个卡池的保底计数器（poolId → 自上次出S后的连续未出S抽数）
    public static readonly int[] PityCount = new int[GachaPool.PoolCount];
    public static readonly int[] PoolPoints = new int[GachaPool.PoolCount];

    public static int UpMainItemId = 0;
    public static readonly int[] UpSubItemIds = new int[3];

    public static bool GuaranteeMainOnNextSRoll = false;

    // 原神风格：1-73 抽 0.6%，74-89 抽每抽额外 +6%，第 90 抽必出
    public const int SoftPityStartDraw = 74;
    public const int HardPityThreshold = 90;
    public const float SoftPityBonusPerDraw = 0.06f;

    public const long UpRotationInterval = 216_000L;
    public static long UpRotationNextTick = UpRotationInterval;
    public static int CurrentUpGroupIndex = 0;

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

    private static long NormalizeUpRotationNextTick(long value) {
        return value <= 0 ? UpRotationInterval : value;
    }

    private static int NormalizeUpGroupIndex(int value) {
        int groupCount = GachaService.UpGroupCount;
        if (groupCount <= 0) {
            return 0;
        }
        return value < 0 || value >= groupCount ? 0 : value;
    }

    private static bool HasBytesRemaining(BinaryReader reader, int size) {
        if (!reader.BaseStream.CanSeek) {
            return true;
        }
        return reader.BaseStream.Length - reader.BaseStream.Position >= size;
    }

    private static int ReadInt32OrDefault(BinaryReader reader, int defaultValue) {
        if (!HasBytesRemaining(reader, sizeof(int))) {
            return defaultValue;
        }
        try {
            return reader.ReadInt32();
        } catch (EndOfStreamException) {
            return defaultValue;
        }
    }

    private static long ReadInt64OrDefault(BinaryReader reader, long defaultValue) {
        if (!HasBytesRemaining(reader, sizeof(long))) {
            return defaultValue;
        }
        try {
            return reader.ReadInt64();
        } catch (EndOfStreamException) {
            return defaultValue;
        }
    }

    private static bool ReadBooleanOrDefault(BinaryReader reader, bool defaultValue) {
        if (!HasBytesRemaining(reader, sizeof(bool))) {
            return defaultValue;
        }
        try {
            return reader.ReadBoolean();
        } catch (EndOfStreamException) {
            return defaultValue;
        }
    }

    private static void NormalizeImportedState() {
        for (int i = 0; i < PityCount.Length; i++) {
            PityCount[i] = ClampPityCount(PityCount[i]);
        }

        for (int i = 0; i < PoolPoints.Length; i++) {
            PoolPoints[i] = ClampNonNegative(PoolPoints[i]);
        }

        UpMainItemId = ClampNonNegative(UpMainItemId);
        UpSubItemIds[0] = ClampNonNegative(UpSubItemIds[0]);
        UpSubItemIds[1] = ClampNonNegative(UpSubItemIds[1]);
        UpSubItemIds[2] = ClampNonNegative(UpSubItemIds[2]);

        UpRotationNextTick = NormalizeUpRotationNextTick(UpRotationNextTick);
        CurrentUpGroupIndex = NormalizeUpGroupIndex(CurrentUpGroupIndex);
    }

    /// <summary>
    /// 计算当前这一抽的 S 概率。
    /// 1-73 抽固定基础概率；74-89 抽每抽额外 +6%；90 抽硬保底。
    /// </summary>
    public static float GetCurrentSRate(int poolId, float baseRateS) {
        if (!GachaPool.IsValidPoolId(poolId)) {
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

    /// <summary>
    /// 判断是否触发硬保底
    /// </summary>
    public static bool IsHardPity(int poolId) {
        if (!GachaPool.IsValidPoolId(poolId)) {
            return false;
        }
        return PityCount[poolId] >= HardPityThreshold - 1;
    }

    /// <summary>
    /// 记录一次抽卡（更新计数器）
    /// </summary>
    public static void RecordDraw(int poolId, bool isS) {
        if (!GachaPool.IsValidPoolId(poolId)) {
            return;
        }

        if (isS) {
            ResetPity(poolId);
            return;
        }

        int nextCount = PityCount[poolId] + 1;
        int maxPityCount = HardPityThreshold - 1;
        PityCount[poolId] = nextCount > maxPityCount ? maxPityCount : nextCount;
    }

    /// <summary>
    /// 重置保底计数
    /// </summary>
    public static void ResetPity(int poolId) {
        if (!GachaPool.IsValidPoolId(poolId)) {
            return;
        }
        PityCount[poolId] = 0;
    }

    public static bool ShouldGuaranteeUpOnSRoll(double random01) {
        return GuaranteeMainOnNextSRoll || random01 < 0.4;
    }

    public static void RecordUpSResult(bool hitMainTarget) {
        if (hitMainTarget) {
            GuaranteeMainOnNextSRoll = false;
            return;
        }
        GuaranteeMainOnNextSRoll = true;
    }

    public static void SetCurrentUpTargets(int mainItemId, int sub1ItemId, int sub2ItemId, int sub3ItemId) {
        UpMainItemId = mainItemId;
        UpSubItemIds[0] = sub1ItemId;
        UpSubItemIds[1] = sub2ItemId;
        UpSubItemIds[2] = sub3ItemId;
    }

    public static int GetPoolPoints(int poolId) {
        if (!GachaPool.IsValidPoolId(poolId)) {
            return 0;
        }
        return PoolPoints[poolId];
    }

    public static void AddPoolPoints(int poolId, int amount) {
        if (!GachaPool.IsValidPoolId(poolId)) {
            return;
        }
        if (amount <= 0) {
            return;
        }
        PoolPoints[poolId] += amount;
    }

    public static bool TryConsumePoolPoints(int poolId, int amount) {
        if (!GachaPool.IsValidPoolId(poolId)) {
            return false;
        }
        if (amount <= 0) {
            return false;
        }

        if (PoolPoints[poolId] < amount) {
            return false;
        }
        PoolPoints[poolId] -= amount;
        return true;
    }

    public static void TickRotationIfNeeded() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) return;
        if (GameMain.gameTick < UpRotationNextTick) return;
        long diff = GameMain.gameTick - UpRotationNextTick;
        long skip = diff / UpRotationInterval + 1;
        UpRotationNextTick += skip * UpRotationInterval;
        AdvanceUpGroup(skip);
        GachaService.RefreshUpPool();
    }

    private static void AdvanceUpGroup(long step) {
        int groupCount = GachaService.UpGroupCount;
        if (groupCount <= 0) {
            CurrentUpGroupIndex = 0;
            return;
        }

        long normalizedStep = step % groupCount;
        CurrentUpGroupIndex = (int)((CurrentUpGroupIndex + normalizedStep) % groupCount);
    }

    #region IModCanSave

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("PityCount", bw => {
                for (int i = 0; i < PityCount.Length; i++) {
                    bw.Write(PityCount[i]);
                }
            }),
            ("UpGuarantee", bw => bw.Write(GuaranteeMainOnNextSRoll)),
            ("UpRotation", bw => {
                bw.Write(UpRotationNextTick);
                bw.Write(CurrentUpGroupIndex);
            }),
            ("UpTargets", bw => {
                bw.Write(UpMainItemId);
                bw.Write(UpSubItemIds[0]);
                bw.Write(UpSubItemIds[1]);
                bw.Write(UpSubItemIds[2]);
            }),
            ("PoolPointsByPoolId", bw => {
                for (int i = 0; i < PoolPoints.Length; i++) {
                    bw.Write(PoolPoints[i]);
                }
            })
        );
    }

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("PityCount", br => {
                for (int i = 0; i < PityCount.Length; i++) {
                    PityCount[i] = ClampPityCount(ReadInt32OrDefault(br, 0));
                }
            }),
            ("UpGuarantee", br => {
                GuaranteeMainOnNextSRoll = ReadBooleanOrDefault(br, GuaranteeMainOnNextSRoll);
            }),
            ("UpRotation", br => {
                UpRotationNextTick = NormalizeUpRotationNextTick(ReadInt64OrDefault(br, UpRotationInterval));
                CurrentUpGroupIndex = NormalizeUpGroupIndex(ReadInt32OrDefault(br, 0));
            }),
            ("UpTargets", br => {
                UpMainItemId = ClampNonNegative(ReadInt32OrDefault(br, 0));
                UpSubItemIds[0] = ClampNonNegative(ReadInt32OrDefault(br, 0));
                UpSubItemIds[1] = ClampNonNegative(ReadInt32OrDefault(br, 0));
                UpSubItemIds[2] = ClampNonNegative(ReadInt32OrDefault(br, 0));
            }),
            ("PoolPointsByPoolId", br => {
                for (int i = 0; i < PoolPoints.Length; i++) {
                    PoolPoints[i] = ClampNonNegative(ReadInt32OrDefault(br, 0));
                }
            })
        );

        NormalizeImportedState();
    }

    public static void IntoOtherSave() {
        System.Array.Clear(PityCount, 0, PityCount.Length);
        UpMainItemId = 0;
        UpSubItemIds[0] = 0;
        UpSubItemIds[1] = 0;
        UpSubItemIds[2] = 0;
        GuaranteeMainOnNextSRoll = false;
        System.Array.Clear(PoolPoints, 0, PoolPoints.Length);
        UpRotationNextTick = UpRotationInterval;
        CurrentUpGroupIndex = 0;
    }

    #endregion
}
