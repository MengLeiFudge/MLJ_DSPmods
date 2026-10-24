using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FE.Compatibility.Nebula;
using FE.Logic.Civilization;
using FE.Logic.Enhancements.Stacking;
using FE.Logic.Fractionation.Recipes;
using UnityEngine;
using static FE.Logic.Fractionation.Fractionators.ProcessManager;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>高级塔运行状态；显示文本和配色由界面映射，不参与业务判断。</summary>
public enum AdvancedTowerStatus {
    /// <summary>实例尚未建立。</summary>
    AwaitingNourishment,
    /// <summary>食物能量耗尽，需要矩阵供养。</summary>
    MissingMatrix,
    /// <summary>尚未选择配方。</summary>
    AwaitingRecipe,
    /// <summary>配方原料不足。</summary>
    MissingInput,
    /// <summary>流动输出缓存已满。</summary>
    FlowOutputBlocked,
    /// <summary>产物输出缓存已满。</summary>
    ProductOutputBlocked,
    /// <summary>正在运行，产物等待凑足整堆。</summary>
    Stacking,
    /// <summary>正常运行。</summary>
    Running,
}

/// <summary>
/// 一座高级分馏塔的多输入、多输出、食物能量和配方状态。
/// </summary>
public sealed class AdvancedFractionatorState {
    /// <summary>选定配方的稳定整数 ID；0 表示未选择。</summary>
    public int SelectedRecipeId;

    /// <summary>反应输入数量。</summary>
    public readonly Dictionary<int, int> Inputs = [];

    /// <summary>每种反应输入的等效货物堆数；部分消耗按件数比例扣减，用于保留真实平均堆叠。</summary>
    public readonly Dictionary<int, double> InputCargo = [];

    /// <summary>实例锁内复用的待排空物品键；跨输入与旧食物缓存复用，不写入存档或联机状态。</summary>
    internal readonly List<int> UnprocessedInputIds = [];

    /// <summary>最近半秒实际配对轮数/秒，供产物与营养估算使用；不包含未配对回流。</summary>
    public double ProcessingRate;

    /// <summary>各主料已推进但尚未结算的件数；实物仍在Inputs中，进度不得超过库存。</summary>
    public readonly Dictionary<int, double> InputProgress = [];

    /// <summary>最近半秒各主料实际经过件数/秒，包括配对反应和未配对回流。</summary>
    public readonly Dictionary<int, double> InputRates = [];

    /// <summary>当前半秒窗口的经过件数与配对轮数；仅主机累计，不持久化。</summary>
    internal readonly Dictionary<int, int> InputFlowCounts = [];
    internal int FlowSampleTicks;
    internal int FlowSampleRounds;

    /// <summary>反应输入增产点。</summary>
    public readonly Dictionary<int, int> InputInc = [];

    /// <summary>尚未转化为食物能量的矩阵食物数量。</summary>
    public readonly Dictionary<int, int> Foods = [];

    /// <summary>备用矩阵增产点，点燃时增加该件能量，取出或拆除时原样返还。</summary>
    public readonly Dictionary<int, int> FoodInc = [];

    /// <summary>配方产物数量。</summary>
    public readonly Dictionary<int, int> Products = [];

    /// <summary>非目标物品和失败轮流动输出数量。</summary>
    public readonly Dictionary<int, int> FlowOutputs = [];

    /// <summary>流动输出增产点。</summary>
    public readonly Dictionary<int, int> FlowOutputInc = [];

    /// <summary>正在消化的一件矩阵；0表示消化槽为空。</summary>
    public int DigestingItemId;

    /// <summary>当前件剩余的能量，按实际主料消费或待机时间扣减。</summary>
    public double DigestingEnergy;

    /// <summary>当前件点燃时的增产点，只增加可用能量。</summary>
    public int DigestingInc;

    /// <summary>当前消化物的本地代次，用于取消已经换件的长按操作；不持久化。</summary>
    internal long DigestionVersion;

    /// <summary>按比例轮询产物的游标。</summary>
    public int OutputCursor;

    /// <summary>按配方输入顺序轮询流动输出的游标。</summary>
    public int FlowOutputCursor;

    /// <summary>实例有待广播的食物能量、配方或缓存变化；在实例锁内写入，切档不持久化。</summary>
    internal bool SnapshotDirty = true;
}

/// <summary>
/// 三类高级塔的多输入反应运行时。
/// </summary>
public static partial class AdvancedFractionatorRuntime {
    /// <summary>原版进度镜像的满轮刻度；高级塔实际按各主料的件数进度结算。</summary>
    private const int ProgressPerRound = 10000;
    /// <summary>每种主料的件数准入阈值为40乘当前集装层数；货物数只参与速度计算。</summary>
    public static int InputCapacity => FractionatorManager.VanillaFluidInputCargoMax * StackingManager.CurrentMaxStack;
    /// <summary>持续缺少食物能量时，扣完一个最大耐久所需的秒结算次数。</summary>
    private const int StarvationSecondsToDestroy = 360;
    /// <summary>读档安全边界覆盖最后一批越阈值的缓存，包含最多两批主料；不按尚未同步的当前集装裁剪。</summary>
    private const int MaximumBufferCapacity = (FractionatorManager.VanillaFluidInputCargoMax * 2 + ProcessManager.BufferStackCount + 1) * StackingManager.AbsoluteMaxStack;
    private const int MaximumStateCount = 1000000;
    private const int MaximumDictionaryEntryCount = 12000;
    private static readonly ConcurrentDictionary<(int planetId, int fractionatorId), AdvancedFractionatorState>
        States = [];

