using System;
using FE.Compatibility;
using FE.Logic.Manager;
using FE.UI.MainPanel.Setting;
using NebulaAPI;
using FE.Logic.Progression;
using static FE.Logic.Manager.ItemManager;

namespace FE.Utils;

public static partial class Utils {
    /// <summary>
    /// 从ModData背包取出指定物品，再将其放入玩家背包/物流背包/手上。
    /// 如果数目不足，则取出全部物品；否则取出指定数目的物品。
    /// </summary>
    /// <param name="itemId">要转移的物品ID</param>
    /// <param name="leftClick">是否为左键；左键和右键对应不同倍率</param>
    public static void ClickToMoveModDataItem(int itemId, bool leftClick) {
        ItemProto item = LDB.items.Select(itemId);
        if (item == null) {
            return;
        }
        int count = leftClick
            ? item.StackSize * Miscellaneous.LeftClickTakeCount
            : item.StackSize * Miscellaneous.RightClickTakeCount;
        int inc;
        lock (centerItemCount) {
            count = TakeItemFromModData(itemId, count, out inc, true);
        }
        if (count > 0) {
            ManualExtractCount++;
        }
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
                AddItemToPackage(itemId, count, inc, true);
            }
        }
    }

    /// <summary>
    /// 获取当前 MOD 数据中残片的可用数目。
    /// </summary>
    public static int GetFragmentMinCount() {
        long count = centerItemCount[IFE残片];
        return (int)Math.Min(int.MaxValue, count);
    }

    /// <summary>
    /// 从 Mod 数据中拿取 n 个残片。
    /// 如果数目不足，则不拿取；否则扣除对应物品。
    /// </summary>
    /// <param name="n">要扣除的残片数量</param>
    /// <param name="consumeRegister">消耗登记表，会在残片索引上累加本次消耗</param>
    /// <returns>扣除成功返回 true；库存不足返回 false</returns>
    public static bool TakeFragmentsFromModData(int n, int[] consumeRegister) {
        if (centerItemCount[IFE残片] < n) {
            return false;
        }
        lock (centerItemCount) {
            TakeItemFromModData(IFE残片, n, out _);
        }
        lock (consumeRegister) {
            consumeRegister[IFE残片] += n;
        }
        return true;
    }

    /// <summary>
    /// 旧版精华命名接口，仅为兼容保留。
    /// </summary>
    public static int GetEssenceMinCount() => GetFragmentMinCount();

    /// <summary>
    /// 旧版精华命名接口，仅为兼容保留。
    /// </summary>
    public static bool TakeEssenceFromModData(int n, int[] consumeRegister) =>
        TakeFragmentsFromModData(n, consumeRegister);

    /// <summary>
    /// 将指定物品添加到ModData背包
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <param name="count">新增数量</param>
    /// <param name="inc">新增增产点数总量</param>
    /// <param name="manual">是否为手动操作（多人模式下会触发同步包）</param>
    public static void AddItemToModData(int itemId, int count, int inc = 0, bool manual = false) {
        if (itemId == I沙土) {
            GameMain.mainPlayer.sandCount += count;
            return;
        }
        if (itemId <= 0 || itemId >= 12000) {
            return;
        }
        lock (centerItemCount) {
            centerItemCount[itemId] += count;
            centerItemInc[itemId] += inc;
            if (itemId >= IFE交互塔 && itemId <= IFE精馏塔) {
                TechManager.CheckTechUnlockCondition(itemId);
            }
        }
        if (NebulaModAPI.IsMultiplayerActive && manual) {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new CenterItemChangePacket(itemId, count, inc));
        }
    }

    /// <summary>
    /// 获取MOD数据中指定物品的数量。
    /// </summary>
    public static long GetModDataItemCount(int itemId) {
        return GetModDataItemCount(itemId, out _);
    }

    /// <summary>
    /// 获取MOD数据中指定物品的数量。
    /// </summary>
    public static long GetModDataItemCount(int itemId, out long inc) {
        inc = 0;
        if (itemId == I沙土) {
            //如果是沙盒模式并且无限沙土开启，直接返回long最大值
            if (GameMain.data.history.HasFeatureKey(1100001) && GameMain.sandboxToolsEnabled) {
                return long.MaxValue;
            }
            return GameMain.mainPlayer.sandCount;
        }
        if (itemId <= 0 || itemId >= 12000) {
            return 0;
        }
        inc = centerItemInc[itemId];
        return centerItemCount[itemId];
    }

    /// <summary>
    /// 从ModData背包取出指定物品。
    /// 如果数目不足，则取出全部物品；否则取出指定数目的物品。
    /// 注意，通过此方法取出的物品数目应该远小于int.MaxValue，以避免增产点数超过int。
    /// </summary>
    /// <param name="itemId">物品ID</param>
    /// <param name="count">期望取出数量</param>
    /// <param name="inc">返回实际取出物品携带的增产点数总量</param>
    /// <param name="manual">是否为手动操作（多人模式下会触发同步包）</param>
    /// <returns>实际拿到的数目</returns>
    public static int TakeItemFromModData(int itemId, int count, out int inc, bool manual = false) {
        //如果是沙土，直接拿取
        if (itemId == I沙土) {
            inc = 0;
            if (GameMain.mainPlayer.sandCount >= count) {
                //count不变，表示成功拿取count个沙土
                GameMain.mainPlayer.sandCount -= count;
                return count;
            } else {
                count = (int)GameMain.mainPlayer.sandCount;
                GameMain.mainPlayer.sandCount = 0;
                return count;
            }
        }
        if (itemId <= 0 || itemId >= 12000) {
            inc = 0;
            return 0;
        }
        lock (centerItemCount) {
            count = (int)Math.Min(count, centerItemCount[itemId]);
            count = Math.Min(100000, count);
            if (count <= 0) {
                inc = 0;
                return 0;
            }
            if (centerItemInc[itemId] / centerItemCount[itemId] >= 4) {
                //如果平均增产点数大于等于4，按照原版分割
                inc = (int)split_inc(ref centerItemCount[itemId], ref centerItemInc[itemId], count);
            } else {
                //否则尽量输出4点
                if (centerItemInc[itemId] >= count * 4) {
                    centerItemCount[itemId] -= count;
                    inc = count * 4;
                    centerItemInc[itemId] -= inc;
                } else {
                    centerItemCount[itemId] -= count;
                    inc = (int)centerItemInc[itemId];
                    centerItemInc[itemId] = 0;
                }
            }
            if (NebulaModAPI.IsMultiplayerActive && manual) {
                NebulaModAPI.MultiplayerSession.Network.SendPacket(new CenterItemChangePacket(itemId, -count, -inc));
            }
            return count;
        }
    }

    /// <summary>
    /// 当某个分馏塔在数据中心存储的数目超过1000时，取走10%
    /// </summary>
    /// <param name="itemId">分馏塔物品ID</param>
    /// <returns>实际取出数量</returns>
    public static int Take10PercentTower(int itemId) {
        if (itemId <= 0 || itemId >= 12000) {
            return 0;
        }
        return centerItemCount[itemId] < 1000
            ? 0
            : TakeItemFromModData(itemId, (int)Math.Min(int.MaxValue, centerItemCount[itemId] / 10), out _);
    }
}
