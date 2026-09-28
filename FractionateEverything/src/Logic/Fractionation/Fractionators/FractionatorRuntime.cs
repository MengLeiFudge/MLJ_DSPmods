// 分馏塔运行时：热路径、配置、功耗、扩展状态、科技投影与单塔同步边界。
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using UnityEngine;
using HarmonyLib;
using FE.Logic.Civilization;
using FE.Logic.DataCenter;
using FE.Logic.Fractionation.Recipes;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Fractionators {

/// <summary>
/// 修改所有分馏塔的处理逻辑，以及对应的显示。
/// </summary>
public static partial class ProcessManager {

    #region 运行配置

    // partial 类跨文件静态字段初始化顺序不稳定，不能用另一个文件里的 handler 数组决定长度。
    private const int FractionatorBuildingTypeCount = 5;
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
    public static int BaseFracFluidInputCargoMax = 20;
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
        BaseFracFluidInputCargoMax = 20;
        BaseFracProductOutputMax = 20;
        BaseFracFluidOutputMax = 20;

        // 增产剂表在游戏静态数据加载后才可靠，不能放到类型静态初始化阶段读取。
        incTableFixedRatio = new double[Cargo.incTableMilli.Length];
        //增产剂的增产效果修复，因为增产点数对于增产的加成不是线性的，但对于加速的加成是线性的
        for (int i = 1; i < Cargo.incTableMilli.Length; i++) {
            incTableFixedRatio[i] = Cargo.accTableMilli[i] / Cargo.incTableMilli[i];
        }
        RefreshFractionatorRuntimeConfig();
    }

    /// <summary>
    /// 刷新两座基础塔使用的运行参数。
    /// </summary>
    public static void RefreshFractionatorRuntimeConfig() {
        SetRuntimeConfig(IFE基础矿物塔, BasicMineralTower.MaxStack, BasicMineralTower.PlrRatio);
        SetRuntimeConfig(IFE基础解析塔, BasicAnalysisTower.MaxStack, BasicAnalysisTower.PlrRatio);
    }

    private static void SetRuntimeConfig(int buildingID, int maxStack, float plrRatio) {

        int index = FractionatorTowerCatalog.GetActiveFractionatorIndex(buildingID);
        if (index < 0 || index >= runtimeConfigsByBuildingOffset.Length) {
            return;
        }
        runtimeConfigsByBuildingOffset[index] = new FractionatorRuntimeConfig {
            MaxStack = maxStack,
            ProductOutputMax = BaseFracProductOutputMax,
            FluidOutputMax = BaseFracFluidOutputMax,
            PlrRatio = plrRatio,
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
            MaxStack = 1,
            ProductOutputMax = BaseFracProductOutputMax,
            FluidOutputMax = BaseFracFluidOutputMax,
            PlrRatio = 1.0f,
        };
    }

    #endregion

    #region 性能

    private static bool EnableFractionatorPerfProbe = false;
    private const int FractionatorPerfBucketCount = 4;
    private const int FractionatorPerfLogIntervalTicks = 300;
    private const int FractionatorPerfUpdateFe = 0;
    private const int FractionatorPerfUpdateVanilla = 1;
    private const int FractionatorPerfSetPcFe = 2;
    private const int FractionatorPerfSetPcVanilla = 3;
    private const int FractionatorPerfStageCount = 7;
    private const int FractionatorPerfStagePrepare = 0;
    private const int FractionatorPerfStageProcess = 1;
    private const int FractionatorPerfStageFlushDeltas = 2;
    private const int FractionatorPerfStagePostProcess = 3;
    private const int FractionatorPerfStageFluidBelts = 4;
    private const int FractionatorPerfStageProductBelt = 5;
    private const int FractionatorPerfStageFinalize = 6;
    private const int FractionatorPerfDetailCount = 7;
    private const int FractionatorPerfDetailPrepareStateRecipe = 0;
    private const int FractionatorPerfDetailPrepareSchema = 1;
    private const int FractionatorPerfDetailPrepareProduct = 2;
    private const int FractionatorPerfDetailPrepareConfig = 3;
    private const int FractionatorPerfDetailFlushDeltas = 4;
    private const int FractionatorPerfDetailProcessGetOutputs = 5;
    private const int FractionatorPerfDetailProcessMergeOutputs = 6;
    private static readonly long[] fractionatorPerfCalls = new long[FractionatorPerfBucketCount];
    private static readonly long[] fractionatorPerfTicks = new long[FractionatorPerfBucketCount];
    private static readonly long[] fractionatorPerfMaxTicks = new long[FractionatorPerfBucketCount];
    private static readonly long[] fractionatorPerfStageCalls = new long[FractionatorPerfStageCount];
    private static readonly long[] fractionatorPerfStageTicks = new long[FractionatorPerfStageCount];
    private static readonly long[] fractionatorPerfStageMaxTicks = new long[FractionatorPerfStageCount];
    private static readonly long[] fractionatorPerfDetailCalls = new long[FractionatorPerfDetailCount];
    private static readonly long[] fractionatorPerfDetailTicks = new long[FractionatorPerfDetailCount];
    private static readonly long[] fractionatorPerfDetailMaxTicks = new long[FractionatorPerfDetailCount];
    private static readonly long[] fractionatorPerfFeUpdateCallsByType =
        new long[FractionatorBuildingTypeCount];
    private static readonly long[] fractionatorPerfFeUpdateTicksByType =
        new long[FractionatorBuildingTypeCount];
    private static long fractionatorPerfWindowStartTick = -1;
    private static int fractionatorPerfLogging;

    private static void RecordFractionatorPerf(int bucket, int buildingID, long elapsedTicks) {
        if (!EnableFractionatorPerfProbe) {
            return;
        }
        fractionatorPerfCalls[bucket]++;
        fractionatorPerfTicks[bucket] += elapsedTicks;
        UpdateFractionatorPerfMax(fractionatorPerfMaxTicks, bucket, elapsedTicks);

        int towerIndex = FractionatorTowerCatalog.GetActiveFractionatorIndex(buildingID);
        if (bucket == FractionatorPerfUpdateFe
            && towerIndex >= 0
            && towerIndex < fractionatorPerfFeUpdateCallsByType.Length) {
            fractionatorPerfFeUpdateCallsByType[towerIndex]++;
            fractionatorPerfFeUpdateTicksByType[towerIndex] += elapsedTicks;
        }

        MaybeLogFractionatorPerf();
    }

    private static void RecordFractionatorPerfStage(int stage, long elapsedTicks) {
        if (!EnableFractionatorPerfProbe) {
            return;
        }
        fractionatorPerfStageCalls[stage]++;
        fractionatorPerfStageTicks[stage] += elapsedTicks;
        UpdateFractionatorPerfMax(fractionatorPerfStageMaxTicks, stage, elapsedTicks);
    }

    /// <summary>
    /// 记录分馏塔热路径指定阶段的性能耗时。
    /// </summary>
    public static void RecordFractionatorPerfDetail(int detail, long elapsedTicks) {
        if (!EnableFractionatorPerfProbe) {
            return;
        }
        fractionatorPerfDetailCalls[detail]++;
        fractionatorPerfDetailTicks[detail] += elapsedTicks;
        UpdateFractionatorPerfMax(fractionatorPerfDetailMaxTicks, detail, elapsedTicks);
    }

    private static long GetFractionatorPerfTimestamp() {
        return EnableFractionatorPerfProbe ? Stopwatch.GetTimestamp() : 0L;
    }

    private static long GetFractionatorPerfElapsed(long startTicks) {
        return EnableFractionatorPerfProbe ? Stopwatch.GetTimestamp() - startTicks : 0L;
    }

    private static void UpdateFractionatorPerfMax(long[] maxTicks, int index, long elapsedTicks) {
        if (elapsedTicks > maxTicks[index]) {
            maxTicks[index] = elapsedTicks;
        }
    }

    private static void MaybeLogFractionatorPerf() {
        long gameTick = GameMain.gameTick;
        if (gameTick < 0) {
            return;
        }

        long windowStart = fractionatorPerfWindowStartTick;
        if (windowStart < 0) {
            fractionatorPerfWindowStartTick = gameTick;
            return;
        }
        if (gameTick - windowStart < FractionatorPerfLogIntervalTicks) {
            return;
        }
        if (fractionatorPerfLogging != 0) {
            return;
        }
        fractionatorPerfLogging = 1;

        try {
            windowStart = fractionatorPerfWindowStartTick;
            fractionatorPerfWindowStartTick = gameTick;
            long windowTicks = Math.Max(1, gameTick - windowStart);
            long[] calls = new long[FractionatorPerfBucketCount];
            long[] ticks = new long[FractionatorPerfBucketCount];
            long[] maxTicks = new long[FractionatorPerfBucketCount];
            for (int i = 0; i < FractionatorPerfBucketCount; i++) {
                calls[i] = fractionatorPerfCalls[i];
                ticks[i] = fractionatorPerfTicks[i];
                maxTicks[i] = fractionatorPerfMaxTicks[i];
                fractionatorPerfCalls[i] = 0;
                fractionatorPerfTicks[i] = 0;
                fractionatorPerfMaxTicks[i] = 0;
            }

            long[] feTypeCalls = new long[fractionatorPerfFeUpdateCallsByType.Length];
            long[] feTypeTicks = new long[fractionatorPerfFeUpdateTicksByType.Length];
            for (int i = 0; i < feTypeCalls.Length; i++) {
                feTypeCalls[i] = fractionatorPerfFeUpdateCallsByType[i];
                feTypeTicks[i] = fractionatorPerfFeUpdateTicksByType[i];
                fractionatorPerfFeUpdateCallsByType[i] = 0;
                fractionatorPerfFeUpdateTicksByType[i] = 0;
            }
            long[] stageCalls = new long[FractionatorPerfStageCount];
            long[] stageTicks = new long[FractionatorPerfStageCount];
            long[] stageMaxTicks = new long[FractionatorPerfStageCount];
            for (int i = 0; i < FractionatorPerfStageCount; i++) {
                stageCalls[i] = fractionatorPerfStageCalls[i];
                stageTicks[i] = fractionatorPerfStageTicks[i];
                stageMaxTicks[i] = fractionatorPerfStageMaxTicks[i];
                fractionatorPerfStageCalls[i] = 0;
                fractionatorPerfStageTicks[i] = 0;
                fractionatorPerfStageMaxTicks[i] = 0;
            }
            long[] detailCalls = new long[FractionatorPerfDetailCount];
            long[] detailTicks = new long[FractionatorPerfDetailCount];
            long[] detailMaxTicks = new long[FractionatorPerfDetailCount];
            for (int i = 0; i < FractionatorPerfDetailCount; i++) {
                detailCalls[i] = fractionatorPerfDetailCalls[i];
                detailTicks[i] = fractionatorPerfDetailTicks[i];
                detailMaxTicks[i] = fractionatorPerfDetailMaxTicks[i];
                fractionatorPerfDetailCalls[i] = 0;
                fractionatorPerfDetailTicks[i] = 0;
                fractionatorPerfDetailMaxTicks[i] = 0;
            }
            LogInfo("[FractionatorPerf] "
                    + $"window={windowTicks}gt "
                    + FormatPerfBucket("feUpdate", calls[FractionatorPerfUpdateFe],
                        ticks[FractionatorPerfUpdateFe], maxTicks[FractionatorPerfUpdateFe])
                    + " "
                    + FormatPerfBucket("vanillaUpdate", calls[FractionatorPerfUpdateVanilla],
                        ticks[FractionatorPerfUpdateVanilla], maxTicks[FractionatorPerfUpdateVanilla])
                    + " "
                    + FormatPerfBucket("feSetPC", calls[FractionatorPerfSetPcFe],
                        ticks[FractionatorPerfSetPcFe], maxTicks[FractionatorPerfSetPcFe])
                    + " "
                    + FormatPerfBucket("vanillaSetPC", calls[FractionatorPerfSetPcVanilla],
                        ticks[FractionatorPerfSetPcVanilla], maxTicks[FractionatorPerfSetPcVanilla])
                    + " "
                    + FormatStagePerf(stageCalls, stageTicks, stageMaxTicks)
                    + " "
                    + FormatDetailPerf(detailCalls, detailTicks, detailMaxTicks)
                    + " "
                    + FormatFeTypePerf(feTypeCalls, feTypeTicks));
        }
        finally {
            fractionatorPerfLogging = 0;
        }
    }

    private static string FormatPerfBucket(string name, long calls, long elapsedTicks, long maxTicks) {
        double totalMs = elapsedTicks * 1000.0 / Stopwatch.Frequency;
        double avgUs = calls > 0 ? elapsedTicks * 1000000.0 / Stopwatch.Frequency / calls : 0.0;
        double maxUs = maxTicks * 1000000.0 / Stopwatch.Frequency;
        return $"{name}:calls={calls},totalMs={totalMs:F3},avgUs={avgUs:F3},maxUs={maxUs:F3}";
    }

    private static string FormatFeTypePerf(long[] calls, long[] ticks) {
        return "feTypes="
               + FormatFeType(IFE基础矿物塔, calls, ticks)
               + ";"
               + FormatFeType(IFE基础解析塔, calls, ticks)
               + ";"
               + FormatFeType(IFE高级矿物塔, calls, ticks)
               + ";"
               + FormatFeType(IFE高级交互塔, calls, ticks)
               + ";"
               + FormatFeType(IFE高级转化塔, calls, ticks);
    }

    private static string FormatStagePerf(long[] calls, long[] ticks, long[] maxTicks) {
        StringBuilder builder = new("feStages=");
        AppendStagePerf(builder, "prepare", FractionatorPerfStagePrepare, calls, ticks, maxTicks);
        AppendStagePerf(builder, "process", FractionatorPerfStageProcess, calls, ticks, maxTicks);
        AppendStagePerf(builder, "flush", FractionatorPerfStageFlushDeltas, calls, ticks, maxTicks);
        AppendStagePerf(builder, "postProcess", FractionatorPerfStagePostProcess, calls, ticks, maxTicks);
        AppendStagePerf(builder, "fluidBelts", FractionatorPerfStageFluidBelts, calls, ticks, maxTicks);
        AppendStagePerf(builder, "productBelt", FractionatorPerfStageProductBelt, calls, ticks, maxTicks);
        AppendStagePerf(builder, "finalize", FractionatorPerfStageFinalize, calls, ticks, maxTicks);
        return builder.ToString();
    }

    private static string FormatDetailPerf(long[] calls, long[] ticks, long[] maxTicks) {
        StringBuilder builder = new("feDetails=");
        AppendStagePerf(builder, "stateRecipe", FractionatorPerfDetailPrepareStateRecipe, calls, ticks, maxTicks);
        AppendStagePerf(builder, "schema", FractionatorPerfDetailPrepareSchema, calls, ticks, maxTicks);
        AppendStagePerf(builder, "product", FractionatorPerfDetailPrepareProduct, calls, ticks, maxTicks);
        AppendStagePerf(builder, "config", FractionatorPerfDetailPrepareConfig, calls, ticks, maxTicks);
        AppendStagePerf(builder, "flushDeltas", FractionatorPerfDetailFlushDeltas, calls, ticks, maxTicks);
        AppendStagePerf(builder, "processGetOutputs", FractionatorPerfDetailProcessGetOutputs, calls, ticks,
            maxTicks);
        AppendStagePerf(builder, "processMergeOutputs", FractionatorPerfDetailProcessMergeOutputs, calls, ticks,
            maxTicks);
        return builder.ToString();
    }

    private static void AppendStagePerf(StringBuilder builder, string name, int stage, long[] calls, long[] ticks,
        long[] maxTicks) {
        if (stage > 0) {
            builder.Append('|');
        }
        builder.Append(name);
        builder.Append(':');
        builder.Append(calls[stage]);
        builder.Append('/');
        builder.Append((ticks[stage] * 1000.0 / Stopwatch.Frequency).ToString("F3"));
        builder.Append("ms/");
        builder.Append((calls[stage] > 0 ? ticks[stage] * 1000000.0 / Stopwatch.Frequency / calls[stage] : 0.0)
            .ToString("F3"));
        builder.Append("us/");
        builder.Append((maxTicks[stage] * 1000000.0 / Stopwatch.Frequency).ToString("F3"));
        builder.Append("maxUs");
    }

    private static string FormatFeType(int buildingID, long[] calls, long[] ticks) {
        int index = FractionatorTowerCatalog.GetActiveFractionatorIndex(buildingID);
        if (index < 0 || index >= calls.Length) {
            return $"{buildingID}:0/0.000us";
        }
        long callCount = calls[index];
        double avgUs = callCount > 0 ? ticks[index] * 1000000.0 / Stopwatch.Frequency / callCount : 0.0;
        return $"{buildingID}:{callCount}/{avgUs:F3}us";
    }

    #endregion

    #region 功耗

    /// <summary>
    /// 将原版分馏器能耗更新调用替换为 FE 分馏塔能耗适配入口。
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.GameTick))]
    public static IEnumerable<CodeInstruction> FactorySystem_GameTick_Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        TranspileFractionatorUpdate(instructions, original);

    /// <summary>
    /// 将并行工厂更新中的原版分馏调用替换为 FE 分馏塔适配入口。
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GameLogic), nameof(GameLogic._fractionator_parallel))]
    public static IEnumerable<CodeInstruction> GameLogic_FractionatorParallel_Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        TranspileFractionatorUpdate(instructions, original);

    private static IEnumerable<CodeInstruction> TranspileFractionatorUpdate(
        IEnumerable<CodeInstruction> instructions, MethodBase original) {
        MethodInfo targetUpdateMethod =
            AccessTools.Method(typeof(FractionatorComponent), nameof(FractionatorComponent.InternalUpdate));
        MethodInfo replacementUpdateMethod =
            AccessTools.Method(typeof(ProcessManager), nameof(InternalUpdateWithModDispatch));
        List<CodeInstruction> codes = instructions.ToList();
        foreach (CodeInstruction instruction in codes) {
            if (instruction.Calls(targetUpdateMethod)) {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacementUpdateMethod;
            }
        }

        CodeMatcher matcher = new(codes);
        MethodInfo targetSetPCStateMethod =
            AccessTools.Method(typeof(FractionatorComponent), nameof(FractionatorComponent.SetPCState));
        MethodInfo replacementSetPCStateMethod =
            AccessTools.Method(typeof(ProcessManager), nameof(SetPCStateWithEntityPool));
        FieldInfo factoryField = AccessTools.Field(typeof(FactorySystem), "factory");
        FieldInfo entityPoolField = AccessTools.Field(typeof(PlanetFactory), "entityPool");

        matcher.MatchForward(false, new CodeMatch(i => i.Calls(targetSetPCStateMethod)))
            .Repeat(m => {
                if (original.DeclaringType == typeof(FactorySystem)) {
                    m.InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0));// FactorySystem
                    m.InsertAndAdvance(new CodeInstruction(OpCodes.Ldfld, factoryField));// PlanetFactory
                    m.InsertAndAdvance(new CodeInstruction(OpCodes.Ldfld, entityPoolField));// EntityData[]
                } else {
                    // GameLogic._fractionator_parallel: planetFactory 是局部变量，通过方法体局部变量列表定位
                    var locals = original.GetMethodBody()!.LocalVariables;
                    var planetFactoryLocal = locals.First(v => v.LocalType == typeof(PlanetFactory));
                    m.InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_S,
                        (byte)planetFactoryLocal.LocalIndex));// PlanetFactory（局部变量）
                    m.InsertAndAdvance(new CodeInstruction(OpCodes.Ldfld, entityPoolField));// EntityData[]
                }
                m.SetInstruction(new CodeInstruction(OpCodes.Call, replacementSetPCStateMethod));
            });

        return matcher.InstructionEnumeration();
    }

    /// <summary>
    /// 使用实体池为分馏塔设置电力消费者状态。
    /// </summary>
    public static void SetPCStateWithEntityPool(ref FractionatorComponent fractionator, PowerConsumerComponent[] pcPool,
        EntityData[] entityPool) {
        long perfStart = GetFractionatorPerfTimestamp();
        int buildingID = entityPool[fractionator.entityId].protoId;
        if (!FractionatorTowerCatalog.IsActiveFractionator(buildingID)) {
            try {
                fractionator.SetPCState(pcPool);// 原版分馏塔保持原逻辑
                return;
            }
            finally {
                RecordFractionatorPerf(FractionatorPerfSetPcVanilla, buildingID,
                    GetFractionatorPerfElapsed(perfStart));
            }
        }
        try {
            fractionator.SetPCState(pcPool, buildingID);
        }
        finally {
            RecordFractionatorPerf(FractionatorPerfSetPcFe, buildingID, GetFractionatorPerfElapsed(perfStart));
        }
    }

    /// <summary>
    /// 按 FE 分馏塔能耗规则设置电力消费者状态。
    /// </summary>
    public static void SetPCState(this ref FractionatorComponent fractionator,
        PowerConsumerComponent[] pcPool, int buildingID) {
        double num1 = fractionator.fluidInputCargoCount > 0.0001
            ? fractionator.fluidInputCount / (double)fractionator.fluidInputCargoCount
            : 4.0;
        double num2 = (double)fractionator.fluidInputCargoCount < MaxBeltSpeed
            ? (double)fractionator.fluidInputCargoCount
            : MaxBeltSpeed;
        num2 = num2 * num1 - MaxBeltSpeed;
        if (num2 < 0.0)
            num2 = 0.0;
        double powerRatio = Cargo.powerTableRatio[fractionator.incLevel] * GetFractionatorEnergyRatio(buildingID);
        ref PowerConsumerComponent pc = ref pcPool[fractionator.pcId];
        pc.workEnergyPerTick = GetFractionatorWorkEnergyPerTick(buildingID);
        pc.idleEnergyPerTick = GetFractionatorIdleEnergyPerTick(buildingID);
        int permillage = (int)((num2 * 50.0 * 30.0 / MaxBeltSpeed + 1000.0) * powerRatio + 0.5);
        pc.SetRequiredEnergy(fractionator.isWorking, permillage);
    }

    private static long GetFractionatorWorkEnergyPerTick(int buildingID) {
        return buildingID switch {
            IFE基础矿物塔 => BasicMineralTower.workEnergyPerTick,
            IFE基础解析塔 => BasicAnalysisTower.workEnergyPerTick,
            IFE高级矿物塔 => AdvancedMineralTower.workEnergyPerTick,
            IFE高级交互塔 => AdvancedInteractionTower.workEnergyPerTick,
            IFE高级转化塔 => AdvancedConversionTower.workEnergyPerTick,
            _ => 0
        };
    }

    private static long GetFractionatorIdleEnergyPerTick(int buildingID) {
        return buildingID switch {
            IFE基础矿物塔 => BasicMineralTower.idleEnergyPerTick,
            IFE基础解析塔 => BasicAnalysisTower.idleEnergyPerTick,
            IFE高级矿物塔 => AdvancedMineralTower.idleEnergyPerTick,
            IFE高级交互塔 => AdvancedInteractionTower.idleEnergyPerTick,
            IFE高级转化塔 => AdvancedConversionTower.idleEnergyPerTick,
            _ => 0
        };
    }

    private static float GetFractionatorEnergyRatio(int buildingID) {
        return buildingID switch {
            IFE基础矿物塔 => BasicMineralTower.EnergyRatio,
            IFE基础解析塔 => BasicAnalysisTower.EnergyRatio,
            IFE高级矿物塔 => AdvancedMineralTower.EnergyRatio,
            IFE高级交互塔 => AdvancedInteractionTower.EnergyRatio,
            IFE高级转化塔 => AdvancedConversionTower.EnergyRatio,
            _ => 1.0f
        };
    }


    #endregion

    #region 热路径

    /// <summary>
    /// 注册该分馏域对象需要的本地化文本。
    /// </summary>
    public static void AddTranslations() {
        Register("交互模式", "Interaction mode");
        Register("原料堆积", "Fluid overflow");
        Register("搬运模式", "Transport mode");
        Register("主产物", "Main Outputs");
        Register("副产物", "Byproducts");
        Register("流动输入", "Flow input");
        Register("流动输出", "Flow output");
        Register("配方不存在", "Recipe does not exist");
        Register("配方选择", "Recipe Selection");
        Register("未选择配方", "No Recipe Selected");
        Register("等待配方", "Waiting for a recipe");
        Register("当前营养：{0} / {1}", "Nutrition: {0} / {1}");
        Register("缺少营养", "Insufficient nutrition");
        Register("生命反应中", "Living reaction active");
        Register("配方产物", "Recipe products");
        Register("高级塔客户端手动取出受限",
            "Clients cannot manually extract advanced-tower outputs; use belts.",
            "联机客户端不能手动取出高级塔产物，请使用传送带。");
        Register("栏位已满", "The slot is full.");
    }

    #region 字段

    /// <summary>
    /// 定义单次更新最多尝试输出产物的次数。
    /// </summary>
    public static int MaxOutputTimes = 2;
    /// <summary>
    /// 提供无产物状态下复用的空输出列表。
    /// </summary>
    public static readonly List<ProductOutputInfo> emptyOutputs = [];
    /// <summary>
    /// 表示当前缓存包含主产物输出。
    /// </summary>
    public const byte OutputFlagMain = 1 << 0;
    /// <summary>
    /// 表示当前缓存包含副产物输出。
    /// </summary>
    public const byte OutputFlagSide = 1 << 1;
    /// <summary>
    /// 表示当前缓存包含流动物品输出。
    /// </summary>
    public const byte OutputFlagFluid = 1 << 2;
    /// <summary>
    /// 累计分馏成功次数（用于任务系统）
    /// </summary>
    public static long totalFractionSuccesses;
    private const int FractionRateWindowSeconds = 60;
    private static readonly long[] fractionSuccessBuckets = new long[FractionRateWindowSeconds];
    private static readonly object fractionRateWindowLock = new();
    private static long currentFractionRateSecond = -1;
    private static long currentFractionSuccessesPerMinute;
    /// <summary>
    /// 记录当前存档历史峰值每分钟成功分馏次数。
    /// </summary>
    public static long peakFractionSuccessesPerMinute;


    /// <summary>
    /// 读取分馏塔当前输出缓存包含的产物类型标记。
    /// </summary>
    public static byte GetCurrentOutputFlags(this FractionatorComponent fractionator,
        PlanetFactory factory) {

        if (factory == null) return 0;
        return fractionator.GetExtraState(factory).CurrentOutputFlags;
    }

    private static void SetCurrentOutputFlags(PlanetFactory factory,
        FractionatorOutputState.FractionatorExtraState extraState,
        bool main, bool side, bool fluid) {

        if (factory == null) return;
        byte flags = 0;
        if (main) flags |= OutputFlagMain;
        if (side) flags |= OutputFlagSide;
        if (fluid) flags |= OutputFlagFluid;
        if (extraState.CurrentOutputFlags == flags) {
            return;
        }
        extraState.CurrentOutputFlags = flags;
    }

    #endregion


    #region 分馏塔处理逻辑

    /// <summary>
    /// 返回增产加成、加速加成中二者最大的值。
    /// </summary>
    public static double MaxTableMilli(int fluidInputIncAvg) {
        // 旧存档里可能留下负的增产点数，这里先把索引夹回合法范围，避免 UI 刷新直接越界崩溃。
        int avgPoint = Math.Max(0, Math.Min(fluidInputIncAvg, 10));
        double ratioAcc = Cargo.accTableMilli[avgPoint];
        double ratioInc = Cargo.incTableMilli[avgPoint] * incTableFixedRatio[avgPoint];
        return ratioAcc > ratioInc ? ratioAcc : ratioInc;
    }

    /// <summary>
    /// 在原版分馏器更新前按建筑 ID 分发到 FE 五塔热路径，非 FE 塔回落原版更新。
    /// </summary>
    /// <param name="fractionator">当前分馏器组件。</param>
    /// <param name="factory">所属行星工厂。</param>
    /// <param name="power">当前供电比例。</param>
    /// <param name="signPool">实体图标/标记池。</param>
    /// <param name="productRegister">产物统计寄存器。</param>
    /// <param name="consumeRegister">消耗统计寄存器。</param>
    /// <returns>分馏器工作状态码；FE 塔由热路径写入，原版塔透传 InternalUpdate。</returns>
    /// <remarks>
    /// <para>FE 分馏塔使用扩展状态保存全部主产物和副产物；原版字段只保留首个主产物兼容数据。</para>
    /// <para>具体分馏结果由 <see cref="BaseRecipe.GetOutputs"/> 及其批量实现结算。</para>
    /// </remarks>
    public static uint InternalUpdateWithModDispatch(ref FractionatorComponent fractionator,
        PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister) {
        long perfStart = GetFractionatorPerfTimestamp();
        int buildingID = factory.entityPool[fractionator.entityId].protoId;
        // 五塔按建筑 ID 分发：基础塔复用单输入热路径，高级塔使用独立多输入状态。
        if (FractionatorTowerCatalog.IsActiveFractionator(buildingID)) {
            try {
                if (FractionatorTowerCatalog.IsAdvancedTower(buildingID)) {
                    return AdvancedFractionatorRuntime.InternalUpdate(ref fractionator, factory, signPool,
                        productRegister, consumeRegister);
                }

                uint result = 0;
                switch (buildingID) {
                    case IFE基础解析塔:
                        InternalUpdate<WorldviewRecipe>(ref fractionator, factory, power, signPool,
                            productRegister, consumeRegister, ref result, ERecipe.BasicAnalysis);
                        break;
                    case IFE基础矿物塔:
                        InternalUpdate<WorldviewRecipe>(ref fractionator, factory, power, signPool,
                            productRegister, consumeRegister, ref result, ERecipe.BasicMineral);
                        break;
                }
                return result;
            }
            finally {
                RecordFractionatorPerf(FractionatorPerfUpdateFe, buildingID, GetFractionatorPerfElapsed(perfStart));
            }
        }

        // 原版分馏塔不做处理。
        try {
            return fractionator.InternalUpdate(factory, power, signPool, productRegister, consumeRegister);
        }
        finally {
            RecordFractionatorPerf(FractionatorPerfUpdateVanilla, buildingID, GetFractionatorPerfElapsed(perfStart));
        }
    }

    /// <summary>
    /// InternalUpdate的默认实现。
    /// </summary>
    public static void InternalUpdate<T>(ref FractionatorComponent __instance,
        PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister,
        ref uint __result, ERecipe recipeType) where T : BaseRecipe {
        long perfStageStart = GetFractionatorPerfTimestamp();
        long perfDetailStart = perfStageStart;
        int entityId = __instance.entityId;
        int buildingID = factory.entityPool[entityId].protoId;
        bool isBasicTower = FractionatorTowerCatalog.IsBasicTower(buildingID);
        //所有产物输出
        FractionatorOutputState.FractionatorExtraState extraState = __instance.GetExtraState(factory);
        List<ProductOutputInfo> products = extraState.Products;
        ProductOutputBuffer outputBuffer = extraState.ScratchOutputs;
        int fluidId = __instance.fluidId;
        BaseRecipe recipe = extraState.GetRecipe(recipeType, fluidId);
        RecordFractionatorPerfDetail(FractionatorPerfDetailPrepareStateRecipe,
            GetFractionatorPerfElapsed(perfDetailStart));
        perfDetailStart = GetFractionatorPerfTimestamp();
        //检测products和recipe的输出是否一致
        ProductOutputInfo product0 = null;
        if (recipe == null) {
            bool needResetProducts = !extraState.TryGetRuntimeSchema(recipeType, fluidId, null, __instance.productId,
                                         out _)
                                     || products.Count > 0
                                     || __instance.productId != fluidId
                                     || __instance.productOutputCount != 0;
            if (needResetProducts) {
                products.Clear();
                extraState.InvalidateFullProductCache();
                __instance.productId = fluidId;
                __instance.productOutputCount = 0;
                __instance.produceProb = 0.01f;
                signPool[entityId].iconId0 = 0;
                signPool[entityId].iconType = 0U;
            }
            if (needResetProducts) {
                extraState.MarkRuntimeSchema(recipeType, fluidId, null, __instance.productId, null);
            }
        } else if (!extraState.TryGetRuntimeSchema(recipeType, fluidId, recipe, __instance.productId,
                       out product0)) {
            int expectedProductCount = recipe.OutputMain.Count + recipe.OutputAppend.Count;
            int firstProductId = recipe.OutputMain.Count > 0 ? recipe.OutputMain[0].OutputID : recipe.InputID;
            bool needResetProducts = __instance.productId != firstProductId
                                     || products.Count != expectedProductCount
                                     || !MatchesRecipeOutputs(products, recipe);
            if (needResetProducts) {
                products.Clear();
                extraState.InvalidateFullProductCache();
                __instance.productId = firstProductId;
                __instance.productOutputCount = 0;
                __instance.produceProb = 0.01f;
                signPool[entityId].iconId0 = (uint)__instance.fluidId;
                signPool[entityId].iconType = 1U;
                foreach (OutputInfo info in recipe.OutputMain) {
                    products.Add(new(true, info.OutputID, 0));
                }
                foreach (OutputInfo info in recipe.OutputAppend) {
                    products.Add(new(false, info.OutputID, 0));
                }
            }
            int productId = __instance.productId;
            product0 = products.Count > 0 && products[0].itemId == productId
                ? products[0]
                : FindProduct(products, productId);
            extraState.MarkRuntimeSchema(recipeType, fluidId, recipe, productId, product0);
        }
        RecordFractionatorPerfDetail(FractionatorPerfDetailPrepareSchema, GetFractionatorPerfElapsed(perfDetailStart));
        perfDetailStart = GetFractionatorPerfTimestamp();
        //第一个主输出，recipe有则必定有，recipe没有则必定没有
        int product0Id = __instance.productId;
        //如果通过面板取了物品，需要同步数目到products
        if (product0 != null && product0.count != __instance.productOutputCount) {
            product0.count = __instance.productOutputCount;
            extraState.InvalidateFullProductCache();
        }
        RecordFractionatorPerfDetail(FractionatorPerfDetailPrepareProduct, GetFractionatorPerfElapsed(perfDetailStart));
        if (power < 0.1) {
            __result = 0;
            RecordFractionatorPerfStage(FractionatorPerfStagePrepare, GetFractionatorPerfElapsed(perfStageStart));
            return;
        }
        perfDetailStart = GetFractionatorPerfTimestamp();
        long perfConfigStart = perfDetailStart;
        float fluidInputCountPerCargo = 1.0f;
        if (__instance.fluidInputCount == 0)
            __instance.fluidInputCargoCount = 0f;
        else
            fluidInputCountPerCargo = __instance.fluidInputCargoCount > 0.0001
                ? __instance.fluidInputCount / __instance.fluidInputCargoCount
                : 4f;
        FractionatorRuntimeConfig runtimeConfig = GetRuntimeConfig(buildingID);
        int maxStack = runtimeConfig.MaxStack;
        float plrRatio = runtimeConfig.PlrRatio;
        int fluidInputCargoMax = BaseFracFluidInputCargoMax;
        int productOutputMax = runtimeConfig.ProductOutputMax;
        int fluidOutputMax = runtimeConfig.FluidOutputMax;
        bool canProcessRecipe = recipe != null && RecipeAvailabilityStore.IsAvailable(recipe);
        bool moveDirectly = !canProcessRecipe;
        bool producedMainThisTick = false;
        bool producedSideThisTick = false;
        bool producedFluidThisTick = false;
        bool hasFullProduct = extraState.HasFullProduct(productOutputMax);
        bool needRecheckFullProduct = false;
        int consumedInputThisTick = 0;
        int successCountThisTick = 0;
        List<ProductOutputInfo> productRegisterDeltas = null;
        RecordFractionatorPerfDetail(FractionatorPerfDetailPrepareConfig, GetFractionatorPerfElapsed(perfConfigStart));
        RecordFractionatorPerfStage(FractionatorPerfStagePrepare, GetFractionatorPerfElapsed(perfStageStart));
        perfStageStart = GetFractionatorPerfTimestamp();
        perfDetailStart = perfStageStart;
        if (__instance.fluidInputCount > 0
            && __instance.fluidOutputCount < fluidOutputMax) {
            //分馏塔正常运转时，计算进度，10000点进度可以处理一次
            __instance.progress += (int)(power
                                         * (500.0 / 3.0)
                                         * (__instance.fluidInputCargoCount < MaxBeltSpeed
                                             ? __instance.fluidInputCargoCount
                                             : MaxBeltSpeed)
                                         * fluidInputCountPerCargo
                                         + 0.75);
            if (__instance.progress > 300000) {
                __instance.progress = 300000;
            }
            int batchCount = Math.Min(__instance.progress / 10000, __instance.fluidInputCount);
            if (batchCount > 0) {
                int reservedBatchCount = batchCount;
                __instance.progress -= reservedBatchCount * 10000;
                int fluidInputIncAvg = __instance.fluidInputInc <= 0 || __instance.fluidInputCount <= 0
                    ? 0
                    : __instance.fluidInputInc / __instance.fluidInputCount;
                if (!__instance.incUsed)
                    __instance.incUsed = fluidInputIncAvg > 0;

                // 无可用配方或产物缓存已满时，输入直接进入流动输出。
                bool isForcedPassthrough = moveDirectly || hasFullProduct;
                FractionationBatchResult batchResult;
                if (isForcedPassthrough) {
                    outputBuffer.Clear();
                    batchResult = new FractionationBatchResult {
                        InputRemoveCount = batchCount,
                        ConsumedRegisterCount = 0,
                        SuccessCount = 0,
                        DestroyedCount = 0,
                        PassThroughCount = batchCount,
                        PassThroughInc = fluidInputIncAvg * batchCount,
                    };
                    __instance.fluidInputInc -= fluidInputIncAvg * batchCount;
                    if (__instance.fluidInputInc < 0) __instance.fluidInputInc = 0;
                } else {
                    float pointsBonus = (float)MaxTableMilli(fluidInputIncAvg) * plrRatio;
                    // 两座基础塔只使用配方自身概率与增产剂加成；3.0 不再修改基础成功率。
                    float successBoost = 0f;
                    if (recipe is WorldviewRecipe worldviewRecipe) {
                        int availableOutput = productOutputMax - (product0?.count ?? __instance.productOutputCount);
                        batchCount = worldviewRecipe.LimitBasicBatchCountByOutputRoom(
                            batchCount, availableOutput, pointsBonus, successBoost);
                        if (batchCount < reservedBatchCount) {
                            __instance.progress += (reservedBatchCount - batchCount) * 10000;
                        }
                    }
                    perfDetailStart = GetFractionatorPerfTimestamp();
                    batchResult = recipe.GetOutputsBatchFast(ref __instance.seed, pointsBonus, successBoost,
                        batchCount, fluidInputIncAvg, ref __instance.fluidInputInc, outputBuffer);
                    RecordFractionatorPerfDetail(FractionatorPerfDetailProcessGetOutputs,
                        GetFractionatorPerfElapsed(perfDetailStart));
                }

                recipe?.RecordSuccesses(batchResult.SuccessCount);
                __instance.fractionSuccess = batchResult.HasOutput;

                if (batchResult.InputRemoveCount > 0) {
                    __instance.fluidInputCount -= batchResult.InputRemoveCount;
                    if (__instance.fluidInputCount < 0) __instance.fluidInputCount = 0;
                    if (__instance.fluidInputCount == 0) __instance.fluidInputInc = 0;
                    __instance.fluidInputCargoCount -= batchResult.InputRemoveCount / fluidInputCountPerCargo;
                    if (__instance.fluidInputCargoCount < 0f) __instance.fluidInputCargoCount = 0f;
                }

                if (batchResult.PassThroughCount > 0) {
                    __instance.fluidOutputCount += batchResult.PassThroughCount;
                    __instance.fluidOutputTotal += batchResult.PassThroughCount;
                    __instance.fluidOutputInc += batchResult.PassThroughInc;
                    producedFluidThisTick = true;
                }

                if (batchResult.SuccessCount > 0) {
                    perfDetailStart = GetFractionatorPerfTimestamp();
                    successCountThisTick += batchResult.SuccessCount;
                    __instance.productOutputTotal += batchResult.SuccessCount;
                    for (int i = 0; i < outputBuffer.Count; i++) {
                        ProductOutputInfo p = outputBuffer[i];
                        int itemID = p.itemId;
                        int itemCount = p.count;
                        if (p.isMainOutput) producedMainThisTick = true;
                        else producedSideThisTick = true;
                        AddProductRegisterDelta(ref productRegisterDeltas, itemID, itemCount);
                        if (itemID == product0Id) {
                            product0.count += itemCount;
                            __instance.productOutputCount = product0.count;
                            NotifyProductCountIncreased(extraState, product0.count, productOutputMax,
                                ref hasFullProduct);
                        } else {
                            ProductOutputInfo target = FindProduct(products, itemID);
                            if (target != null) {
                                target.count += itemCount;
                                NotifyProductCountIncreased(extraState, target.count, productOutputMax,
                                    ref hasFullProduct);
                            } else {
                                products.Add(new ProductOutputInfo(p.isMainOutput, itemID, itemCount));
                                NotifyProductCountIncreased(extraState, itemCount, productOutputMax,
                                    ref hasFullProduct);
                            }
                        }
                    }
                    RecordFractionatorPerfDetail(FractionatorPerfDetailProcessMergeOutputs,
                        GetFractionatorPerfElapsed(perfDetailStart));
                }

                consumedInputThisTick += batchResult.ConsumedRegisterCount;
            }
        } else {
            __instance.fractionSuccess = false;
        }

        RecordFractionatorPerfStage(FractionatorPerfStageProcess, GetFractionatorPerfElapsed(perfStageStart));
        perfStageStart = GetFractionatorPerfTimestamp();
        perfDetailStart = GetFractionatorPerfTimestamp();
        FlushProcessingDeltas(fluidId, consumedInputThisTick, successCountThisTick,
            productRegisterDeltas, productRegister, consumeRegister);
        RecordFractionatorPerfDetail(FractionatorPerfDetailFlushDeltas, GetFractionatorPerfElapsed(perfDetailStart));

        SetCurrentOutputFlags(factory,
            extraState,
            producedMainThisTick, producedSideThisTick, producedFluidThisTick);

        RecordFractionatorPerfStage(FractionatorPerfStageFlushDeltas, GetFractionatorPerfElapsed(perfStageStart));
        perfStageStart = GetFractionatorPerfTimestamp();
        RecordFractionatorPerfStage(FractionatorPerfStagePostProcess, GetFractionatorPerfElapsed(perfStageStart));
        perfStageStart = GetFractionatorPerfTimestamp();
        CargoTraffic cargoTraffic = factory.cargoTraffic;
        byte stack = 0;
        byte inc = 0;
        if (__instance.belt1 > 0) {
            if (__instance.isOutput1) {
                TryOutputFluidToBelt(ref __instance, !moveDirectly, maxStack,
                    cargoTraffic, __instance.belt1, fluidInputCountPerCargo, forceSingleStack: moveDirectly);
            } else if (!__instance.isOutput1
                       && CanAcceptBasicInputStack(cargoTraffic, __instance.belt1,
                           fluidInputCargoMax, __instance.fluidInputCount)) {
                if (fluidId > 0) {
                    for (int i = 0;
                         i < MaxOutputTimes && CanAcceptBasicInputStack(cargoTraffic, __instance.belt1,
                             fluidInputCargoMax, __instance.fluidInputCount);
                         i++) {
                        if (cargoTraffic.TryPickItemAtRear(__instance.belt1, fluidId, null, out stack, out inc) > 0) {
                            __instance.fluidInputCount += stack;
                            __instance.fluidInputInc += inc;
                            __instance.fluidInputCargoCount++;
                        } else {
                            break;
                        }
                    }
                } else {
                    int needId = cargoTraffic.TryPickItemAtRear(__instance.belt1, 0, null, out stack, out inc);
                    if (needId > 0) {
                        __instance.fluidInputCount += stack;
                        __instance.fluidInputInc += inc;
                        __instance.fluidInputCargoCount++;
                        __instance.fluidId = needId;
                        recipe = extraState.GetRecipe(recipeType, needId);
                        if (recipe == null) {
                            __instance.productId = needId;
                            __instance.produceProb = 0.01f;
                            signPool[entityId].iconId0 = 0;
                            signPool[entityId].iconType = 0U;
                        } else {
                            __instance.productId = recipe.OutputMain.Count > 0
                                ? recipe.OutputMain[0].OutputID
                                : recipe.InputID;
                            __instance.produceProb = 0.01f;
                            signPool[entityId].iconId0 = (uint)__instance.fluidId;
                            signPool[entityId].iconType = 1U;
                            foreach (OutputInfo info in recipe.OutputMain) {
                                products.Add(new(true, info.OutputID, 0));
                            }
                            foreach (OutputInfo info in recipe.OutputAppend) {
                                products.Add(new(false, info.OutputID, 0));
                            }
                            extraState.InvalidateFullProductCache();
                        }
                        // 初始拾取一个后，尝试继续拾取同类物品以快速填满
                        for (int i = 1;
                             i < MaxOutputTimes && CanAcceptBasicInputStack(cargoTraffic, __instance.belt1,
                                 fluidInputCargoMax, __instance.fluidInputCount);
                             i++) {
                            if (cargoTraffic.TryPickItemAtRear(__instance.belt1, needId, null, out stack, out inc)
                                > 0) {
                                __instance.fluidInputCount += stack;
                                __instance.fluidInputInc += inc;
                                __instance.fluidInputCargoCount++;
                            } else {
                                break;
                            }
                        }
                    }
                }
            }
        }
        if (__instance.belt2 > 0) {
            if (__instance.isOutput2) {
                TryOutputFluidToBelt(ref __instance, !moveDirectly, maxStack,
                    cargoTraffic, __instance.belt2, fluidInputCountPerCargo, forceSingleStack: moveDirectly);
            } else if (!__instance.isOutput2
                       && CanAcceptBasicInputStack(cargoTraffic, __instance.belt2,
                           fluidInputCargoMax, __instance.fluidInputCount)) {
                if (__instance.fluidId > 0) {
                    for (int i = 0;
                         i < MaxOutputTimes && CanAcceptBasicInputStack(cargoTraffic, __instance.belt2,
                             fluidInputCargoMax, __instance.fluidInputCount);
                         i++) {
                        if (cargoTraffic.TryPickItemAtRear(__instance.belt2, __instance.fluidId, null,
                                out stack, out inc) > 0) {
                            __instance.fluidInputCount += stack;
                            __instance.fluidInputInc += inc;
                            __instance.fluidInputCargoCount++;
                        } else {
                            break;
                        }
                    }
                } else {
                    int needId = cargoTraffic.TryPickItemAtRear(__instance.belt2, 0, null, out stack, out inc);
                    if (needId > 0) {
                        __instance.fluidInputCount += stack;
                        __instance.fluidInputInc += inc;
                        __instance.fluidInputCargoCount++;
                        __instance.fluidId = needId;
                        recipe = extraState.GetRecipe(recipeType, needId);
                        if (recipe == null) {
                            __instance.productId = needId;
                            __instance.produceProb = 0.01f;
                            signPool[entityId].iconId0 = 0;
                            signPool[entityId].iconType = 0U;
                        } else {
                            __instance.productId = recipe.OutputMain.Count > 0
                                ? recipe.OutputMain[0].OutputID
                                : recipe.InputID;
                            __instance.produceProb = 0.01f;
                            signPool[entityId].iconId0 = (uint)__instance.fluidId;
                            signPool[entityId].iconType = 1U;
                            foreach (OutputInfo info in recipe.OutputMain) {
                                products.Add(new(true, info.OutputID, 0));
                            }
                            foreach (OutputInfo info in recipe.OutputAppend) {
                                products.Add(new(false, info.OutputID, 0));
                            }
                            extraState.InvalidateFullProductCache();
                        }
                        // 初始拾取一个后，尝试继续拾取同类物品以快速填满
                        for (int i = 1;
                             i < MaxOutputTimes && CanAcceptBasicInputStack(cargoTraffic, __instance.belt2,
                                 fluidInputCargoMax, __instance.fluidInputCount);
                             i++) {
                            if (cargoTraffic.TryPickItemAtRear(__instance.belt2, needId, null, out stack, out inc)
                                > 0) {
                                __instance.fluidInputCount += stack;
                                __instance.fluidInputInc += inc;
                                __instance.fluidInputCargoCount++;
                            } else {
                                break;
                            }
                        }
                    }
                }
            }
        }
        RecordFractionatorPerfStage(FractionatorPerfStageFluidBelts, GetFractionatorPerfElapsed(perfStageStart));
        perfStageStart = GetFractionatorPerfTimestamp();
        bool interactionMode = false;
        bool interactionTransferred = false;
        int sacrificedTowerId = 0;
        if (__instance.belt0 > 0) {
            if (__instance.isOutput0) {
                if (products.Count > 0) {
                    // 获取分馏塔产物输出堆叠。
                    int productStack = maxStack;
                    ProductOutputInfo product = SelectProductForBeltOutput(products, productStack);
                    //输出产物
                    if (product != null && product.count > 0) {
                        if (product.count >= productStack) {
                            //产物达到最大堆叠数目，直接尝试输出
                            if (cargoTraffic.TryInsertItemAtHead(__instance.belt0, product.itemId, (byte)productStack,
                                    (byte)(productStack * (recipe?.GetOutputInc(product.itemId) ?? 0)))) {
                                product.count -= productStack;
                                extraState.InvalidateFullProductCache();
                                needRecheckFullProduct = needRecheckFullProduct
                                                         || hasFullProduct && product.count < productOutputMax;
                                if (ReferenceEquals(product, product0)) {
                                    __instance.productOutputCount = product.count;
                                }
                            }
                        } else if (product.count > 0 && __instance.fluidInputCount == 0) {
                            // 输入排空后释放不足整堆的尾料。
                            if (cargoTraffic.TryInsertItemAtHead(__instance.belt0, product.itemId, (byte)product.count,
                                    (byte)(product.count * (recipe?.GetOutputInc(product.itemId) ?? 0)))) {
                                product.count = 0;
                                extraState.InvalidateFullProductCache();
                                needRecheckFullProduct = needRecheckFullProduct || hasFullProduct;
                                if (ReferenceEquals(product, product0)) {
                                    __instance.productOutputCount = product.count;
                                }
                            }
                        }
                    }
                }
            } else if (isBasicTower
                       && __instance.belt1 <= 0
                       && __instance.belt2 <= 0
                       && AreAllProductsEmpty(products)) {
                // 基础塔正面反向输入且两侧未连接时上传到亚空间；容量不足一份带上堆叠时停止取货。
                interactionMode = true;
                int interactionItemId = 0;
                long accepted = 0;
                lock (DataCenterInventory.centerItemCount) {
                    int rearItemId = cargoTraffic.GetItemIdAtRear(__instance.belt0);
                    int rearStack = GetRearCargoStack(cargoTraffic, __instance.belt0);
                    if (DataCenterUploadRouter.CanUploadStack(rearItemId, rearStack)) {
                        interactionItemId = cargoTraffic.TryPickItemAtRear(__instance.belt0, rearItemId,
                            null, out stack, out inc);
                    }
                    if (interactionItemId > 0) {
                        if (FractionatorTowerCatalog.IsAdvancedTowerItem(interactionItemId)) {
                            accepted = DataCenterInventory.AddItemToModData(IFE记忆源点, stack);
                            sacrificedTowerId = accepted > 0 ? interactionItemId : 0;
                        } else {
                            accepted = DataCenterUploadRouter.Upload(interactionItemId, stack, inc);
                        }
                    }
                }
                if (sacrificedTowerId > 0) {
                    ResearchManager.NotifyAdvancedTowerObtained(sacrificedTowerId);
                }
                interactionTransferred = interactionItemId > 0 && accepted == stack;
                if (interactionTransferred) {
                    __instance.fluidId = interactionItemId;
                    __instance.productId = interactionItemId;
                    __instance.produceProb = 0.01f;
                    signPool[entityId].iconId0 = (uint)__instance.fluidId;
                    signPool[entityId].iconType = 1U;
                }
            }
        }

        RecordFractionatorPerfStage(FractionatorPerfStageProductBelt, GetFractionatorPerfElapsed(perfStageStart));
        perfStageStart = GetFractionatorPerfTimestamp();
        if (interactionMode) {
            __instance.isWorking = interactionTransferred;
        } else {
            // 如果缓存区全部清空，重置全部
            if (__instance.fluidInputCount == 0
                && __instance.fluidOutputCount == 0
                && AreAllProductsEmpty(products)) {
                __instance.fluidId = 0;
                __instance.productId = 0;
                products.Clear();
                hasFullProduct = false;
                extraState.InvalidateFullProductCache();
                signPool[entityId].iconId0 = 0;
                signPool[entityId].iconType = 0U;
            }
            if (needRecheckFullProduct) {
                hasFullProduct = extraState.HasFullProduct(productOutputMax, forceRefresh: true);
            }
            __instance.isWorking = __instance.fluidInputCount > 0
                                   && !hasFullProduct
                                   && __instance.fluidOutputCount < fluidOutputMax
                                   && !moveDirectly;
        }

        __result = !__instance.isWorking ? 0U : 1U;
        RecordFractionatorPerfStage(FractionatorPerfStageFinalize, GetFractionatorPerfElapsed(perfStageStart));
    }

    private static bool CanAcceptBasicInputStack(CargoTraffic cargoTraffic, int beltId,
        int capacity, int currentCount) {
        if (cargoTraffic == null || beltId <= 0 || currentCount >= capacity) {
            return false;
        }
        int stack = GetRearCargoStack(cargoTraffic, beltId);
        return stack > 0 && currentCount + stack <= capacity;
    }

    internal static int GetRearCargoStack(CargoTraffic cargoTraffic, int beltId) {
        if (cargoTraffic == null || beltId <= 0) {
            return 0;
        }
        CargoPath path = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[beltId].segPathId);
        int cargoId = path?.GetCargoIdAtRear() ?? -1;
        if (cargoId <= 0 || cargoId >= cargoTraffic.container.cargoPool.Length) {
            return 0;
        }
        return cargoTraffic.container.cargoPool[cargoId].stack;
    }

    private static void AddProductRegisterDelta(ref List<ProductOutputInfo> deltas, int itemId, int count) {
        if (count <= 0) {
            return;
        }
        deltas ??= [];
        ProductOutputInfo delta = FindProduct(deltas, itemId);
        if (delta == null) {
            deltas.Add(new ProductOutputInfo(false, itemId, count));
            return;
        }
        delta.count += count;
    }

    private static void FlushProcessingDeltas(int fluidId, int consumedInputCount,
        int successCount, List<ProductOutputInfo> productRegisterDeltas,
        int[] productRegister, int[] consumeRegister) {
        if (consumedInputCount > 0) {
            Interlocked.Add(ref consumeRegister[fluidId], consumedInputCount);
        }
        if (productRegisterDeltas != null) {
            foreach (ProductOutputInfo delta in productRegisterDeltas) {
                Interlocked.Add(ref productRegister[delta.itemId], delta.count);
            }
        }
        if (successCount > 0) {
            RecordFractionSuccess(successCount);
        }
    }

    internal static void RecordFractionSuccess(int count) {
        // 与 BaseRecipe.RecordSuccesses 一致：线程安全饱和累加，避免 long.MaxValue 后再加回绕为负。
        if (count > 0) {
            long current;
            long updated;
            do {
                current = Interlocked.Read(ref totalFractionSuccesses);
                updated = current > long.MaxValue - count ? long.MaxValue : current + count;
            } while (Interlocked.CompareExchange(ref totalFractionSuccesses, updated, current) != current);
        }
        long second = GameMain.gameTick >= 0 ? GameMain.gameTick / 60L : 0L;
        lock (fractionRateWindowLock) {
            AdvanceFractionRateWindow(second);

            int bucketIndex = (int)(second % FractionRateWindowSeconds);
            fractionSuccessBuckets[bucketIndex] += count;
            currentFractionSuccessesPerMinute += count;
            if (currentFractionSuccessesPerMinute > peakFractionSuccessesPerMinute) {
                peakFractionSuccessesPerMinute = currentFractionSuccessesPerMinute;
            }
        }
    }

    private static void AdvanceFractionRateWindow(long second) {
        if (currentFractionRateSecond < 0) {
            currentFractionRateSecond = second;
            return;
        }

        if (second <= currentFractionRateSecond) {
            return;
        }

        long delta = second - currentFractionRateSecond;
        if (delta >= FractionRateWindowSeconds) {
            Array.Clear(fractionSuccessBuckets, 0, fractionSuccessBuckets.Length);
            currentFractionSuccessesPerMinute = 0;
            currentFractionRateSecond = second;
            return;
        }

        for (long bucketSecond = currentFractionRateSecond + 1; bucketSecond <= second; bucketSecond++) {
            int bucketIndex = (int)(bucketSecond % FractionRateWindowSeconds);
            currentFractionSuccessesPerMinute -= fractionSuccessBuckets[bucketIndex];
            if (currentFractionSuccessesPerMinute < 0) {
                currentFractionSuccessesPerMinute = 0;
            }
            fractionSuccessBuckets[bucketIndex] = 0;
        }
        currentFractionRateSecond = second;
    }

    private static void ResetFractionRateWindow() {
        lock (fractionRateWindowLock) {
            Array.Clear(fractionSuccessBuckets, 0, fractionSuccessBuckets.Length);
            currentFractionRateSecond = -1;
            currentFractionSuccessesPerMinute = 0;
        }
    }

    private static ProductOutputInfo FindProduct(List<ProductOutputInfo> products, int itemId, bool mainOnly = false) {
        foreach (ProductOutputInfo product in products) {
            if (product.itemId != itemId) {
                continue;
            }
            if (mainOnly && !product.isMainOutput) {
                continue;
            }
            return product;
        }
        return null;
    }

    private static ProductOutputInfo SelectByNormalOutputPriority(ProductOutputInfo bestSideProduct,
        ProductOutputInfo bestMainProduct, int productStack) {
        ProductOutputInfo product = bestSideProduct;
        if (product == null || product.count < productStack) {
            if (bestMainProduct != null && (product == null || bestMainProduct.count > product.count)) {
                product = bestMainProduct;
            }
        }
        return product;
    }

    private static ProductOutputInfo SelectProductForBeltOutput(List<ProductOutputInfo> products, int productStack) {
        ProductOutputInfo bestSideProduct = null;
        ProductOutputInfo bestMainProduct = null;
        foreach (ProductOutputInfo product in products) {
            if (product.count <= 0) {
                continue;
            }
            if (product.isMainOutput) {
                if (bestMainProduct == null || product.count > bestMainProduct.count) {
                    bestMainProduct = product;
                }
            } else if (bestSideProduct == null || product.count > bestSideProduct.count) {
                bestSideProduct = product;
            }
        }
        return SelectByNormalOutputPriority(bestSideProduct, bestMainProduct, productStack);
    }

    private static bool MatchesRecipeOutputs(List<ProductOutputInfo> products, BaseRecipe recipe) {
        int expectedCount = recipe.OutputMain.Count + recipe.OutputAppend.Count;
        if (products.Count != expectedCount) {
            return false;
        }

        int productIndex = 0;
        for (int i = 0; i < recipe.OutputMain.Count; i++, productIndex++) {
            ProductOutputInfo product = products[productIndex];
            if (!product.isMainOutput || product.itemId != recipe.OutputMain[i].OutputID) {
                return false;
            }
        }

        for (int i = 0; i < recipe.OutputAppend.Count; i++, productIndex++) {
            ProductOutputInfo product = products[productIndex];
            if (product.isMainOutput || product.itemId != recipe.OutputAppend[i].OutputID) {
                return false;
            }
        }

        return true;
    }

    private static void NotifyProductCountIncreased(FractionatorOutputState.FractionatorExtraState extraState,
        int productCount, int productOutputMax, ref bool hasFullProduct) {

        extraState.InvalidateFullProductCache();
        if (productCount >= productOutputMax) {
            hasFullProduct = true;
            extraState.MarkFullProductCache(productOutputMax);
        }
    }

    private static bool AreAllProductsEmpty(List<ProductOutputInfo> products) {
        foreach (ProductOutputInfo product in products) {
            if (product.count > 0) {
                return false;
            }
        }
        return true;
    }

    private static int GetFluidOutputStackToMove(FractionatorComponent fractionator, int preferredStack) {
        if (fractionator.fluidOutputCount >= preferredStack) {
            return preferredStack;
        }
        // 输入已空时释放不足一组的尾料，避免旧 fluidId 被残留流动输出卡住。
        return fractionator.fluidInputCount == 0 ? fractionator.fluidOutputCount : 0;
    }

    private static int GetFluidOutputIncAvg(FractionatorComponent fractionator, int outputStack) {
        if (outputStack <= 0 || fractionator.fluidOutputCount <= 0) {
            return 0;
        }
        return fractionator.fluidOutputInc / fractionator.fluidOutputCount;
    }

    private static void RemoveFluidOutput(ref FractionatorComponent fractionator, int outputStack, int incAvg) {
        fractionator.fluidOutputCount -= outputStack;
        fractionator.fluidOutputInc -= incAvg * outputStack;
        if (fractionator.fluidOutputCount <= 0) {
            fractionator.fluidOutputCount = 0;
            fractionator.fluidOutputInc = 0;
        } else if (fractionator.fluidOutputInc < 0) {
            fractionator.fluidOutputInc = 0;
        }
    }

    private static int GetPreferredFluidOutputStack(bool enableFluidEnhancement, int fluidStack,
        float fluidInputCountPerCargo, bool forceSingleStack) {
        if (forceSingleStack) {
            return 1;
        }
        int inputStack = Mathf.Max(1, Mathf.RoundToInt(fluidInputCountPerCargo));
        return enableFluidEnhancement ? Math.Max(fluidStack, inputStack) : inputStack;
    }

    private static bool TryInsertFluidOutputAtHead(CargoPath cargoPath, int itemId, int maxStack,
        int outputStack, int incAvg, out int insertedStack) {

        insertedStack = outputStack;
        if (cargoPath.TryUpdateItemAtHeadAndFillBlank(itemId, maxStack, (byte)outputStack,
                (byte)Math.Min(255, incAvg * outputStack))) {
            return true;
        }

        if (outputStack <= 1) {
            insertedStack = 0;
            return false;
        }

        // 循环带头部可能已有同类半堆；整组写入失败时，退回单个填充以打破无空位卡死。
        insertedStack = 1;
        if (cargoPath.TryUpdateItemAtHeadAndFillBlank(itemId, maxStack, 1,
                (byte)Math.Min(255, incAvg))) {
            return true;
        }

        insertedStack = 0;
        return false;
    }

    private static void TryOutputFluidToBelt(ref FractionatorComponent fractionator,
        bool enableFluidEnhancement, int fluidStack, CargoTraffic cargoTraffic, int beltId,
        float fluidInputCountPerCargo, bool forceSingleStack = false) {
        if (beltId <= 0 || fractionator.fluidOutputCount <= 0) {
            return;
        }

        // 流动强化不能把高堆叠输入拆回塔等级上限，否则循环带会被侧边输出口反向限速卡住。
        // 无配方/配方未解锁的直通物不应用塔等级集装输出，保持原版单件流动输出语义。
        int preferredStack = GetPreferredFluidOutputStack(enableFluidEnhancement, fluidStack, fluidInputCountPerCargo,
            forceSingleStack);
        CargoPath cargoPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[beltId].segPathId);
        if (cargoPath == null) {
            return;
        }
        for (int i = 0; i < MaxOutputTimes && fractionator.fluidOutputCount > 0; i++) {
            int outputStack = GetFluidOutputStackToMove(fractionator, preferredStack);
            if (outputStack <= 0) {
                break;
            }
            int fluidOutputIncAvg = GetFluidOutputIncAvg(fractionator, outputStack);
            if (!TryInsertFluidOutputAtHead(cargoPath, fractionator.fluidId, preferredStack, outputStack,
                    fluidOutputIncAvg, out int insertedStack)) {
                break;
            }
            RemoveFluidOutput(ref fractionator, insertedStack, fluidOutputIncAvg);
        }
    }

    #endregion

    #region 模组存档接口实现

    /// <summary>
    /// 将该分馏域状态写入存档。
    /// </summary>
    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("TotalFractionSuccesses", bw => bw.Write(Interlocked.Read(ref totalFractionSuccesses))),
            ("PeakFractionSuccessesPerMinute", bw => bw.Write(Interlocked.Read(ref peakFractionSuccessesPerMinute)))
        );
    }

    /// <summary>
    /// 从存档读取该分馏域状态。
    /// </summary>
    public static void Import(BinaryReader r) {
        ResetFractionRateWindow();
        r.ReadBlocks(
            ("TotalFractionSuccesses", br =>
                Interlocked.Exchange(ref totalFractionSuccesses, Math.Max(0, br.ReadInt64()))),
            ("PeakFractionSuccessesPerMinute", br =>
                Interlocked.Exchange(ref peakFractionSuccessesPerMinute, Math.Max(0, br.ReadInt64())))
        );
    }

    /// <summary>
    /// 切换或进入其他存档时重置该分馏域状态。
    /// </summary>
    public static void IntoOtherSave() {
        Interlocked.Exchange(ref totalFractionSuccesses, 0);
        Interlocked.Exchange(ref peakFractionSuccessesPerMinute, 0);
        ResetFractionRateWindow();
    }

    #endregion

    #endregion
}

