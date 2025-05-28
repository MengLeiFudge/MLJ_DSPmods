using BuildBarTool;
using CommonAPI.Systems;
using FE.Logic.Building;
using HarmonyLib;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using UnityEngine;
using xiaoye97;
using static FE.Utils.ProtoID;
using static FE.Logic.Manager.ProcessManager;

namespace FE.Logic.Manager;

public static class BuildingManager {
    #region 在PreAdd阶段添加新建筑

    private static readonly ModelProto FractionatorModel = LDB.models.Select(M分馏塔);
    public static readonly PrefabDesc FractionatorPrefabDesc = FractionatorModel.prefabDesc;
    private static List<ItemProto> buildingList = [];

    public static void CreateAndPreAddNewFractionators() {
        //assembler-mk-1至assembler-mk-4，但对于分馏塔而言太暗，需要适当增加亮度
        //new(1.0f, 0.6596f, 0.3066f)
        //new(0.0f, 1.0f, 0.9112f)
        //new(0.3726f, 0.8f, 1.0f)
        //new(0.549f, 0.5922f, 0.6235f)

        //交互塔
        var interactionTower = InteractionTower.Create();
        interactionTower.Item3.SetBuildBar(5, 1, true);

        //矿物复制塔
        var mineralCopyTower = MineralCopyTower.Create();
        mineralCopyTower.Item3.SetBuildBar(5, 2, true);

        //点数聚集塔
        var pointAggregatorTower = PointAggregatorTower.Create();
        pointAggregatorTower.Item3.SetBuildBar(5, 3, true);

        //量子复制塔
        var quantumCopyTower = QuantumCopyTower.Create();
        quantumCopyTower.Item3.SetBuildBar(5, 4, true);

        //点金塔
        var alchemyTower = AlchemyTower.Create();
        alchemyTower.Item3.SetBuildBar(6, 1, true);

        //转化塔MK1-MK7
        var conversionTowers = ConversionTower.CreateAll();
        conversionTowers[0].Item3.SetBuildBar(6, 2, true);
        conversionTowers[1].Item3.SetBuildBar(6, 3, true);
        conversionTowers[2].Item3.SetBuildBar(6, 4, true);
        conversionTowers[3].Item3.SetBuildBar(6, 5, true);
        conversionTowers[4].Item3.SetBuildBar(6, 6, true);
        conversionTowers[5].Item3.SetBuildBar(6, 7, true);
        conversionTowers[6].Item3.SetBuildBar(6, 8, true);

        //分解塔
        var deconstructionTower = DeconstructionTower.Create();
        deconstructionTower.Item3.SetBuildBar(6, 9, true);


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
    public static (RecipeProto, ModelProto, ItemProto) CreateAndPreAddNewFractionator(
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
        buildingList.Add(item);

        return (null, model, item);
    }

    #endregion


    #region 在PostAdd之后根据已添加的物品修改部分属性

    /// <summary>
    /// 调整Model的缓存区大小，从而使分馏塔在传送带速度较高的情况下也能满带运行
    /// </summary>
    public static void SetFractionatorCacheSize() {
        foreach (var building in buildingList) {
            if (building.prefabDesc.isFractionator) {
                var prefabDesc = LDB.items.Select(building.ID).prefabDesc;
                prefabDesc.fracFluidInputMax = FracFluidInputMax;
                prefabDesc.fracProductOutputMax = FracProductOutputMax;
                prefabDesc.fracFluidOutputMax = FracFluidOutputMax;
            }
        }
    }

    /// <summary>
    /// 更改已放置的分馏塔的缓存区大小，从而使分馏塔在传送带速度较高的情况下也能满带运行
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(FractionatorComponent), nameof(FractionatorComponent.Import))]
    public static void FractionatorComponent_Import_Postfix(ref FractionatorComponent __instance) {
        __instance.fluidInputMax = FracFluidInputMax;
        __instance.productOutputMax = FracProductOutputMax;
        __instance.fluidOutputMax = FracFluidOutputMax;
    }

    #endregion

    #region 分馏塔字段拓展

    /// <summary>
    /// 存储分馏塔所有副产物。结构：
    /// (planetId, entityId) => Dictionary&lt;itemId, itemCount&gt;
    /// </summary>
    private static readonly ConcurrentDictionary<(int, int), Dictionary<int, int>> outputExtend = [];

    public static void Import(BinaryReader r) {
        outputExtend.Clear();
        int fractionatorNum = r.ReadInt32();
        for (int i = 0; i < fractionatorNum; i++) {
            int planetId = r.ReadInt32();
            int entityId = r.ReadInt32();
            Dictionary<int, int> outputDic = [];
            int outputKinds = r.ReadInt32();
            for (int j = 0; j < outputKinds; j++) {
                int outputId = r.ReadInt32();
                int outputCount = r.ReadInt32();
                if (LDB.items.Select(outputId) == null) {
                    continue;
                }
                outputDic.Add(outputId, outputCount);
            }
            outputExtend.TryAdd((planetId, entityId), outputDic);
        }
    }

    public static void Export(BinaryWriter w) {
        w.Write(outputExtend.Count);
        foreach (var p in outputExtend) {
            w.Write(p.Key.Item1);
            w.Write(p.Key.Item2);
            Dictionary<int, int> outputDic = outputExtend[p.Key];
            //去除所有物品数目为0的情况，节约存储体积
            List<int> keys = outputDic.Keys.Where(Key => outputDic[Key] > 0).ToList();
            w.Write(keys.Count);
            for (int i = 0; i < keys.Count; i++) {
                w.Write(keys[i]);
                w.Write(outputDic[keys[i]]);
            }
        }
    }

    public static void IntoOtherSave() {
        outputExtend.Clear();
    }

    public static Dictionary<int, int> productExpansion(this FractionatorComponent fractionator,
        PlanetFactory factory) {
        int planetId = factory.planetId;
        int entityId = fractionator.entityId;
        if (!outputExtend.ContainsKey((planetId, entityId))) {
            outputExtend.TryAdd((planetId, entityId), []);
        }
        return outputExtend[(planetId, entityId)];
    }

    #endregion
}
