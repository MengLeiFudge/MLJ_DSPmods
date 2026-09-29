using System;
using System.IO;
using HarmonyLib;
using static FE.Logic.Fractionation.Fractionators.ProcessManager;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>
/// 分馏塔原型注册、缓存扩展、固定运行属性和分馏塔实例状态存档入口。
/// </summary>
public static class FractionatorManager {
    public static void AddTranslations() {
        BasicMineralTower.AddTranslations();
        BasicAnalysisTower.AddTranslations();
        AdvancedMineralTower.AddTranslations();
        AdvancedInteractionTower.AddTranslations();
        AdvancedConversionTower.AddTranslations();
    }

    public static void AddFractionators() {
        //assembler-mk-1至assembler-mk-4，但对于分馏塔而言太暗，需要适当增加亮度
        //new(1.0f, 0.6596f, 0.3066f)
        //new(0.0f, 1.0f, 0.9112f)
        //new(0.3726f, 0.8f, 1.0f)
        //new(0.549f, 0.5922f, 0.6235f)

        BasicMineralTower.Create();
        BasicAnalysisTower.Create();
        AdvancedMineralTower.Create();
        AdvancedInteractionTower.Create();
        AdvancedConversionTower.Create();
    }

    public static void SetFractionatorMaterial() {
        BasicMineralTower.SetMaterial();
        BasicAnalysisTower.SetMaterial();
        AdvancedMineralTower.SetMaterial();
        AdvancedInteractionTower.SetMaterial();
        AdvancedConversionTower.SetMaterial();
    }

    public static void UpdateHpAndEnergy() {
        BasicMineralTower.UpdateHpAndEnergy();
        BasicAnalysisTower.UpdateHpAndEnergy();
        AdvancedMineralTower.UpdateHpAndEnergy();
        AdvancedInteractionTower.UpdateHpAndEnergy();
        AdvancedConversionTower.UpdateHpAndEnergy();
    }

    /// <summary>
    /// 原版分馏塔的输入缓存上限，单位是堆（原版按输入货物数计，不按件数）。
    /// </summary>
    /// <remarks>原版处理速度取 min(输入堆数, 30) × 平均堆叠，至少缓存 30 堆才能跑满传送带，40 堆留出余量。</remarks>
    public const int VanillaFluidInputCargoMax = 40;

    /// <summary>
    /// 按当前全局堆叠刷新分馏塔原型和已放置分馏塔的缓存上限。
    /// </summary>
    /// <remarks>
    /// <para>五座 FE 塔的三项上限都是 20 堆；原版分馏塔输入固定 40 堆，两项输出为 20 堆。</para>
    /// <para>FE 塔运行时直接读当前上限，这些原版字段供原版逻辑、窗口和生产统计读取。</para>
    /// </remarks>
    public static void SetFractionatorCacheSize() {
        foreach (ModelProto modelProto in LDB.models.dataArray) {
            if (modelProto.prefabDesc.isFractionator) {
                modelProto.prefabDesc.fracFluidInputMax =
                    GetFluidInputMax(FractionatorTowerCatalog.IsActiveFractionatorModel(modelProto.ID));
                modelProto.prefabDesc.fracProductOutputMax = BaseFracProductOutputMax;
                modelProto.prefabDesc.fracFluidOutputMax = BaseFracFluidOutputMax;
            }
        }

        // 原型只影响新建的塔；已放置的塔在建造或读档时复制过旧值，需要逐个同步。
        GameData data = GameMain.data;
        if (data?.factories == null) {
            return;
        }
        for (int i = 0; i < data.factoryCount; i++) {
            SyncPlacedFractionatorCaches(data.factories[i]?.factorySystem);
        }
    }

    /// <summary>
    /// 读档后按塔型写入已放置分馏塔的缓存上限。
    /// </summary>
    /// <remarks>实体池先于分馏组件读入，此时已能按建筑 ID 区分 FE 塔与原版塔。</remarks>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.Import))]
    public static void FactorySystem_Import_Postfix(FactorySystem __instance) {
        SyncPlacedFractionatorCaches(__instance);
    }

