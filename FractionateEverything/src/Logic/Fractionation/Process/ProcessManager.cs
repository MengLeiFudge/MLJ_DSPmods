using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Fractionation.FracRecipes;
using FE.Logic.Fractionation.FracRecipes.Runtime;
using FE.Logic.DataCenter;
using FE.Logic.Items;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Process;

/// <summary>
/// 修改所有分馏塔的处理逻辑，以及对应的显示。
/// </summary>
public static partial class ProcessManager {
    private delegate void FractionatorUpdateHandler(ref FractionatorComponent fractionator,
        PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister,
        ref uint result);

    private static readonly FractionatorUpdateHandler[] updateHandlersByBuildingOffset = [
        UpdateInteractionTower,
        UpdateMineralReplicationTower,
        UpdateConversionTower,
        UpdateRectificationTower,
    ];

    /// <summary>
    /// 注册该分馏域对象需要的本地化文本。
    /// </summary>
    public static void AddTranslations() {
        Register("交互模式", "Interaction mode");
        Register("原料堆积", "Fluid overflow");
        Register("搬运模式", "Transport mode");
        Register("缺少残片", "Lack of fragments");
        Register("分馏永动", "Frac forever");
        Register("无配方", "No recipe");
        Register("主产物", "Main product");
        Register("副产物", "Append product");
        Register("流动", "Flow");
        Register("损毁", "Destroy");
        Register("流动输入", "Flow input");
        Register("流动输出", "Flow output");
        Register("配方不存在", "Recipe does not exist");
        Register("配方强化", "Recipe enhancement");
        Register("单锁", "Main Route Lock", "主路锁定");
        Register("未锁定", "Not locked");
        Register("主路目标", "Main route target");
        Register("右键设为单锁", "Right-click to set main route target", "右键设为主路目标");
        Register("右键清除单锁", "Right-click to clear main route target", "右键清除主路目标");
        Register("已锁定单路产物：{0}", "Main route target: {0}", "已设定主路目标：{0}");
        Register("已清除单路锁定", "Main route target cleared", "已清除主路目标");
        Register("锁定产物无效，已清除", "Main route target invalid, cleared", "主路目标无效，已清除");
        Register("谱系方向", "Lineage target");
        Register("右键设为谱系方向", "Right-click to set lineage target");
        Register("右键清除谱系方向", "Right-click to clear lineage target");
        Register("已设定谱系方向：{0}", "Lineage target: {0}");
        Register("已清除谱系方向", "Lineage target cleared");
        Register("弃置", "Discard");
        Register("保留", "Keep");
        Register("右键切换副产物弃置", "Right-click a byproduct to toggle discard");
        Register("已启用副产物弃置", "Byproduct discard enabled");
        Register("已关闭副产物弃置", "Byproduct discard disabled");
    }

