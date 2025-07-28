using System;
using FE.Logic.Recipe;
using static FE.Logic.Manager.ItemManager;

namespace FE.Utils;

public static partial class Utils {
    #region 向背包添加物品

    /// <summary>
    /// 将指定物品添加到ModData背包
    /// </summary>
    public static void AddItemToModData(int giveId, int giveCount) {
        lock (itemModDataCount) {
            if (itemModDataCount.ContainsKey(giveId)) {
                itemModDataCount[giveId] += giveCount;
            } else {
                itemModDataCount[giveId] = giveCount;
            }
        }
    }

    /// <summary>
    /// 将指定物品添加到背包，并在左侧显示物品变动。
    /// 放入物品顺序为：背包 -> 物流背包 -> 手上/地上
    /// </summary>
    /// <param name="throwTrash">背包满的情况下，true表示将该物品丢出去；否则，将手中的物品丢出去，将物品拿到手中</param>
    public static void AddItemToPackage(int giveId, int giveCount, bool throwTrash = true) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        int package = GameMain.mainPlayer.TryAddItemToPackage(giveId, giveCount, 0, throwTrash);
        if (package > 0) {
            UIItemup.Up(giveId, package);
        }
    }

    #endregion

    #region 获取背包中物品的数目

    /// <summary>
    /// 获取MOD数据中指定物品的数量。
    /// </summary>
    public static int GetModDataItemCount(int itemId) {
        return itemModDataCount.TryGetValue(itemId, out int value) ? value : 0;
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
    public static int GetItemTotalCount(int itemId) {
        return GetModDataItemCount(itemId) + GetPackageItemCount(itemId) + GetDeliveryPackageItemCount(itemId);
    }

    #endregion

    #region 从背包拿取物品

    /// <summary>
    /// 从ModData背包取出指定物品。
    /// 如果数目不足，则取出全部物品；否则取出指定数目的物品。最终返回取出的物品数量。
    /// </summary>
    /// <returns>取出的物品数量</returns>
    public static int TakeItemFromModData(int takeId, int takeCount) {
        lock (itemModDataCount) {
            if (itemModDataCount.ContainsKey(takeId)) {
                takeCount = Math.Min(takeCount, itemModDataCount[takeId]);
                itemModDataCount[takeId] -= takeCount;
                if (itemModDataCount[takeId] == 0) {
                    itemModDataCount.Remove(takeId);
                }
            } else {
                takeCount = 0;
            }
            return takeCount;
        }
    }

    /// <summary>
    /// 拿取指定物品。
    /// 如果数目不足，则不拿取，弹窗提示失败；否则仅拿取，不弹窗。
    /// </summary>
    /// <returns>是否拿取成功</returns>
    public static bool TakeItem(int takeId, int takeCount) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return false;
        }
        ItemProto takeProto = LDB.items.Select(takeId);
        if (GetItemTotalCount(takeId) < takeCount) {
            UIMessageBox.Show("提示", $"{takeProto.name} 不足 {takeCount}！",
                "确定", UIMessageBox.WARNING);
            return false;
        }
        takeCount -= TakeItemFromModData(takeId, takeCount);
        if (takeCount > 0) {
            takeCount -= GameMain.mainPlayer.package.TakeItem(takeId, takeCount, out _);
            if (takeCount > 0) {
                GameMain.mainPlayer.deliveryPackage.TakeItems(ref takeId, ref takeCount, out _);
            }
        }
        return true;
    }

    #endregion

    /// <summary>
    /// 弹窗询问是否兑换，然后尝试用一定数量的物品交换其他物品，最后提示兑换成功。
    /// </summary>
    /// <details>
    /// 拿取物品顺序为：MOD数据 -> 背包 -> 物流背包
    /// <para></para>
    /// 放入物品顺序为：背包 -> 物流背包 -> 掉落到地上
    /// </details>
    /// <param name="takeId">从MOD数据、背包或物流背包中取走的物品ID</param>
    /// <param name="takeCount">从MOD数据、背包或物流背包中取走的物品数量</param>
    /// <param name="giveId">添加到背包中的物品ID</param>
    /// <param name="giveCount">添加到背包中的物品数量</param>
    public static void ExchangeItemsWithQuestion(int takeId, int takeCount, int giveId, int giveCount) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        ItemProto takeProto = LDB.items.Select(takeId);
        ItemProto giveProto = LDB.items.Select(giveId);
        UIMessageBox.Show("提示", $"确认花费 {takeProto.name} x {takeCount} 兑换 {giveProto.name} x {giveCount} 吗？",
            "确定", "取消", UIMessageBox.QUESTION, () => {
                if (!TakeItem(takeId, takeCount)) {
                    return;
                }
                AddItemToPackage(giveId, giveCount);
                UIMessageBox.Show("提示", $"已兑换 {giveProto.name} x {giveCount} ！",
                    "确定", UIMessageBox.INFO);
            }, null);
    }

    public static void ExchangeRecipeWithQuestion(int takeId, int takeCount, BaseRecipe recipe) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (recipe == null) {
            UIMessageBox.Show("提示", "配方不存在，无法兑换！", "确定", UIMessageBox.WARNING);
            return;
        }
        if (recipe.MemoryCount >= recipe.MaxMemoryCount) {
            UIMessageBox.Show("提示", "该配方回响数目已达到上限！", "确定", UIMessageBox.WARNING);
            return;
        }
        ItemProto takeProto = LDB.items.Select(takeId);
        UIMessageBox.Show("提示", $"确认花费 {takeProto.name} x {takeCount} 兑换 {recipe.TypeNameWC} 吗？",
            "确定", "取消", UIMessageBox.QUESTION, () => {
                if (!TakeItem(takeId, takeCount)) {
                    return;
                }
                if (!recipe.IsUnlocked) {
                    recipe.Level = 1;
                    recipe.Quality = 1;
                    UIMessageBox.Show("提示", $"已解锁 {recipe.TypeName}！",
                        "确定", UIMessageBox.INFO);
                } else {
                    recipe.MemoryCount++;
                    UIMessageBox.Show("提示", $"已兑换 {recipe.TypeName}，自动转化为对应回响！\n"
                                            + $"当前回响数目：{recipe.MemoryCount}",
                        "确定", UIMessageBox.INFO);
                }
            }, null);
    }

    public static void ExchangeRecipeExpWithQuestion(BaseRecipe recipe) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (recipe == null) {
            UIMessageBox.Show("提示", "配方不存在，无法兑换！", "确定", UIMessageBox.WARNING);
            return;
        }
        if (!recipe.IsUnlocked) {
            UIMessageBox.Show("提示", "配方尚未解锁！", "确定", UIMessageBox.WARNING);
            return;
        }
        if (recipe.Quality == recipe.MaxQuality && recipe.Level == recipe.MaxLevel) {
            UIMessageBox.Show("提示", "配方已升到最高级！", "确定", UIMessageBox.WARNING);
            return;
        }
        if (recipe.Exp >= recipe.LevelUpExp) {
            UIMessageBox.Show("提示", "配方经验已达当前上限！", "确定", UIMessageBox.WARNING);
            return;
        }
        int takeId = I沙土;
        float needExp = recipe.LevelUpExp - recipe.Exp;
        int takeCount = (int)Math.Ceiling(needExp * 10);
        ItemProto takeProto = LDB.items.Select(I沙土);
        UIMessageBox.Show("提示",
            $"确认花费 {takeProto.name} x {takeCount} 兑换 {recipe.TypeNameWC} 经验 x {(int)needExp} 吗？",
            "确定", "取消", UIMessageBox.QUESTION, () => {
                if (!TakeItem(takeId, takeCount)) {
                    return;
                }
                recipe.AddExp(needExp);
                UIMessageBox.Show("提示", $"已兑换 {(int)needExp} 配方经验！",
                    "确定", UIMessageBox.INFO);
            }, null);
    }

    /// <summary>
    /// 从Mod数据中拿取每种精华各n个。
    /// 如果数目不足，则不拿取；否则扣除对应物品。
    /// 注意，为了提高性能，此方法未判断某些前置条件。使用时需注意情况。
    /// </summary>
    public static bool TakeEssenceFromModData(int n) {
        lock (itemModDataCount) {
            if (GetModDataItemCount(IFE复制精华) < n
                || GetModDataItemCount(IFE点金精华) < n
                || GetModDataItemCount(IFE分解精华) < n
                || GetModDataItemCount(IFE转化精华) < n) {
                return false;
            }
            TakeItemFromModData(IFE复制精华, n);
            TakeItemFromModData(IFE点金精华, n);
            TakeItemFromModData(IFE分解精华, n);
            TakeItemFromModData(IFE转化精华, n);
            return true;
        }
    }
}
