using System;
using FE.Compatibility.Nebula;
using FE.Logic.Fractionation.Fractionators;
using static FE.Logic.Items.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.DataCenter;

/// <summary>
/// 处理玩家和建筑发起的实体上传，按原物品及增产点进入亚空间。
/// </summary>
public static class DataCenterUploadRouter {

    /// <summary>整堆上传基础塔带尾货物；空带不加库存锁，容量检查、取货和入库保持原子性。</summary>
    /// <param name="cargoTraffic">所属工厂的传送带系统。</param>
    /// <param name="beltId">正面输入带身份。</param>
    /// <returns>成功上传的物品 ID；未取货时为 0。</returns>
    public static int UploadRearStack(CargoTraffic cargoTraffic, int beltId) {
        if (NebulaMultiplayerModAPI.IsClient) { return 0; }
        int itemId = cargoTraffic.GetItemIdAtRear(beltId);
        int count = ProcessManager.GetRearCargoStack(cargoTraffic, beltId);
        if (itemId <= 0 || count <= 0 || !IsUploadableItem(itemId)) { return 0; }

        long newCount;
        StorageReceipt receipt;
        lock (DataCenterInventory.centerItemCount) {
            count = ProcessManager.GetRearCargoStack(cargoTraffic, beltId);
            if (!CanUploadStack(itemId, count)) { return 0; }
            itemId = cargoTraffic.TryPickItemAtRear(beltId, itemId, null, out byte stack, out byte inc);
            if (itemId <= 0) { return 0; }
            // 同一锁内保证整堆可接收，超出实体容量的部分仅记扩容价值。
            receipt = DataCenterInventory.AddStoredItem(itemId, stack, inc, StorageSource.BasicTower, out newCount);
        }
        if (receipt.Stored > 0) { DataCenterInventory.OnItemCountIncreased(itemId, newCount); }
        return itemId;
    }

    /// <summary>
    /// 上传整数数量的物品；返回实际接收的源物品件数。
    /// </summary>
    public static long Upload(int itemId, int count, int inc = 0, bool manual = false) {
        return Upload(itemId, (long)count, inc, manual);
    }

    /// <summary>
    /// 上传长整型数量的物品；返回实际接收的源物品件数。
    /// 玩家上传保留实体，不受玩法容量限制。
    /// </summary>
    public static long Upload(int itemId, long count, long inc = 0, bool manual = false) {
        if (count <= 0 || (itemId == I沙土 && NebulaMultiplayerModAPI.IsMultiplayerActive)) {
            return 0;
        }

        if (!TryNormalizeUpload(itemId, count, inc, out int storeItemId, out long storeCount,
                out long storeInc)) {
            return 0;
        }

        // 高级塔入库后的取得记录与 1000 座门槛由 DataCenterInventory 统一处理。
        return DataCenterInventory.AddItemToModData(storeItemId, storeCount, storeInc, manual);
    }

    /// <summary>交互站自动上传；有限目标在库存锁内复核，无限超额只折算一次。</summary>
    internal static StorageReceipt UploadFromStation(int itemId, int count, int inc, bool unlimited, long limit) {
        if (NebulaMultiplayerModAPI.IsClient || !IsUploadableItem(itemId)) { return new(count, inc, 0, 0); }
        StorageReceipt receipt = DataCenterInventory.AddStoredItem(itemId, count, inc,
            unlimited ? StorageSource.UnlimitedStation : StorageSource.LimitedStation, out long newCount, limit);
        if (receipt.Stored > 0) { DataCenterInventory.OnItemCountIncreased(itemId, newCount); }
        return receipt;
    }

    /// <summary>
    /// 获取上传后实际占用容量的物品 ID。
    /// </summary>
    public static int GetUploadCapacityItemId(int itemId) => itemId;

    /// <summary>
    /// 判断一整份带上货物能否由基础塔安全上传；只有全部可接收时才允许先从传送带取走。
    /// </summary>
    public static bool CanUploadStack(int itemId, int count) {
        if (count <= 0 || (itemId == I沙土 && NebulaMultiplayerModAPI.IsMultiplayerActive)
            || !IsUploadableItem(itemId)) {
            return false;
        }
        double value = itemValue[itemId];
        return value > 0d && value < maxValue && !double.IsNaN(value) && !double.IsInfinity(value)
            || DataCenterInventory.GetRemainingCapacity(itemId) >= count;
    }

    /// <summary>
    /// 校验上传请求的身份、数量和增产点。
    /// </summary>
    public static bool TryNormalizeUpload(int itemId, long count, long inc,
        out int storeItemId, out long storeCount, out long storeInc) {
        storeItemId = 0;
        storeCount = 0;
        storeInc = 0;
        if (count <= 0 || itemId <= 0 || itemId >= 12000) {
            return false;
        }

        storeItemId = itemId;
        storeCount = count;
        storeInc = Math.Max(0L, inc);
        return true;
    }
}
