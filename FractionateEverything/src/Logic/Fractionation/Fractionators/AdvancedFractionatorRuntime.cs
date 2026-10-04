using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FE.Compatibility.Nebula;
using FE.Logic.Civilization;
using FE.Logic.Enhancements.Stacking;
using FE.Logic.Fractionation.Recipes;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>
/// 一座高级分馏塔的多输入、多输出、营养和配方状态。
/// </summary>
public sealed class AdvancedFractionatorState {
    /// <summary>选定配方的稳定整数 ID；0 表示未选择。</summary>
    public int SelectedRecipeId;

    /// <summary>反应输入数量。</summary>
    public readonly Dictionary<int, int> Inputs = [];

    /// <summary>反应输入增产点。</summary>
    public readonly Dictionary<int, int> InputInc = [];

    /// <summary>尚未转化为营养的矩阵食物数量。</summary>
    public readonly Dictionary<int, int> Foods = [];

    /// <summary>矩阵食物增产点，仅用于拆除时原样返还。</summary>
    public readonly Dictionary<int, int> FoodInc = [];

    /// <summary>配方产物数量。</summary>
    public readonly Dictionary<int, int> Products = [];

    /// <summary>非目标物品和失败轮流动输出数量。</summary>
    public readonly Dictionary<int, int> FlowOutputs = [];

    /// <summary>流动输出增产点。</summary>
    public readonly Dictionary<int, int> FlowOutputInc = [];

    /// <summary>当前营养点数，范围 0..1000。</summary>
    public float Nutrition;

    /// <summary>最近一次转入营养的矩阵层次，范围 0..5。</summary>
    public int LastFoodTier;

    /// <summary>下一次正常输出优先尝试流动输出；仅为调度游标，不必持久化。</summary>
    public bool PreferFlowOutput = true;

    /// <summary>按比例轮询产物的游标。</summary>
    public int OutputCursor;

    /// <summary>按配方输入顺序轮询流动输出的游标。</summary>
    public int FlowOutputCursor;

    /// <summary>按配方输入顺序轮询多余底料的游标。</summary>
    public int InputOutputCursor;
}

/// <summary>
/// 三类高级塔的多输入反应运行时。
/// </summary>
public static class AdvancedFractionatorRuntime {
    public const float MaximumNutrition = 1000f;
    private const float WorkingNutritionPerRound = 0.02f;
    private const float IdleNutritionPerSecond = 0.002f;
    private const int ProgressPerSecond = 60000;
    /// <summary>读档时允许的最大缓存件数，按堆叠上限计算；读档时全局堆叠可能尚未同步，不能用当前值裁剪。</summary>
    private const int MaximumBufferCapacity = ProcessManager.BufferStackCount * StackingManager.AbsoluteMaxStack;
    private const int MaximumStateCount = 1000000;
    private const int MaximumDictionaryEntryCount = 12000;
    private static readonly ConcurrentDictionary<(int planetId, int fractionatorId), AdvancedFractionatorState>
        States = [];
    private static readonly float[] FoodNutrition = [1f, 2f, 4f, 8f, 16f, 32f];
    private static readonly float[] FoodSpeed = [1f, 1.1f, 1.2f, 1.3f, 1.4f, 1.5f];

    /// <summary>供窗口与汇总页共用的工作状态，按营养、配方、原料和输出空间区分原因。</summary>
    /// <param name="state">高级塔状态。</param>
    /// <returns>玩家可读的状态文案。</returns>
    public static string GetOperatingStatus(AdvancedFractionatorState state) {
        if (state == null) { return "等待供养"; }
        lock (state) {
            if (state.Nutrition <= 0f) { return "饥饿"; }
            WorldviewRecipe recipe = RecipeManager.GetRecipe(state.SelectedRecipeId);
            if (recipe == null) { return "等待配方"; }
            int rounds = GetAvailableRounds(state, recipe);
            if (rounds <= 0) { return "缺少原材料"; }
            if (LimitRoundsByOutputRoom(state, recipe, rounds, ResearchManager.MemorySuccessBoost) <= 0) {
                return "输出堵塞";
            }
            int stack = StackingManager.CurrentMaxStack;
            if (stack > 1 && state.Products.Values.Any(count => count > 0 && count < stack)) { return "运行中 · 等待集装"; }
            return "正常运转";
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
                case "饥饿": counts.Hungry++; break;
                case "缺少原材料": counts.Missing++; break;
                case "输出堵塞": counts.Blocked++; break;
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
            return taken;
        }
    }