    /// <summary>供窗口与汇总页共用的工作状态，按食物能量、配方、原料和输出空间区分原因。</summary>
    /// <param name="state">高级塔状态。</param>
    /// <returns>与显示语言无关的运行状态。</returns>
    public static AdvancedTowerStatus GetOperatingStatus(AdvancedFractionatorState state) {
        if (state == null) { return AdvancedTowerStatus.AwaitingNourishment; }
        lock (state) {
            if (state.DigestingEnergy <= 0d) { return AdvancedTowerStatus.MissingMatrix; }
            return GetProcessingStatus(state);
        }
    }

    /// <summary>独立读取配方处理状态；食物供养由窗口另一行显示。</summary>
    public static AdvancedTowerStatus GetProcessingStatus(AdvancedFractionatorState state) {
        if (state == null) { return AdvancedTowerStatus.AwaitingRecipe; }
        lock (state) {
            WorldviewRecipe recipe = RecipeManager.GetRecipe(state.SelectedRecipeId);
            if (recipe == null) { return AdvancedTowerStatus.AwaitingRecipe; }
            if (HasBlockedProduct(state, recipe)) { return AdvancedTowerStatus.ProductOutputBlocked; }
            if (!CanProcessOutputs(state, recipe)) { return AdvancedTowerStatus.FlowOutputBlocked; }
            int rounds = GetAvailableRounds(state, recipe);
            if (rounds <= 0) { return AdvancedTowerStatus.MissingInput; }
            int stack = StackingManager.CurrentMaxStack;
            if (stack > 1 && state.Products.Values.Any(count => count > 0 && count < stack)) {
                return AdvancedTowerStatus.Stacking;
            }
            return AdvancedTowerStatus.Running;
        }
    }

    /// <summary>可见页面按低频请求统计现有运行状态，不扫描银河全部建筑。</summary>
    /// <returns>按塔体类型分组的数量与异常计数。</returns>
    public static Dictionary<(int PlanetId, int ItemId), (int Total, int Hungry, int Missing, int Blocked)> GetTowerSummary() {
        var result = new Dictionary<(int PlanetId, int ItemId), (int Total, int Hungry, int Missing, int Blocked)>();
        foreach (var pair in States) {
            PlanetFactory factory = GameMain.galaxy?.PlanetById(pair.Key.planetId)?.factory;
            var system = factory?.factorySystem;
            int id = pair.Key.fractionatorId;
            if (system == null || id <= 0 || id >= system.fractionatorCursor) { continue; }
            int entity = system.fractionatorPool[id].entityId;
            if (entity <= 0 || entity >= factory.entityPool.Length) { continue; }
            var key = (pair.Key.planetId, (int)factory.entityPool[entity].protoId);
            result.TryGetValue(key, out var counts);
            counts.Total++;
            switch (GetOperatingStatus(pair.Value)) {
                case AdvancedTowerStatus.MissingMatrix: counts.Hungry++; break;
                case AdvancedTowerStatus.MissingInput: counts.Missing++; break;
                case AdvancedTowerStatus.FlowOutputBlocked:
                case AdvancedTowerStatus.ProductOutputBlocked: counts.Blocked++; break;
            }
            result[key] = counts;
        }
        return result;
    }

    /// <summary>
    /// 获取指定实例状态；不存在时创建。
    /// </summary>
    public static AdvancedFractionatorState GetOrCreateState(PlanetFactory factory, int fractionatorId) {
        int planetId = factory?.planetId ?? 0;
        return States.GetOrAdd((planetId, fractionatorId), _ => new AdvancedFractionatorState());
    }

    /// <summary>
    /// 获取指定实例状态；不存在时返回 null。
    /// </summary>
    public static AdvancedFractionatorState GetState(PlanetFactory factory, int fractionatorId) {
        if (factory == null) {
            return null;
        }
        return States.TryGetValue((factory.planetId, fractionatorId), out AdvancedFractionatorState state)
            ? state
            : null;
    }

    /// <summary>
    /// 从高级塔的配方产物或流动输出缓存取出物品。
    /// </summary>
    public static int TakeOutput(PlanetFactory factory, int fractionatorId, int itemId, int count,
        bool flowOutput, out int takenInc) {
        takenInc = 0;
        if (factory == null || count <= 0 || itemId <= 0) {
            return 0;
        }
        AdvancedFractionatorState state = GetState(factory, fractionatorId);
        if (state == null) {
            return 0;
        }
        lock (state) {
            Dictionary<int, int> source = flowOutput ? state.FlowOutputs : state.Products;
            if (!source.TryGetValue(itemId, out int stored) || stored <= 0) {
                return 0;
            }
            int taken = Math.Min(stored, count);
            AddCount(source, itemId, -taken);
            if (flowOutput) {
                int inc = GetCount(state.FlowOutputInc, itemId);
                if (inc > 0) {
                    takenInc = split_inc(ref stored, ref inc, taken);
                    AddCount(state.FlowOutputInc, itemId, -takenInc);
                }
            }
            state.SnapshotDirty = true;
            return taken;
        }
    }

