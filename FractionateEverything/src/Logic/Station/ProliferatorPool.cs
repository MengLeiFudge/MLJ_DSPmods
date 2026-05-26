using System;
using static FE.Logic.DataCenter.DataCenterInventory;
using static FE.Utils.Utils;

namespace FE.Logic.Station;

/// <summary>
/// 物流交互站自动增产点池状态与结算逻辑。
/// </summary>
public static class ProliferatorPool {
    #region 物流交互站自动喷涂

    private static readonly int[] plrIDs = [I增产剂MkI, I增产剂MkII, I增产剂MkIII];
    private static readonly int[] plrBaseUseCounts = [12, 24, 60];
    private static readonly int[] plrBasePoints = [1, 2, 4];
    /// <summary>
    /// [增产剂MkI-MkIII, 自身携带点数0-10] = 该增产剂可提供的点数总和
    /// </summary>
    private static readonly int[,] plrTotalPoints = new int[3, 11];

    private static int _authInitializer = InitLookup();

    private static int InitLookup() {
        for (int i = 0; i < 3; i++) {
            for (int j = 0; j <= 10; j++) {
                int useCount = (int)(plrBaseUseCounts[i] * (1 + Cargo.incTableMilli[j]) + 1e-6);
                plrTotalPoints[i, j] = useCount * plrBasePoints[i];
            }
        }
        return 1;
    }

    /// <summary>
    /// 消耗池内点数，将物品的增产点数提升到至多4点。
    /// </summary>
    public static void AddIncToItem(int itemCount, ref int itemInc) {
        //如果本身携带的平均点数有4点，直接跳过
        int targetTotal = itemCount * plrBasePoints[2];
        if (itemInc >= targetTotal) {
            return;
        }
        lock (centerItemCount) {
            int need = targetTotal - itemInc;
            EnsureLeftInc(need);
            //用池内点数补足物品点数
            if (leftInc >= need) {
                itemInc += need;
                leftInc -= need;
            } else {
                itemInc += leftInc;
                leftInc = 0;
            }
        }
    }

    /// <summary>
    /// 下载到物流交互站的物品先按 1:1 补到 4 点，12 级后再按 1:3 最高补到 10 点。
    /// </summary>
    public static void AddIncToDownloadedItem(int itemCount, ref int itemInc, bool allowOverdrive) {
        if (itemCount <= 0) {
            return;
        }

        AddIncToItem(itemCount, ref itemInc);
        int standardTargetTotal = itemCount * plrBasePoints[2];
        if (!allowOverdrive || itemInc < standardTargetTotal) {
            return;
        }

        int targetTotal = itemCount * 10;
        if (itemInc >= targetTotal) {
            return;
        }

        lock (centerItemCount) {
            const int overdrivePointCost = 3;
            int needInc = targetTotal - itemInc;
            EnsureLeftInc(needInc * overdrivePointCost);

            int addedInc = Math.Min(needInc, leftInc / overdrivePointCost);
            itemInc += addedInc;
            leftInc -= addedInc * overdrivePointCost;
        }
    }

    private static void EnsureLeftInc(int need) {
        if (leftInc >= need) {
            return;
        }

        // i=2: MkIII (4点), i=1: MkII (2点), i=0: MkI (1点)
        for (int i = 2; i >= 0; i--) {
            // 本次喷涂预估需要 need / plrBasePoints[i] + 1，额外再拿 plrBaseUseCounts[i] * 2 个
            int needCount = need / plrBasePoints[i] + 1 + plrBaseUseCounts[i] * 2;
            int actualTake = TakeItemFromModData(plrIDs[i], needCount, out int actualInc);
            if (actualTake == 0) {
                continue;
            }

            if (actualInc >= actualTake * 4) {
                int highPoint = (actualInc + actualTake - 1) / actualTake;// 向上取整
                int lowPoint = highPoint - 1;
                int highCount = actualInc - (actualTake * lowPoint);
                int lowCount = actualTake - highCount;
                leftInc += highCount * plrTotalPoints[i, Math.Min(10, highPoint)];
                leftInc += lowCount * plrTotalPoints[i, Math.Min(10, lowPoint)];
            } else {
                int needToUpgrade = actualTake * 4 - actualInc;
                if (leftInc >= needToUpgrade) {
                    leftInc -= needToUpgrade;
                    leftInc += actualTake * (plrTotalPoints[i, 4]);
                } else {
                    float avgNow = (float)actualInc / actualTake;
                    float costPerItem = 4.0f - avgNow;
                    int canUpgradeCount = (int)(leftInc / costPerItem);
                    if (canUpgradeCount > actualTake) canUpgradeCount = actualTake;
                    int selfSprayCount = actualTake - canUpgradeCount;
                    leftInc -= (int)(canUpgradeCount * costPerItem);
                    leftInc += canUpgradeCount * plrTotalPoints[i, 4];
                    leftInc += selfSprayCount * (plrTotalPoints[i, 4] - plrBasePoints[i]);
                }
            }

            if (leftInc >= need) {
                break;
            }
        }
    }

    /// <summary>
    /// 读取当前可用的全局增产点数。只读热路径使用，真实扣除仍走 <see cref="TryConsumeInc"/>。
    /// </summary>
    public static int GetAvailableInc() {
        lock (centerItemCount) {
            return leftInc;
        }
    }

    /// <summary>
    /// 仅在点数充足时从全局增产点数池扣除指定点数。
    /// </summary>
    public static bool TryConsumeInc(int need) {
        if (need <= 0) {
            return true;
        }
        lock (centerItemCount) {
            if (leftInc < need) {
                return false;
            }
            leftInc -= need;
            return true;
        }
    }

    #endregion
}
