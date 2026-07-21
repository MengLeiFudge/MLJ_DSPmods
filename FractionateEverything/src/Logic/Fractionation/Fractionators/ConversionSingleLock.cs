using System;
using System.Collections.Concurrent;
using System.IO;
using FE.Compatibility.Nebula;
using FE.Logic.Buildings.Migration;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Fractionation.Process;
using FE.Logic.Fractionation.FracRecipes;
using HarmonyLib;
using NebulaAPI;
using static FE.Logic.Fractionation.FracRecipes.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>
/// 分馏塔主路锁定状态、交互和存档逻辑。
/// </summary>
public static class FractionatorSingleLock {
    #region 分馏塔锁定

    /// <summary>
    /// 存储分馏塔锁定的输出物品ID。结构：
    /// (planetId, entityId) => lockedOutputItemId (0 = 未锁定)
    /// </summary>
    private static readonly ConcurrentDictionary<(int, int), int> lockedOutputDic = [];
    private const int LockedOutputParamMagic = 0x4C4F434B;
    private const int LockedOutputParamVersion = 1;
    private static bool hasLockedOutputClipboard;
    private static int lockedOutputClipboardItemId;

    private static void ClearLockedOutputClipboard() {
        hasLockedOutputClipboard = false;
        lockedOutputClipboardItemId = 0;
    }

    /// <summary>
    /// 读取分馏塔主路锁定输出的实例状态。
    /// </summary>
    public static void LockedOutputImport(BinaryReader r) {
        lockedOutputDic.Clear();
        int count = r.ReadInt32();
        for (int i = 0; i < count; i++) {
            int planetId = r.ReadInt32();
            int entityId = r.ReadInt32();
            int itemId = LegacyProtoMigration.MapItemId(r.ReadInt32());
            lockedOutputDic.TryAdd((planetId, entityId), itemId);
        }
    }

    /// <summary>
    /// 写入分馏塔主路锁定输出的实例状态。
    /// </summary>
    public static void LockedOutputExport(BinaryWriter w) {
        w.Write(lockedOutputDic.Count);
        foreach (var p in lockedOutputDic) {
            w.Write(p.Key.Item1);
            w.Write(p.Key.Item2);
            w.Write(p.Value);
        }
    }

    /// <summary>
    /// 重置分馏塔主路锁定输出的实例状态。
    /// </summary>
    public static void LockedOutputIntoOtherSave() {
        lockedOutputDic.Clear();
        ClearLockedOutputClipboard();
    }

    /// <summary>
    /// 读取指定分馏塔当前锁定的输出物品。
    /// </summary>
    public static int GetLockedOutput(this FractionatorComponent fractionator, PlanetFactory factory) {
        int planetId = factory.planetId;
        int entityId = fractionator.entityId;
        return lockedOutputDic.TryGetValue((planetId, entityId), out int itemId) ? itemId : 0;
    }

    /// <summary>
    /// 设置指定分馏塔当前锁定的输出物品。
    /// </summary>
    public static void SetLockedOutput(this FractionatorComponent fractionator, PlanetFactory factory, int itemId) {
        int planetId = factory.planetId;
        int entityId = fractionator.entityId;
        if (itemId == 0) {
            lockedOutputDic.TryRemove((planetId, entityId), out _);
        } else {
            lockedOutputDic[(planetId, entityId)] = itemId;
        }
    }

    /// <summary>
    /// 统一处理单锁设置与联机广播。广播只发生在本地手动操作时。
    /// </summary>
    public static int SetLockedOutputAndSync(this FractionatorComponent fractionator, PlanetFactory factory, int itemId,
        bool manual = false) {
        int normalizedItemId = fractionator.NormalizeLockedOutput(factory, itemId);
        fractionator.SetLockedOutput(factory, normalizedItemId);
        if (manual && factory != null && NebulaModAPI.IsMultiplayerActive && !NebulaMultiplayerModAPI.IsOthers()) {
            int buildingId = factory.entityPool[fractionator.entityId].protoId;
            NebulaModAPI.MultiplayerSession.Network.SendPacket(
                new BuildingChangePacket(buildingId, 2, factory.planetId, fractionator.entityId, normalizedItemId));
        }
        return normalizedItemId;
    }

    private static bool TryGetSingleLockFractionator(PlanetFactory factory, int entityId,
        out FractionatorComponent fractionator) {
        fractionator = default;
        if (factory == null || entityId <= 0 || entityId >= factory.entityPool.Length) {
            return false;
        }
        EntityData entityData = factory.entityPool[entityId];
        if (entityData.id != entityId
            || entityData.protoId is not (IFE交互塔 or IFE转化塔)
            || entityData.fractionatorId <= 0) {
            return false;
        }
        fractionator = factory.factorySystem.fractionatorPool[entityData.fractionatorId];
        return fractionator.id == entityData.fractionatorId;
    }

