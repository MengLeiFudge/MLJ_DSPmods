using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using FE.Logic.Building;
using FE.Logic.Recipe;
using FE.Logic.RecipeGrowth;
using FE.UI.MainPanel.ProgressTask;
using HarmonyLib;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static partial class ProcessManager {
    public static readonly float[] ReinforcementBonusArr = new float[MaxLevel + 1];
    public static readonly float[] ReinforcementSuccessRatioArr = new float[MaxLevel + 1];
    public static readonly int MaxLevel = 12;
    private static readonly double[] incTableFixedRatio = new double[Cargo.incTableMilli.Length];
    public static int BaseFracFluidOutputMax = 20;
    public static int BaseFracProductOutputMax = 20;
    public static int BaseFracFluidInputCargoMax = 40;
    public static int MaxBeltSpeed = 30;
    private struct FractionatorRuntimeConfig {
        public int MaxStack;
        public int ProductOutputMax;
        public int FluidOutputMax;
        public float PlrRatio;
        public float SuccessBoost;
        public bool EnableFluidEnhancement;
    }

    private static readonly FractionatorRuntimeConfig[] runtimeConfigsByBuildingOffset =
        new FractionatorRuntimeConfig[updateHandlersByBuildingOffset.Length];

    static ProcessManager() {
        //强化成功率
        int index = 0;
        float ratio = 0.5f;
        for (int loopCount = 1; index < ReinforcementSuccessRatioArr.Length - 1 && ratio > 0; loopCount++) {
            for (int j = 0; j < loopCount && index < ReinforcementSuccessRatioArr.Length - 1; j++) {
                ReinforcementSuccessRatioArr[index++] = ratio;
            }
            ratio -= 0.05f;
        }
        //强化加成
        for (int i = 1; i < ReinforcementBonusArr.Length; i++) {
            ReinforcementBonusArr[i] = i < 10
                ? 0.001f * i * i + 0.019f * i
                : 0.003f * i * i - 0.019f * i + 0.18f;
        }
    }

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
        BaseFracProductOutputMax = (int)(desc.fracProductOutputMax * ratio * 12 / 4);//todo: 最大堆叠12改为全局
        BaseFracFluidOutputMax = (int)(desc.fracFluidOutputMax * ratio * 12 / 4);

        //增产剂的增产效果修复，因为增产点数对于增产的加成不是线性的，但对于加速的加成是线性的
        for (int i = 1; i < Cargo.incTableMilli.Length; i++) {
            incTableFixedRatio[i] = Cargo.accTableMilli[i] / Cargo.incTableMilli[i];
        }
        RefreshFractionatorRuntimeConfig();
    }

    public static void RefreshFractionatorRuntimeConfig() {
        SetRuntimeConfig(IFE交互塔, InteractionTower.MaxStack, InteractionTower.PlrRatio,
            InteractionTower.SuccessBoost, InteractionTower.EnableFluidEnhancement);
        SetRuntimeConfig(IFE矿物复制塔, MineralReplicationTower.MaxStack, MineralReplicationTower.PlrRatio,
            MineralReplicationTower.SuccessBoost, MineralReplicationTower.EnableFluidEnhancement);
        SetRuntimeConfig(IFE点数聚集塔, PointAggregateTower.MaxStack, PointAggregateTower.PlrRatio,
            PointAggregateTower.SuccessBoost, PointAggregateTower.EnableFluidEnhancement);
        SetRuntimeConfig(IFE转化塔, ConversionTower.MaxStack, ConversionTower.PlrRatio,
            ConversionTower.SuccessBoost, ConversionTower.EnableFluidEnhancement);
        SetRuntimeConfig(IFE精馏塔, RectificationTower.MaxStack, RectificationTower.PlrRatio,
            RectificationTower.SuccessBoost, RectificationTower.EnableFluidEnhancement);
    }

    private static void SetRuntimeConfig(int buildingID, int maxStack, float plrRatio, float successBoost,
        bool enableFluidEnhancement) {

        int index = buildingID - IFE交互塔;
        if (index < 0 || index >= runtimeConfigsByBuildingOffset.Length) {
            return;
        }
        runtimeConfigsByBuildingOffset[index] = new FractionatorRuntimeConfig {
            MaxStack = maxStack,
            ProductOutputMax = BaseFracProductOutputMax * maxStack,
            FluidOutputMax = BaseFracFluidOutputMax * Math.Max(1, maxStack / 4),
            PlrRatio = plrRatio,
            SuccessBoost = successBoost,
            EnableFluidEnhancement = enableFluidEnhancement,
        };
    }

    private static FractionatorRuntimeConfig GetRuntimeConfig(int buildingID) {
        int index = buildingID - IFE交互塔;
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
            EnableFluidEnhancement = false,
        };
    }}