#region 扩展输出状态

/// <summary>
/// 分馏塔扩展产物状态缓存、存档和同步逻辑。
/// </summary>
public static class FractionatorOutputState {
    #region 分馏塔产物输出拓展

    /// <summary>
    /// 存储分馏塔所有产物。结构：
    /// (planetId, entityId) => List&lt;ProductOutputInfo&gt;
    /// </summary>
    public sealed class FractionatorExtraState {
        /// <summary>
        /// 保存该分馏塔当前累计的扩展产物列表。
        /// </summary>
        public readonly List<ProductOutputInfo> Products = [];
        /// <summary>
        /// 保存热路径复用的临时产物缓冲。
        /// </summary>
        public readonly ProductOutputBuffer ScratchOutputs = new();
        /// <summary>
        /// 保存当前扩展产物列表包含的产物类型标记。
        /// </summary>
        public byte CurrentOutputFlags;
        /// <summary>
        /// 保存当前运行时识别出的主产物物品 ID。
        /// </summary>
        public ProductOutputInfo PrimaryProduct;
        private bool cachedRecipeValid;
        private int cachedFluidId;
        private ERecipe cachedRecipeType;
        private BaseRecipe cachedRecipe;
        private bool runtimeSchemaValid;
        private int runtimeSchemaFluidId;
        private ERecipe runtimeSchemaRecipeType;
        private BaseRecipe runtimeSchemaRecipe;
        private int runtimeSchemaProductId;
        private int runtimeSchemaProductCount;
        private bool fullProductCacheValid;
        private int fullProductCacheThreshold;
        private bool fullProductCacheValue;

