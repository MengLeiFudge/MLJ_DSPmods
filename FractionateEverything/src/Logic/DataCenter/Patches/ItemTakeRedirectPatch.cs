using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Logic.DataCenter.Patches;
/// <summary>
/// 玩家取物调用重定向到统一库存访问的补丁。
/// </summary>
public static class PlayerInventoryItemAccessPatches {
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
                    .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(PlayerInventoryItemAccessPatches), nameof(TakeItem))));
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
        PlayerInventoryAccess.TakeItemInternal(storage, itemId, count, out int realCount, out inc);
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
                    .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(PlayerInventoryItemAccessPatches), nameof(TakeTailItems))));
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
    public static void TakeTailItems(StorageComponent storage, ref int itemId, ref int count, out int inc,
        bool useBan = false) {
        if (storage != GameMain.mainPlayer.package) {
            storage.TakeTailItems(ref itemId, ref count, out inc, useBan);
            return;
        }
        PlayerInventoryAccess.TakeItemInternal(storage, itemId, count, out int realCount, out inc, useBan);
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
                    .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(PlayerInventoryItemAccessPatches), nameof(TakeTailItemsByIncTable))));
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
        PlayerInventoryAccess.TakeItemInternal(storage, itemId, count, out int realCount, out int inc, useBan);
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

}
