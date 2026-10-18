// 基础分馏塔逐帧处理、共享堆叠配置与功耗适配。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using HarmonyLib;
using FE.Logic.DataCenter;
using FE.Logic.Fractionation.Recipes;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>
/// 连接 FE 与原版分馏运行入口，并维护基础塔处理和共享运行参数。
/// </summary>
public static partial class ProcessManager {

    #region 运行配置

    /// <summary>
    /// 基础输入准入按当前集装层数折算的堆数基准，速度在一半基准的真实货物数时达到满速。
    /// </summary>
    public const int BufferStackCount = 20;
    /// <summary>
    /// 逐帧处理使用的当前全局最大堆叠快照，随 <see cref="RefreshFractionatorRuntimeConfig"/> 更新。
    /// </summary>
    private static int globalMaxStack = 1;
    private static double[] incTableFixedRatio = [];
    /// <summary>基础塔输入准入阈值：20乘当前集装层数，单位为件。</summary>
    public static int BufferCapacity { get; private set; } = BufferStackCount;
    /// <summary>流动输出与每种产物的准入阈值基准；原版20件在4层集装下对应5堆。</summary>
    private const int OutputStackCount = 5;
    /// <summary>两类输出的件数阈值；批前低于此值即可完整处理，本批结果允许越界。</summary>
    public static int OutputCapacity => OutputStackCount * globalMaxStack;
    /// <summary>
    /// 定义热路径按传送带速度估算的最大输入速度。
    /// </summary>
    public static int MaxBeltSpeed = 30;

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
        // 增产剂表在游戏静态数据加载后才可靠，不能放到类型静态初始化阶段读取。
        incTableFixedRatio = new double[Cargo.incTableMilli.Length];
        //增产剂的增产效果修复，因为增产点数对于增产的加成不是线性的，但对于加速的加成是线性的
        for (int i = 1; i < Cargo.incTableMilli.Length; i++) {
            incTableFixedRatio[i] = Cargo.accTableMilli[i] / Cargo.incTableMilli[i];
        }
        RefreshFractionatorRuntimeConfig();
    }

    /// <summary>
    /// 刷新两座基础塔使用的运行参数，并按当前全局堆叠重算缓存上限。
    /// </summary>
    /// <remarks>输入为20乘当前层数件，两项输出为5乘当前层数件；完整货物和本帧批次可超过准入阈值。</remarks>
    public static void RefreshFractionatorRuntimeConfig() {
        globalMaxStack = Math.Max(1, BasicMineralTower.MaxStack);
        BufferCapacity = BufferStackCount * globalMaxStack;
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
        int buildingID = entityPool[fractionator.entityId].protoId;
        if (FractionatorTowerCatalog.IsActiveFractionator(buildingID)) {
            fractionator.SetPCState(pcPool, buildingID);
        } else {
            fractionator.SetPCState(pcPool);
        }
    }

    /// <summary>
    /// 按 FE 分馏塔能耗规则设置电力消费者状态。
    /// </summary>
    public static void SetPCState(this ref FractionatorComponent fractionator,
        PowerConsumerComponent[] pcPool, int buildingID) {
        double extraThroughput = Math.Max(0d, GetBasicProcessingRate(fractionator) - MaxBeltSpeed);
        double powerRatio = Cargo.powerTableRatio[fractionator.incLevel] * GetFractionatorEnergyRatio(buildingID);
        ref PowerConsumerComponent pc = ref pcPool[fractionator.pcId];
        pc.workEnergyPerTick = GetFractionatorWorkEnergyPerTick(buildingID);
        pc.idleEnergyPerTick = GetFractionatorIdleEnergyPerTick(buildingID);
        int permillage = (int)((extraThroughput * 50.0 * 30.0 / MaxBeltSpeed + 1000.0) * powerRatio + 0.5);
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

    #region 字段

    /// <summary>
    /// 定义单次更新最多尝试输出产物的次数。
    /// </summary>
    public static int MaxOutputTimes = 2;
    /// <summary>
    /// 表示当前缓存包含主产物输出。
    /// </summary>
    public const byte OutputFlagMain = 1 << 0;
    /// <summary>
    /// 表示当前缓存包含流动物品输出。
    /// </summary>
    public const byte OutputFlagFluid = 1 << 2;

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
        bool main, bool fluid) {

        if (factory == null) return;
        byte flags = 0;
        if (main) flags |= OutputFlagMain;
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

    /// <summary>计算基础塔满供电时每秒可执行的次数，供运行与两处速度显示共用。</summary>
    /// <param name="fractionator">当前输入数量与实际货物堆数。</param>
    /// <returns>每秒处理件数；空输入时为0。</returns>
    /// <remarks>半缓存达到带速上限，剩余容量吸收收发时序差；堆叠按真实平均件数缩放。</remarks>
    public static double GetBasicProcessingRate(in FractionatorComponent fractionator) {
        if (fractionator.fluidInputCount <= 0) { return 0d; }
        double cargos = fractionator.fluidInputCargoCount;
        double averageStack = cargos > 0.0001d ? fractionator.fluidInputCount / cargos : 4d;
        double fullness = Math.Min(1d, Math.Max(0d, cargos) / (BufferStackCount / 2d));
        return fullness * MaxBeltSpeed * averageStack;
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
    /// <para>FE 分馏塔使用扩展状态保存产物；原版字段只保留首个主产物兼容数据。</para>
    /// <para>基础塔结果由 <see cref="BaseRecipe.GetOutputsBatchFast"/> 结算，高级塔由独立运行器结算。</para>
    /// </remarks>
    public static uint InternalUpdateWithModDispatch(ref FractionatorComponent fractionator,
        PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister) {
        int buildingID = factory.entityPool[fractionator.entityId].protoId;
        if (FractionatorTowerCatalog.IsAdvancedTower(buildingID)) {
            return AdvancedFractionatorRuntime.InternalUpdate(ref fractionator, factory, signPool,
                productRegister, consumeRegister);
        }
        if (FractionatorTowerCatalog.IsBasicTower(buildingID)) {
            return InternalUpdate(ref fractionator, factory, power, signPool, productRegister, consumeRegister,
                FractionatorTowerCatalog.GetRecipeType(buildingID));
        }

        int fluidOutputStack = GetVanillaFluidOutputStack(in fractionator);
        uint state = fractionator.InternalUpdate(factory, power, signPool, productRegister, consumeRegister);
        // 原版在电力不足时不做传送带交互，其余情况按当前堆叠补放输出。
        return power < 0.1f ? state : TopUpVanillaOutputs(ref fractionator, factory.cargoTraffic, fluidOutputStack);
    }

    /// <summary>
    /// 两座基础塔的单输入批量处理、回流、端口收发与正面上传。
    /// </summary>
    /// <param name="fractionator">当前基础塔组件。</param>
    /// <param name="factory">所属工厂。</param>
    /// <param name="power">供电比例。</param>
    /// <param name="signPool">实体图标池。</param>
    /// <param name="productRegister">原版产出统计。</param>
    /// <param name="consumeRegister">原版消耗统计。</param>
    /// <param name="recipeType">该塔的基础配方分支。</param>
    /// <returns>工作时为 1，否则为 0。</returns>
    private static uint InternalUpdate(ref FractionatorComponent fractionator,
        PlanetFactory factory, float power, SignData[] signPool, int[] productRegister, int[] consumeRegister,
        ERecipe recipeType) {
        int entityId = fractionator.entityId;
        //所有产物输出
        FractionatorOutputState.FractionatorExtraState extraState = fractionator.GetExtraState(factory);
        List<ProductOutputInfo> products = extraState.Products;
        ProductOutputBuffer outputBuffer = extraState.ScratchOutputs;
        int fluidId = fractionator.fluidId;
        BaseRecipe recipe = extraState.GetRecipe(recipeType, fluidId);
        //检测products和recipe的输出是否一致
        ProductOutputInfo product0 = null;
        if (recipe == null) {
            int firstProductId = products.Count > 0 ? products[0].itemId : fluidId;
            bool needResetProducts = !extraState.TryGetRuntimeSchema(recipeType, fluidId, null, firstProductId,
                                         out product0)
                                     || fractionator.productId != firstProductId;
            if (needResetProducts) {
                ResetProductsForRecipe(ref fractionator, extraState, null, signPool);
                product0 = products.Count > 0 ? products[0] : null;
                extraState.MarkRuntimeSchema(recipeType, fluidId, null, fractionator.productId, product0);
            }
        } else if (!extraState.TryGetRuntimeSchema(recipeType, fluidId, recipe, fractionator.productId,
                       out product0)) {
            int firstProductId = recipe.OutputMain.Count > 0 ? recipe.OutputMain[0].OutputID : recipe.InputID;
            bool needResetProducts = fractionator.productId != firstProductId
                                     || !MatchesRecipeOutputs(products, recipe);
            if (needResetProducts) {
                ResetProductsForRecipe(ref fractionator, extraState, recipe, signPool);
            }
            int productId = fractionator.productId;
            product0 = products.Count > 0 && products[0].itemId == productId
                ? products[0]
                : FindProduct(products, productId);
            extraState.MarkRuntimeSchema(recipeType, fluidId, recipe, productId, product0);
        }
        // 原版首产物镜像可对应当前目录或尚未排出的既有实体。
        int product0Id = fractionator.productId;
        //如果通过面板取了物品，需要同步数目到products
        if (product0 != null && product0.count != fractionator.productOutputCount) {
            product0.count = fractionator.productOutputCount;
            extraState.InvalidateFullProductCache();
        }
        if (power < 0.1) {
            return 0;
        }
        float fluidInputCountPerCargo = 1.0f;
        if (fractionator.fluidInputCount == 0)
            fractionator.fluidInputCargoCount = 0f;
        else
            fluidInputCountPerCargo = fractionator.fluidInputCargoCount > 0.0001
                ? fractionator.fluidInputCount / fractionator.fluidInputCargoCount
                : 4f;
        int maxStack = globalMaxStack;
        int fluidInputCargoMax = BufferCapacity;
        int productOutputMax = OutputCapacity;
        int fluidOutputMax = OutputCapacity;
        bool canProcessRecipe = recipe != null && RecipeAvailabilityStore.IsAvailable(recipe);
        bool moveDirectly = !canProcessRecipe;
        bool producedMainThisTick = false;
        bool producedFluidThisTick = false;
        bool hasFullProduct = extraState.HasFullProduct(productOutputMax);
        bool needRecheckFullProduct = false;
        int consumedInputThisTick = 0;
        List<ProductOutputInfo> productRegisterDeltas = null;
        if (fractionator.fluidInputCount > 0
            && fractionator.fluidOutputCount < fluidOutputMax) {
            //分馏塔正常运转时，计算进度，10000点进度可以处理一次
            fractionator.progress += (int)(power * (10000d / 60d) * GetBasicProcessingRate(fractionator) + 0.75);
            if (fractionator.progress > 300000) {
                fractionator.progress = 300000;
            }
            int batchCount = Math.Min(fractionator.progress / 10000, fractionator.fluidInputCount);
            if (batchCount > 0) {
                fractionator.progress -= batchCount * 10000;
                int fluidInputIncAvg = fractionator.fluidInputInc <= 0 || fractionator.fluidInputCount <= 0
                    ? 0
                    : fractionator.fluidInputInc / fractionator.fluidInputCount;
                if (!fractionator.incUsed)
                    fractionator.incUsed = fluidInputIncAvg > 0;

                // 产物达到阈值后停止反应，完整本批输入保留增产点侧流。
                bool isForcedPassthrough = moveDirectly || hasFullProduct;
                FractionationBatchResult batchResult;
                if (isForcedPassthrough) {
                    outputBuffer.Clear();
                    batchResult = new FractionationBatchResult {
                        InputRemoveCount = batchCount,
                        ConsumedRegisterCount = 0,
                        SuccessCount = 0,
                        PassThroughCount = batchCount,
                        PassThroughInc = fluidInputIncAvg * batchCount,
                    };
                    fractionator.fluidInputInc -= fluidInputIncAvg * batchCount;
                    if (fractionator.fluidInputInc < 0) fractionator.fluidInputInc = 0;
                } else {
                    float pointsBonus = (float)MaxTableMilli(fluidInputIncAvg);
                    // 两座基础塔只使用配方自身概率与增产剂加成，不再叠加额外成功率。
                    float successBoost = 0f;
                    batchResult = recipe.GetOutputsBatchFast(ref fractionator.seed, pointsBonus, successBoost,
                        batchCount, fluidInputIncAvg, ref fractionator.fluidInputInc, outputBuffer);
                }

                fractionator.fractionSuccess = batchResult.HasOutput;

                if (batchResult.InputRemoveCount > 0) {
                    fractionator.fluidInputCount -= batchResult.InputRemoveCount;
                    if (fractionator.fluidInputCount < 0) fractionator.fluidInputCount = 0;
                    if (fractionator.fluidInputCount == 0) fractionator.fluidInputInc = 0;
                    fractionator.fluidInputCargoCount -= batchResult.InputRemoveCount / fluidInputCountPerCargo;
                    if (fractionator.fluidInputCargoCount < 0f) fractionator.fluidInputCargoCount = 0f;
                }

                if (batchResult.PassThroughCount > 0) {
                    fractionator.fluidOutputCount += batchResult.PassThroughCount;
                    fractionator.fluidOutputTotal += batchResult.PassThroughCount;
                    fractionator.fluidOutputInc += batchResult.PassThroughInc;
                    producedFluidThisTick = true;
                }

                if (batchResult.SuccessCount > 0) {
                    // 虚拟成果点亮正面方向，原样本返还不计新制造，实体仍正常进入缓存。
                    producedMainThisTick = true;
                    fractionator.productOutputTotal += batchResult.SuccessCount;
                    for (int i = 0; i < outputBuffer.Count; i++) {
                        ProductOutputInfo p = outputBuffer[i];
                        int itemID = p.itemId;
                        int itemCount = p.count;
                        int manufacturedCount = itemCount - (itemID == fluidId ? batchResult.ReturnedProductCount : 0);
                        AddProductRegisterDelta(ref productRegisterDeltas, itemID, manufacturedCount);
                        if (itemID == product0Id) {
                            product0.count += itemCount;
                            fractionator.productOutputCount = product0.count;
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
                }

                consumedInputThisTick += batchResult.ConsumedRegisterCount;
            }
        } else {
            fractionator.fractionSuccess = false;
        }

        FlushProcessingDeltas(fluidId, consumedInputThisTick, productRegisterDeltas, productRegister, consumeRegister);

        SetCurrentOutputFlags(factory,
            extraState,
            producedMainThisTick, producedFluidThisTick);

        CargoTraffic cargoTraffic = factory.cargoTraffic;
        HandleBasicSidePort(ref fractionator, fractionator.belt1, fractionator.isOutput1);
        HandleBasicSidePort(ref fractionator, fractionator.belt2, fractionator.isOutput2);
        bool interactionMode = false;
        bool interactionTransferred = false;
        if (fractionator.belt0 > 0) {
            if (fractionator.isOutput0) {
                if (products.Count > 0) {
                    // 获取分馏塔产物输出堆叠。
                    int productStack = maxStack;
                    ProductOutputInfo product = SelectProductForBeltOutput(products);
                    //输出产物
                    if (product != null && product.count > 0) {
                        if (product.count >= productStack) {
                            //产物达到最大堆叠数目，直接尝试输出
                            if (cargoTraffic.TryInsertItemAtHead(fractionator.belt0, product.itemId, (byte)productStack,
                                    0)) {
                                product.count -= productStack;
                                extraState.InvalidateFullProductCache();
                                needRecheckFullProduct = needRecheckFullProduct
                                                         || hasFullProduct && product.count < productOutputMax;
                                if (ReferenceEquals(product, product0)) {
                                    fractionator.productOutputCount = product.count;
                                }
                            }
                        } else if (product.count > 0 && fractionator.fluidInputCount == 0) {
                            // 输入排空后释放不足整堆的尾料。
                            if (cargoTraffic.TryInsertItemAtHead(fractionator.belt0, product.itemId, (byte)product.count,
                                    0)) {
                                product.count = 0;
                                extraState.InvalidateFullProductCache();
                                needRecheckFullProduct = needRecheckFullProduct || hasFullProduct;
                                if (ReferenceEquals(product, product0)) {
                                    fractionator.productOutputCount = product.count;
                                }
                            }
                        }
                    }
                }
            } else if (fractionator.belt1 <= 0
                       && fractionator.belt2 <= 0
                       && AreAllProductsEmpty(products)) {
                // 基础塔正面反向输入且两侧未连接时整堆上传；超额物料由亚空间按价值折算扩容。
                interactionMode = true;
                int interactionItemId = DataCenterUploadRouter.UploadRearStack(cargoTraffic, fractionator.belt0);
                interactionTransferred = interactionItemId > 0;
                if (interactionTransferred) {
                    fractionator.fluidId = interactionItemId;
                    fractionator.productId = interactionItemId;
                    fractionator.produceProb = 0.01f;
                    signPool[entityId].iconId0 = (uint)fractionator.fluidId;
                    signPool[entityId].iconType = 1U;
                }
            }
        }

        if (interactionMode) {
            fractionator.isWorking = interactionTransferred;
        } else {
            // 如果缓存区全部清空，重置全部
            if (fractionator.fluidInputCount == 0
                && fractionator.fluidOutputCount == 0
                && AreAllProductsEmpty(products)) {
                fractionator.fluidId = 0;
                fractionator.productId = 0;
                products.Clear();
                hasFullProduct = false;
                extraState.InvalidateFullProductCache();
                signPool[entityId].iconId0 = 0;
                signPool[entityId].iconType = 0U;
            }
            if (needRecheckFullProduct) {
                hasFullProduct = extraState.HasFullProduct(productOutputMax, forceRefresh: true);
            }
            fractionator.isWorking = fractionator.fluidInputCount > 0
                                   && !hasFullProduct
                                   && fractionator.fluidOutputCount < fluidOutputMax
                                   && !moveDirectly;
        }

        return fractionator.isWorking ? 1U : 0U;

        /// <summary>按当前帧上下文处理一个侧面端口，首次取料时建立配方产物架构。</summary>
        /// <param name="tower">当前塔组件，通过参数引用避免捕获外层 ref 参数。</param>
        /// <param name="beltId">侧面传送带 ID。</param>
        /// <param name="isOutput">该端口是否向传送带输出。</param>
        void HandleBasicSidePort(ref FractionatorComponent tower, int beltId, bool isOutput) {
            if (beltId <= 0) { return; }
            if (isOutput) {
                TryOutputFluidToBelt(ref tower, !moveDirectly, maxStack, cargoTraffic, beltId,
                    fluidInputCountPerCargo, forceSingleStack: moveDirectly);
                return;
            }
            for (int i = 0; i < MaxOutputTimes
                 && CanAcceptBasicInputStack(cargoTraffic, beltId, fluidInputCargoMax, tower.fluidInputCount); i++) {
                int itemId = cargoTraffic.TryPickItemAtRear(beltId, tower.fluidId, null, out byte stack, out byte inc);
                if (itemId <= 0) { break; }
                tower.fluidInputCount += stack;
                tower.fluidInputInc += inc;
                tower.fluidInputCargoCount++;
                if (tower.fluidId != 0) { continue; }
                tower.fluidId = itemId;
                ResetProductsForRecipe(ref tower, extraState, extraState.GetRecipe(recipeType, itemId), signPool);
            }
        }
    }

    private static bool CanAcceptBasicInputStack(CargoTraffic cargoTraffic, int beltId,
        int capacity, int currentCount) {
        if (cargoTraffic == null || beltId <= 0 || currentCount >= capacity) {
            return false;
        }
        int stack = GetRearCargoStack(cargoTraffic, beltId);
        return stack > 0;
    }

    internal static int GetRearCargoStack(CargoTraffic cargoTraffic, int beltId) {
        if (cargoTraffic == null || beltId <= 0) {
            return 0;
        }
        CargoPath path = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[beltId].segPathId);
        int cargoId = path?.GetCargoIdAtRear() ?? -1;
        if (cargoId < 0 || cargoId >= cargoTraffic.container.cargoPool.Length) {
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
        List<ProductOutputInfo> productRegisterDeltas,
        int[] productRegister, int[] consumeRegister) {
        if (consumedInputCount > 0) {
            lock (consumeRegister) { consumeRegister[fluidId] += consumedInputCount; }
        }
        if (productRegisterDeltas != null) {
            lock (productRegister) {
                foreach (ProductOutputInfo delta in productRegisterDeltas) {
                    productRegister[delta.itemId] += delta.count;
                }
            }
        }
    }

    private static ProductOutputInfo FindProduct(List<ProductOutputInfo> products, int itemId) {
        foreach (ProductOutputInfo product in products) {
            if (product.itemId == itemId) {
                return product;
            }
        }
        return null;
    }

    /// <summary>选择积压件数最多的基础塔产物，数量相同时沿用配方顺序。</summary>
    /// <param name="products">当前配方产物缓存。</param>
    /// <returns>有库存的产物；缓存全空时为 null。</returns>
    private static ProductOutputInfo SelectProductForBeltOutput(List<ProductOutputInfo> products) {
        ProductOutputInfo best = null;
        foreach (ProductOutputInfo product in products) {
            if (product.count > 0 && (best == null || product.count > best.count)) { best = product; }
        }
        return best;
    }

    /// <summary>重建基础塔产物目录与镜像，保留读档或目录变化前已经存在的实体。</summary>
    /// <param name="fractionator">需要重建显示与缓存的基础塔。</param>
    /// <param name="state">该塔扩展缓存。</param>
    /// <param name="recipe">当前识别的配方；没有配方时仍保留已有实体直到排空。</param>
    /// <param name="signPool">实体图标池。</param>
    private static void ResetProductsForRecipe(ref FractionatorComponent fractionator,
        FractionatorOutputState.FractionatorExtraState state, BaseRecipe recipe, SignData[] signPool) {
        ProductOutputInfo[] retained = state.Products.Where(product => product.count > 0).ToArray();
        state.Products.Clear();
        if (recipe != null) {
            foreach (OutputInfo output in recipe.OutputMain) {
                int count = retained.Where(product => product.itemId == output.OutputID).Sum(product => product.count);
                state.Products.Add(new(true, output.OutputID, count));
            }
        }
        foreach (ProductOutputInfo product in retained) {
            if (FindProduct(state.Products, product.itemId) == null) { state.Products.Add(product); }
        }
        state.InvalidateFullProductCache();
        state.ClearRuntimeSchema();
        fractionator.productId = state.Products.Count > 0 ? state.Products[0].itemId : fractionator.fluidId;
        fractionator.productOutputCount = state.Products.Count > 0 ? state.Products[0].count : 0;
        fractionator.produceProb = 0.01f;
        signPool[fractionator.entityId].iconId0 = recipe == null ? 0U : (uint)fractionator.fluidId;
        signPool[fractionator.entityId].iconType = recipe == null ? 0U : 1U;
    }

    /// <summary>检查固定配方产物前缀，允许后面保留目录变化前尚未排出的实体缓存。</summary>
    /// <param name="products">当前扩展产物列表。</param>
    /// <param name="recipe">当前识别的配方。</param>
    /// <returns>当前配方产物前缀一致时为true。</returns>
    private static bool MatchesRecipeOutputs(List<ProductOutputInfo> products, BaseRecipe recipe) {
        if (products.Count < recipe.OutputMain.Count) {
            return false;
        }
        for (int i = 0; i < recipe.OutputMain.Count; i++) {
            if (!products[i].isMainOutput || products[i].itemId != recipe.OutputMain[i].OutputID) {
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

    #endregion
}
