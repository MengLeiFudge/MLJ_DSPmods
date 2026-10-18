using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using CommonAPI.Systems;
using FE.Compatibility.Nebula;
using HarmonyLib;
using UnityEngine;
using static FE.FractionateEverything;
using static FE.Utils.Utils;

namespace FE.Logic.DataCenter.Assembly;

/// <summary>独立供电装配节点，仅贡献共享工艺速度，不借用制造台库存或第三方工厂接口。</summary>
public static class AssemblyNodes {
    private sealed class NodeSet { public readonly HashSet<int> Consumers = []; }
    private static ConditionalWeakTable<PowerSystem, NodeSet> nodes = new();
    private static readonly object CreditLock = new();
    private static double credit;
    private static ModelProto model;
    /// <summary>每节点满电提供一倍普通配方速度；工作1.2MW，待机60kW。</summary>
    public const double Speed = 1d;
    public static volatile bool WorkRequested;

    public static void Create() {
        ItemProto item = ProtoRegistry.RegisterItem(IFE亚空间装配节点, "FE.Assembly.Node", "FE.Assembly.NodeDescription",
            "Assets/fe/interaction-station", tab分馏 * 1000 + 309, 20, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(new Color(0.4f, 0.9f, 0.8f), Color.gray));
        item.IconTag = "subspace-assembly";
        RecipeProto recipe = ProtoRegistry.RegisterRecipe(RFE亚空间装配节点, ERecipeType.Assemble, 600,
            [I钢材, I处理器, I电浆激发器], [20, 10, 10], [IFE亚空间装配节点], [1],
            "FE.Assembly.NodeDescription", T处理器, item.GridIndex, item.Name, item.IconPath);
        recipe.NonProductive = true;
        model = ProtoRegistry.RegisterModel(MFE亚空间装配节点, item,
            "Entities/Prefabs/fractionator", null, [53, 11, 12, 1, 40], 0);
    }

    /// <summary>移除模板的分馏能力，只保留供电、碰撞与建筑生命周期。</summary>
    public static void Configure() {
        PrefabDesc desc = model.prefabDesc;
        desc.slotPoses = []; desc.isFractionator = false; desc.isAssembler = false; desc.assemblerRecipeType = ERecipeType.None;
        desc.isPowerConsumer = true; desc.workEnergyPerTick = 20000; desc.idleEnergyPerTick = 1000;
        model.HpMax = LDB.models.Select(M分馏塔).HpMax;
    }

    internal static double DrainWork() { lock (CreditLock) { double result = credit; credit = 0; return result; } }
    internal static void Reset() { WorkRequested = false; lock (CreditLock) { credit = 0; } nodes = new(); }

    private static bool IsNode(PowerSystem system, int id) {
        if (id <= 0 || id >= system.consumerCursor || system.consumerPool[id].id != id) { return false; }
        int entity = system.consumerPool[id].entityId;
        return entity > 0 && entity < system.factory.entityPool.Length && system.factory.entityPool[entity].id == entity
            && system.factory.entityPool[entity].protoId == IFE亚空间装配节点;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PowerSystem), nameof(PowerSystem.NewConsumerComponent))]
    private static void PowerSystem_NewConsumerComponent_Postfix(PowerSystem __instance, int __result) {
        if (!IsNode(__instance, __result)) { return; }
        NodeSet set = nodes.GetValue(__instance, _ => new());
        lock (set) { set.Consumers.Add(__result); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PowerSystem), nameof(PowerSystem.Import))]
    private static void PowerSystem_Import_Postfix(PowerSystem __instance, BinaryReader r) {
        NodeSet set = nodes.GetValue(__instance, _ => new());
        lock (set) {
            set.Consumers.Clear();
            for (int i = 1; i < __instance.consumerCursor; i++) { if (IsNode(__instance, i)) { set.Consumers.Add(i); } }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PowerSystem), nameof(PowerSystem.GameTick))]
    private static void PowerSystem_GameTick_Prefix(PowerSystem __instance, out bool __state) {
        __state = false;
        if (NebulaMultiplayerModAPI.IsClient) { return; }
        NodeSet set = nodes.GetValue(__instance, system => {
            var restored = new NodeSet();
            for (int id = 1; id < system.consumerCursor; id++) { if (IsNode(system, id)) { restored.Consumers.Add(id); } }
            return restored;
        });
        __state = WorkRequested;
        lock (set) {
            set.Consumers.RemoveWhere(id => !IsNode(__instance, id));
            foreach (int id in set.Consumers) { __instance.consumerPool[id].SetRequiredEnergy(__state); }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PowerSystem), nameof(PowerSystem.GameTick))]
    private static void PowerSystem_GameTick_Postfix(PowerSystem __instance, bool __state) {
        if (!__state || !ReferenceEquals(__instance.factory.gameData, GameMain.data)
            || !nodes.TryGetValue(__instance, out NodeSet set)) { return; }
        double gained = 0;
        lock (set) {
            foreach (int id in set.Consumers) {
                if (!IsNode(__instance, id)) { continue; }
                int network = __instance.consumerPool[id].networkId;
                if (network > 0 && network < __instance.networkServes.Length) { gained += Math.Max(0, Math.Min(1, __instance.networkServes[network])) * Speed; }
            }
        }
        lock (CreditLock) { credit += gained; }
    }
}
