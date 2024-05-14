using BepInEx.Bootstrap;
using CommonAPI.Systems;
using HarmonyLib;
using System.Collections.Generic;
using xiaoye97;
using static FractionateEverything.Utils.ProtoID;
using static FractionateEverything.Compatibility.CheckPlugins;

namespace FractionateEverything.Compatibility {
    public static class GenesisBook {
        internal const string GUID = "org.LoShin.GenesisBook";

        internal static bool Enable;
        internal static int tab精炼;
        internal static int tab化工;
        internal static int tab防御;
        private static bool _finished;

        #region 创世ERecipeType拓展

        internal const ERecipeType 基础制造 = ERecipeType.Assemble;
        internal const ERecipeType 标准制造 = (ERecipeType)9;
        internal const ERecipeType 高精度加工 = (ERecipeType)10;

        #endregion

        internal static void Compatible() {
            Enable = Chainloader.PluginInfos.TryGetValue(GUID, out _);

            if (!Enable) return;

            tab精炼 = TabSystem.GetTabId("org.LoShin.GenesisBook:org.LoShin.GenesisBookTab1");
            tab化工 = TabSystem.GetTabId("org.LoShin.GenesisBook:org.LoShin.GenesisBookTab2");
            tab防御 = TabSystem.GetTabId("org.LoShin.GenesisBook:org.LoShin.GenesisBookTab3");

            var harmony = new Harmony(FractionateEverything.GUID + ".Compatibility.GenesisBook");
            harmony.PatchAll(typeof(GenesisBook));
            harmony.Patch(
                AccessTools.Method(typeof(VFPreload), "InvokeOnLoadWorkEnded"),
                null,
                new(typeof(GenesisBook), nameof(AfterLDBToolPostAddData)) {
                    after = [LDBToolPlugin.MODGUID]
                }
            );
            LogInfo("GenesisBook Compatibility Compatible finish.");
        }

        public static void AfterLDBToolPostAddData() {
            if (_finished) return;

            //修改重氢的前置科技为奇异物质
            LDB.items.Select(I重氢).preTech = LDB.techs.Select(T奇异物质);

            //修改创世部分配方的显示位置，给万物分馏腾出来地方
            LDB.recipes.Select(RGB物质回收).GridIndex = 1209;
            LDB.recipes.Select(R动力引擎).GridIndex = 1210;
            List<int> idList = [
                R原型机, R精准无人机, R攻击无人机, R护卫舰, R驱逐舰,
                R高频激光塔_GB高频激光塔MKI, RGB高频激光塔MKII,
            ];
            foreach (var id in idList) {
                LDB.recipes.Select(id).GridIndex--;
            }
            idList = [
                R机枪弹箱, RGB钢芯弹箱, R超合金弹箱, RGB钨芯弹箱, RGB三元弹箱, RGB湮灭弹箱,
                R燃烧单元, R爆破单元, R晶石爆破单元_GB核子爆破单元, RGB反物质湮灭单元,
                R炮弹组, R高爆炮弹组, R晶石炮弹组_GB微型核弹组, RGB反物质炮弹组,
                R导弹组, R超音速导弹组, R引力导弹组, RGB反物质导弹组,
                R干扰胶囊, R压制胶囊, R等离子胶囊, R反物质胶囊,
            ];
            foreach (var id in idList) {
                LDB.recipes.Select(id).GridIndex += 2;
            }
            idList = [
                R近程电浆塔, R磁化电浆炮, R战场分析基站, R信号塔,
            ];
            foreach (var id in idList) {
                LDB.recipes.Select(id).GridIndex -= 4;
            }
            LDB.recipes.Select(R行星护盾发生器).GridIndex -= 3;
            LDB.recipes.Select(R干扰塔).GridIndex -= 5;
            LDB.recipes.Select(R高斯机枪塔).GridIndex = tab防御 * 1000 + 301;
            LDB.recipes.Select(R聚爆加农炮_GB聚爆加农炮MKI).GridIndex = tab防御 * 1000 + 501;
            LDB.recipes.Select(RGB聚爆加农炮MKII).GridIndex = tab防御 * 1000 + 502;

            _finished = true;
            LogInfo("GenesisBook Compatibility LDBToolOnPostAddDataAction finish.");
        }
    }
}
