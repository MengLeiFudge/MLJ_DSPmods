using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Logic.DataCenter.Patches;

/// <summary>
/// 玩家取物调用重定向到统一库存访问的补丁。
/// </summary>
public static class PlayerInventoryItemAccessPatches {
    // StorageComponent 取出物品方法一览（用于决定本文件和 TemporaryPackageAccessPatch 分别 patch 哪些方法）：
    // [背包][测试背包]int TakeItem(int itemId, int count, out int inc)
    // [不关联]void TakeItemFromGrid(int gridIndex, ref int itemId, ref int count, out int inc)
    // [不关联]void TakeHeadItems(ref int itemId, ref int count, out int inc)
    // [背包][测试背包]void TakeTailItems(ref int itemId, ref int count, out int inc, bool useBan = false)
    // [不关联]bool TakeTailItems(ref int itemId, ref int count, int[] needs, out int inc, bool useBan = false)
    // [不关联]void TakeTailItemsFiltered(ref int filter, ref int count, out int inc, bool useBan = false)
    // [测试背包]void TakeTailItemsWithIncTable(int itemId, ref int count, out int inc, ref int[] incTable, bool useBan = false)
    // [背包]bool TakeTailItemsByIncTable(int itemId, out int count, ref int[] incTable, bool useBan = false)

    /// <summary>
    /// 从玩家背包获取物品时，可以从 背包/物流背包/Mod背包 中获取
    /// </summary>
    [HarmonyPatch]
    private static class TakeItemPatch {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods() {
            yield return AccessTools.Method(typeof(Mecha), nameof(Mecha.AutoReplenishAmmo));
            yield return AccessTools.Method(typeof(Mecha), nameof(Mecha.AutoReplenishBomb));
            yield return AccessTools.Method(typeof(Mecha), nameof(Mecha.AutoReplenishFuel));
            yield return AccessTools.Method(typeof(Mecha), nameof(Mecha.AutoReplenishFuelAll));
            yield return AccessTools.Method(typeof(Mecha), nameof(Mecha.AutoReplenishWarper));
            yield return AccessTools.Method(typeof(MechaForge), nameof(MechaForge.AddTaskIterate));
        }

        [HarmonyTranspiler]
        [HarmonyPriority(Priority.High)]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            TakeItem_Transpiler(instructions);
    }

    private static IEnumerable<CodeInstruction> TakeItem_Transpiler(IEnumerable<CodeInstruction> instructions) {
        try {
            // 替换 player.package.TakeItem(int itemId, int count, out int inc) 调用
            var method = AccessTools.Method(typeof(StorageComponent), nameof(StorageComponent.TakeItem),
                [typeof(int), typeof(int), typeof(int).MakeByRefType()]);
            var codeMatcher = new CodeMatcher(instructions)
                .MatchForward(false,
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt
                                       && i.operand.Equals(method)))
                .Repeat(matcher => matcher
                    .SetAndAdvance(OpCodes.Call,
                        AccessTools.Method(typeof(PlayerInventoryItemAccessPatches), nameof(TakeItem))));
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
    [HarmonyPatch]
    private static class TakeTailItemsPatch {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods() {
            yield return AccessTools.Method(typeof(BuildTool_Addon), nameof(BuildTool_Addon.CreatePrebuilds));
            yield return AccessTools.Method(typeof(BuildTool_BlueprintPaste),
                nameof(BuildTool_BlueprintPaste.CreatePrebuilds));
            yield return AccessTools.Method(typeof(BuildTool_BlueprintPaste),
                nameof(BuildTool_BlueprintPaste.DetermineReforms));
            yield return AccessTools.Method(typeof(BuildTool_Click), nameof(BuildTool_Click.CreatePrebuilds));
            yield return AccessTools.Method(typeof(BuildTool_Inserter), nameof(BuildTool_Inserter.CreatePrebuilds));
            yield return AccessTools.Method(typeof(BuildTool_Path), nameof(BuildTool_Path.CreatePrebuilds));
            yield return AccessTools.Method(typeof(BuildTool_Reform),
                nameof(BuildTool_Reform.ConsumeItemsAndRegisterStats));
            yield return AccessTools.Method(typeof(BuildTool_Reform), nameof(BuildTool_Reform.RemoveBasePit));
            yield return AccessTools.Method(typeof(ConstructionModuleComponent),
                nameof(ConstructionModuleComponent.PlaceItems));
            yield return AccessTools.Method(typeof(MechaLab), nameof(MechaLab.ManageSupply));
            yield return AccessTools.Method(typeof(PlanetFactory),
                nameof(PlanetFactory.EntityAutoReplenishIfNeeded));
            yield return AccessTools.Method(typeof(PlanetFactory),
                nameof(PlanetFactory.StationAutoReplenishIfNeeded));
            yield return AccessTools.Method(typeof(Player), nameof(Player.TakeItemFromPlayer));
            yield return AccessTools.Method(typeof(PlayerAction_Build), nameof(PlayerAction_Build.DoUpgradeObject));
            yield return AccessTools.Method(typeof(PlayerAction_Inspect), nameof(PlayerAction_Inspect.GameTick));
            yield return AccessTools.Method(typeof(PlayerPackageUtility),
                nameof(PlayerPackageUtility.TakeItemFromAllPackages));
            yield return AccessTools.Method(typeof(PlayerPackageUtility),
                nameof(PlayerPackageUtility.TryTakeItemFromAllPackages));
            yield return AccessTools.Method(typeof(UIControlPanelObjectEntry),
                nameof(UIControlPanelObjectEntry.ReplenishItems));
            yield return AccessTools.Method(typeof(UITurretWindow),
                nameof(UITurretWindow.OnHandFillAmmoButtonClick));
        }

        [HarmonyTranspiler]
        [HarmonyPriority(Priority.High)]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            TakeTailItems_Transpiler(instructions);
    }

    public static IEnumerable<CodeInstruction> TakeTailItems_Transpiler(IEnumerable<CodeInstruction> instructions) {
        try {
            // 替换 player.package.TakeTailItems(ref int itemId, ref int count, out int inc, bool useBan = false) 调用
            var method = AccessTools.Method(typeof(StorageComponent), nameof(StorageComponent.TakeTailItems),
                [typeof(int).MakeByRefType(), typeof(int).MakeByRefType(), typeof(int).MakeByRefType(), typeof(bool)]);
            var codeMatcher = new CodeMatcher(instructions)
                .MatchForward(false,
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt
                                       && i.operand.Equals(method)))
                .Repeat(matcher => matcher
                    .SetAndAdvance(OpCodes.Call,
                        AccessTools.Method(typeof(PlayerInventoryItemAccessPatches), nameof(TakeTailItems))));
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
            // 替换 player.package.TakeTailItemsByIncTable(int itemId, out int count, ref int[] incTable, bool useBan = false) 调用
            var method = AccessTools.Method(typeof(StorageComponent), nameof(StorageComponent.TakeTailItemsByIncTable),
                [typeof(int), typeof(int).MakeByRefType(), typeof(int[]).MakeByRefType(), typeof(bool)]);
            var codeMatcher = new CodeMatcher(instructions)
                .MatchForward(false,
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt
                                       && i.operand.Equals(method)))
                .Repeat(matcher => matcher
                    .SetAndAdvance(OpCodes.Call,
                        AccessTools.Method(typeof(PlayerInventoryItemAccessPatches), nameof(TakeTailItemsByIncTable))));
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
                realCount -= incTable[i];
                incTable[i] = 0;
            } else {
                incTable[i] -= realCount;
                return false;
            }
        }
        return true;
    }
}