        /// <summary>
        /// 按配方类型和输入物品读取分馏配方。
        /// </summary>
        public BaseRecipe GetRecipe(ERecipe recipeType, int fluidId) {
            if (cachedRecipeValid
                && cachedFluidId == fluidId
                && cachedRecipeType == recipeType) {
                return cachedRecipe;
            }

            cachedRecipeValid = true;
            cachedFluidId = fluidId;
            cachedRecipeType = recipeType;
            cachedRecipe = RecipeManager.GetRecipe<BaseRecipe>(recipeType, fluidId);
            return cachedRecipe;
        }

        /// <summary>
        /// 按配方类型和输入物品读取分馏配方。
        /// </summary>
        public T GetRecipe<T>(ERecipe recipeType, int fluidId) where T : BaseRecipe {
            return GetRecipe(recipeType, fluidId) as T;
        }

        /// <summary>
        /// 清除扩展状态中的配方查询缓存。
        /// </summary>
        public void ClearRecipeCache() {
            cachedRecipeValid = false;
            cachedFluidId = 0;
            cachedRecipeType = default;
            cachedRecipe = null;
            ClearRuntimeSchema();
        }

        /// <summary>
        /// 尝试读取分馏塔当前缓存的运行时输出架构。
        /// </summary>
        public bool TryGetRuntimeSchema(ERecipe recipeType, int fluidId, BaseRecipe recipe, int productId,
            out ProductOutputInfo primaryProduct) {

            primaryProduct = null;
            if (!runtimeSchemaValid
                || runtimeSchemaFluidId != fluidId
                || runtimeSchemaRecipeType != recipeType
                || !ReferenceEquals(runtimeSchemaRecipe, recipe)
                || runtimeSchemaProductId != productId
                || runtimeSchemaProductCount != Products.Count) {
                return false;
            }
            if (runtimeSchemaProductCount <= 0) {
                return true;
            }
            if (PrimaryProduct == null || PrimaryProduct.itemId != productId) {
                return false;
            }

            primaryProduct = PrimaryProduct;
            return true;
        }