    private static PlanetFactory GetFactoryByPlanetId(int planetId) {
        GameData gameData = GameMain.data;
        PlanetData planet = gameData?.galaxy?.PlanetById(planetId);
        if (planet?.factory != null) {
            return planet.factory;
        }
        int factoryIndex = planet?.factoryIndex ?? -1;
        if (gameData?.factories == null || factoryIndex < 0 || factoryIndex >= gameData.factories.Length) {
            return null;
        }
        return gameData.factories[factoryIndex];
    }

    /// <summary>
    /// 应用联机同步过来的单锁状态。若对应工厂当前已加载，则立刻规范化到运行态。
    /// </summary>
    public static void ApplyLockedOutputPacket(int planetId, int entityId, int itemId) {
        if (planetId <= 0 || entityId <= 0) {
            return;
        }
        if (itemId == 0) {
            lockedOutputDic.TryRemove((planetId, entityId), out _);
        } else {
            lockedOutputDic[(planetId, entityId)] = itemId;
        }
        PlanetFactory factory = GetFactoryByPlanetId(planetId);
        if (TryGetSingleLockFractionator(factory, entityId, out FractionatorComponent fractionator)) {
            fractionator.SetLockedOutput(factory, fractionator.NormalizeLockedOutput(factory, itemId));
        }
    }

    /// <summary>
    /// 校验并修正分馏塔锁定输出，使其匹配当前配方。
    /// </summary>
    public static int NormalizeLockedOutput(this FractionatorComponent fractionator, PlanetFactory factory,
        int itemId) {
        if (itemId == 0 || factory == null || fractionator.entityId <= 0
            || fractionator.entityId >= factory.entityPool.Length) {
            return 0;
        }

        int buildingId = factory.entityPool[fractionator.entityId].protoId;
        ERecipe recipeType = buildingId switch {
            IFE交互塔 => ERecipe.BuildingTrain,
            IFE转化塔 => ERecipe.Conversion,
            _ => (ERecipe)0,
        };
        if (recipeType == (ERecipe)0 || !TowerRuntimeModifierCache.IsMainOutputLockEnabled(recipeType)) {
            return 0;
        }
        if (fractionator.fluidId == 0) {
            return itemId;
        }

        BaseRecipe recipe = GetRecipe<BaseRecipe>(recipeType, fractionator.fluidId);
        if (recipe == null || !recipe.IsMainOutputLockCalibrated) {
            return 0;
        }
        if (recipe is ConversionRecipe conversionRecipe) {
            return conversionRecipe.TryGetLockedOutputPlan(itemId, out _) ? itemId : 0;
        }
        return recipe.SupportsMainOutputLock(itemId) ? itemId : 0;
    }

    /// <summary>
    /// 读取经过配方校验后的分馏塔锁定输出。
    /// </summary>
    public static int GetNormalizedLockedOutput(this FractionatorComponent fractionator, PlanetFactory factory) {
        int lockedItemId = fractionator.GetLockedOutput(factory);
        int normalizedItemId = fractionator.NormalizeLockedOutput(factory, lockedItemId);
        if (normalizedItemId != lockedItemId) {
            fractionator.SetLockedOutput(factory, normalizedItemId);
        }
        return normalizedItemId;
    }

    private static int[] AppendLockedOutputParam(int[] parameters, int lockedItemId) =>
        FractionatorBlueprintParameters.Upsert(parameters, LockedOutputParamMagic, LockedOutputParamVersion,
            lockedItemId);

    private static bool TryReadLockedOutputParam(int[] parameters, out int lockedItemId) =>
        FractionatorBlueprintParameters.TryRead(parameters, LockedOutputParamMagic, LockedOutputParamVersion,
            out lockedItemId);

    private static bool TryGetBlueprintLockedOutput(PlanetFactory factory, int objectId, out int lockedItemId) {
        lockedItemId = 0;
        if (factory == null || objectId == 0) {
            return false;
        }
        if (objectId > 0) {
            if (!TryGetSingleLockFractionator(factory, objectId, out FractionatorComponent fractionator)) {
                return false;
            }
            lockedItemId = fractionator.GetLockedOutput(factory);
            return true;
        }
        int prebuildId = -objectId;
        if (prebuildId <= 0 || prebuildId >= factory.prebuildPool.Length) {
            return false;
        }
        ref PrebuildData prebuild = ref factory.prebuildPool[prebuildId];
        if (prebuild.id != prebuildId || prebuild.protoId is not (IFE交互塔 or IFE转化塔)) {
            return false;
        }
        TryReadLockedOutputParam(prebuild.parameters, out lockedItemId);
        return true;
    }

    private static void ApplyLockedOutputFromParameters(PlanetFactory factory, int entityId, int[] parameters) {
        if (!TryReadLockedOutputParam(parameters, out int lockedItemId)) {
            return;
        }
        if (!TryGetSingleLockFractionator(factory, entityId, out FractionatorComponent fractionator)) {
            return;
        }
        fractionator.SetLockedOutput(factory, fractionator.NormalizeLockedOutput(factory, lockedItemId));
    }

