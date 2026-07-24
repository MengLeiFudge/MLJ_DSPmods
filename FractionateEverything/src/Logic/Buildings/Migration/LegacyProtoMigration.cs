using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Logic.Buildings.Migration;

/// <summary>
/// 迁移 FE 旧版本中仍有现役对应物的 ID，并清理没有新对应物的废弃物品。
/// </summary>
public static class LegacyProtoMigration {
    private static readonly Dictionary<int, int> ItemIds = new() {
        // FE 2.x 的 I-V 型原胚没有稳定的新谱系对应物；只有定向原胚迁移为通用原胚。
        [8016] = IFE通用原胚,
        [8021] = IFE培养塔,
        [8022] = IFE复制塔,
        [8026] = IFE解析塔,
        [8027] = IFE转化塔,
        [8028] = IFE行星内物流交互站,
        [8029] = IFE星际物流交互站,
    };

    private static readonly Dictionary<int, int> ModelIds = new() {
        [601] = MFE培养塔,
        [602] = MFE复制塔,
        [606] = MFE解析塔,
        [607] = MFE转化塔,
        [608] = MFE行星内物流交互站,
        [609] = MFE星际物流交互站,
    };

    private static readonly Dictionary<int, int> RecipeIds = new() {
        [928] = RFE行星内物流交互站,
        [929] = RFE星际物流交互站,
    };

    private static readonly Dictionary<int, int> TechIds = new() {
        [1251] = TFE分馏数据中心,
        [1252] = TFE分馏塔原胚,
        [1253] = TFE培养塔,
        [1254] = TFE复制塔,
        [1258] = TFE解析塔,
        [1259] = TFE转化塔,
        [1281] = TFE阶段补给1,
        [1282] = TFE阶段补给2,
        [1283] = TFE阶段补给3,
        [1284] = TFE阶段补给4,
        [1285] = TFE阶段补给5,
        [1286] = TFE阶段补给6,
    };

    public static bool IsRetiredItemId(int id) => id is >= 8011 and <= 8015;

    public static int MapItemId(int id) => ItemIds.TryGetValue(id, out int mapped)
        ? mapped
        : IsRetiredItemId(id) ? 0 : id;

    public static int MapModelId(int id) => ModelIds.TryGetValue(id, out int mapped) ? mapped : id;

    public static int MapRecipeId(int id) => RecipeIds.TryGetValue(id, out int mapped) ? mapped : id;

    public static int MapTechId(int id) => TechIds.TryGetValue(id, out int mapped) ? mapped : id;

    /// <summary>
    /// 合并数据中心中有现役对应物的 FE 2.x 旧槽位并清空来源槽位。
    /// </summary>
    public static void MigrateDataCenterInventory(long[] counts, long[] incs) {
        foreach (KeyValuePair<int, int> pair in ItemIds) {
            if (pair.Key >= counts.Length || pair.Value >= counts.Length || pair.Key == pair.Value) {
                continue;
            }
            counts[pair.Value] += counts[pair.Key];
            incs[pair.Value] += incs[pair.Key];
            counts[pair.Key] = 0;
            incs[pair.Key] = 0;
        }
        for (int retiredId = 8011; retiredId <= 8015 && retiredId < counts.Length; retiredId++) {
            counts[retiredId] = 0;
            incs[retiredId] = 0;
        }
    }

