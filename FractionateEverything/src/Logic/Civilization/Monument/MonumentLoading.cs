using System;
using System.Linq;
using System.Reflection;
using FE.Compatibility.Mods;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization.Monument;

/// <summary>空仓自动识别专属来料；第三方弹药重设在执行前分流，已有普通弹药不被替换或丢弃。</summary>
internal static class MonumentLoading {
    // 每台设备独占原版六格需求数组，空仓轮换额外来源。

    private static int[] sails = [];

    /// <summary>全部第三方补丁安装后，定点保护会清仓的两个入口，其他补丁仍然执行。</summary>
    internal static void Initialize() {
        sails = LDB.items.dataArray.Where(item => item != null && MonumentCatalog.IsSail(item.ID)).Select(item => item.ID).ToArray();
        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".MonumentLaunchers");
        foreach ((Assembly assembly, string name) in new[] {
            (OrbitalRing.assembly, "ProjectOrbitalRing.Patches.Logic.MathematicalRateEngine.SiloAndEjector"),
            (MoreMegaStructure.assembly, "MoreMegaStructure.MoreMegaStructure"),
        }) {
            if (assembly == null) { continue; }
            Type type = assembly.GetType(name);
            if (type == null) { throw new InvalidOperationException($"丰碑弹药保护类型不兼容：{name}"); }
            Protect(type, "SiloUpdatePatch", typeof(SiloComponent), nameof(KeepSilo), harmony);
            Protect(type, "EjectorUpdatePatch", typeof(EjectorComponent), nameof(KeepEjector), harmony);
            if (name == "MoreMegaStructure.MoreMegaStructure") {
                MethodInfo receiver = AccessTools.Method(type, "GameTick_GammaPatch");
                if (receiver == null) { throw new InvalidOperationException("丰碑接收站缺少巨构分流入口。"); }
                harmony.Patch(receiver, prefix: new HarmonyMethod(typeof(MonumentLoading), nameof(KeepReceiver)) { priority = Priority.First });
            }
        }
    }

    private static void Protect(Type type, string name, Type component, string guard, Harmony harmony) {
        MethodInfo method = AccessTools.Method(type, name, [component.MakeByRefType()]);
        if (method == null) { throw new InvalidOperationException($"丰碑弹药保护入口不兼容：{type.FullName}.{name}"); }
        // 被保护方法本身也是Harmony补丁；用位置参数读取它收到的组件，而非再次取实例。
        harmony.Patch(method, prefix: new HarmonyMethod(typeof(MonumentLoading), guard) { priority = Priority.First });
    }

    private static bool KeepSilo(ref SiloComponent __0) => __0.bulletId != IFE丰碑火箭;
    private static bool KeepEjector(ref EjectorComponent __0) => !MonumentCatalog.IsSail(__0.bulletId);
    private static bool KeepReceiver(ref PowerGeneratorComponent __0, ref bool __result) {
        if (__0.productId != IFE丰碑结晶) { return true; }
        __result = true;
        return false;
    }

    private static void UpdateNeeds(ref int[] needs, int current, int count, bool ejector) {
        // 原版取料只检查六格：保留当前弹药，空仓每帧轮换五种候选。
        if (needs == null || needs.Length != 6) { needs = new int[6]; }
        Array.Clear(needs, 0, needs.Length);
        needs[0] = count < 20 ? current : 0;
        if (count != 0) { return; }
        if (!ejector) { needs[1] = IFE丰碑火箭; return; }
        for (int i = 0; i < Math.Min(5, sails.Length); i++) {
            needs[i + 1] = sails[(int)((GameMain.gameTick * 5 + i) % sails.Length)];
        }
    }



    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(EjectorComponent), nameof(EjectorComponent.InternalUpdate))]
    private static void EjectorComponent_InternalUpdate_Postfix(ref EjectorComponent __instance) =>
        UpdateNeeds(ref __instance.needs, __instance.bulletId, __instance.bulletCount, true);

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(SiloComponent), nameof(SiloComponent.InternalUpdate))]
    private static void SiloComponent_InternalUpdate_Postfix(ref SiloComponent __instance) =>
        UpdateNeeds(ref __instance.needs, __instance.bulletId, __instance.bulletCount, false);

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.InsertInto),
        [typeof(int), typeof(int), typeof(int), typeof(byte), typeof(byte), typeof(byte)],
        [ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out])]
    private static bool PlanetFactory_InsertInto_Prefix(PlanetFactory __instance, int entityId, int itemId,
        byte itemCount, byte itemInc, ref byte remainInc, ref int __result) =>
        Insert(__instance, entityId, itemId, itemCount, itemInc, ref remainInc, ref __result);

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.InsertInto),
        [typeof(uint), typeof(int), typeof(int), typeof(byte), typeof(byte), typeof(byte)],
        [ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out])]
    private static bool PlanetFactory_InsertIntoTyped_Prefix(PlanetFactory __instance, uint ioTargetTypedId, int itemId,
        byte itemCount, byte itemInc, ref byte remainInc, ref int __result) {
        int id = (int)(ioTargetTypedId & 0xffffff);
        var type = (EFactoryIOTargetType)(ioTargetTypedId & 0xff000000);
        int entity = type == EFactoryIOTargetType.Ejector && id > 0 && id < __instance.factorySystem.ejectorCursor
            ? __instance.factorySystem.ejectorPool[id].entityId
            : type == EFactoryIOTargetType.Silo && id > 0 && id < __instance.factorySystem.siloCursor
                ? __instance.factorySystem.siloPool[id].entityId : 0;
        return Insert(__instance, entity, itemId, itemCount, itemInc, ref remainInc, ref __result);
    }

    /// <summary>只接管专属来料并在实体锁内换空仓类型与入库；装有异种弹药时拒收，绝不清仓。</summary>
    private static bool Insert(PlanetFactory factory, int entity, int item, byte count, byte inc, ref byte remain, ref int result) {
        if (entity <= 0 || entity >= factory.entityPool.Length || factory.entityPool[entity].id != entity
            || count == 0 || !MonumentCatalog.IsSail(item) && item != IFE丰碑火箭) { return true; }
        int ejector = factory.entityPool[entity].ejectorId, silo = factory.entityPool[entity].siloId;
        if (ejector <= 0 && silo <= 0) { return true; }
        result = 0; remain = inc;
        lock (factory.entityMutexs[entity]) {
            if (ejector > 0 && MonumentCatalog.IsSail(item)) {
                ref EjectorComponent component = ref factory.factorySystem.ejectorPool[ejector];
                if (component.bulletCount >= 20 || component.bulletCount > 0 && component.bulletId != item) { return false; }
                component.bulletId = item; component.bulletCount += count; component.bulletInc += inc;
            } else if (silo > 0 && item == IFE丰碑火箭) {
                ref SiloComponent component = ref factory.factorySystem.siloPool[silo];
                if (component.bulletCount >= 20 || component.bulletCount > 0 && component.bulletId != item) { return false; }
                component.bulletId = item; component.bulletCount += count; component.bulletInc += inc;
            } else { return false; }
        }
        result = count; remain = 0;
        return false;
    }
}