        /// <summary>
        /// 记录分馏塔当前运行时输出架构。
        /// </summary>
        public void MarkRuntimeSchema(ERecipe recipeType, int fluidId, BaseRecipe recipe, int productId,
            ProductOutputInfo primaryProduct) {

            runtimeSchemaValid = true;
            runtimeSchemaFluidId = fluidId;
            runtimeSchemaRecipeType = recipeType;
            runtimeSchemaRecipe = recipe;
            runtimeSchemaProductId = productId;
            runtimeSchemaProductCount = Products.Count;
            PrimaryProduct = primaryProduct;
        }

        /// <summary>
        /// 清除分馏塔运行时输出架构缓存。
        /// </summary>
        public void ClearRuntimeSchema() {
            runtimeSchemaValid = false;
            runtimeSchemaFluidId = 0;
            runtimeSchemaRecipeType = default;
            runtimeSchemaRecipe = null;
            runtimeSchemaProductId = 0;
            runtimeSchemaProductCount = 0;
            PrimaryProduct = null;
        }

        /// <summary>
        /// 判断分馏塔扩展产物缓存是否已有足够输出。
        /// </summary>
        public bool HasFullProduct(int countThreshold, bool forceRefresh = false) {
            if (!forceRefresh
                && fullProductCacheValid
                && fullProductCacheThreshold == countThreshold) {
                return fullProductCacheValue;
            }

            bool hasFullProduct = false;
            foreach (ProductOutputInfo product in Products) {
                if (product.count >= countThreshold) {
                    hasFullProduct = true;
                    break;
                }
            }

            fullProductCacheValid = true;
            fullProductCacheThreshold = countThreshold;
            fullProductCacheValue = hasFullProduct;
            return hasFullProduct;
        }