    #region Field

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
    /// <para>除此之外，分馏判定结果由<see cref="FE.Logic.Fractionation.FracRecipes.BaseRecipe.GetOutputs"/>得到。</para>
    /// </remarks>
    /// <summary>
    /// 在原版分馏器更新前分发到 FE 分馏塔热路径。
    /// </summary>
    public static uint InternalUpdateWithModDispatch(ref FractionatorComponent fractionator,
        PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister) {
        long perfStart = GetFractionatorPerfTimestamp();
        int buildingID = factory.entityPool[fractionator.entityId].protoId;
        int handlerIndex = FractionatorTowerCatalog.GetActiveFractionatorIndex(buildingID);
        if (handlerIndex >= 0 && handlerIndex < updateHandlersByBuildingOffset.Length) {
            try {
                uint result = 0;
                updateHandlersByBuildingOffset[handlerIndex](ref fractionator, factory, power, signPool,
                    productRegister, consumeRegister, ref result);
                return result;
            }
            finally {
                RecordFractionatorPerf(FractionatorPerfUpdateFe, buildingID, GetFractionatorPerfElapsed(perfStart));
            }
        }

        //原版分馏塔不做处理
        try {
            return fractionator.InternalUpdate(factory, power, signPool, productRegister, consumeRegister);
        }
        finally {
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
        bool isConversionTower = buildingID == IFE转化塔;
        bool isRectificationTower = buildingID == IFE解析塔;
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
            if (isInteractionTower || isConversionTower) {
                __instance.SetLockedOutput(factory,
                    __instance.NormalizeLockedOutput(factory, __instance.GetLockedOutput(factory)));
            }
            if (isRectificationTower) {
                __instance.SetLineageTarget(factory,
                    __instance.NormalizeLineageTarget(factory, __instance.GetLineageTarget(factory)));
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
                if (isInteractionTower || isConversionTower) {
                    __instance.SetLockedOutput(factory,
                        __instance.NormalizeLockedOutput(factory, __instance.GetLockedOutput(factory)));
                }
                if (isRectificationTower) {
                    __instance.SetLineageTarget(factory,
                        __instance.NormalizeLineageTarget(factory, __instance.GetLineageTarget(factory)));
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
        bool enableFluidOutputStacking = runtimeConfig.EnableFluidOutputStacking;
        bool enableProductOutputStacking = runtimeConfig.EnableProductOutputStacking;
        bool enableFracForever = runtimeConfig.EnableFractionationForever;
        bool discardByproducts = runtimeConfig.EnableByproductDiscard
                                 && recipe != null
                                 && recipe.IsByproductDiscardCalibrated
                                 && __instance.GetNormalizedByproductDiscard(factory);
        if (discardByproducts) {
            foreach (ProductOutputInfo product in products) {
                if (!product.isMainOutput && product.count > 0) {
                    product.count = 0;
                    extraState.InvalidateFullProductCache();
                }
            }
        }
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
                        PassThroughInc = fluidInputIncAvg * batchCount,
                    };
                    __instance.fluidInputInc -= fluidInputIncAvg * batchCount;
                    if (__instance.fluidInputInc < 0) __instance.fluidInputInc = 0;
                } else {
                    float pointsBonus = (float)MaxTableMilli(fluidInputIncAvg) * plrRatio;
                    float successBoost = RecipeModifierCache.GetSuccessRateBonus(recipe);
                    // C8: 单路锁定 - 在调用 GetOutputs 前设置当前锁定产物ID
                    if (isInteractionTower) {
                        BaseRecipe.CurrentMainOutputTargetId = __instance.GetNormalizedLockedOutput(factory);
                    }
                    if (isConversionTower) {
                        ConversionRecipe.CurrentLockedOutputId = __instance.GetNormalizedLockedOutput(factory);
                    }
                    if (isRectificationTower) {
                        RectificationRecipe.CurrentLineageTargetId = __instance.GetNormalizedLineageTarget(factory);
                    }
                    perfDetailStart = GetFractionatorPerfTimestamp();
                    try {
                        batchResult = recipe.GetOutputsBatchFast(ref __instance.seed, pointsBonus, successBoost,
                            batchCount, fluidInputIncAvg, ref __instance.fluidInputInc, outputBuffer);
                    }
                    finally {
                        if (isInteractionTower) {
                            BaseRecipe.CurrentMainOutputTargetId = 0;
                        }
                        if (isConversionTower) {
                            ConversionRecipe.CurrentLockedOutputId = 0;
                        }
                        if (isRectificationTower) {
                            RectificationRecipe.CurrentLineageTargetId = 0;
                        }
                    }
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
                        if (!p.isMainOutput && discardByproducts) {
                            continue;
                        }
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
        byte stack;
        byte inc;
        if (__instance.belt1 > 0) {
            if (__instance.isOutput1) {
                TryOutputFluidToBelt(ref __instance, enableFluidOutputStacking && !moveDirectly, maxStack,
                    cargoTraffic, __instance.belt1, fluidInputCountPerCargo, forceSingleStack: moveDirectly);
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
                TryOutputFluidToBelt(ref __instance, enableFluidOutputStacking && !moveDirectly, maxStack,
                    cargoTraffic, __instance.belt2, fluidInputCountPerCargo, forceSingleStack: moveDirectly);
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
                    int productStack = enableProductOutputStacking ? maxStack : 1;
                    int lockedOutputId = (isInteractionTower || isConversionTower)
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
                    DataCenterUploadRouter.Upload(interactionItemId, stack, inc);
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
                if ((isInteractionTower || isConversionTower)
                    && !TowerRuntimeModifierCache.IsMainOutputLockEnabled(recipeType)) {
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

    #region IModCanSave

    /// <summary>
    /// 将该分馏域状态写入存档。
    /// </summary>
    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("TotalFractionSuccesses", bw => bw.Write(totalFractionSuccesses)),
            ("PeakFractionSuccessesPerMinute", bw => bw.Write(peakFractionSuccessesPerMinute))
        );
    }

    /// <summary>
    /// 从存档读取该分馏域状态。
    /// </summary>
    public static void Import(BinaryReader r) {
        ResetFractionRateWindow();
        r.ReadBlocks(
            ("TotalFractionSuccesses", br => totalFractionSuccesses = Math.Max(0, br.ReadInt64())),
            ("PeakFractionSuccessesPerMinute", br => peakFractionSuccessesPerMinute = Math.Max(0, br.ReadInt64()))
        );
    }

    /// <summary>
    /// 切换或进入其他存档时重置该分馏域状态。
    /// </summary>
    public static void IntoOtherSave() {
        totalFractionSuccesses = 0;
        peakFractionSuccessesPerMinute = 0;
        ResetFractionRateWindow();
    }

    #endregion
}
