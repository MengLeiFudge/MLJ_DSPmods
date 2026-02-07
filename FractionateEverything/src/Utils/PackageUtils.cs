using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BuildBarTool;
using CommonAPI.Patches;
using FE.Compatibility;
using FE.Logic.Manager;
using FE.UI.View.Setting;
using HarmonyLib;
using NebulaAPI;
using static FE.Logic.Manager.ItemManager;

namespace FE.Utils;

public static partial class Utils {
    /// <summary>
    /// 建筑师模式，所有建筑数目显示为999且建造时不消耗
    /// </summary>
    private static bool ArchitectMode => Multfunction_mod.ArchitectMode
                                         || CheatEnabler.ArchitectMode
                                         || DeliverySlotsTweaks.ArchitectMode;

    /// <summary>
    /// 指示物品交互科技是否已解锁
    /// </summary>
    public static bool TechItemInteractionUnlocked => GameMain.history.TechUnlocked(TFE物品交互);

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
            ? item.StackSize * Miscellaneous.LeftClickTakeCount
            : item.StackSize * Miscellaneous.RightClickTakeCount;
        int inc;
        lock (centerItemCount) {
            count = TakeItemFromModData(itemId, count, out inc, true);
        }
        if (itemId == I沙土) {
            if (GameMain.mainPlayer.inhandItemId != I沙土) {
                GameMain.mainPlayer.ThrowHandItems();
            }
            GameMain.mainPlayer.inhandItemId = I沙土;
            GameMain.mainPlayer.inhandItemCount += count;
        } else {
            AddItemToPackage(itemId, count, inc, false);
        }
    }

    /// <summary>
    /// 获取当前MOD数据中最少的精华的数目。
    /// </summary>
    public static int GetEssenceMinCount() {
        long minCount = Math.Min(centerItemCount[IFE速度精华], centerItemCount[IFE产能精华]);
        minCount = Math.Min(minCount, centerItemCount[IFE节能精华]);
        minCount = Math.Min(minCount, centerItemCount[IFE增产精华]);
        return (int)Math.Min(int.MaxValue, minCount);
    }

    /// <summary>
    ///     从Mod数据中拿取每种精华各n个。
    ///     如果数目不足，则不拿取；否则扣除对应物品。
    /// </summary>
    public static bool TakeEssenceFromModData(int n, int[] consumeRegister) {
        if (centerItemCount[IFE速度精华] < n
            || centerItemCount[IFE产能精华] < n
            || centerItemCount[IFE节能精华] < n
            || centerItemCount[IFE增产精华] < n) {
            return false;
        }
        lock (centerItemCount) {
            TakeItemFromModData(IFE速度精华, n, out _);
            TakeItemFromModData(IFE产能精华, n, out _);
            TakeItemFromModData(IFE节能精华, n, out _);
            TakeItemFromModData(IFE增产精华, n, out _);
        }
        lock (consumeRegister) {
            consumeRegister[IFE速度精华] += n;
            consumeRegister[IFE产能精华] += n;
            consumeRegister[IFE节能精华] += n;
            consumeRegister[IFE增产精华] += n;
        }
        return true;
    }

    #region 向背包添加物品

    //配送器与玩家交互：GameMain.mainPlayer.packageUtility.AddItemToAllPackages
    //此方法有三种模式，分别为：
    //priorityMode<0: 先物流背包，再背包
    //priorityMode>0: 先背包，再物流背包
    //priorityMode=0: 背包中属于这个物品的格子（有物品或者是筛选格）填满，再物流背包，最后背包

    //除了配送器都用这个：GameMain.mainPlayer.TryAddItemToPackage
    //此方法顺序固定为：先背包，再物流背包，最后扔出去或者手上

    /// <summary>
    /// 扔掉的垃圾会自动回收到Mod背包
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.ThrowTrash))]
    private static bool Player_ThrowTrash_Prefix(Player __instance, int itemId, int count, int inc) {
        if (!TechItemInteractionUnlocked) {
            return true;
        }
        if (itemValue[itemId] >= maxValue) {
            return true;
        }
        AddItemToModData(itemId, count, inc, true);
        return false;
    }

    /// <summary>
    /// 扔掉的垃圾会自动回收到Mod背包
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.ThrowHandItems))]
    private static bool Player_ThrowHandItems_Prefix(Player __instance) {
        if (!TechItemInteractionUnlocked) {
            return true;
        }
        if (itemValue[__instance.inhandItemId] >= maxValue) {
            return true;
        }
        if (__instance.inhandItemId > 0 && __instance.inhandItemCount > 0) {
            AddItemToModData(__instance.inhandItemId, __instance.inhandItemCount, __instance.inhandItemInc, true);
        }
        __instance.inhandItemId = 0;
        __instance.inhandItemCount = 0;
        __instance.inhandItemInc = 0;
        return false;
    }

    /// <summary>
    /// 将指定物品添加到ModData背包
    /// </summary>
    public static void AddItemToModData(int itemId, int count, int inc = 0, bool manual = false) {
        if (itemId == I沙土) {
            GameMain.mainPlayer.sandCount += count;
            return;
        }
        lock (centerItemCount) {
            centerItemCount[itemId] += count;
            centerItemInc[itemId] += inc;
            if (itemId >= IFE交互塔 && itemId <= IFE转化塔) {
                TechManager.CheckTechUnlockCondition(itemId);
            }
        }
        if (NebulaModAPI.IsMultiplayerActive && manual) {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(new CenterItemChangePacket(itemId, count, inc));
        }
    }

    /// <summary>
    /// 将指定物品添加到背包，并在左侧显示物品变动。
    /// 放入物品顺序为：背包 -> 物流背包 -> 手上/Mod背包
    /// </summary>
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

    //StorageComponent获取物品数目方法一览：
    //public int GetItemCount(int itemId)：player.package多处调用此方法
    //public int GetItemCount(int itemId, out int inc)：player.package不调用此方法

    /// <summary>
    /// 获取玩家持有的物品数目时，返回 背包+物流背包+Mod背包 的物品总数
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPriority(Priority.High)]
    [HarmonyAfter(BuildToolOpt.GUID)]
    [HarmonyPatch(typeof(BuildTool_Reform), nameof(BuildTool_Reform.ReformAction))]
    [HarmonyPatch(typeof(BuildTool_Reform), nameof(BuildTool_Reform.RemoveBasePit))]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.EntityAutoReplenishIfNeeded))]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.StationAutoReplenishIfNeeded))]
    [HarmonyPatch(typeof(PlayerAction_Inspect), nameof(PlayerAction_Inspect.GameTick))]
    [HarmonyPatch(typeof(UIBlueprintInspector), nameof(UIBlueprintInspector.SetComponentItem))]
    [HarmonyPatch(typeof(UIBlueprintInspector), nameof(UIBlueprintInspector._OnUpdate))]
    [HarmonyPatch(typeof(UIBuildMenu), nameof(UIBuildMenu.OnChildButtonClick))]
    [HarmonyPatch(typeof(UIBuildMenu), nameof(UIBuildMenu.SetCurrentCategory))]
    [HarmonyPatch(typeof(UIBuildMenu), nameof(UIBuildMenu._OnUpdate))]
    [HarmonyPatch(typeof(UIControlPanelObjectEntry), nameof(UIControlPanelObjectEntry.ReplenishItems))]
    [HarmonyPatch(typeof(UIHandTip), nameof(UIHandTip._OnUpdate))]
    [HarmonyPatch(typeof(UIItemup), nameof(UIItemup.Up))]
    [HarmonyPatch(typeof(UIRemoveBasePitButton), nameof(UIRemoveBasePitButton._OnUpdate))]
    [HarmonyPatch(typeof(UIReplicatorWindow), nameof(UIReplicatorWindow._OnUpdate))]
    [HarmonyPatch(typeof(UISandboxMenu), nameof(UISandboxMenu.OnChildButtonClick))]
    [HarmonyPatch(typeof(UITurretWindow), nameof(UITurretWindow.OnHandFillAmmoButtonClick))]
    public static IEnumerable<CodeInstruction> GetItemCount_Transpiler(IEnumerable<CodeInstruction> instructions) {
        try {
            // Replace player.package.GetItemCount(int itemId)
            var method = AccessTools.Method(typeof(StorageComponent), nameof(StorageComponent.GetItemCount),
                [typeof(int)]);
            var codeMatcher = new CodeMatcher(instructions)
                .MatchForward(false,
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt
                                       && i.operand.Equals(method)))
                .Repeat(matcher => matcher
                    .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(Utils), nameof(GetItemCount))));
            return codeMatcher.InstructionEnumeration();
        }
        catch (Exception ex) {
            LogError($"Error in GetItemCount_Transpiler: {ex}");
            return instructions;
        }
    }

    /// <summary>
    /// 获取玩家持有的物品数目时，返回 背包+物流背包+Mod背包 的物品总数
    /// </summary>
    private static int GetItemCount(StorageComponent storage, int itemId) {
        if (storage != GameMain.mainPlayer?.package) {
            return storage.GetItemCount(itemId);
        }
        ItemProto item = LDB.items.Select(itemId);
        if (item == null) {
            return 0;
        }
        if (ArchitectMode && item.BuildMode != 0) {
            return 999;
        }
        return (int)Math.Min(int.MaxValue, GetItemTotalCount(itemId));
    }

    /// <summary>
    /// 背包物品总数、快捷建造栏的相关修改也兼容BuildBarTool
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPriority(Priority.High)]
    [HarmonyPatch(typeof(BuildBarToolPlugin), nameof(BuildBarToolPlugin.UIBuildMenuSetCurrentCategoryPostPatch))]
    [HarmonyPatch(typeof(BuildBarToolPlugin), nameof(BuildBarToolPlugin.UIBuildMenuOnUpdatePostPatch),
        [typeof(UIBuildMenu)], [ArgumentType.Ref])]
    [HarmonyPatch(typeof(BuildBarToolPlugin), nameof(BuildBarToolPlugin.OnChildButtonClick))]
    private static IEnumerable<CodeInstruction> BuildBarTool_Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase original) {
        try {
            //移除以下代码：
            //if (DeliverySlotsTweaksCompat.enabled)
            //    itemCount += uiBuildMenu.player.deliveryPackage.GetItemCount(id);
            var codeMatcher = new CodeMatcher(instructions)
                .MatchForward(false,
                    new CodeMatch(OpCodes.Ldsfld, AccessTools.Field(typeof(DeliverySlotsTweaksCompat), "enabled")),
                    new CodeMatch(OpCodes.Stloc_S),
                    new CodeMatch(OpCodes.Ldloc_S),
                    new CodeMatch(OpCodes.Brfalse)
                );
            //查IL可知要将12行设置为Nop
            if (codeMatcher.IsValid && codeMatcher.Length >= codeMatcher.Pos + 12) {
                for (int i = 0; i <= 12; i++) {
                    codeMatcher.SetAndAdvance(OpCodes.Nop, null);
                }
            } else {
                LogWarning($"MethodBase {original}, DeliverySlotsTweaksCompat.enabled not found");
            }
            // Replace player.package.GetItemCount(int itemId)
            var method = AccessTools.Method(typeof(StorageComponent), nameof(StorageComponent.GetItemCount),
                [typeof(int)]);
            codeMatcher = new CodeMatcher(codeMatcher.InstructionEnumeration())
                .MatchForward(false,
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt
                                       && i.operand.Equals(method)))
                .Repeat(matcher => matcher
                    .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(Utils), nameof(GetItemCount))));
            // Replace history.ItemUnlocked(int itemId)
            method = AccessTools.Method(typeof(GameHistoryData), nameof(GameHistoryData.ItemUnlocked),
                [typeof(int)]);
            codeMatcher = new CodeMatcher(codeMatcher.InstructionEnumeration())
                .MatchForward(false,
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt
                                       && i.operand.Equals(method)))
                .Repeat(matcher => matcher
                    .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(Utils), nameof(ItemUnlocked))));
            return codeMatcher.InstructionEnumeration();
        }
        catch (Exception ex) {
            LogError($"Error in GetItemCount_Transpiler: {ex}");
            return instructions;
        }
    }

    /// <summary>
    /// 某个建筑在所有背包的物品总数大于0时，无论是否已解锁，都在快捷建造栏、物品选择界面显示。
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPriority(Priority.High)]
    [HarmonyPatch(typeof(UIBuildMenu), nameof(UIBuildMenu.OnChildButtonClick))]
    [HarmonyPatch(typeof(UIBuildMenu), nameof(UIBuildMenu.SetCurrentCategory))]
    [HarmonyPatch(typeof(UIBuildMenu), nameof(UIBuildMenu._OnUpdate))]
    [HarmonyPatch(typeof(UIItemPicker_Patch), nameof(UIItemPicker_Patch.CheckItem))]
    [HarmonyPatch(typeof(BuildBarToolPlugin), nameof(BuildBarToolPlugin.UIBuildMenuOnUpdatePostPatch),
        [typeof(UIFunctionPanel)], [ArgumentType.Ref])]
    private static IEnumerable<CodeInstruction> ItemUnlocked_Transpiler(IEnumerable<CodeInstruction> instructions) {
        try {
            // Replace history.ItemUnlocked(int itemId)
            var method = AccessTools.Method(typeof(GameHistoryData), nameof(GameHistoryData.ItemUnlocked),
                [typeof(int)]);
            var codeMatcher = new CodeMatcher(instructions)
                .MatchForward(false,
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt
                                       && i.operand.Equals(method)))
                .Repeat(matcher => matcher
                    .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(Utils), nameof(ItemUnlocked))));
            return codeMatcher.InstructionEnumeration();
        }
        catch (Exception ex) {
            LogError($"Error in ItemUnlocked_Transpiler: {ex}");
            return instructions;
        }
    }

    /// <summary>
    /// 某个建筑在所有背包的物品总数大于0时，无论是否已解锁，都在快捷建造栏、物品选择界面显示。
    /// </summary>
    private static bool ItemUnlocked(GameHistoryData history, int itemId) {
        return history.ItemUnlocked(itemId) || GetItemTotalCount(itemId) > 0;
    }

    /// <summary>
    /// 修正建造可用的物品数目
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPriority(Priority.High)]
    [HarmonyPatch(typeof(ConstructionModuleComponent), nameof(ConstructionModuleComponent.PlaceItems))]
    private static IEnumerable<CodeInstruction> PlaceItems_Transpiler(IEnumerable<CodeInstruction> instructions) {
        try {
            var codeMatcher = new CodeMatcher(instructions);
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
            codeMatcher.MatchForward(false,
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
            return codeMatcher.InstructionEnumeration();
        }
        catch (Exception ex) {
            LogError($"Error in PlaceItems_Transpiler: {ex}");
            return instructions;
        }
    }

    /// <summary>
    /// 修正建造可用的物品数目
    /// </summary>
    private static void AddConstructableCountsInStorage(ConstructionModuleComponent _this, Player player,
        ref int num) {
        num = 0;
        foreach (var itemId in ItemProto.constructableIdHash) {
            int count = GetItemCount(player.package, itemId);
            int index = ItemProto.constructableIndiceById[itemId];
            _this.constructableCountsInStorage[index].haveCount = count;
            num += count;
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
        inc = centerItemInc[itemId];
        return centerItemCount[itemId];
    }

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

    //StorageComponent取出物品方法一览（下面说明了是否关联，以及关联到背包还是测试背包）：
    //[背包][测试背包]int TakeItem(int itemId, int count, out int inc)
    //[不关联]void TakeItemFromGrid(int gridIndex, ref int itemId, ref int count, out int inc)
    //[不关联]void TakeHeadItems(ref int itemId, ref int count, out int inc)
    //[背包][测试背包]void TakeTailItems(ref int itemId, ref int count, out int inc, bool useBan = false)
    //[不关联]bool TakeTailItems(ref int itemId, ref int count, int[] needs, out int inc, bool useBan = false)
    //[不关联]void TakeTailItemsFiltered(ref int filter, ref int count, out int inc, bool useBan = false)
    //[测试背包]void void TakeTailItemsWithIncTable(int itemId, ref int count, out int inc, ref int[] incTable, bool useBan = false)
    //[背包]bool TakeTailItemsByIncTable(int itemId, out int count, ref int[] incTable, bool useBan = false)

    /// <summary>
    /// 按照玩家设定的顺序，从各个背包拿取物品。
    /// 使用前需要检测是不是目标背包。
    /// </summary>
    private static void TakeItemInternal(this StorageComponent storage, int itemId, int needCount, out int realCount,
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
        if (ArchitectMode && item.BuildMode != 0) {
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

    #region Player.package

    /// <summary>
    /// 从玩家背包获取物品时，可以从 背包/物流背包/Mod背包 中获取
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPriority(Priority.High)]
    [HarmonyPatch(typeof(Mecha), nameof(Mecha.AutoReplenishAmmo))]
    [HarmonyPatch(typeof(Mecha), nameof(Mecha.AutoReplenishBomb))]
    [HarmonyPatch(typeof(Mecha), nameof(Mecha.AutoReplenishFuel))]
    [HarmonyPatch(typeof(Mecha), nameof(Mecha.AutoReplenishFuelAll))]
    [HarmonyPatch(typeof(Mecha), nameof(Mecha.AutoReplenishWarper))]
    [HarmonyPatch(typeof(MechaForge), nameof(MechaForge.AddTaskIterate))]
    private static IEnumerable<CodeInstruction> TakeItem_Transpiler(IEnumerable<CodeInstruction> instructions) {
        try {
            // Replace player.package.TakeItem(int itemId, int count, out int inc)
            var method = AccessTools.Method(typeof(StorageComponent), nameof(StorageComponent.TakeItem),
                [typeof(int), typeof(int), typeof(int).MakeByRefType()]);
            var codeMatcher = new CodeMatcher(instructions)
                .MatchForward(false,
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt
                                       && i.operand.Equals(method)))
                .Repeat(matcher => matcher
                    .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(Utils), nameof(TakeItem))));
            return codeMatcher.InstructionEnumeration();
        }
        catch (Exception ex) {
            LogError($"Error in TakeItem_Transpiler: {ex}");
            return instructions;
        }
    }

    /// <summary>
    /// 从玩家背包获取物品时，可以从 背包/物流背包/Mod背包 中获取
    /// </summary>
    private static int TakeItem(StorageComponent storage, int itemId, int count, out int inc) {
        if (storage != GameMain.mainPlayer.package) {
            return storage.TakeItem(itemId, count, out inc);
        }
        storage.TakeItemInternal(itemId, count, out int realCount, out inc);
        return realCount;
    }

    /// <summary>
    /// 从玩家背包获取物品时，可以从 背包/物流背包/Mod背包 中获取
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPriority(Priority.High)]
    [HarmonyPatch(typeof(BuildTool_Addon), nameof(BuildTool_Addon.CreatePrebuilds))]
    [HarmonyPatch(typeof(BuildTool_BlueprintPaste), nameof(BuildTool_BlueprintPaste.CreatePrebuilds))]
    [HarmonyPatch(typeof(BuildTool_BlueprintPaste), nameof(BuildTool_BlueprintPaste.DetermineReforms))]
    [HarmonyPatch(typeof(BuildTool_Click), nameof(BuildTool_Click.CreatePrebuilds))]
    [HarmonyPatch(typeof(BuildTool_Inserter), nameof(BuildTool_Inserter.CreatePrebuilds))]
    [HarmonyPatch(typeof(BuildTool_Path), nameof(BuildTool_Path.CreatePrebuilds))]
    [HarmonyPatch(typeof(BuildTool_Reform), nameof(BuildTool_Reform.ReformAction))]
    [HarmonyPatch(typeof(BuildTool_Reform), nameof(BuildTool_Reform.RemoveBasePit))]
    [HarmonyPatch(typeof(ConstructionModuleComponent), nameof(ConstructionModuleComponent.PlaceItems))]
    [HarmonyPatch(typeof(MechaLab), nameof(MechaLab.ManageSupply))]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.EntityAutoReplenishIfNeeded))]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.StationAutoReplenishIfNeeded))]
    [HarmonyPatch(typeof(Player), nameof(Player.TakeItemFromPlayer))]
    [HarmonyPatch(typeof(PlayerAction_Build), nameof(PlayerAction_Build.DoUpgradeObject))]
    [HarmonyPatch(typeof(PlayerAction_Inspect), nameof(PlayerAction_Inspect.GameTick))]
    [HarmonyPatch(typeof(PlayerPackageUtility), nameof(PlayerPackageUtility.TakeItemFromAllPackages))]
    [HarmonyPatch(typeof(PlayerPackageUtility), nameof(PlayerPackageUtility.TryTakeItemFromAllPackages))]
    [HarmonyPatch(typeof(UIControlPanelObjectEntry), nameof(UIControlPanelObjectEntry.ReplenishItems))]
    [HarmonyPatch(typeof(UITurretWindow), nameof(UITurretWindow.OnHandFillAmmoButtonClick))]
    public static IEnumerable<CodeInstruction> TakeTailItems_Transpiler(IEnumerable<CodeInstruction> instructions) {
        try {
            // Replace player.package.TakeTailItems(ref int itemId, ref int count, out int inc, bool useBan = false)
            var method = AccessTools.Method(typeof(StorageComponent), nameof(StorageComponent.TakeTailItems),
                [typeof(int).MakeByRefType(), typeof(int).MakeByRefType(), typeof(int).MakeByRefType(), typeof(bool)]);
            var codeMatcher = new CodeMatcher(instructions)
                .MatchForward(false,
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt
                                       && i.operand.Equals(method)))
                .Repeat(matcher => matcher
                    .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(Utils), nameof(TakeTailItems))));
            return codeMatcher.InstructionEnumeration();
        }
        catch (Exception ex) {
            LogError($"Error in TakeTailItems_Transpiler: {ex}");
            return instructions;
        }
    }

    /// <summary>
    /// 从玩家背包获取物品时，可以从 背包/物流背包/Mod背包 中获取
    /// </summary>
    private static void TakeTailItems(StorageComponent storage, ref int itemId, ref int count, out int inc,
        bool useBan = false) {
        if (storage != GameMain.mainPlayer.package) {
            storage.TakeTailItems(ref itemId, ref count, out inc, useBan);
            return;
        }
        storage.TakeItemInternal(itemId, count, out int realCount, out inc, useBan);
        if (realCount == 0) {
            itemId = 0;
        }
        count = realCount;
    }

    /// <summary>
    /// 从玩家背包获取物品时，可以从 背包/物流背包/Mod背包 中获取
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPriority(Priority.High)]
    [HarmonyPatch(typeof(BuildTool_BlueprintPaste), nameof(BuildTool_BlueprintPaste.DetermineReforms))]
    private static IEnumerable<CodeInstruction> TakeTailItemsByIncTable_Transpiler(
        IEnumerable<CodeInstruction> instructions) {
        try {
            // Replace player.package.TakeTailItemsByIncTable(int itemId, out int count, ref int[] incTable, bool useBan = false)
            var method = AccessTools.Method(typeof(StorageComponent), nameof(StorageComponent.TakeTailItemsByIncTable),
                [typeof(int), typeof(int).MakeByRefType(), typeof(int[]).MakeByRefType(), typeof(bool)]);
            var codeMatcher = new CodeMatcher(instructions)
                .MatchForward(false,
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt
                                       && i.operand.Equals(method)))
                .Repeat(matcher => matcher
                    .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(Utils), nameof(TakeTailItemsByIncTable))));
            return codeMatcher.InstructionEnumeration();
        }
        catch (Exception ex) {
            LogError($"Error in TakeTailItemsByIncTable_Transpiler: {ex}");
            return instructions;
        }
    }

    /// <summary>
    /// 从玩家背包获取物品时，可以从 背包/物流背包/Mod背包 中获取
    /// </summary>
    private static bool TakeTailItemsByIncTable(StorageComponent storage, int itemId, out int count, ref int[] incTable,
        bool useBan = false) {
        if (storage != GameMain.mainPlayer.package) {
            return storage.TakeTailItemsByIncTable(itemId, out count, ref incTable, useBan);
        }
        count = 0;
        for (int i = 0; i < incTable.Length; i++) {
            count += incTable[i];
        }
        storage.TakeItemInternal(itemId, count, out int realCount, out int inc, useBan);
        count = realCount;
        for (int i = incTable.Length - 1; i >= 0; i--) {
            if (realCount >= incTable[i]) {
                incTable[i] = 0;
                realCount -= incTable[i];
            } else {
                incTable[i] -= realCount;
                return false;
            }
        }
        return true;
    }

    #endregion

    /// <summary>
    /// 临时背包已经消耗的物品数目
    /// </summary>
    private static readonly int[] testPackageUsedCounts = new int[12000];

    #region MechaForge._test_storage

    /// <summary>
    /// 初始化临时背包已经消耗的物品数目
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MechaForge), nameof(MechaForge.TryAddTask))]
    [HarmonyPatch(typeof(MechaForge), nameof(MechaForge.TryTaskWithTestPackage))]
    private static bool MechaForge_ClearTestPackageUsedCounts(MechaForge __instance) {
        Array.Clear(testPackageUsedCounts, 0, 12000);
        return true;
    }

    /// <summary>
    /// 从临时玩家背包获取物品时，返回正确的可用物品总数
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPriority(Priority.High)]
    [HarmonyPatch(typeof(MechaForge), nameof(MechaForge.TryAddTaskIterate))]
    private static IEnumerable<CodeInstruction> TryTakeItem_Transpiler(IEnumerable<CodeInstruction> instructions) {
        try {
            // Replace _test_storage.TakeItem(int itemId, int count, out int inc)
            var method = AccessTools.Method(typeof(StorageComponent), nameof(StorageComponent.TakeItem),
                [typeof(int), typeof(int), typeof(int).MakeByRefType()]);
            var codeMacher = new CodeMatcher(instructions)
                .MatchForward(false,
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt
                                       && i.operand.Equals(method)))
                .Repeat(matcher => matcher
                    .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(Utils), nameof(TryTakeItem))));
            return codeMacher.InstructionEnumeration();
        }
        catch (Exception ex) {
            LogError($"Error in TryTakeItem_Transpiler: {ex}");
            return instructions;
        }
    }

    /// <summary>
    /// 从临时玩家背包获取物品时，返回正确的可用物品总数
    /// </summary>
    private static int TryTakeItem(StorageComponent storage, int itemId, int count, out int inc) {
        inc = 0;
        count = (int)Math.Min(count, GetItemTotalCount(itemId) - testPackageUsedCounts[itemId]);
        testPackageUsedCounts[itemId] += count;
        return count;
    }

    #endregion

    #region BuildTool.tmpPackage

    /// <summary>
    /// 初始化临时背包已经消耗的物品数目
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(BuildTool), nameof(BuildTool._GameTick))]
    private static bool BuildTool_ClearTestPackageUsedCounts(BuildTool __instance) {
        if (!__instance.active) {
            return true;
        }
        Array.Clear(testPackageUsedCounts, 0, 12000);
        return true;
    }

    /// <summary>
    /// 初始化临时背包已经消耗的物品数目
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildTool_BlueprintPaste), nameof(BuildTool_BlueprintPaste.CalculateReformData))]
    [HarmonyPatch(typeof(BuildTool_BlueprintPaste), nameof(BuildTool_BlueprintPaste.DetermineReforms))]
    private static void BuildTool_BlueprintPaste_ClearTestPackageUsedCounts() {
        Array.Clear(testPackageUsedCounts, 0, 12000);
    }

    /// <summary>
    /// 初始化临时背包已经消耗的物品数目
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(BuildTool_Reform), nameof(BuildTool_Reform.GetNeedSandCountByInc))]
    private static bool BuildTool_Reform_ClearTestPackageUsedCounts(BuildTool_Reform __instance) {
        if (!__instance.active) {
            Array.Clear(testPackageUsedCounts, 0, 12000);
        }
        return true;
    }

    /// <summary>
    /// 从临时玩家背包获取物品时，返回正确的可用物品总数
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPriority(Priority.High)]
    [HarmonyPatch(typeof(BuildTool_Addon), nameof(BuildTool_Addon.CheckBuildConditions))]
    [HarmonyPatch(typeof(BuildTool_BlueprintPaste), nameof(BuildTool_BlueprintPaste.CalculateReformData))]
    [HarmonyPatch(typeof(BuildTool_Click), nameof(BuildTool_Click.CheckBuildConditions))]
    [HarmonyPatch(typeof(BuildTool_Inserter), nameof(BuildTool_Inserter.CheckBuildConditions))]
    [HarmonyPatch(typeof(BuildTool_Path), nameof(BuildTool_Path.CheckBuildConditions))]
    [HarmonyPatch(typeof(BuildTool_Reform), nameof(BuildTool_Reform.GetNeedSandCountByInc))]
    private static IEnumerable<CodeInstruction> TryTakeTailItems_Transpiler(IEnumerable<CodeInstruction> instructions) {
        try {
            // Replace BuildTool.tmpPackage.TakeTailItems(ref int itemId, ref int count, out int inc, bool useBan = false)
            var method = AccessTools.Method(typeof(StorageComponent), nameof(StorageComponent.TakeTailItems),
                [typeof(int).MakeByRefType(), typeof(int).MakeByRefType(), typeof(int).MakeByRefType(), typeof(bool)]);
            var codeMacher = new CodeMatcher(instructions)
                .MatchForward(false,
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt
                                       && i.operand.Equals(method)))
                .Repeat(matcher => matcher
                    .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(Utils), nameof(TryTakeTailItems))));
            return codeMacher.InstructionEnumeration();
        }
        catch (Exception ex) {
            LogError($"Error in TryTakeTailItems_Transpiler: {ex}");
            return instructions;
        }
    }

    /// <summary>
    /// 从临时玩家背包获取物品时，返回正确的可用物品总数
    /// </summary>
    private static void TryTakeTailItems(StorageComponent storage, ref int itemId, ref int count, out int inc,
        bool useBan = false) {
        inc = 0;
        count = (int)Math.Min(count, GetItemTotalCount(itemId) - testPackageUsedCounts[itemId]);
        testPackageUsedCounts[itemId] += count;
        if (count == 0) {
            itemId = 0;
        }
    }

    /// <summary>
    /// 从临时玩家背包获取物品时，返回正确的可用物品总数
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPriority(Priority.High)]
    [HarmonyPatch(typeof(BuildTool_BlueprintPaste), nameof(BuildTool_BlueprintPaste.DetermineReforms))]
    private static IEnumerable<CodeInstruction> TakeTailItemsWithIncTable_Transpiler(
        IEnumerable<CodeInstruction> instructions) {
        try {
            // Replace BuildTool.tmpPackage.TakeTailItemsWithIncTable(int itemId, ref int count, out int inc, ref int[] incTable, bool useBan = false)
            var method = AccessTools.Method(typeof(StorageComponent),
                nameof(StorageComponent.TakeTailItemsWithIncTable),
                [
                    typeof(int), typeof(int).MakeByRefType(), typeof(int).MakeByRefType(),
                    typeof(int[]).MakeByRefType(), typeof(bool)
                ]);
            var codeMacher = new CodeMatcher(instructions)
                .MatchForward(false,
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt
                                       && i.operand.Equals(method)))
                .Repeat(matcher => matcher
                    .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(Utils), nameof(TakeTailItemsWithIncTable))));
            return codeMacher.InstructionEnumeration();
        }
        catch (Exception ex) {
            LogError($"Error in TakeTailItemsWithIncTable_Transpiler: {ex}");
            return instructions;
        }
    }

    /// <summary>
    /// 从临时玩家背包获取物品时，返回正确的可用物品总数
    /// </summary>
    private static void TakeTailItemsWithIncTable(StorageComponent storage, int itemId, ref int count, out int inc,
        ref int[] incTable, bool useBan = false) {
        inc = 0;
        if (incTable == null || incTable.Length <= 10) {
            incTable = new int[11];
        } else {
            Array.Clear(incTable, 0, incTable.Length);
        }
        count = (int)Math.Min(count, GetItemTotalCount(itemId) - testPackageUsedCounts[itemId]);
        if (count <= 0) {
            return;
        }
        testPackageUsedCounts[itemId] += count;
        long n = GetItemTotalCountAndInc(itemId, out long m);
        long takeInc = split_inc(ref n, ref m, count);
        int incLow = (int)(takeInc / count);
        int incHighCount = (int)(takeInc - count * incLow);
        int incLowCount = count - incHighCount;
        if (incLow > 10) {
            incLow = 10;
        }
        incTable[incLow] += incLowCount;
        inc += incLow * incLowCount;
        if (incLow + 1 > 10) {
            incTable[10] += incHighCount;
            inc += 10 * incHighCount;
        } else {
            incTable[incLow + 1] += incHighCount;
            inc += (incLow + 1) * incHighCount;
        }
        if (count > 0) {
            storage.lastFullItem = -1;
            storage.NotifyStorageChange();
        }
    }

    #endregion

    /// <summary>
    /// 从ModData背包取出指定物品。
    /// 如果数目不足，则取出全部物品；否则取出指定数目的物品。
    /// 注意，通过此方法取出的物品数目应该远小于int.MaxValue，以避免增产点数超过int。
    /// </summary>
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
    /// 拿取指定物品。
    /// 如果数目不足，则不拿取，弹窗提示失败；否则仅拿取，不弹窗。
    /// </summary>
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
        if (ArchitectMode && takeProto.BuildMode != 0) {
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
        TakeTailItems(GameMain.mainPlayer.package, ref itemId, ref count, out inc);
        return true;
    }

    /// <summary>
    /// 原版分割增产点数的方法。
    /// 使用前需注意物品总数n不能为0。
    /// </summary>
    /// <param name="n">物品总数，不能为0</param>
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

    /// <summary>
    /// 原版分割增产点数的方法。
    /// 使用前需注意物品总数n不能为0。
    /// </summary>
    /// <param name="n">物品总数，不能为0</param>
    /// <param name="m">物品总增产点数</param>
    /// <param name="p">要取走的物品数目</param>
    /// <returns>被取走的物品增产点数</returns>
    public static long split_inc(ref long n, ref long m, long p) {
        long num1 = m / n;
        long num2 = m - num1 * n;
        n -= p;
        long num3 = num2 - n;
        long num4 = num3 > 0 ? num1 * p + num3 : num1 * p;
        m -= num4;
        return num4;
    }

    #endregion

    #region 背包排序

    private static DateTime lastSortTime = DateTime.MinValue;

    /// <summary>
    /// 单击玩家背包排序按钮时，背包内的物品会尽可能转移到物流背包；
    /// 双击玩家背包排序按钮时，背包物品会全部转移到Mod背包。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(StorageComponent), nameof(StorageComponent.Sort))]
    private static bool StorageComponent_Sort_Prefix(StorageComponent __instance) {
        if (__instance != GameMain.mainPlayer?.package) {
            return true;
        }
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return true;
        }
        bool isDoubleClick = (DateTime.Now - lastSortTime).TotalMilliseconds < 400 && TechItemInteractionUnlocked;
        lastSortTime = DateTime.Now;
        if (!isDoubleClick) {
            //一次排序
            if (!GameMain.mainPlayer.deliveryPackage.unlocked) {
                return true;
            }
            StorageComponent package = GameMain.mainPlayer.package;
            DeliveryPackage deliveryPackage = GameMain.mainPlayer.deliveryPackage;
            for (int gridIndex = 99; gridIndex >= 0; gridIndex--) {
                int itemId = deliveryPackage.grids[gridIndex].itemId;
                for (int index = 0; index < package.size; index++) {
                    if (package.grids[index].itemId == itemId) {
                        int count = deliveryPackage.AddItem(itemId,
                            package.grids[index].count, package.grids[index].inc, out int remainInc);
                        package.grids[index].count -= count;
                        package.grids[index].inc = remainInc;
                    }
                }
            }
        } else {
            //二次排序
            if (!TechItemInteractionUnlocked || !Miscellaneous.EnablePackageSortTwice) {
                return true;
            }
            StorageComponent package = GameMain.mainPlayer.package;
            for (int index = 0; index < package.size; index++) {
                // 忽略过滤格
                if (package.grids[index].filter != 0) {
                    continue;
                }
                if (itemValue[package.grids[index].itemId] >= maxValue) {
                    continue;
                }
                AddItemToModData(package.grids[index].itemId, package.grids[index].count, package.grids[index].inc,
                    true);
                package.grids[index].itemId = 0;
                package.grids[index].count = 0;
                package.grids[index].inc = 0;
            }
        }
        return true;
    }

    #endregion

    #region 物流交互站自动喷涂

    private static readonly int[] proliferatorIDs = { I增产剂MkI, I增产剂MkII, I增产剂MkIII };
    private static readonly int[] baseUseCounts = { 12, 30, 60 };
    private static readonly int[] basePoints = { 1, 2, 4 };

    // 存储：[增产剂等级0-2, 自身携带点数0-10] = 该增产剂总共能提供的喷涂点数
    private static readonly int[,] totalPointsLookup = new int[3, 11];

    private static int _authInitializer = InitLookup();

    private static int InitLookup() {
        for (int i = 0; i < 3; i++) {
            for (int j = 0; j <= 10; j++) {
                // 额外次数 = 基础次数 * 增加比例 (来自 Cargo.incTableMilli)
                double bonusPercent = Cargo.incTableMilli[j];
                int extraUses = (int)(baseUseCounts[i] * bonusPercent + 1e-6);// 加微小值防止浮点误差
                int totalUses = baseUseCounts[i] + extraUses;

                // 如果是自喷涂，消耗1次掉，剩下 totalUses - 1 次服务于其他物品
                // 注意：如果 j=0 说明没喷涂，则不减去自消耗
                int effectiveUses = (j > 0) ? (totalUses - 1) : totalUses;
                totalPointsLookup[i, j] = effectiveUses * basePoints[i];
            }
        }
        return 1;
    }


    // /// <summary>
    // /// 使用分馏数据中心的增产剂喷涂物品
    // /// </summary>
    // public static void AddIncToItem(int itemCount, ref int itemInc) {
    //     // 从 MkIII 到 MkI 尝试喷涂
    //     for (int i = 2; i >= 0; i--) {
    //         int plrId = proliferatorIDs[i];
    //         int targetPoints = itemCount * basePoints[i];
    //
    //         if (itemInc >= targetPoints) continue;
    //
    //         while (itemInc < targetPoints) {
    //             // 1. 优先消耗 leftInc 缓存
    //             if (leftInc[i] > 0) {
    //                 int need = targetPoints - itemInc;
    //                 int take = Math.Min(need, leftInc[i]);
    //                 itemInc += take;
    //                 leftInc[i] -= take;
    //                 if (itemInc >= targetPoints) return;
    //             }
    //
    //             // 2. 缓存不足，提取一个增产剂
    //             int actualTake = TakeItemFromModData(plrId, 1, out int selfInc);
    //             if (actualTake > 0) {
    //                 // 如果拿出来的增产剂点数不足 4 (MkIII的最高点数)，尝试将其补到 4 点
    //                 if (selfInc < 4) {
    //                     int selfNeed = 4 - selfInc;
    //                     // 我们尝试用同级别的 leftInc 缓存或者递归调用来补齐这 4 点
    //                     // 简化逻辑：直接从当前等级的缓存中扣除（因为我们优先用最高级增产）
    //                     if (leftInc[i] >= selfNeed) {
    //                         leftInc[i] -= selfNeed;
    //                         selfInc = 4;
    //                     } else {
    //                         // 如果缓存连补自喷涂都不够，那就按原始点数算，不强求补满
    //                     }
    //                 }
    //                 // ---------------------------
    //
    //                 // 使用预计算表 totalPointsLookup 获取该增产剂能提供的总点数
    //                 // 此时 selfInc 已经尽可能被补正了
    //                 leftInc[i] += totalPointsLookup[i, Math.Min(10, selfInc)];
    //             } else {
    //                 break; // 没药剂了
    //             }
    //         }
    //     }
    // }


    private const int REFILL_THRESHOLD = 1000;// 低于这个值就触发补给
    private const int TARGET_CAPACITY = 40000;// 目标蓄水量

    /// <summary>
    /// 使用分馏数据中心的增产剂喷涂物品
    /// </summary>
    public static void AddIncToItem(int itemCount, ref int itemInc) {
        for (int i = 2; i >= 0; i--) {
            int targetPoints = itemCount * basePoints[i];
            if (itemInc >= targetPoints) continue;

            int need = targetPoints - itemInc;

            // 1. 尝试直接从蓄水池扣除
            if (leftInc[i] < need) {
                // 2. 蓄水池不足，触发一次批量补给
                RefillInc(i);
            }

            // 3. 再次检查并扣除（如果补给后还是不够，能扣多少扣多少）
            int take = Math.Min(need, leftInc[i]);
            itemInc += take;
            leftInc[i] -= take;

            // 如果已经补到了当前最高级别，直接返回
            if (itemInc >= targetPoints) return;
        }
    }

    private static void RefillInc(int index) {
        int plrId = proliferatorIDs[index];
        if (centerItemCount[plrId] <= 0) return;

        // 计算当前缺口
        int gap = TARGET_CAPACITY - leftInc[index];
        if (gap <= 0) return;

        // 预估需要多少个增产剂（按最差情况：无增产状态计算）
        int singleFullPoints = baseUseCounts[index] * basePoints[index];
        int countToTake = (gap + singleFullPoints - 1) / singleFullPoints;

        // 批量从数据中心提取
        int actualTake = TakeItemFromModData(plrId, countToTake, out int totalSelfInc);
        if (actualTake <= 0) return;

        // 计算这批增产剂的平均增产等级
        int avgInc = totalSelfInc / actualTake;
        avgInc = Math.Min(10, avgInc);

        // --- 自喷涂自动升级逻辑 ---
        // 如果这批增产剂没达到4级，且池子里还有点水，尝试花点小钱给它们全部“升级”
        if (avgInc < 4 && leftInc[index] > actualTake * (4 - avgInc)) {
            leftInc[index] -= actualTake * (4 - avgInc);
            avgInc = 4;
        }

        // 查表转换成总点数注入蓄水池
        // 注意：totalPointsLookup[index, avgInc] 内部已经处理了 (次数-1)
        leftInc[index] += actualTake * totalPointsLookup[index, avgInc];
    }

    #endregion
}
