using FE.Logic.Manager;
using FE.Logic.Recipe;

namespace FE.Utils;

public static partial class Utils {
    /// <summary>
    /// 尝试用一定数量的物品交换其他物品。
    /// </summary>
    /// <details>
    /// 拿取物品顺序为：MOD数据 -> 背包 -> 物流背包
    /// <para></para>
    /// 放入物品顺序为：背包 -> 物流背包 -> 掉落到地上
    /// </details>
    /// <param name="takeId">从背包或物流背包中取走的物品ID</param>
    /// <param name="takeCount">从背包或物流背包中取走的物品数量</param>
    /// <param name="giveId">添加到背包中的物品ID</param>
    /// <param name="giveCount">添加到背包中的物品数量</param>
    private static void ExchangeItems(int takeId, int takeCount, int giveId, int giveCount) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (!LDB.items.Exist(takeId) || takeCount == 0 || !LDB.items.Exist(giveId) || giveCount == 0) {
            return;
        }
        ItemProto takeProto = LDB.items.Select(takeId);
        ItemProto giveProto = LDB.items.Select(giveId);
        if (GetItemTotalCount(takeId) < takeCount) {
            UIMessageBox.Show("提示", $"{takeProto.name} 不足 {takeCount}，无法兑换！",
                "确定", UIMessageBox.WARNING);
            return;
        }
        takeCount -= ItemManager.TakeItem(takeId, takeCount);
        if (takeCount > 0) {
            takeCount -= GameMain.mainPlayer.package.TakeItem(takeId, takeCount, out _);
            if (takeCount > 0) {
                GameMain.mainPlayer.deliveryPackage.TakeItems(ref takeId, ref takeCount, out _);
            }
        }
        GameMain.mainPlayer.TryAddItemToPackage(giveId, giveCount, 0, true);
        UIMessageBox.Show("提示", $"已兑换 {giveProto.name} x {giveCount}！",
            "确定", UIMessageBox.INFO);
    }

    /// <summary>
    /// 弹窗询问是否兑换，然后尝试用一定数量的物品交换其他物品，最后提示兑换成功。
    /// </summary>
    /// <details>
    /// 拿取物品顺序为：MOD数据 -> 背包 -> 物流背包
    /// <para></para>
    /// 放入物品顺序为：背包 -> 物流背包 -> 掉落到地上
    /// </details>
    /// <param name="takeId">从背包或物流背包中取走的物品ID</param>
    /// <param name="takeCount">从背包或物流背包中取走的物品数量</param>
    /// <param name="giveId">添加到背包中的物品ID</param>
    /// <param name="giveCount">添加到背包中的物品数量</param>
    public static void ExchangeItemsWithQuestion(int takeId, int takeCount, int giveId, int giveCount) {
        ItemProto takeProto = LDB.items.Select(takeId);
        ItemProto giveProto = LDB.items.Select(giveId);
        UIMessageBox.Show("提示", $"确认花费 {takeProto.name} x {takeCount} 兑换 {giveProto.name} x {giveCount} 吗？",
            "确定", "取消", UIMessageBox.QUESTION, () => { ExchangeItems(takeId, takeCount, giveId, giveCount); }, null);
    }

    private static void ExchangeRecipe(int takeId, int takeCount, BaseRecipe recipe) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (!LDB.items.Exist(takeId) || takeCount == 0) {
            return;
        }
        ItemProto takeProto = LDB.items.Select(takeId);
        if (GetItemTotalCount(takeId) < takeCount) {
            UIMessageBox.Show("提示", $"{takeProto.name} 不足 {takeCount}，无法兑换！",
                "确定", UIMessageBox.WARNING);
            return;
        }
        takeCount -= ItemManager.TakeItem(takeId, takeCount);
        if (takeCount > 0) {
            takeCount -= GameMain.mainPlayer.package.TakeItem(takeId, takeCount, out _);
            if (takeCount > 0) {
                GameMain.mainPlayer.deliveryPackage.TakeItems(ref takeId, ref takeCount, out _);
            }
        }
        if (!recipe.IsUnlocked) {
            recipe.Level = 1;
            recipe.Quality = 1;
            UIMessageBox.Show("提示", $"已解锁 {recipe.ShortInfo()}！",
                "确定", UIMessageBox.INFO);
        } else {
            recipe.MemoryCount++;
            UIMessageBox.Show("提示", $"已兑换 {recipe.ShortInfo()}，自动转化为对应回响！\n"
                                    + $"当前回响数目：{recipe.MemoryCount}",
                "确定", UIMessageBox.INFO);
        }
    }

    public static void ExchangeRecipeWithQuestion(int takeId, int takeCount, BaseRecipe recipe) {
        if (recipe == null) {
            UIMessageBox.Show("提示", "配方不存在，无法兑换！", "确定", UIMessageBox.INFO);
            return;
        }
        ItemProto takeProto = LDB.items.Select(takeId);
        UIMessageBox.Show("提示", $"确认花费 {takeProto.name} x {takeCount} 兑换 {recipe.ShortInfo()} 吗？",
            "确定", "取消", UIMessageBox.QUESTION, () => { ExchangeRecipe(takeId, takeCount, recipe); }, null);
    }

    /// <summary>
    /// 获取MOD数据中指定物品的数量。
    /// </summary>
    public static int GetModDataItemCount(int itemId) {
        return ItemManager.GetItemCount(itemId);
    }

    /// <summary>
    /// 获取背包中指定物品的数量。
    /// </summary>
    public static int GetPackageItemCount(int itemId) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return 0;
        }
        if (!LDB.items.Exist(itemId)) {
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
        if (!LDB.items.Exist(itemId) || !GameMain.mainPlayer.deliveryPackage.unlocked) {
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
}
