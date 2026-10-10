using System;
using FE.Compatibility.Nebula;
using FE.Logic.Enhancements.Stacking;
using FE.Logic.Fractionation.Recipes;
using static FE.Logic.Fractionation.Fractionators.ProcessManager;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>高级塔按物品独立限量、整堆准入及手动存取；所有调用共用实例锁。</summary>
public static partial class AdvancedFractionatorRuntime {
    /// <summary>备用食物最多保存的未点燃矩阵件数，不随集装科技增长。</summary>
    public const int FoodCapacity = 5;
    /// <summary>每种低概率消耗催化剂的储备件数，不占用主料缓存。</summary>
    public const int CatalystCapacity = 5;
    /// <summary>原版参与速度计算的最大货物堆数；40堆输入容量另留收发余量。</summary>
    private const double ProcessingCargoLimit = 30d;

    /// <summary>读取一种反应物的独立容量；配方无关物只经过流动输出。</summary>
    /// <param name="recipe">当前选择的配方。</param>
    /// <param name="itemId">待接收或显示的物品。</param>
    /// <returns>主料使用40堆件数，催化剂使用固定储备；无关物为0。</returns>
    public static int GetInputCapacity(WorldviewRecipe recipe, int itemId) {
        if (recipe == null || !recipe.TryGetInput(itemId, out WorldviewRecipeInput input)) { return 0; }
        return input.ConsumeProbability < 0.9999f ? CatalystCapacity : InputCapacity;
    }

    /// <summary>读取一种主料满倍率下的经过件数/秒；运行和界面共用真实平均堆叠。</summary>
    /// <param name="state">调用方已持有实例锁的输入件数与货物数。</param>
    /// <param name="itemId">配方主料的物品ID。</param>
    /// <returns>原版前30堆乘真实平均堆叠的件数/秒，空输入为0。</returns>
    private static double GetInputProcessingRate(AdvancedFractionatorState state, int itemId) {
        int count = GetCount(state.Inputs, itemId);
        double cargo = state.InputCargo.TryGetValue(itemId, out double stored) ? stored : count;
        return count <= 0 || cargo <= 0d ? 0d : Math.Min(cargo, ProcessingCargoLimit) * count / cargo;
    }

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

    /// <summary>向当前配方输入槽补料，只接收该槽剩余容量并保留对应增产点。</summary>
    /// <param name="factory">目标工厂。</param>
    /// <param name="fractionatorId">目标分馏组件ID。</param>
    /// <param name="itemId">手持反应物ID。</param>
    /// <param name="count">手持件数。</param>
    /// <param name="inc">手持增产点。</param>
    /// <param name="addedInc">实际转入塔内的增产点。</param>
    /// <returns>实际转入件数；不属于当前配方、槽满或客户端时为0。</returns>
    public static int AddInput(PlanetFactory factory, int fractionatorId, int itemId, int count, int inc, out int addedInc) {
        addedInc = 0;
        if (NebulaMultiplayerModAPI.IsClient || itemId <= 0 || count <= 0) { return 0; }
        AdvancedFractionatorState state = GetState(factory, fractionatorId);
        if (state == null) { return 0; }
        lock (state) {
            WorldviewRecipe recipe = RecipeManager.GetRecipe(state.SelectedRecipeId);
            int room = Math.Max(0, GetInputCapacity(recipe, itemId) - GetCount(state.Inputs, itemId));
            int added = Math.Min(count, room);
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

    /// <summary>只在各目标槽能共同接收整堆时拿起货物；多余物直接进入同物品流出槽。</summary>
    /// <param name="state">调用方已持有实例锁的缓存。</param>
    /// <param name="recipe">当前配方；无配方时仅接收食物与直通物。</param>
    /// <param name="cargoTraffic">当前工厂的带与货物容器。</param>
    /// <param name="beltId">输入侧面连接的传送带。</param>
    /// <returns>是否接收了一整堆货物。</returns>
    private static bool TryReceiveCargo(AdvancedFractionatorState state, WorldviewRecipe recipe,
        CargoTraffic cargoTraffic, int beltId) {
        CargoPath path = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[beltId].segPathId);
        // 将窥看与拿起放在同一带路径锁内，避免货物移动后按上一堆的数量预留空间。
        lock (path.buffer) {
            int cargoId = path.GetCargoIdAtRear();
            if (cargoId < 0) { return false; }
            Cargo incoming = cargoTraffic.container.cargoPool[cargoId];
            int itemId = incoming.item;
            int count = incoming.stack;
            if (itemId <= 0 || count <= 0) { return false; }
            int food = CanTakeFood(state, itemId) ? Math.Min(count, Math.Max(0, FoodCapacity - SumCounts(state.Foods))) : 0;
            int input = Math.Min(count - food, Math.Max(0, GetInputCapacity(recipe, itemId) - GetCount(state.Inputs, itemId)));
            int flow = count - food - input;
            if (flow > Math.Max(0, BufferCapacity - GetCount(state.FlowOutputs, itemId))) { return false; }
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
            state.SnapshotDirty = true;
            return true;
        }
    }

    /// <summary>将旧配方输入与超过独立容量的库存逐步流出；空间不足时保留真实库存。</summary>
    /// <param name="state">调用方已持有实例锁的缓存。</param>
    /// <param name="recipe">当前配方，决定需要保留的各项输入容量。</param>
    private static void DrainExcessInputs(AdvancedFractionatorState state, WorldviewRecipe recipe) {
        var pending = state.UnprocessedInputIds;
        pending.Clear();
        foreach (var pair in state.Inputs) {
            if (pair.Value > GetInputCapacity(recipe, pair.Key)) { pending.Add(pair.Key); }
        }
        foreach (int itemId in pending) {
            int excess = GetCount(state.Inputs, itemId) - GetInputCapacity(recipe, itemId);
            int moved = Math.Min(excess, Math.Max(0, BufferCapacity - GetCount(state.FlowOutputs, itemId)));
            if (moved <= 0) { continue; }
            int inc = TakeInput(state, itemId, moved);
            AddCount(state.FlowOutputs, itemId, moved);
            AddCount(state.FlowOutputInc, itemId, inc);
            state.SnapshotDirty = true;
        }
        // 旧档若保留多种矩阵，仅为最高档留一份储备，其余按流出空间逐步排空。
        int reserveId = 0;
        foreach (var pair in state.Foods) {
            if (pair.Value > 0) { reserveId = Math.Max(reserveId, pair.Key); }
        }
        pending.Clear();
        foreach (var pair in state.Foods) {
            if (pair.Key != reserveId || pair.Value > FoodCapacity) { pending.Add(pair.Key); }
        }
        foreach (int itemId in pending) {
            int excess = GetCount(state.Foods, itemId) - (itemId == reserveId ? FoodCapacity : 0);
            int moved = Math.Min(excess, Math.Max(0, BufferCapacity - GetCount(state.FlowOutputs, itemId)));
            if (moved <= 0) { continue; }
            int inc = TakeCount(state.Foods, state.FoodInc, itemId, moved);
            AddCount(state.FlowOutputs, itemId, moved);
            AddCount(state.FlowOutputInc, itemId, inc);
            state.SnapshotDirty = true;
        }
    }
}