    /// <summary>
    /// 选择高级配方。共同输入与食物能量保留，旧输入转到流动输出，旧产物继续从正面排出。
    /// </summary>
    public static bool SetSelectedRecipe(PlanetFactory factory, int fractionatorId, int persistentRecipeId) {
        if (factory == null || fractionatorId <= 0
            || fractionatorId >= factory.factorySystem.fractionatorPool.Length) {
            return false;
        }
        ref FractionatorComponent fractionator = ref factory.factorySystem.fractionatorPool[fractionatorId];
        if (fractionator.id != fractionatorId || fractionator.entityId <= 0
            || fractionator.entityId >= factory.entityPool.Length) {
            return false;
        }
        int buildingId = factory.entityPool[fractionator.entityId].protoId;
        WorldviewRecipe recipe = RecipeManager.GetRecipe(persistentRecipeId);
        if (persistentRecipeId != 0 && recipe == null) {
            return false;
        }
        if (recipe != null
            && (!recipe.IsAdvanced
                || recipe.RecipeType != FractionatorTowerCatalog.GetRecipeType(buildingId)
                || !ResearchManager.IsContentUnlocked(recipe.StableId))) {
            return false;
        }

        AdvancedFractionatorState state = GetOrCreateState(factory, fractionatorId);
        lock (state) {
            DrainUnusedInputs(state, recipe, passthrough: false);
            ResetInputFlow(state);
            state.SnapshotDirty = true;
            state.SelectedRecipeId = persistentRecipeId;
            state.OutputCursor = 0;
            state.FlowOutputCursor = 0;
            fractionator.progress = 0;
            MirrorVanillaFields(ref fractionator, state, factory.entitySignPool, fractionator.entityId, buildingId);
        }
        return true;
    }

    /// <summary>
    /// 替代原版单输入 InternalUpdate，处理高级塔全部实体状态。
    /// </summary>
    public static uint InternalUpdate(ref FractionatorComponent fractionator, PlanetFactory factory,
        SignData[] signPool, int[] productRegister, int[] consumeRegister) {
        int entityId = fractionator.entityId;
        if (entityId <= 0 || entityId >= factory.entityPool.Length) {
            return 0;
        }
        int buildingId = factory.entityPool[entityId].protoId;
        AdvancedFractionatorState state = GetOrCreateState(factory, fractionator.id);
        if (NebulaMultiplayerModAPI.IsClient) {
            lock (state) {
                fractionator.isWorking = state.ProcessingRate > 0d || state.InputRates.Values.Any(rate => rate > 0d);
                MirrorVanillaFields(ref fractionator, state, signPool, entityId, buildingId);
            }
            return fractionator.isWorking ? 1U : 0U;
        }

        lock (state) {
            WorldviewRecipe recipe = RecipeManager.GetRecipe(state.SelectedRecipeId);
            if (recipe == null || recipe.RecipeType != FractionatorTowerCatalog.GetRecipeType(buildingId)
                || !ResearchManager.IsContentUnlocked(recipe.StableId)) {
                state.SnapshotDirty |= state.SelectedRecipeId != 0;
                state.SelectedRecipeId = 0;
                state.OutputCursor = 0;
                state.FlowOutputCursor = 0;
                ResetInputFlow(state);
                recipe = null;
            }

            CargoTraffic cargoTraffic = factory.cargoTraffic;
            // 先排产物，再判断本帧是否只能直通；排出使阈值恢复后立即允许正常进料。
            if (fractionator.belt0 > 0 && fractionator.isOutput0) {
                state.SnapshotDirty |= TryOutputProducts(state, recipe, cargoTraffic, fractionator.belt0,
                    Math.Max(1, StackingManager.CurrentMaxStack), RequireFullStack(state, recipe));
            }
            bool passthrough = recipe != null && HasBlockedProduct(state, recipe);
            DrainUnusedInputs(state, recipe, passthrough);
            HandleSidePort(state, recipe, cargoTraffic, fractionator.belt1, fractionator.isOutput1, passthrough);
            HandleSidePort(state, recipe, cargoTraffic, fractionator.belt2, fractionator.isOutput2, passthrough);
            EnsureDigestingFood(state, consumeRegister);

            float sacrificeBoost = ResearchManager.GetAdvancedTowerSacrificeBoost(buildingId);
            float durabilityEfficiency = GetDurabilityEfficiency(factory, entityId, state);
            int availableRounds = recipe == null ? 0 : GetAvailableRounds(state, recipe);
            bool canWork = recipe != null && availableRounds > 0 && durabilityEfficiency > 0f
                && CanProcessOutputs(state, recipe);
            int rounds = 0;
            int successfulRounds = 0;
            bool flowing = false;
            if (recipe != null && !passthrough) {
                int readyRounds = AdvanceInputFlows(state, recipe, GetFoodSpeed(state) * durabilityEfficiency);
                if (canWork) {
                    rounds = Math.Min(readyRounds, availableRounds);
                    if (rounds > 0) {
                        successfulRounds = SettleRounds(ref fractionator.seed, state, recipe, rounds, buildingId,
                            sacrificeBoost, productRegister, consumeRegister);
                        state.SnapshotDirty = true;
                    }
                }
                flowing = DrainUnpairedInputs(state, recipe);
            }
            UpdateFlowRates(state, recipe, rounds);
            fractionator.fractionSuccess = successfulRounds > 0;
            fractionator.isWorking = canWork || flowing;
            if (!canWork) { ConsumeFoodEnergy(state, IdleFoodEnergyPerSecond / 60d, consumeRegister); }
            EnsureDigestingFood(state, consumeRegister);
            ApplyDigestionDurability(factory, entityId, state);
            MirrorVanillaFields(ref fractionator, state, signPool, entityId, buildingId);
            return fractionator.isWorking ? 1U : 0U;
        }
    }

