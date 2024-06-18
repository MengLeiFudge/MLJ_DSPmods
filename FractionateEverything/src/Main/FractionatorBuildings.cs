using CommonAPI;
using CommonAPI.Systems;
using FractionateEverything.Compatibility;
using HarmonyLib;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;
using xiaoye97;
using static FractionateEverything.Utils.ProtoID;
using static FractionateEverything.Main.FractionatorLogic;

namespace FractionateEverything.Main {
    struct Unlock {
        public int recipeID;
        public int itemID;
        public int preTechID;
    }

    /// <summary>
    /// 添加新的分馏塔（物品、模型、配方），适配显示位置。
    /// </summary>
    public static class FractionatorBuildings {
        private static RecipeProto RecipeFractionator;
        private static ModelProto ModelFractionator;
        private static ItemProto ItemFractionator;
        private static readonly List<Unlock> unlockList = [];
        /// <summary>
        /// 仅用于获取基础缓存大小。
        /// </summary>
        public static PrefabDesc FractionatorPrefabDesc => ModelFractionator.prefabDesc;

        /// <summary>
        /// 修改原版分馏塔相关内容，以适配万物分馏。
        /// </summary>
        public static void OriginFractionatorAdaptation() {
            ModelFractionator = LDB.models.Select(M分馏塔_FE通用分馏塔);
            ItemFractionator = LDB.items.Select(I分馏塔_FE通用分馏塔);
            if (!GenesisBook.Enable) {
                RecipeFractionator = ItemFractionator.maincraft;
                unlockList.Add(new() {
                    itemID = ItemFractionator.ID,
                    recipeID = RecipeFractionator.ID,
                    preTechID = T重氢分馏_GB强相互作用力材料_FE通用分馏,
                });
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
                RecipeFractionator = new() {
                    ID = RFE通用分馏塔,
                    Type = GenesisBook.标准制造,
                    Handcraft = true,
                    Name = "通用分馏塔",
                    TimeSpend = 240,
                    Items = [IGB先进机械组件, I钛合金, I钛化玻璃, I处理器],
                    ItemCounts = [4, 8, 4, 1],
                    Results = [I分馏塔_FE通用分馏塔],
                    ResultCounts = [1],
                    GridIndex = 2604,
                    IconPath = "Icons/ItemRecipe/fractionator",
                    preTech = null,
                };
                LDBTool.PreAddProto(RecipeFractionator);
                RecipeFractionator.Preload(RFE通用分馏塔);
                RecipeFractionator.ID = RFE通用分馏塔;
                unlockList.Add(new() {
                    itemID = ItemFractionator.ID,
                    recipeID = RecipeFractionator.ID,
                    preTechID = T微型粒子对撞机_GB粒子对撞机,
                });
            }
            //修改分馏塔的名称、描述、快捷栏绑定位置
            ItemFractionator.Name = "通用分馏塔";
            ItemFractionator.Description = "I通用分馏塔";
            ItemFractionator.GridIndex = 2604;
            RecipeFractionator.GridIndex = 2604;
            ItemFractionator.BuildIndex = 0;//使用两行快捷建造栏，不需要绑定到原有的BuildMenu上面了
            //BuildBar.Bind(5, ItemFractionator.GridIndex % 10, ItemFractionator.ID, 2);
            ItemFractionator.Preload(ItemFractionator.index);
            RecipeFractionator.Preload(RecipeFractionator.index);
        }

