using CommonAPI;
using HarmonyLib;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using xiaoye97;

namespace FractionateEverything.Utils {
    public static class FractionatorUtils {
        public static RecipeProto RecipeFractionator;
        public static ModelProto ModelFractionator;
        public static ItemProto ItemFractionator;

        public static (RecipeProto, ModelProto, ItemProto) CreateAndPreAddNewFractionator(
            string name, int recipeID, int itemID, int modelID,
            int[] items, int[] itemCounts,
            int gridIndex, int preTech, int buildIndex, Color color, double energyRatio = 1.0) {
            return CreateAndPreAddNewFractionator(name, recipeID, itemID, modelID, items, itemCounts, gridIndex,
                LDB.techs.Select(preTech), buildIndex, color, energyRatio);
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
        /// <param name="preTech">建筑和配方的前置科技</param>
        /// <param name="buildIndex">在下方快捷制作栏的哪个位置</param>
        /// <param name="color">分馏塔颜色，只更改主体材质颜色，所以只需要一个颜色参数</param>
        /// <param name="energyRatio">能耗比例（相比于原版分馏塔）</param>
        [SuppressMessage("ReSharper", "ParameterHidesMember")]
        public static (RecipeProto, ModelProto, ItemProto) CreateAndPreAddNewFractionator(
            string name, int recipeID, int itemID, int modelID,
            int[] items, int[] itemCounts,
            int gridIndex, TechProto preTech, int buildIndex, Color color, double energyRatio = 1.0) {
            ItemProto oriItem = ItemFractionator;
            ModelProto oriModel = ModelFractionator;
            RecipeProto oriRecipe = RecipeFractionator;
            PrefabDesc oriPrefabDesc = oriModel.prefabDesc;
            string iconPath = $"Assets/fracicons/fractionator-{gridIndex % 10}";
            Sprite sprite = Resources.Load<Sprite>(iconPath);

            //添加制作分馏塔的配方
            RecipeProto recipe = new();
            oriRecipe.CopyPropsTo(ref recipe);
            recipe.ID = recipeID;
            recipe.Name = name;
            recipe.name = name.Translate();
            recipe.Items = items;
            recipe.ItemCounts = itemCounts;
            recipe.Results = [itemID];
            recipe.ResultCounts = [1];
            recipe.GridIndex = gridIndex;
            recipe.IconPath = iconPath;
            Traverse.Create(recipe).Field("_iconSprite").SetValue(sprite);
            recipe.preTech = preTech;
            LDBTool.PreAddProto(recipe);
            recipe.ID = recipeID;

            //添加分馏塔模型
            ModelProto model = new();
            oriModel.CopyPropsTo(ref model);
            //prefabDesc必须new一个对象，不能使用Copy或者CopyPropsTo
            var _prefab = Resources.Load<GameObject>(model.PrefabPath);
            var _colliderPrefab = Resources.Load<GameObject>(model.ColliderPath);
            model.prefabDesc = !_prefab || !_colliderPrefab
                ? !_prefab
                    ? PrefabDesc.none
                    : new(modelID, _prefab)
                : new(modelID, _prefab, _colliderPrefab);
            model.ID = modelID;
            model.Name = modelID.ToString();
            model.name = modelID.ToString();
            model.SID = name;
            model.sid = name;
            model.Order = 38000 + modelID;
            Traverse.Create(model).Field("_iconSprite").SetValue(sprite);
            model.prefabDesc.modelIndex = modelID;
            model.prefabDesc.workEnergyPerTick = (long)(model.prefabDesc.workEnergyPerTick * energyRatio);
            model.prefabDesc.idleEnergyPerTick = (long)(model.prefabDesc.idleEnergyPerTick * energyRatio);
            //更换材质的颜色。每个建筑的prefabDesc.lodMaterials长度都不一样，需要具体查看
            //分馏塔只需要换主体材质m_main的颜色
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
            // LDB.models.OnAfterDeserialize();
            // ModelProto.InitMaxModelIndex();
            // ModelProto.InitModelIndices();
            // ModelProto.InitModelOrders();
            LDBTool.PreAddProto(model);

            //添加分馏塔物品
            ItemProto item = new();
            oriItem.CopyPropsTo(ref item);
            //应与model指向同一个prefabDesc
            item.prefabDesc = model.prefabDesc;
            item.ModelIndex = modelID;
            item.ID = itemID;
            item.Name = name;
            item.name = name.Translate();
            item.Description = "I" + name;
            item.description = ("I" + name).Translate();
            item.GridIndex = gridIndex;
            item.IconPath = iconPath;
            Traverse.Create(item).Field("_iconSprite").SetValue(sprite);
            item.preTech = preTech;
            item.recipes = [recipe];
            item.maincraft = recipe;
            item.handcraft = recipe;
            item.handcrafts = [recipe];
            item.BuildIndex = buildIndex;
            LDBTool.PreAddProto(item);

            //设置快捷制作栏位置
            LDBTool.SetBuildBar(buildIndex / 100, buildIndex % 100, item.ID);

            return (recipe, model, item);
        }
    }
}
