using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using FE.Compatibility.Nebula;
using FE.UI.MainPanel.Setting;
using NebulaAPI;
using static FE.Logic.Items.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.DataCenter;

/// <summary>
/// 分馏数据中心库存、手动上传/提取统计，以及对应存档。
/// </summary>
public static class DataCenterInventory {
    private const int MaxIntTakeCountByInc = int.MaxValue / 10;
    private const long MaxLongTakeCountByInc = long.MaxValue / 10L;

    public static readonly long[] centerItemCount = new long[12000];
    public static readonly long[] centerItemInc = new long[12000];
    public static long ManualExtractCount;
    public static long ManualUploadCount;

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("CenterItems", bw => {
                List<int> activeIds = [];
                for (int i = 0; i < centerItemCount.Length; i++) {
                    if (centerItemCount[i] > 0) activeIds.Add(i);
                }
                bw.Write(activeIds.Count);
                foreach (int itemId in activeIds) {
                    bw.Write(itemId);
                    bw.Write(centerItemCount[itemId]);
                    bw.Write(centerItemInc[itemId]);
                }
            }),
            ("ManualInteractionStats", bw => {
                bw.Write(ManualExtractCount);
                bw.Write(ManualUploadCount);
            })
        );
    }

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("CenterItems", br => {
                int size = br.ReadInt32();
                for (int i = 0; i < size; i++) {
                    int itemId = br.ReadInt32();
                    long count = br.ReadInt64();
                    long inc = br.ReadInt64();
                    if (itemId >= 0 && itemId < centerItemCount.Length) {
                        centerItemCount[itemId] = Math.Max(0L, count);
                        long safeCount = centerItemCount[itemId];
                        long maxInc = MaxIncForCount(safeCount);
                        centerItemInc[itemId] = Math.Max(0L, Math.Min(inc, maxInc));
                    }
                }
            }),
            ("ManualInteractionStats", br => {
                ManualExtractCount = Math.Max(0L, br.ReadInt64());
                ManualUploadCount = Math.Max(0L, br.ReadInt64());
            })
        );
        // FE 自有 DataCenter 余额不经 Legacy 物理引用迁移；旧槽位由后续自有状态叶处理。
        NormalizeCurrencyPackagesLocked();
    }

    /// <summary>
    /// 将误入自身槽的货币包裹折算为基础余额并清空包裹槽。
    /// </summary>
    private static void NormalizeCurrencyPackagesLocked() {
        lock (centerItemCount) {
            NormalizeOnePackage(IFE残片10, IFE残片, 10);
            NormalizeOnePackage(IFE残片100, IFE残片, 100);
            NormalizeOnePackage(IFE残片1000, IFE残片, 1000);
            NormalizeOnePackage(IFE记忆源点10, IFE记忆源点, 10);
            NormalizeOnePackage(IFE记忆源点100, IFE记忆源点, 100);
            NormalizeOnePackage(IFE记忆源点1000, IFE记忆源点, 1000);
        }
    }

    private static void NormalizeOnePackage(int packageId, int baseId, int faceValue) {
        long packageCount = centerItemCount[packageId];
        if (packageCount <= 0) {
            centerItemCount[packageId] = 0;
            centerItemInc[packageId] = 0;
            return;
        }

        // 溢出时基础余额饱和，仍清空包裹槽，避免丢值回绕。
        long converted = SaturatingMulNonNegative(packageCount, faceValue);
        centerItemCount[baseId] = SaturatingAddNonNegative(centerItemCount[baseId], converted);
        centerItemCount[packageId] = 0;
        centerItemInc[packageId] = 0;
    }

    public static void IntoOtherSave() {
        Array.Clear(centerItemCount, 0, centerItemCount.Length);
        Array.Clear(centerItemInc, 0, centerItemInc.Length);
        ManualExtractCount = 0;
        ManualUploadCount = 0;
    }

    /// <summary>
    /// 记录一次成功的手动提取；达到 <see cref="long.MaxValue"/> 后保持饱和，避免回绕为负。
    /// </summary>
    public static void RecordManualExtract() {
        IncrementSaturating(ref ManualExtractCount);
    }

    /// <summary>
    /// 记录一次成功的手动上传；达到 <see cref="long.MaxValue"/> 后保持饱和，避免回绕为负。
    /// </summary>
    public static void RecordManualUpload() {
        IncrementSaturating(ref ManualUploadCount);
    }

    /// <summary>
    /// 线程安全地将计数加 1；已达 <see cref="long.MaxValue"/> 时保持不变。
    /// </summary>
    private static void IncrementSaturating(ref long counter) {
        long current;
        do {
            current = Interlocked.Read(ref counter);
            if (current >= long.MaxValue) {
                return;
            }
        } while (Interlocked.CompareExchange(ref counter, current + 1, current) != current);
    }

    /// <summary>
    /// 从数据中心取出指定物品，再将其放入玩家背包/物流背包/手上。
    /// </summary>
    public static void ClickToMoveModDataItem(int itemId, bool leftClick) {
        if (!PackageAccessRules.TechItemInteractionUnlocked) {
            return;
        }
        // 下载只可能取得基础 Frag/Memory 等普通物品，永不下载货币包裹。
        if (!PackageAccessRules.CanDownloadFromDataCenter(itemId)) {
            return;
        }
        ItemProto item = LDB.items.Select(itemId);
        if (item == null) {
            return;
        }
        int count = leftClick
            ? item.StackSize * Miscellaneous.LeftClickTakeCount
            : item.StackSize * Miscellaneous.RightClickTakeCount;
        int inc;
        lock (centerItemCount) {
            count = TakeItemFromModData(itemId, count, out inc, true);
        }
        if (count <= 0) {
            return;
        }
        RecordManualExtract();
        if (itemId == I沙土) {
            if (GameMain.mainPlayer.inhandItemId != I沙土) {
                GameMain.mainPlayer.ThrowHandItems();
            }
            GameMain.mainPlayer.inhandItemId = I沙土;
            GameMain.mainPlayer.inhandItemCount += count;
        } else {
            if (Miscellaneous.ExtractToHand) {
                if (GameMain.mainPlayer.inhandItemId == 0 || GameMain.mainPlayer.inhandItemCount == 0) {
                    GameMain.mainPlayer.inhandItemId = itemId;
                    GameMain.mainPlayer.inhandItemCount = count;
                    GameMain.mainPlayer.inhandItemInc = inc;
                } else if (GameMain.mainPlayer.inhandItemId == itemId) {
                    GameMain.mainPlayer.inhandItemCount += count;
                    GameMain.mainPlayer.inhandItemInc += inc;
                } else {
                    GameMain.mainPlayer.ThrowHandItems();
                    GameMain.mainPlayer.inhandItemId = itemId;
                    GameMain.mainPlayer.inhandItemCount = count;
                    GameMain.mainPlayer.inhandItemInc = inc;
                }
            } else {
                PlayerInventoryAccess.AddItemToPackage(itemId, count, inc, true);
            }
        }
    }

    public static int GetFragmentMinCount() {
        long count = centerItemCount[IFE残片];
        return (int)Math.Min(int.MaxValue, count);
    }

    public static bool TakeFragmentsFromModData(int n, int[] consumeRegister) {
        if (centerItemCount[IFE残片] < n) {
            return false;
        }
        lock (centerItemCount) {
            TakeItemFromModData(IFE残片, n, out _);
        }
        lock (consumeRegister) {
            consumeRegister[IFE残片] += n;
        }
        return true;
    }

    public static void AddItemToModData(int itemId, int count, int inc = 0, bool manual = false) {
        AddItemToModDataInternal(itemId, count, inc);
        if (NebulaModAPI.IsMultiplayerActive && manual) {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new CenterItemChangePacket(itemId, count, inc));
        }
    }

    public static void AddItemToModData(int itemId, long count, long inc = 0, bool manual = false) {
        AddItemToModDataInternal(itemId, count, inc);
        if (NebulaModAPI.IsMultiplayerActive && manual) {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new CenterItemChangeLongPacket(itemId, count, inc));
        }
    }

    private static void AddItemToModDataInternal(int itemId, long count, long inc = 0) {
        if (count <= 0) {
            return;
        }
        if (itemId == I沙土) {
            GameMain.mainPlayer.sandCount = SaturatingAddNonNegative(GameMain.mainPlayer.sandCount, count);
            return;
        }
        if (itemId <= 0 || itemId >= 12000) {
            return;
        }

        // 货币包裹不得占用自身库存槽，始终折算为基础 Frag/Memory；溢出时基础余额饱和。
        if (IsCurrencyPackageItem(itemId)
            && TryGetCurrencyNormalization(itemId, out int baseItemId, out int faceValue)) {
            itemId = baseItemId;
            count = SaturatingMulNonNegative(count, faceValue);
            inc = 0;
        }

        lock (centerItemCount) {
            centerItemCount[itemId] = SaturatingAddNonNegative(centerItemCount[itemId], count);
            long addInc = Math.Max(0L, inc);
            centerItemInc[itemId] = SaturatingAddNonNegative(centerItemInc[itemId], addInc);
            long maxInc = MaxIncForCount(centerItemCount[itemId]);
            if (centerItemInc[itemId] > maxInc) {
                centerItemInc[itemId] = maxInc;
            }
        }
    }

    public static long GetModDataItemCount(int itemId) {
        return GetModDataItemCount(itemId, out _);
    }

    public static long GetModDataItemCount(int itemId, out long inc) {
        inc = 0;
        if (itemId == I沙土) {
            if (GameMain.data.history.HasFeatureKey(1100001) && GameMain.sandboxToolsEnabled) {
                return long.MaxValue;
            }
            return GameMain.mainPlayer.sandCount;
        }
        if (itemId <= 0 || itemId >= 12000) {
            return 0;
        }
        inc = centerItemInc[itemId];
        return centerItemCount[itemId];
    }

    public static int TakeItemFromModData(int itemId, int count, out int inc, bool manual = false) {
        long cappedCount = Math.Min(count, MaxIntTakeCountByInc);
        long takeCount = TakeItemFromModDataInternal(itemId, cappedCount, out long takeInc);
        inc = (int)takeInc;
        if (NebulaModAPI.IsMultiplayerActive && manual && takeCount > 0) {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(
                new CenterItemChangePacket(itemId, -(int)takeCount, -inc));
        }
        return (int)takeCount;
    }

    public static long TakeItemFromModData(int itemId, long count, out long inc, bool manual = false) {
        long cappedCount = Math.Min(count, MaxLongTakeCountByInc);
        long takeCount = TakeItemFromModDataInternal(itemId, cappedCount, out inc);
        if (NebulaModAPI.IsMultiplayerActive && manual && takeCount > 0) {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new CenterItemChangeLongPacket(itemId, -takeCount, -inc));
        }
        return takeCount;
    }

    private static long TakeItemFromModDataInternal(int itemId, long count, out long inc) {
        inc = 0;
        if (count <= 0) {
            return 0;
        }
        if (IsCurrencyPackageItem(itemId)) {
            return 0;
        }
        if (itemId == I沙土) {
            if (GameMain.mainPlayer.sandCount >= count) {
                GameMain.mainPlayer.sandCount -= count;
                return count;
            } else {
                count = GameMain.mainPlayer.sandCount;
                GameMain.mainPlayer.sandCount = 0;
                return count;
            }
        }
        if (itemId <= 0 || itemId >= 12000) {
            return 0;
        }
        lock (centerItemCount) {
            count = Math.Min(count, centerItemCount[itemId]);
            if (count <= 0) {
                return 0;
            }
            if (centerItemInc[itemId] / centerItemCount[itemId] >= 4) {
                inc = split_inc(ref centerItemCount[itemId], ref centerItemInc[itemId], count);
            } else {
                long expectedInc = count * 4;
                if (centerItemInc[itemId] >= expectedInc) {
                    centerItemCount[itemId] -= count;
                    inc = expectedInc;
                    centerItemInc[itemId] -= inc;
                } else {
                    centerItemCount[itemId] -= count;
                    inc = centerItemInc[itemId];
                    centerItemInc[itemId] = 0;
                }
            }
            return count;
        }
    }

    /// <summary>
    /// 在同一把锁内聚合预检并一次扣除多种物品；任一不足或输入非法则零改动。
    /// 扣除时丢弃对应增产点，不向外返回 inc。
    /// </summary>
    /// <param name="costs">待扣除的 (itemId, count) 列表，允许重复 itemId。</param>
    /// <returns>全部扣除成功返回 true；否则 false 且库存不变。</returns>
    public static bool TryConsumeItemsAtomic(IReadOnlyList<(int itemId, long count)> costs) {
        if (costs == null || costs.Count == 0) {
            return true;
        }

        Dictionary<int, long> aggregated = new();
        for (int i = 0; i < costs.Count; i++) {
            (int itemId, long count) = costs[i];
            if (count < 0) {
                return false;
            }
            // count==0 的非法 itemId 也必须失败，避免静默跳过脏输入。
            if (itemId != I沙土 && (itemId <= 0 || itemId >= 12000)) {
                return false;
            }
            if (IsCurrencyPackageItem(itemId)) {
                // 协议成本只接受基础余额，不接受包裹面额。
                return false;
            }
            if (count == 0) {
                continue;
            }
            if (aggregated.TryGetValue(itemId, out long existing)) {
                if (count > long.MaxValue - existing) {
                    return false;
                }
                aggregated[itemId] = existing + count;
            } else {
                aggregated[itemId] = count;
            }
        }

        if (aggregated.Count == 0) {
            return true;
        }

        lock (centerItemCount) {
            foreach (KeyValuePair<int, long> pair in aggregated) {
                if (!HasEnoughLocked(pair.Key, pair.Value)) {
                    return false;
                }
            }

            foreach (KeyValuePair<int, long> pair in aggregated) {
                ConsumeDiscardIncLocked(pair.Key, pair.Value);
            }
        }
        return true;
    }

    /// <summary>
    /// 便捷重载：按并行数组构造原子扣除请求。
    /// </summary>
    public static bool TryConsumeItemsAtomic(int[] itemIds, long[] counts) {
        if (itemIds == null || counts == null || itemIds.Length != counts.Length) {
            return false;
        }
        List<(int itemId, long count)> costs = new(itemIds.Length);
        for (int i = 0; i < itemIds.Length; i++) {
            costs.Add((itemIds[i], counts[i]));
        }
        return TryConsumeItemsAtomic(costs);
    }

    private static bool HasEnoughLocked(int itemId, long count) {
        if (itemId == I沙土) {
            return GameMain.mainPlayer != null && GameMain.mainPlayer.sandCount >= count;
        }
        return centerItemCount[itemId] >= count;
    }

    private static void ConsumeDiscardIncLocked(int itemId, long count) {
        if (count <= 0) {
            return;
        }
        if (itemId == I沙土) {
            long sand = GameMain.mainPlayer.sandCount;
            GameMain.mainPlayer.sandCount = sand >= count ? sand - count : 0;
            return;
        }

        long remainingCount = centerItemCount[itemId];
        if (remainingCount <= 0) {
            centerItemCount[itemId] = 0;
            centerItemInc[itemId] = 0;
            return;
        }

        long take = count < remainingCount ? count : remainingCount;
        long remainingInc = centerItemInc[itemId];
        if (remainingInc < 0) {
            remainingInc = 0;
        }
        long maxIncBefore = MaxIncForCount(remainingCount);
        if (remainingInc > maxIncBefore) {
            remainingInc = maxIncBefore;
        }

        if (take >= remainingCount) {
            centerItemCount[itemId] = 0;
            centerItemInc[itemId] = 0;
            return;
        }

        // 复用项目既有 split_inc：更新剩余 count/inc，扣除部分增产点直接丢弃。
        _ = split_inc(ref remainingCount, ref remainingInc, take);
        if (remainingCount <= 0) {
            centerItemCount[itemId] = 0;
            centerItemInc[itemId] = 0;
            return;
        }

        if (remainingInc < 0) {
            remainingInc = 0;
        }
        long maxIncAfter = MaxIncForCount(remainingCount);
        if (remainingInc > maxIncAfter) {
            remainingInc = maxIncAfter;
        }
        centerItemCount[itemId] = remainingCount;
        centerItemInc[itemId] = remainingInc;
    }

    /// <summary>
    /// 非负 long 饱和加法，避免回绕。
    /// </summary>
    private static long SaturatingAddNonNegative(long value, long delta) {
        if (delta <= 0) {
            return value < 0 ? 0 : value;
        }
        if (value <= 0) {
            return delta;
        }
        return value > long.MaxValue - delta ? long.MaxValue : value + delta;
    }

    /// <summary>
    /// 非负 long 饱和乘法，避免回绕。
    /// </summary>
    private static long SaturatingMulNonNegative(long left, long right) {
        if (left <= 0 || right <= 0) {
            return 0;
        }
        return left > long.MaxValue / right ? long.MaxValue : left * right;
    }

    /// <summary>
    /// 计算 count 对应的增产点合法上限（count*10），使用饱和算术。
    /// </summary>
    private static long MaxIncForCount(long count) {
        if (count <= 0) {
            return 0;
        }
        return count > long.MaxValue / 10L ? long.MaxValue : count * 10L;
    }

}
