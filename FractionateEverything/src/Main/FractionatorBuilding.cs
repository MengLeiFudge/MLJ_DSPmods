using FractionateEverything.Compatibility;
using HarmonyLib;
using xiaoye97;
using static FractionateEverything.Utils.FractionatorUtils;
using static FractionateEverything.Utils.ProtoID;
using static FractionateEverything.Main.Tech;

namespace FractionateEverything.Main {
    /// <summary>
    /// 添加新的分馏塔（物品、模型、配方），适配显示位置。
    /// </summary>
    public static class FractionatorBuilding {
        /// <summary>
        /// 修改原版分馏塔相关内容，以适配万物分馏。
        /// </summary>
        public static void OriginFractionatorAdaptation() {
            ModelFractionator = LDB.models.Select(M分馏塔_FE通用分馏塔);
            ItemFractionator = LDB.items.Select(I分馏塔_FE通用分馏塔);
            var item = ItemFractionator;
            if (!GenesisBook.Enable) {
                RecipeFractionator = ItemFractionator.maincraft;
                //原版前移部分建筑位置，填补分馏塔空缺
                int[] buildingIDArr = [I化工厂, I量子化工厂_GB先进化学反应釜, I微型粒子对撞机];
                foreach (int buildingID in buildingIDArr) {
                    ItemProto building = LDB.items.Select(buildingID);
                    building.GridIndex--;
                    building.maincraft.GridIndex--;
                }
            }
            else {
                //创世去除了制作分馏塔的配方，需要加回来
                LDB.techs.Select(T高分子化工).UnlockRecipes.AddToArray(RFE通用分馏塔);
                RecipeFractionator = new() {
                    ID = RFE通用分馏塔,
                    Type = GenesisBook.标准制造,
                    Handcraft = true,
                    Name = "通用分馏塔",
                    TimeSpend = 240,
                    Items = [IGB先进机械组件, I钛合金, I钛化玻璃],
                    ItemCounts = [4, 8, 4],
                    Results = [I分馏塔_FE通用分馏塔],
                    ResultCounts = [1],
                    GridIndex = 2603,
                    IconPath = "Icons/ItemRecipe/fractionator",
                    preTech = LDB.techs.Select(T高分子化工),
                };
                LDBTool.PreAddProto(RecipeFractionator);
                RecipeFractionator.Preload(1003);
                RecipeFractionator.ID = RFE通用分馏塔;
                item.maincraft = RecipeFractionator;
            }
            //修改分馏塔的名称、描述、快捷栏绑定位置
            item.Name = "通用分馏塔";
            item.Description = "I通用分馏塔";
            item.GridIndex = 2603;
            item.maincraft.GridIndex = 2603;
            item.BuildIndex = 408;//创世将BuildIndex改为0，即“该版本尚未加入”，这里正好加回来
            item.Preload(item.index);
            LDBTool.SetBuildBar(item.BuildIndex / 100, item.BuildIndex % 100, item.ID);
        }

        public static void CreateAndPreAddNewFractionators() {
            //assembler-mk-1至assembler-mk-4，但对于分馏塔而言太暗，需要适当增加亮度
            //new(1.0f, 0.6596f, 0.3066f)
            //new(0.0f, 1.0f, 0.9112f)
            //new(0.3726f, 0.8f, 1.0f)
            //new(0.549f, 0.5922f, 0.6235f)

            //原版解锁科技：无 蓝 红 黄/紫 绿  因为重氢分馏是红糖解锁
            //[I铁块, I石材, I玻璃, I电路板]
            //[I铁块, I石材, I玻璃, I电路板]
            //[I钢材, I石材, I玻璃, I处理器]
            //[I钢材, I石墨烯, I钛化玻璃, I处理器]
            //[I钛合金, I粒子宽带, I位面过滤器, I量子芯片, I物质重组器]

            //创世解锁科技：无 蓝 黄 紫 绿
            //[IGB基础机械组件, I铁块, I玻璃]
            //[IGB基础机械组件, I钢材, I玻璃]
            //[IGB先进机械组件, I钛合金, I钛化玻璃]
            //[IGB尖端机械组件, IGB钨合金, IGB钨强化玻璃, IGB量子计算主机]
            //[IGB尖端机械组件, IGB三元精金, IGB钨强化玻璃, IGB超越X1型光学主机, I物质重组器]

            //创建新建筑
            var f1 = CreateAndPreAddNewFractionator(
                "精准分馏塔", RFE精准分馏塔, IFE精准分馏塔, MFE精准分馏塔,
                GenesisBook.Enable ? [IGB基础机械组件, I铁块, I玻璃] : [I铁块, I石材, I玻璃, I电路板],
                GenesisBook.Enable ? [2, 4, 2] : [4, 2, 2, 1],
                2601, GenesisBook.Enable ? TGB基础机械组件 : T电磁学, 406, new(1.0f, 0.7019f, 0.4f), 0.4);
            var f2 = CreateAndPreAddNewFractionator(
                "建筑极速分馏塔", RFE建筑极速分馏塔, IFE建筑极速分馏塔, MFE建筑极速分馏塔,
                GenesisBook.Enable ? [IGB基础机械组件, I钢材, I玻璃] : [I铁块, I石材, I玻璃, I电路板],
                GenesisBook.Enable ? [2, 4, 2] : [4, 2, 2, 1],
                2602, T改良物流系统, 407, new(0.4f, 1.0f, 0.949f), 1.0);
            var f3 = (
                RecipeFractionator,
                LDB.models.Select(M分馏塔_FE通用分馏塔),
                ItemFractionator);
            var f4 = CreateAndPreAddNewFractionator(
                "点数聚集分馏塔", RFE点数聚集分馏塔, IFE点数聚集分馏塔, MFE点数聚集分馏塔,
                GenesisBook.Enable
                    ? [IGB尖端机械组件, IGB钨合金, IGB钨强化玻璃, IGB量子计算主机]
                    : [I钢材, I石墨烯, I钛化玻璃, I处理器],
                GenesisBook.Enable ? [4, 8, 4, 1] : [8, 4, 4, 1],
                2604, tech增产点数聚集, 409, new(0.2509f, 0.8392f, 1.0f), 2.0);
            var f5 = CreateAndPreAddNewFractionator(
                "增产分馏塔", RFE增产分馏塔, IFE增产分馏塔, MFE增产分馏塔,
                GenesisBook.Enable
                    ? [IGB尖端机械组件, IGB三元精金, IGB钨强化玻璃, IGB超越X1型光学主机, I物质重组器]
                    : [I钛合金, I粒子宽带, I位面过滤器, I量子芯片, I物质重组器],
                GenesisBook.Enable ? [10, 20, 8, 1, 30] : [16, 8, 4, 1, 30],
                2605, tech增产分馏, 410, new(0.6235f, 0.6941f, 0.8f), 4.0);

            //设定升降级关系
            f1.Item3.Grade = 1;
            f3.Item3.Grade = 2;
            f1.Item3.Upgrades = [f1.Item3.ID, f3.Item3.ID];
            f3.Item3.Upgrades = [f1.Item3.ID, f3.Item3.ID];

            //适配创世
            if (GenesisBook.Enable) {
                f1.Item1.Type = GenesisBook.基础制造;
                f2.Item1.Type = GenesisBook.基础制造;
                f3.Item1.Type = GenesisBook.标准制造;
                f4.Item1.Type = GenesisBook.高精度加工;
                f5.Item1.Type = GenesisBook.高精度加工;
            }
        }
    }
}
