using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Compatibility.LegacySave;

/// <summary>
/// 仅迁移 FE 2.2.10 仍有当前对应物的 Item/Model/Recipe 物理引用，并清理无对应旧 ID。
/// 不迁移 Tech、不读写 FE 自有 DataCenter/Recipe 状态。
/// 退休实体/预建筑必须延迟到 PlanetFactory.Import 完整消费外层 payload 后再 Remove*WithComponents。
/// </summary>
public static class LegacyProtoMigration {
    [ThreadStatic]
    private static bool inGameDataImport;

    [ThreadStatic]
    private static bool migrateCurrentGameData;

    private static bool migratePendingSave;

    // 同线程 PlanetFactory.Import 作用域：避免跨 factory 的 id 串扰；游戏导入通常单线程。
    [ThreadStatic]
    private static bool inPlanetFactoryImport;

    [ThreadStatic]
    private static List<int> pendingRetiredEntityIds;

    [ThreadStatic]
    private static List<int> pendingRetiredPrebuildIds;
    // v2.2.10 映射：仅保留当前仍有对应物的物理引用。
    private static readonly Dictionary<int, int> ItemIds = new() {
        [8016] = IFE通用原胚, // 8115
        [8021] = IFE高级交互塔, // 8121
        [8022] = IFE基础矿物塔, // 8123
        [8026] = IFE基础解析塔, // 8122
        [8027] = IFE高级转化塔, // 8124
        [8028] = IFE行星内物流交互站, // 8131
        [8029] = IFE星际物流交互站, // 8132
    };

    private static readonly Dictionary<int, int> ModelIds = new() {
        [601] = MFE高级交互塔, // 651
        [602] = MFE基础矿物塔, // 653
        [606] = MFE基础解析塔, // 652
        [607] = MFE高级转化塔, // 654
        [608] = MFE行星内物流交互站, // 661
        [609] = MFE星际物流交互站, // 662
    };

    private static readonly Dictionary<int, int> RecipeIds = new() {
        [928] = RFE行星内物流交互站, // 1061
        [929] = RFE星际物流交互站, // 1062
    };

    // 仅枚举 v2.2.10 当前无对应物的旧 ID，不用 8000..8999 宽泛范围。
    private static readonly HashSet<int> RetiredItemIds = [
        8001, 8002, 8003, 8004, 8005, 8006, 8007,
        8011, 8012, 8013, 8014, 8015,
        8017, 8018,
        8023, 8024, 8025,
        8031, 8032, 8033, 8034, 8035, 8036, 8037,
        8041, 8042, 8043, 8044,
        8048, 8049,
    ];

    private static readonly HashSet<int> RetiredModelIds = [
        603, 604, 605,
        611, 612, 613, 614, 615, 616, 617,
    ];

    private static readonly HashSet<int> RetiredRecipeIds = [
        901, 902, 903, 904, 905, 906, 907,
        916,
        921, 922, 923, 924, 925, 926, 927,
        931, 932, 933, 934, 935, 936, 937,
    ];

    private static bool ShouldMapImportedId => !inGameDataImport || migrateCurrentGameData;

    public static bool IsRetiredItemId(int id) => RetiredItemIds.Contains(id);

    public static bool IsRetiredModelId(int id) => RetiredModelIds.Contains(id);

    public static bool IsRetiredRecipeId(int id) => RetiredRecipeIds.Contains(id);

    /// <summary>
    /// 导入物品 ID：2.2 存档及独立导入执行当前映射或退休清零，当前格式的 GameData 导入原样返回。
    /// </summary>
    public static int MapItemId(int id) {
        if (!ShouldMapImportedId) {
            return id;
        }
        if (ItemIds.TryGetValue(id, out int mapped)) {
            return mapped;
        }
        return RetiredItemIds.Contains(id) ? 0 : id;
    }

    /// <summary>
    /// 导入模型 ID：2.2 存档及独立导入执行当前映射或退休清零，当前格式的 GameData 导入原样返回。
    /// </summary>
    public static int MapModelId(int id) {
        if (!ShouldMapImportedId) {
            return id;
        }
        if (ModelIds.TryGetValue(id, out int mapped)) {
            return mapped;
        }
        return RetiredModelIds.Contains(id) ? 0 : id;
    }

    /// <summary>
    /// 导入配方 ID：2.2 存档及独立导入执行当前映射或退休清零，当前格式的 GameData 导入原样返回。
    /// </summary>
    public static int MapRecipeId(int id) {
        if (!ShouldMapImportedId) {
            return id;
        }
        if (RecipeIds.TryGetValue(id, out int mapped)) {
            return mapped;
        }
        return RetiredRecipeIds.Contains(id) ? 0 : id;
    }