        /// <summary>
        /// 标记扩展产物满载缓存失效。
        /// </summary>
        public void InvalidateFullProductCache() {
            fullProductCacheValid = false;
        }

        /// <summary>
        /// 记录扩展产物已达到指定输出阈值。
        /// </summary>
        public void MarkFullProductCache(int countThreshold) {
            fullProductCacheValid = true;
            fullProductCacheThreshold = countThreshold;
            fullProductCacheValue = true;
        }
    }

    private static readonly ConcurrentDictionary<(int, int), FractionatorExtraState> outputDic = [];

    /// <summary>
    /// 单个星球的分馏塔扩展状态数组和版本号。
    /// </summary>
    private sealed class FractionatorExtraStateArray(int length) {
        /// <summary>
        /// 保存该星球分馏塔扩展状态的同步锁。
        /// </summary>
        public readonly object SyncRoot = new();
        /// <summary>
        /// 保存该星球按实体 ID 索引的分馏塔扩展状态。
        /// </summary>
        public FractionatorExtraState[] States = new FractionatorExtraState[length];
        /// <summary>
        /// 保存该星球分馏塔扩展状态表的变更版本。
        /// </summary>
        public int Version;
    }

    private static readonly ConcurrentDictionary<int, FractionatorExtraStateArray> outputStateArraysByPlanet = [];
    private static int outputStateArrayGeneration;
    [ThreadStatic] private static int cachedOutputStatePlanetId;
    [ThreadStatic] private static int cachedOutputStateVersion;
    [ThreadStatic] private static int cachedOutputStateGeneration;
    [ThreadStatic] private static FractionatorExtraStateArray cachedOutputStateArray;