        public static void CreateAndPreAddNewFractionators() {
            //assembler-mk-1至assembler-mk-4，但对于分馏塔而言太暗，需要适当增加亮度
            //new(1.0f, 0.6596f, 0.3066f)
            //new(0.0f, 1.0f, 0.9112f)
            //new(0.3726f, 0.8f, 1.0f)
            //new(0.549f, 0.5922f, 0.6235f)

            //创世解锁科技：蓝 蓝 红 黄 紫 绿
            //[IGB基础机械组件, I铁块, I玻璃, I电路板][2, 4, 2, 1]
            //[IGB基础机械组件, I钢材, I玻璃, I电路板][4, 8, 4, 1]
            //[IGB先进机械组件, I钛合金, I钛化玻璃, I处理器][4, 8, 4, 1]
            //[IGB先进机械组件, I钛合金, I钛化玻璃, I处理器][4, 8, 4, 1]
            //[IGB尖端机械组件, IGB钨合金, IGB钨强化玻璃, I量子芯片][4, 8, 4, 1]
            //[IGB尖端机械组件, IGB三元精金, IGB钨强化玻璃, IGB光学处理器, I物质重组器][10, 20, 8, 1, 30]

            //原版解锁科技：蓝 蓝 红 红 黄 绿
            //[I铁块, I石材, I玻璃, I电路板][4, 2, 2, 1]
            //[I铁块, I石材, I玻璃, I电路板][8, 4, 4, 1]
            //[I钢材, I石材, I玻璃, I处理器][8, 4, 4, 1]
            //[I钢材, I石材, I玻璃, I处理器][8, 4, 4, 1]
            //[I钛合金, I石墨烯, I钛化玻璃, I处理器][8, 4, 4, 1]
            //[I钛合金, I粒子宽带, I位面过滤器, I量子芯片, I物质重组器][16, 8, 4, 1, 30]

            //创建新建筑
            List<ItemProto> upgradeList = [];
            var f1 = CreateAndPreAddNewFractionator(
                "精准分馏塔", RFE精准分馏塔, IFE精准分馏塔, MFE精准分馏塔,
                GenesisBook.Enable ? [IGB基础机械组件, I铁块, I玻璃, I电路板] : [I铁块, I石材, I玻璃, I电路板],
                GenesisBook.Enable ? [2, 4, 2, 1] : [4, 2, 2, 1],
                2601, TFE精准分馏, new(1.0f, 0.7019f, 0.4f), -20, 0.4f);
            upgradeList.Add(f1.Item3);
            var f2 = CreateAndPreAddNewFractionator(
                "建筑极速分馏塔", RFE建筑极速分馏塔, IFE建筑极速分馏塔, MFE建筑极速分馏塔,
                GenesisBook.Enable ? [IGB基础机械组件, I钢材, I玻璃, I电路板] : [I铁块, I石材, I玻璃, I电路板],
                GenesisBook.Enable ? [4, 8, 4, 1] : [8, 4, 4, 1],
                2602, TFE建筑极速分馏, new(0.4f, 1.0f, 0.949f), 0, 1.0f);
            upgradeList.Add(f2.Item3);
            var f3 = CreateAndPreAddNewFractionator(
                "垃圾回收分馏塔", RFE垃圾回收分馏塔, IFE垃圾回收分馏塔, MFE垃圾回收分馏塔,
                GenesisBook.Enable ? [IGB先进机械组件, I钛合金, I钛化玻璃, I处理器] : [I钢材, I石材, I玻璃, I处理器],
                GenesisBook.Enable ? [4, 8, 4, 1] : [8, 4, 4, 1],
                2603, TFE垃圾回收, new(0.4f, 1.0f, 0.5f), 0, 2.0f);
            var f4 = (
                RecipeFractionator,
                ModelFractionator,
                ItemFractionator);
            upgradeList.Add(f4.Item3);
            var f5 = CreateAndPreAddNewFractionator(
                "点数聚集分馏塔", RFE点数聚集分馏塔, IFE点数聚集分馏塔, MFE点数聚集分馏塔,
                GenesisBook.Enable
                    ? [IGB尖端机械组件, IGB钨合金, IGB钨强化玻璃, I量子芯片]
                    : [I钛合金, I石墨烯, I钛化玻璃, I处理器],
                GenesisBook.Enable ? [4, 8, 4, 1] : [8, 4, 4, 1],
                2605, TFE增产点数聚集, new(0.2509f, 0.8392f, 1.0f), 20, 2.5f);
            upgradeList.Add(f5.Item3);
            var f6 = CreateAndPreAddNewFractionator(
                "增产分馏塔", RFE增产分馏塔, IFE增产分馏塔, MFE增产分馏塔,
                GenesisBook.Enable
                    ? [IGB尖端机械组件, IGB三元精金, IGB钨强化玻璃, IGB光学处理器, I物质重组器]
                    : [I钛合金, I粒子宽带, I位面过滤器, I量子芯片, I物质重组器],
                GenesisBook.Enable ? [10, 20, 8, 1, 30] : [16, 8, 4, 1, 30],
                2606, TFE增产分馏, new(0.6235f, 0.6941f, 0.8f), 40, 2.0f);
            upgradeList.Add(f6.Item3);

            //设定升降级关系
            int[] upgradeItemIDList = upgradeList.Select(item => item.ID).ToArray();
            for (int i = 0; i < upgradeList.Count; i++) {
                ItemProto item = upgradeList[i];
                item.Upgrades = upgradeItemIDList;
                item.Grade = i + 1;
            }

            //适配创世
            if (GenesisBook.Enable) {
                f1.Item1.Type = GenesisBook.基础制造;
                f2.Item1.Type = GenesisBook.基础制造;
                f3.Item1.Type = GenesisBook.标准制造;
                f4.Item1.Type = GenesisBook.标准制造;
                f5.Item1.Type = GenesisBook.高精度加工;
                f6.Item1.Type = GenesisBook.高精度加工;
            }
        }

