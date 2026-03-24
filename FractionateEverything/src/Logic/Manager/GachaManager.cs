using System.IO;
using FE.Utils;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static class GachaManager {
    // 每个卡池的保底计数器（poolId → 自上次出S后的连续抽数）
    public static readonly int[] PityCount = new int[GachaPool.PoolCount];

    public static int UpMainItemId = 0;
    public static readonly int[] UpSubItemIds = new int[3];

    public static bool GuaranteeMainOnNextSRoll = false;

    public static int NormalPoolPoints = 0;
    public static int FeaturedPoolPoints = 0;

    // 软保底阈值、硬保底阈值
    public const int SoftPityThreshold = 75;
    public const int HardPityThreshold = 90;
    public const float SoftPityBonusPerDraw = 0.001f;
    public const float SoftPityBonusCap = 0.015f;

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

    public static int GetPoolPointsByTicket(int ticketId) {
        return ticketId switch {
            IFE普通抽卡券 => NormalPoolPoints,
            IFE精选抽卡券 => FeaturedPoolPoints,
            _ => 0,
        };
    }

    public static void AddPoolPointsByTicket(int ticketId, int amount) {
        if (amount <= 0) {
            return;
        }

        switch (ticketId) {
            case IFE普通抽卡券:
                NormalPoolPoints += amount;
                break;
            case IFE精选抽卡券:
                FeaturedPoolPoints += amount;
                break;
        }
    }

    public static bool TryConsumePoolPointsByTicket(int ticketId, int amount) {
        if (amount <= 0) {
            return false;
        }

        switch (ticketId) {
            case IFE普通抽卡券:
                if (NormalPoolPoints < amount) {
                    return false;
                }
                NormalPoolPoints -= amount;
                return true;
            case IFE精选抽卡券:
                if (FeaturedPoolPoints < amount) {
                    return false;
                }
                FeaturedPoolPoints -= amount;
                return true;
            default:
                return false;
        }
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
            ("TicketPoolPoints", bw => {
                bw.Write(NormalPoolPoints);
                bw.Write(FeaturedPoolPoints);
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
            ("UpGuarantee", br => {
                if (br.BaseStream.Length - br.BaseStream.Position >= sizeof(bool)) {
                    GuaranteeMainOnNextSRoll = br.ReadBoolean();
                }
            }),
            ("UpRotation", br => {
                UpRotationNextTick = br.ReadInt64();
                CurrentUpGroupIndex = br.ReadInt32();
                if (CurrentUpGroupIndex < 0 || CurrentUpGroupIndex >= GachaService.UpGroupCount) {
                    CurrentUpGroupIndex = 0;
                }
            }),
            ("UpTargets", br => {
                UpMainItemId = br.ReadInt32();
                UpSubItemIds[0] = br.ReadInt32();
                UpSubItemIds[1] = br.ReadInt32();
                UpSubItemIds[2] = br.ReadInt32();
            }),
            ("TicketPoolPoints", br => {
                NormalPoolPoints = br.ReadInt32();
                FeaturedPoolPoints = br.ReadInt32();
            })
        );
    }

    public static void IntoOtherSave() {
        System.Array.Clear(PityCount, 0, PityCount.Length);
        UpMainItemId = 0;
        UpSubItemIds[0] = 0;
        UpSubItemIds[1] = 0;
        UpSubItemIds[2] = 0;
        GuaranteeMainOnNextSRoll = false;
        NormalPoolPoints = 0;
        FeaturedPoolPoints = 0;
        UpRotationNextTick = UpRotationInterval;
        CurrentUpGroupIndex = 0;
    }

    #endregion
}
