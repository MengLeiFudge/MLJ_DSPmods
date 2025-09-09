using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BuildBarTool;
using CommonAPI.Patches;
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

    /// <summary>
    /// 建筑师模式，所有建筑数目显示为999且建造时不消耗
    /// </summary>
    private static bool ArchitectMode => Multfunction_mod.ArchitectMode
                                         || CheatEnabler.ArchitectMode
                                         || DeliverySlotsTweaks.ArchitectMode;

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
        AddItemToModData(itemId, count, inc);
        return false;
    }

    /// <summary>
    /// 扔掉的垃圾会自动回收到Mod背包
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.ThrowHandItems))]
    private static bool Player_ThrowHandItems_Prefix(Player __instance) {
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
    [HarmonyPriority(Priority.Low)]
    [HarmonyPatch(typeof(BuildTool_Reform), nameof(BuildTool_Reform.ReformAction))]
    [HarmonyPatch(typeof(BuildTool_Reform), nameof(BuildTool_Reform.RemoveBasePit))]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.EntityAutoReplenishIfNeeded))]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.StationAutoReplenishIfNeeded))]
    [HarmonyPatch(typeof(PlayerAction_Inspect), nameof(PlayerAction_Inspect.GameTick))]
    [HarmonyPatch(typeof(UIBlueprintInspector), nameof(UIBlueprintInspector.OnPlayerPackageChange))]
    [HarmonyPatch(typeof(UIBlueprintInspector), nameof(UIBlueprintInspector.SetComponentItem))]
    [HarmonyPatch(typeof(UIBuildMenu), nameof(UIBuildMenu.OnChildButtonClick))]
    [HarmonyPatch(typeof(UIBuildMenu), nameof(UIBuildMenu.SetCurrentCategory))]
    [HarmonyPatch(typeof(UIBuildMenu), nameof(UIBuildMenu._OnUpdate))]
    [HarmonyPatch(typeof(UIControlPanelObjectEntry), nameof(UIControlPanelObjectEntry.ReplenishItems))]
    [HarmonyPatch(typeof(UIHandTip), nameof(UIHandTip._OnUpdate))]
    [HarmonyPatch(typeof(UIRemoveBasePitButton), nameof(UIRemoveBasePitButton._OnUpdate))]
    [HarmonyPatch(typeof(UISandboxMenu), nameof(UISandboxMenu.OnChildButtonClick))]
    [HarmonyPatch(typeof(UITurretWindow), nameof(UITurretWindow.OnHandFillAmmoButtonClick))]
    private static IEnumerable<CodeInstruction> GetItemCount_Transpiler(IEnumerable<CodeInstruction> instructions) {
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
    [HarmonyPriority(Priority.Low)]
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
    [HarmonyPriority(Priority.Low)]
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
    [HarmonyPriority(Priority.Low)]
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
    /// 移除“物品不足”的提示
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPriority(Priority.Low)]
    [HarmonyAfter("dsp.nebula-multiplayer")]
    [HarmonyPatch(typeof(BuildTool_Click), nameof(BuildTool_Click.CheckBuildConditions))]
    [HarmonyPatch(typeof(BuildTool_Inserter), nameof(BuildTool_Inserter.CheckBuildConditions))]
    [HarmonyPatch(typeof(BuildTool_Addon), nameof(BuildTool_Addon.CheckBuildConditions))]
    [HarmonyPatch(typeof(BuildTool_Path), nameof(BuildTool_Path.CheckBuildConditions))]
    public static IEnumerable<CodeInstruction> CheckBuildConditions_Transpiler(
        IEnumerable<CodeInstruction> instructions) {
        try {
            var codeMatcher = new CodeMatcher(instructions)
                .MatchForward(false,
                    new CodeMatch(OpCodes.Ldc_I4_2),// EBuildCondition.NotEnoughItem
                    new CodeMatch(OpCodes.Stfld,
                        AccessTools.Field(typeof(BuildPreview), nameof(BuildPreview.condition)))
                );
            if (codeMatcher.IsInvalid) {
                LogWarning("Can't find EBuildCondition.NotEnoughItem");
                return instructions;
            }
            codeMatcher
                .Advance(-1)
                .SetAndAdvance(OpCodes.Nop, null)
                .SetAndAdvance(OpCodes.Nop, null)
                .SetAndAdvance(OpCodes.Nop, null);
            if (codeMatcher.Opcode == OpCodes.Br)
                codeMatcher.RemoveInstruction();
            return codeMatcher.InstructionEnumeration();
        }
        catch (Exception ex) {
            LogError($"Error in CheckBuildConditions_Transpiler: {ex}");
            return instructions;
        }
    }

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
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null || !GameMain.mainPlayer.deliveryPackage.unlocked) {
            return 0;
        }
        DeliveryPackage deliveryPackage = GameMain.mainPlayer.deliveryPackage;
        int count = 0;
        for (int gridIndex = 99; gridIndex >= 0; gridIndex--) {
            if (deliveryPackage.grids[gridIndex].itemId == itemId) {
                count += deliveryPackage.grids[gridIndex].count;
                break;
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

    //StorageComponent取出物品方法一览：
    //public int TakeItem(int itemId, int count, out int inc)：player.package多处调用此方法
    //public void TakeItemFromGrid(int gridIndex, ref int itemId, ref int count, out int inc)：player.package多处调用此方法
    //public void TakeHeadItems(ref int itemId, ref int count, out int inc)：player.package不调用此方法
    //public void TakeTailItems(ref int itemId, ref int count, out int inc, bool useBan = false)：player.package多处调用此方法
    //public bool TakeTailItems(ref int itemId, ref int count, int[] needs, out int inc, bool useBan = false)：player.package不调用此方法
    //public TakeTailItemsFiltered(ref int filter, ref int count, out int inc, bool useBan = false)：player.package不调用此方法

    /// <summary>
    /// 从玩家背包获取物品时，可以从 背包/物流背包/Mod背包 中获取
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPriority(Priority.Low)]
    [HarmonyPatch(typeof(Mecha), nameof(Mecha.AutoReplenishAmmo))]
    [HarmonyPatch(typeof(Mecha), nameof(Mecha.AutoReplenishBomb))]
    [HarmonyPatch(typeof(Mecha), nameof(Mecha.AutoReplenishFuel))]
    [HarmonyPatch(typeof(Mecha), nameof(Mecha.AutoReplenishFuelAll))]
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
    /// 尝试从临时玩家背包获取物品时，可以从 背包/物流背包/Mod背包 中获取
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPriority(Priority.Low)]
    [HarmonyPatch(typeof(MechaForge), nameof(MechaForge.TryAddTaskIterate))]
    private static IEnumerable<CodeInstruction> TryTakeItem_Transpiler(IEnumerable<CodeInstruction> instructions) {
        try {
            // Replace player.package.TakeItem(int itemId, int count, out int inc)
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
            LogError($"Error in TakeItem_Transpiler: {ex}");
            return instructions;
        }
    }

    private static int TryTakeItem(StorageComponent storage, int itemId, int count, out int inc) {
        return (int)GetModDataItemCount(itemId) + storage.TakeItem(itemId, count, out inc) +
               GetDeliveryPackageItemCount(itemId);
    }

    /// <summary>
    /// 从玩家背包获取物品时，可以从 背包/物流背包/Mod背包 中获取
    /// </summary>
    private static int TakeItem(StorageComponent storage, int itemId, int count, out int inc) {
        //如果不是玩家背包，直接调用原始方法并返回
        if (storage != GameMain.mainPlayer.package) {
            return storage.TakeItem(itemId, count, out inc);
        }
        //如果是玩家背包，按照 背包-物流背包-Mod背包 的顺序取走物品
        //如果是建筑师模式并且为建筑，不需要消耗物品
        inc = 0;
        if (itemId <= 0) {
            return 0;
        }
        ItemProto item = LDB.items.Select(itemId);
        if (item == null) {
            return 0;
        }
        if (ArchitectMode && item.BuildMode != 0) {
            return count;
        }
        //背包
        // count -= storage.TakeItem(itemId, count, out inc);

        int countReal = 0;
        int itemIdOri = itemId;
        int countNeed = count;
        //执行TakeItem后，count表示实际取到的数目
        count = storage.TakeItem(itemId, count, out inc);
        countReal += count;
        if (countReal >= countNeed) {
            return countReal;
        }
        //count改为还需要获取的物品数目
        count = countNeed - countReal;
        //物流背包
        //执行TakeItems后，count表示实际取到的数目
        GameMain.mainPlayer.deliveryPackage.TakeItems(ref itemId, ref count, out int incDP);
        inc += incDP;
        countReal += count;
        if (countReal >= countNeed) {
            return countReal;
        }
        //itemId还原，count改为还需要获取的物品数目
        itemId = itemIdOri;
        count = countNeed - countReal;
        //Mod背包
        //执行TakeItemFromModData后，count表示实际取到的数目
        count = TakeItemFromModData(itemId, count, out int incMP);
        inc += incMP;
        countReal += count;
        //返回实际取到的数目
        return countReal;
    }

    // /// <summary>
    // /// 从玩家背包获取物品时，可以从 背包/物流背包/Mod背包 中获取
    // /// </summary>
    // [HarmonyTranspiler]
    // [HarmonyPriority(Priority.Low)]
    // [HarmonyPatch(typeof(Mecha), nameof(PlanetFactory.EntityFastFillIn))]
    // [HarmonyPatch(typeof(Mecha), nameof(Player.SetHandItems))]
    // [HarmonyPatch(typeof(Mecha), nameof(PlayerPackageUtility.ThrowAllItemsInAllPackage))]
    // [HarmonyPatch(typeof(Mecha), nameof(StorageComponent.TransferTo),
    //     [typeof(StorageComponent)], [ArgumentType.Normal])]
    // [HarmonyPatch(typeof(Mecha), nameof(StorageComponent.TransferTo),
    //     [typeof(StorageComponent), typeof(ItemBundle)], [ArgumentType.Normal, ArgumentType.Normal])]
    // private static IEnumerable<CodeInstruction> TakeItemFromGrid_Transpiler(IEnumerable<CodeInstruction> instructions) {
    //     try {
    //         // Replace player.package.TakeItemFromGrid(int gridIndex, ref int itemId, ref int count, out int inc)
    //         var method = AccessTools.Method(typeof(StorageComponent), "TakeItemFromGrid",
    //             [typeof(int), typeof(int).MakeByRefType(), typeof(int).MakeByRefType(), typeof(int).MakeByRefType()]);
    //         var codeMatcher = new CodeMatcher(instructions)
    //             .MatchForward(false,
    //                 new CodeMatch(i => i.opcode == OpCodes.Callvirt
    //                                    && i.operand.Equals(method)))
    //             .Repeat(matcher => matcher
    //                 .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(Utils), nameof(TakeItemFromGrid))));
    //         return codeMatcher.InstructionEnumeration();
    //     }
    //     catch (Exception ex) {
    //         LogError($"Error in TakeItemFromGrid_Transpiler: {ex}");
    //         return instructions;
    //     }
    // }
    //
    // /// <summary>
    // /// 从玩家背包获取物品时，可以从 背包/物流背包/Mod背包 中获取
    // /// </summary>
    // private static int TakeItemFromGrid(StorageComponent storage, int gridIndex, ref int itemId, ref int count,
    //     out int inc) {
    //
    // }

    /// <summary>
    /// 从玩家背包获取物品时，可以从 背包/物流背包/Mod背包 中获取
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPriority(Priority.Low)]
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
    private static IEnumerable<CodeInstruction> TakeTailItems_Transpiler(IEnumerable<CodeInstruction> instructions) {
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
        //如果不是玩家背包，直接调用原始方法并返回
        if (storage != GameMain.mainPlayer.package) {
            storage.TakeTailItems(ref itemId, ref count, out inc, useBan);
            return;
        }
        //如果是玩家背包，按照 背包-Mod背包-物流背包 的顺序取走物品
        //如果是建筑师模式并且为建筑，不需要消耗物品
        inc = 0;
        if (itemId <= 0) {
            itemId = 0;
            count = 0;
            return;
        }
        ItemProto item = LDB.items.Select(itemId);
        if (item == null) {
            itemId = 0;
            count = 0;
            return;
        }
        if (ArchitectMode && item.BuildMode != 0) {
            return;
        }
        //背包
        int countReal = 0;
        int itemIdOri = itemId;
        int countNeed = count;
        //执行TakeTailItems后，count表示实际取到的数目
        storage.TakeTailItems(ref itemId, ref count, out inc, useBan);
        countReal += count;
        if (countReal >= countNeed) {
            return;
        }
        //itemId还原，count改为还需要获取的物品数目
        itemId = itemIdOri;
        count = countNeed - countReal;
        //物流背包
        //执行TakeItems后，count表示实际取到的数目
        GameMain.mainPlayer.deliveryPackage.TakeItems(ref itemId, ref count, out int incDP);
        inc += incDP;
        countReal += count;
        if (countReal >= countNeed) {
            return;
        }
        //itemId还原，count改为还需要获取的物品数目
        itemId = itemIdOri;
        count = countNeed - countReal;
        //Mod背包
        //执行TakeItemFromModData后，count表示实际取到的数目
        count = TakeItemFromModData(itemId, count, out int incMP);
        inc += incMP;
        countReal += count;
        if (countReal >= countNeed) {
            return;
        }
        //物品不够，判断获取的物品数目是否为0，如果为0则itemId置为0
        if (countReal > 0) {
            itemId = itemIdOri;
            count = countReal;
        } else {
            itemId = 0;
            count = 0;
        }
    }

    /// <summary>
    /// 从ModData背包取出指定物品。
    /// 如果数目不足，则取出全部物品；否则取出指定数目的物品。
    /// 注意，通过此方法取出的物品数目应该远小于int.MaxValue，以避免增产点数超过int。
    /// </summary>
    /// <returns>实际拿到的数目</returns>
    public static int TakeItemFromModData(int itemId, int count, out int inc) {
        lock (centerItemCount) {
            count = (int)Math.Min(count, centerItemCount[itemId]);
            count = Math.Min(int.MaxValue / 10, count);
            if (count <= 0) {
                inc = 0;
                return 0;
            }
            inc = (int)split_inc(ref centerItemCount[itemId], ref centerItemInc[itemId], count);
            return count;
        }
    }

    /// <summary>
    /// 拿取指定物品。
    /// 如果数目不足，则不拿取，弹窗提示失败；否则仅拿取，不弹窗。
    /// </summary>
    /// <returns>是否拿取成功</returns>
    public static bool TakeItemWithTip(int itemId, int count, out int inc, bool showMessage = true) {
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

    /// <summary>
    /// 原版分割增产点数的方法。
    /// </summary>
    /// <param name="n">物品总数</param>
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

    /// <summary>
    /// 排序时背包内的物品会尽可能转移到物流背包。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(StorageComponent), nameof(StorageComponent.Sort))]
    private static bool StorageComponent_Sort_Prefix(StorageComponent __instance) {
        if (__instance != GameMain.mainPlayer?.package) {
            return true;
        }
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null || !GameMain.mainPlayer.deliveryPackage.unlocked) {
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
