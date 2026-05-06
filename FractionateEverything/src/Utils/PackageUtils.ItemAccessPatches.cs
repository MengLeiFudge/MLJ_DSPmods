using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BuildBarTool;
using CommonAPI.Patches;
using FE.Compatibility;
using HarmonyLib;
using static FE.Logic.Manager.ItemManager;

namespace FE.Utils;

public static partial class Utils {
    #region 丢弃回收

    /// <summary>
    /// 扔掉的垃圾会自动回收到Mod背包
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.ThrowTrash))]
    private static bool Player_ThrowTrash_Prefix(Player __instance, int itemId, int count, int inc) {
        if (!TechItemInteractionUnlocked) {
            return true;
        }
        if (itemId <= 0 || itemId >= 12000 || itemValue[itemId] >= maxValue) {
            return true;
        }
        ManualUploadCount++;
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
        int itemId = __instance.inhandItemId;
        if (itemId <= 0 || itemId >= 12000 || itemValue[itemId] >= maxValue) {
            return true;
        }
        if (__instance.inhandItemId > 0 && __instance.inhandItemCount > 0) {
            ManualUploadCount++;
            AddItemToModData(__instance.inhandItemId, __instance.inhandItemCount, __instance.inhandItemInc, true);
        }
        __instance.inhandItemId = 0;
        __instance.inhandItemCount = 0;
        __instance.inhandItemInc = 0;
        return false;
    }

    #endregion

    #region 获取背包中物品的数目

    /// <summary>
    /// 兼容旧版炮台窗口与战斗模组对 turretNeeds 的直接索引访问。
    /// 某些炮台（玩家反馈为电浆塔）在异常 ammoType/空需求数组状态下会让原版 UI 直接越界，
    /// 因此在窗口打开、刷新、手动补弹前，先把当前 ammoType 对应的槽位补齐到可安全读取的长度。
    /// </summary>
    private static void EnsureTurretNeedsSafe(UITurretWindow turretWindow) {
        if (turretWindow == null || turretWindow.turretId <= 0 || turretWindow.defenseSystem == null) {
            return;
        }

        var turrets = turretWindow.defenseSystem.turrets;
        if (turrets.buffer == null || turretWindow.turretId >= turrets.buffer.Length) {
            return;
        }

        ref TurretComponent turret = ref turrets.buffer[turretWindow.turretId];
        if (turret.id != turretWindow.turretId) {
            return;
        }

        int ammoType = (int)turret.ammoType;
        if (ammoType < 0) {
            LogWarning($"UITurretWindow 检测到非法 ammoType={ammoType}，已回退为 0。");
            turret.ammoType = 0;
            ammoType = 0;
        }

        if (ItemProto.turretNeeds == null) {
            ItemProto.turretNeeds = new int[Math.Max(16, ammoType + 1)][];
        } else if (ammoType >= ItemProto.turretNeeds.Length) {
            Array.Resize(ref ItemProto.turretNeeds, Math.Max(ItemProto.turretNeeds.Length * 2, ammoType + 1));
        }

        int[] needs = ItemProto.turretNeeds[ammoType];
        if (needs != null && needs.Length >= MinTurretAmmoNeedCount) {
            return;
        }

        int[] padded = new int[MinTurretAmmoNeedCount];
        if (needs != null && needs.Length > 0) {
            Array.Copy(needs, padded, needs.Length);
        }
        ItemProto.turretNeeds[ammoType] = padded;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UITurretWindow), nameof(UITurretWindow.OnTurretIdChange))]
    [HarmonyPatch(typeof(UITurretWindow), nameof(UITurretWindow._OnUpdate))]
    [HarmonyPatch(typeof(UITurretWindow), nameof(UITurretWindow.OnHandFillAmmoButtonClick))]
    private static void UITurretWindow_EnsureTurretNeedsSafe(UITurretWindow __instance) {
        EnsureTurretNeedsSafe(__instance);
    }

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

    #endregion

    #region 从背包拿取物品

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
        if (itemId <= 0 || itemId >= 12000) {
            return 0;
        }
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
        if (itemId <= 0 || itemId >= 12000) {
            count = 0;
            return;
        }
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
        if (itemId <= 0 || itemId >= 12000) {
            count = 0;
            return;
        }
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
}
