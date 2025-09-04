using System;
using FE.UI.View.Setting;
using static FE.Logic.Manager.ItemManager;

namespace FE.Utils;

public static partial class Utils {
    public static void AddTranslations() {
        Register("提示", "Tip");
        Register("确定", "Confirm");
        Register("取消", "Cancel");
        Register("要花费", "Would you like to spend");
        Register("来兑换", "to exchange");
        Register("吗？", "?");
        Register("兑换", "Exchange");
        Register("已兑换", "Exchanged");
        Register("无法兑换", "Can not exchange");
        Register("配方经验", "recipe experience");
    }

    #region 向背包添加物品

    /// <summary>
    /// 将指定物品添加到ModData背包
    /// </summary>
    public static void AddItemToModData(int itemId, int count, int inc = 0) {
        if (itemId == I沙土) {
            AddItemToPackage(itemId, count);
            return;
        }
        lock (centerItemCount) {
            centerItemCount[itemId] += count;
            centerItemInc[itemId] += inc;
        }
    }

    /// <summary>
    /// 将指定物品添加到背包，并在左侧显示物品变动。
    /// 放入物品顺序为：背包 -> 物流背包 -> 手上/地上
    /// </summary>
    /// <param name="throwTrash">背包满的情况下，true表示将该物品丢出去；否则，将手中的物品丢出去，将物品拿到手中</param>
    public static void AddItemToPackage(int itemId, int count, int inc = 0, bool throwTrash = true) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        int package = GameMain.mainPlayer.TryAddItemToPackage(itemId, count, inc, throwTrash);
        if (package > 0) {
            UIItemup.Up(itemId, package);
        }
    }

    #endregion

    #region 获取背包中物品的数目

    /// <summary>
    /// 获取MOD数据中指定物品的数量。
    /// </summary>
    public static long GetModDataItemCount(int itemId) {
        return centerItemCount[itemId];
    }

    /// <summary>
    /// 获取背包中指定物品的数量。
    /// </summary>
    public static int GetPackageItemCount(int itemId) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return 0;
        }
        StorageComponent package = GameMain.mainPlayer.package;
        int count = 0;
        for (int index = 0; index < package.size; index++) {
            if (package.grids[index].itemId == itemId && package.grids[index].count > 0) {
                count += package.grids[index].count;
            }
        }
        return count;
    }

    /// <summary>
    /// 获取物流背包中指定物品的数量。
    /// </summary>
    public static int GetDeliveryPackageItemCount(int itemId) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return 0;
        }
        if (!GameMain.mainPlayer.deliveryPackage.unlocked) {
            return 0;
        }
        DeliveryPackage deliveryPackage = GameMain.mainPlayer.deliveryPackage;
        int count = 0;
        for (int gridIndex = 99; gridIndex >= 0; gridIndex--) {
            if (deliveryPackage.grids[gridIndex].itemId == itemId && deliveryPackage.grids[gridIndex].count > 0) {
                count += deliveryPackage.grids[gridIndex].count;
            }
        }
        return count;
    }

    /// <summary>
    /// 获取所有背包中指定物品的总数。
    /// </summary>
    public static long GetItemTotalCount(int itemId) {
        if (itemId == I沙土) {
            //如果是沙盒模式并且无限沙土开启，直接返回long最大值
            if (GameMain.data.history.HasFeatureKey(1100001) && GameMain.sandboxToolsEnabled) {
                return long.MaxValue;
            }
            return GameMain.mainPlayer.sandCount;
        }
        return GetModDataItemCount(itemId) + GetPackageItemCount(itemId) + GetDeliveryPackageItemCount(itemId);
    }

    #endregion

    #region 从背包拿取物品

    /// <summary>
    /// 从ModData背包取出指定物品。
    /// 如果数目不足，则取出全部物品；否则取出指定数目的物品。
    /// 注意，通过此方法取出的物品数目应该远小于int.MaxValue，以避免增产点数超过int。
    /// </summary>
    /// <returns>实际拿到的数目</returns>
    public static int TakeItemFromModData(int itemId, int count, out int inc) {
        lock (centerItemCount) {
            count = (int)Math.Min(count, centerItemCount[itemId]);
            if (count == 0) {
                inc = 0;
            } else {
                inc = count == centerItemCount[itemId]
                    ? (int)centerItemInc[itemId]
                    : (int)((float)count / centerItemCount[itemId] * centerItemInc[itemId]);
                centerItemCount[itemId] -= count;
                centerItemInc[itemId] -= inc;
            }
            return count;
        }
    }

    /// <summary>
    /// 拿取指定物品。
    /// 如果数目不足，则不拿取，弹窗提示失败；否则仅拿取，不弹窗。
    /// </summary>
    /// <returns>是否拿取成功</returns>
    public static bool TakeItem(int itemId, int count, out int inc, bool showMessage = true) {
        inc = 0;
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return false;
        }
        ItemProto takeProto = LDB.items.Select(itemId);
        if (GetItemTotalCount(itemId) < count) {
            if (showMessage) {
                UIMessageBox.Show("提示".Translate(),
                    $"{takeProto.name} 不足 {count}！",
                    "确定".Translate(), UIMessageBox.WARNING,
                    null);
            }
            return false;
        }
        if (itemId == I沙土) {
            //如果是沙盒模式并且无限沙土开启，直接返回true
            if (GameMain.data.history.HasFeatureKey(1100001) && GameMain.sandboxToolsEnabled) {
                return true;
            }
            GameMain.mainPlayer.sandCount -= count;
            return true;
        }
        count -= TakeItemFromModData(itemId, count, out int inc1);
        inc += inc1;
        if (count > 0) {
            count -= GameMain.mainPlayer.package.TakeItem(itemId, count, out int inc2);
            inc += inc2;
            if (count > 0) {
                GameMain.mainPlayer.deliveryPackage.TakeItems(ref itemId, ref count, out int inc3);
                inc += inc3;
            }
        }
        return true;
    }

    #endregion

    /// <summary>
    /// 从ModData背包取出指定物品，再将其放入玩家背包/物流背包/手上。
    /// 如果数目不足，则取出全部物品；否则取出指定数目的物品。
    /// </summary>
    public static void ClickToMoveModDataItem(int itemId, bool leftClick) {
        ItemProto item = LDB.items.Select(itemId);
        if (item == null) {
            return;
        }
        int count = leftClick
            ? item.StackSize * ExtractAndPopup.LeftClickTakeCount
            : item.StackSize * ExtractAndPopup.RightClickTakeCount;
        int inc;
        lock (centerItemCount) {
            count = TakeItemFromModData(itemId, count, out inc);
        }
        AddItemToPackage(itemId, count, inc, false);
    }

    /// <summary>
    /// 获取当前MOD数据中最少的精华的数目。
    /// </summary>
    public static int GetEssenceMinCount() {
        long minCount = Math.Min(centerItemCount[IFE复制精华], centerItemCount[IFE点金精华]);
        minCount = Math.Min(minCount, centerItemCount[IFE分解精华]);
        minCount = Math.Min(minCount, centerItemCount[IFE转化精华]);
        return (int)Math.Min(int.MaxValue, minCount);
    }

    /// <summary>
    /// 从Mod数据中拿取每种精华各n个。
    /// 如果数目不足，则不拿取；否则扣除对应物品。
    /// </summary>
    public static bool TakeEssenceFromModData(int n, int[] consumeRegister) {
        if (centerItemCount[IFE复制精华] < n
            || centerItemCount[IFE点金精华] < n
            || centerItemCount[IFE分解精华] < n
            || centerItemCount[IFE转化精华] < n) {
            return false;
        }
        lock (centerItemCount) {
            TakeItemFromModData(IFE复制精华, n, out _);
            TakeItemFromModData(IFE点金精华, n, out _);
            TakeItemFromModData(IFE分解精华, n, out _);
            TakeItemFromModData(IFE转化精华, n, out _);
        }
        lock (consumeRegister) {
            consumeRegister[IFE复制精华] += n;
            consumeRegister[IFE点金精华] += n;
            consumeRegister[IFE分解精华] += n;
            consumeRegister[IFE转化精华] += n;
        }
        return true;
    }
}