    private static FractionatorExtraState[] EnsureOutputStateArray(int planetId, int entityId,
        PlanetFactory factory = null) {
        int generation = Volatile.Read(ref outputStateArrayGeneration);
        FractionatorExtraStateArray cachedArray = cachedOutputStateArray;
        if (cachedArray != null
            && cachedOutputStatePlanetId == planetId
            && cachedOutputStateVersion == cachedArray.Version
            && cachedOutputStateGeneration == generation
            && (uint)entityId < (uint)cachedArray.States.Length) {
            return cachedArray.States;
        }

        int minLength = entityId + 1;
        if (factory?.entityPool != null && factory.entityPool.Length > minLength) {
            minLength = factory.entityPool.Length;
        }
        minLength = Math.Max(minLength, 64);

        FractionatorExtraStateArray stateArray =
            outputStateArraysByPlanet.GetOrAdd(planetId, _ => new FractionatorExtraStateArray(minLength));
        FractionatorExtraState[] states;
        int version;
        lock (stateArray.SyncRoot) {
            states = stateArray.States;
            if (states.Length <= entityId) {
                Array.Resize(ref states, minLength);
                stateArray.States = states;
                stateArray.Version++;
            }
            version = stateArray.Version;
        }

        cachedOutputStatePlanetId = planetId;
        cachedOutputStateVersion = version;
        cachedOutputStateGeneration = generation;
        cachedOutputStateArray = stateArray;
        return states;
    }