    private static bool WasRetiredContent(int originalId, int mappedId) => originalId > 0 && mappedId == 0;

    private static bool ReadLegacySaveGeneration(string saveName) {
        string path = Path.Combine(GameConfig.gameSaveFolder, $"{saveName}.moddsv");
        if (!File.Exists(path)) {
            return false;
        }
        try {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using BinaryReader reader = new(stream);
            if (reader.ReadChar() != 'M' || reader.ReadChar() != 'O' || reader.ReadChar() != 'D') {
                return false;
            }
            reader.ReadInt32();
            for (int i = 0; i < 5; i++) {
                reader.ReadInt64();
            }
            int entryCount = reader.ReadInt32();
            if (entryCount < 0 || entryCount > 10000) {
                return false;
            }
            long feDataStart = -1;
            for (int i = 0; i < entryCount; i++) {
                string guid = reader.ReadString();
                long dataStart = reader.ReadInt64();
                reader.ReadInt64();
                if (guid == PluginInfo.PLUGIN_GUID) {
                    feDataStart = dataStart;
                }
            }
            if (feDataStart < 0 || feDataStart > stream.Length - sizeof(int)) {
                return false;
            }
            stream.Position = feDataStart;
            int feSaveVersion = reader.ReadInt32();
            if (feSaveVersion is 1 or 2) { return true; }
            if (feSaveVersion != FE.Lifecycle.FeatureSaveRegistry.CurrentVersion) {
                throw new NotSupportedException($"不支持 FE 存档格式 {feSaveVersion}；只支持当前格式和 2.2 存档，不兼容历史 3.0 存档。");
            }
            return false;
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or FormatException or DecoderFallbackException) {
            LogWarning($"[LegacySave] 无法读取 {saveName}.moddsv 的 FE 版本，已禁用 2.2 物理转换：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 在原版 GameData 导入前识别 2.2 的格式1/2，并拒绝历史3.0存档。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameSave), nameof(GameSave.LoadCurrentGame))]
    public static void GameSave_LoadCurrentGame_Prefix(string saveName) {
        migratePendingSave = ReadLegacySaveGeneration(saveName);
    }

    /// <summary>
    /// 将已识别的存档代际限定在本次 GameData.Import 作用域内；独立蓝图导入仍执行旧 ID 映射。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameData), nameof(GameData.Import))]
    public static void GameData_Import_Prefix() {
        inGameDataImport = true;
        migrateCurrentGameData = migratePendingSave;
    }

    /// <summary>
    /// GameData 导入结束或异常退出时清理本次存档的迁移门禁状态。
    /// </summary>
    /// <param name="__exception">原始导入异常；成功时为 null。</param>
    /// <returns>原样返回导入异常。</returns>
    [HarmonyFinalizer]
    [HarmonyPatch(typeof(GameData), nameof(GameData.Import))]
    public static Exception GameData_Import_Finalizer(Exception __exception) {
        inGameDataImport = false;
        migrateCurrentGameData = false;
        migratePendingSave = false;
        return __exception;
    }

    private static void EnsureImportScopeLists() {
        pendingRetiredEntityIds ??= [];
        pendingRetiredPrebuildIds ??= [];
    }

    private static void ClearImportScope() {
        inPlanetFactoryImport = false;
        pendingRetiredEntityIds?.Clear();
        pendingRetiredPrebuildIds?.Clear();
    }

    private static void MarkRetiredEntity(int entityId) {
        if (!inPlanetFactoryImport || entityId <= 0) {
            return;
        }
        EnsureImportScopeLists();
        pendingRetiredEntityIds.Add(entityId);
    }

    private static void MarkRetiredPrebuild(int prebuildId) {
        if (!inPlanetFactoryImport || prebuildId <= 0) {
            return;
        }
        EnsureImportScopeLists();
        pendingRetiredPrebuildIds.Add(prebuildId);
    }

    private static void FlushRetiredPlanetObjects(PlanetFactory factory) {
        if (factory == null) {
            return;
        }
        EnsureImportScopeLists();
        // 先实体后预建筑：组件池与连接由原生 Remove*WithComponents 一并清理。
        for (int i = 0; i < pendingRetiredEntityIds.Count; i++) {
            int entityId = pendingRetiredEntityIds[i];
            if (entityId > 0
                && factory.entityPool != null
                && entityId < factory.entityPool.Length
                && factory.entityPool[entityId].id == entityId) {
                factory.RemoveEntityWithComponents(entityId, false);
            }
        }
        for (int i = 0; i < pendingRetiredPrebuildIds.Count; i++) {
            int prebuildId = pendingRetiredPrebuildIds[i];
            if (prebuildId > 0
                && factory.prebuildPool != null
                && prebuildId < factory.prebuildPool.Length
                && factory.prebuildPool[prebuildId].id == prebuildId) {
                factory.RemovePrebuildWithComponents(prebuildId);
            }
        }
    }

    /// <summary>
    /// 每次 PlanetFactory.Import 开始清空当前线程待清理清单，隔离不同 factory 的 id。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.Import))]
    public static void PlanetFactory_Import_Prefix() {
        EnsureImportScopeLists();
        pendingRetiredEntityIds.Clear();
        pendingRetiredPrebuildIds.Clear();
        inPlanetFactoryImport = true;
    }

    /// <summary>
    /// 外层 animation/sign/connection 与组件池均已读完后，再移除退休实体/预建筑。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.Import))]
    public static void PlanetFactory_Import_Postfix(PlanetFactory __instance) {
        try {
            FlushRetiredPlanetObjects(__instance);
        } finally {
            ClearImportScope();
        }
    }

    /// <summary>
    /// Import 异常时也关闭作用域，避免待清理 id 泄漏到下一 factory。
    /// </summary>
    [HarmonyFinalizer]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.Import))]
    public static Exception PlanetFactory_Import_Finalizer(Exception __exception) {
        ClearImportScope();
        return __exception;
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

    /// <summary>
    /// 只映射 LDB.Select 查询参数，不改写字段 recipeId，避免 Import 在读完 payload 前因 recipeId&lt;=0 提前 return。
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(AssemblerComponent), nameof(AssemblerComponent.Import))]
    public static IEnumerable<CodeInstruction> AssemblerComponent_Import_Transpiler(
        IEnumerable<CodeInstruction> instructions) {
        return MapSelectArgument(instructions,
            AccessTools.Method(typeof(ProtoSet<RecipeProto>), nameof(ProtoSet<RecipeProto>.Select), [typeof(int)]),
            AccessTools.Method(typeof(LegacyProtoMigration), nameof(MapRecipeId)));
    }

    /// <summary>
    /// 928/929：字段切到新配方（原版 Import 已按映射后 Select 结果校验 served/produced）。
    /// 退休配方：recipeId=0 并清空运行数组与 replicating 状态。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(AssemblerComponent), nameof(AssemblerComponent.Import))]
    public static void AssemblerComponent_Import_Postfix(ref AssemblerComponent __instance) {
        int originalRecipeId = __instance.recipeId;
        int mappedRecipeId = MapRecipeId(originalRecipeId);
        if (WasRetiredContent(originalRecipeId, mappedRecipeId)) {
            __instance.recipeId = 0;
            __instance.recipeExecuteData = null;
            __instance.recipeType = ERecipeType.None;
            __instance.served = null;
            __instance.incServed = null;
            __instance.needs = null;
            __instance.produced = null;
            __instance.replicating = false;
            __instance.time = 0;
            __instance.extraTime = 0;
            __instance.cycleCount = 0;
            __instance.extraCycleCount = 0;
            return;
        }
        __instance.recipeId = mappedRecipeId;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EntityData), nameof(EntityData.Import))]
    public static void EntityData_Import_Postfix(ref EntityData __instance) {
        int originalProtoId = __instance.protoId;
        int mappedItemId = MapItemId(originalProtoId);
        if (WasRetiredContent(originalProtoId, mappedItemId)) {
            // 不可在此 SetNull/清 id：PlanetFactory.Import 在 EntityData.Import 之后仍按 id!=0
            // 继续读取 animation/sign/16 连接字节；提前 id=0 会跳过 payload 并令后续存档流错位。
            // 组件池此时尚未导入，仅 SetNull 也会留下孤儿组件。延迟到 PlanetFactory.Import postfix。
            int entityId = __instance.id;
            __instance.protoId = 0;
            __instance.modelIndex = 0;
            // 保留 id 与组件引用，供外层完整消费 payload 与后续 RemoveEntityWithComponents。
            MarkRetiredEntity(entityId);
            return;
        }
        __instance.protoId = (short)mappedItemId;
        __instance.modelIndex = (short)MapModelId(__instance.modelIndex);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PrebuildData), nameof(PrebuildData.Import))]
    public static void PrebuildData_Import_Postfix(ref PrebuildData __instance) {
        int originalProtoId = __instance.protoId;
        int mappedItemId = MapItemId(originalProtoId);
        if (WasRetiredContent(originalProtoId, mappedItemId)) {
            // 同实体：保留 id，仅置安全 proto/model/recipe/filter；连接字节由外层按 id!=0 读取。
            int prebuildId = __instance.id;
            __instance.protoId = 0;
            __instance.modelIndex = 0;
            __instance.recipeId = 0;
            __instance.filterId = 0;
            MarkRetiredPrebuild(prebuildId);
            return;
        }
        __instance.protoId = (short)mappedItemId;
        __instance.modelIndex = (short)MapModelId(__instance.modelIndex);
        __instance.recipeId = MapRecipeId(__instance.recipeId);
        __instance.filterId = MapItemId(__instance.filterId);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BlueprintBuilding), nameof(BlueprintBuilding.Import))]
    public static void BlueprintBuilding_Import_Postfix(BlueprintBuilding __instance) {
        int originalItemId = __instance.itemId;
        int mappedItemId = MapItemId(originalItemId);
        if (WasRetiredContent(originalItemId, mappedItemId)) {
            __instance.itemId = 0;
            __instance.modelIndex = 0;
            __instance.recipeId = 0;
            __instance.filterId = 0;
            return;
        }
        __instance.itemId = (short)mappedItemId;
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
            int mappedItemId = MapItemId(originalItemId);
            __instance.grids[i].itemId = mappedItemId;
            __instance.grids[i].filter = MapItemId(__instance.grids[i].filter);
            if (WasRetiredContent(originalItemId, mappedItemId)) {
                __instance.grids[i].count = 0;
                __instance.grids[i].inc = 0;
                __instance.grids[i].stackSize = 0;
                continue;
            }
            ItemProto item = LDB.items.Select(mappedItemId);
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
            int mappedItemId = MapItemId(originalItemId);
            __instance.grids[i].itemId = mappedItemId;
            if (WasRetiredContent(originalItemId, mappedItemId)) {
                __instance.grids[i].count = 0;
                __instance.grids[i].inc = 0;
                __instance.grids[i].ordered = 0;
                __instance.grids[i].requireCount = 0;
                __instance.grids[i].recycleCount = 0;
                __instance.grids[i].stackSize = 0;
                continue;
            }
            ItemProto item = LDB.items.Select(mappedItemId);
            if (item != null) {
                __instance.grids[i].stackSize = item.StackSize;
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StationStore), nameof(StationStore.Import))]
    public static void StationStore_Import_Postfix(ref StationStore __instance) {
        int originalItemId = __instance.itemId;
        int mappedItemId = MapItemId(originalItemId);
        __instance.itemId = mappedItemId;
        if (WasRetiredContent(originalItemId, mappedItemId)) {
            __instance.count = 0;
            __instance.inc = 0;
            __instance.localOrder = 0;
            __instance.remoteOrder = 0;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CargoContainer), nameof(CargoContainer.Import))]
    public static void CargoContainer_Import_Postfix(CargoContainer __instance) {
        if (__instance.cargoPool == null) {
            return;
        }
        for (int i = 0; i < __instance.cursor; i++) {
            short originalItemId = __instance.cargoPool[i].item;
            int mappedItemId = MapItemId(originalItemId);
            __instance.cargoPool[i].item = (short)mappedItemId;
            if (WasRetiredContent(originalItemId, mappedItemId)) {
                __instance.cargoPool[i].stack = 0;
                __instance.cargoPool[i].inc = 0;
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TrashObject), nameof(TrashObject.Import))]
    public static void TrashObject_Import_Postfix(ref TrashObject __instance) {
        int originalItemId = __instance.item;
        int mappedItemId = MapItemId(originalItemId);
        __instance.item = mappedItemId;
        if (WasRetiredContent(originalItemId, mappedItemId)) {
            __instance.count = 0;
            __instance.inc = 0;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FractionatorComponent), nameof(FractionatorComponent.Import))]
    public static void FractionatorComponent_Import_Postfix(ref FractionatorComponent __instance) {
        int originalFluidId = __instance.fluidId;
        int originalProductId = __instance.productId;
        int mappedFluidId = MapItemId(originalFluidId);
        int mappedProductId = MapItemId(originalProductId);
        __instance.fluidId = mappedFluidId;
        __instance.productId = mappedProductId;
        if (WasRetiredContent(originalFluidId, mappedFluidId)) {
            __instance.fluidInputCount = 0;
            __instance.fluidInputInc = 0;
            __instance.fluidInputCargoCount = 0f;
            __instance.fluidOutputCount = 0;
            __instance.fluidOutputInc = 0;
        }
        if (WasRetiredContent(originalProductId, mappedProductId)) {
            __instance.productOutputCount = 0;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InserterComponent), nameof(InserterComponent.Import))]
    public static void InserterComponent_Import_Postfix(ref InserterComponent __instance) {
        int originalItemId = __instance.itemId;
        int mappedItemId = MapItemId(originalItemId);
        __instance.filter = MapItemId(__instance.filter);
        __instance.itemId = mappedItemId;
        if (WasRetiredContent(originalItemId, mappedItemId)) {
            __instance.stackCount = 0;
            __instance.itemCount = 0;
            __instance.itemInc = 0;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DispenserComponent), nameof(DispenserComponent.Import))]
    public static void DispenserComponent_Import_Postfix(ref DispenserComponent __instance) {
        __instance.filter = MapItemId(__instance.filter);
        if (__instance.holdupPackage == null) {
            __instance.holdupItemCount = 0;
            return;
        }
        int retainedCount = 0;
        for (int i = 0; i < __instance.holdupItemCount; i++) {
            DispenserStore store = __instance.holdupPackage[i];
            int originalItemId = store.itemId;
            int mappedItemId = MapItemId(originalItemId);
            if (WasRetiredContent(originalItemId, mappedItemId)) {
                continue;
            }
            store.itemId = mappedItemId;
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
        int mappedItemId = MapItemId(originalItemId);
        __instance.itemId = mappedItemId;
        if (WasRetiredContent(originalItemId, mappedItemId)) {
            __instance.itemCount = 0;
            __instance.inc = 0;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ShipData), nameof(ShipData.Import))]
    public static void ShipData_Import_Postfix(ref ShipData __instance) {
        int originalItemId = __instance.itemId;
        int mappedItemId = MapItemId(originalItemId);
        __instance.itemId = mappedItemId;
        if (WasRetiredContent(originalItemId, mappedItemId)) {
            __instance.itemCount = 0;
            __instance.inc = 0;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CourierData), nameof(CourierData.Import))]
    public static void CourierData_Import_Postfix(ref CourierData __instance) {
        int originalItemId = __instance.itemId;
        int mappedItemId = MapItemId(originalItemId);
        __instance.itemId = mappedItemId;
        if (WasRetiredContent(originalItemId, mappedItemId)) {
            __instance.itemCount = 0;
            __instance.inc = 0;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DispenserStore), nameof(DispenserStore.Import))]
    public static void DispenserStore_Import_Postfix(ref DispenserStore __instance) {
        int originalItemId = __instance.itemId;
        int mappedItemId = MapItemId(originalItemId);
        __instance.itemId = mappedItemId;
        if (WasRetiredContent(originalItemId, mappedItemId)) {
            __instance.count = 0;
            __instance.inc = 0;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DeliveryLogisticOrder), nameof(DeliveryLogisticOrder.Import))]
    public static void DeliveryLogisticOrder_Import_Postfix(ref DeliveryLogisticOrder __instance) {
        int originalItemId = __instance.itemId;
        int mappedItemId = MapItemId(originalItemId);
        __instance.itemId = mappedItemId;
        if (WasRetiredContent(originalItemId, mappedItemId)) {
            __instance.thisOrdered = 0;
            __instance.otherOrdered = 0;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(LocalLogisticOrder), nameof(LocalLogisticOrder.Import))]
    public static void LocalLogisticOrder_Import_Postfix(ref LocalLogisticOrder __instance) {
        int originalItemId = __instance.itemId;
        int mappedItemId = MapItemId(originalItemId);
        __instance.itemId = mappedItemId;
        if (WasRetiredContent(originalItemId, mappedItemId)) {
            __instance.thisOrdered = 0;
            __instance.otherOrdered = 0;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RemoteLogisticOrder), nameof(RemoteLogisticOrder.Import))]
    public static void RemoteLogisticOrder_Import_Postfix(ref RemoteLogisticOrder __instance) {
        int originalItemId = __instance.itemId;
        int mappedItemId = MapItemId(originalItemId);
        __instance.itemId = mappedItemId;
        if (WasRetiredContent(originalItemId, mappedItemId)) {
            __instance.thisOrdered = 0;
            __instance.otherOrdered = 0;
        }
    }
}
