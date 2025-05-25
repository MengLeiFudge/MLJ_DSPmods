using BuildBarTool;
using CommonAPI.Systems;
using FE.Compatibility;
using HarmonyLib;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;
using xiaoye97;
using static FE.Utils.ProtoID;

namespace FE.Logic;

public static class BuildingManager {
    private static readonly ModelProto FractionatorModel = LDB.models.Select(M分馏塔);
    public static readonly PrefabDesc FractionatorPrefabDesc = FractionatorModel.prefabDesc;
    public static readonly List<BuildingInfo> buildingInfoList = [];

    public static void CreateAndPreAddNewFractionators() {
        //assembler-mk-1至assembler-mk-4，但对于分馏塔而言太暗，需要适当增加亮度
        //new(1.0f, 0.6596f, 0.3066f)
        //new(0.0f, 1.0f, 0.9112f)
        //new(0.3726f, 0.8f, 1.0f)
        //new(0.549f, 0.5922f, 0.6235f)

        //创建新建筑
        var f11 = CreateAndPreAddNewFractionator(
            "交互塔", IFE交互塔, MFE交互塔, 2601, new(0.8f, 0.3f, 0.6f), -50, 2.5f);
        f11.Item3.SetBuildBar(5, 1, true);
        var f21 = CreateAndPreAddNewFractionator(
            "矿物复制塔", IFE矿物复制塔, MFE矿物复制塔, 2602, new(1.0f, 0.7019f, 0.4f), -20, 0.4f);
        f21.Item3.SetBuildBar(5, 2, true);
        var f31 = CreateAndPreAddNewFractionator(
            "转化塔MK1", IFE转化塔MK1, MFE转化塔MK1, 2603, new(0.4f, 1.0f, 0.949f), 0, 1.0f);
        f31.Item3.SetBuildBar(6, 1, true);
        var f32 = CreateAndPreAddNewFractionator(
            "转化塔MK2", IFE转化塔MK2, MFE转化塔MK2, 2604, new(0.7f, 0.6f, 0.8f), 0, 1.0f);
        f32.Item3.SetBuildBar(6, 2, true);
        var f33 = CreateAndPreAddNewFractionator(
            "转化塔MK3", IFE转化塔MK3, MFE转化塔MK3, 2605, new(0.4f, 1.0f, 0.5f), 0, 1.0f);
        f33.Item3.SetBuildBar(6, 3, true);
        var f34 = CreateAndPreAddNewFractionator(
            "转化塔MK4", IFE转化塔MK4, MFE转化塔MK4, 2606, new(0.4f, 1.0f, 0.949f), 0, 1.0f);
        f34.Item3.SetBuildBar(6, 4, true);
        var f35 = CreateAndPreAddNewFractionator(
            "转化塔MK5", IFE转化塔MK5, MFE转化塔MK5, 2607, new(0.4f, 1.0f, 0.949f), 0, 1.0f);
        f35.Item3.SetBuildBar(6, 5, true);
        var f36 = CreateAndPreAddNewFractionator(
            "转化塔MK6", IFE转化塔MK6, MFE转化塔MK6, 2608, new(0.4f, 1.0f, 0.949f), 0, 1.0f);
        f36.Item3.SetBuildBar(6, 6, true);
        var f37 = CreateAndPreAddNewFractionator(
            "转化塔MK7", IFE转化塔MK7, MFE转化塔MK7, 2609, new(0.4f, 1.0f, 0.949f), 0, 1.0f);
        f37.Item3.SetBuildBar(6, 7, true);
        var f41 = CreateAndPreAddNewFractionator(
            "点数聚集塔", IFE点数聚集塔, MFE点数聚集塔, 2710, new(0.2509f, 0.8392f, 1.0f), 0, 1.0f);
        f41.Item3.SetBuildBar(5, 4, true);
        var f51 = CreateAndPreAddNewFractionator(
            "量子复制塔", IFE量子复制塔, MFE量子复制塔, 2711, new(0.6235f, 0.6941f, 0.8f), 0, 1.0f);
        f51.Item3.SetBuildBar(5, 5, true);

        // //设定升降级关系
        // //原版分馏塔可以升级为矿物复制塔，以适配BPT的替换建筑
        // ItemProto originFractionator = LDB.items.Select(I分馏塔);
        // originFractionator.Upgrades = [I分馏塔, f1.Item3.ID];
        // originFractionator.Grade = 1;
        // //其他分馏塔形成升级链（垃圾回收、老虎机除外）
        // List<ItemProto> upgradeList = [
        //     f1.Item3, f2.Item3, f3.Item3, f5.Item3, f6.Item3
        // ];
        // int[] upgradeItemIDList = upgradeList.Select(item => item.ID).ToArray();
        // for (int i = 0; i < upgradeList.Count; i++) {
        //     ItemProto item = upgradeList[i];
        //     item.Upgrades = upgradeItemIDList;
        //     item.Grade = i + 1;
        // }
        //
        // //适配创世
        // if (GenesisBook.Enable) {
        //     f1.Item1.Type = GenesisBook.基础制造;
        //     f2.Item1.Type = GenesisBook.基础制造;
        //     f3.Item1.Type = GenesisBook.基础制造;
        //     f4.Item1.Type = GenesisBook.标准制造;
        //     f5.Item1.Type = GenesisBook.高精度加工;
        //     f6.Item1.Type = GenesisBook.高精度加工;
        //     f7.Item1.Type = GenesisBook.基础制造;
        // }
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
        string name, int itemID, int modelID,
        int gridIndex, Color color, int hpAdjust, float energyRatio) {
        ModelProto oriModel = FractionatorModel;
        PrefabDesc oriPrefabDesc = oriModel.prefabDesc;
        string iconPath = $"Assets/fracicons/fractionator-{name}";

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
            recipes = [],
            makes = [],
            maincraft = null,
            maincraftProductCount = 1,
            handcraft = null,
            handcraftProductCount = 1,
            handcrafts = [],
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
        // item.SetBuildBar(5, item.GridIndex % 10, true);

        //添加科技解锁相关信息。可以避免万物分馏PreAddAction时，LDB中找不到创世科技的问题
        // buildingInfoList.Add(new() { itemID = itemID, recipeID = recipeID });

        return (null, model, item);
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
            // var recipe = LDB.recipes.Select(unlock.recipeID);
            // recipe.Preload(recipe.index);
        }
    }
}
