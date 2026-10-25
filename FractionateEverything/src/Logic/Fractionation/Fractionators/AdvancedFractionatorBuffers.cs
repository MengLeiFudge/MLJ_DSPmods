using System;
using FE.Compatibility.Nebula;
using FE.Logic.Enhancements.Stacking;
using FE.Logic.Fractionation.Recipes;
using static FE.Logic.Fractionation.Fractionators.ProcessManager;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>高级塔按物品阈值接收完整货物并维护手动存取；所有调用共用实例锁。</summary>
public static partial class AdvancedFractionatorRuntime {
    /// <summary>备用食物硬容量等于当前全局堆叠层数，消化中的一件另计。</summary>
    public static int FoodCapacity => StackingManager.CurrentMaxStack;
    /// <summary>每种低概率消耗催化剂的储备件数，不占用主料缓存。</summary>
    public const int CatalystCapacity = 5;

    /// <summary>读取一种反应物的独立容量；配方无关物只经过流动输出。</summary>
    /// <param name="recipe">当前选择的配方。</param>
    /// <param name="itemId">待接收或显示的物品。</param>
    /// <returns>主料和辅料使用40堆件数；催化剂保留至少一轮且不低于5件；无关物为0。</returns>
    public static int GetInputCapacity(WorldviewRecipe recipe, int itemId) {
        if (recipe == null || !recipe.TryGetInput(itemId, out WorldviewRecipeInput input)) { return 0; }
        return input.ConsumeProbability < 0.1f ? Math.Max(CatalystCapacity, input.Count) : InputCapacity;
    }

    /// <summary>读取缓存中的真实等效货物堆数；缺少货物元数据时按单件一堆处理。</summary>
    private static double GetInputCargo(AdvancedFractionatorState state, int itemId, int count) =>
        state.InputCargo.TryGetValue(itemId, out double cargo) && cargo > 0d ? cargo : count;

    /// <summary>从真实反应输入取物，同时按比例扣除货物堆数及增产点。</summary>
    /// <param name="factory">目标工厂。</param>
    /// <param name="fractionatorId">目标分馏组件ID。</param>
    /// <param name="itemId">待取物品ID。</param>
    /// <param name="count">最多取出的件数。</param>
    /// <param name="takenInc">实际取出的增产点。</param>
    /// <returns>实际取出件数；客户端或无库存时为0。</returns>
    public static int TakeInput(PlanetFactory factory, int fractionatorId, int itemId, int count, out int takenInc) {
        takenInc = 0;
        if (NebulaMultiplayerModAPI.IsClient || itemId <= 0 || count <= 0) { return 0; }
        AdvancedFractionatorState state = GetState(factory, fractionatorId);
        if (state == null) { return 0; }
        lock (state) {
            int taken = Math.Min(count, GetCount(state.Inputs, itemId));
            if (taken <= 0) { return 0; }
            takenInc = TakeInput(state, itemId, taken);
            state.SnapshotDirty = true;
            return taken;
        }
    }

    /// <summary>向输入补入手持物；配方外物品沿既有旁路侧流，保留增产点。</summary>
    /// <param name="factory">目标工厂。</param>
    /// <param name="fractionatorId">目标分馏组件ID。</param>
    /// <param name="itemId">手持反应物ID。</param>
    /// <param name="count">手持件数。</param>
    /// <param name="inc">手持增产点。</param>
    /// <param name="addedInc">实际转入塔内的增产点。</param>
    /// <returns>实际转入件数；槽满、无效物品或客户端时为0。</returns>
    public static int AddInput(PlanetFactory factory, int fractionatorId, int itemId, int count, int inc, out int addedInc) {
        addedInc = 0;
        if (NebulaMultiplayerModAPI.IsClient || itemId <= 0 || count <= 0 || !LDB.items.Exist(itemId)) { return 0; }
        AdvancedFractionatorState state = GetState(factory, fractionatorId);
        if (state == null) { return 0; }
        lock (state) {
            WorldviewRecipe recipe = RecipeManager.GetRecipe(state.SelectedRecipeId);
            int capacity = GetInputCapacity(recipe, itemId);
            if (capacity <= 0) { capacity = InputCapacity; }
            int added = GetCount(state.Inputs, itemId) < capacity ? Math.Min(count, capacity) : 0;
            if (added <= 0) { return 0; }
            long validInc = Math.Max(0L, Math.Min(inc, (long)count * 10));
            addedInc = (int)(validInc * added / count);
            AddCount(state.Inputs, itemId, added);
            AddCount(state.InputInc, itemId, addedInc);
            // 手持库存没有带上货物元数据，人工补料按当前集装上限分堆。
            state.InputCargo.TryGetValue(itemId, out double cargo);
            state.InputCargo[itemId] = cargo + added / (double)Math.Max(1, StackingManager.CurrentMaxStack);
            state.SnapshotDirty = true;
            return added;
        }
    }

