using System;
using System.Collections.Generic;
using HarmonyLib;

namespace FE.Logic.Station;

/// <summary>
/// 通过 vanilla 建筑参数通道保存物流交互站的 FE 槽位模式。
/// 覆盖蓝图、Q 键复制建造和复制/粘贴设置，旧 2048 长度参数自然兼容。
/// </summary>
public static partial class StationManager {
    private const int StationBaseParameterLength = 2048;
    private const int InteractionStationParamMagic = 0x46455354;
    private const int InteractionStationParamVersion = 1;
    private const int InteractionStationParamHeaderLength = 3;
    private const int InteractionStationParamValuesPerSlot = 2;
    private const int InteractionStationParamMaxSlotCount = 32;

    private static bool IsInteractionStation(PlanetFactory factory, int entityId, out StationComponent station) {
        station = null;
        if (factory == null
            || entityId <= 0
            || entityId >= factory.entityPool.Length
            || factory.entityPool[entityId].id != entityId) {
            return false;
        }

        int stationId = factory.entityPool[entityId].stationId;
        if (stationId <= 0
            || factory.transport == null
            || stationId >= factory.transport.stationPool.Length) {
            return false;
        }

        station = factory.transport.stationPool[stationId];
        return station != null
               && station.id == stationId
               && station.entityId == entityId
               && IsInteractionStation(factory.entityPool[entityId].protoId);
    }

    private static int[] AppendInteractionStationParams(int[] parameters, StationComponent station) {
        int[] baseParameters = TrimInteractionStationParams(parameters);
        if (station?.storage == null) {
            return baseParameters;
        }

        int slotCount = Math.Min(station.storage.Length, InteractionStationParamMaxSlotCount);
        int extensionLength = InteractionStationParamHeaderLength + slotCount * 2;
        int[] result = new int[Math.Max(StationBaseParameterLength, baseParameters.Length) + extensionLength];
        Array.Copy(baseParameters, result, Math.Min(baseParameters.Length, result.Length));

        int tailIndex = Math.Max(StationBaseParameterLength, baseParameters.Length);
        result[tailIndex] = InteractionStationParamMagic;
        result[tailIndex + 1] = InteractionStationParamVersion;
        result[tailIndex + 2] = slotCount;
        for (int i = 0; i < slotCount; i++) {
            TryGetSlotModes(station.entityId, i, out int transferMode, out int capacityMode);
            result[tailIndex + InteractionStationParamHeaderLength + i * InteractionStationParamValuesPerSlot] =
                transferMode;
            result[tailIndex + InteractionStationParamHeaderLength + i * InteractionStationParamValuesPerSlot + 1] =
                capacityMode;
        }
        return result;
    }

    private static int[] TrimInteractionStationParams(int[] parameters) {
        if (!TryGetInteractionStationParamOffset(parameters, out int offset, out _)) {
            return parameters ?? [];
        }

        int[] result = new int[offset];
        Array.Copy(parameters, result, offset);
        return result;
    }

    private static bool TryGetInteractionStationParamOffset(int[] parameters, out int offset, out int slotCount) {
        offset = 0;
        slotCount = 0;
        if (parameters == null || parameters.Length < StationBaseParameterLength + InteractionStationParamHeaderLength) {
            return false;
        }

        for (int i = StationBaseParameterLength;
             i <= parameters.Length - InteractionStationParamHeaderLength;
             i++) {
            if (parameters[i] != InteractionStationParamMagic
                || parameters[i + 1] != InteractionStationParamVersion) {
                continue;
            }

            int candidateSlotCount = parameters[i + 2];
            if (candidateSlotCount < 0 || candidateSlotCount > InteractionStationParamMaxSlotCount) {
                continue;
            }

            int expectedLength =
                i + InteractionStationParamHeaderLength + candidateSlotCount * InteractionStationParamValuesPerSlot;
            if (expectedLength > parameters.Length) {
                continue;
            }

            offset = i;
            slotCount = candidateSlotCount;
            return true;
        }
        return false;
    }

