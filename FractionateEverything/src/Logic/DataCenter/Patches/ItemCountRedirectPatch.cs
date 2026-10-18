using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BuildBarTool;
using CommonAPI.Patches;
using FE.Compatibility.Mods;
using FE.Compatibility.Nebula;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Logic.DataCenter.Patches;

/// <summary>
/// 玩家物品数量查询重定向到统一库存访问的补丁。
/// </summary>
public static class ItemCountRedirectPatch {
    [ThreadStatic] private static bool requestingMaterials;
    //StorageComponent获取物品数目方法一览：
    //public int GetItemCount(int itemId)：player.package多处调用此方法
    //public int GetItemCount(int itemId, out int inc)：player.package不调用此方法

    /// <summary>
    /// 获取玩家持有的物品数目时，返回 背包+物流背包+Mod背包 的物品总数
    /// </summary>
    [HarmonyPatch]
    private static class GetItemCountPatch {
        // 仅实际补给/点击操作请求取物，浏览建造目录与物品提示不搬运库存。
        [HarmonyPrefix]
        private static void Begin(MethodBase __originalMethod, out bool __state) {
            __state = requestingMaterials;
            requestingMaterials |= __originalMethod.Name is "EntityAutoReplenishIfNeeded" or "StationAutoReplenishIfNeeded"
                or "ReplenishItems" or "OnHandFillAmmoButtonClick" or "OnChildButtonClick";
        }
        [HarmonyFinalizer]
        private static void End(bool __state) => requestingMaterials = __state;

        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods() {
            yield return AccessTools.Method(typeof(BuildTool_Reform), nameof(BuildTool_Reform.DoFlattenExecute));
            yield return AccessTools.Method(typeof(BuildTool_Reform), nameof(BuildTool_Reform.RemoveBasePit));
            yield return AccessTools.Method(typeof(PlanetFactory),
                nameof(PlanetFactory.EntityAutoReplenishIfNeeded));
            yield return AccessTools.Method(typeof(PlanetFactory),
                nameof(PlanetFactory.StationAutoReplenishIfNeeded));
            yield return AccessTools.Method(typeof(PlayerAction_Inspect), nameof(PlayerAction_Inspect.GameTick));
            yield return AccessTools.Method(typeof(UIBlueprintInspector),
                nameof(UIBlueprintInspector.SetComponentItem));
            yield return AccessTools.Method(typeof(UIBlueprintInspector), nameof(UIBlueprintInspector._OnUpdate));
            yield return AccessTools.Method(typeof(UIBuildMenu), nameof(UIBuildMenu.OnChildButtonClick));
            yield return AccessTools.Method(typeof(UIBuildMenu), nameof(UIBuildMenu.SetCurrentCategory));
            yield return AccessTools.Method(typeof(UIBuildMenu), nameof(UIBuildMenu._OnUpdate));
            yield return AccessTools.Method(typeof(UIControlPanelObjectEntry),
                nameof(UIControlPanelObjectEntry.ReplenishItems));
            yield return AccessTools.Method(typeof(UIHandTip), nameof(UIHandTip._OnUpdate));
            yield return AccessTools.Method(typeof(UIItemup), nameof(UIItemup.Up));
            yield return AccessTools.Method(typeof(UIRemoveBasePitButton),
                nameof(UIRemoveBasePitButton._OnUpdate));
            yield return AccessTools.Method(typeof(UIReplicatorWindow), nameof(UIReplicatorWindow._OnUpdate));
            yield return AccessTools.Method(typeof(UISandboxMenu), nameof(UISandboxMenu.OnChildButtonClick));
            yield return AccessTools.Method(typeof(UITurretWindow),
                nameof(UITurretWindow.OnHandFillAmmoButtonClick));
        }

        [HarmonyTranspiler]
        [HarmonyPriority(Priority.High)]
        [HarmonyAfter(BuildToolOpt.GUID)]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            GetItemCount_Transpiler(instructions);
    }

    public static IEnumerable<CodeInstruction> GetItemCount_Transpiler(IEnumerable<CodeInstruction> instructions) {
        try {
            // 替换 player.package.GetItemCount(int itemId) 调用
            var method = AccessTools.Method(typeof(StorageComponent), nameof(StorageComponent.GetItemCount),
                [typeof(int)]);
            var codeMatcher = new CodeMatcher(instructions)
                .MatchForward(false,
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt
                                       && i.operand.Equals(method)))
                .Repeat(matcher => matcher
                    .SetAndAdvance(OpCodes.Call,
                        AccessTools.Method(typeof(ItemCountRedirectPatch), nameof(GetItemCount))));
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
        if (requestingMaterials) { MaterialLeases.Ensure(itemId, Math.Max(1, item.StackSize)); }
        return (int)Math.Min(int.MaxValue, PlayerInventoryAccess.GetItemTotalCount(itemId));
    }

