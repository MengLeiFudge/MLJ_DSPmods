using BepInEx.Bootstrap;
using CommonAPI.Systems;
using FractionateEverything.Main;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using xiaoye97;
using static FractionateEverything.Utils.ProtoID;
using static FractionateEverything.Compatibility.CheckPlugins;

namespace FractionateEverything.Compatibility {
    public static class MoreMegaStructure {
        internal const string GUID = "Gnimaerd.DSP.plugin.MoreMegaStructure";

        internal static bool Enable;
        internal static int tab巨构;
        private static Sprite alienmatrix;
        private static Sprite alienmatrixGray;
        private static bool _finished;

        internal static void Compatible() {
            Enable = Chainloader.PluginInfos.TryGetValue(GUID, out _);

            if (!Enable) return;

            tab巨构 = TabSystem.GetTabId("MegaStructures:MegaStructuresTab");

            var harmony = new Harmony(FractionateEverything.GUID + ".Compatibility.MoreMegaStructure");
            harmony.PatchAll(typeof(MoreMegaStructure));
            harmony.Patch(
                AccessTools.Method(typeof(VFPreload), "InvokeOnLoadWorkEnded"),
                null,
                new(typeof(MoreMegaStructure), nameof(AfterLDBToolPostAddData)) {
                    after = [LDBToolPlugin.MODGUID]
                }
            );
            //将巨构原有的所有方法全部屏蔽，不只是patch，初始化UI的也要屏蔽，防止创建两次UI
            //动态patch引用到了dll，需要用子方法，不然会报找不到巨构dll的错误
            UIBuildMenuPatcher.IgnoreMSPatches(harmony);
            LogInfo("MoreMegaStructure Compatibility Compatible finish.");
        }

        public static void AfterLDBToolPostAddData() {
            if (_finished) return;

            //水滴物品、配方迁移到巨构tab112
            ItemProto item = LDB.items.Select(IMS水滴);
            item.GridIndex = tab巨构 * 1000 + 112;
            item.maincraft.GridIndex = item.GridIndex;

            //为传送带上没有图标显示的物品添加显示图标
            int[] itemIDs = [
                IMS铁金属重构装置, IMS铜金属重构装置, IMS高纯硅重构装置, IMS钛金属重构装置, IMS单极磁石重构装置,
                IMS晶体接收器, IMS组件集成装置, IMS石墨提炼装置, IMS光栅晶体接收器, IMS物资交换物流站,
            ];
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
}
