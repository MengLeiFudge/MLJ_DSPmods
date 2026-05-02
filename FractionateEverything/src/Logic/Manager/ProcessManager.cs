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
using FE.UI.View.ProgressTask;
using HarmonyLib;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

/// <summary>
/// 修改所有分馏塔的处理逻辑，以及对应的显示。
/// </summary>
public static class ProcessManager {
    private delegate void FractionatorUpdateHandler(ref FractionatorComponent fractionator,
        PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister,
        ref uint result);

    private static readonly FractionatorUpdateHandler[] updateHandlersByBuildingOffset = [
        UpdateInteractionTower,
        UpdateMineralReplicationTower,
        UpdatePointAggregateTower,
        UpdateConversionTower,
        UpdateRectificationTower,
    ];
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

    public static void AddTranslations() {
        Register("交互模式", "Interaction mode");
        Register("原料堆积", "Fluid overflow");
        Register("搬运模式", "Transport mode");
        Register("缺少精华", "Lack of fragments", "缺少残片");
        Register("分馏永动", "Frac forever");
        Register("无配方", "No recipe");
        Register("主产物", "Main product");
        Register("副产物", "Append product");
        Register("流动", "Flow");
        Register("损毁", "Destroy");
        Register("流体输出", "Fluid output");
        Register("配方强化", "Recipe enhancement");
        Register("单锁", "Single Lock");
        Register("单锁产物数目", "Single-lock output count");
        Register("未锁定", "Not locked");
        Register("右键设为单锁", "Right-click to lock this output");
        Register("右键清除单锁", "Right-click to clear single lock");
        Register("已锁定单路产物：{0}", "Locked output: {0}");
        Register("已清除单路锁定", "Single lock cleared");
        Register("锁定产物无效，已清除", "Locked output invalid, cleared");
    }

    #region Field

    public static int MaxOutputTimes = 2;
    public static int MaxBeltSpeed = 30;
    public static int BaseFracFluidInputCargoMax = 40;
    public static int BaseFracProductOutputMax = 20;
    public static int BaseFracFluidOutputMax = 20;
    private static readonly double[] incTableFixedRatio = new double[Cargo.incTableMilli.Length];
    public static readonly List<ProductOutputInfo> emptyOutputs = [];
    public static readonly int MaxLevel = 12;
    private const int SacrificeTowerTypeCount = IFE精馏塔 - IFE交互塔 + 1;
    private const float SacrificeBoostStep = 0.05f;
    private const float SacrificeBoostCapTrait1 = 0.75f;
    private const float SacrificeBoostCapTrait2 = 1.00f;
    private const int ZeroPressureInternalStackCap = 8;
    public static readonly float[] ReinforcementSuccessRatioArr = new float[MaxLevel + 1];
    public static readonly float[] ReinforcementBonusArr = new float[MaxLevel + 1];
    private static readonly int[] sacrificeStepIndex = new int[SacrificeTowerTypeCount];
    public const byte OutputFlagMain = 1 << 0;
    public const byte OutputFlagSide = 1 << 1;
    public const byte OutputFlagFluid = 1 << 2;
    /// <summary>
    /// 累计分馏成功次数（用于任务系统）
    /// </summary>
    public static long totalFractionSuccesses;
    private const int FractionRateWindowSeconds = 60;
    private static readonly long[] fractionSuccessBuckets = new long[FractionRateWindowSeconds];
    private static long currentFractionRateSecond = -1;
    private static long currentFractionSuccessesPerMinute;
    public static long peakFractionSuccessesPerMinute;
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
    private const int FractionatorPerfStageZeroPressure = 3;
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
    private static readonly long[] fractionatorPerfFeUpdateCallsByType = new long[updateHandlersByBuildingOffset.Length];
    private static readonly long[] fractionatorPerfFeUpdateTicksByType = new long[updateHandlersByBuildingOffset.Length];
    private static long fractionatorPerfWindowStartTick = -1;
    private static int fractionatorPerfLogging;

    public static byte GetCurrentOutputFlags(this FractionatorComponent fractionator,
        PlanetFactory factory) {

        if (factory == null) return 0;
        return fractionator.GetExtraState(factory).CurrentOutputFlags;
    }

