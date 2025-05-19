using BuildBarTool;
using CommonAPI.Systems;
using FractionateEverything.Compatibility;
using HarmonyLib;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;
using xiaoye97;
using static FractionateEverything.Utils.ProtoID;

namespace FractionateEverything.Logic;

public static class FracItemManager {
    private static readonly ModelProto FractionatorModel = LDB.models.Select(M分馏塔);
    public static readonly PrefabDesc FractionatorPrefabDesc = FractionatorModel.prefabDesc;
    public static readonly List<BuildingInfo> buildingInfoList = [];

    public static void CreateAndPreAddNewFractionators() {
        //assembler-mk-1至assembler-mk-4，但对于分馏塔而言太暗，需要适当增加亮度
        //new(1.0f, 0.6596f, 0.3066f)
        //new(0.0f, 1.0f, 0.9112f)
        //new(0.3726f, 0.8f, 1.0f)
        //new(0.549f, 0.5922f, 0.6235f)

        //创世解锁科技：蓝 蓝 蓝 红 紫 绿
        //[IGB基础机械组件, I铁块, I玻璃, I电路板][2, 4, 2, 1]
        //[IGB基础机械组件, I钢材, I玻璃, I电路板][4, 8, 4, 1] [IGB基础机械组件, I钢材, I玻璃, I电路板][4, 8, 4, 1]
        //[IGB先进机械组件, I钛合金, I钛化玻璃, I处理器][4, 8, 4, 1]
        //[IGB尖端机械组件, IGB钨合金, IGB钨强化玻璃, I量子芯片][4, 8, 4, 1]
        //[IGB尖端机械组件, IGB三元精金, IGB钨强化玻璃, IGB光学处理器, I物质重组器][10, 20, 8, 1, 30]

        //原版解锁科技：蓝 蓝 蓝 红 紫 绿
        //[I铁块, I石材, I玻璃, I电路板][4, 2, 2, 1]
        //[I钢材, I石材, I玻璃, I电路板][8, 4, 4, 1] [I钢材, I石材, I玻璃, I电路板][8, 4, 4, 1]
        //[I钢材, I石材, I玻璃, I处理器][8, 4, 4, 1]
        //[I钛合金, I石墨烯, I钛化玻璃, I处理器][8, 4, 4, 1]
        //[I钛合金, I粒子宽带, I位面过滤器, I量子芯片, I物质重组器][16, 8, 4, 1, 30]

        //创建新建筑
        var f1 = CreateAndPreAddNewFractionator(
            "自然资源分馏塔", RFE自然资源分馏塔, IFE自然资源分馏塔, MFE自然资源分馏塔,
            GenesisBook.Enable ? [IGB基础机械组件, I铁块, I玻璃, I电路板] : [I铁块, I石材, I玻璃, I电路板],
            GenesisBook.Enable ? [2, 4, 2, 1] : [4, 2, 2, 1],
            2601, new(1.0f, 0.7019f, 0.4f), -20, 0.4f);
        var f2 = CreateAndPreAddNewFractionator(
            "升级分馏塔", RFE升级分馏塔, IFE升级分馏塔, MFE升级分馏塔,
            GenesisBook.Enable ? [IGB基础机械组件, I钢材, I玻璃, I电路板] : [I钢材, I石材, I玻璃, I电路板],
            GenesisBook.Enable ? [4, 8, 4, 1] : [8, 4, 4, 1],
            2602, new(0.4f, 1.0f, 0.949f), 0, 1.0f);
        var f3 = CreateAndPreAddNewFractionator(
            "降级分馏塔", RFE降级分馏塔, IFE降级分馏塔, MFE降级分馏塔,
            GenesisBook.Enable ? [IGB基础机械组件, I钢材, I玻璃, I电路板] : [I钢材, I石材, I玻璃, I电路板],
            GenesisBook.Enable ? [4, 8, 4, 1] : [8, 4, 4, 1],
            2603, new(0.7f, 0.6f, 0.8f), 0, 1.0f);
        var f4 = CreateAndPreAddNewFractionator(
            "垃圾回收分馏塔", RFE垃圾回收分馏塔, IFE垃圾回收分馏塔, MFE垃圾回收分馏塔,
            GenesisBook.Enable ? [IGB先进机械组件, I钛合金, I钛化玻璃, I处理器] : [I钢材, I石材, I玻璃, I处理器],
            GenesisBook.Enable ? [4, 8, 4, 1] : [8, 4, 4, 1],
            2604, new(0.4f, 1.0f, 0.5f), 0, 2.0f);
        var f5 = CreateAndPreAddNewFractionator(
            "点数聚集分馏塔", RFE点数聚集分馏塔, IFE点数聚集分馏塔, MFE点数聚集分馏塔,
            GenesisBook.Enable
                ? [IGB尖端机械组件, IGB钨合金, IGB钨强化玻璃, I量子芯片]
                : [I钛合金, I石墨烯, I钛化玻璃, I处理器],
            GenesisBook.Enable ? [4, 8, 4, 1] : [8, 4, 4, 1],
            2605, new(0.2509f, 0.8392f, 1.0f), 20, 2.5f);
        var f6 = CreateAndPreAddNewFractionator(
            "增产分馏塔", RFE增产分馏塔, IFE增产分馏塔, MFE增产分馏塔,
            GenesisBook.Enable
                ? [IGB尖端机械组件, IGB三元精金, IGB钨强化玻璃, IGB光学处理器, I物质重组器]
                : [I钛合金, I粒子宽带, I位面过滤器, I量子芯片, I物质重组器],
            GenesisBook.Enable ? [10, 20, 8, 1, 30] : [16, 8, 4, 1, 30],
            2606, new(0.6235f, 0.6941f, 0.8f), 40, 2.0f);
        var f7 = CreateAndPreAddNewFractionator(
            "老虎机分馏塔", RFE老虎机分馏塔, IFE老虎机分馏塔, MFE老虎机分馏塔,
            GenesisBook.Enable ? [IGB基础机械组件, I铁块, I玻璃, I电路板] : [I铁块, I石材, I玻璃, I电路板],
            GenesisBook.Enable ? [2, 4, 2, 1] : [4, 2, 2, 1],
            2607, new(0.8f, 0.3f, 0.6f), 0, 0.0f);

        //设定升降级关系
        //原版分馏塔可以升级为自然资源分馏塔，以适配BPT的替换建筑
        ItemProto originFractionator = LDB.items.Select(I分馏塔);
        originFractionator.Upgrades = [I分馏塔, f1.Item3.ID];
        originFractionator.Grade = 1;
        //其他分馏塔形成升级链（垃圾回收、老虎机除外）
        List<ItemProto> upgradeList = [
            f1.Item3, f2.Item3, f3.Item3, f5.Item3, f6.Item3
        ];
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
            f3.Item1.Type = GenesisBook.基础制造;
            f4.Item1.Type = GenesisBook.标准制造;
            f5.Item1.Type = GenesisBook.高精度加工;
            f6.Item1.Type = GenesisBook.高精度加工;
            f7.Item1.Type = GenesisBook.基础制造;
        }
    }

    /// <summary>
    /// 添加一个分馏塔，以及制作它的配方
    /// </summary>
    /// <param name="name">分馏塔名称，用于名称显示、描述显示</param>
    /// <param name="recipeID">制作分馏塔配方id</param>
    /// <param name="itemID">分馏塔物品id</param>
    /// <param name="modelID">分馏塔模型ID</param>
    /// <param name="items">制作分馏塔需要的材料种类</param>
    /// <param name="itemCounts">制作分馏塔需要的材料个数</param>
    /// <param name="gridIndex">分馏塔在背包显示的位置（配方位置）、物流塔选择物品位置（物品位置）</param>
    /// <param name="color">分馏塔颜色，只更改主体材质颜色，所以只需要一个颜色参数</param>
    /// <param name="hpAdjust">hp调节量（相比于原版分馏塔）</param>
    /// <param name="energyRatio">能耗比例（相比于原版分馏塔）</param>
    [SuppressMessage("ReSharper", "ParameterHidesMember")]
    private static (RecipeProto, ModelProto, ItemProto) CreateAndPreAddNewFractionator(
        string name, int recipeID, int itemID, int modelID,
        int[] items, int[] itemCounts,
        int gridIndex, Color color, int hpAdjust, float energyRatio) {
        ModelProto oriModel = FractionatorModel;
        PrefabDesc oriPrefabDesc = oriModel.prefabDesc;
        string iconPath = $"Assets/fracicons/fractionator-{gridIndex % 10}";

        //添加制作分馏塔的配方，IconPath和preTech都无需设定，Preload会自动生成
        RecipeProto recipe = new() {
            Type = ERecipeType.Assemble,
            ID = recipeID,
            SID = "",
            Name = name,
            Description = "",
            GridIndex = gridIndex,
            IconPath = "",//IconPath为空时，会自动使用产物的图标
            Items = items,
            ItemCounts = itemCounts,
            Results = [itemID],
            ResultCounts = [1],
            Handcraft = true,
            TimeSpend = 180,
        };
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
        model.ID = modelID;

        //添加分馏塔物品，preTech无需设定，Preload会自动生成
        //由于使用双行样式，BuildIndex只要保持默认值0即可
        ItemProto item = new() {
            Type = EItemType.Production,
            ID = itemID,
            SID = "",
            Name = name,
            Description = "I" + name,
            GridIndex = gridIndex,
            IconPath = iconPath,
            StackSize = 30,
            DescFields = [53, 11, 12, 1, 40],
            HpMax = 1200,
            recipes = [recipe],
            makes = [],
            maincraft = recipe,
            maincraftProductCount = 1,
            handcraft = recipe,
            handcraftProductCount = 1,
            handcrafts = [recipe],
            MiningFrom = "",
            ProduceFrom = "",
            Upgrades = [],
            Grade = 0,
            BuildMode = 1,
            CanBuild = true,
            IsEntity = true,
            ModelIndex = modelID,
            ModelCount = 1,
            prefabDesc = model.prefabDesc,//应与model指向同一个prefabDesc
        };
        if (itemID == IFE垃圾回收分馏塔 && FractionateEverything.enableBuildingAsTrash) {
            item.Description += "2";
        }
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
        item.ID = itemID;
        //最后调用StorageComponent.LoadStatic()才能使物品的堆叠上限生效

        //设置快捷制作栏位置
        item.SetBuildBar(5, item.GridIndex % 10, true);

        //添加科技解锁相关信息。可以避免万物分馏PreAddAction时，LDB中找不到创世科技的问题
        buildingInfoList.Add(new() { itemID = itemID, recipeID = recipeID });

        return (recipe, model, item);
    }

    /// <summary>
    /// 关联物品、配方、前置科技，以使分馏塔在正确的科技被解锁
    /// </summary>
    public static void PreloadAll() {
        //此时翻译字符串已经添加，再次Preload科技以更新其名称、描述、结论等
        foreach (var unlock in buildingInfoList) {
            //配方Preload会自动使用Results[0]的图标，所以先Preload item，再Preload recipe
            var item = LDB.items.Select(unlock.itemID);
            item.Preload(item.index);
            var recipe = LDB.recipes.Select(unlock.recipeID);
            recipe.Preload(recipe.index);
        }
    }
}
