using System.IO;
using FE.Utils;

namespace FE.Logic.Manager;

public static class GachaManager {
    // 每个卡池的保底计数器（poolId → 抽卡次数）
    // 池ID定义：0=常驻配方池, 1=常驻建筑池, 2=UP池, 3=限定池
    public static readonly int[] PityCount = new int[4];

    // UP池大保底标记（连续几次S未出UP）
    public static int UpGuaranteeCount = 0;

    // 软保底阈值、硬保底阈值
    public const int SoftPityThreshold = 70;
    public const int HardPityThreshold = 90;

    // UP轮换：72h = 72×60×60×60 ticks
    public const long UpRotationInterval = 216_000L;
    public static long UpRotationNextTick = UpRotationInterval;
    public static int CurrentUpGroupIndex = 0;

    /// <summary>
    /// 计算当前抽卡的S触发概率加成（软保底）
    /// count > SoftPityThreshold 时，每超出1抽额外+0.2%概率，上限+8%
    /// </summary>
    public static float GetSoftPityBonus(int poolId) {
        int count = PityCount[poolId];
        if (count <= SoftPityThreshold) {
            return 0f;
        }
        int excess = count - SoftPityThreshold;
        float bonus = excess * 0.002f;
        return bonus > 0.08f ? 0.08f : bonus;
    }

    /// <summary>
    /// 判断是否触发硬保底
    /// </summary>
    public static bool IsHardPity(int poolId) {
        return PityCount[poolId] >= HardPityThreshold;
    }

    /// <summary>
    /// 记录一次抽卡（更新计数器）
    /// </summary>
    public static void RecordDraw(int poolId, bool isS) {
        if (poolId >= 0 && poolId < PityCount.Length) {
            PityCount[poolId]++;
            if (isS) {
                ResetPity(poolId);
            }
        }
    }

    /// <summary>
    /// 重置保底计数
    /// </summary>
    public static void ResetPity(int poolId) {
        if (poolId >= 0 && poolId < PityCount.Length) {
            PityCount[poolId] = 0;
        }
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
