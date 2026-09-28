using System;
using FE.Compatibility.Nebula;
using FE.Logic.Civilization;
using FE.Logic.Fractionation.Fractionators;
using NebulaAPI;
using static FE.Logic.Items.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.DataCenter;

/// <summary>
/// 处理玩家和建筑发起的实体上传：货币包裹规范化为基础余额，其他物品进入数据中心库存。
/// </summary>
public static class DataCenterUploadRouter {

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

        if (FractionatorTowerCatalog.IsAdvancedTowerItem(itemId)) {
            ResearchManager.NotifyAdvancedTowerObtained(itemId);
            long accepted = DataCenterInventory.AddItemToModData(IFE记忆源点, count, 0);
            if (NebulaMultiplayerModAPI.IsMultiplayerActive && manual && accepted > 0) {
                NebulaModAPI.MultiplayerSession.Network.SendPacket(
                    new CenterItemChangeLongPacket(itemId, accepted));
            }
            return accepted;
        }

        if (IsCurrencyPackageItem(itemId)
            && TryGetCurrencyNormalization(itemId, out int baseItemId, out int faceValue)) {
            lock (DataCenterInventory.centerItemCount) {
                long acceptedPackages = Math.Min(count,
                    DataCenterInventory.GetRemainingCapacity(baseItemId) / faceValue);
                if (acceptedPackages <= 0) {
                    return 0;
                }
                long convertedCount = acceptedPackages * faceValue;
                long acceptedValue = DataCenterInventory.AddItemToModData(
                    baseItemId, convertedCount, 0, manual);
                return acceptedValue / faceValue;
            }
        }

        if (!TryNormalizeUpload(itemId, count, inc, out int storeItemId, out long storeCount,
                out long storeInc)) {
            return 0;
        }

        return DataCenterInventory.AddItemToModData(storeItemId, storeCount, storeInc, manual);
    }

    /// <summary>
    /// 获取上传后实际占用容量的物品 ID；高级塔献祭统一写入 Memory。
    /// </summary>
    public static int GetUploadCapacityItemId(int itemId) =>
        FractionatorTowerCatalog.IsAdvancedTowerItem(itemId) ? IFE记忆源点 : itemId;

    /// <summary>
    /// 判断一整份带上货物能否由基础塔安全上传；只有全部可接收时才允许先从传送带取走。
    /// </summary>
    public static bool CanUploadStack(int itemId, int count) {
        if (count <= 0 || (itemId == I沙土 && NebulaMultiplayerModAPI.IsMultiplayerActive)
            || Array.IndexOf(needs, itemId) < 0) {
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
