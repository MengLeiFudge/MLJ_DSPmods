using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using DeliverySlotsTweaks;
using FE.Compatibility;
using FE.UI.View.Setting;
using HarmonyLib;
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

    // public static  bool architectMode => false;

    // if (DeliveryPackagePatch.architectMode)
    // return 999;
    // int num1;
    // DeliveryPackagePatch.packageItemCount.TryGetValue(itemId, out num1);
    // int num2;
    // DeliveryPackagePatch.deliveryItemCount.TryGetValue(itemId, out num2);
    // return num1 + num2;

    #region 向背包添加物品

    /// <summary>
    /// 扔掉的垃圾会自动回收到mod背包
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.ThrowTrash))]
    public static bool Player_ThrowTrash_Prefix(Player __instance, int itemId, int count, int inc) {
        AddItemToModData(itemId, count, inc);
        return false;
    }

    /// <summary>
    /// 扔掉的垃圾会自动回收到mod背包
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.ThrowHandItems))]
    public static bool Player_ThrowHandItems_Prefix(Player __instance) {
        if (__instance.inhandItemId > 0 && __instance.inhandItemCount > 0) {
            AddItemToModData(__instance.inhandItemId, __instance.inhandItemCount, __instance.inhandItemInc);
        }
        __instance.inhandItemId = 0;
        __instance.inhandItemCount = 0;
        __instance.inhandItemInc = 0;
        return false;
    }

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
    /// 放入物品顺序为：背包 -> 物流背包 -> 手上/地上（不会到地上，已改为到mod背包中）
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
            if (package.grids[index].itemId == itemId) {
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
            if (deliveryPackage.grids[gridIndex].itemId == itemId) {
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
    /// 从背包取出的物品数目不够时，使用mod背包补足。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerPackageUtility), nameof(PlayerPackageUtility.TakeItemFromAllPackages))]
    public static bool PlayerPackageUtility_TakeItemFromAllPackages_Prefix(PlayerPackageUtility __instance,
        int gridIndex, ref int itemId, ref int count, ref int inc, bool deliveryFirst, out int[] __state) {
        inc = 0;
        __state = [0, 0];
        if (itemId <= 0 || count <= 0 || itemId == 1099) {
            itemId = 0;
            count = 0;
            return false;
        }
        int takeCount = TakeItemFromModData(itemId, count, out inc);
        if (takeCount == count) {
            return false;
        }
        __state = [takeCount, inc];
        count -= takeCount;
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerPackageUtility), nameof(PlayerPackageUtility.TakeItemFromAllPackages))]
    public static void PlayerPackageUtility_TakeItemFromAllPackages_Postfix(PlayerPackageUtility __instance,
        int gridIndex, ref int itemId, ref int count, ref int inc, bool deliveryFirst, int[] __state) {
        count += __state[0];
        inc += __state[1];
    }

    /// <summary>
    /// 从背包取出的物品数目不够时，使用mod背包补足。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerPackageUtility), nameof(PlayerPackageUtility.TryTakeItemFromAllPackages))]
    public static bool PlayerPackageUtility_TryTakeItemFromAllPackages_Prefix(PlayerPackageUtility __instance,
        ref int itemId, ref int count, ref int inc, bool deliveryFirst, out int[] __state) {
        inc = 0;
        __state = [0, 0];
        if (itemId <= 0 || count <= 0 || itemId == 1099) {
            itemId = 0;
            count = 0;
            return false;
        }
        int takeCount = TakeItemFromModData(itemId, count, out inc);
        if (takeCount == count) {
            return false;
        }
        __state = [takeCount, inc];
        count -= takeCount;
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerPackageUtility), nameof(PlayerPackageUtility.TryTakeItemFromAllPackages))]
    public static void PlayerPackageUtility_TryTakeItemFromAllPackages_Postfix(PlayerPackageUtility __instance,
        ref int itemId, ref int count, ref int inc, bool deliveryFirst, int[] __state) {
        count += __state[0];
        inc += __state[1];
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

    /// <summary>
    /// 原版分割增产点数的方法。
    /// </summary>
    /// <param name="n">物品总数</param>
    /// <param name="m">物品总增产点数</param>
    /// <param name="p">要取走的物品数目</param>
    /// <returns>被取走的物品增产点数</returns>
    public static int split_inc(ref int n, ref int m, int p) {
        int num1 = m / n;
        int num2 = m - num1 * n;
        n -= p;
        int num3 = num2 - n;
        int num4 = num3 > 0 ? num1 * p + num3 : num1 * p;
        m -= num4;
        return num4;
    }

    #endregion

    #region 建造时修正可使用物品数目

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(ConstructionModuleComponent), nameof(ConstructionModuleComponent.PlaceItems))]
    public static IEnumerable<CodeInstruction> PlaceItems_Transpiler(IEnumerable<CodeInstruction> instructions) {
        if (Compatibility.DeliverySlotsTweaks.Enable) {
            return instructions;
        }
        try {
            var codeMacher = new CodeMatcher(instructions);

            /*
            if (this.entityId == 0)
            {
                StorageComponent package = player.package;
                for (int i = 0; i < package.size; i++) {
                    ...
                    num += count;
                }
                AddConstructableCountsInStorage(this, player, ref num); // Insert method here
            }
            else if (this.entityId > 0)
            {
                ...
            */

            codeMacher.MatchForward(false,
                    new CodeMatch(OpCodes.Br),
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldfld,
                        AccessTools.Field(typeof(ConstructionModuleComponent),
                            nameof(ConstructionModuleComponent.entityId))),
                    new CodeMatch(OpCodes.Ldc_I4_0),
                    new CodeMatch(OpCodes.Ble)
                )
                .Insert(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldarg_2),
                    new CodeInstruction(OpCodes.Ldloca_S, (byte)0),// num += count; in the loop
                    new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(Utils), nameof(AddConstructableCountsInStorage)))
                );

            // Replace player.package.TakeTailItems

            codeMacher
                .MatchForward(true,
                    new CodeMatch(OpCodes.Ldarg_2),
                    new CodeMatch(OpCodes.Callvirt,
                        AccessTools.DeclaredPropertyGetter(typeof(Player), nameof(Player.package))),
                    new CodeMatch(OpCodes.Ldloca_S),
                    new CodeMatch(OpCodes.Ldloca_S),
                    new CodeMatch(OpCodes.Ldloca_S),
                    new CodeMatch(OpCodes.Ldc_I4_0),
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt && ((MethodInfo)i.operand).Name == "TakeTailItems")
                )
                .Repeat(matcher => matcher.SetAndAdvance(OpCodes.Call,
                    AccessTools.Method(typeof(Utils), nameof(TakeTailItems)))
                );

            return codeMacher.InstructionEnumeration();
        }
        catch (Exception e) {
            Plugin.Log.LogWarning("Transpiler PlaceItems error");
            Plugin.Log.LogWarning(e);
            return instructions;
        }
    }

    public static void AddConstructableCountsInStorage(ConstructionModuleComponent constructionModule, Player player,
        ref int num) {
        LogInfo("AddConstructableCountsInStorage");
        var array = constructionModule.constructableCountsInStorage;
        foreach (var itemId in ItemProto.constructableIdHash) {
            int count = (int)Math.Min(int.MaxValue, GetModDataItemCount(itemId));
            int index = ItemProto.constructableIndiceById[itemId];
            array[index].haveCount += count;
            num += count;
        }
    }

    public static bool architectMode = false;

    public static void TakeTailItems(StorageComponent storage, ref int itemId, ref int count, out int inc, bool _) {
        LogInfo($"TakeTailItems1, itemId: {itemId}, count: {count}");
        if (architectMode) {
            inc = 0;
            return;
        }
        if (NebulaMultiplayerModAPI.IsActive && NebulaMultiplayerModAPI.IsOthers()) {
            inc = 0;
            return;
        }

        // if (deliveryGridindex.TryGetValue(itemId, out int gridindex))
        // {
        //     GameMain.mainPlayer.packageUtility.TakeItemFromAllPackages(gridindex, ref itemId, ref count, out inc, false);
        //     if (packageItemCount.ContainsKey(itemId))
        //     {
        //         int num = packageItemCount[itemId] - count;
        //         packageItemCount[itemId] = num;
        //         if (num <= 0) packageItemCount.Remove(itemId);
        //     }
        //     else if (deliveryItemCount.ContainsKey(itemId))
        //     {
        //         int num = deliveryItemCount[itemId] - count;
        //         deliveryItemCount[itemId] = num;
        //         if (num <= 0) deliveryItemCount.Remove(itemId);
        //     }
        //
        // }
        // else
        // {
        //     storage.TakeTailItems(ref itemId, ref count, out inc, false);
        //     if (packageItemCount.ContainsKey(itemId))
        //     {
        //         int num = packageItemCount[itemId] - count;
        //         packageItemCount[itemId] = num;
        //         if (num <= 0) packageItemCount.Remove(itemId);
        //     }
        // }

        GameMain.mainPlayer.packageUtility.TryTakeItemFromAllPackages(ref itemId, ref count, out inc);
        LogInfo($"TakeTailItems2, itemId: {itemId}, count: {count}, inc: {inc}");
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