    private static void ApplyInteractionStationParams(PlanetFactory factory, int entityId, int[] parameters) {
        if (!IsInteractionStation(factory, entityId, out StationComponent station)
            || !TryGetInteractionStationParamOffset(parameters, out int offset, out int slotCount)) {
            return;
        }

        int maxSlotCount = Math.Min(slotCount, station.storage?.Length ?? 0);
        for (int i = 0; i < maxSlotCount; i++) {
            int valueIndex = offset + InteractionStationParamHeaderLength + i * InteractionStationParamValuesPerSlot;
            SetSlotModes(station.entityId, i, parameters[valueIndex], parameters[valueIndex + 1]);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildingParameters), nameof(BuildingParameters.CopyFromFactoryObject))]
    public static void BuildingParameters_CopyFromFactoryObject_StationParams_Postfix(ref BuildingParameters __instance,
        int objectId, PlanetFactory factory, bool __result) {
        if (!__result || objectId <= 0 || !IsInteractionStation(factory, objectId, out StationComponent station)) {
            return;
        }
        __instance.parameters = AppendInteractionStationParams(__instance.parameters, station);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BlueprintUtils), nameof(BlueprintUtils.GenerateBlueprintData))]
    public static void BlueprintUtils_GenerateBlueprintData_StationParams_Postfix(BlueprintData _blueprintData,
        PlanetData _planet, int[] _objIds, int _objCount) {
        if (_blueprintData?.buildings == null || _planet?.factory == null || _objIds == null) {
            return;
        }

        int count = Math.Min(_objCount, Math.Min(_objIds.Length, _blueprintData.buildings.Length));
        for (int i = 0; i < count; i++) {
            if (!IsInteractionStation(_planet.factory, _objIds[i], out StationComponent station)) {
                continue;
            }

            BlueprintBuilding building = _blueprintData.buildings[i];
            if (building == null) {
                continue;
            }
            building.parameters = AppendInteractionStationParams(building.parameters, station);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildingParameters), nameof(BuildingParameters.GenerateBuildPreviews))]
    public static void BuildingParameters_GenerateBuildPreviews_StationParams_Postfix(List<BuildPreview> bplist) {
        if (BuildingParameters.template.type != BuildingType.Station
            || bplist == null
            || bplist.Count == 0
            || !TryGetInteractionStationParamOffset(BuildingParameters.template.parameters, out _, out _)) {
            return;
        }

        foreach (BuildPreview buildPreview in bplist) {
            if (buildPreview?.desc != null && buildPreview.desc.isStation) {
                buildPreview.parameters = BuildingParameters.template.parameters;
                buildPreview.paramCount = buildPreview.parameters.Length;
                return;
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildingParameters), nameof(BuildingParameters.ApplyPrebuildParametersToEntity))]
    public static void BuildingParameters_ApplyPrebuildParametersToEntity_StationParams_Postfix(int entityId,
        int[] parameters, PlanetFactory factory) {
        ApplyInteractionStationParams(factory, entityId, parameters);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildingParameters), nameof(BuildingParameters.PasteToFactoryObject))]
    public static void BuildingParameters_PasteToFactoryObject_StationParams_Postfix(BuildingParameters __instance,
        int objectId, PlanetFactory factory, bool __result) {
        if (__result && objectId > 0) {
            ApplyInteractionStationParams(factory, objectId, __instance.parameters);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildTool_BlueprintPaste), nameof(BuildTool_BlueprintPaste.PasteForceDown))]
    public static void BuildTool_BlueprintPaste_PasteForceDown_StationParams_Postfix(
        BuildTool_BlueprintPaste __instance) {
        if (__instance?.factory == null || __instance.bpPool == null) {
            return;
        }

        for (int i = 0; i < __instance.bpCursor; i++) {
            BuildPreview buildPreview = __instance.bpPool[i];
            if (buildPreview?.coverObjId > 0) {
                ApplyInteractionStationParams(__instance.factory, buildPreview.coverObjId, buildPreview.parameters);
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.RemoveEntityWithComponents))]
    public static void PlanetFactory_RemoveEntityWithComponents_StationParams_Prefix(PlanetFactory __instance, int id) {
        if (IsInteractionStation(__instance, id, out StationComponent station)) {
            RemoveSlotModes(station.entityId);
        }
    }
}
