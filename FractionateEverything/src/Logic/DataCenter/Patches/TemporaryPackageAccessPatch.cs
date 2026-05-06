using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Logic.DataCenter.Patches;
/// <summary>
/// TemporaryPackageAccessPatch 类型。
/// </summary>
public static class TemporaryPackageAccessPatch {
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
                    .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(TemporaryPackageAccessPatch), nameof(TryTakeItem))));
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
        if (itemId <= 0 || itemId >= 12000) {
            return 0;
        }
        count = (int)Math.Min(count, PlayerInventoryAccess.GetItemTotalCount(itemId) - testPackageUsedCounts[itemId]);
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
                    .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(TemporaryPackageAccessPatch), nameof(TryTakeTailItems))));
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
        if (itemId <= 0 || itemId >= 12000) {
            count = 0;
            return;
        }
        count = (int)Math.Min(count, PlayerInventoryAccess.GetItemTotalCount(itemId) - testPackageUsedCounts[itemId]);
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
                    .SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(TemporaryPackageAccessPatch), nameof(TakeTailItemsWithIncTable))));
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
        if (itemId <= 0 || itemId >= 12000) {
            count = 0;
            return;
        }
        if (incTable == null || incTable.Length <= 10) {
            incTable = new int[11];
        } else {
            Array.Clear(incTable, 0, incTable.Length);
        }
        count = (int)Math.Min(count, PlayerInventoryAccess.GetItemTotalCount(itemId) - testPackageUsedCounts[itemId]);
        if (count <= 0) {
            return;
        }
        testPackageUsedCounts[itemId] += count;
        long n = PlayerInventoryAccess.GetItemTotalCountAndInc(itemId, out long m);
        long takeInc = PlayerInventoryAccess.split_inc(ref n, ref m, count);
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
}