        /// <summary>
        /// 添加一个分馏塔，以及制作它的配方。
        /// </summary>
        /// <param name="name">分馏塔名称，用于名称显示、描述显示</param>
        /// <param name="recipeID">制作分馏塔配方id</param>
        /// <param name="itemID">分馏塔物品id</param>
        /// <param name="modelID">分馏塔模型ID</param>
        /// <param name="items">制作分馏塔需要的材料种类</param>
        /// <param name="itemCounts">制作分馏塔需要的材料个数</param>
        /// <param name="gridIndex">分馏塔在背包显示的位置（配方位置）、物流塔选择物品位置（物品位置）</param>
        /// <param name="preTechID">建筑和配方的前置科技</param>
        /// <param name="color">分馏塔颜色，只更改主体材质颜色，所以只需要一个颜色参数</param>
        /// <param name="hpAdjust">hp调节量（相比于原版分馏塔）</param>
        /// <param name="energyRatio">能耗比例（相比于原版分馏塔）</param>
        [SuppressMessage("ReSharper", "ParameterHidesMember")]
        public static (RecipeProto, ModelProto, ItemProto) CreateAndPreAddNewFractionator(
            string name, int recipeID, int itemID, int modelID,
            int[] items, int[] itemCounts,
            int gridIndex, int preTechID, Color color, int hpAdjust, float energyRatio) {
            ItemProto oriItem = ItemFractionator;
            ModelProto oriModel = ModelFractionator;
            RecipeProto oriRecipe = RecipeFractionator;
            PrefabDesc oriPrefabDesc = oriModel.prefabDesc;
            string iconPath = $"Assets/fracicons/fractionator-{gridIndex % 10}";

            //添加制作分馏塔的配方
            RecipeProto recipe = new();
            oriRecipe.CopyPropsTo(ref recipe);
            recipe.ID = recipeID;
            recipe.Name = name;
            recipe.Items = items;
            recipe.ItemCounts = itemCounts;
            recipe.Results = [itemID];
            recipe.ResultCounts = [1];
            recipe.GridIndex = gridIndex;
            recipe.IconPath = null;//建筑配方的图标设为null，将会自动使用产物[0]的图标
            recipe.preTech = null;
            LDBTool.PreAddProto(recipe);
            recipe.ID = recipeID;

            //添加分馏塔模型
            ModelProto model = new() {
                PrefabPath = oriModel.PrefabPath,
                ID = modelID,
                Name = modelID.ToString(),
                SID = name,
                Order = 38000 + modelID,
            };
            //新建筑必须使用新建的prefabDesc，不能复制，Preload可生成新的prefabDesc
            model.Preload();
            model.HpMax = oriModel.HpMax + hpAdjust * 100;
            model.prefabDesc.modelIndex = modelID;
            model.prefabDesc.workEnergyPerTick = (long)(model.prefabDesc.workEnergyPerTick * energyRatio);
            model.prefabDesc.idleEnergyPerTick = (long)(model.prefabDesc.idleEnergyPerTick * energyRatio);
            //更换材质的颜色。每个建筑的prefabDesc.lodMaterials长度都不一样，需要具体查看
            var m_main = new Material(oriPrefabDesc.lodMaterials[0][0]) { color = color };//主体材质
            var m_black = oriPrefabDesc.lodMaterials[0][1];//黑色材质不改
            var m_glass = oriPrefabDesc.lodMaterials[0][2];//玻璃材质不改
            var m_glass1 = oriPrefabDesc.lodMaterials[0][3];//玻璃材质不改
            var m_lod = new Material(oriPrefabDesc.lodMaterials[1][0]) { color = color };//缩小时看到的材质
            var m_lod2 = new Material(oriPrefabDesc.lodMaterials[2][0]) { color = color };//缩小时看到的材质
            //同样的材质要指向同一个对象
            model.prefabDesc.lodMaterials = [
                [m_main, m_black, m_glass, m_glass1],
                [m_lod, m_black, m_glass, m_glass1],
                [m_lod2, m_black, m_glass, m_glass1],
                null,
            ];
            model.prefabDesc.materials = [m_main, m_black];
            LDBTool.PreAddProto(model);

            //添加分馏塔物品
            ItemProto item = new();
            oriItem.CopyPropsTo(ref item);
            //应与model指向同一个prefabDesc
            item.prefabDesc = model.prefabDesc;
            item.ModelIndex = modelID;
            item.ID = itemID;
            item.Name = name;
            item.Description = "I" + name;
            item.GridIndex = gridIndex;
            item.IconPath = iconPath;
            item.preTech = null;
            item.recipes = [recipe];
            item.maincraft = recipe;
            item.handcraft = recipe;
            item.handcrafts = [recipe];
            item.BuildIndex = 0;//使用巨构双行样式，不占用原本的快捷制作栏，所以这里固定使用0
            //添加物品在传送带上的显示情况
            ref Dictionary<int, IconToolNew.IconDesc> itemIconDescs
                = ref AccessTools.StaticFieldRefAccess<Dictionary<int, IconToolNew.IconDesc>>(typeof(ProtoRegistry),
                    "itemIconDescs");
            IconToolNew.IconDesc iconDesc = new() {
                faceColor = Color.white,
                sideColor = color,
                faceEmission = Color.black,
                sideEmission = Color.black,
                iconEmission = Color.clear,
                metallic = 0.8f,
                smoothness = 0.5f,
                solidAlpha = 1f,
                iconAlpha = 1f,
            };
            itemIconDescs.Add(itemID, iconDesc);
            LDBTool.PreAddProto(item);
            //最后调用StorageComponent.LoadStatic()才能使物品的堆叠上限生效

            //设置快捷制作栏位置
            //使用两行快捷建造栏，不需要绑定到原有的BuildMenu上面了
            //LDBTool.SetBuildBar(buildIndex / 100, buildIndex % 100, item.ID);
            //BuildBar.Bind(5, gridIndex % 10, item.ID, 2);

            //添加科技解锁相关信息。可以避免万物分馏PreAddAction时，LDB中找不到创世科技的问题
            unlockList.Add(new() { itemID = itemID, recipeID = recipeID, preTechID = preTechID });

            return (recipe, model, item);
        }