    /// <summary>
    /// 选择高级配方。共同输入和营养保留，其他缓存进入优先流出区。
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
            MoveNonTargetEntries(state.Inputs, state.InputInc, state.FlowOutputs, state.FlowOutputInc,
                recipe, retainInputs: true);
            MoveNonTargetEntries(state.Products, null, state.FlowOutputs, state.FlowOutputInc,
                recipe, retainInputs: false);
            state.SelectedRecipeId = persistentRecipeId;
            state.OutputCursor = 0;
            state.FlowOutputCursor = 0;
            state.InputOutputCursor = 0;
            fractionator.progress = 0;
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
            MirrorVanillaFields(ref fractionator, state, signPool, entityId, buildingId);
            return fractionator.isWorking ? 1U : 0U;
        }

        lock (state) {
            WorldviewRecipe recipe = RecipeManager.GetRecipe(state.SelectedRecipeId);
            if (recipe == null || recipe.RecipeType != FractionatorTowerCatalog.GetRecipeType(buildingId)
                || !ResearchManager.IsContentUnlocked(recipe.StableId)) {
                state.SelectedRecipeId = 0;
                state.OutputCursor = 0;
                state.FlowOutputCursor = 0;
                state.InputOutputCursor = 0;
                recipe = null;
            }

            CargoTraffic cargoTraffic = factory.cargoTraffic;
            HandlePort(ref fractionator, state, recipe, cargoTraffic, fractionator.belt0,
                fractionator.isOutput0, consumeRegister);
            HandlePort(ref fractionator, state, recipe, cargoTraffic, fractionator.belt1,
                fractionator.isOutput1, consumeRegister);
            HandlePort(ref fractionator, state, recipe, cargoTraffic, fractionator.belt2,
                fractionator.isOutput2, consumeRegister);
            ConvertFoodToNutrition(state, consumeRegister);

            int availableRounds = recipe == null ? 0 : GetAvailableRounds(state, recipe);
            float sacrificeBoost = ResearchManager.GetAdvancedTowerSacrificeBoost(buildingId);
            int executableRounds = recipe == null
                ? 0
                : LimitRoundsByOutputRoom(state, recipe, availableRounds, sacrificeBoost);
            float durabilityEfficiency = GetDurabilityEfficiency(factory, entityId, state);
            bool canWork = recipe != null && executableRounds > 0 && durabilityEfficiency > 0f;
            float processingRate = 0f;
            int successfulRounds = 0;
            if (canWork) {
                float speed = FoodSpeed[Math.Max(0, Math.Min(FoodSpeed.Length - 1, state.LastFoodTier))];
                processingRate = executableRounds * speed * durabilityEfficiency;
                fractionator.progress += Mathf.RoundToInt(ProgressPerSecond / 60f * processingRate);
                int rounds = Math.Min(fractionator.progress / ProgressPerSecond, executableRounds);
                if (rounds > 0) {
                    fractionator.progress -= rounds * ProgressPerSecond;
                    successfulRounds = SettleRounds(ref fractionator.seed, state, recipe, rounds, buildingId,
                        sacrificeBoost, productRegister, consumeRegister);
                }
            }

            fractionator.fractionSuccess = successfulRounds > 0;
            fractionator.isWorking = canWork;
            ConsumeNutritionAndApplyDurability(factory, entityId, state, processingRate);
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

    /// <summary>
    /// 读取全部高级塔实例状态。
    /// </summary>
    public static void Import(BinaryReader r) {
        States.Clear();
        int count = r.ReadInt32();
        if (count < 0 || count > MaximumStateCount) {
            throw new InvalidDataException($"Invalid advanced fractionator state count: {count}");
        }
        for (int i = 0; i < count; i++) {
            int planetId = r.ReadInt32();
            int fractionatorId = r.ReadInt32();
            int selectedRecipeId = r.ReadInt32();
            WorldviewRecipe selectedRecipe = RecipeManager.GetRecipe(selectedRecipeId);
            if (selectedRecipeId != 0 && selectedRecipe?.IsAdvanced != true) {
                selectedRecipeId = 0;
            }
            float nutrition = r.ReadSingle();
            if (float.IsNaN(nutrition) || float.IsInfinity(nutrition)) {
                nutrition = 0f;
            }
            AdvancedFractionatorState state = new() {
                SelectedRecipeId = selectedRecipeId,
                Nutrition = Mathf.Clamp(nutrition, 0f, MaximumNutrition),
                LastFoodTier = Math.Max(0, Math.Min(5, r.ReadInt32())),
                OutputCursor = Math.Max(0, r.ReadInt32()),
            };
            ReadDictionary(r, state.Inputs, MaximumBufferCapacity);
            ReadDictionary(r, state.InputInc, MaximumBufferCapacity * 10);
            ReadDictionary(r, state.Foods, MaximumBufferCapacity);
            ReadDictionary(r, state.FoodInc, MaximumBufferCapacity * 10);
            ReadDictionary(r, state.Products, MaximumBufferCapacity);
            ReadDictionary(r, state.FlowOutputs, MaximumBufferCapacity);
            ReadDictionary(r, state.FlowOutputInc, MaximumBufferCapacity * 10);
            ClampIncDictionary(state.InputInc, state.Inputs);
            RemoveNonFoodEntries(state.Foods, state.FoodInc);
            ClampIncDictionary(state.FoodInc, state.Foods);
            ClampIncDictionary(state.FlowOutputInc, state.FlowOutputs);
            if (planetId > 0 && fractionatorId > 0) {
                States[(planetId, fractionatorId)] = state;
            }
        }
    }

    /// <summary>
    /// 写入全部高级塔实例状态。
    /// </summary>
    public static void Export(BinaryWriter w) {
        KeyValuePair<(int planetId, int fractionatorId), AdvancedFractionatorState>[] statePairs = States
            .OrderBy(pair => pair.Key.planetId)
            .ThenBy(pair => pair.Key.fractionatorId)
            .ToArray();
        w.Write(statePairs.Length);
        foreach (KeyValuePair<(int planetId, int fractionatorId), AdvancedFractionatorState> pair in statePairs) {
            (int planetId, int fractionatorId) key = pair.Key;
            AdvancedFractionatorState state = pair.Value;
            lock (state) {
                w.Write(key.planetId);
                w.Write(key.fractionatorId);
                w.Write(state.SelectedRecipeId);
                w.Write(state.Nutrition);
                w.Write(state.LastFoodTier);
                w.Write(state.OutputCursor);
                WriteDictionary(w, state.Inputs);
                WriteDictionary(w, state.InputInc);
                WriteDictionary(w, state.Foods);
                WriteDictionary(w, state.FoodInc);
                WriteDictionary(w, state.Products);
                WriteDictionary(w, state.FlowOutputs);
                WriteDictionary(w, state.FlowOutputInc);
            }
        }
    }

    /// <summary>
    /// 切档时清除全部高级塔实例状态。
    /// </summary>
    public static void IntoOtherSave() => States.Clear();

    private static void HandlePort(ref FractionatorComponent fractionator, AdvancedFractionatorState state,
        WorldviewRecipe recipe, CargoTraffic cargoTraffic, int beltId, bool isOutput, int[] consumeRegister) {
        if (beltId <= 0) {
            return;
        }
        if (isOutput) {
            TryOutput(state, recipe, cargoTraffic, beltId);
            return;
        }

        for (int i = 0; i < 2; i++) {
            int rearItemId = cargoTraffic.GetItemIdAtRear(beltId);
            int rearStack = ProcessManager.GetRearCargoStack(cargoTraffic, beltId);
            if (rearItemId <= 0 || !CanAcceptIncomingStack(state, recipe, rearItemId, rearStack)) {
                break;
            }
            int itemId = cargoTraffic.TryPickItemAtRear(beltId, 0, null, out byte stack, out byte inc);
            if (itemId <= 0 || stack <= 0) {
                break;
            }
            int remainingCount = stack;
            int remainingInc = inc;
            int foodRoom = CanTakeFood(state, itemId)
                ? Math.Max(0, BufferCapacity - GetCount(state.Foods, itemId))
                : 0;
            MoveIncoming(ref remainingCount, ref remainingInc,
                state.Foods, state.FoodInc, itemId, Math.Min(remainingCount, foodRoom));
            if (remainingCount <= 0) {
                continue;
            }

            WorldviewRecipeInput input = null;
            if (recipe != null) {
                recipe.TryGetInput(itemId, out input);
            }
            if (input != null) {
                int retainedCount = Math.Min(BufferCapacity, Math.Max(input.Count, input.Count * 2));
                int reserveRoom = Math.Max(0, retainedCount - GetCount(state.Inputs, itemId));
                MoveIncoming(ref remainingCount, ref remainingInc,
                    state.Inputs, state.InputInc, itemId, Math.Min(remainingCount, reserveRoom));

                int flowRoom = Math.Max(0, BufferCapacity - GetCount(state.FlowOutputs, itemId));
                MoveIncoming(ref remainingCount, ref remainingInc,
                    state.FlowOutputs, state.FlowOutputInc, itemId, Math.Min(remainingCount, flowRoom));

                int overflowRoom = Math.Max(0, BufferCapacity - GetCount(state.Inputs, itemId));
                MoveIncoming(ref remainingCount, ref remainingInc,
                    state.Inputs, state.InputInc, itemId, Math.Min(remainingCount, overflowRoom));
            } else {
                MoveIncoming(ref remainingCount, ref remainingInc,
                    state.FlowOutputs, state.FlowOutputInc, itemId, remainingCount);
            }
        }
    }

    private static bool CanAcceptIncomingStack(AdvancedFractionatorState state, WorldviewRecipe recipe,
        int itemId, int incomingCount) {
        if (incomingCount <= 0) {
            return false;
        }
        int availableRoom = CanTakeFood(state, itemId)
            ? Math.Max(0, BufferCapacity - GetCount(state.Foods, itemId))
            : 0;
        WorldviewRecipeInput input = null;
        if (recipe != null) {
            recipe.TryGetInput(itemId, out input);
        }
        if (input != null) {
            availableRoom += Math.Max(0, BufferCapacity - GetCount(state.Inputs, itemId));
        }
        availableRoom += Math.Max(0, BufferCapacity - GetCount(state.FlowOutputs, itemId));
        return availableRoom >= incomingCount;
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

    private static void ConvertFoodToNutrition(AdvancedFractionatorState state, int[] consumeRegister) {
        if (state.Nutrition >= MaximumNutrition) {
            return;
        }
        bool convertedFood = false;
        for (int tier = FoodNutrition.Length - 1; tier >= 0; tier--) {
            int itemId = I电磁矩阵 + tier;
            float nutritionPerItem = FoodNutrition[tier];
            int available = GetCount(state.Foods, itemId);
            if (available <= 0) {
                continue;
            }
            int consumed = Math.Min(available,
                Mathf.FloorToInt((MaximumNutrition - state.Nutrition) / nutritionPerItem));
            if (consumed <= 0) {
                // 营养空间装不下一件时等待，不拿低层矩阵填小缺口。
                break;
            }
            TakeCount(state.Foods, state.FoodInc, itemId, consumed);
            state.Nutrition += nutritionPerItem * consumed;
            if (!convertedFood) {
                state.LastFoodTier = tier;
                convertedFood = true;
            }
            Interlocked.Add(ref consumeRegister[itemId], consumed);
        }
        state.Nutrition = Mathf.Min(MaximumNutrition, state.Nutrition);
    }

    /// <summary>
    /// 食物槽只有一个位置：槽内已有某种矩阵时只接收同种，空槽时传送带先来哪种就收哪种。
    /// </summary>
    private static bool CanTakeFood(AdvancedFractionatorState state, int itemId) {
        if (!IsFoodItem(itemId)) {
            return false;
        }
        foreach (KeyValuePair<int, int> pair in state.Foods) {
            if (pair.Value > 0 && pair.Key != itemId) {
                return false;
            }
        }
        return true;
    }

    /// <summary>当前缓存区上限：20 堆 × 当前全局堆叠。</summary>
    private static int BufferCapacity => ProcessManager.BaseFracFluidInputCargoMax;

    private static bool IsFoodItem(int itemId) {
        int tier = itemId - I电磁矩阵;
        return tier >= 0 && tier < FoodNutrition.Length;
    }

    private static int SettleRounds(ref uint seed, AdvancedFractionatorState state, WorldviewRecipe recipe,
        int rounds, int buildingId, float sacrificeBoost, int[] productRegister, int[] consumeRegister) {
        float successProbability = Math.Min(1f,
            recipe.CycleSuccessProbability * (1f + sacrificeBoost));
        int successCount = WorldviewRecipe.RollExpected(ref seed, rounds * successProbability);
        successCount = Math.Min(successCount, rounds);
        int failedCount = rounds - successCount;

        foreach (WorldviewRecipeInput input in recipe.InputArray) {
            if (input.ConsumeProbability >= 0.9999f) {
                int removed = rounds * input.Count;
                int removedInc = TakeCount(state.Inputs, state.InputInc, input.ItemId, removed);
                int failedOutput = failedCount * input.Count;
                if (failedOutput > 0) {
                    AddCount(state.FlowOutputs, input.ItemId, failedOutput);
                    AddCount(state.FlowOutputInc, input.ItemId,
                        removed <= 0 ? 0 : removedInc * failedOutput / removed);
                }
                int consumed = successCount * input.Count;
                if (consumed > 0) {
                    Interlocked.Add(ref consumeRegister[input.ItemId], consumed);
                }
            } else {
                float probability = input.ConsumeProbability;
                if (buildingId == IFE高级矿物塔) {
                    probability *= 1f - GrowthTechnologyManager.AdvancedMineralTraitLevel * 0.1f;
                }
                int consumed = WorldviewRecipe.RollExpected(ref seed,
                    successCount * input.Count * Math.Max(0f, probability));
                consumed = Math.Min(consumed, GetCount(state.Inputs, input.ItemId));
                if (consumed > 0) {
                    TakeCount(state.Inputs, state.InputInc, input.ItemId, consumed);
                    Interlocked.Add(ref consumeRegister[input.ItemId], consumed);
                }
            }
        }

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
            Interlocked.Add(ref productRegister[output.ItemId], outputCount);
            if (FractionatorTowerCatalog.IsAdvancedTowerItem(output.ItemId)) {
                ResearchManager.NotifyAdvancedTowerObtained(output.ItemId);
            }
        }
        recipe.RecordSuccesses(successCount);
        ProcessManager.RecordFractionSuccess(successCount);
        return successCount;
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

    private static int LimitRoundsByOutputRoom(AdvancedFractionatorState state, WorldviewRecipe recipe,
        int rounds, float sacrificeBoost) {
        if (rounds <= 0) {
            return 0;
        }
        int outputCapacity = GetOutputCapacity();
        foreach (WorldviewRecipeInput input in recipe.InputArray) {
            if (input.ConsumeProbability < 0.9999f) {
                continue;
            }
            int available = outputCapacity - GetCount(state.FlowOutputs, input.ItemId);
            rounds = Math.Min(rounds, Math.Max(0, available / Math.Max(1, input.Count)));
        }
        if (rounds <= 0) {
            return 0;
        }
        int maxSuccesses = int.MaxValue;
        foreach (WorldviewRecipeOutput output in recipe.ProductArray) {
            int maximumPerSuccess = Math.Max(1, Mathf.CeilToInt(GetOutputPerSuccess(recipe, output)));
            int available = outputCapacity - GetCount(state.Products, output.ItemId);
            maxSuccesses = Math.Min(maxSuccesses, Math.Max(0, available / maximumPerSuccess));
        }
        if (maxSuccesses <= 0) {
            return 0;
        }
        float successProbability = Math.Max(0.0001f,
            Math.Min(1f, recipe.CycleSuccessProbability * (1f + sacrificeBoost)));
        int maximumRoundsByProducts = Mathf.FloorToInt(maxSuccesses / successProbability);
        return Math.Min(rounds, maximumRoundsByProducts);
    }

    private static void TryOutput(AdvancedFractionatorState state, WorldviewRecipe recipe,
        CargoTraffic cargoTraffic, int beltId) {
        int stackLimit = Math.Max(1, StackingManager.CurrentMaxStack);

        // 换配方后的旧物和完全无关物品优先排空，不等待集装。
        if (TryOutputNonTarget(state.FlowOutputs, state.FlowOutputInc, recipe, currentInput: true,
                cargoTraffic, beltId, stackLimit)
            || TryOutputNonTarget(state.Inputs, state.InputInc, recipe, currentInput: true,
                cargoTraffic, beltId, stackLimit)
            || TryOutputNonTarget(state.Products, null, recipe, currentInput: false,
                cargoTraffic, beltId, stackLimit)) {
            return;
        }

        // 主消耗物已不足一轮时释放尾料；保留的催化剂不能让尾料永久等待集装。
        bool requireFullStack = stackLimit > 1 && recipe != null && GetAvailableRounds(state, recipe) > 0;
        if (state.PreferFlowOutput) {
            if (TryOutputCurrentFlow(state, recipe, cargoTraffic, beltId, stackLimit, requireFullStack)) {
                state.PreferFlowOutput = false;
                return;
            }
            if (TryOutputProducts(state, recipe, cargoTraffic, beltId, stackLimit, requireFullStack)) {
                state.PreferFlowOutput = true;
            }
            return;
        }

        if (TryOutputProducts(state, recipe, cargoTraffic, beltId, stackLimit, requireFullStack)) {
            state.PreferFlowOutput = true;
            return;
        }
        if (TryOutputCurrentFlow(state, recipe, cargoTraffic, beltId, stackLimit, requireFullStack)) {
            state.PreferFlowOutput = false;
        }
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
        return TryOutputExcessInputs(state, recipe, cargoTraffic, beltId, stackLimit, requireFullStack);
    }

    private static bool TryOutputProducts(AdvancedFractionatorState state, WorldviewRecipe recipe,
        CargoTraffic cargoTraffic, int beltId, int stackLimit, bool requireFullStack) {
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

    private static bool TryOutputExcessInputs(AdvancedFractionatorState state, WorldviewRecipe recipe,
        CargoTraffic cargoTraffic, int beltId, int stackLimit, bool requireFullStack) {
        int inputCount = recipe.InputArray.Length;
        int startIndex = state.InputOutputCursor % inputCount;
        for (int offset = 0; offset < inputCount; offset++) {
            int index = (startIndex + offset) % inputCount;
            WorldviewRecipeInput input = recipe.InputArray[index];
            int count = GetCount(state.Inputs, input.ItemId);
            int retainedCount = Math.Min(BufferCapacity, input.Count * 2);
            int excessCount = Math.Max(0, count - retainedCount);
            if (excessCount <= 0 || requireFullStack && excessCount < stackLimit) {
                continue;
            }
            int outputLimit = Math.Min(stackLimit, excessCount);
            if (!TryOutputItem(state.Inputs, state.InputInc, input.ItemId,
                    cargoTraffic, beltId, outputLimit, requireFullStack: false)) {
                continue;
            }
            state.InputOutputCursor = (index + 1) % inputCount;
            return true;
        }
        return false;
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

    private static bool AreCurrentInputsEmpty(AdvancedFractionatorState state, WorldviewRecipe recipe) {
        if (recipe == null) {
            return true;
        }
        foreach (WorldviewRecipeInput input in recipe.InputArray) {
            if (GetCount(state.Inputs, input.ItemId) > 0) {
                return false;
            }
        }
        return true;
    }

    private static float GetDurabilityEfficiency(PlanetFactory factory, int entityId,
        AdvancedFractionatorState state) {
        if (state.Nutrition > 0f) {
            return 1f;
        }
        int combatStatId = factory.entityPool[entityId].combatStatId;
        if (combatStatId <= 0 || combatStatId >= factory.skillSystem.combatStats.buffer.Length) {
            return 1f;
        }
        CombatStat combatStat = factory.skillSystem.combatStats.buffer[combatStatId];
        return combatStat.hpMax <= 0 ? 1f : Mathf.Clamp01(combatStat.hp / (float)combatStat.hpMax);
    }

    private static void ConsumeNutritionAndApplyDurability(PlanetFactory factory, int entityId,
        AdvancedFractionatorState state, float processingRate) {
        float drain = processingRate > 0f
            ? WorkingNutritionPerRound * processingRate / 60f
            : IdleNutritionPerSecond / 60f;
        state.Nutrition = Mathf.Max(0f, state.Nutrition - drain);

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

        bool losingDurability = state.Nutrition <= 0f;
        if (!losingDurability && state.Nutrition <= MaximumNutrition * 0.5f) {
            return;
        }
        if (!losingDurability && combatStatId <= 0) {
            return;
        }

        factory.skillSystem.GetCombatStat(ref target);
        combatStatId = factory.entityPool[entityId].combatStatId;
        if (combatStatId <= 0) {
            return;
        }
        ref CombatStat combatStat = ref factory.skillSystem.combatStats.buffer[combatStatId];
        int change = losingDurability
            ? -Math.Max(1, Mathf.CeilToInt(combatStat.hpMax * 0.01f))
            : Math.Max(1, Mathf.CeilToInt(combatStat.hpMax * 0.005f));
        combatStat.hpRecover = 0;
        combatStat.hp = Math.Max(0, Math.Min(combatStat.hpMax, combatStat.hp + change));
    }

    private static void MirrorVanillaFields(ref FractionatorComponent fractionator,
        AdvancedFractionatorState state, SignData[] signPool, int entityId, int buildingId) {
        WorldviewRecipe recipe = RecipeManager.GetRecipe(state.SelectedRecipeId);
        int inputItemId = recipe?.PrimaryInputId ?? 0;
        int productItemId = recipe?.PrimaryProductId ?? 0;
        fractionator.fluidId = inputItemId;
        fractionator.fluidInputCount = GetCount(state.Inputs, inputItemId);
        fractionator.fluidInputInc = GetCount(state.InputInc, inputItemId);
        fractionator.fluidInputCargoCount = fractionator.fluidInputCount;
        fractionator.productId = productItemId;
        fractionator.productOutputCount = GetCount(state.Products, productItemId);
        fractionator.produceProb = recipe == null
            ? 0f
            : Math.Min(1f, recipe.CycleSuccessProbability
                           * (1f + ResearchManager.GetAdvancedTowerSacrificeBoost(buildingId)));
        if (entityId > 0 && entityId < signPool.Length) {
            signPool[entityId].iconId0 = (uint)(fractionator.fluidId > 0 ? fractionator.fluidId : 0);
            signPool[entityId].iconType = fractionator.fluidId > 0 ? 1U : 0U;
        }
    }

    private static int GetOutputCapacity() => BufferCapacity;

    private static void MoveNonTargetEntries(Dictionary<int, int> source, Dictionary<int, int> sourceInc,
        Dictionary<int, int> target, Dictionary<int, int> targetInc, WorldviewRecipe recipe,
        bool retainInputs) {
        foreach (int itemId in source.Keys.ToArray()) {
            bool retained = recipe != null && (retainInputs
                ? recipe.ContainsInput(itemId)
                : recipe.ContainsProduct(itemId));
            if (retained) {
                continue;
            }
            int sourceCount = GetCount(source, itemId);
            int moved = Math.Min(sourceCount,
                Math.Max(0, BufferCapacity - GetCount(target, itemId)));
            if (moved <= 0) {
                continue;
            }
            int movedInc = sourceInc == null
                ? 0
                : GetCount(sourceInc, itemId) * moved / sourceCount;
            AddCount(target, itemId, moved);
            AddCount(targetInc, itemId, movedInc);
            AddCount(source, itemId, -moved);
            AddCount(sourceInc, itemId, -movedInc);
        }
    }

    private static float GetOutputPerSuccess(WorldviewRecipe recipe, WorldviewRecipeOutput output) {
        if (recipe.Kind == WorldviewRecipeKind.EmbryoBreeding && output.ItemId == IFE通用原胚) {
            return 1.5f + GrowthTechnologyManager.AdvancedInteractionTraitLevel * 0.05f;
        }
        if (output.ScalesWithRecovery) {
            float recoveryRatio = 0.5f + GrowthTechnologyManager.AdvancedConversionTraitLevel * 0.05f;
            return output.Count * recoveryRatio;
        }
        return output.Count;
    }

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
        writer.Write(dictionary.Count);
        foreach (KeyValuePair<int, int> pair in dictionary.OrderBy(pair => pair.Key)) {
            writer.Write(pair.Key);
            writer.Write(pair.Value);
        }
    }
}
