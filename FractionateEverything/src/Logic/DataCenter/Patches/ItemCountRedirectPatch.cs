using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BuildBarTool;
using CommonAPI.Patches;
using FE.Compatibility.Mods;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Logic.DataCenter.Patches;
/// <summary>
/// 玩家物品数量查询重定向到统一库存访问的补丁。
/// </summary>
public static class ItemCountRedirectPatch {
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
                    .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(ItemCountRedirectPatch), nameof(GetItemCount))));
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
        if (PackageAccessRules.ArchitectMode && item.BuildMode != 0) {
            return 999;
        }
        return (int)Math.Min(int.MaxValue, PlayerInventoryAccess.GetItemTotalCount(itemId));
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
                    .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(ItemCountRedirectPatch), nameof(GetItemCount))));
            // Replace history.ItemUnlocked(int itemId)
            method = AccessTools.Method(typeof(GameHistoryData), nameof(GameHistoryData.ItemUnlocked),
                [typeof(int)]);
            codeMatcher = new CodeMatcher(codeMatcher.InstructionEnumeration())
                .MatchForward(false,
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt
                                       && i.operand.Equals(method)))
                .Repeat(matcher => matcher
                    .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(ItemCountRedirectPatch), nameof(ItemUnlocked))));
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
                    .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(ItemCountRedirectPatch), nameof(ItemUnlocked))));
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
        return history.ItemUnlocked(itemId) || PlayerInventoryAccess.GetItemTotalCount(itemId) > 0;
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
                        AccessTools.Method(typeof(ItemCountRedirectPatch), nameof(AddConstructableCountsInStorage)))
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

}