    /// <summary>
    /// 拆除高级塔时将缓存内容返还到玩家背包，并移除实体状态。
    /// </summary>
    public static void ReturnAndRemoveState(PlanetFactory factory, int fractionatorId,
        bool returnContents = true, Player player = null) {
        if (factory == null || fractionatorId <= 0) {
            return;
        }
        if (!States.TryRemove((factory.planetId, fractionatorId), out AdvancedFractionatorState state)) {
            return;
        }
        if (NebulaMultiplayerModAPI.IsMultiplayerActive && !NebulaMultiplayerModAPI.IsClient) {
            RemovedStates[(factory.planetId, fractionatorId)] = 0;
        }
        if (!returnContents) {
            return;
        }
        lock (state) {
            ReturnDictionary(player ?? GameMain.mainPlayer, state.Inputs, state.InputInc);
            ReturnDictionary(player ?? GameMain.mainPlayer, state.Foods, state.FoodInc);
            ReturnDictionary(player ?? GameMain.mainPlayer, state.Products, null);
            ReturnDictionary(player ?? GameMain.mainPlayer, state.FlowOutputs, state.FlowOutputInc);
        }
    }

    /// <summary>侧口接收整堆或排出流动物；超量食物与反应物均保留增产点侧流。</summary>
    private static void HandleSidePort(AdvancedFractionatorState state, WorldviewRecipe recipe,
        CargoTraffic cargoTraffic, int beltId, bool isOutput, bool passthrough) {
        if (beltId <= 0) {
            return;
        }
        if (isOutput) {
            state.SnapshotDirty |= TryOutput(state, recipe, cargoTraffic, beltId);
            return;
        }

        for (int i = 0; i < 2; i++) {
            if (!TryReceiveCargo(state, recipe, cargoTraffic, beltId, passthrough)) { break; }
        }
    }

    private static void MoveIncoming(ref int remainingCount, ref int remainingInc,
        Dictionary<int, int> targetCounts, Dictionary<int, int> targetIncs, int itemId, int count) {
        int moved = Math.Min(Math.Max(0, count), remainingCount);
        if (moved <= 0) {
            return;
        }
        int movedInc = remainingCount <= 0 ? 0 : remainingInc * moved / remainingCount;
        AddCount(targetCounts, itemId, moved);
        AddCount(targetIncs, itemId, movedInc);
        remainingCount -= moved;
        remainingInc -= movedInc;
    }

    /// <summary>
    /// 食物槽只保留一种矩阵；正在消化时，备用必须与当前件同种，二者均空才可换种。
    /// </summary>
    private static bool CanTakeFood(AdvancedFractionatorState state, int itemId) {
        if (!IsFoodItem(itemId) || state.DigestingEnergy > 0d && state.DigestingItemId != itemId) {
            return false;
        }
        foreach (KeyValuePair<int, int> pair in state.Foods) {
            if (pair.Value > 0 && pair.Key != itemId) {
                return false;
            }
        }
        return true;
    }

    private static bool IsFoodItem(int itemId) => FE.Logic.Items.MatrixNutrition.IsFood(itemId);

    /// <summary>读取献祭增益后的成功概率，供结算与界面共用。</summary>
    /// <param name="recipe">当前生产配方。</param>
    /// <param name="sacrificeBoost">本塔的相对成功率增益。</param>
    /// <returns>不超过1的成功概率。</returns>
    public static float GetSuccessProbability(WorldviewRecipe recipe, float sacrificeBoost) =>
        Math.Min(1f, recipe.CycleSuccessProbability * (1f + sacrificeBoost));

    /// <summary>读取成功轮中每件输入的实际消耗概率，包括高级矿物特质。</summary>
    /// <param name="input">当前配方输入。</param>
    /// <param name="buildingId">塔体物品ID。</param>
    /// <returns>主料为1，催化剂使用特质减损后的概率。</returns>
    public static float GetInputConsumeProbability(WorldviewRecipeInput input, int buildingId) {
        float probability = input.ConsumeProbability;
        if (probability < 0.9999f && buildingId == IFE高级矿物塔) {
            probability *= GrowthTechnologyManager.AdvancedMineralCatalystConsumptionRatio;
        }
        return Math.Max(0f, probability);
    }

