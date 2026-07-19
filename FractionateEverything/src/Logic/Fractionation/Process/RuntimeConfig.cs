using System;
using System.Linq;
using FE.Logic.Fractionation.FracRecipes;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Progression;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Process;

/// <summary>
/// 分馏塔运行参数、增产倍率和核心更新热路径。
/// </summary>
public static partial class ProcessManager {
    // partial 类跨文件静态字段初始化顺序不稳定，不能用另一个文件里的 handler 数组决定长度。
    private const int FractionatorBuildingTypeCount = 4;
    /// <summary>
    /// 获取该规则或快照允许的最高等级。
    /// </summary>
    public static readonly int MaxLevel = 12;
    private static double[] incTableFixedRatio = [];
    /// <summary>
    /// 定义分馏塔流动输出缓存的基础上限。
    /// </summary>
    public static int BaseFracFluidOutputMax = 20;
    /// <summary>
    /// 定义分馏塔产物输出缓存的基础上限。
    /// </summary>
    public static int BaseFracProductOutputMax = 20;
    /// <summary>
    /// 定义分馏塔流动输入缓存的基础上限。
    /// </summary>
    public static int BaseFracFluidInputCargoMax = 40;
    /// <summary>
    /// 定义热路径按传送带速度估算的最大输入速度。
    /// </summary>
    public static int MaxBeltSpeed = 30;

    /// <summary>
    /// 单次分馏更新使用的运行参数快照。
    /// </summary>
    private struct FractionatorRuntimeConfig {
        /// <summary>
        /// 读取该建筑当前允许的分馏处理堆叠上限。
        /// </summary>
        public int MaxStack;
        /// <summary>
        /// 保存该分馏塔运行配置允许缓存的产物输出上限。
        /// </summary>
        public int ProductOutputMax;
        /// <summary>
        /// 保存该分馏塔运行配置允许缓存的流动输出上限。
        /// </summary>
        public int FluidOutputMax;
        /// <summary>
        /// 读取该建筑当前增产点倍率。
        /// </summary>
        public float PlrRatio;
        /// <summary>
        /// 保存该分馏塔类型当前获得的全局成功率加成。
        /// </summary>
        public float SuccessBoost;
        /// <summary>
        /// 判断该建筑是否已启用流动输出堆叠。
        /// </summary>
        public bool EnableFluidOutputStacking;
        /// <summary>
        /// 判断该建筑是否已启用产物输出堆叠。
        /// </summary>
        public bool EnableProductOutputStacking;
        /// <summary>
        /// 判断该建筑是否已启用产物满载时继续分馏的永动能力。
        /// </summary>
        public bool EnableFractionationForever;
    }

    private static readonly FractionatorRuntimeConfig[] runtimeConfigsByBuildingOffset =
        new FractionatorRuntimeConfig[FractionatorBuildingTypeCount];

    /// <summary>
    /// 初始化分馏运行热路径的配置表。
    /// </summary>
    public static void Init() {
        //获取传送带的最大速度，以此决定循环的最大次数以及缓存区大小
        //游戏逻辑帧只有60，就算传送带再快，也只能取放一个槽位的物品，也就是最多4个，再多也取不到
        //所以下面均以60/s的传送带速率作为极限值考虑
        MaxBeltSpeed = (from item in LDB.items.dataArray
            where item.Type == EItemType.Logistics && item.prefabDesc.isBelt
            select item.prefabDesc.beltSpeed * 6).Prepend(0).Max();
        MaxBeltSpeed = Math.Min(60, MaxBeltSpeed);
        MaxOutputTimes = (int)Math.Ceiling(MaxBeltSpeed / 15.0);
        float ratio = MaxBeltSpeed / 30.0f;
        PrefabDesc desc = LDB.models.Select(M分馏塔).prefabDesc;
        BaseFracFluidInputCargoMax = (int)(desc.fracFluidInputMax * ratio);
        BaseFracProductOutputMax = (int)(desc.fracProductOutputMax * ratio);
        BaseFracFluidOutputMax = (int)(desc.fracFluidOutputMax * ratio);

        // 增产剂表在游戏静态数据加载后才可靠，不能放到类型静态初始化阶段读取。
        incTableFixedRatio = new double[Cargo.incTableMilli.Length];
        //增产剂的增产效果修复，因为增产点数对于增产的加成不是线性的，但对于加速的加成是线性的
        for (int i = 1; i < Cargo.incTableMilli.Length; i++) {
            incTableFixedRatio[i] = Cargo.accTableMilli[i] / Cargo.incTableMilli[i];
        }
        RefreshFractionatorRuntimeConfig();
    }

    /// <summary>
    /// 刷新分馏塔原型参数和远古科技节点派生出的运行参数。
    /// </summary>
    public static void RefreshFractionatorRuntimeConfig() {
        SetRuntimeConfig(IFE交互塔, ERecipe.BuildingTrain, InteractionTower.MaxStack, InteractionTower.PlrRatio,
            InteractionTower.SuccessBoost);
        SetRuntimeConfig(IFE矿物复制塔, ERecipe.MineralCopy, MineralReplicationTower.MaxStack,
            MineralReplicationTower.PlrRatio, MineralReplicationTower.SuccessBoost);
        SetRuntimeConfig(IFE转化塔, ERecipe.Conversion, ConversionTower.MaxStack, ConversionTower.PlrRatio,
            ConversionTower.SuccessBoost);
        SetRuntimeConfig(IFE精馏塔, ERecipe.Rectification, RectificationTower.MaxStack, RectificationTower.PlrRatio,
            RectificationTower.SuccessBoost);
    }

    private static void SetRuntimeConfig(int buildingID, ERecipe recipeType, int maxStack, float plrRatio,
        float successBoost) {

        int index = FractionatorTowerCatalog.GetActiveFractionatorIndex(buildingID);
        if (index < 0 || index >= runtimeConfigsByBuildingOffset.Length) {
            return;
        }
        runtimeConfigsByBuildingOffset[index] = new FractionatorRuntimeConfig {
            MaxStack = maxStack,
            ProductOutputMax = BaseFracProductOutputMax * maxStack,
            FluidOutputMax = BaseFracFluidOutputMax * Math.Max(1, maxStack / 4),
            PlrRatio = plrRatio,
            SuccessBoost = successBoost,
            EnableFluidOutputStacking = TowerRuntimeModifierCache.IsFluidOutputStackingEnabled(recipeType),
            EnableProductOutputStacking = TowerRuntimeModifierCache.IsProductOutputStackingEnabled(recipeType),
            EnableFractionationForever = TowerRuntimeModifierCache.IsFractionationForeverEnabled(recipeType),
        };
    }

    private static FractionatorRuntimeConfig GetRuntimeConfig(int buildingID) {
        int index = FractionatorTowerCatalog.GetActiveFractionatorIndex(buildingID);
        if (index >= 0 && index < runtimeConfigsByBuildingOffset.Length) {
            FractionatorRuntimeConfig config = runtimeConfigsByBuildingOffset[index];
            if (config.MaxStack > 0) {
                return config;
            }
        }

        return new FractionatorRuntimeConfig {
            MaxStack = 3,
            ProductOutputMax = BaseFracProductOutputMax * 3,
            FluidOutputMax = BaseFracFluidOutputMax,
            PlrRatio = 1.0f,
            SuccessBoost = 0f,
            EnableFluidOutputStacking = false,
            EnableProductOutputStacking = false,
            EnableFractionationForever = false,
        };
    }
}
