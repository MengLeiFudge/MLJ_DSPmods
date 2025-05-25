using BepInEx.Bootstrap;
using CommonAPI.Systems;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using xiaoye97;
using static FE.Utils.ProtoID;
using static FE.Compatibility.CheckPlugins;

namespace FE.Compatibility;

public static class MoreMegaStructure {
    internal const string GUID = "Gnimaerd.DSP.plugin.MoreMegaStructure";

    internal static bool Enable;
    internal static int tab巨构;
    private static bool _finished;

    internal static void Compatible() {
        Enable = Chainloader.PluginInfos.TryGetValue(GUID, out _);
        if (!Enable) return;

        tab巨构 = TabSystem.GetTabId("MegaStructures:MegaStructuresTab");

        var harmony = new Harmony(PluginInfo.PLUGIN_GUID + ".Compatibility.MoreMegaStructure");
        harmony.PatchAll(typeof(MoreMegaStructure));
        harmony.Patch(
            AccessTools.Method(typeof(VFPreload), "InvokeOnLoadWorkEnded"),
            null,
            new(typeof(MoreMegaStructure), nameof(AfterLDBToolPostAddData)) {
                after = [LDBToolPlugin.MODGUID]
            }
        );
        LogInfo("MoreMegaStructure Compat finish.");
    }

    public static void AfterLDBToolPostAddData() {
        if (_finished) return;

        //水滴物品、配方迁移到巨构tab111
        ItemProto item = LDB.items.Select(IVD水滴);
        item.GridIndex = tab巨构 * 1000 + 111;
        item.maincraft.GridIndex = item.GridIndex;

        //为传送带上没有图标显示的物品添加显示图标
        int[] itemIDs = [IMS组件集成装置, IMS物资交换物流站];
        foreach (int itemID in itemIDs) {
            try {
                ref Dictionary<int, IconToolNew.IconDesc> itemIconDescs
                    = ref AccessTools.StaticFieldRefAccess<Dictionary<int, IconToolNew.IconDesc>>(
                        typeof(ProtoRegistry),
                        "itemIconDescs");
                if (!itemIconDescs.ContainsKey(itemID)) {
                    IconToolNew.IconDesc iconDesc = new() {
                        faceColor = Color.white,
                        sideColor = LDB.items.Select(itemID).prefabDesc.lodMaterials[0][0].color,
                        faceEmission = Color.black,
                        sideEmission = Color.black,
                        iconEmission = Color.clear,
                        metallic = 0.8f,
                        smoothness = 0.5f,
                        solidAlpha = 1f,
                        iconAlpha = 1f,
                    };
                    itemIconDescs.Add(itemID, iconDesc);
                }
            }
            catch {
                // ignored
            }
        }

        _finished = true;
        LogInfo("MoreMegaStructure Compatibility LDBToolOnPostAddDataAction finish.");
    }
}