    /// <summary>
    /// 读取分馏塔扩展输出状态。
    /// </summary>
    public static void OutputExtendImport(BinaryReader r) {
        outputDic.Clear();
        outputStateArraysByPlanet.Clear();
        Interlocked.Increment(ref outputStateArrayGeneration);
        cachedOutputStatePlanetId = 0;
        cachedOutputStateVersion = 0;
        cachedOutputStateGeneration = 0;
        cachedOutputStateArray = null;
        int fractionatorNum = r.ReadInt32();
        for (int i = 0; i < fractionatorNum; i++) {
            int planetId = r.ReadInt32();
            int entityId = r.ReadInt32();
            FractionatorExtraState state = new();
            int outputKinds = r.ReadInt32();
            for (int j = 0; j < outputKinds; j++) {
                bool isMainOutput = r.ReadBoolean();
                int outputId = r.ReadInt32();
                int outputCount = r.ReadInt32();
                if (!LDB.items.Exist(outputId)) {
                    continue;
                }
                state.Products.Add(new(isMainOutput, outputId, outputCount));
            }
            outputDic.TryAdd((planetId, entityId), state);
            EnsureOutputStateArray(planetId, entityId)[entityId] = state;
        }
    }

    /// <summary>
    /// 写入分馏塔扩展输出状态。
    /// </summary>
    public static void OutputExtendExport(BinaryWriter w) {
        w.Write(outputDic.Count);
        foreach (var p in outputDic) {
            w.Write(p.Key.Item1);
            w.Write(p.Key.Item2);
            List<ProductOutputInfo> products = p.Value.Products;
            w.Write(products.Count);
            foreach (ProductOutputInfo outputItem in products) {
                w.Write(outputItem.isMainOutput);
                w.Write(outputItem.itemId);
                w.Write(outputItem.count);
            }
        }
    }