    /// <summary>
    /// 同步一个星球上所有已放置分馏塔的缓存上限。
    /// </summary>
    /// <param name="factorySystem">星球的工厂系统；为空时不处理。</param>
    private static void SyncPlacedFractionatorCaches(FactorySystem factorySystem) {
        FractionatorComponent[] pool = factorySystem?.fractionatorPool;
        EntityData[] entityPool = factorySystem?.factory?.entityPool;
        if (pool == null || entityPool == null) {
            return;
        }
        int cursor = Math.Min(factorySystem.fractionatorCursor, pool.Length);
        for (int i = 1; i < cursor; i++) {
            ref FractionatorComponent fractionator = ref pool[i];
            if (fractionator.id != i) {
                continue;
            }
            int entityId = fractionator.entityId;
            bool activeFractionator = entityId > 0 && entityId < entityPool.Length
                && FractionatorTowerCatalog.IsActiveFractionator(entityPool[entityId].protoId);
            fractionator.fluidInputMax = GetFluidInputMax(activeFractionator);
            fractionator.productOutputMax = BaseFracProductOutputMax;
            fractionator.fluidOutputMax = BaseFracFluidOutputMax;
        }
    }

    /// <summary>
    /// 返回原版字段 fluidInputMax 应写入的值。
    /// </summary>
    /// <param name="activeFractionator">是否为五座 FE 塔之一。</param>
    /// <returns>FE 塔为件数上限（20 堆），原版塔为堆数上限（40 堆）。</returns>
    private static int GetFluidInputMax(bool activeFractionator) =>
        activeFractionator ? BaseFracFluidInputCargoMax : VanillaFluidInputCargoMax;

    /// <summary>
    /// 返回流动输入缓存上限：20 堆 × 当前全局堆叠。
    /// </summary>
    public static int FluidInputCargoMax(this ItemProto fractionator) {
        return BaseFracFluidInputCargoMax;
    }

    /// <summary>
    /// 返回产物输出缓存上限：20 堆 × 当前全局堆叠。
    /// </summary>
    public static int ProductOutputMax(this ItemProto fractionator) => BaseFracProductOutputMax;

    /// <summary>
    /// 返回流动输出缓存上限：20 堆 × 当前全局堆叠。
    /// </summary>
    public static int FluidOutputMax(this ItemProto fractionator) => BaseFracFluidOutputMax;

    /// <summary>
    /// 读取分馏塔当前允许的处理堆叠上限；该值来自全局堆叠科技，不属于建筑等级。
    /// </summary>
    public static int MaxStack(this ItemProto building) {
        return building.ID switch {
            IFE基础矿物塔 => BasicMineralTower.MaxStack,
            IFE基础解析塔 => BasicAnalysisTower.MaxStack,
            IFE高级矿物塔 => AdvancedMineralTower.MaxStack,
            IFE高级交互塔 => AdvancedInteractionTower.MaxStack,
            IFE高级转化塔 => AdvancedConversionTower.MaxStack,
            _ => 1
        };
    }

    /// <summary>
    /// 读取分馏塔的固定增产点倍率。
    /// </summary>
    public static float PlrRatio(this ItemProto building) {
        return building.ID switch {
            IFE基础矿物塔 => BasicMineralTower.PlrRatio,
            IFE基础解析塔 => BasicAnalysisTower.PlrRatio,
            IFE高级矿物塔 => AdvancedMineralTower.PlrRatio,
            IFE高级交互塔 => AdvancedInteractionTower.PlrRatio,
            IFE高级转化塔 => AdvancedConversionTower.PlrRatio,
            _ => 1.0f
        };
    }

    #region 模组存档接口实现

    public static void Import(BinaryReader r) {
        r.ReadBlocks(
            ("OutputExtend", FractionatorOutputState.OutputExtendImport),
            ("AdvancedRuntime", AdvancedFractionatorRuntime.Import)
        );
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("OutputExtend", FractionatorOutputState.OutputExtendExport),
            ("AdvancedRuntime", AdvancedFractionatorRuntime.Export)
        );
    }

    public static void IntoOtherSave() {
        FractionatorOutputState.OutputExtendIntoOtherSave();
        AdvancedFractionatorRuntime.IntoOtherSave();
    }

    #endregion
}