    private static int SettleRounds(ref uint seed, AdvancedFractionatorState state, WorldviewRecipe recipe,
        int rounds, int buildingId, float sacrificeBoost, int[] productRegister, int[] consumeRegister) {
        float successProbability = GetSuccessProbability(recipe, sacrificeBoost);
        int successCount = WorldviewRecipe.RollExpected(ref seed, rounds * successProbability);
        successCount = Math.Min(successCount, rounds);
        // 先限制可支付的成功轮，再统一扣料和产出；未支付部分保留为失败回流。
        foreach (WorldviewRecipeInput input in recipe.InputArray) {
            if (input.ConsumeProbability >= 0.9999f) { continue; }
            double perSuccess = input.Count * (double)GetInputConsumeProbability(input, buildingId);
            if (perSuccess <= 0d) { continue; }
            int stock = GetCount(state.Inputs, input.ItemId);
            successCount = (int)Math.Min(successCount, Math.Floor(stock / perSuccess));
        }
        int failedCount = rounds - successCount;
        int consumedMain = 0;

        foreach (WorldviewRecipeInput input in recipe.InputArray) {
            if (input.ConsumeProbability >= 0.9999f) {
                int removed = rounds * input.Count;
                int removedInc = TakeInput(state, input.ItemId, removed);
                AddCount(state.InputFlowCounts, input.ItemId, removed);
                int failedOutput = failedCount * input.Count;
                if (failedOutput > 0) {
                    AddCount(state.FlowOutputs, input.ItemId, failedOutput);
                    AddCount(state.FlowOutputInc, input.ItemId,
                        removed <= 0 ? 0 : removedInc * failedOutput / removed);
                }
                int consumed = successCount * input.Count;
                consumedMain += consumed;
                if (consumed > 0) {
                    lock (consumeRegister) { consumeRegister[input.ItemId] += consumed; }
                }
            } else {
                float probability = GetInputConsumeProbability(input, buildingId);
                int consumed = WorldviewRecipe.RollExpected(ref seed,
                    successCount * (double)input.Count * Math.Max(0f, probability));
                consumed = Math.Min(consumed, GetCount(state.Inputs, input.ItemId));
                if (consumed > 0) {
                    TakeInput(state, input.ItemId, consumed);
                    lock (consumeRegister) { consumeRegister[input.ItemId] += consumed; }
                }
            }
        }

        ConsumeFoodEnergy(state, consumedMain, consumeRegister);
        if (successCount <= 0) {
            return 0;
        }

        foreach (WorldviewRecipeOutput output in recipe.ProductArray) {
            float outputPerSuccess = GetOutputPerSuccess(recipe, output);
            int outputCount = WorldviewRecipe.RollExpected(ref seed, successCount * outputPerSuccess);
            if (outputCount <= 0) {
                continue;
            }
            AddCount(state.Products, output.ItemId, outputCount);
            lock (productRegister) { productRegister[output.ItemId] += outputCount; }
            if (FractionatorTowerCatalog.IsAdvancedTower(output.ItemId)) {
                ResearchManager.NotifyAdvancedTowerObtained(output.ItemId);
            }
        }
        return successCount;
    }

    /// <summary>将一条当前满堆叠传送带按配方平均消耗配比分摊，食物不计入配方输入。</summary>
    /// <remarks>主料权重为每轮件数；辅料和催化剂权重乘当前生效的消耗概率，不再乘成功率。</remarks>
    /// <param name="recipe">当前配方。</param>
    /// <param name="input">需要分摊速率的配方输入。</param>
    /// <param name="buildingId">塔型，用于读取特质后的消耗概率。</param>
    /// <returns>该材料的满带基准件数/秒；无有效消耗时为0。</returns>
    public static double GetInputFullBeltRate(WorldviewRecipe recipe, WorldviewRecipeInput input, int buildingId) {
        if (recipe == null || input == null) { return 0d; }
        double totalWeight = 0d;
        foreach (WorldviewRecipeInput material in recipe.InputArray) {
            totalWeight += material.Count * (double)GetInputConsumeProbability(material, buildingId);
        }
        if (totalWeight <= 0d) { return 0d; }
        double weight = input.Count * (double)GetInputConsumeProbability(input, buildingId);
        return MaxBeltSpeed * StackingManager.CurrentMaxStack * weight / totalWeight;
    }

    /// <summary>按真实消耗比例扣减货物堆数及增产点，保留余料的平均堆叠。</summary>
    /// <param name="state">当前实例。</param>
    /// <param name="itemId">被消耗的输入。</param>
    /// <param name="count">取出件数。</param>
    /// <returns>取出的增产点。</returns>
    private static int TakeInput(AdvancedFractionatorState state, int itemId, int count) {
        int before = GetCount(state.Inputs, itemId);
        int inc = TakeCount(state.Inputs, state.InputInc, itemId, count);
        int remaining = GetCount(state.Inputs, itemId);
        if (state.InputProgress.TryGetValue(itemId, out double progress)) {
            if (remaining <= 0) { state.InputProgress.Remove(itemId); }
            else { state.InputProgress[itemId] = Math.Min(remaining, Math.Max(0d, progress - (before - remaining))); }
        }
        if (remaining <= 0) { state.InputCargo.Remove(itemId); }
        else if (state.InputCargo.TryGetValue(itemId, out double cargo) && before > 0) {
            state.InputCargo[itemId] = cargo * remaining / before;
        }
        return inc;
    }

    private static int GetAvailableRounds(AdvancedFractionatorState state, WorldviewRecipe recipe) {
        int rounds = int.MaxValue;
        foreach (WorldviewRecipeInput input in recipe.InputArray) {
            int count = GetCount(state.Inputs, input.ItemId);
            if (input.ConsumeProbability >= 0.9999f) {
                rounds = Math.Min(rounds, count / input.Count);
            } else if (count < input.Count) {
                return 0;
            }
        }
        return rounds == int.MaxValue ? 0 : Math.Max(0, rounds);
    }