    private static IEnumerable<CodeInstruction> MapSelectArgument(IEnumerable<CodeInstruction> instructions,
        MethodInfo selectMethod, MethodInfo mapMethod) {
        foreach (CodeInstruction instruction in instructions) {
            if (instruction.Calls(selectMethod)) {
                yield return new CodeInstruction(OpCodes.Call, mapMethod);
            }
            yield return instruction;
        }
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.Import))]
    public static IEnumerable<CodeInstruction> GameHistoryData_Import_Transpiler(
        IEnumerable<CodeInstruction> instructions) {
        return MapSelectArgument(instructions,
            AccessTools.Method(typeof(ProtoSet<TechProto>), nameof(ProtoSet<TechProto>.Select), [typeof(int)]),
            AccessTools.Method(typeof(LegacyProtoMigration), nameof(MapTechId)));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.Import))]
    public static void GameHistoryData_Import_Postfix(GameHistoryData __instance) {
        foreach (KeyValuePair<int, int> pair in TechIds) {
            if (!__instance.techStates.TryGetValue(pair.Key, out TechState legacyState)) {
                continue;
            }
            if (__instance.techStates.TryGetValue(pair.Value, out TechState activeState)) {
                __instance.techStates[pair.Value] = MergeTechState(activeState, legacyState);
            } else {
                __instance.techStates[pair.Value] = legacyState;
            }
            __instance.techStates.Remove(pair.Key);
        }

        foreach (KeyValuePair<int, int> pair in RecipeIds) {
            if (!__instance.recipeUnlocked.Remove(pair.Key)) {
                continue;
            }
            __instance.recipeUnlocked.Add(pair.Value);
            UnlockTechState(__instance, pair.Key == 928 ? TFE行星内物流交互 : TFE星际物流交互);
        }
        __instance.currentTech = MapTechId(__instance.currentTech);
        if (__instance.techQueue != null) {
            for (int i = 0; i < __instance.techQueue.Length; i++) {
                __instance.techQueue[i] = MapTechId(__instance.techQueue[i]);
            }
        }
    }

    private static TechState MergeTechState(TechState activeState, TechState legacyState) {
        if (activeState.unlocked || legacyState.unlocked) {
            activeState.unlocked = true;
            activeState.curLevel = activeState.maxLevel;
            activeState.hashUploaded = activeState.hashNeeded;
        } else {
            activeState.curLevel = System.Math.Min(activeState.maxLevel,
                System.Math.Max(activeState.curLevel, legacyState.curLevel));
            long uploaded = System.Math.Max(activeState.hashUploaded, legacyState.hashUploaded);
            activeState.hashUploaded = activeState.hashNeeded > 0
                ? System.Math.Min(activeState.hashNeeded, uploaded)
                : uploaded;
        }
        if (activeState.unlockTick <= 0
            || legacyState.unlockTick > 0 && legacyState.unlockTick < activeState.unlockTick) {
            activeState.unlockTick = legacyState.unlockTick;
        }
        return activeState;
    }

    private static void UnlockTechState(GameHistoryData history, int techId) {
        if (!history.techStates.TryGetValue(techId, out TechState state)) {
            return;
        }
        state.unlocked = true;
        state.curLevel = state.maxLevel;
        state.hashUploaded = state.hashNeeded;
        history.techStates[techId] = state;
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(AssemblerComponent), nameof(AssemblerComponent.Import))]
    public static IEnumerable<CodeInstruction> AssemblerComponent_Import_Transpiler(
        IEnumerable<CodeInstruction> instructions) {
        return MapSelectArgument(instructions,
            AccessTools.Method(typeof(ProtoSet<RecipeProto>), nameof(ProtoSet<RecipeProto>.Select), [typeof(int)]),
            AccessTools.Method(typeof(LegacyProtoMigration), nameof(MapRecipeId)));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AssemblerComponent), nameof(AssemblerComponent.Import))]
    public static void AssemblerComponent_Import_Postfix(ref AssemblerComponent __instance) {
        __instance.recipeId = MapRecipeId(__instance.recipeId);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EntityData), nameof(EntityData.Import))]
    public static void EntityData_Import_Postfix(ref EntityData __instance) {
        __instance.protoId = (short)MapItemId(__instance.protoId);
        __instance.modelIndex = (short)MapModelId(__instance.modelIndex);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PrebuildData), nameof(PrebuildData.Import))]
    public static void PrebuildData_Import_Postfix(ref PrebuildData __instance) {
        __instance.protoId = (short)MapItemId(__instance.protoId);
        __instance.modelIndex = (short)MapModelId(__instance.modelIndex);
        __instance.recipeId = MapRecipeId(__instance.recipeId);
        __instance.filterId = MapItemId(__instance.filterId);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BlueprintBuilding), nameof(BlueprintBuilding.Import))]
    public static void BlueprintBuilding_Import_Postfix(BlueprintBuilding __instance) {
        __instance.itemId = (short)MapItemId(__instance.itemId);
        __instance.modelIndex = (short)MapModelId(__instance.modelIndex);
        __instance.recipeId = MapRecipeId(__instance.recipeId);
        __instance.filterId = MapItemId(__instance.filterId);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StorageComponent), nameof(StorageComponent.Import))]
    public static void StorageComponent_Import_Postfix(StorageComponent __instance) {
        if (__instance.grids == null) {
            return;
        }
        for (int i = 0; i < __instance.grids.Length; i++) {
            int originalItemId = __instance.grids[i].itemId;
            __instance.grids[i].itemId = MapItemId(originalItemId);
            __instance.grids[i].filter = MapItemId(__instance.grids[i].filter);
            if (IsRetiredItemId(originalItemId)) {
                __instance.grids[i].count = 0;
                __instance.grids[i].inc = 0;
                __instance.grids[i].stackSize = 0;
            }
            ItemProto item = LDB.items.Select(__instance.grids[i].itemId);
            if (item != null) {
                __instance.grids[i].stackSize = item.StackSize;
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DeliveryPackage), nameof(DeliveryPackage.Import))]
    public static void DeliveryPackage_Import_Postfix(DeliveryPackage __instance) {
        if (__instance.grids == null) {
            return;
        }
        for (int i = 0; i < __instance.grids.Length; i++) {
            int originalItemId = __instance.grids[i].itemId;
            __instance.grids[i].itemId = MapItemId(originalItemId);
            if (IsRetiredItemId(originalItemId)) {
                __instance.grids[i].count = 0;
                __instance.grids[i].inc = 0;
                __instance.grids[i].ordered = 0;
                __instance.grids[i].requireCount = 0;
                __instance.grids[i].recycleCount = 0;
                __instance.grids[i].stackSize = 0;
            }
            ItemProto item = LDB.items.Select(__instance.grids[i].itemId);
            if (item != null) {
                __instance.grids[i].stackSize = item.StackSize;
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StationStore), nameof(StationStore.Import))]
    public static void StationStore_Import_Postfix(ref StationStore __instance) {
        int originalItemId = __instance.itemId;
        __instance.itemId = MapItemId(originalItemId);
        if (IsRetiredItemId(originalItemId)) {
            __instance.count = 0;
            __instance.inc = 0;
            __instance.localOrder = 0;
            __instance.remoteOrder = 0;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CargoContainer), nameof(CargoContainer.Import))]
    public static void CargoContainer_Import_Postfix(CargoContainer __instance) {
        for (int i = 0; i < __instance.cursor; i++) {
            short originalItemId = __instance.cargoPool[i].item;
            __instance.cargoPool[i].item = (short)MapItemId(originalItemId);
            if (IsRetiredItemId(originalItemId)) {
                __instance.cargoPool[i].stack = 0;
                __instance.cargoPool[i].inc = 0;
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FractionatorComponent), nameof(FractionatorComponent.Import))]
    public static void FractionatorComponent_Import_Postfix(ref FractionatorComponent __instance) {
        int originalFluidId = __instance.fluidId;
        int originalProductId = __instance.productId;
        __instance.fluidId = MapItemId(originalFluidId);
        __instance.productId = MapItemId(originalProductId);
        if (IsRetiredItemId(originalFluidId)) {
            __instance.fluidInputCount = 0;
            __instance.fluidInputInc = 0;
            __instance.fluidInputCargoCount = 0f;
            __instance.fluidOutputCount = 0;
            __instance.fluidOutputInc = 0;
        }
        if (IsRetiredItemId(originalProductId)) {
            __instance.productOutputCount = 0;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InserterComponent), nameof(InserterComponent.Import))]
    public static void InserterComponent_Import_Postfix(ref InserterComponent __instance) {
        int originalItemId = __instance.itemId;
        __instance.filter = MapItemId(__instance.filter);
        __instance.itemId = MapItemId(originalItemId);
        if (IsRetiredItemId(originalItemId)) {
            __instance.stackCount = 0;
            __instance.itemCount = 0;
            __instance.itemInc = 0;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DispenserComponent), nameof(DispenserComponent.Import))]
    public static void DispenserComponent_Import_Postfix(ref DispenserComponent __instance) {
        __instance.filter = MapItemId(__instance.filter);
        int retainedCount = 0;
        for (int i = 0; i < __instance.holdupItemCount; i++) {
            DispenserStore store = __instance.holdupPackage[i];
            if (IsRetiredItemId(store.itemId)) {
                continue;
            }
            store.itemId = MapItemId(store.itemId);
            __instance.holdupPackage[retainedCount++] = store;
        }
        for (int i = retainedCount; i < __instance.holdupItemCount; i++) {
            __instance.holdupPackage[i] = default;
        }
        __instance.holdupItemCount = retainedCount;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DroneData), nameof(DroneData.Import))]
    public static void DroneData_Import_Postfix(ref DroneData __instance) {
        int originalItemId = __instance.itemId;
        __instance.itemId = MapItemId(originalItemId);
        if (IsRetiredItemId(originalItemId)) {
            __instance.itemCount = 0;
            __instance.inc = 0;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ShipData), nameof(ShipData.Import))]
    public static void ShipData_Import_Postfix(ref ShipData __instance) {
        int originalItemId = __instance.itemId;
        __instance.itemId = MapItemId(originalItemId);
        if (IsRetiredItemId(originalItemId)) {
            __instance.itemCount = 0;
            __instance.inc = 0;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CourierData), nameof(CourierData.Import))]
    public static void CourierData_Import_Postfix(ref CourierData __instance) {
        int originalItemId = __instance.itemId;
        __instance.itemId = MapItemId(originalItemId);
        if (IsRetiredItemId(originalItemId)) {
            __instance.itemCount = 0;
            __instance.inc = 0;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DispenserStore), nameof(DispenserStore.Import))]
    public static void DispenserStore_Import_Postfix(ref DispenserStore __instance) {
        int originalItemId = __instance.itemId;
        __instance.itemId = MapItemId(originalItemId);
        if (IsRetiredItemId(originalItemId)) {
            __instance.count = 0;
            __instance.inc = 0;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DeliveryLogisticOrder), nameof(DeliveryLogisticOrder.Import))]
    public static void DeliveryLogisticOrder_Import_Postfix(ref DeliveryLogisticOrder __instance) {
        int originalItemId = __instance.itemId;
        __instance.itemId = MapItemId(originalItemId);
        if (IsRetiredItemId(originalItemId)) {
            __instance.thisOrdered = 0;
            __instance.otherOrdered = 0;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(LocalLogisticOrder), nameof(LocalLogisticOrder.Import))]
    public static void LocalLogisticOrder_Import_Postfix(ref LocalLogisticOrder __instance) {
        int originalItemId = __instance.itemId;
        __instance.itemId = MapItemId(originalItemId);
        if (IsRetiredItemId(originalItemId)) {
            __instance.thisOrdered = 0;
            __instance.otherOrdered = 0;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RemoteLogisticOrder), nameof(RemoteLogisticOrder.Import))]
    public static void RemoteLogisticOrder_Import_Postfix(ref RemoteLogisticOrder __instance) {
        int originalItemId = __instance.itemId;
        __instance.itemId = MapItemId(originalItemId);
        if (IsRetiredItemId(originalItemId)) {
            __instance.thisOrdered = 0;
            __instance.otherOrdered = 0;
        }
    }
}
