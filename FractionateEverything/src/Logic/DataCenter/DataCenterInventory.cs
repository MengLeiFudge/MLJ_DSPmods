using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using FE.Compatibility.Nebula;
using FE.Logic.Civilization;
using FE.Logic.Fractionation.Fractionators;
using FE.UI.MainPanel.Setting;
using NebulaAPI;
using static FE.Logic.Items.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.DataCenter;

/// <summary>
/// 分馏亚空间库存、单物品容量、手动上传/提取统计及存档。
/// </summary>
public static class DataCenterInventory {
    private const int MaxIntTakeCountByInc = int.MaxValue / 10;
    private const long MaxLongTakeCountByInc = long.MaxValue / 10L;

    public static readonly long[] centerItemCount = new long[12000];
    public static readonly long[] centerItemInc = new long[12000];
    public static long ManualExtractCount;
    public static long ManualUploadCount;
    private static bool snapshotDirty;

    public static void Export(BinaryWriter w) {
        lock (centerItemCount) {
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
                ("Expansion", SubspaceExpansion.Export),
                ("ManualInteractionStats", bw => {
                    bw.Write(Interlocked.Read(ref ManualExtractCount));
                    bw.Write(Interlocked.Read(ref ManualUploadCount));
                }),
                ("MaterialLeases", MaterialLeases.Export)
            );
        }
    }

    public static void Import(BinaryReader r) {
        lock (centerItemCount) {
            SubspaceExpansion.IntoOtherSave();
            MaterialLeases.BeforeImport();
            var refunds = new RetiredAssemblyData();
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
                ("Expansion", SubspaceExpansion.Import),
                ("ManualInteractionStats", br => {
                    ManualExtractCount = Math.Max(0L, br.ReadInt64());
                    ManualUploadCount = Math.Max(0L, br.ReadInt64());
                }),
                ("Assembly", refunds.Read),
                ("MaterialLeases", MaterialLeases.Import)
            );
            // FE 自有 DataCenter 余额不经 Legacy 物理引用迁移；旧槽位由后续自有状态叶处理。
            if (!NebulaMultiplayerModAPI.IsClient) { refunds.Restore(centerItemCount, centerItemInc); }
            NormalizeCurrencyPackagesLocked();
        }
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
        lock (centerItemCount) {
            Array.Clear(centerItemCount, 0, centerItemCount.Length);
            Array.Clear(centerItemInc, 0, centerItemInc.Length);
            ManualExtractCount = 0;
            ManualUploadCount = 0;
            SubspaceExpansion.IntoOtherSave();
            MaterialLeases.ClearWorld();
            snapshotDirty = false;
        }
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
        if (NebulaMultiplayerModAPI.IsClient && itemId != I沙土) { MaterialLeases.RequestManual(itemId, count); return; }
        int inc;
        lock (centerItemCount) {
            count = TakeItemFromModData(itemId, count, out inc, true);
        }
        if (count <= 0) {
            return;
        }
        RecordManualExtract();
        PlaceExtractedItem(itemId, count, inc);
    }

    /// <summary>已实际取得的物品按玩家提取偏好放入手中或背包；不再改动共享库存。</summary>
    internal static void PlaceExtractedItem(int itemId, int count, int inc) {
        if (count <= 0) { return; }
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

    public static long AddItemToModData(int itemId, int count, int inc = 0, bool manual = false) {
        long accepted = AddItemToModDataInternal(itemId, count, inc);
        if (itemId != I沙土 && NebulaModAPI.IsMultiplayerActive && manual && accepted > 0) {
            int acceptedInc = count <= 0 ? 0 : (int)Math.Min(int.MaxValue, (long)inc * accepted / count);
            NebulaModAPI.MultiplayerSession.Network.SendPacket(
                new CenterItemChangePacket(itemId, (int)accepted, acceptedInc));
        }
        return accepted;
    }

    public static long AddItemToModData(int itemId, long count, long inc = 0, bool manual = false) {
        long accepted = AddItemToModDataInternal(itemId, count, inc);
        if (itemId != I沙土 && NebulaModAPI.IsMultiplayerActive && manual && accepted > 0) {
            long acceptedInc = new StorageReceipt(count, inc, accepted, 0).StoredInc;
            NebulaModAPI.MultiplayerSession.Network.SendPacket(
                new CenterItemChangeLongPacket(itemId, accepted, acceptedInc));
        }
        return accepted;
    }

    /// <summary>在释放全部库存锁之后，补做入库的研究通知与手动联机同步。</summary>
    /// <param name="itemId">已规范化的库存物品 ID。</param>
    /// <param name="accepted">实际入库件数；为 0 时不做任何事。</param>
    /// <param name="acceptedInc">随入库件数一起写入的增产点。</param>
    /// <param name="newCount">入库后的库存件数，供高级塔门槛判断。</param>
    /// <param name="manual">是否为玩家手动操作；只有手动入库需要广播给其他玩家。</param>
    /// <remarks>与 <see cref="AddStoredItem"/> 配对使用：后者可在调用方的库存锁内执行，本方法不得在锁内调用。</remarks>
    internal static void NotifyItemAdded(int itemId, long accepted, long acceptedInc, long newCount, bool manual) {
        if (accepted <= 0) {
            return;
        }
        OnItemCountIncreased(itemId, newCount);
        if (itemId != I沙土 && NebulaModAPI.IsMultiplayerActive && manual) {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(
                new CenterItemChangeLongPacket(itemId, accepted, acceptedInc));
        }
    }

    private static long AddItemToModDataInternal(int itemId, long count, long inc = 0) {
        if (count <= 0) {
            return 0;
        }
        if (itemId <= 0 || itemId >= 12000) {
            return 0;
        }

        if (IsCurrencyPackageItem(itemId)
            && TryGetCurrencyNormalization(itemId, out int baseItemId, out int faceValue)) {
            itemId = baseItemId;
            count = SaturatingMulNonNegative(count, faceValue);
            inc = 0;
        }

        StorageReceipt receipt = AddStoredItem(itemId, count, inc, StorageSource.Manual, out long newCount);
        if (receipt.Stored > 0) { OnItemCountIncreased(itemId, newCount); }
        return receipt.Stored;
    }

    /// <summary>锁内按来源分配实体、溢出和拒收；调用方释放全部库存锁后再通知研究域。</summary>
    internal static StorageReceipt AddStoredItem(int itemId, long count, long inc, StorageSource source,
        out long newCount, long limit = long.MaxValue) {
        newCount = 0;
        count = Math.Max(0L, count);
        inc = Math.Max(0L, Math.Min(inc, MaxIncForCount(count)));
        if (count == 0 || itemId <= 0 || itemId >= centerItemCount.Length || LDB.items.Select(itemId) == null) {
            return new(count, inc, 0, 0);
        }
        if (itemId == I沙土) {
            long stored = Math.Min(count, long.MaxValue - GameMain.mainPlayer.sandCount);
            GameMain.mainPlayer.sandCount += stored;
            return new(count, inc, stored, 0);
        }
        lock (centerItemCount) {
            long capacity = source == StorageSource.Manual ? long.MaxValue : GetItemCapacity(itemId);
            if (source == StorageSource.LimitedStation) { capacity = Math.Min(capacity, Math.Max(0L, limit)); }
            long stored = Math.Min(count, Math.Max(0L, capacity - centerItemCount[itemId]));
            double value = itemValue[itemId];
            bool convert = source is StorageSource.BasicTower or StorageSource.UnlimitedStation
                && value > 0d && value < maxValue && !double.IsNaN(value) && !double.IsInfinity(value);
            long converted = convert ? count - stored : 0L;
            var receipt = new StorageReceipt(count, inc, stored, converted);
            centerItemCount[itemId] += stored;
            centerItemInc[itemId] = Math.Min(MaxIncForCount(centerItemCount[itemId]),
                SaturatingAddNonNegative(centerItemInc[itemId], receipt.StoredInc));
            if (converted > 0) { SubspaceExpansion.AddOverflowValue(converted * value); }
            snapshotDirty |= stored > 0;
            newCount = centerItemCount[itemId];
            return receipt;
        }
    }

    /// <summary>
    /// 亚空间库存增加后的统一处理点。所有入库路径都经过这里，高级塔的 1000 座门槛只在此检测。
    /// </summary>
    /// <remarks>必须在释放库存锁之后调用，避免与研究锁形成锁顺序冲突。</remarks>
    internal static void OnItemCountIncreased(int itemId, long newCount) {
        if (!FractionatorTowerCatalog.IsAdvancedTower(itemId)) {
            return;
        }
        ResearchManager.NotifyAdvancedTowerObtained(itemId);
        ResearchManager.NotifyAdvancedTowerInventoryCount(itemId, newCount);
    }

    /// <summary>
    /// 探索奖励直接写入亚空间并允许超过常规容量上限。
    /// </summary>
    public static void AddExplorationReward(int itemId, int count) {
        if (count <= 0 || itemId <= 0 || itemId >= 12000) {
            return;
        }
        if (itemId == I沙土) {
            GameMain.mainPlayer.sandCount = SaturatingAddNonNegative(GameMain.mainPlayer.sandCount, count);
            return;
        }
        long newCount;
        lock (centerItemCount) {
            centerItemCount[itemId] = SaturatingAddNonNegative(centerItemCount[itemId], count);
            snapshotDirty = true;
            newCount = centerItemCount[itemId];
        }
        OnItemCountIncreased(itemId, newCount);
    }

    /// <summary>
    /// 返回并清除亚空间待同步标记。
    /// </summary>
    internal static void MarkSnapshotDirty() { lock (centerItemCount) { snapshotDirty = true; } }

    public static bool TakeSnapshotDirty() {
        lock (centerItemCount) {
            bool dirty = snapshotDirty | SubspaceExpansion.TakeSnapshotDirty();
            snapshotDirty = false;
            return dirty;
        }
    }

    /// <summary>
    /// 按价值折算基础件数并乘永久容量倍率，建筑和未知估值保留十组底线。
    /// </summary>
    /// <param name="itemId">物品原型身份。</param>
    /// <returns>单物品独立容量；不因容量变化截断现有库存。</returns>
    public static long GetItemCapacity(int itemId) {
        if (itemId <= 0 || itemId >= 12000) {
            return 0;
        }
        if (GrowthTechnologyManager.UltimateCapacity) {
            return long.MaxValue / 10L;
        }
        ItemProto item = LDB.items.Select(itemId);
        int stackSize = Math.Max(1, item?.StackSize ?? 1);
        double baseline = 10d * stackSize;
        double value = itemValue[itemId];
        if (item?.BuildMode == 0 && value > 0 && value < maxValue) {
            baseline = Math.Max(baseline, 10000d / Math.Max(1d, Math.Log10(value + 1d)));
        }
        return (long)Math.Min(long.MaxValue / 10d, Math.Floor(baseline * SubspaceExpansion.CapacityScale));
    }

    /// <summary>
    /// 获取指定物品当前还能写入亚空间的数量。
    /// </summary>
    public static long GetRemainingCapacity(int itemId) {
        if (itemId == I沙土) {
            return long.MaxValue;
        }
        if (itemId <= 0 || itemId >= 12000) {
            return 0;
        }
        lock (centerItemCount) {
            return GetRemainingCapacityUnsafe(itemId);
        }
    }

    /// <summary>
    /// 判断指定物品是否已经达到单物品容量上限。
    /// </summary>
    public static bool IsItemFull(int itemId) => GetRemainingCapacity(itemId) <= 0;

    private static long GetRemainingCapacityUnsafe(int itemId) =>
        Math.Max(0L, GetItemCapacity(itemId) - centerItemCount[itemId]);

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
        lock (centerItemCount) {
            inc = centerItemInc[itemId];
            return centerItemCount[itemId];
        }
    }

    /// <summary>可供取物的自由库存；装配预留仍留在仓储中，但不再计入可用材料。</summary>
    public static long GetAvailableItemCount(int itemId, out long inc) {
        inc = 0;
        if (itemId == I沙土) { return GetModDataItemCount(itemId, out inc); }
        if (itemId <= 0 || itemId >= centerItemCount.Length) { return 0; }
        lock (centerItemCount) {
            long free = centerItemCount[itemId];
            if (free == 0) { return 0; }
            long total = centerItemCount[itemId], points = centerItemInc[itemId];
            inc = points / total >= 4 ? split_inc(ref total, ref points, free)
                : Math.Min(points, SaturatingMulNonNegative(free, 4));
            return free;
        }
    }

    public static int TakeItemFromModData(int itemId, int count, out int inc, bool manual = false) {
        if (manual && NebulaMultiplayerModAPI.IsClient && itemId != I沙土) {
            MaterialLeases.Ensure(itemId, count); return MaterialLeases.Take(itemId, count, out inc);
        }
        long cappedCount = Math.Min(count, MaxIntTakeCountByInc);
        long takeCount = TakeItemFromModDataInternal(itemId, cappedCount, out long takeInc);
        inc = (int)takeInc;
        if (itemId != I沙土 && NebulaModAPI.IsMultiplayerActive && manual && takeCount > 0) {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(
                new CenterItemChangePacket(itemId, -(int)takeCount, -inc));
        }
        return (int)takeCount;
    }

    public static long TakeItemFromModData(int itemId, long count, out long inc, bool manual = false) {
        if (manual && NebulaMultiplayerModAPI.IsClient && itemId != I沙土) {
            int wanted = (int)Math.Min(1000000, Math.Max(0, count)); MaterialLeases.Ensure(itemId, wanted);
            int taken = MaterialLeases.Take(itemId, wanted, out int points); inc = points; return taken;
        }
        long cappedCount = Math.Min(count, MaxLongTakeCountByInc);
        long takeCount = TakeItemFromModDataInternal(itemId, cappedCount, out inc);
        if (itemId != I沙土 && NebulaModAPI.IsMultiplayerActive && manual && takeCount > 0) {
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
            snapshotDirty = true;
            return count;
        }
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
