using System;
using System.Collections.Concurrent;
using System.IO;
using FE.Compatibility.Nebula;
using FE.Logic.Fractionation.FracRecipes;
using HarmonyLib;
using NebulaAPI;
using static FE.Logic.Fractionation.FracRecipes.RecipeManager;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>
/// 精馏塔精华调相方向状态、交互和存档逻辑。
/// </summary>
public static class RectificationTuningTarget {
    private static readonly ConcurrentDictionary<(int, int), int> tuningTargetDic = [];
    private const int TuningTargetParamMagic = 0x54554E45;
    private const int TuningTargetParamVersion = 1;
    private static bool hasTuningTargetClipboard;
    private static int tuningTargetClipboardItemId;

    private static void ClearTuningTargetClipboard() {
        hasTuningTargetClipboard = false;
        tuningTargetClipboardItemId = 0;
    }

    public static void TuningTargetImport(BinaryReader r) {
        tuningTargetDic.Clear();
        int count = r.ReadInt32();
        for (int i = 0; i < count; i++) {
            int planetId = r.ReadInt32();
            int entityId = r.ReadInt32();
            int itemId = r.ReadInt32();
            if (itemId > 0) {
                tuningTargetDic.TryAdd((planetId, entityId), itemId);
            }
        }
    }

    public static void TuningTargetExport(BinaryWriter w) {
        w.Write(tuningTargetDic.Count);
        foreach (var p in tuningTargetDic) {
            w.Write(p.Key.Item1);
            w.Write(p.Key.Item2);
            w.Write(p.Value);
        }
    }

    public static void TuningTargetIntoOtherSave() {
        tuningTargetDic.Clear();
        ClearTuningTargetClipboard();
    }

    public static int GetTuningTarget(this FractionatorComponent fractionator, PlanetFactory factory) {
        int planetId = factory.planetId;
        int entityId = fractionator.entityId;
        return tuningTargetDic.TryGetValue((planetId, entityId), out int itemId) ? itemId : 0;
    }

    public static void SetTuningTarget(this FractionatorComponent fractionator, PlanetFactory factory, int itemId) {
        int planetId = factory.planetId;
        int entityId = fractionator.entityId;
        if (itemId == 0) {
            tuningTargetDic.TryRemove((planetId, entityId), out _);
        } else {
            tuningTargetDic[(planetId, entityId)] = itemId;
        }
    }