    /// <summary>
    /// 复制建筑设置后保存分馏塔锁定输出到剪贴板状态。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.OnCopyBuildingSetting))]
    public static void PlanetFactory_OnCopyBuildingSetting_Postfix(PlanetFactory __instance, int entityId) {
        if (TryGetSingleLockFractionator(__instance, entityId, out FractionatorComponent fractionator)) {
            hasLockedOutputClipboard = true;
            lockedOutputClipboardItemId = fractionator.GetLockedOutput(__instance);
            return;
        }
        ClearLockedOutputClipboard();
    }

    /// <summary>
    /// 粘贴建筑设置后恢复分馏塔锁定输出。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.OnPasteBuildingSetting))]
    public static void PlanetFactory_OnPasteBuildingSetting_Postfix(PlanetFactory __instance, int entityId) {
        if (!hasLockedOutputClipboard
            || !TryGetSingleLockFractionator(__instance, entityId, out FractionatorComponent fractionator)) {
            return;
        }
        fractionator.SetLockedOutputAndSync(__instance, lockedOutputClipboardItemId, manual: true);
    }

    /// <summary>
    /// 生成蓝图数据后写入分馏塔锁定输出参数。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(BlueprintUtils), nameof(BlueprintUtils.GenerateBlueprintData))]
    public static void BlueprintUtils_GenerateBlueprintData_Postfix(BlueprintData _blueprintData, PlanetData _planet,
        int[] _objIds, int _objCount) {
        if (_blueprintData?.buildings == null || _planet?.factory == null || _objIds == null) {
            return;
        }
        int count = Math.Min(_objCount, Math.Min(_objIds.Length, _blueprintData.buildings.Length));
        for (int i = 0; i < count; i++) {
            if (!TryGetBlueprintLockedOutput(_planet.factory, _objIds[i], out int lockedItemId)) {
                continue;
            }
            BlueprintBuilding building = _blueprintData.buildings[i];
            if (building == null) {
                continue;
            }
            building.parameters = AppendLockedOutputParam(building.parameters, lockedItemId);
        }
    }

    /// <summary>
    /// 实体逻辑组件创建后从预建筑参数恢复分馏塔锁定输出。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.CreateEntityLogicComponents))]
    public static void PlanetFactory_CreateEntityLogicComponents_Postfix(PlanetFactory __instance, int entityId,
        PrefabDesc desc, int prebuildId) {
        if (prebuildId <= 0 || desc == null || !desc.isFractionator || prebuildId >= __instance.prebuildPool.Length) {
            return;
        }
        PrebuildData prebuild = __instance.prebuildPool[prebuildId];
        if (prebuild.id != prebuildId) {
            return;
        }
        ApplyLockedOutputFromParameters(__instance, entityId, prebuild.parameters);
    }

    /// <summary>
    /// 蓝图强制铺设后清理临时锁定输出参数缓存。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildTool_BlueprintPaste), nameof(BuildTool_BlueprintPaste.PasteForceDown))]
    public static void BuildTool_BlueprintPaste_PasteForceDown_Postfix(BuildTool_BlueprintPaste __instance) {
        if (__instance?.factory == null || __instance.bpPool == null) {
            return;
        }
        for (int i = 0; i < __instance.bpCursor; i++) {
            BuildPreview buildPreview = __instance.bpPool[i];
            if (buildPreview == null || buildPreview.coverObjId <= 0 || buildPreview.willReconstructCover) {
                continue;
            }
            ApplyLockedOutputFromParameters(__instance.factory, buildPreview.coverObjId, buildPreview.parameters);
        }
    }

    /// <summary>
    /// 实体删除前清理分馏塔实例级扩展状态。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.RemoveEntityWithComponents))]
    public static void PlanetFactory_RemoveEntityWithComponents_Prefix(PlanetFactory __instance, int id) {
        if (__instance == null || id <= 0) {
            return;
        }
        lockedOutputDic.TryRemove((__instance.planetId, id), out _);
        FractionatorByproductDiscard.Clear(__instance, id);
        AnalysisLineageTarget.ClearLineageTarget(__instance, id);
        FractionatorOutputState.ClearExtraState(__instance, id);
    }

    /// <summary>
    /// 统计当前工厂中已建成的交互塔数量。
    /// </summary>
    public static int CountInteractionTowers() {
        int count = 0;
        if (GameMain.data == null || GameMain.data.factories == null) return 0;
        for (int i = 0; i < GameMain.data.factories.Length; i++) {
            PlanetFactory factory = GameMain.data.factories[i];
            if (factory == null || factory.factorySystem == null) continue;
            for (int j = 1; j < factory.factorySystem.fractionatorCursor; j++) {
                if (factory.factorySystem.fractionatorPool[j].id == j) {
                    int buildingID = factory.entityPool[factory.factorySystem.fractionatorPool[j].entityId].protoId;
                    if (buildingID == IFE交互塔) count++;
                }
            }
        }
        return count;
    }

    #endregion
}