    /// <summary>在本帧结算前检查各输出当前量；满足阈值即可完整结算，不预留本批结果空间。</summary>
    /// <param name="state">调用方已持有实例锁的缓存。</param>
    /// <param name="recipe">当前生产配方。</param>
    /// <returns>全部主料回流和产物均低于各自阈值时为true。</returns>
    private static bool CanProcessOutputs(AdvancedFractionatorState state, WorldviewRecipe recipe) {
        foreach (WorldviewRecipeInput input in recipe.InputArray) {
            if (input.ConsumeProbability >= 0.9999f && GetCount(state.FlowOutputs, input.ItemId) >= OutputCapacity) {
                return false;
            }
        }
        return !HasBlockedProduct(state, recipe);
    }

    /// <summary>判断当前配方是否有产物达到阈值；与反应停止及输入直通共用。</summary>
    /// <param name="state">调用方已持有实例锁的缓存。</param>
    /// <param name="recipe">当前生产配方。</param>
    /// <returns>任一产物达到或超过阈值时为true。</returns>
    private static bool HasBlockedProduct(AdvancedFractionatorState state, WorldviewRecipe recipe) {
        foreach (WorldviewRecipeOutput output in recipe.ProductArray) {
            if (GetCount(state.Products, output.ItemId) >= OutputCapacity) { return true; }
        }
        return false;
    }

    /// <summary>主料与催化剂齐备且输出低于阈值时等待集装；缺料或堵塞时释放尾料。</summary>
    private static bool RequireFullStack(AdvancedFractionatorState state, WorldviewRecipe recipe) =>
        recipe != null && GetAvailableRounds(state, recipe) > 0
        && CanProcessOutputs(state, recipe);

    private static bool TryOutput(AdvancedFractionatorState state, WorldviewRecipe recipe,
        CargoTraffic cargoTraffic, int beltId) {
        int stackLimit = Math.Max(1, StackingManager.CurrentMaxStack);
        return TryOutputNonTarget(state.FlowOutputs, state.FlowOutputInc, recipe, currentInput: true,
                   cargoTraffic, beltId, stackLimit)
               || TryOutputCurrentFlow(state, recipe, cargoTraffic, beltId, stackLimit, RequireFullStack(state, recipe));
    }

    private static bool TryOutputCurrentFlow(AdvancedFractionatorState state, WorldviewRecipe recipe,
        CargoTraffic cargoTraffic, int beltId, int stackLimit, bool requireFullStack) {
        if (recipe == null || recipe.InputArray.Length == 0) {
            return false;
        }
        int inputCount = recipe.InputArray.Length;
        int startIndex = state.FlowOutputCursor % inputCount;
        for (int offset = 0; offset < inputCount; offset++) {
            int index = (startIndex + offset) % inputCount;
            int itemId = recipe.InputArray[index].ItemId;
            if (!TryOutputItem(state.FlowOutputs, state.FlowOutputInc, itemId,
                    cargoTraffic, beltId, stackLimit, requireFullStack)) {
                continue;
            }
            state.FlowOutputCursor = (index + 1) % inputCount;
            return true;
        }
        return false;
    }

    private static bool TryOutputProducts(AdvancedFractionatorState state, WorldviewRecipe recipe,
        CargoTraffic cargoTraffic, int beltId, int stackLimit, bool requireFullStack) {
        if (TryOutputNonTarget(state.Products, null, recipe, currentInput: false, cargoTraffic, beltId, stackLimit)) {
            return true;
        }
        if (recipe == null || recipe.ProductArray.Length == 0 || state.Products.Count == 0) {
            return false;
        }

        int totalWeight = 0;
        foreach (WorldviewRecipeOutput output in recipe.ProductArray) {
            int count = GetCount(state.Products, output.ItemId);
            if (count <= 0 || requireFullStack && count < stackLimit) {
                continue;
            }
            totalWeight += (count + stackLimit - 1) / stackLimit;
        }
        if (totalWeight <= 0) {
            return false;
        }

        int ticket = state.OutputCursor % totalWeight;
        state.OutputCursor = state.OutputCursor == int.MaxValue ? 0 : state.OutputCursor + 1;
        int selectedItemId = 0;
        foreach (WorldviewRecipeOutput output in recipe.ProductArray) {
            int count = GetCount(state.Products, output.ItemId);
            if (count <= 0 || requireFullStack && count < stackLimit) {
                continue;
            }
            int itemWeight = (count + stackLimit - 1) / stackLimit;
            if (ticket < itemWeight) {
                selectedItemId = output.ItemId;
                break;
            }
            ticket -= itemWeight;
        }
        return selectedItemId > 0
               && TryOutputItem(state.Products, null, selectedItemId,
                   cargoTraffic, beltId, stackLimit, requireFullStack);
    }

    private static bool TryOutputNonTarget(Dictionary<int, int> counts, Dictionary<int, int> incs,
        WorldviewRecipe recipe, bool currentInput, CargoTraffic cargoTraffic, int beltId, int stackLimit) {
        int selectedItemId = 0;
        foreach (KeyValuePair<int, int> pair in counts) {
            bool belongsToCurrentRecipe = recipe != null && (currentInput
                ? recipe.ContainsInput(pair.Key)
                : recipe.ContainsProduct(pair.Key));
            if (!belongsToCurrentRecipe && pair.Value > 0) {
                selectedItemId = pair.Key;
                break;
            }
        }
        return selectedItemId > 0
               && TryOutputItem(counts, incs, selectedItemId,
                   cargoTraffic, beltId, stackLimit, requireFullStack: false);
    }

