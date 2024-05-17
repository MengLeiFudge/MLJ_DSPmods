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

            //调整黑雾物品的位置，调回胶囊位置
            List<int> idList = [I能量碎片, I黑雾矩阵, I物质重组器, I硅基神经元, I负熵奇点, I核心素];
            for (int i = 0; i < idList.Count; i++) {
                LDB.items.Select(idList[i]).GridIndex = tab防御 * 1000 + (i + 2) * 100 + 9;
            }
            idList = [I干扰胶囊, I压制胶囊, I等离子胶囊, I反物质胶囊];
            for (int i = 0; i < idList.Count; i++) {
                LDB.items.Select(idList[i]).GridIndex = tab防御 * 1000 + 700 + i + 1;
            }

            //修改创世部分物品、配方的显示位置
            LDB.recipes.Select(RGB物质回收).GridIndex = 1209;
            ModifyItemAndItsRecipeGridIndex(I动力引擎, 1, 210);
            idList = [
                I原型机, I精准无人机, I攻击无人机, I护卫舰, I驱逐舰,
                I高频激光塔_GB高频激光塔MKI, IGB高频激光塔MKII,
            ];
            foreach (var id in idList) {
                ModifyItemAndItsRecipeGridIndex(id, -1);
            }
            idList = [
                I机枪弹箱, IGB钢芯弹箱, I超合金弹箱, IGB钨芯弹箱, IGB三元弹箱, IGB湮灭弹箱,
                I燃烧单元, I爆破单元, IGB核子爆破单元, IGB反物质湮灭单元,
                I炮弹组, I高爆炮弹组, IGB微型核弹组, IGB反物质炮弹组,
                I导弹组, I超音速导弹组, I引力导弹组, IGB反物质导弹组,
                I干扰胶囊, I压制胶囊, I等离子胶囊, I反物质胶囊,
            ];
            foreach (var id in idList) {
                ModifyItemAndItsRecipeGridIndex(id, 2);
            }
            idList = [I近程电浆塔, I磁化电浆炮, I战场分析基站, I信号塔];
            foreach (var id in idList) {
                ModifyItemAndItsRecipeGridIndex(id, -4);
            }
            ModifyItemAndItsRecipeGridIndex(I干扰塔, tab防御, 207);
            ModifyItemAndItsRecipeGridIndex(I行星护盾发生器, tab防御, 208);
            ModifyItemAndItsRecipeGridIndex(I高斯机枪塔, tab防御, 301);
            ModifyItemAndItsRecipeGridIndex(I聚爆加农炮_GB聚爆加农炮MKI, tab防御, 501);
            ModifyItemAndItsRecipeGridIndex(IGB聚爆加农炮MKII, tab防御, 502);
            ModifyItemAndItsRecipeGridIndex(I导弹防御塔, tab防御, 601);

            _finished = true;
            LogInfo("GenesisBook Compatibility LDBToolOnPostAddDataAction finish.");
        }

        private static void ModifyItemAndItsRecipeGridIndex(int itemId, int tab, int rowColumn) {
            var item = LDB.items.Select(itemId);
            item.GridIndex = tab * 1000 + rowColumn;
            item.maincraft.GridIndex = item.GridIndex;
        }

        private static void ModifyItemAndItsRecipeGridIndex(int itemId, int offset) {
            var item = LDB.items.Select(itemId);
            item.GridIndex += offset;
            item.maincraft.GridIndex = item.GridIndex;
        }
    }
}