    /// <summary>手持物补入流出或产物缓存，不登记生产量；正常输出及拆除返还保留增产点。</summary>
    public static int AddOutput(PlanetFactory factory, int fractionatorId, int itemId, int count, int inc,
        bool flowOutput, out int addedInc) {
        addedInc = 0;
        if (NebulaMultiplayerModAPI.IsClient || itemId <= 0 || count <= 0 || !LDB.items.Exist(itemId)) { return 0; }
        AdvancedFractionatorState state = GetState(factory, fractionatorId);
        if (state == null) { return 0; }
        lock (state) {
            var counts = flowOutput ? state.FlowOutputs : state.Products;
            var incs = flowOutput ? state.FlowOutputInc : state.ProductInc;
            int added = Math.Min(count, Math.Max(0, OutputCapacity - GetCount(counts, itemId)));
            if (added <= 0) { return 0; }
            int remaining = count;
            int remainingInc = Math.Max(0, (int)Math.Min(inc, (long)count * 10));
            int validInc = remainingInc;
            MoveIncoming(ref remaining, ref remainingInc, counts, incs, itemId, added);
            addedInc = validInc - remainingInc;
            state.SnapshotDirty = true;
            return added;
        }
    }

    /// <summary>手动补入同种备用矩阵，遵守食物硬容量，不提前点燃或扣除手持之外的库存。</summary>
    public static int AddFood(PlanetFactory factory, int fractionatorId, int itemId, int count, int inc, out int addedInc) {
        addedInc = 0;
        if (NebulaMultiplayerModAPI.IsClient || count <= 0) { return 0; }
        AdvancedFractionatorState state = GetState(factory, fractionatorId);
        if (state == null) { return 0; }
        lock (state) {
            if (!CanTakeFood(state, itemId)) { return 0; }
            int added = Math.Min(count, Math.Max(0, FoodCapacity - SumCounts(state.Foods)));
            if (added <= 0) { return 0; }
            int remaining = count;
            int remainingInc = Math.Max(0, (int)Math.Min(inc, (long)count * 10));
            int validInc = remainingInc;
            MoveIncoming(ref remaining, ref remainingInc, state.Foods, state.FoodInc, itemId, added);
            addedInc = validInc - remainingInc;
            state.SnapshotDirty = true;
            return added;
        }
    }

    /// <summary>低于阈值时接收完整货物；满缓存只允许借用一次收料空间，打破回环满带与满缓存的相互等待。</summary>
    /// <param name="state">调用方已持有实例锁的缓存。</param>
    /// <param name="recipe">当前配方；无配方时仅接收食物与直通物。</param>
    /// <param name="cargoTraffic">当前工厂的带与货物容器。</param>
    /// <param name="beltId">输入侧面连接的传送带。</param>
    /// <returns>是否接收了一整堆货物。</returns>
    private static bool TryReceiveCargo(AdvancedFractionatorState state, WorldviewRecipe recipe,
        CargoTraffic cargoTraffic, int beltId, bool passthrough, bool hasFlowOutput) {
        CargoPath path = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[beltId].segPathId);
        // 窥看与拿起共用路径锁，按当前数量决定整批去向，不预留剩余件数。
        lock (path.buffer) {
            int cargoId = path.GetCargoIdAtRear();
            if (cargoId < 0) { return false; }
            Cargo incoming = cargoTraffic.container.cargoPool[cargoId];
            int itemId = incoming.item;
            int count = incoming.stack;
            if (itemId <= 0 || count <= 0) { return false; }
            bool reactionInput = !passthrough && recipe?.ContainsInput(itemId) == true;
            // 矩阵兼作反应物时，只在断粮且没有备用时留一件点燃，后续货物优先进入配方。
            int foodRoom = reactionInput ? state.DigestingEnergy <= 0d && SumCounts(state.Foods) == 0 ? 1 : 0
                : Math.Max(0, FoodCapacity - SumCounts(state.Foods));
            int food = CanTakeFood(state, itemId) ? Math.Min(count, foodRoom) : 0;
            bool foreignFood = !reactionInput && IsFoodItem(itemId) && !CanTakeFood(state, itemId);
            int input = !passthrough && !foreignFood && GetCount(state.Inputs, itemId) < GetInputCapacity(recipe, itemId)
                ? count - food : 0;
            int flow = count - food - input;
            int flowCount = GetCount(state.FlowOutputs, itemId);
            bool needsCirculationSpace = flow > 0 && flowCount >= OutputCapacity;
            if (needsCirculationSpace && (!hasFlowOutput || state.CirculationIntakePending
                || flowCount + flow > MaximumBufferCapacity)) { return false; }
            if (cargoTraffic.TryPickItemAtRear(beltId, itemId, null, out byte stack, out byte inc) <= 0) { return false; }
            int remainingCount = stack;
            int remainingInc = inc;
            MoveIncoming(ref remainingCount, ref remainingInc, state.Foods, state.FoodInc, itemId, food);
            if (input > 0) {
                state.InputCargo.TryGetValue(itemId, out double cargo);
                state.InputCargo[itemId] = cargo + input / (double)stack;
                MoveIncoming(ref remainingCount, ref remainingInc, state.Inputs, state.InputInc, itemId, input);
            }
            MoveIncoming(ref remainingCount, ref remainingInc, state.FlowOutputs, state.FlowOutputInc, itemId, remainingCount);
            state.CirculationIntakePending |= needsCirculationSpace;
            state.SnapshotDirty = true;
            return true;
        }
    }

}