    private static bool TryOutputItem(Dictionary<int, int> counts, Dictionary<int, int> incs,
        int itemId, CargoTraffic cargoTraffic, int beltId, int stackLimit, bool requireFullStack) {
        int count = GetCount(counts, itemId);
        if (count <= 0 || requireFullStack && count < stackLimit) {
            return false;
        }
        int outputCount = Math.Min(stackLimit, count);
        int outputInc = count <= 0 ? 0 : GetCount(incs, itemId) * outputCount / count;
        if (!cargoTraffic.TryInsertItemAtHead(
                beltId, itemId, (byte)outputCount, (byte)Math.Min(byte.MaxValue, outputInc))) {
            return false;
        }
        AddCount(counts, itemId, -outputCount);
        AddCount(incs, itemId, -outputInc);
        return true;
    }

    private static float GetDurabilityEfficiency(PlanetFactory factory, int entityId,
        AdvancedFractionatorState state) {
        if (state.DigestingEnergy > 0d) {
            return 1f;
        }
        int combatStatId = factory.entityPool[entityId].combatStatId;
        if (combatStatId <= 0 || combatStatId >= factory.skillSystem.combatStats.buffer.Length) {
            return 1f;
        }
        CombatStat combatStat = factory.skillSystem.combatStats.buffer[combatStatId];
        return combatStat.hpMax <= 0 ? 1f : Mathf.Clamp01(combatStat.hp / (float)combatStat.hpMax);
    }

    private static void ApplyDigestionDurability(PlanetFactory factory, int entityId,
        AdvancedFractionatorState state) {
        int combatStatId = factory.entityPool[entityId].combatStatId;
        if (combatStatId > 0) {
            factory.skillSystem.combatStats.buffer[combatStatId].hpRecover = 0;
        }
        if (GameMain.gameTick % 60L != 0L) {
            return;
        }

        SkillTarget target = new() {
            type = ETargetType.Entity,
            id = entityId,
            astroId = factory.planetId,
        };

        bool hasReserve = SumCounts(state.Foods) > 0;
        bool losingDurability = state.DigestingEnergy <= 0d && !hasReserve;
        bool recovering = state.DigestingEnergy > 0d && GetCount(state.Foods, state.DigestingItemId) > 0;
        if (!losingDurability && !recovering) { return; }
        if (!losingDurability && combatStatId <= 0) {
            return;
        }

        factory.skillSystem.GetCombatStat(ref target);
        combatStatId = factory.entityPool[entityId].combatStatId;
        if (combatStatId <= 0) {
            return;
        }
        ref CombatStat combatStat = ref factory.skillSystem.combatStats.buffer[combatStatId];
        // 累计差额让任意连续 360 次扣血之和恰为 hpMax；周期取模也避免长期存档乘法溢出。
        long second = GameMain.gameTick / 60L % StarvationSecondsToDestroy;
        int change = losingDurability
            ? -(int)((long)combatStat.hpMax * (second + 1) / StarvationSecondsToDestroy
                     - (long)combatStat.hpMax * second / StarvationSecondsToDestroy)
            : Math.Max(1, Mathf.CeilToInt(combatStat.hpMax * 0.005f));
        combatStat.hpRecover = 0;
        combatStat.hp = Math.Max(0, Math.Min(combatStat.hpMax, combatStat.hp + change));
    }

    private static void MirrorVanillaFields(ref FractionatorComponent fractionator,
        AdvancedFractionatorState state, SignData[] signPool, int entityId, int buildingId) {
        WorldviewRecipe recipe = RecipeManager.GetRecipe(state.SelectedRecipeId);
        int inputItemId = recipe?.PrimaryInputId ?? 0;
        int productItemId = recipe?.PrimaryProductId ?? 0;
        double progress = recipe == null ? 0d : 1d;
        if (recipe != null) {
            foreach (WorldviewRecipeInput input in recipe.InputArray) {
                if (input.ConsumeProbability < 0.9999f) { continue; }
                state.InputProgress.TryGetValue(input.ItemId, out double inputProgress);
                progress = Math.Min(progress, inputProgress / input.Count);
            }
        }
        fractionator.progress = (int)(progress * ProgressPerRound);
        fractionator.fluidId = inputItemId;
        fractionator.fluidInputCount = GetCount(state.Inputs, inputItemId);
        fractionator.fluidInputInc = GetCount(state.InputInc, inputItemId);
        fractionator.fluidInputCargoCount = state.InputCargo.TryGetValue(inputItemId, out double cargo) ? (float)cargo : 0f;
        fractionator.fluidInputMax = InputCapacity;
        fractionator.fluidOutputCount = SumCounts(state.FlowOutputs);
        fractionator.fluidOutputInc = SumCounts(state.FlowOutputInc);
        fractionator.productId = productItemId;
        fractionator.productOutputCount = GetCount(state.Products, productItemId);
        fractionator.productOutputMax = GetProductCapacity(recipe,
            recipe != null && recipe.ProductArray.Length > 0 ? recipe.ProductArray[0] : null);
        fractionator.produceProb = recipe == null
            ? 0f
            : Math.Min(1f, recipe.CycleSuccessProbability
                           * (1f + ResearchManager.GetAdvancedTowerSacrificeBoost(buildingId)));
        if (entityId > 0 && entityId < signPool.Length) {
            int recipeId = recipe == null ? 0 : AdvancedRecipeCatalog.GetProtoId(recipe.PersistentId);
            signPool[entityId].iconId0 = (uint)recipeId;
            signPool[entityId].iconType = recipeId > 0 ? 2U : 0U;
        }
    }

