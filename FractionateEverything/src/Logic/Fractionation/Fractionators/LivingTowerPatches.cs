using HarmonyLib;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>
/// 高级生命塔的耐久与拆除边界：无人机不维修，拆除返还缓存，损毁不返还。
/// </summary>
public static class LivingTowerPatches {
    /// <summary>
    /// 从无人机候选评分中排除高级塔。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ConstructionSystem), nameof(ConstructionSystem.GetRepairValue))]
    private static bool ConstructionSystem_GetRepairValue_Prefix(ConstructionSystem __instance,
        ref ConstructStat constructStat, ref float __result) {
        if (!IsAdvancedTowerEntity(__instance.factory, constructStat.entityId)) {
            return true;
        }
        __result = float.MinValue;
        return false;
    }

    /// <summary>
    /// 禁止高级塔申请维修无人机。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ConstructionSystem), nameof(ConstructionSystem.GetRepairDroneDemand))]
    private static bool ConstructionSystem_GetRepairDroneDemand_Prefix(ConstructionSystem __instance,
        ref ConstructStat constructStat, ref float __result) {
        if (!IsAdvancedTowerEntity(__instance.factory, constructStat.entityId)) {
            return true;
        }
        __result = -1000f;
        return false;
    }

    /// <summary>
    /// 阻止已经分配的无人机继续给高级塔回复耐久。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ConstructionSystem), nameof(ConstructionSystem.Repair))]
    private static bool ConstructionSystem_Repair_Prefix(ConstructionSystem __instance,
        int entityId, ref bool __result) {
        if (!IsAdvancedTowerEntity(__instance.factory, entityId)) {
            return true;
        }
        __result = true;
        return false;
    }

    /// <summary>
    /// 高级塔拆除时接管原版单输入返还，按真实多缓存向执行拆除的玩家返还。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.TakeBackItems_Fractionator))]
    private static bool FactorySystem_TakeBackItems_Fractionator_Prefix(FactorySystem __instance,
        Player player, int fracId) {
        if (__instance?.factory == null || fracId <= 0 || fracId >= __instance.fractionatorPool.Length) {
            return true;
        }
        ref FractionatorComponent fractionator = ref __instance.fractionatorPool[fracId];
        if (fractionator.id != fracId || !IsAdvancedTowerEntity(__instance.factory, fractionator.entityId)) {
            return true;
        }
        AdvancedFractionatorRuntime.ReturnAndRemoveState(__instance.factory, fracId,
            returnContents: true, player: player);
        return false;
    }

    /// <summary>
    /// 在原版组件被移除前结算高级塔缓存。
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.RemoveEntityWithComponents))]
    private static void PlanetFactory_RemoveEntityWithComponents_Prefix(PlanetFactory __instance,
        int id) {
        if (id <= 0 || id >= __instance.entityPool.Length) {
            return;
        }
        ref EntityData entity = ref __instance.entityPool[id];
        if (entity.id != id || entity.fractionatorId <= 0
            || !FractionatorTowerCatalog.IsAdvancedTower(entity.protoId)) {
            return;
        }
        AdvancedFractionatorRuntime.ReturnAndRemoveState(__instance, entity.fractionatorId,
            returnContents: false);
    }

    private static bool IsAdvancedTowerEntity(PlanetFactory factory, int entityId) {
        if (factory == null || entityId <= 0 || entityId >= factory.entityPool.Length) {
            return false;
        }
        ref EntityData entity = ref factory.entityPool[entityId];
        return entity.id == entityId && FractionatorTowerCatalog.IsAdvancedTower(entity.protoId);
    }
}
