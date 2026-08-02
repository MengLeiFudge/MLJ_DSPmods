using System;
using FE.Compatibility.Nebula;
using FE.Logic.Civilization;
using FE.Logic.Fractionation.Fractionators;
using static FE.Logic.Items.ItemManager;

namespace FE.Logic.DataCenter;

/// <summary>
/// 处理玩家和建筑发起的实体上传：货币包裹规范化为基础余额，其他物品进入数据中心库存。
/// </summary>
public static class DataCenterUploadRouter {
    /// <summary>
    /// 上传整数数量的物品；包裹会按面额折算并丢弃增产点。
    /// </summary>
    public static void Upload(int itemId, int count, int inc = 0, bool manual = false) {
        Upload(itemId, (long)count, inc, manual);
    }

    /// <summary>
    /// 上传长整型数量的物品；Frag/Memory 包裹只写入对应基础余额。
    /// </summary>
    public static void Upload(int itemId, long count, long inc = 0, bool manual = false) {
        if (count <= 0) {
            return;
        }

        if (TryNormalizeUpload(itemId, count, inc, out int storeItemId, out long storeCount, out long storeInc)) {
            DataCenterInventory.AddItemToModData(storeItemId, storeCount, storeInc, manual);
            if (FractionatorTowerCatalog.IsActiveFractionator(storeItemId)) {
                TechManager.CheckTechUnlockCondition(storeItemId);
            }
        }
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