    private static void SetCurrentOutputFlags(PlanetFactory factory,
        BuildingManager.FractionatorExtraState extraState,
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

    private static void RecordFractionatorPerf(int bucket, int buildingID, long elapsedTicks) {
        if (!EnableFractionatorPerfProbe) {
            return;
        }
        fractionatorPerfCalls[bucket]++;
        fractionatorPerfTicks[bucket] += elapsedTicks;
        UpdateFractionatorPerfMax(fractionatorPerfMaxTicks, bucket, elapsedTicks);

        int handlerIndex = buildingID - IFE交互塔;
        if (bucket == FractionatorPerfUpdateFe
            && handlerIndex >= 0
            && handlerIndex < fractionatorPerfFeUpdateCallsByType.Length) {
            fractionatorPerfFeUpdateCallsByType[handlerIndex]++;
            fractionatorPerfFeUpdateTicksByType[handlerIndex] += elapsedTicks;
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
        } finally {
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
               + FormatFeType(IFE交互塔, calls, ticks)
               + ";"
               + FormatFeType(IFE矿物复制塔, calls, ticks)
               + ";"
               + FormatFeType(IFE点数聚集塔, calls, ticks)
               + ";"
               + FormatFeType(IFE转化塔, calls, ticks)
               + ";"
               + FormatFeType(IFE精馏塔, calls, ticks);
    }

    private static string FormatStagePerf(long[] calls, long[] ticks, long[] maxTicks) {
        StringBuilder builder = new("feStages=");
        AppendStagePerf(builder, "prepare", FractionatorPerfStagePrepare, calls, ticks, maxTicks);
        AppendStagePerf(builder, "process", FractionatorPerfStageProcess, calls, ticks, maxTicks);
        AppendStagePerf(builder, "flush", FractionatorPerfStageFlushDeltas, calls, ticks, maxTicks);
        AppendStagePerf(builder, "zeroPressure", FractionatorPerfStageZeroPressure, calls, ticks, maxTicks);
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
        int index = buildingID - IFE交互塔;
        long callCount = calls[index];
        double avgUs = callCount > 0 ? ticks[index] * 1000000.0 / Stopwatch.Frequency / callCount : 0.0;
        return $"{buildingID}:{callCount}/{avgUs:F3}us";
    }

    #endregion

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
    }

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
    /// 修改分馏塔的运行逻辑。
    /// </summary>
    /// <remarks>
    /// <para>注意：新增分馏塔产物输出使用Mod拓展存储。数据结构如下：</para>
    /// <ul>
    /// <li>int __instance.productId: 第一个主输出的ID，无用</li>
    /// <li>int __instance.productOutputCount: 第一个主输出的数目，无用</li>
    /// <li>int __instance.productOutputTotal: 第一个主输出的统计数目</li>
    /// <li>List&lt;ProductOutputInfo&gt; __instance.productOutputs(factory): 存储所有产物输出</li>
    /// </ul>
    /// <para>除此之外，分馏判定结果由<see cref="FE.Logic.Recipe.BaseRecipe.GetOutputs"/>得到。</para>
    /// </remarks>
    public static uint InternalUpdateWithModDispatch(ref FractionatorComponent fractionator,
        PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister) {
        long perfStart = GetFractionatorPerfTimestamp();
        int buildingID = factory.entityPool[fractionator.entityId].protoId;
        int handlerIndex = buildingID - IFE交互塔;
        if (handlerIndex >= 0 && handlerIndex < updateHandlersByBuildingOffset.Length) {
            try {
                uint result = 0;
                updateHandlersByBuildingOffset[handlerIndex](ref fractionator, factory, power, signPool,
                    productRegister, consumeRegister, ref result);
                return result;
            } finally {
                RecordFractionatorPerf(FractionatorPerfUpdateFe, buildingID, GetFractionatorPerfElapsed(perfStart));
            }
        }

        //原版分馏塔不做处理
        try {
            return fractionator.InternalUpdate(factory, power, signPool, productRegister, consumeRegister);
        } finally {
            RecordFractionatorPerf(FractionatorPerfUpdateVanilla, buildingID, GetFractionatorPerfElapsed(perfStart));
        }
    }

    private static void UpdateInteractionTower(ref FractionatorComponent fractionator,
        PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister,
        ref uint result) {
        InternalUpdate<BuildingTrainRecipe>(ref fractionator, factory, power, signPool, productRegister,
            consumeRegister, ref result, ERecipe.BuildingTrain);
    }

    private static void UpdateMineralReplicationTower(ref FractionatorComponent fractionator,
        PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister,
        ref uint result) {
        InternalUpdate<MineralCopyRecipe>(ref fractionator, factory, power, signPool, productRegister,
            consumeRegister, ref result, ERecipe.MineralCopy);
    }

    private static void UpdatePointAggregateTower(ref FractionatorComponent fractionator,
        PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister,
        ref uint result) {
        InternalUpdate<PointAggregateRecipe>(ref fractionator, factory, power, signPool, productRegister,
            consumeRegister, ref result, ERecipe.PointAggregate);
    }

    private static void UpdateConversionTower(ref FractionatorComponent fractionator,
        PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister,
        ref uint result) {
        InternalUpdate<ConversionRecipe>(ref fractionator, factory, power, signPool, productRegister,
            consumeRegister, ref result, ERecipe.Conversion);
    }

    private static void UpdateRectificationTower(ref FractionatorComponent fractionator,
        PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister,
        ref uint result) {
        InternalUpdate<RectificationRecipe>(ref fractionator, factory, power, signPool, productRegister,
            consumeRegister, ref result, ERecipe.Rectification);
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

    private static ProductOutputInfo SelectProductForBeltOutput(List<ProductOutputInfo> products, int productStack,
        int lockedOutputId, out bool flushNonLockedProduct) {
        ProductOutputInfo bestSideProduct = null;
        ProductOutputInfo bestMainProduct = null;
        ProductOutputInfo bestNonLockedSideProduct = null;
        ProductOutputInfo bestNonLockedMainProduct = null;
        foreach (ProductOutputInfo p in products) {
            if (p.count <= 0) {
                continue;
            }
            if (p.isMainOutput) {
                if (bestMainProduct == null || p.count > bestMainProduct.count) {
                    bestMainProduct = p;
                }
                if (lockedOutputId != 0
                    && p.itemId != lockedOutputId
                    && (bestNonLockedMainProduct == null || p.count > bestNonLockedMainProduct.count)) {
                    bestNonLockedMainProduct = p;
                }
            } else {
                if (bestSideProduct == null || p.count > bestSideProduct.count) {
                    bestSideProduct = p;
                }
                if (lockedOutputId != 0
                    && p.itemId != lockedOutputId
                    && (bestNonLockedSideProduct == null || p.count > bestNonLockedSideProduct.count)) {
                    bestNonLockedSideProduct = p;
                }
            }
        }

        ProductOutputInfo nonLockedProduct = SelectByNormalOutputPriority(bestNonLockedSideProduct,
            bestNonLockedMainProduct, productStack);
        if (nonLockedProduct != null) {
            flushNonLockedProduct = true;
            return nonLockedProduct;
        }

        flushNonLockedProduct = false;
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

    private static void NotifyProductCountIncreased(BuildingManager.FractionatorExtraState extraState,
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

    private static int GetFluidOutputIncAvg(FractionatorComponent fractionator, int buildingID, int outputStack) {
        if (outputStack <= 0 || fractionator.fluidOutputCount <= 0) {
            return 0;
        }
        if (buildingID == IFE点数聚集塔) {
            return fractionator.fluidOutputInc >= 4 * outputStack ? 4 : 0;
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

    private static void TryOutputFluidToBelt(ref FractionatorComponent fractionator, int buildingID,
        bool enableFluidEnhancement, int fluidStack, CargoTraffic cargoTraffic, int beltId,
        float fluidInputCountPerCargo) {
        if (beltId <= 0 || fractionator.fluidOutputCount <= 0) {
            return;
        }

        if (enableFluidEnhancement) {
            for (int i = 0; i < MaxOutputTimes && fractionator.fluidOutputCount > 0; i++) {
                int outputStack = GetFluidOutputStackToMove(fractionator, fluidStack);
                if (outputStack <= 0) {
                    break;
                }
                int fluidOutputIncAvg = GetFluidOutputIncAvg(fractionator, buildingID, outputStack);
                if (!cargoTraffic.TryInsertItemAtHead(beltId, fractionator.fluidId, (byte)outputStack,
                        (byte)Math.Min(255, fluidOutputIncAvg * outputStack))) {
                    break;
                }
                RemoveFluidOutput(ref fractionator, outputStack, fluidOutputIncAvg);
            }
            return;
        }

        CargoPath cargoPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[beltId].segPathId);
        if (cargoPath == null) {
            return;
        }
        int preferredStack = Mathf.Max(1, Mathf.RoundToInt(fluidInputCountPerCargo));
        for (int i = 0; i < MaxOutputTimes && fractionator.fluidOutputCount > 0; i++) {
            int outputStack = GetFluidOutputStackToMove(fractionator, preferredStack);
            if (outputStack <= 0) {
                break;
            }
            int fluidOutputIncAvg = GetFluidOutputIncAvg(fractionator, buildingID, outputStack);
            if (!cargoPath.TryUpdateItemAtHeadAndFillBlank(fractionator.fluidId,
                    Mathf.CeilToInt((float)(fluidInputCountPerCargo / outputStack - 0.1)),
                    (byte)outputStack,
                    (byte)Math.Min(255, fluidOutputIncAvg * outputStack))) {
                break;
            }
            RemoveFluidOutput(ref fractionator, outputStack, fluidOutputIncAvg);
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
        bool isInteractionTower = buildingID == IFE交互塔;
        bool isMineralReplicationTower = buildingID == IFE矿物复制塔;
        bool isPointAggregateTower = buildingID == IFE点数聚集塔;
        bool isConversionTower = buildingID == IFE转化塔;
        bool enableMassEnergyFission = isMineralReplicationTower && MineralReplicationTower.EnableMassEnergyFission;
        //所有产物输出
        BuildingManager.FractionatorExtraState extraState = __instance.GetExtraState(factory);
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
            if (isConversionTower) {
                __instance.SetLockedOutput(factory,
                    __instance.NormalizeLockedOutput(factory, __instance.GetLockedOutput(factory)));
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
                // C8: 单路锁定 - 配方变化时按新配方校验，兼容复制粘贴/蓝图带过来的预设锁定。
                if (isConversionTower) {
                    __instance.SetLockedOutput(factory,
                        __instance.NormalizeLockedOutput(factory, __instance.GetLockedOutput(factory)));
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
        float buildingSuccessBoost = runtimeConfig.SuccessBoost;
        bool enableFracForever = runtimeConfig.EnableFluidEnhancement;
        int fluidInputCargoMax = BaseFracFluidInputCargoMax;
        int productOutputMax = runtimeConfig.ProductOutputMax;
        int fluidOutputMax = runtimeConfig.FluidOutputMax;
        bool moveDirectly = recipe == null || !RecipeGrowthQueries.IsUnlocked(recipe);
        RecipeGrowthContext growthContext = default;
        bool growthContextReady = false;
        bool producedMainThisTick = false;
        bool producedSideThisTick = false;
        bool producedFluidThisTick = false;
        bool hasFullProduct = extraState.HasFullProduct(productOutputMax);
        bool needRecheckFullProduct = false;
        int consumedInputThisTick = 0;
        int successCountThisTick = 0;
        int fragmentRewardThisTick = 0;
        List<ProductOutputInfo> productRegisterDeltas = null;
        RecordFractionatorPerfDetail(FractionatorPerfDetailPrepareConfig, GetFractionatorPerfElapsed(perfConfigStart));
        RecordFractionatorPerfStage(FractionatorPerfStagePrepare, GetFractionatorPerfElapsed(perfStageStart));
        perfStageStart = GetFractionatorPerfTimestamp();
        perfDetailStart = perfStageStart;
        if (__instance.fluidInputCount > 0
            && (!hasFullProduct || enableFracForever)
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
            // 虚空喷涂 - 点数聚集塔在 Level >= 6 时自动补充增产点数
            if (isPointAggregateTower && PointAggregateTower.EnableVoidSpray) {
                AddIncToItem(__instance.fluidInputCount, ref __instance.fluidInputInc);
            }
            // 质能裂变 - 矿物复制塔在 Level >= 6 时，维持池中点数在目标值以上；
            // 当池量不足时，批量消耗原料填满点数池（每个原料+25点，零压循环激活时+50点）。
            // 取用时：若平均增产点数不足10，从池中补足至10。
            if (enableMassEnergyFission && __instance.fluidInputCount > 0) {
                int pointsPerItem = MineralReplicationTower.EnableZeroPressureCycle ? 40 : 25;
                int poolTarget = __instance.fluidInputCount * 15;
                int pool = __instance.GetFissionPointPool(factory);
                // 池量不足时批量消耗原料补满
                if (pool <= 0) {
                    int pointsNeeded = poolTarget - pool;
                    int itemsToConsume = (pointsNeeded + pointsPerItem - 1) / pointsPerItem;// 向上取整
                    int itemsAvail = __instance.fluidInputCount;
                    int itemsConsumed = Math.Min(itemsToConsume, itemsAvail);
                    if (itemsConsumed > 0) {
                        int incAvgForConsume = __instance.fluidInputInc > 0 && __instance.fluidInputCount > 0
                            ? __instance.fluidInputInc / __instance.fluidInputCount
                            : 0;
                        __instance.fluidInputCount -= itemsConsumed;
                        if (__instance.fluidInputCount < 0) __instance.fluidInputCount = 0;
                        __instance.fluidInputCargoCount -= (float)itemsConsumed / fluidInputCountPerCargo;
                        if (__instance.fluidInputCargoCount < 0f) __instance.fluidInputCargoCount = 0f;
                        __instance.fluidInputInc -= incAvgForConsume * itemsConsumed;
                        if (__instance.fluidInputInc < 0) __instance.fluidInputInc = 0;
                        pool += itemsConsumed * pointsPerItem;
                        __instance.SetFissionPointPool(factory, pool);
                    }
                }
                // 取用：若输入平均点数不足10，从池中补足
                if (__instance.fluidInputCount > 0) {
                    int avgInc = __instance.fluidInputInc / __instance.fluidInputCount;
                    if (avgInc < 10) {
                        int needed = (10 - avgInc) * __instance.fluidInputCount;
                        int toUse = Math.Min(pool, needed);
                        if (toUse > 0) {
                            __instance.fluidInputInc += toUse;
                            pool -= toUse;
                            __instance.SetFissionPointPool(factory, pool);
                        }
                    }
                }
            }
            int batchCount = Math.Min(__instance.progress / 10000, __instance.fluidInputCount);
            if (batchCount > 0) {
                __instance.progress -= batchCount * 10000;
                int fluidInputIncAvg = __instance.fluidInputInc <= 0 || __instance.fluidInputCount <= 0
                    ? 0
                    : __instance.fluidInputInc / __instance.fluidInputCount;
                if (!__instance.incUsed)
                    __instance.incUsed = fluidInputIncAvg > 0;

                // 判断是否直通（永动且满了，或者无配方/配方锁定）
                bool isForcedPassthrough =
                    moveDirectly || (enableFracForever && hasFullProduct);
                FractionationBatchResult batchResult;
                if (isForcedPassthrough) {
                    outputBuffer.Clear();
                    batchResult = new FractionationBatchResult {
                        InputRemoveCount = batchCount,
                        ConsumedRegisterCount = 0,
                        SuccessCount = 0,
                        DestroyedCount = 0,
                        PassThroughCount = batchCount,
                    };
                    __instance.fluidInputInc -= fluidInputIncAvg * batchCount;
                    if (__instance.fluidInputInc < 0) __instance.fluidInputInc = 0;
                } else {
                    float pointsBonus = (float)MaxTableMilli(fluidInputIncAvg) * plrRatio;
                    float successBoost = buildingSuccessBoost + Achievements.GetSuccessRateBonus();
                    // C8: 单路锁定 - 在调用 GetOutputs 前设置当前锁定产物ID
                    if (isConversionTower) {
                        ConversionRecipe.CurrentLockedOutputId = __instance.GetLockedOutput(factory);
                    }
                    perfDetailStart = GetFractionatorPerfTimestamp();
                    try {
                        batchResult = recipe.GetOutputsBatchFast(ref __instance.seed, pointsBonus, successBoost,
                            batchCount, fluidInputIncAvg, ref __instance.fluidInputInc, outputBuffer);
                    }
                    finally {
                        if (isConversionTower) {
                            ConversionRecipe.CurrentLockedOutputId = 0;
                        }
                    }
                    RecordFractionatorPerfDetail(FractionatorPerfDetailProcessGetOutputs,
                        GetFractionatorPerfElapsed(perfDetailStart));
                }

                // 因果溯源 - 转化塔在 Level >= 6 时，50%概率让损毁不消耗原料。
                if (isConversionTower && ConversionTower.EnableCausalTracing && batchResult.DestroyedCount > 0) {
                    int savedDestroyed = BaseRecipe.RollBinomialApprox(ref __instance.seed,
                        batchResult.DestroyedCount, 0.5f);
                    if (savedDestroyed > 0) {
                        batchResult.InputRemoveCount -= savedDestroyed;
                        batchResult.ConsumedRegisterCount -= savedDestroyed;
                        __instance.fluidInputInc += fluidInputIncAvg * savedDestroyed;
                    }
                }

                __instance.fractionSuccess = batchResult.HasOutput;

                if (batchResult.InputRemoveCount > 0) {
                    __instance.fluidInputCount -= batchResult.InputRemoveCount;
                    if (__instance.fluidInputCount < 0) __instance.fluidInputCount = 0;
                    __instance.fluidInputCargoCount -= batchResult.InputRemoveCount / fluidInputCountPerCargo;
                    if (__instance.fluidInputCargoCount < 0f) __instance.fluidInputCargoCount = 0f;
                }

                if (batchResult.PassThroughCount > 0) {
                    __instance.fluidOutputCount += batchResult.PassThroughCount;
                    __instance.fluidOutputTotal += batchResult.PassThroughCount;
                    __instance.fluidOutputInc += fluidInputIncAvg * batchResult.PassThroughCount;
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
                    fragmentRewardThisTick += BaseRecipe.RollBinomialApprox(ref __instance.seed,
                        batchResult.SuccessCount, 0.02f);
                }

                consumedInputThisTick += batchResult.ConsumedRegisterCount;
            }
        } else {
            __instance.fractionSuccess = false;
        }

        RecordFractionatorPerfStage(FractionatorPerfStageProcess, GetFractionatorPerfElapsed(perfStageStart));
        perfStageStart = GetFractionatorPerfTimestamp();
        perfDetailStart = GetFractionatorPerfTimestamp();
        FlushProcessingDeltas(recipe, buildingID, fluidId, consumedInputThisTick, successCountThisTick,
            fragmentRewardThisTick, productRegisterDeltas, productRegister, consumeRegister, ref growthContext,
            ref growthContextReady);
        RecordFractionatorPerfDetail(FractionatorPerfDetailFlushDeltas, GetFractionatorPerfElapsed(perfDetailStart));

        SetCurrentOutputFlags(factory,
            extraState,
            producedMainThisTick, producedSideThisTick, producedFluidThisTick);

        RecordFractionatorPerfStage(FractionatorPerfStageFlushDeltas, GetFractionatorPerfElapsed(perfStageStart));
        perfStageStart = GetFractionatorPerfTimestamp();
        // 零压循环 - 矿物复制塔在 Level >= 12 时，将产物和流动输出回流到输入
        if (isMineralReplicationTower
            && MineralReplicationTower.EnableZeroPressureCycle) {
            // 12 级仍然允许自循环，但内循环缓冲只按 8-stack 设计，避免完全替代外部物流与供料。
            int zeroPressureStack = Math.Min(MineralReplicationTower.MaxStack, ZeroPressureInternalStackCap);
            int fluidInputTarget = MaxBeltSpeed * zeroPressureStack;
            int fluidOutputTarget = 2 * zeroPressureStack;
            bool hasFluidOutputBelt = __instance.belt1 > 0 && __instance.isOutput1
                                      || __instance.belt2 > 0 && __instance.isOutput2;

            // 步骤1：无传送带时，把 fluidOutput 超过 fluidOutputTarget 的部分回填到 fluidInput
            if (!hasFluidOutputBelt) {
                int fluidMoveCount = Math.Max(0, __instance.fluidOutputCount - fluidOutputTarget);
                if (fluidMoveCount > 0) {
                    int fluidOutputIncAvg = __instance.fluidOutputCount > 0
                        ? __instance.fluidOutputInc / __instance.fluidOutputCount
                        : 0;
                    int moveInc = fluidOutputIncAvg * fluidMoveCount;
                    __instance.fluidInputCount += fluidMoveCount;
                    __instance.fluidInputCargoCount = Math.Min(fluidInputCargoMax,
                        __instance.fluidInputCargoCount + (float)fluidMoveCount / fluidInputCountPerCargo);
                    __instance.fluidInputInc += moveInc;
                    __instance.fluidOutputCount -= fluidMoveCount;
                    __instance.fluidOutputInc -= moveInc;
                }
            }

            // 步骤2 & 3：从产物补 fluidOutput 到 fluidOutputTarget，再补 fluidInput 到 fluidInputTarget
            if (recipe != null) {
                ProductOutputInfo mainProduct = FindProduct(products, fluidId, mainOnly: true);
                if (mainProduct != null && mainProduct.count > 0) {
                    int productIncPerItem = recipe.GetOutputInc(fluidId);

                    // 步骤2：补 fluidOutput 到 fluidOutputTarget（无论有无传送带，始终确保24）
                    int needForOutput = Math.Max(0, fluidOutputTarget - __instance.fluidOutputCount);
                    int moveToOutput = Math.Min(mainProduct.count, needForOutput);
                    if (moveToOutput > 0) {
                        __instance.fluidOutputCount += moveToOutput;
                        __instance.fluidOutputInc += productIncPerItem * moveToOutput;
                        mainProduct.count -= moveToOutput;
                        extraState.InvalidateFullProductCache();
                        needRecheckFullProduct = needRecheckFullProduct
                                                 || hasFullProduct && mainProduct.count < productOutputMax;
                        if (mainProduct.itemId == product0Id) {
                            __instance.productOutputCount = mainProduct.count;
                        }
                    }

                    // 步骤3：补 fluidInput 到 fluidInputTarget
                    if (mainProduct.count > 0) {
                        int needForInput = Math.Max(0, fluidInputTarget - __instance.fluidInputCount);
                        int moveToInput = Math.Min(mainProduct.count, needForInput);
                        if (moveToInput > 0) {
                            __instance.fluidInputCount += moveToInput;
                            __instance.fluidInputCargoCount = Math.Min(fluidInputCargoMax,
                                __instance.fluidInputCargoCount + (float)moveToInput / fluidInputCountPerCargo);
                            __instance.fluidInputInc += productIncPerItem * moveToInput;
                            mainProduct.count -= moveToInput;
                            extraState.InvalidateFullProductCache();
                            needRecheckFullProduct = needRecheckFullProduct
                                                     || hasFullProduct && mainProduct.count < productOutputMax;
                            if (mainProduct.itemId == product0Id) {
                                __instance.productOutputCount = mainProduct.count;
                            }
                        }
                    }
                }
            }
        }
        RecordFractionatorPerfStage(FractionatorPerfStageZeroPressure, GetFractionatorPerfElapsed(perfStageStart));
        perfStageStart = GetFractionatorPerfTimestamp();
        CargoTraffic cargoTraffic = factory.cargoTraffic;
        byte stack;
        byte inc;
        if (__instance.belt1 > 0) {
            if (__instance.isOutput1) {
                TryOutputFluidToBelt(ref __instance, buildingID, enableFracForever, maxStack, cargoTraffic,
                    __instance.belt1, fluidInputCountPerCargo);
            } else if (!__instance.isOutput1 && __instance.fluidInputCargoCount < fluidInputCargoMax) {
                if (fluidId > 0) {
                    for (int i = 0; i < MaxOutputTimes && __instance.fluidInputCargoCount < fluidInputCargoMax; i++) {
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
                             i < MaxOutputTimes && __instance.fluidInputCargoCount < fluidInputCargoMax;
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
                TryOutputFluidToBelt(ref __instance, buildingID, enableFracForever, maxStack, cargoTraffic,
                    __instance.belt2, fluidInputCountPerCargo);
            } else if (!__instance.isOutput2 && __instance.fluidInputCargoCount < fluidInputCargoMax) {
                if (fluidId > 0) {
                    for (int i = 0; i < MaxOutputTimes && __instance.fluidInputCargoCount < fluidInputCargoMax; i++) {
                        if (cargoTraffic.TryPickItemAtRear(__instance.belt2, fluidId, null, out stack, out inc) > 0) {
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
                             i < MaxOutputTimes && __instance.fluidInputCargoCount < fluidInputCargoMax;
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
        if (__instance.belt0 > 0) {
            if (__instance.isOutput0) {
                if (products.Count > 0) {
                    //获取分馏塔产物输出堆叠
                    int productStack = maxStack;
                    int lockedOutputId = isConversionTower && ConversionTower.EnableSingleLock
                        ? __instance.GetNormalizedLockedOutput(factory)
                        : 0;
                    ProductOutputInfo product = SelectProductForBeltOutput(products, productStack, lockedOutputId,
                        out bool flushNonLockedProduct);
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
                        } else if (product.count > 0 && (flushNonLockedProduct || __instance.fluidInputCount == 0)) {
                            // 单锁后非锁定产物要尽快清空；普通产物仍等输入停下后再吐出尾料。
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
            } else if (isInteractionTower
                       && __instance.belt1 <= 0
                       && __instance.belt2 <= 0
                       && AreAllProductsEmpty(products)) {
                //正面作为输入，数据传到数据中心。可接受未到最大价值，且GridIndex可见的物品。
                interactionMode = true;
                int interactionItemId =
                    cargoTraffic.TryPickItemAtRear(__instance.belt0, 0, ItemManager.needs, out stack, out inc);
                if (interactionItemId > 0) {
                    AddItemToModData(interactionItemId, stack, inc);
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
            __instance.isWorking = true;
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
                // C8: 单路锁定 - 缓存区清空后保留实体级锁定，允许空塔预设目标产物。
                if (isConversionTower && !ConversionTower.EnableSingleLock) {
                    __instance.SetLockedOutput(factory, 0);
                }
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

    private static void FlushProcessingDeltas(BaseRecipe recipe, int buildingID, int fluidId, int consumedInputCount,
        int successCount, int fragmentRewardCount, List<ProductOutputInfo> productRegisterDeltas,
        int[] productRegister, int[] consumeRegister, ref RecipeGrowthContext growthContext,
        ref bool growthContextReady) {
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
            BuildingManager.AddBuildingExp(buildingID, successCount);
        }
        if (successCount > 0) {
            if (recipe != null && RecipeGrowthQueries.CanApplyProcessingProgress(recipe)) {
                if (!growthContextReady) {
                    growthContext = RecipeGrowthManager.BuildContext();
                    growthContextReady = true;
                }
                RecipeGrowthExecutor.ApplyProcessingProgress(recipe, successCount, successCount, growthContext);
            }
        }
        if (fragmentRewardCount > 0) {
            AddItemToModData(IFE残片, fragmentRewardCount, 0, false);
        }
    }

    private static void RecordFractionSuccess(int count) {
        totalFractionSuccesses += count;
        long second = GameMain.gameTick >= 0 ? GameMain.gameTick / 60L : 0L;
        AdvanceFractionRateWindow(second);

        int bucketIndex = (int)(second % FractionRateWindowSeconds);
        fractionSuccessBuckets[bucketIndex] += count;
        currentFractionSuccessesPerMinute += count;
        if (currentFractionSuccessesPerMinute > peakFractionSuccessesPerMinute) {
            peakFractionSuccessesPerMinute = currentFractionSuccessesPerMinute;
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
        Array.Clear(fractionSuccessBuckets, 0, fractionSuccessBuckets.Length);
        currentFractionRateSecond = -1;
        currentFractionSuccessesPerMinute = 0;
    }

    #endregion

    #region 分馏塔耗电调整

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.GameTick))]
    [HarmonyPatch(typeof(GameLogic), nameof(GameLogic._fractionator_parallel))]
    public static IEnumerable<CodeInstruction> FactorySystem_SetPCState_Transpiler(
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
                        (byte)planetFactoryLocal.LocalIndex));// PlanetFactory (local var)
                    m.InsertAndAdvance(new CodeInstruction(OpCodes.Ldfld, entityPoolField));// EntityData[]
                }
                m.SetInstruction(new CodeInstruction(OpCodes.Call, replacementSetPCStateMethod));
            });

        return matcher.InstructionEnumeration();
    }

    public static void SetPCStateWithEntityPool(ref FractionatorComponent fractionator, PowerConsumerComponent[] pcPool,
        EntityData[] entityPool) {
        long perfStart = GetFractionatorPerfTimestamp();
        int buildingID = entityPool[fractionator.entityId].protoId;
        if (buildingID < IFE交互塔 || buildingID > IFE精馏塔) {
            try {
                fractionator.SetPCState(pcPool);// 原版分馏塔保持原逻辑
                return;
            } finally {
                RecordFractionatorPerf(FractionatorPerfSetPcVanilla, buildingID,
                    GetFractionatorPerfElapsed(perfStart));
            }
        }
        try {
            fractionator.SetPCState(pcPool, buildingID);
        } finally {
            RecordFractionatorPerf(FractionatorPerfSetPcFe, buildingID, GetFractionatorPerfElapsed(perfStart));
        }
    }

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
        double powerRatio = buildingID switch {
            IFE点数聚集塔 => 1.0,
            _ => Cargo.powerTableRatio[fractionator.incLevel] * GetFractionatorEnergyRatio(buildingID)
        };
        ref PowerConsumerComponent pc = ref pcPool[fractionator.pcId];
        pc.workEnergyPerTick = GetFractionatorWorkEnergyPerTick(buildingID);
        pc.idleEnergyPerTick = GetFractionatorIdleEnergyPerTick(buildingID);
        int permillage = (int)((num2 * 50.0 * 30.0 / MaxBeltSpeed + 1000.0) * powerRatio + 0.5);
        pc.SetRequiredEnergy(fractionator.isWorking, permillage);
    }

    private static long GetFractionatorWorkEnergyPerTick(int buildingID) {
        return buildingID switch {
            IFE交互塔 => InteractionTower.workEnergyPerTick,
            IFE矿物复制塔 => MineralReplicationTower.workEnergyPerTick,
            IFE点数聚集塔 => PointAggregateTower.workEnergyPerTick,
            IFE转化塔 => ConversionTower.workEnergyPerTick,
            IFE精馏塔 => RectificationTower.workEnergyPerTick,
            _ => 0
        };
    }

    private static long GetFractionatorIdleEnergyPerTick(int buildingID) {
        return buildingID switch {
            IFE交互塔 => InteractionTower.idleEnergyPerTick,
            IFE矿物复制塔 => MineralReplicationTower.idleEnergyPerTick,
            IFE点数聚集塔 => PointAggregateTower.idleEnergyPerTick,
            IFE转化塔 => ConversionTower.idleEnergyPerTick,
            IFE精馏塔 => RectificationTower.idleEnergyPerTick,
            _ => 0
        };
    }

    private static float GetFractionatorEnergyRatio(int buildingID) {
        return buildingID switch {
            IFE交互塔 => InteractionTower.EnergyRatio,
            IFE矿物复制塔 => MineralReplicationTower.EnergyRatio,
            IFE转化塔 => ConversionTower.EnergyRatio,
            IFE精馏塔 => RectificationTower.EnergyRatio,
            _ => 1.0f
        };
    }

    /// <summary>
    /// 交互塔特质
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameMain), nameof(GameMain.FixedUpdate))]
    public static void GameData_FixedUpdate_Postfix() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (GameMain.gameTick % 60 != 3) {
            return;
        }
        if (!InteractionTower.EnableSacrificeTrait) {
            ResetSacrificeBoostState();
            return;
        }
        int buffCount = 0;
        int[] takeCounts = new int[SacrificeTowerTypeCount];
        for (int i = 0; i < takeCounts.Length; i++) {
            takeCounts[i] = Take10PercentTower(IFE交互塔 + i);
            if (takeCounts[i] > 0) {
                buffCount++;
            }
        }
        if (InteractionTower.EnableDimensionalResonance) {
            for (int i = 0; i < takeCounts.Length; i++) {
                takeCounts[i] = (int)(takeCounts[i] * (1 + 0.1 * buffCount));
            }
        }
        UpdateSacrificeBoost(takeCounts);
    }

    private static void UpdateSacrificeBoost(int[] takeCounts) {
        float boostCap = InteractionTower.EnableDimensionalResonance
            ? SacrificeBoostCapTrait2
            : SacrificeBoostCapTrait1;
        for (int i = 0; i < SacrificeTowerTypeCount; i++) {
            float rawBoost = Mathf.Sqrt(takeCounts[i]) / 10.0f;
            float clampedBoost = Mathf.Min(rawBoost, boostCap);
            sacrificeStepIndex[i] = Math.Max(0, Mathf.FloorToInt(clampedBoost / SacrificeBoostStep));
        }

        InteractionTower.SuccessBoost = sacrificeStepIndex[0] * SacrificeBoostStep;
        MineralReplicationTower.SuccessBoost = sacrificeStepIndex[1] * SacrificeBoostStep;
        PointAggregateTower.SuccessBoost = sacrificeStepIndex[2] * SacrificeBoostStep;
        ConversionTower.SuccessBoost = sacrificeStepIndex[3] * SacrificeBoostStep;
        RectificationTower.SuccessBoost = sacrificeStepIndex[4] * SacrificeBoostStep;
        RefreshFractionatorRuntimeConfig();
    }

    private static void ResetSacrificeBoostState() {
        Array.Clear(sacrificeStepIndex, 0, sacrificeStepIndex.Length);
        InteractionTower.SuccessBoost = 0f;
        MineralReplicationTower.SuccessBoost = 0f;
        PointAggregateTower.SuccessBoost = 0f;
        ConversionTower.SuccessBoost = 0f;
        RectificationTower.SuccessBoost = 0f;
        RefreshFractionatorRuntimeConfig();
    }

    #endregion

    #region IModCanSave

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("TotalFractionSuccesses", bw => bw.Write(totalFractionSuccesses)),
            ("PeakFractionSuccessesPerMinute", bw => bw.Write(peakFractionSuccessesPerMinute))
        );
    }

    public static void Import(BinaryReader r) {
        ResetFractionRateWindow();
        r.ReadBlocks(
            ("TotalFractionSuccesses", br => totalFractionSuccesses = Math.Max(0, br.ReadInt64())),
            ("PeakFractionSuccessesPerMinute", br => peakFractionSuccessesPerMinute = Math.Max(0, br.ReadInt64()))
        );
    }

    public static void IntoOtherSave() {
        totalFractionSuccesses = 0;
        peakFractionSuccessesPerMinute = 0;
        ResetFractionRateWindow();
        ResetSacrificeBoostState();
    }

    #endregion
}
