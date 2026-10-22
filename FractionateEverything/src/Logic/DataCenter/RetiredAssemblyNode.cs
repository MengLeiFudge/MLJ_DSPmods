using System;
using CommonAPI.Systems;
using HarmonyLib;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.DataCenter;

/// <summary>退役节点仅保留旧实体及机器缓存的原型引用，不再提供建造、生产或供工时能力。</summary>
internal static class RetiredAssemblyNode {
    private static ItemProto item;
    private static ModelProto model;

    internal static void Create() {
        item = ProtoRegistry.RegisterItem(IFE亚空间装配节点, "FE.RetiredAssembly.Node", "FE.RetiredAssembly.Description",
            "Assets/fe/interaction-station", 0, 20, EItemType.Production,
            ProtoRegistry.GetDefaultIconDesc(new Color(0.4f, 0.9f, 0.8f), Color.gray));
        model = ProtoRegistry.RegisterModel(MFE亚空间装配节点, item,
            "Entities/Prefabs/fractionator", null, [53, 11, 12, 1, 40], 0);
        // 旧制造台导入和取回缓存仍会解引用该配方；None 类型不进入生产配方选择。
        // CommonAPI在GridIndex为0时读取产物原型；留空图标使其先取得产物，避免空引用。
        RecipeProto recipe = ProtoRegistry.RegisterRecipe(RFE亚空间装配节点, ERecipeType.None, 600,
            [I钢材, I处理器, I电浆激发器], [20, 10, 10], [IFE亚空间装配节点], [1],
            "FE.RetiredAssembly.Description", 0, 0, item.Name, "");
        recipe.Handcraft = false;
        recipe.Explicit = true;
        recipe.NonProductive = true;
    }

    internal static void Configure() {
        item.CanBuild = false;
        item.BuildMode = 0;
        item.GridIndex = 0;
        PrefabDesc desc = model.prefabDesc;
        desc.slotPoses = [];
        desc.isFractionator = false;
        desc.isAssembler = false;
        desc.assemblerRecipeType = ERecipeType.None;
        desc.isPowerConsumer = true;
        desc.workEnergyPerTick = 0;
        desc.idleEnergyPerTick = 0;
        model.HpMax = LDB.models.Select(M分馏塔).HpMax;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PowerSystem), nameof(PowerSystem.Import))]
    private static void PowerSystem_Import_Postfix(PowerSystem __instance) {
        for (int i = 1; i < __instance.consumerCursor; i++) {
            ref PowerConsumerComponent consumer = ref __instance.consumerPool[i];
            int entity = consumer.entityId;
            if (consumer.id != i || entity <= 0 || entity >= __instance.factory.entityPool.Length
                || __instance.factory.entityPool[entity].protoId != IFE亚空间装配节点) { continue; }
            consumer.workEnergyPerTick = 0;
            consumer.idleEnergyPerTick = 0;
            consumer.SetRequiredEnergy(false);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AssemblerComponent), nameof(AssemblerComponent.InternalUpdate))]
    private static bool AssemblerComponent_InternalUpdate_Prefix(ref AssemblerComponent __instance, ref uint __result) {
        if (__instance.recipeId != RFE亚空间装配节点) { return true; }
        __instance.replicating = false;
        __result = 0;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AssemblerComponent), nameof(AssemblerComponent.UpdateNeeds))]
    private static bool AssemblerComponent_UpdateNeeds_Prefix(ref AssemblerComponent __instance) {
        if (__instance.recipeId != RFE亚空间装配节点) { return true; }
        if (__instance.needs != null) { Array.Clear(__instance.needs, 0, __instance.needs.Length); }
        return false;
    }
}
