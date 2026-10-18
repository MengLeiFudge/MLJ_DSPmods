using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using FE.Compatibility.Nebula;
using HarmonyLib;
using UnityEngine;

namespace FE.Logic.Civilization;

internal enum ChallengeFactKind { Build, Produce, Kill }

/// <summary>从工厂线程传给探索主线程的不可变事实，世界引用隔离切档前的尾部事件。</summary>
internal readonly struct ChallengeFact(ChallengeFactKind kind, int planetId, int assemblerId, int entityId,
    int recipeId, int count, Vector3 position) {
    public readonly GameData World = GameMain.data;
    public readonly long Tick = GameMain.gameTick;
    public readonly ChallengeFactKind Kind = kind;
    public readonly int PlanetId = planetId;
    public readonly int AssemblerId = assemblerId;
    public readonly int EntityId = entityId;
    public readonly int RecipeId = recipeId;
    public readonly int Count = count;
    public readonly Vector3 Position = position;
}

/// <summary>只观察实际制造完成、组件新建和击杀，不从库存或全局生产统计反推任务进度。</summary>
public static class ExplorationChallengeFacts {
    private sealed class Target(int planet, int recipe, int[] register, (int, int)[] machines) {
        public readonly int Planet = planet;
        public readonly int Recipe = recipe;
        public readonly int[] Register = register;
        public readonly HashSet<(int, int)> Machines = new(machines);
    }
    private static volatile Target[] targets = [];
    private static volatile bool watchCombat;
    private static readonly ConcurrentQueue<ChallengeFact> Facts = new();

    internal static void SetTargets((int Planet, int Recipe, (int, int)[] Machines)[] entries, bool combat = false) {
        watchCombat = combat;
        targets = entries.Select(entry => {
            PlanetFactory factory = GameMain.galaxy?.PlanetById(entry.Planet)?.factory;
            var statistics = GameMain.statistics?.production?.factoryStatPool;
            return factory != null && statistics != null && factory.index < statistics.Length
                ? new Target(entry.Planet, entry.Recipe, statistics[factory.index]?.productRegister, entry.Machines) : null;
        }).Where(target => target?.Register != null).ToArray();
    }

    internal static bool TryDequeue(out ChallengeFact fact) => Facts.TryDequeue(out fact);

    internal static void Clear() {
        targets = []; watchCombat = false;
        while (Facts.TryDequeue(out _)) { }
    }

    public static void RecordKill(ref EnemyData enemy) {
        if (watchCombat && !NebulaMultiplayerModAPI.IsClient && !enemy.isSpace && enemy.id > 0) {
            Facts.Enqueue(new(ChallengeFactKind.Kill, enemy.astroId, 0, 0, 0, 1, (Vector3)enemy.pos));
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.NewAssemblerComponent))]
    private static void FactorySystem_NewAssemblerComponent_Postfix(FactorySystem __instance, int entityId, int __result) {
        if (NebulaMultiplayerModAPI.IsClient || __result <= 0 || !targets.Any(t => t.Planet == __instance.planet.id)) { return; }
        Facts.Enqueue(new(ChallengeFactKind.Build, __instance.planet.id, __result, entityId, 0, 0, default));
    }

    /// <summary>制造台本轮完成前的产物缓存；无匹配挑战时为默认值。</summary>
    private readonly struct ProductionCapture(int planet, int before) {
        public readonly int Planet = planet;
        public readonly int Before = before;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AssemblerComponent), nameof(AssemblerComponent.InternalUpdate))]
    private static void AssemblerComponent_InternalUpdate_Prefix(ref AssemblerComponent __instance,
        int[] productRegister, out ProductionCapture __state) {
        __state = default;
        if (targets.Length == 0 || NebulaMultiplayerModAPI.IsClient || __instance.produced == null || __instance.produced.Length == 0) { return; }
        foreach (Target target in targets) {
            if (target.Recipe == __instance.recipeId && ReferenceEquals(target.Register, productRegister)
                && target.Machines.Contains((__instance.id, __instance.entityId))) {
                __state = new(target.Planet, __instance.produced[0]); return;
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AssemblerComponent), nameof(AssemblerComponent.InternalUpdate))]
    private static void AssemblerComponent_InternalUpdate_Postfix(ref AssemblerComponent __instance, ProductionCapture __state) {
        if (__state.Planet == 0 || __instance.produced == null || __instance.produced.Length == 0) { return; }
        int produced = __instance.produced[0] - __state.Before;
        if (produced <= 0) { return; }
        Facts.Enqueue(new(ChallengeFactKind.Produce, __state.Planet, __instance.id, __instance.entityId, __instance.recipeId, produced, default));
    }
}
