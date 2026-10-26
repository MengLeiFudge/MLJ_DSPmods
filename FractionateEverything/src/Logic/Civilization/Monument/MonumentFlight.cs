using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using FE.Compatibility.Nebula;
using HarmonyLib;

namespace FE.Logic.Civilization.Monument;

/// <summary>专属弹药复用原版发射、节点订单与飞行；负层号保留火箭来源，负帆状态保留矩阵来源。</summary>
internal static class MonumentFlight {
    [ThreadStatic] private static int firingMatrix;
    [ThreadStatic] private static bool firingRocket;
    [ThreadStatic] private static int constructingStar;

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(EjectorComponent), nameof(EjectorComponent.InternalUpdate))]
    private static bool EjectorUpdate(ref EjectorComponent __instance, float power, long tick,
        DysonSwarm swarm, AstroData[] astroPoses, AnimData[] animPool, int[] consumeRegister, ref uint __result, out int __state) {
        __state = firingMatrix;
        if (__instance.bulletCount == 0 && MonumentCatalog.IsSail(__instance.bulletId)) { __instance.bulletId = FE.Utils.Utils.I太阳帆; }
        firingMatrix = MonumentCatalog.TryGetSource(__instance.bulletId, out int source) ? source : 0;
        if (firingMatrix == 0) { return true; }
        if (!MonumentManager.CanReceiveSail(swarm.starData.id, firingMatrix)) { __result = 0; return false; }
        __result = NativeEjector(ref __instance, power, tick, swarm, astroPoses, animPool, consumeRegister);
        return false;
    }

    [HarmonyReversePatch]
    [HarmonyPatch(typeof(EjectorComponent), nameof(EjectorComponent.InternalUpdate))]
    private static uint NativeEjector(ref EjectorComponent instance, float power, long tick,
        DysonSwarm swarm, AstroData[] astroPoses, AnimData[] animPool, int[] consumeRegister) =>
        throw new NotSupportedException("丰碑弹射器原版入口未安装。");

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(EjectorComponent), nameof(EjectorComponent.InternalUpdate))]
    private static void EjectorFinished(int __state) => firingMatrix = __state;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonSwarm), nameof(DysonSwarm.AddBullet))]
    private static bool BeforeAddBullet(ref int __result) {
        if (firingMatrix == 0 || !NebulaMultiplayerModAPI.IsClient) { return true; }
        __result = 0; return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DysonSwarm), nameof(DysonSwarm.AddBullet))]
    private static void AfterAddBullet(DysonSwarm __instance, int __result) {
        if (firingMatrix <= 0 || __result <= 0) { return; }
        __instance.bulletPool[__result].state = -firingMatrix;
        MonumentProjectiles.Send(__instance.starData.id, __instance.bulletPool[__result]);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonSwarm), nameof(DysonSwarm.BulletGameTick))]
    private static void ArriveSails(DysonSwarm __instance) {
        for (int i = 1; i < __instance.bulletCursor; i++) {
            ref SailBullet bullet = ref __instance.bulletPool[i];
            if (bullet.id != i || bullet.state >= 0 || bullet.t + 1f / 60f < bullet.maxt) { continue; }
            int matrix = -bullet.state;
            if (NebulaMultiplayerModAPI.IsClient || GameMain.gameTick % 60 == 0 && MonumentManager.ReceiveSail(__instance.starData.id, matrix)) {
                bullet.state = 0;
            } else {
                // 已付费的在途帆等待可用细胞，不因同批抵达或临时配额已满而销毁。
                bullet.t = bullet.maxt - 2f / 60f;
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(SiloComponent), nameof(SiloComponent.InternalUpdate))]
    private static bool SiloUpdate(ref SiloComponent __instance, float power, DysonSphere sphere,
        AnimData[] animPool, int[] consumeRegister, ref uint __result, out bool __state) {
        __state = firingRocket;
        if (__instance.bulletCount == 0 && __instance.bulletId == FE.Utils.Utils.IFE丰碑火箭) { __instance.bulletId = FE.Utils.Utils.I小型运载火箭; }
        firingRocket = __instance.bulletId == FE.Utils.Utils.IFE丰碑火箭;
        if (!firingRocket) { return true; }
        __result = NativeSilo(ref __instance, power, sphere, animPool, consumeRegister);
        return false;
    }

    [HarmonyReversePatch]
    [HarmonyPatch(typeof(SiloComponent), nameof(SiloComponent.InternalUpdate))]
    private static uint NativeSilo(ref SiloComponent instance, float power, DysonSphere sphere, AnimData[] animPool, int[] consumeRegister) =>
        throw new NotSupportedException("丰碑发射井原版入口未安装。");

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(SiloComponent), nameof(SiloComponent.InternalUpdate))]
    private static void SiloFinished(bool __state) => firingRocket = __state;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.AddDysonRocket))]
    private static bool BeforeAddRocket(ref int __result) {
        if (!firingRocket || !NebulaMultiplayerModAPI.IsClient) { return true; }
        __result = 0; return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.AddDysonRocket))]
    private static void AfterAddRocket(DysonSphere __instance, int __result) {
        if (!firingRocket || __result <= 0) { return; }
        ref DysonRocket rocket = ref __instance.rocketPool[__result];
        rocket.nodeLayerId = -rocket.nodeLayerId;
        MonumentProjectiles.Send(__instance.starData.id, rocket);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DysonRocket), nameof(DysonRocket.FindRefNode))]
    private static void FindNode(ref DysonRocket __instance, out int __state) {
        __state = __instance.nodeLayerId;
        if (__state is >= -10 and <= -1) { __instance.nodeLayerId = -__state; }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DysonRocket), nameof(DysonRocket.FindRefNode))]
    private static void FoundNode(ref DysonRocket __instance, int __state) {
        if (__state is >= -10 and <= -1 && __instance.node != null) { __instance.nodeLayerId = __state; }
    }

    internal static void AddRocket(DysonSphere sphere, DysonRocket rocket) {
        if (rocket.node == null) { return; }
        int id = sphere.AddDysonRocket(rocket, rocket.node);
        if (id > 0) { sphere.rocketPool[id].nodeLayerId = rocket.nodeLayerId; }
    }

    private static void ConstructRocket(DysonSphere sphere, ref DysonRocket rocket) {
        int previous = constructingStar;
        constructingStar = rocket.nodeLayerId is >= -10 and <= -1 ? sphere.starData.id : 0;
        try { sphere.ConstructSp(rocket.node); }
        finally { constructingStar = previous; }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DysonNode), nameof(DysonNode.ConstructSp))]
    private static void StructureBuilt(object __result) {
        if (constructingStar > 0) { MonumentManager.RecordStructure(constructingStar, __result); }
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.RocketGameTick))]
    private static IEnumerable<CodeInstruction> RocketConstruction(IEnumerable<CodeInstruction> instructions) {
        var code = instructions.ToList();
        var construct = AccessTools.Method(typeof(DysonSphere), nameof(DysonSphere.ConstructSp));
        var field = AccessTools.Field(typeof(DysonRocket), nameof(DysonRocket.node));
        int replaced = 0;
        for (int i = 1; i < code.Count; i++) {
            if (!code[i].Calls(construct) || !code[i - 1].LoadsField(field)) { continue; }
            code[i - 1].opcode = OpCodes.Nop; code[i - 1].operand = null;
            code[i].opcode = OpCodes.Call;
            code[i].operand = AccessTools.Method(typeof(MonumentFlight), nameof(ConstructRocket));
            replaced++;
        }
        if (replaced != 1) { throw new InvalidOperationException("丰碑火箭未匹配原版节点建设边界。"); }
        return code;
    }
}
