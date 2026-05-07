using FE.Logic.DataCenter.Patches;
using FE.UI.MainPanel.Setting;
using static FE.Utils.Utils;
using static FE.Logic.DataCenter.DataCenterInventory;

namespace FE.Logic.DataCenter;

/// <summary>
/// 玩家背包、物流背包和数据中心的统一物品访问逻辑。
/// </summary>
public static class PlayerInventoryAccess {
    #region 向背包添加物品

    //配送器与玩家交互：GameMain.mainPlayer.packageUtility.AddItemToAllPackages
    //此方法有三种模式，分别为：
    //priorityMode<0: 先物流背包，再背包
    //priorityMode>0: 先背包，再物流背包
    //priorityMode=0: 背包中属于这个物品的格子（有物品或者是筛选格）填满，再物流背包，最后背包

    //除了配送器都用这个：GameMain.mainPlayer.TryAddItemToPackage
    //此方法顺序固定为：先背包，再物流背包，最后扔出去或者手上

    /// <summary>
    /// 将指定物品添加到背包，并在左侧显示物品变动。
    /// 放入物品顺序为：背包 -> 物流背包 -> 手上/Mod背包
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <param name="count">数量</param>
    /// <param name="inc">增产点数总量</param>
    /// <param name="throwTrash">背包已满的情况下，true表示将物品放入Mod背包，false表示将手中的物品放入Mod背包，将物品拿到手中</param>
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
    /// 获取背包中指定物品的数量。
    /// </summary>
    public static int GetPackageItemCount(int itemId, out int inc) {
        inc = 0;
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return 0;
        }
        StorageComponent package = GameMain.mainPlayer.package;
        int count = 0;
        for (int index = 0; index < package.size; index++) {
            if (package.grids[index].itemId == itemId) {
                count += package.grids[index].count;
                inc += package.grids[index].inc;
            }
        }
        return count;
    }

    /// <summary>
    /// 获取物流背包中指定物品的数量。
    /// </summary>
    public static int GetDeliveryPackageItemCount(int itemId, out int inc) {
        inc = 0;
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null || !GameMain.mainPlayer.deliveryPackage.unlocked) {
            return 0;
        }
        DeliveryPackage deliveryPackage = GameMain.mainPlayer.deliveryPackage;
        int count = 0;
        for (int gridIndex = 99; gridIndex >= 0; gridIndex--) {
            if (deliveryPackage.grids[gridIndex].itemId == itemId) {
                count += deliveryPackage.grids[gridIndex].count;
                inc += deliveryPackage.grids[gridIndex].inc;
                break;
            }
        }
        return count;
    }

    /// <summary>
    /// 获取所有背包中指定物品的总数。
    /// </summary>
    public static long GetItemTotalCount(int itemId) {
        return GetModDataItemCount(itemId, out _)
               + GetPackageItemCount(itemId, out _)
               + GetDeliveryPackageItemCount(itemId, out _);
    }

    /// <summary>
    /// 获取所有背包中指定物品的总数。
    /// </summary>
    public static long GetItemTotalCountAndInc(int itemId, out long inc) {
        long ret = GetModDataItemCount(itemId, out long inc1)
                   + GetPackageItemCount(itemId, out int inc2)
                   + GetDeliveryPackageItemCount(itemId, out int inc3);
        inc = inc1 + inc2 + inc3;
        return ret;
    }

    #endregion

    #region 从背包拿取物品

    /// <summary>
    /// 按照玩家设定的顺序，从各个背包拿取物品。
    /// 使用前需要检测是不是目标背包。
    /// </summary>
    public static void TakeItemInternal(this StorageComponent storage, int itemId, int needCount, out int realCount,
        out int inc, bool useBan = false) {
        realCount = 0;
        inc = 0;
        if (itemId <= 0) {
            return;
        }
        //如果是沙土，直接拿取
        if (itemId == I沙土) {
            if (GameMain.mainPlayer.sandCount >= needCount) {
                realCount = needCount;
                GameMain.mainPlayer.sandCount -= needCount;
            } else {
                realCount = (int)GameMain.mainPlayer.sandCount;
                GameMain.mainPlayer.sandCount = 0;
            }
            return;
        }
        ItemProto item = LDB.items.Select(itemId);
        if (item == null) {
            return;
        }
        //如果是建筑师模式并且为建筑，不消耗物品
        if (PackageAccessRules.ArchitectMode && item.BuildMode != 0) {
            realCount = needCount;
            return;
        }
        //根据玩家设置的顺序拿取物品
        int[] TakeItemPriority = Miscellaneous.TakeItemPriority;
        for (int i = 0; i < TakeItemPriority.Length; i++) {
            int itemIdTmp = itemId;
            int realCountTemp = needCount;
            int incTemp;
            if (TakeItemPriority[i] == 0) {
                //背包
                storage.TakeTailItems(ref itemIdTmp, ref realCountTemp, out incTemp, useBan);
            } else if (TakeItemPriority[i] == 1) {
                //物流背包
                GameMain.mainPlayer.deliveryPackage.TakeItems(ref itemIdTmp, ref realCountTemp, out incTemp);
            } else {
                //Mod背包
                realCountTemp = TakeItemFromModData(itemIdTmp, realCountTemp, out incTemp, true);
            }
            needCount -= realCountTemp;
            realCount += realCountTemp;
            inc += incTemp;
            if (needCount == 0) {
                return;
            }
        }
    }

    /// <summary>
    /// 拿取指定物品。
    /// 如果数目不足，则不拿取，弹窗提示失败；否则仅拿取，不弹窗。
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <param name="count">期望拿取数量</param>
    /// <param name="inc">返回实际扣除物品携带的增产点数总量</param>
    /// <param name="showTakeFailMessage">数量不足时是否弹出提示</param>
    /// <returns>是否拿取成功</returns>
    public static bool TakeItemWithTip(int itemId, int count, out int inc, bool showTakeFailMessage = true) {
        inc = 0;
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return false;
        }
        ItemProto takeProto = LDB.items.Select(itemId);
        if (takeProto == null) {
            return false;
        }
        if (PackageAccessRules.ArchitectMode && takeProto.BuildMode != 0) {
            return true;
        }
        if (GetItemTotalCount(itemId) < count) {
            if (showTakeFailMessage) {
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
        PlayerInventoryItemAccessPatches.TakeTailItems(GameMain.mainPlayer.package, ref itemId, ref count, out inc);
        return true;
    }

    #endregion
}
