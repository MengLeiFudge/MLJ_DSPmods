using System.IO;
using FE.Utils;

namespace FE.Logic.Manager;

public static class GachaManager {
    // 每个卡池的保底计数器（poolId → 自上次出S后的连续抽数）
    public static readonly int[] PityCount = new int[GachaPool.PoolCount];

    // UP池大保底标记（连续几次S未出UP）
    public static int UpGuaranteeCount = 0;

    // 软保底阈值、硬保底阈值
    public const int SoftPityThreshold = 75;
    public const int HardPityThreshold = 90;
    public const float SoftPityBonusPerDraw = 0.001f;
    public const float SoftPityBonusCap = 0.015f;

    // UP轮换：72h = 72×60×60×60 ticks
    public const long UpRotationInterval = 216_000L;
    public static long UpRotationNextTick = UpRotationInterval;
    public static int CurrentUpGroupIndex = 0;

    /// <summary>
    /// 计算当前抽卡的S触发概率加成（软保底）
    /// count > SoftPityThreshold 时，每超出1抽额外+0.1%概率，上限+1.5%
    /// </summary>
    public static float GetSoftPityBonus(int poolId) {
        if (!GachaPool.IsValidPoolId(poolId)) {
            return 0f;
        }
        int count = PityCount[poolId];
        if (count <= SoftPityThreshold) {
            return 0f;
        }
        int excess = count - SoftPityThreshold;
        float bonus = excess * SoftPityBonusPerDraw;
        return bonus > SoftPityBonusCap ? SoftPityBonusCap : bonus;
    }

    /// <summary>
    /// 判断是否触发硬保底
    /// </summary>
    public static bool IsHardPity(int poolId) {
        if (!GachaPool.IsValidPoolId(poolId)) {
            return false;
        }
        return PityCount[poolId] >= HardPityThreshold;
    }

    /// <summary>
    /// 保底重置语义：仅当本次抽卡产出S时重置计数。
    /// </summary>
    public static bool ShouldResetPityOnDraw(bool isS) {
        return isS;
    }

    /// <summary>
    /// 记录一次抽卡（更新计数器）
    /// </summary>
    public static void RecordDraw(int poolId, bool isS) {
        if (!GachaPool.IsValidPoolId(poolId)) {
            return;
        }

        PityCount[poolId]++;
        if (ShouldResetPityOnDraw(isS)) {
            ResetPity(poolId);
        }
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

    /// <summary>
    /// UP池S抽的UP判定语义：连续2次S未UP后，下次S必UP；否则50%概率UP。
    /// </summary>
    public static bool ShouldGuaranteeUpOnSRoll(double random01) {
        return UpGuaranteeCount >= 2 || random01 < 0.5;
    }

    /// <summary>
    /// 记录UP池S抽结果：命中UP则重置，未命中则累加。
    /// </summary>
    public static void RecordUpSResult(bool isUp) {
        if (isUp) {
            UpGuaranteeCount = 0;
            return;
        }
        UpGuaranteeCount++;
    }

    public static void TickRotationIfNeeded() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) return;
        if (GameMain.gameTick < UpRotationNextTick) return;
        long diff = GameMain.gameTick - UpRotationNextTick;
        long skip = diff / UpRotationInterval + 1;
        UpRotationNextTick += skip * UpRotationInterval;
        AdvanceUpGroup();
        GachaService.RefreshUpPool();
    }

    private static void AdvanceUpGroup() {
        CurrentUpGroupIndex++;
        if (CurrentUpGroupIndex >= GachaService.UpGroupCount) {
            CurrentUpGroupIndex = 0;
        }
    }

    #region IModCanSave

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("PityCount", bw => {
                for (int i = 0; i < PityCount.Length; i++) {
                    bw.Write(PityCount[i]);
                }
            }),
            ("UpGuarantee", bw => bw.Write(UpGuaranteeCount)),
            ("UpRotation", bw => {
                bw.Write(UpRotationNextTick);
                bw.Write(CurrentUpGroupIndex);
            })
        );
    }

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("PityCount", br => {
                for (int i = 0; i < PityCount.Length; i++) {
                    PityCount[i] = br.ReadInt32();
                }
            }),
            ("UpGuarantee", br => UpGuaranteeCount = br.ReadInt32()),
            ("UpRotation", br => {
                UpRotationNextTick = br.ReadInt64();
                CurrentUpGroupIndex = br.ReadInt32();
                if (CurrentUpGroupIndex < 0 || CurrentUpGroupIndex >= GachaService.UpGroupCount) {
                    CurrentUpGroupIndex = 0;
                }
            })
        );
    }

    public static void IntoOtherSave() {
        System.Array.Clear(PityCount, 0, PityCount.Length);
        UpGuaranteeCount = 0;
        UpRotationNextTick = UpRotationInterval;
        CurrentUpGroupIndex = 0;
    }

    #endregion
}
