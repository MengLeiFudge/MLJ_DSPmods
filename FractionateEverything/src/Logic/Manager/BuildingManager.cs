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

    private static ModelProto FractionatorModel => LDB.models.Select(M分馏塔);
    public static PrefabDesc FractionatorPrefabDesc => FractionatorModel.prefabDesc;
    private static readonly List<(RecipeProto, ModelProto, ItemProto)> buildingList = [];

    public static void AddFractionators() {
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
        pointAggregatorTower.Item3.SetBuildBar(5, 4, true);

        //量子复制塔
        var quantumCopyTower = QuantumCopyTower.Create();
        quantumCopyTower.Item3.SetBuildBar(5, 5, true);

        //点金塔
        var alchemyTower = AlchemyTower.Create();
        alchemyTower.Item3.SetBuildBar(5, 6, true);

        //分解塔
        var deconstructionTower = DeconstructionTower.Create();
        deconstructionTower.Item3.SetBuildBar(5, 7, true);

        //转化塔
        var conversionTower = ConversionTower.Create();
        conversionTower.Item3.SetBuildBar(5, 8, true);


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
    public static (RecipeProto, ModelProto, ItemProto) CreateFractionator(
        string name, int recipeID, int itemID, int modelID, int[] items, int[] itemCounts, int[] resultCounts,
        int gridIndex, Color color, int hpAdjust, float energyRatio, int techID) {
        ModelProto oriModel = FractionatorModel;
        PrefabDesc oriPrefabDesc = oriModel.prefabDesc;
        string iconPath = $"Assets/fracicons/fractionator-{name}";

        ItemProto item = ProtoRegistry.RegisterItem(itemID, name, "I" + name, iconPath,
            gridIndex, 30, EItemType.Production, ProtoRegistry.GetDefaultIconDesc(Color.white, color));
        RecipeProto recipe = ProtoRegistry.RegisterRecipe(recipeID, ERecipeType.Assemble, 60,
            items, itemCounts, [itemID], resultCounts, "I" + name, techID, gridIndex, name, iconPath);
        ModelProto model = ProtoRegistry.RegisterModel(modelID, item, oriModel.PrefabPath,
            null, [53, 11, 12, 1, 40], 0);
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

        // ProtoRegistry.AddLodMaterials(model.PrefabPath, 0, [m_main, m_black, m_glass, m_glass1]);
        // ProtoRegistry.AddLodMaterials(model.PrefabPath, 1, [m_lod, m_black, m_glass, m_glass1]);
        // ProtoRegistry.AddLodMaterials(model.PrefabPath, 2, [m_lod2, m_black, m_glass, m_glass1]);

        //设置快捷制作栏位置
        item.SetBuildBar(5, item.GridIndex % 10, true);

        //添加科技解锁相关信息。可以避免万物分馏PreAddAction时，LDB中找不到创世科技的问题
        buildingList.Add((recipe, model, item));

        return (recipe, model, item);
    }

    #endregion

    #region 在PostAdd之后根据已添加的物品修改部分属性

    /// <summary>
    /// 调整Model的缓存区大小，从而使分馏塔在传送带速度较高的情况下也能满带运行
    /// </summary>
    public static void SetFractionatorCacheSize() {
        foreach (var p in buildingList) {
            if (p.Item3.prefabDesc.isFractionator) {
                var prefabDesc = LDB.items.Select(p.Item3.ID).prefabDesc;
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

    public static Dictionary<int, int> otherProductOutput(this FractionatorComponent fractionator,
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
