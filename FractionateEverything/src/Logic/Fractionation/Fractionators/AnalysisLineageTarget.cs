using System;
using System.Collections.Concurrent;
using System.IO;
using FE.Compatibility.Nebula;
using FE.Logic.Buildings.Migration;
using FE.Logic.Fractionation.FracRecipes;
using HarmonyLib;
using NebulaAPI;
using static FE.Logic.Fractionation.FracRecipes.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>
/// 保存解析塔谱系分化目标，并同步实体、复制粘贴、蓝图和联机状态。
/// </summary>
public static class AnalysisLineageTarget {
    private static readonly ConcurrentDictionary<(int, int), int> lineageTargetDic = [];
    private const int LineageTargetParamMagic = 0x54554E45;
    private const int LineageTargetParamVersion = 1;
    private static bool hasLineageTargetClipboard;
    private static int lineageTargetClipboardItemId;

    private static void ClearLineageTargetClipboard() {
        hasLineageTargetClipboard = false;
        lineageTargetClipboardItemId = 0;
    }

    public static void LineageTargetImport(BinaryReader r) {
        lineageTargetDic.Clear();
        int count = r.ReadInt32();
        for (int i = 0; i < count; i++) {
            int planetId = r.ReadInt32();
            int entityId = r.ReadInt32();
            int itemId = LegacyProtoMigration.MapItemId(r.ReadInt32());
            if (itemId > 0) {
                lineageTargetDic.TryAdd((planetId, entityId), itemId);
            }
        }
    }

    public static void LineageTargetExport(BinaryWriter w) {
        w.Write(lineageTargetDic.Count);
        foreach (var p in lineageTargetDic) {
            w.Write(p.Key.Item1);
            w.Write(p.Key.Item2);
            w.Write(p.Value);
        }
    }

    public static void LineageTargetIntoOtherSave() {
        lineageTargetDic.Clear();
        ClearLineageTargetClipboard();
    }

    public static int GetLineageTarget(this FractionatorComponent fractionator, PlanetFactory factory) {
        int planetId = factory.planetId;
        int entityId = fractionator.entityId;
        return lineageTargetDic.TryGetValue((planetId, entityId), out int itemId) ? itemId : 0;
    }

    public static void SetLineageTarget(this FractionatorComponent fractionator, PlanetFactory factory, int itemId) {
        int planetId = factory.planetId;
        int entityId = fractionator.entityId;
        if (itemId == 0) {
            lineageTargetDic.TryRemove((planetId, entityId), out _);
        } else {
            lineageTargetDic[(planetId, entityId)] = itemId;
        }
    }

    public static int SetLineageTargetAndSync(this FractionatorComponent fractionator, PlanetFactory factory, int itemId,
        bool manual = false) {
        int normalizedItemId = fractionator.NormalizeLineageTarget(factory, itemId);
        fractionator.SetLineageTarget(factory, normalizedItemId);
        if (manual && factory != null && NebulaModAPI.IsMultiplayerActive && !NebulaMultiplayerModAPI.IsOthers()) {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(
                new BuildingChangePacket(IFE解析塔, 3, factory.planetId, fractionator.entityId, normalizedItemId));
        }
        return normalizedItemId;
    }