        /// <summary>
        /// 关联物品、配方、前置科技
        /// </summary>
        public static void SetUnlockInfo() {
            //此时翻译字符串已经添加，再次Preload科技以更新其名称、描述、结论等
            Tech.PreloadAll();
            foreach (var unlock in unlockList) {
                //配方Preload会自动使用Results[0]的图标，所以先Preload item，再Preload recipe
                var item = LDB.items.Select(unlock.itemID);
                item.Preload(item.index);
                var recipe = LDB.recipes.Select(unlock.recipeID);
                recipe.Preload(recipe.index);
            }
        }

        /// <summary>
        /// 调整Model的缓存区大小，从而使分馏塔在传送带速度较高的情况下也能满带运行
        /// </summary>
        public static void SetFractionatorCacheSize() {
            foreach (var unlock in unlockList) {
                var prefabDesc = LDB.items.Select(unlock.itemID).prefabDesc;
                prefabDesc.fracFluidInputMax = FracFluidInputMax;
                prefabDesc.fracProductOutputMax = FracProductOutputMax;
                prefabDesc.fracFluidOutputMax = FracFluidOutputMax;
            }
        }

        /// <summary>
        /// 更改已放置的分馏塔的缓存区大小，从而使分馏塔在传送带速度较高的情况下也能满带运行
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(FractionatorComponent), "Import")]
        public static void FractionatorComponent_Import_Postfix(ref FractionatorComponent __instance) {
            __instance.fluidInputMax = FracFluidInputMax;
            __instance.productOutputMax = FracProductOutputMax;
            __instance.fluidOutputMax = FracFluidOutputMax;
        }
    }
}