    /// <summary>读取成功轮的平均实物产量，包括繁育与回收特质；结算与窗口共用。</summary>
    /// <param name="recipe">当前生产配方。</param>
    /// <param name="output">当前产物定义。</param>
    /// <returns>每个成功轮次的平均产出件数。</returns>
    public static float GetOutputPerSuccess(WorldviewRecipe recipe, WorldviewRecipeOutput output) {
        if (recipe.Kind == WorldviewRecipeKind.EmbryoBreeding && output.ItemId == IFE通用原胚) {
            return GrowthTechnologyManager.EmbryoBreedingOutput;
        }
        if (output.ScalesWithRecovery) {
            float recoveryRatio = GrowthTechnologyManager.AdvancedConversionRecoveryRatio;
            return output.Count * recoveryRatio;
        }
        return output.Count;
    }

    /// <summary>读取每种产物的停止阈值，完整本批产量可超过此值。</summary>
    /// <param name="recipe">窗口当前配方；保留调用接口，阈值不取决于配方。</param>
    /// <param name="output">窗口当前产物；所有产物使用相同阈值。</param>
    /// <returns>5乘当前集装层数，单位为件。</returns>
    public static int GetProductCapacity(WorldviewRecipe recipe, WorldviewRecipeOutput output) => OutputCapacity;

    private static void RemoveNonFoodEntries(Dictionary<int, int> foods, Dictionary<int, int> foodInc) {
        foreach (int itemId in foods.Keys.Where(itemId => !IsFoodItem(itemId)).ToArray()) {
            foods.Remove(itemId);
            foodInc.Remove(itemId);
        }
    }

    private static void ReturnDictionary(Player player, Dictionary<int, int> counts,
        Dictionary<int, int> incs) {
        if (player == null) {
            return;
        }
        foreach (KeyValuePair<int, int> pair in counts) {
            int itemId = pair.Key;
            int count = pair.Value;
            if (count <= 0) {
                continue;
            }
            int returned = player.TryAddItemToPackage(
                itemId, count, incs == null ? 0 : GetCount(incs, itemId), throwTrash: true);
            if (returned > 0 && ReferenceEquals(player, GameMain.mainPlayer)) {
                UIItemup.Up(itemId, returned);
            }
        }
    }

    private static int TakeCount(Dictionary<int, int> counts, Dictionary<int, int> incs,
        int itemId, int requested) {
        int current = GetCount(counts, itemId);
        int taken = Math.Min(Math.Max(0, requested), current);
        int currentInc = GetCount(incs, itemId);
        int takenInc = current <= 0 ? 0 : currentInc * taken / current;
        AddCount(counts, itemId, -taken);
        AddCount(incs, itemId, -takenInc);
        return takenInc;
    }

    /// <summary>直接枚举具体字典汇总件数，避免每帧经 LINQ 接口枚举产生装箱。</summary>
    /// <param name="counts">已持有实例锁的数量或增产点字典。</param>
    /// <returns>各物品数量之和，保留原求和的溢出检查。</returns>
    private static int SumCounts(Dictionary<int, int> counts) {
        int total = 0;
        foreach (KeyValuePair<int, int> pair in counts) {
            total = checked(total + pair.Value);
        }
        return total;
    }

    private static int GetCount(Dictionary<int, int> dictionary, int itemId) =>
        dictionary != null && dictionary.TryGetValue(itemId, out int count) ? count : 0;

    private static void AddCount(Dictionary<int, int> dictionary, int itemId, int delta) {
        if (dictionary == null || itemId <= 0 || delta == 0) {
            return;
        }
        long value = (long)GetCount(dictionary, itemId) + delta;
        int normalized = (int)Math.Max(0L, Math.Min(int.MaxValue, value));
        if (normalized == 0) {
            dictionary.Remove(itemId);
        } else {
            dictionary[itemId] = normalized;
        }
    }

    private static void ReadDictionary(BinaryReader reader, Dictionary<int, int> dictionary, int maximumValue) {
        dictionary.Clear();
        int count = reader.ReadInt32();
        if (count < 0 || count > MaximumDictionaryEntryCount) {
            throw new InvalidDataException($"Invalid advanced fractionator dictionary size: {count}");
        }
        for (int i = 0; i < count; i++) {
            int itemId = reader.ReadInt32();
            int itemCount = reader.ReadInt32();
            if (itemId > 0 && itemId < 12000 && itemCount > 0) {
                dictionary[itemId] = Math.Min(maximumValue, itemCount);
            }
        }
    }

    private static void ClampIncDictionary(Dictionary<int, int> incs, Dictionary<int, int> counts) {
        foreach (int itemId in incs.Keys.ToArray()) {
            int maximumInc = GetCount(counts, itemId) * 10;
            if (maximumInc <= 0) {
                incs.Remove(itemId);
            } else {
                incs[itemId] = Math.Min(incs[itemId], maximumInc);
            }
        }
    }

    private static void WriteDictionary(BinaryWriter writer, Dictionary<int, int> dictionary) {
        writer.WriteList(dictionary.OrderBy(pair => pair.Key).ToArray(), (item, pair) => {
            item.Write(pair.Key);
            item.Write(pair.Value);
        });
    }
}