    /// <summary>
    /// 背包物品总数、快捷建造栏的相关修改也兼容BuildBarTool
    /// </summary>
    [HarmonyPatch]
    private static class BuildBarToolPatch {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods() {
            yield return AccessTools.Method(typeof(BuildBarToolPlugin),
                nameof(BuildBarToolPlugin.UIBuildMenuSetCurrentCategoryPostPatch));
            yield return AccessTools.Method(typeof(BuildBarToolPlugin),
                nameof(BuildBarToolPlugin.UIBuildMenuOnUpdatePostPatch),
                [typeof(UIBuildMenu).MakeByRefType()]);
            yield return AccessTools.Method(typeof(BuildBarToolPlugin),
                nameof(BuildBarToolPlugin.OnChildButtonClick));
        }

        [HarmonyTranspiler]
        [HarmonyPriority(Priority.High)]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions, MethodBase original) =>
            BuildBarTool_Transpiler(instructions, original);
    }

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
            // 替换 player.package.GetItemCount(int itemId) 调用
            var method = AccessTools.Method(typeof(StorageComponent), nameof(StorageComponent.GetItemCount),
                [typeof(int)]);
            codeMatcher = new CodeMatcher(codeMatcher.InstructionEnumeration())
                .MatchForward(false,
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt
                                       && i.operand.Equals(method)))
                .Repeat(matcher => matcher
                    .SetAndAdvance(OpCodes.Call,
                        AccessTools.Method(typeof(ItemCountRedirectPatch), nameof(GetItemCount))));
            // 替换 history.ItemUnlocked(int itemId) 调用
            method = AccessTools.Method(typeof(GameHistoryData), nameof(GameHistoryData.ItemUnlocked),
                [typeof(int)]);
            codeMatcher = new CodeMatcher(codeMatcher.InstructionEnumeration())
                .MatchForward(false,
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt
                                       && i.operand.Equals(method)))
                .Repeat(matcher => matcher
                    .SetAndAdvance(OpCodes.Call,
                        AccessTools.Method(typeof(ItemCountRedirectPatch), nameof(ItemUnlocked))));
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
    [HarmonyPatch]
    private static class ItemUnlockedPatch {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods() {
            yield return AccessTools.Method(typeof(UIBuildMenu), nameof(UIBuildMenu.OnChildButtonClick));
            yield return AccessTools.Method(typeof(UIBuildMenu), nameof(UIBuildMenu.SetCurrentCategory));
            yield return AccessTools.Method(typeof(UIBuildMenu), nameof(UIBuildMenu._OnUpdate));
            yield return AccessTools.Method(typeof(UIItemPicker_Patch), nameof(UIItemPicker_Patch.CheckItem));
            yield return AccessTools.Method(typeof(BuildBarToolPlugin),
                nameof(BuildBarToolPlugin.UIBuildMenuOnUpdatePostPatch),
                [typeof(UIFunctionPanel).MakeByRefType()]);
        }

        [HarmonyTranspiler]
        [HarmonyPriority(Priority.High)]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
            ItemUnlocked_Transpiler(instructions);
    }

    private static IEnumerable<CodeInstruction> ItemUnlocked_Transpiler(IEnumerable<CodeInstruction> instructions) {
        try {
            // 替换 history.ItemUnlocked(int itemId) 调用
            var method = AccessTools.Method(typeof(GameHistoryData), nameof(GameHistoryData.ItemUnlocked),
                [typeof(int)]);
            var codeMatcher = new CodeMatcher(instructions)
                .MatchForward(false,
                    new CodeMatch(i => i.opcode == OpCodes.Callvirt
                                       && i.operand.Equals(method)))
                .Repeat(matcher => matcher
                    .SetAndAdvance(OpCodes.Call,
                        AccessTools.Method(typeof(ItemCountRedirectPatch), nameof(ItemUnlocked))));
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
                AddConstructableCountsInStorage(this, player, ref num); // 在此插入方法
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
                    new CodeInstruction(OpCodes.Ldloca_S, (byte)0),// 循环内执行 num += count
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
