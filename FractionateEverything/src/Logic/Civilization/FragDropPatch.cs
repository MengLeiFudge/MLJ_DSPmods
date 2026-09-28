using FE.Compatibility.Nebula;
using HarmonyLib;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

/// <summary>
/// 黑雾击杀的 Frag 与通用原胚掉落入口。无黑雾存档仍由探索资源点提供原胚和完整主线。
/// </summary>
public static class FragDropPatch {
    private const int DropProbabilityScale = 10000;
    private const int DropLifetime = 1800;
    private const double BaseFragDropProbability = 0.01d;
    private const double EmbryoDropProbability = 0.01d;

    /// <summary>
    /// 每次黑雾击杀分别判定 Frag 与通用原胚；成长科技只提高 Frag 的基础概率。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.OnEnemyKilled))]
    private static void GameHistoryData_OnEnemyKilled_Postfix(GameHistoryData __instance,
        ref EnemyData enemyData) {
        TrashSystem trashSystem = __instance?.gameData?.trashSystem;
        if (NebulaMultiplayerModAPI.IsClient || enemyData.id <= 0 || trashSystem == null) {
            return;
        }

        double fragProbability = BaseFragDropProbability
                                 * (1d + GrowthTechnologyManager.ExplorationLevel * 0.2d);
        TryDropItem(__instance.gameData, trashSystem, ref enemyData, IFE残片, fragProbability);
        TryDropItem(__instance.gameData, trashSystem, ref enemyData, IFE通用原胚,
            EmbryoDropProbability);
    }

    private static void TryDropItem(GameData gameData, TrashSystem trashSystem, ref EnemyData enemyData,
        int itemId, double probability) {
        if (trashSystem.enemyDropBans.Contains(itemId)) {
            return;
        }
        int threshold = (int)(probability * DropProbabilityScale);
        if (RandomTable.Integer(ref trashSystem.randSeed, DropProbabilityScale) >= threshold) {
            return;
        }

        if (enemyData.isSpace) {
            AddSpaceDrop(gameData, trashSystem, ref enemyData, itemId);
        } else {
            AddGroundDrop(gameData, trashSystem, ref enemyData, itemId);
        }
    }

    private static void AddGroundDrop(GameData gameData, TrashSystem trashSystem, ref EnemyData enemyData,
        int itemId) {
        PlanetFactory[] factories = gameData?.spaceSector?.skillSystem?.astroFactories;
        int astroId = enemyData.astroId;
        if (factories == null || astroId <= 0 || astroId >= factories.Length) {
            return;
        }
        PlanetFactory factory = factories[astroId];
        if (factory != null) {
            trashSystem.AddTrashFromGroundEnemy(itemId, 1, DropLifetime, enemyData.id, factory);
        }
    }

    private static void AddSpaceDrop(GameData gameData, TrashSystem trashSystem, ref EnemyData enemyData,
        int itemId) {
        SpaceSector sector = gameData?.spaceSector;
        EnemyDFHiveSystem hive = sector?.GetHiveByAstroId(enemyData.originAstroId);
        StarData star = hive?.starData;
        if (sector == null || star == null || !enemyData.positionIsValid) {
            return;
        }

        VectorLF3 localPosition = enemyData.pos;
        Quaternion localRotation = enemyData.rot;
        Vector3 localVelocity = enemyData.vel;
        sector.TransformFromAstro_ref(enemyData.astroId, out VectorLF3 universalPosition,
            out Quaternion universalRotation, ref localPosition, ref localRotation);
        sector.TransformVelocityFromAstro_ref(enemyData.astroId, out Vector3 universalVelocity,
            ref localPosition, ref localVelocity);

        TrashData trashData = new() {
            warningId = 0,
            landPlanetId = 0,
            nearPlanetId = 0,
            nearStarId = star.astroId,
            nearStarGravity = trashSystem.GetStarGravity(star.id),
            life = DropLifetime,
            lPos = Vector3.zero,
            lRot = Quaternion.identity,
            uPos = universalPosition + RandomTable.SphericNormal(ref trashSystem.randSeed, 0.2),
            uRot = universalRotation,
            uVel = universalVelocity + (Vector3)RandomTable.SphericNormal(ref trashSystem.randSeed, 1.5),
            uAgl = RandomTable.SphericNormal(ref trashSystem.randSeed, 0.03),
        };
        TrashObject trashObject = new(itemId, 1, 0,
            Maths.QInvRotateLF(gameData.relativeRot, trashData.uPos - gameData.relativePos),
            Quaternion.Inverse(gameData.relativeRot) * trashData.uRot);
        trashSystem.container.NewTrash(trashObject, trashData);
    }
}