    /// <summary>
    /// 重置分馏塔扩展输出状态。
    /// </summary>
    public static void OutputExtendIntoOtherSave() {
        outputDic.Clear();
        outputStateArraysByPlanet.Clear();
        Interlocked.Increment(ref outputStateArrayGeneration);
        cachedOutputStatePlanetId = 0;
        cachedOutputStateVersion = 0;
        cachedOutputStateGeneration = 0;
        cachedOutputStateArray = null;
    }

    /// <summary>
    /// 读取指定分馏塔当前可显示的产物列表。
    /// </summary>
    public static List<ProductOutputInfo> products(this FractionatorComponent fractionator,
        PlanetFactory factory) {
        return fractionator.GetExtraState(factory).Products;
    }

    /// <summary>
    /// 读取或创建指定分馏塔的扩展运行状态。
    /// </summary>
    public static FractionatorExtraState GetExtraState(this FractionatorComponent fractionator,
        PlanetFactory factory) {
        int planetId = factory.planetId;
        int entityId = fractionator.entityId;
        FractionatorExtraState[] states = EnsureOutputStateArray(planetId, entityId, factory);
        FractionatorExtraState state = states[entityId];
        if (state != null) {
            return state;
        }

        var key = (planetId, entityId);
        if (outputDic.TryGetValue(key, out state)) {
            states[entityId] = state;
            return state;
        }

        state = new FractionatorExtraState();
        states[entityId] = state;
        outputDic[key] = state;
        return state;
    }

    /// <summary>
    /// 清除指定实体对应的分馏塔扩展状态。
    /// </summary>
    public static void ClearExtraState(PlanetFactory factory, int entityId) {
        if (factory == null || entityId <= 0) {
            return;
        }
        int planetId = factory.planetId;
        outputDic.TryRemove((planetId, entityId), out _);
        if (outputStateArraysByPlanet.TryGetValue(planetId, out FractionatorExtraStateArray stateArray)) {
            lock (stateArray.SyncRoot) {
                FractionatorExtraState[] states = stateArray.States;
                if (entityId < states.Length) {
                    states[entityId] = null;
                }
            }
        }
        if (cachedOutputStatePlanetId == planetId
            && cachedOutputStateArray != null
            && cachedOutputStateGeneration == Volatile.Read(ref outputStateArrayGeneration)) {
            lock (cachedOutputStateArray.SyncRoot) {
                FractionatorExtraState[] states = cachedOutputStateArray.States;
                if (cachedOutputStateVersion == cachedOutputStateArray.Version
                    && entityId < states.Length) {
                    states[entityId] = null;
                }
            }
        }
    }

    #endregion
}

#endregion

#region 蓝图/复制粘贴/Nebula 边界

/// <summary>
/// 在实体移除前清理基础塔的多产物缓存投影。
/// </summary>
public static class FractionatorStateCleanup {
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.RemoveEntityWithComponents))]
    private static void PlanetFactory_RemoveEntityWithComponents_Prefix(PlanetFactory __instance, int id) {
        FractionatorOutputState.ClearExtraState(__instance, id);
    }
}

/// <summary>
/// 在同一蓝图参数数组中共存读写 FE 分馏塔的多个实例状态块。
/// </summary>
internal static class FractionatorBlueprintParameters {
    private const int BlockLength = 3;

    /// <summary>
    /// 更新已有状态块；不存在时在参数尾部追加新的三整数状态块。
    /// </summary>
    public static int[] Upsert(int[] parameters, int magic, int version, int value) {
        int[] result = parameters ?? [];
        int index = Find(result, magic, version);
        if (index >= 0) {
            result = (int[])result.Clone();
            result[index + 2] = value;
            return result;
        }

        int originalLength = result.Length;
        Array.Resize(ref result, originalLength + BlockLength);
        result[originalLength] = magic;
        result[originalLength + 1] = version;
        result[originalLength + 2] = value;
        return result;
    }

    /// <summary>
    /// 从任意位置读取指定状态块，允许不同 FE 实例状态共用参数数组。
    /// </summary>
    public static bool TryRead(int[] parameters, int magic, int version, out int value) {
        int index = Find(parameters, magic, version);
        value = index >= 0 ? parameters[index + 2] : 0;
        return index >= 0;
    }

    private static int Find(int[] parameters, int magic, int version) {
        if (parameters == null || parameters.Length < BlockLength) {
            return -1;
        }

        for (int i = parameters.Length - BlockLength; i >= 0; i--) {
            if (parameters[i] == magic && parameters[i + 1] == version) {
                return i;
            }
        }
        return -1;
    }
}

#endregion

}
