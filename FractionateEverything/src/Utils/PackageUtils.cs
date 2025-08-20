using System;
using FE.Logic.Recipe;
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
        if (itemId == I沙土) {
            return 0;
        }
        return centerItemCount[itemId];
    }

    /// <summary>
    /// 获取MOD数据中指定物品的数量。
    /// </summary>
    public static int GetModDataItemIntCount(int itemId) {
        if (itemId == I沙土) {
            return 0;
        }
        return (int)Math.Min(int.MaxValue, centerItemCount[itemId]);
    }

    /// <summary>
    /// 获取背包中指定物品的数量。
    /// </summary>
    public static int GetPackageItemCount(int itemId) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return 0;
        }
        if (itemId == I沙土) {
            return (int)Math.Min(int.MaxValue, GameMain.mainPlayer.sandCount);
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
        if (itemId == I沙土) {
            return 0;
        }
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
        return GetModDataItemCount(itemId) + GetPackageItemCount(itemId) + GetDeliveryPackageItemCount(itemId);
    }

    #endregion

    #region 从背包拿取物品

    /// <summary>
    /// 从ModData背包取出指定物品。
    /// 如果数目不足，则取出全部物品；否则取出指定数目的物品。
    /// </summary>
    /// <returns>实际拿到的数目</returns>
    public static int TakeItemFromModData(int itemId, int count, out int inc) {
        lock (centerItemCount) {
            count = (int)Math.Min(count, centerItemCount[itemId]);
            inc = count == 0 ? 0 : (int)(count / centerItemCount[itemId]);
            centerItemCount[itemId] -= count;
            centerItemInc[itemId] -= inc;
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
    public static void ExchangeItem2Item(int takeId, int takeCount, int giveId, int giveCount) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        ItemProto takeProto = LDB.items.Select(takeId);
        ItemProto giveProto = LDB.items.Select(giveId);
        UIMessageBox.Show("提示".Translate(),
            $"{"要花费".Translate()} {takeProto.name} x {takeCount} "
            + $"{"来兑换".Translate()} {giveProto.name} x {giveCount} {"吗？".Translate()}",
            "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => {
                if (!TakeItem(takeId, takeCount, out _)) {
                    return;
                }
                AddItemToPackage(giveId, giveCount);
            },
            null);
    }

    public static void ExchangeItem2Recipe(BaseRecipe recipe) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (recipe == null) {
            UIMessageBox.Show("提示".Translate(),
                "配方不存在，无法兑换！",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (!GameMain.history.ItemUnlocked(itemToMatrix[recipe.InputID])) {
            UIMessageBox.Show("提示".Translate(),
                "当前物品尚未解锁，无法兑换！",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (recipe.IsMaxMemory) {
            UIMessageBox.Show("提示".Translate(),
                "该配方回响数目已达到上限，无需兑换！",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        int takeId = IFE分馏配方通用核心;
        int takeCount = Math.Max(0, recipe.BreakCurrQualityNeedMemory - recipe.Memory);
        if (takeCount == 0) {
            UIMessageBox.Show("提示".Translate(),
                "该配方回响数目已达到突破要求，暂时无法兑换！",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        ItemProto takeProto = LDB.items.Select(takeId);
        UIMessageBox.Show("提示".Translate(),
            $"{"要花费".Translate()} {takeProto.name} x {takeCount} "
            + $"{"来兑换".Translate()} {recipe.TypeNameWC} {"吗？".Translate()}",
            "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => {
                if (!TakeItem(takeId, takeCount, out _)) {
                    return;
                }
                for (int i = 0; i < takeCount; i++) {
                    recipe.RewardThis();
                }
            },
            null);
    }

    public static void ExchangeSand2RecipeExp(BaseRecipe recipe) {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (recipe == null) {
            UIMessageBox.Show("提示".Translate(),
                "配方不存在，无法兑换！",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (recipe.Locked) {
            UIMessageBox.Show("提示".Translate(),
                "配方尚未解锁！",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (recipe.FullUpgrade) {
            UIMessageBox.Show("提示".Translate(),
                "配方已完全升级！",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        if (recipe.IsCurrQualityCurrLevelMaxExp) {
            UIMessageBox.Show("提示".Translate(),
                "配方经验已达上限！",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        int takeId = I沙土;
        float needExp = recipe.StillNeedExp;
        int takeCount = (int)Math.Ceiling(needExp * 0.5);
        ItemProto takeProto = LDB.items.Select(I沙土);
        UIMessageBox.Show("提示".Translate(),
            $"{"要花费".Translate()} {takeProto.name} x {takeCount} "
            + $"{"来兑换".Translate()} {recipe.TypeNameWC} {"配方经验".Translate()} x {(int)needExp} {"吗？".Translate()}",
            "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => {
                if (!TakeItem(takeId, takeCount, out _)) {
                    return;
                }
                recipe.AddExp(needExp, false);
            },
            null);
    }

    public static int GetEssenceMinCount() {
        return Math.Min(GetModDataItemIntCount(IFE复制精华),
            Math.Min(GetModDataItemIntCount(IFE点金精华),
                Math.Min(GetModDataItemIntCount(IFE分解精华),
                    GetModDataItemIntCount(IFE转化精华))));
    }

    /// <summary>
    /// 从Mod数据中拿取每种精华各n个。
    /// 如果数目不足，则不拿取；否则扣除对应物品。
    /// 注意，为了提高性能，此方法未判断某些前置条件。使用时需注意情况。
    /// </summary>
    public static bool TakeEssenceFromModData(int n, int[] consumeRegister) {
        lock (centerItemCount) {
            if (GetModDataItemCount(IFE复制精华) < n
                || GetModDataItemCount(IFE点金精华) < n
                || GetModDataItemCount(IFE分解精华) < n
                || GetModDataItemCount(IFE转化精华) < n) {
                return false;
            }
            TakeItemFromModData(IFE复制精华, n, out _);
            TakeItemFromModData(IFE点金精华, n, out _);
            TakeItemFromModData(IFE分解精华, n, out _);
            TakeItemFromModData(IFE转化精华, n, out _);
            lock (consumeRegister) {
                consumeRegister[IFE复制精华] += n;
                consumeRegister[IFE点金精华] += n;
                consumeRegister[IFE分解精华] += n;
                consumeRegister[IFE转化精华] += n;
            }
            return true;
        }
    }
}