    public static int SetTuningTargetAndSync(this FractionatorComponent fractionator, PlanetFactory factory, int itemId,
        bool manual = false) {
        int normalizedItemId = fractionator.NormalizeTuningTarget(factory, itemId);
        fractionator.SetTuningTarget(factory, normalizedItemId);
        if (manual && factory != null && NebulaModAPI.IsMultiplayerActive && !NebulaMultiplayerModAPI.IsOthers()) {
            NebulaModAPI.MultiplayerSession.Network.SendPacket(
                new BuildingChangePacket(IFE精馏塔, 3, factory.planetId, fractionator.entityId, normalizedItemId));
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
        if (entityData.id != entityId || entityData.protoId != IFE精馏塔 || entityData.fractionatorId <= 0) {
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

    public static void ApplyTuningTargetPacket(int planetId, int entityId, int itemId) {
        if (planetId <= 0 || entityId <= 0) {
            return;
        }
        if (itemId == 0) {
            tuningTargetDic.TryRemove((planetId, entityId), out _);
        } else {
            tuningTargetDic[(planetId, entityId)] = itemId;
        }
        PlanetFactory factory = GetFactoryByPlanetId(planetId);
        if (TryGetRectificationFractionator(factory, entityId, out FractionatorComponent fractionator)) {
            fractionator.SetTuningTarget(factory, fractionator.NormalizeTuningTarget(factory, itemId));
        }
    }

    public static int NormalizeTuningTarget(this FractionatorComponent fractionator, PlanetFactory factory,
        int itemId) {
        if (itemId == 0 || factory == null) {
            return 0;
        }
        if (fractionator.fluidId == 0) {
            return itemId;
        }
        RectificationRecipe recipe = GetRecipe<RectificationRecipe>(ERecipe.Rectification, fractionator.fluidId);
        return recipe != null && recipe.SupportsTuningTarget(itemId) ? itemId : 0;
    }

    public static int GetNormalizedTuningTarget(this FractionatorComponent fractionator, PlanetFactory factory) {
        int targetItemId = fractionator.GetTuningTarget(factory);
        int normalizedItemId = fractionator.NormalizeTuningTarget(factory, targetItemId);
        if (normalizedItemId != targetItemId) {
            fractionator.SetTuningTarget(factory, normalizedItemId);
        }
        return normalizedItemId;
    }

    private static int[] AppendTuningTargetParam(int[] parameters, int targetItemId) {
        int[] baseParameters = parameters ?? [];
        if (TryReadTuningTargetParam(baseParameters, out _, out int baseParamCount)) {
            Array.Resize(ref baseParameters, baseParamCount);
        }
        int[] result = new int[baseParameters.Length + 3];
        Array.Copy(baseParameters, result, baseParameters.Length);
        int tailIndex = baseParameters.Length;
        result[tailIndex] = TuningTargetParamMagic;
        result[tailIndex + 1] = TuningTargetParamVersion;
        result[tailIndex + 2] = targetItemId;
        return result;
    }

    private static bool TryReadTuningTargetParam(int[] parameters, out int targetItemId) {
        return TryReadTuningTargetParam(parameters, out targetItemId, out _);
    }

    private static bool TryReadTuningTargetParam(int[] parameters, out int targetItemId, out int baseParamCount) {
        targetItemId = 0;
        baseParamCount = parameters?.Length ?? 0;
        if (parameters == null || parameters.Length < 3) {
            return false;
        }
        int tailIndex = parameters.Length - 3;
        if (parameters[tailIndex] != TuningTargetParamMagic || parameters[tailIndex + 1] != TuningTargetParamVersion) {
            return false;
        }
        targetItemId = parameters[tailIndex + 2];
        baseParamCount = tailIndex;
        return true;
    }

    private static bool TryGetBlueprintTuningTarget(PlanetFactory factory, int objectId, out int targetItemId) {
        targetItemId = 0;
        if (factory == null || objectId == 0) {
            return false;
        }
        if (objectId > 0) {
            if (!TryGetRectificationFractionator(factory, objectId, out FractionatorComponent fractionator)) {
                return false;
            }
            targetItemId = fractionator.GetTuningTarget(factory);
            return true;
        }
        int prebuildId = -objectId;
        if (prebuildId <= 0 || prebuildId >= factory.prebuildPool.Length) {
            return false;
        }
        ref PrebuildData prebuild = ref factory.prebuildPool[prebuildId];
        if (prebuild.id != prebuildId || prebuild.protoId != IFE精馏塔) {
            return false;
        }
        TryReadTuningTargetParam(prebuild.parameters, out targetItemId);
        return true;
    }

    private static void ApplyTuningTargetFromParameters(PlanetFactory factory, int entityId, int[] parameters) {
        if (!TryReadTuningTargetParam(parameters, out int targetItemId)) {
            return;
        }
        if (!TryGetRectificationFractionator(factory, entityId, out FractionatorComponent fractionator)) {
            return;
        }
        fractionator.SetTuningTarget(factory, fractionator.NormalizeTuningTarget(factory, targetItemId));
    }

    public static void ClearTuningTarget(PlanetFactory factory, int entityId) {
        if (factory == null || entityId <= 0) {
            return;
        }
        tuningTargetDic.TryRemove((factory.planetId, entityId), out _);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.OnCopyBuildingSetting))]
    public static void PlanetFactory_OnCopyBuildingSetting_Postfix(PlanetFactory __instance, int entityId) {
        if (TryGetRectificationFractionator(__instance, entityId, out FractionatorComponent fractionator)) {
            hasTuningTargetClipboard = true;
            tuningTargetClipboardItemId = fractionator.GetTuningTarget(__instance);
            return;
        }
        ClearTuningTargetClipboard();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.OnPasteBuildingSetting))]
    public static void PlanetFactory_OnPasteBuildingSetting_Postfix(PlanetFactory __instance, int entityId) {
        if (!hasTuningTargetClipboard
            || !TryGetRectificationFractionator(__instance, entityId, out FractionatorComponent fractionator)) {
            return;
        }
        fractionator.SetTuningTargetAndSync(__instance, tuningTargetClipboardItemId, manual: true);
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
            if (!TryGetBlueprintTuningTarget(_planet.factory, _objIds[i], out int targetItemId)) {
                continue;
            }
            BlueprintBuilding building = _blueprintData.buildings[i];
            if (building == null) {
                continue;
            }
            building.parameters = AppendTuningTargetParam(building.parameters, targetItemId);
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
        ApplyTuningTargetFromParameters(__instance, entityId, prebuild.parameters);
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
            ApplyTuningTargetFromParameters(__instance.factory, buildPreview.coverObjId, buildPreview.parameters);
        }
    }
}
