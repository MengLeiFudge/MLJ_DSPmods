using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using FE.Compatibility.Nebula;
using HarmonyLib;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization.Monument;

/// <summary>专属弹药的发射与飞行。帆沿原版弹道；火箭复用原版渲染池但不占戴森节点订单。</summary>
internal static class MonumentFlight {
    private const int RocketMarker = -8180;
    [ThreadStatic] private static int firingMatrix;

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(EjectorComponent), nameof(EjectorComponent.InternalUpdate))]
    private static bool EjectorComponent_InternalUpdate_Prefix(ref EjectorComponent __instance, float power, long tick,
        DysonSwarm swarm, AstroData[] astroPoses, AnimData[] animPool, int[] consumeRegister, ref uint __result, out int __state) {
        if (__instance.bulletCount == 0 && MonumentCatalog.IsSail(__instance.bulletId)) { __instance.bulletId = I太阳帆; }
        __state = firingMatrix;
        firingMatrix = MonumentCatalog.TryGetSource(__instance.bulletId, out int source) ? source : 0;
        if (firingMatrix == 0) { return true; }
        __result = NativeEjector(ref __instance, power, tick, swarm, astroPoses, animPool, consumeRegister);
        return false;
    }

    /// <summary>专属弹药沿原版瞄准与充能，避开第三方把发射转换为普通太阳帆的转译。</summary>
    [HarmonyReversePatch]
    [HarmonyPatch(typeof(EjectorComponent), nameof(EjectorComponent.InternalUpdate))]
    private static uint NativeEjector(ref EjectorComponent instance, float power, long tick,
        DysonSwarm swarm, AstroData[] astroPoses, AnimData[] animPool, int[] consumeRegister) =>
        throw new NotSupportedException("丰碑弹射器原版入口未安装。");

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonSwarm), nameof(DysonSwarm.AddBullet))]
    private static bool DysonSwarm_AddBullet_Prefix(ref int __result) {
        if (firingMatrix == 0 || !NebulaMultiplayerModAPI.IsClient) { return true; }
        __result = 0;
        return false;
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(EjectorComponent), nameof(EjectorComponent.InternalUpdate))]
    private static void EjectorComponent_InternalUpdate_Finalizer(int __state) => firingMatrix = __state;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DysonSwarm), nameof(DysonSwarm.AddBullet))]
    private static void DysonSwarm_AddBullet_Postfix(DysonSwarm __instance, int __result) {
        if (firingMatrix <= 0 || __result <= 0) { return; }
        // 原版仅对正数轨道state创建普通太阳帆；负数保留来源，原版存档原样保存。
        __instance.bulletPool[__result].state = -firingMatrix;
        MonumentProjectiles.Send(__instance.starData.id, __instance.bulletPool[__result]);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonSwarm), nameof(DysonSwarm.BulletGameTick))]
    private static void DysonSwarm_BulletGameTick_Prefix(DysonSwarm __instance) {
        for (int i = 1; i < __instance.bulletCursor; i++) {
            ref SailBullet bullet = ref __instance.bulletPool[i];
            if (bullet.id != i || bullet.state >= 0 || bullet.t + 1f / 60f < bullet.maxt) { continue; }
            int matrix = -bullet.state;
            bullet.state = 0;
            MonumentManager.ReceiveSail(__instance.starData.id, matrix);
        }
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(SiloComponent), nameof(SiloComponent.InternalUpdate))]
    private static bool SiloComponent_InternalUpdate_Prefix(ref SiloComponent __instance, float power,
        DysonSphere sphere, AnimData[] animPool, int[] consumeRegister, ref uint __result) {
        if (__instance.bulletCount == 0 && __instance.bulletId == IFE丰碑火箭) { __instance.bulletId = I小型运载火箭; }
        if (__instance.bulletId != IFE丰碑火箭) { return true; }
        __result = UpdateSilo(ref __instance, power, sphere, animPool, consumeRegister);
        return false;
    }

    /// <summary>原版蓄力、增产加速、动画与冷却状态机；丰碑目标始终存在，不调用普通节点订单。</summary>
    private static uint UpdateSilo(ref SiloComponent silo, float power, DysonSphere sphere,
        AnimData[] animations, int[] consumption) {
        silo.needs ??= new int[6];
        silo.needs[0] = silo.bulletCount < 20 ? silo.bulletId : 0;
        if (silo.fired && silo.direction != -1) { silo.fired = false; }
        if (silo.direction == 1) { animations[silo.entityId].time = (float)silo.time / silo.chargeSpend; }
        else if (silo.direction == -1) { animations[silo.entityId].time = -(float)silo.time / silo.coldSpend; }
        animations[silo.entityId].power = power;
        int step = (int)(power * 10000f * (1f + Cargo.accTableMilli[silo.incLevel]) + 0.1f);
        if (silo.boost) { step *= 10; }
        lock (sphere.dysonSphere_mx) {
            silo.hasNode = true;
            if (power < 0.1f) {
                if (silo.direction == 1) { silo.time = (int)((long)silo.time * silo.coldSpend / silo.chargeSpend); silo.direction = -1; }
                return 0;
            }
            bool loaded = silo.bulletCount > 0;
            if (silo.direction == 1 && !loaded) {
                silo.time = (int)((long)silo.time * silo.coldSpend / silo.chargeSpend); silo.direction = -1;
            } else if (silo.direction == 0 && loaded) { silo.direction = 1; }
            if (silo.direction == 1) {
                silo.time += step;
                if (silo.time >= silo.chargeSpend) {
                    AstroData planet = sphere.starData.galaxy.astrosData[silo.planetId];
                    var rocket = new DysonRocket {
                        planetId = silo.planetId, nodeLayerId = RocketMarker, nodeId = 0,
                        uPos = planet.uPos + Maths.QRotateLF(planet.uRot, silo.localPos + silo.localPos.normalized * 6.1f),
                        uRot = planet.uRot * silo.localRot * Quaternion.Euler(-90f, 0f, 0f),
                        uSpeed = 0f, launch = silo.localPos.normalized, t = -1f,
                    };
                    rocket.uVel = rocket.uRot * Vector3.forward;
                    if (!NebulaMultiplayerModAPI.IsClient) {
                        AddRocket(sphere, rocket);
                        MonumentProjectiles.Send(sphere.starData.id, rocket);
                    }
                    silo.fired = true;
                    silo.autoIndex++;
                    int inc = silo.bulletInc / silo.bulletCount;
                    silo.incUsed |= inc > 0;
                    silo.bulletInc -= inc; silo.bulletCount--;
                    if (silo.bulletCount == 0) { silo.bulletInc = 0; }
                    lock (consumption) { consumption[silo.bulletId]++; }
                    silo.time = silo.coldSpend; silo.direction = -1;
                    sphere.gameData.spaceSector.AddHivePlanetHatred(sphere.starData.index, silo.planetId, 1);
                }
            } else if (silo.direction == -1) {
                silo.time -= step;
                if (silo.time <= 0) { silo.time = 0; silo.direction = loaded ? 1 : 0; }
            } else { silo.time = 0; }
            return loaded ? 3u : 2u;
        }
    }

    internal static void AddRocket(DysonSphere sphere, DysonRocket rocket) {
        lock (sphere.rocketPool) {
            int id;
            if (sphere.rocketRecycleCursor > 0) { id = sphere.rocketRecycle[--sphere.rocketRecycleCursor]; }
            else {
                id = sphere.rocketCursor++;
                if (id == sphere.rocketCapacity) { sphere.SetRocketCapacity(sphere.rocketCapacity * 2); }
            }
            rocket.id = id;
            sphere.rocketPool[id] = rocket;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonRocket), nameof(DysonRocket.FindRefNode))]
    private static bool DysonRocket_FindRefNode_Prefix(ref DysonRocket __instance) {
        if (__instance.nodeLayerId != RocketMarker) { return true; }
        __instance.node = null;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.RocketGameTick))]
    private static void DysonSphere_RocketGameTick_Prefix(DysonSphere __instance) {
        for (int id = 1; id < __instance.rocketCursor; id++) {
            ref DysonRocket rocket = ref __instance.rocketPool[id];
            if (rocket.id != id || rocket.nodeLayerId != RocketMarker) { continue; }
            AstroData planet = __instance.starData.galaxy.astrosData[rocket.planetId];
            VectorLF3 radial = rocket.uPos - planet.uPos;
            double altitude = radial.magnitude - planet.uRadius;
            VectorLF3 fromStar = rocket.uPos - __instance.starData.uPosition;
            double radius = __instance.defOrbitRadius;
            if (altitude >= 200d && Math.Abs(fromStar.magnitude - radius) < Math.Max(4d, rocket.uSpeed / 60d)) {
                __instance.RemoveDysonRocket(id);
                MonumentManager.ReceiveRocket(__instance.starData.id);
                continue;
            }
            Vector3 desired = altitude < 200d ? (Vector3)radial.normalized
                : (Vector3)(fromStar.normalized * (fromStar.magnitude > radius ? -1d : 1d));
            float cruise = 2800f * Mathf.Max(1f, (float)Math.Pow(radius / 10000d, 0.4d));
            float speed = altitude < 200d ? (float)(Math.Pow(Math.Max(0d, altitude) / 200d, 2d) * 600d + 15d) : cruise;
            rocket.uSpeed = Mathf.MoveTowards(rocket.uSpeed, speed, 7.5f);
            rocket.uVel = Vector3.Slerp(rocket.uVel, desired, 0.08f).normalized;
            rocket.uRot = Quaternion.Slerp(rocket.uRot, Quaternion.LookRotation(rocket.uVel), 0.2f);
            rocket.uPos += (VectorLF3)rocket.uVel * (rocket.uSpeed / 60d);
            rocket.t = altitude < 200d ? (float)Math.Max(-1d, altitude / 200d - 1d) : 0f;
        }
    }

    /// <summary>原版看到专属火箭没有节点时保留其渲染项；其他火箭的移除语义完全沿用。</summary>
    private static void RemoveOrdinaryRocket(DysonSphere sphere, int id) {
        if (sphere.rocketPool[id].nodeLayerId != RocketMarker) { sphere.RemoveDysonRocket(id); }
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.RocketGameTick))]
    private static IEnumerable<CodeInstruction> DysonSphere_RocketGameTick_Transpiler(IEnumerable<CodeInstruction> instructions) {
        var original = AccessTools.Method(typeof(DysonSphere), nameof(DysonSphere.RemoveDysonRocket));
        var replacement = AccessTools.Method(typeof(MonumentFlight), nameof(RemoveOrdinaryRocket));
        int replaced = 0;
        var result = instructions.ToList();
        foreach (CodeInstruction instruction in result) {
            if (!instruction.Calls(original)) { continue; }
            instruction.opcode = OpCodes.Call; instruction.operand = replacement; replaced++;
        }
        if (replaced != 2) { throw new InvalidOperationException("丰碑火箭未能匹配原版飞行移除边界。"); }
        return result;
    }
}
