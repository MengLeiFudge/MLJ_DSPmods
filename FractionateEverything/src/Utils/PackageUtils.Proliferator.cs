using System;
using static FE.Logic.Manager.ItemManager;

namespace FE.Utils;

public static partial class Utils {
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
            //如果池内点数不足，尝试用各种增产剂（高级增产剂优先）补充点数
            //需要考虑自喷涂影响；不足的情况下尽量一次性补充大量点数以减少运算消耗
            if (leftInc < need) {
                //i=2: MkIII (4点), i=1: MkII (2点), i=0: MkI (1点)
                for (int i = 2; i >= 0; i--) {
                    //本次喷涂预估需要need / plrBasePoints[i] + 1，额外再拿plrBaseUseCounts[i] * 2个
                    int needCount = need / plrBasePoints[i] + 1 + plrBaseUseCounts[i] * 2;
                    //取增产剂
                    int actualTake = TakeItemFromModData(plrIDs[i], needCount, out int actualInc);
                    if (actualTake == 0) {
                        continue;
                    }
                    if (actualInc >= actualTake * 4) {
                        // 1. 增产剂平均点数 >= 4 (例如平均 7.5 点)
                        // 计算高点位 (Ceil) 和 低点位 (Floor)
                        int highPoint = (actualInc + actualTake - 1) / actualTake;// 向上取整
                        int lowPoint = highPoint - 1;
                        // 设 highCount * highPoint + lowCount * lowPoint = actualInc
                        // 且 highCount + lowCount = actualTake
                        // 解得：highCount = actualInc - actualTake * lowPoint
                        int highCount = actualInc - (actualTake * lowPoint);
                        int lowCount = actualTake - highCount;
                        leftInc += highCount * plrTotalPoints[i, Math.Min(10, highPoint)];
                        leftInc += lowCount * plrTotalPoints[i, Math.Min(10, lowPoint)];
                    } else {
                        // 2. 增产剂平均点数 < 4
                        int needToUpgrade = actualTake * 4 - actualInc;
                        if (leftInc >= needToUpgrade) {
                            // 2a. 点数池充足，消耗点数将这些增产剂全部“补齐”到 4 点级别
                            leftInc -= needToUpgrade;
                            leftInc += actualTake * (plrTotalPoints[i, 4]);
                        } else {
                            // 2b. 点数池也不够，执行极限“自喷涂”
                            // 优先把现有的点数给一部分药剂喷到4点，剩下的药剂没水喷了，执行自喷涂逻辑
                            // 自喷涂逻辑：消耗该药剂自身的 1 次（即 plrBasePoints[i] 点）来换取全额增产
                            // 先计算目前 leftInc 能把多少个药剂强行提升到 4 点
                            // 每提升一个需要：4点 - 该药剂当前平均携带点数
                            float avgNow = (float)actualInc / actualTake;
                            float costPerItem = 4.0f - avgNow;
                            int canUpgradeCount = (int)(leftInc / costPerItem);
                            if (canUpgradeCount > actualTake) canUpgradeCount = actualTake;
                            // 剩下的只能靠自喷涂（消耗自身点数）
                            int selfSprayCount = actualTake - canUpgradeCount;
                            // 处理提升的部分
                            leftInc -= (int)(canUpgradeCount * costPerItem);
                            leftInc += canUpgradeCount * plrTotalPoints[i, 4];
                            // 处理自喷涂部分：视为0点喷涂，但扣除一次消耗
                            // 逻辑：(基础次数 * (1 + 0喷涂增益) - 1次自消耗) * 基础点数
                            // 对应之前定义的 plrTotalPoints[i, 0] 是不扣消耗的，
                            // 所以此处应为：plrTotalPoints[i, 0] - plrBasePoints[i]
                            leftInc += selfSprayCount * (plrTotalPoints[i, 4] - plrBasePoints[i]);
                        }
                    }
                    //如果点数池充足，跳出循环
                    if (leftInc >= need) {
                        break;
                    }
                }
            }
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

    #endregion
}
