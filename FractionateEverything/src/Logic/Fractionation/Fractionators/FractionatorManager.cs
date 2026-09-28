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
    /// 调整分馏塔缓存区大小（实际运行不使用此值，该方法只对原版分馏塔生效）
    /// </summary>
    public static void SetFractionatorCacheSize() {
        foreach (ModelProto modelProto in LDB.models.dataArray) {
            if (modelProto.prefabDesc.isFractionator) {
                modelProto.prefabDesc.fracFluidInputMax = BaseFracFluidInputCargoMax;
                modelProto.prefabDesc.fracProductOutputMax = BaseFracProductOutputMax;
                modelProto.prefabDesc.fracFluidOutputMax = BaseFracFluidOutputMax;
            }
        }
    }

    /// <summary>
    /// 调整已放置的分馏塔缓存区大小（实际运行不使用此值，该方法只对原版分馏塔生效）
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(FractionatorComponent), nameof(FractionatorComponent.Import))]
    public static void FractionatorComponent_Import_Postfix(ref FractionatorComponent __instance) {
        __instance.fluidInputMax = BaseFracFluidInputCargoMax;
        __instance.productOutputMax = BaseFracProductOutputMax;
        __instance.fluidOutputMax = BaseFracFluidOutputMax;
    }

    /// <summary>
    /// 返回基础塔流动输入缓存上限，固定为 20 件。
    /// </summary>
    public static int FluidInputCargoMax(this ItemProto fractionator) {
        return BaseFracFluidInputCargoMax;
    }

    /// <summary>
    /// 返回分馏塔产物输出缓存最大数目，由于输入的物品堆叠数可能超过塔的MaxStack，所以直接按照最高的来
    /// </summary>
    public static int ProductOutputMax(this ItemProto fractionator) => BaseFracProductOutputMax;

    /// <summary>
    /// 返回分馏塔流动输出缓存最大数目，由于输出仅由塔的MaxStack决定，所以根据当前MaxStack动态变化
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
