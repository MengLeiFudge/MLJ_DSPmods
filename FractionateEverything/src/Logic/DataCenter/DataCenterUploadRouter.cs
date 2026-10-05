using System;
using FE.Compatibility.Nebula;
using FE.Logic.Civilization;
using FE.Logic.Fractionation.Fractionators;
using static FE.Logic.Items.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.DataCenter;

/// <summary>
/// 处理玩家和建筑发起的实体上传：货币包裹规范化为基础余额，其他物品按原物进入亚空间。
/// </summary>
public static class DataCenterUploadRouter {

    /// <summary>整堆上传基础塔带尾货物；空带不加库存锁，容量检查、取货和入库保持原子性。</summary>
    /// <param name="cargoTraffic">所属工厂的传送带系统。</param>
    /// <param name="beltId">正面输入带身份。</param>
    /// <returns>成功上传的物品 ID；未取货时为 0。</returns>
    public static int UploadRearStack(CargoTraffic cargoTraffic, int beltId) {
        int itemId = cargoTraffic.GetItemIdAtRear(beltId);
        int count = ProcessManager.GetRearCargoStack(cargoTraffic, beltId);
        if (itemId <= 0 || count <= 0 || !IsUploadableItem(itemId)) { return 0; }

        long newCount;
        lock (DataCenterInventory.centerItemCount) {
            count = ProcessManager.GetRearCargoStack(cargoTraffic, beltId);
            if (!CanUploadStack(itemId, count)) { return 0; }
            itemId = cargoTraffic.TryPickItemAtRear(beltId, itemId, null, out byte stack, out byte inc);
            if (itemId <= 0) { return 0; }
            // 可上传物品已排除历史面额包裹；整堆容量已在同一锁内确认。
            DataCenterInventory.AddStoredItem(itemId, stack, inc, out newCount);
        }
        DataCenterInventory.OnItemCountIncreased(itemId, newCount);
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
    /// Frag/Memory 面额包裹按整包折算，容量不足一整包时不会消耗源物品。
    /// </summary>
    public static long Upload(int itemId, long count, long inc = 0, bool manual = false) {
        if (count <= 0 || (itemId == I沙土 && NebulaMultiplayerModAPI.IsMultiplayerActive)) {
            return 0;
        }

        if (IsCurrencyPackageItem(itemId)
            && TryGetCurrencyNormalization(itemId, out int baseItemId, out int faceValue)) {
            long acceptedValue;
            long newCount;
            lock (DataCenterInventory.centerItemCount) {
                long acceptedPackages = Math.Min(count,
                    DataCenterInventory.GetRemainingCapacity(baseItemId) / faceValue);
                if (acceptedPackages <= 0) {
                    return 0;
                }
                // 锁内只写库存；研究通知和联机包在释放库存锁后处理。
                acceptedValue = DataCenterInventory.AddStoredItem(
                    baseItemId, acceptedPackages * faceValue, 0, out newCount);
            }
            DataCenterInventory.NotifyItemAdded(baseItemId, acceptedValue, 0, newCount, manual);
            return acceptedValue / faceValue;
        }

        if (!TryNormalizeUpload(itemId, count, inc, out int storeItemId, out long storeCount,
                out long storeInc)) {
            return 0;
        }

        // 高级塔入库后的取得记录与 1000 座门槛由 DataCenterInventory 统一处理。
        return DataCenterInventory.AddItemToModData(storeItemId, storeCount, storeInc, manual);
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
        int capacityItemId = GetUploadCapacityItemId(itemId);
        return DataCenterInventory.GetRemainingCapacity(capacityItemId) >= count;
    }

    /// <summary>
    /// 将上传请求规范化为可写入数据中心的基础物品；包裹永不保留自身槽位。
    /// 面额换算溢出时写入 long.MaxValue 饱和值，由库存侧继续饱和累加。
    /// </summary>
    public static bool TryNormalizeUpload(int itemId, long count, long inc,
        out int storeItemId, out long storeCount, out long storeInc) {
        storeItemId = 0;
        storeCount = 0;
        storeInc = 0;
        if (count <= 0 || itemId <= 0 || itemId >= 12000) {
            return false;
        }

        if (TryGetCurrencyNormalization(itemId, out int baseItemId, out int faceValue)) {
            if (IsCurrencyPackageItem(itemId)) {
                storeItemId = baseItemId;
                storeCount = SaturatingMulNonNegative(count, faceValue);
                storeInc = 0; // 包裹增产点上传时丢弃，不放大面额
                return true;
            }

            // 基础 Frag/Memory 直接入账，保留原增产点语义。
            storeItemId = baseItemId;
            storeCount = count;
            storeInc = Math.Max(0L, inc);
            return true;
        }

        storeItemId = itemId;
        storeCount = count;
        storeInc = Math.Max(0L, inc);
        return true;
    }

    /// <summary>
    /// 非负 long 饱和乘法，供包裹面额换算使用。
    /// </summary>
    private static long SaturatingMulNonNegative(long left, long right) {
        if (left <= 0 || right <= 0) {
            return 0;
        }
        return left > long.MaxValue / right ? long.MaxValue : left * right;
    }
}