    private static bool TryGetRectificationFractionator(PlanetFactory factory, int entityId,
        out FractionatorComponent fractionator) {
        fractionator = default;
        if (factory == null || entityId <= 0 || entityId >= factory.entityPool.Length) {
            return false;
        }
        EntityData entityData = factory.entityPool[entityId];
        if (entityData.id != entityId || entityData.protoId != IFE解析塔 || entityData.fractionatorId <= 0) {
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

    public static void ApplyLineageTargetPacket(int planetId, int entityId, int itemId) {
        if (planetId <= 0 || entityId <= 0) {
            return;
        }
        if (itemId == 0) {
            lineageTargetDic.TryRemove((planetId, entityId), out _);
        } else {
            lineageTargetDic[(planetId, entityId)] = itemId;
        }
        PlanetFactory factory = GetFactoryByPlanetId(planetId);
        if (TryGetRectificationFractionator(factory, entityId, out FractionatorComponent fractionator)) {
            fractionator.SetLineageTarget(factory, fractionator.NormalizeLineageTarget(factory, itemId));
        }
    }

    public static int NormalizeLineageTarget(this FractionatorComponent fractionator, PlanetFactory factory,
        int itemId) {
        if (itemId == 0 || factory == null
            || !TowerRuntimeModifierCache.IsMainOutputLockEnabled(ERecipe.Rectification)) {
            return 0;
        }
        if (fractionator.fluidId == 0) {
            return itemId;
        }
        RectificationRecipe recipe = GetRecipe<RectificationRecipe>(ERecipe.Rectification, fractionator.fluidId);
        return recipe != null && recipe.IsMainOutputLockCalibrated && recipe.SupportsLineageTarget(itemId)
            ? itemId
            : 0;
    }

    public static int GetNormalizedLineageTarget(this FractionatorComponent fractionator, PlanetFactory factory) {
        int targetItemId = fractionator.GetLineageTarget(factory);
        int normalizedItemId = fractionator.NormalizeLineageTarget(factory, targetItemId);
        if (normalizedItemId != targetItemId) {
            fractionator.SetLineageTarget(factory, normalizedItemId);
        }
        return normalizedItemId;
    }

    private static int[] AppendLineageTargetParam(int[] parameters, int targetItemId) =>
        FractionatorBlueprintParameters.Upsert(parameters, LineageTargetParamMagic, LineageTargetParamVersion,
            targetItemId);

    private static bool TryReadLineageTargetParam(int[] parameters, out int targetItemId) =>
        FractionatorBlueprintParameters.TryRead(parameters, LineageTargetParamMagic, LineageTargetParamVersion,
            out targetItemId);

    private static bool TryGetBlueprintLineageTarget(PlanetFactory factory, int objectId, out int targetItemId) {
        targetItemId = 0;
        if (factory == null || objectId == 0) {
            return false;
        }
        if (objectId > 0) {
            if (!TryGetRectificationFractionator(factory, objectId, out FractionatorComponent fractionator)) {
                return false;
            }
            targetItemId = fractionator.GetLineageTarget(factory);
            return true;
        }
        int prebuildId = -objectId;
        if (prebuildId <= 0 || prebuildId >= factory.prebuildPool.Length) {
            return false;
        }
        ref PrebuildData prebuild = ref factory.prebuildPool[prebuildId];
        if (prebuild.id != prebuildId || prebuild.protoId != IFE解析塔) {
            return false;
        }
        TryReadLineageTargetParam(prebuild.parameters, out targetItemId);
        return true;
    }

    private static void ApplyLineageTargetFromParameters(PlanetFactory factory, int entityId, int[] parameters) {
        if (!TryReadLineageTargetParam(parameters, out int targetItemId)) {
            return;
        }
        if (!TryGetRectificationFractionator(factory, entityId, out FractionatorComponent fractionator)) {
            return;
        }
        fractionator.SetLineageTarget(factory, fractionator.NormalizeLineageTarget(factory, targetItemId));
    }

    public static void ClearLineageTarget(PlanetFactory factory, int entityId) {
        if (factory == null || entityId <= 0) {
            return;
        }
        lineageTargetDic.TryRemove((factory.planetId, entityId), out _);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.OnCopyBuildingSetting))]
    public static void PlanetFactory_OnCopyBuildingSetting_Postfix(PlanetFactory __instance, int entityId) {
        if (TryGetRectificationFractionator(__instance, entityId, out FractionatorComponent fractionator)) {
            hasLineageTargetClipboard = true;
            lineageTargetClipboardItemId = fractionator.GetLineageTarget(__instance);
            return;
        }
        ClearLineageTargetClipboard();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.OnPasteBuildingSetting))]
    public static void PlanetFactory_OnPasteBuildingSetting_Postfix(PlanetFactory __instance, int entityId) {
        if (!hasLineageTargetClipboard
            || !TryGetRectificationFractionator(__instance, entityId, out FractionatorComponent fractionator)) {
            return;
        }
        fractionator.SetLineageTargetAndSync(__instance, lineageTargetClipboardItemId, manual: true);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BlueprintUtils), nameof(BlueprintUtils.GenerateBlueprintData))]
    public static void BlueprintUtils_GenerateBlueprintData_Postfix(BlueprintData _blueprintData, PlanetData _planet,
        int[] _objIds, int _objCount) {
        if (_blueprintData?.buildings == null || _planet?.factory == null || _objIds == null) {
            return;
        }
        int count = Math.Min(_objCount, Math.Min(_objIds.Length, _blueprintData.buildings.Length));
        for (int i = 0; i < count; i++) {
            if (!TryGetBlueprintLineageTarget(_planet.factory, _objIds[i], out int targetItemId)) {
                continue;
            }
            BlueprintBuilding building = _blueprintData.buildings[i];
            if (building == null) {
                continue;
            }
            building.parameters = AppendLineageTargetParam(building.parameters, targetItemId);
        }
    }

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
        ApplyLineageTargetFromParameters(__instance, entityId, prebuild.parameters);
    }

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
            ApplyLineageTargetFromParameters(__instance.factory, buildPreview.coverObjId, buildPreview.parameters);
        }
    }
}
