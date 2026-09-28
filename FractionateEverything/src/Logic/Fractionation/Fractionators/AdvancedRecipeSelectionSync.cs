using System;
using FE.Compatibility.Nebula;
using HarmonyLib;
using NebulaAPI;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>
/// 高级塔选定配方的复制粘贴、蓝图参数和联机命令入口。
/// </summary>
public static class AdvancedRecipeSelectionSync {
    private const int ParameterMagic = 0x46524543;
    private const int ParameterVersion = 1;
    private static bool hasClipboard;
    private static int clipboardRecipeId;

    /// <summary>
    /// 设置选定配方，并在本地手动操作时发送 Nebula 建筑变更包。
    /// </summary>
    public static bool SetSelectedRecipeAndSync(PlanetFactory factory, int entityId, int recipeId,
        bool manual = false) {
        if (!TryGetAdvancedFractionator(factory, entityId, out FractionatorComponent fractionator)) {
            return false;
        }
        if (!AdvancedFractionatorRuntime.SetSelectedRecipe(factory, fractionator.id, recipeId)) {
            return false;
        }
        if (manual && NebulaModAPI.IsMultiplayerActive && !NebulaMultiplayerModAPI.IsOthers()) {
            int buildingId = factory.entityPool[entityId].protoId;
            NebulaModAPI.MultiplayerSession.Network.SendPacket(
                new BuildingChangePacket(buildingId, 2, factory.planetId, entityId, recipeId));
        }
        return true;
    }

    /// <summary>
    /// 应用收到的配方选择；只有通过当前建筑与研究校验时返回 <see langword="true"/>。
    /// </summary>
    public static bool ApplyPacket(int planetId, int entityId, int recipeId) {
        PlanetFactory factory = GetFactory(planetId);
        return factory != null && SetSelectedRecipeAndSync(factory, entityId, recipeId);
    }

    /// <summary>
    /// 复制建筑设置时记录高级塔配方。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.OnCopyBuildingSetting))]
    private static void PlanetFactory_OnCopyBuildingSetting_Postfix(PlanetFactory __instance, int entityId) {
        if (!TryGetAdvancedFractionator(__instance, entityId, out FractionatorComponent fractionator)) {
            hasClipboard = false;
            clipboardRecipeId = 0;
            return;
        }
        AdvancedFractionatorState state = AdvancedFractionatorRuntime.GetState(__instance, fractionator.id);
        hasClipboard = true;
        clipboardRecipeId = state?.SelectedRecipeId ?? 0;
    }

    /// <summary>
    /// 粘贴建筑设置时恢复高级塔配方。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.OnPasteBuildingSetting))]
    private static void PlanetFactory_OnPasteBuildingSetting_Postfix(PlanetFactory __instance, int entityId) {
        if (hasClipboard) {
            SetSelectedRecipeAndSync(__instance, entityId, clipboardRecipeId, manual: true);
        }
    }

    /// <summary>
    /// 生成蓝图时附加高级塔稳定配方 ID。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(BlueprintUtils), nameof(BlueprintUtils.GenerateBlueprintData))]
    private static void BlueprintUtils_GenerateBlueprintData_Postfix(BlueprintData _blueprintData,
        PlanetData _planet, int[] _objIds, int _objCount) {
        if (_blueprintData?.buildings == null || _planet?.factory == null || _objIds == null) {
            return;
        }
        int count = Math.Min(_objCount, Math.Min(_objIds.Length, _blueprintData.buildings.Length));
        for (int i = 0; i < count; i++) {
            if (!TryGetBlueprintRecipe(_planet.factory, _objIds[i], out int recipeId)
                || _blueprintData.buildings[i] is not BlueprintBuilding building) {
                continue;
            }
            building.parameters = FractionatorBlueprintParameters.Upsert(
                building.parameters, ParameterMagic, ParameterVersion, recipeId);
        }
    }

    /// <summary>
    /// 实体创建后从预建筑参数恢复选定配方。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.CreateEntityLogicComponents))]
    private static void PlanetFactory_CreateEntityLogicComponents_Postfix(PlanetFactory __instance,
        int entityId, PrefabDesc desc, int prebuildId) {
        if (prebuildId <= 0 || desc?.isFractionator != true
            || prebuildId >= __instance.prebuildPool.Length) {
            return;
        }
        ApplyParameters(__instance, entityId, __instance.prebuildPool[prebuildId].parameters);
    }

    /// <summary>
    /// 蓝图强制覆盖已有实体时恢复选定配方。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildTool_BlueprintPaste), nameof(BuildTool_BlueprintPaste.PasteForceDown))]
    private static void BuildTool_BlueprintPaste_PasteForceDown_Postfix(BuildTool_BlueprintPaste __instance) {
        if (__instance?.factory == null || __instance.bpPool == null) {
            return;
        }
        for (int i = 0; i < __instance.bpCursor; i++) {
            BuildPreview preview = __instance.bpPool[i];
            if (preview != null && preview.coverObjId > 0 && !preview.willReconstructCover) {
                ApplyParameters(__instance.factory, preview.coverObjId, preview.parameters);
            }
        }
    }

    private static bool TryGetBlueprintRecipe(PlanetFactory factory, int objectId, out int recipeId) {
        recipeId = 0;
        if (objectId > 0) {
            if (!TryGetAdvancedFractionator(factory, objectId, out FractionatorComponent fractionator)) {
                return false;
            }
            recipeId = AdvancedFractionatorRuntime.GetState(factory, fractionator.id)?.SelectedRecipeId ?? 0;
            return true;
        }
        int prebuildId = -objectId;
        if (factory == null || prebuildId <= 0 || prebuildId >= factory.prebuildPool.Length) {
            return false;
        }
        ref PrebuildData prebuild = ref factory.prebuildPool[prebuildId];
        return prebuild.id == prebuildId
               && FractionatorTowerCatalog.IsAdvancedTower(prebuild.protoId)
               && FractionatorBlueprintParameters.TryRead(
                   prebuild.parameters, ParameterMagic, ParameterVersion, out recipeId);
    }

    private static void ApplyParameters(PlanetFactory factory, int entityId, int[] parameters) {
        if (FractionatorBlueprintParameters.TryRead(
                parameters, ParameterMagic, ParameterVersion, out int recipeId)) {
            SetSelectedRecipeAndSync(factory, entityId, recipeId);
        }
    }

    private static bool TryGetAdvancedFractionator(PlanetFactory factory, int entityId,
        out FractionatorComponent fractionator) {
        fractionator = default;
        if (factory == null || entityId <= 0 || entityId >= factory.entityPool.Length) {
            return false;
        }
        ref EntityData entity = ref factory.entityPool[entityId];
        if (entity.id != entityId || entity.fractionatorId <= 0
            || !FractionatorTowerCatalog.IsAdvancedTower(entity.protoId)) {
            return false;
        }
        fractionator = factory.factorySystem.fractionatorPool[entity.fractionatorId];
        return fractionator.id == entity.fractionatorId;
    }

    private static PlanetFactory GetFactory(int planetId) {
        PlanetData planet = GameMain.data?.galaxy?.PlanetById(planetId);
        if (planet?.factory != null) {
            return planet.factory;
        }
        int index = planet?.factoryIndex ?? -1;
        return GameMain.data?.factories != null && index >= 0 && index < GameMain.data.factories.Length
            ? GameMain.data.factories[index]
            : null;
    }
}
