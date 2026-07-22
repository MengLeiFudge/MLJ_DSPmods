using System;
using System.Collections.Concurrent;
using System.IO;
using FE.Compatibility.Nebula;
using FE.Logic.Fractionation.FracRecipes;
using HarmonyLib;
using NebulaAPI;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>
/// 保存单座分馏塔的副产物弃置开关，并同步复制粘贴、蓝图和联机状态。
/// </summary>
public static class FractionatorByproductDiscard {
    private static readonly ConcurrentDictionary<(int PlanetId, int EntityId), byte> states = [];
    private const int ParameterMagic = 0x44534344;
    private const int ParameterVersion = 1;
    private static bool hasClipboard;
    private static bool clipboardValue;

    public static void Import(BinaryReader reader) {
        states.Clear();
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++) {
            int planetId = reader.ReadInt32();
            int entityId = reader.ReadInt32();
            if (planetId > 0 && entityId > 0) {
                states[(planetId, entityId)] = 1;
            }
        }
    }

    public static void Export(BinaryWriter writer) {
        writer.Write(states.Count);
        foreach ((int PlanetId, int EntityId) key in states.Keys) {
            writer.Write(key.PlanetId);
            writer.Write(key.EntityId);
        }
    }

    public static void IntoOtherSave() {
        states.Clear();
        hasClipboard = false;
        clipboardValue = false;
    }

    public static bool GetByproductDiscard(this FractionatorComponent fractionator, PlanetFactory factory) {
        return factory != null && states.ContainsKey((factory.planetId, fractionator.entityId));
    }

    public static void SetByproductDiscard(this FractionatorComponent fractionator, PlanetFactory factory,
        bool enabled) {
        if (factory == null) {
            return;
        }
        (int, int) key = (factory.planetId, fractionator.entityId);
        if (enabled) {
            states[key] = 1;
        } else {
            states.TryRemove(key, out _);
        }
    }

    public static bool SetByproductDiscardAndSync(this FractionatorComponent fractionator, PlanetFactory factory,
        bool enabled, bool manual = false) {
        bool normalized = fractionator.NormalizeByproductDiscard(factory, enabled);
        fractionator.SetByproductDiscard(factory, normalized);
        if (manual && factory != null && NebulaModAPI.IsMultiplayerActive && !NebulaMultiplayerModAPI.IsOthers()) {
            int buildingId = factory.entityPool[fractionator.entityId].protoId;
            NebulaModAPI.MultiplayerSession.Network.SendPacket(
                new BuildingChangePacket(buildingId, 4, factory.planetId, fractionator.entityId, normalized ? 1 : 0));
        }
        return normalized;
    }

    public static bool NormalizeByproductDiscard(this FractionatorComponent fractionator, PlanetFactory factory,
        bool enabled) {
        if (!enabled || factory == null || fractionator.entityId <= 0
            || fractionator.entityId >= factory.entityPool.Length) {
            return false;
        }

        ERecipe recipeType = FractionatorTowerCatalog.GetRecipeType(factory.entityPool[fractionator.entityId].protoId);
        if (recipeType == (ERecipe)0 || !TowerRuntimeModifierCache.IsByproductDiscardEnabled(recipeType)) {
            return false;
        }
        // 实例开关独立于当前配方保存；校准和副产物存在性只决定运行时是否产生效果。
        return true;
    }

    public static bool GetNormalizedByproductDiscard(this FractionatorComponent fractionator,
        PlanetFactory factory) {
        bool enabled = fractionator.GetByproductDiscard(factory);
        bool normalized = fractionator.NormalizeByproductDiscard(factory, enabled);
        if (normalized != enabled) {
            fractionator.SetByproductDiscard(factory, normalized);
        }
        return normalized;
    }

    public static void ApplyPacket(int planetId, int entityId, bool enabled) {
        if (planetId <= 0 || entityId <= 0) {
            return;
        }
        (int, int) key = (planetId, entityId);
        if (enabled) {
            states[key] = 1;
        } else {
            states.TryRemove(key, out _);
        }
        PlanetFactory factory = GetFactory(planetId);
        if (TryGetFractionator(factory, entityId, out FractionatorComponent fractionator)) {
            fractionator.SetByproductDiscard(factory, fractionator.NormalizeByproductDiscard(factory, enabled));
        }
    }

    public static void Clear(PlanetFactory factory, int entityId) {
        if (factory != null && entityId > 0) {
            states.TryRemove((factory.planetId, entityId), out _);
        }
    }

    private static bool TryGetFractionator(PlanetFactory factory, int entityId,
        out FractionatorComponent fractionator) {
        fractionator = default;
        if (factory == null || entityId <= 0 || entityId >= factory.entityPool.Length) {
            return false;
        }
        EntityData entity = factory.entityPool[entityId];
        if (entity.id != entityId || FractionatorTowerCatalog.GetRecipeType(entity.protoId) == (ERecipe)0
            || entity.fractionatorId <= 0) {
            return false;
        }
        fractionator = factory.factorySystem.fractionatorPool[entity.fractionatorId];
        return fractionator.id == entity.fractionatorId;
    }

    private static PlanetFactory GetFactory(int planetId) {
        GameData gameData = GameMain.data;
        PlanetData planet = gameData?.galaxy?.PlanetById(planetId);
        if (planet?.factory != null) {
            return planet.factory;
        }
        int index = planet?.factoryIndex ?? -1;
        return gameData?.factories != null && index >= 0 && index < gameData.factories.Length
            ? gameData.factories[index]
            : null;
    }

    private static int[] AppendParameter(int[] parameters, bool enabled) =>
        FractionatorBlueprintParameters.Upsert(parameters, ParameterMagic, ParameterVersion, enabled ? 1 : 0);

    private static bool TryReadParameter(int[] parameters, out bool enabled) {
        bool found = FractionatorBlueprintParameters.TryRead(parameters, ParameterMagic, ParameterVersion,
            out int value);
        enabled = found && value != 0;
        return found;
    }

    private static bool TryGetBlueprintValue(PlanetFactory factory, int objectId, out bool enabled) {
        enabled = false;
        if (factory == null || objectId == 0) {
            return false;
        }
        if (objectId > 0) {
            if (!TryGetFractionator(factory, objectId, out FractionatorComponent fractionator)) {
                return false;
            }
            enabled = fractionator.GetByproductDiscard(factory);
            return true;
        }
        int prebuildId = -objectId;
        if (prebuildId <= 0 || prebuildId >= factory.prebuildPool.Length) {
            return false;
        }
        ref PrebuildData prebuild = ref factory.prebuildPool[prebuildId];
        return prebuild.id == prebuildId && FractionatorTowerCatalog.GetRecipeType(prebuild.protoId) != (ERecipe)0
               && TryReadParameter(prebuild.parameters, out enabled);
    }

    private static void ApplyFromParameters(PlanetFactory factory, int entityId, int[] parameters) {
        if (!TryReadParameter(parameters, out bool enabled)
            || !TryGetFractionator(factory, entityId, out FractionatorComponent fractionator)) {
            return;
        }
        fractionator.SetByproductDiscard(factory, fractionator.NormalizeByproductDiscard(factory, enabled));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.OnCopyBuildingSetting))]
    public static void PlanetFactory_OnCopyBuildingSetting_Postfix(PlanetFactory __instance, int entityId) {
        if (TryGetFractionator(__instance, entityId, out FractionatorComponent fractionator)) {
            hasClipboard = true;
            clipboardValue = fractionator.GetByproductDiscard(__instance);
        } else {
            hasClipboard = false;
            clipboardValue = false;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.OnPasteBuildingSetting))]
    public static void PlanetFactory_OnPasteBuildingSetting_Postfix(PlanetFactory __instance, int entityId) {
        if (hasClipboard && TryGetFractionator(__instance, entityId, out FractionatorComponent fractionator)) {
            fractionator.SetByproductDiscardAndSync(__instance, clipboardValue, manual: true);
        }
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
            if (TryGetBlueprintValue(_planet.factory, _objIds[i], out bool enabled)
                && _blueprintData.buildings[i] is BlueprintBuilding building) {
                building.parameters = AppendParameter(building.parameters, enabled);
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.CreateEntityLogicComponents))]
    public static void PlanetFactory_CreateEntityLogicComponents_Postfix(PlanetFactory __instance, int entityId,
        PrefabDesc desc, int prebuildId) {
        if (prebuildId > 0 && desc?.isFractionator == true && prebuildId < __instance.prebuildPool.Length) {
            ApplyFromParameters(__instance, entityId, __instance.prebuildPool[prebuildId].parameters);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildTool_BlueprintPaste), nameof(BuildTool_BlueprintPaste.PasteForceDown))]
    public static void BuildTool_BlueprintPaste_PasteForceDown_Postfix(BuildTool_BlueprintPaste __instance) {
        if (__instance?.factory == null || __instance.bpPool == null) {
            return;
        }
        for (int i = 0; i < __instance.bpCursor; i++) {
            BuildPreview preview = __instance.bpPool[i];
            if (preview != null && preview.coverObjId > 0 && !preview.willReconstructCover) {
                ApplyFromParameters(__instance.factory, preview.coverObjId, preview.parameters);
            }
        }
    }
}
