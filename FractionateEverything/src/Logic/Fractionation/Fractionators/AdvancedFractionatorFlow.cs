using System;
using FE.Logic.Fractionation.Recipes;
using static FE.Logic.Fractionation.Fractionators.ProcessManager;

namespace FE.Logic.Fractionation.Fractionators;

public static partial class AdvancedFractionatorRuntime {
    /// <summary>按游戏刻累计半秒实物流量，避免逐帧整数结算让窗口速率跳动。</summary>
    private const int FlowSampleInterval = 30;

    /// <summary>所有需要流动的输入共享入口带速；积压多的物品获得更多额度，不按物品重复计算整带能力。</summary>
    /// <returns>主料已推进且能共同配对的完整轮数；是否允许反应由调用方检查。</returns>
    private static int AdvanceInputFlows(AdvancedFractionatorState state, WorldviewRecipe recipe, double speed,
        double cargoRate, bool passthrough) {
        double demand = 0d;
        foreach (var pair in state.Inputs) {
            if (pair.Value <= 0 || GetCount(state.FlowOutputs, pair.Key) >= OutputCapacity) { continue; }
            double factor = GetInputFlowFactor(recipe, pair.Key, speed, passthrough);
            demand += GetInputCargo(state, pair.Key, pair.Value) * factor;
        }
        double scale = demand > 0d ? Math.Min(1d, cargoRate / demand) : 0d;
        foreach (var pair in state.Inputs) {
            if (pair.Value <= 0 || GetCount(state.FlowOutputs, pair.Key) >= OutputCapacity) { continue; }
            double factor = GetInputFlowFactor(recipe, pair.Key, speed, passthrough);
            if (factor <= 0d) { continue; }
            state.InputProgress.TryGetValue(pair.Key, out double previous);
            // 真实货物数乘平均堆叠等于件数；先分摊货物额度，再推进现有实物。
            double progress = Math.Min(pair.Value, previous + pair.Value * factor * scale / 60d);
            if (progress != previous) {
                state.InputProgress[pair.Key] = progress;
                state.SnapshotDirty = true;
            }
        }
        int rounds = int.MaxValue;
        if (recipe != null && !passthrough) {
            foreach (WorldviewRecipeInput input in recipe.InputArray) {
                if (input.ConsumeProbability < 0.9999f) { continue; }
                state.InputProgress.TryGetValue(input.ItemId, out double progress);
                rounds = Math.Min(rounds, (int)(progress / input.Count));
            }
        }
        return rounds == int.MaxValue ? 0 : rounds;
    }

    /// <summary>旧输入与产物堵塞时按直通速度排空；正常辅料留在缓存，不占主料推进额度。</summary>
    private static double GetInputFlowFactor(WorldviewRecipe recipe, int itemId, double speed, bool passthrough) {
        if (passthrough || recipe == null || !recipe.TryGetInput(itemId, out WorldviewRecipeInput input)) { return 1d; }
        return input.ConsumeProbability >= 0.9999f ? speed : 0d;
    }

    /// <summary>侧面输入带提供全塔共享的货物/秒上限；无输入带时允许手工库存按单带能力排空。</summary>
    private static double GetInputCargoRate(FractionatorComponent fractionator, CargoTraffic traffic) {
        double rate = 0d;
        if (fractionator.belt1 > 0 && !fractionator.isOutput1) { rate += traffic.beltPool[fractionator.belt1].speed * 6d; }
        if (fractionator.belt2 > 0 && !fractionator.isOutput2) { rate += traffic.beltPool[fractionator.belt2].speed * 6d; }
        return rate > 0d ? Math.Min(rate, MaxBeltSpeed) : MaxBeltSpeed;
    }

    /// <summary>未配对的已推进物原样侧流；正常主料留一轮储备，旧输入和堵塞直通不留储备。</summary>
    private static bool DrainUnpairedInputs(AdvancedFractionatorState state, WorldviewRecipe recipe, bool passthrough) {
        bool moved = false;
        var pending = state.UnprocessedInputIds;
        pending.Clear();
        foreach (var pair in state.Inputs) {
            if (pair.Value > 0 && GetCount(state.FlowOutputs, pair.Key) < OutputCapacity
                && GetInputFlowFactor(recipe, pair.Key, 1d, passthrough) > 0d) { pending.Add(pair.Key); }
        }
        foreach (int itemId in pending) {
            WorldviewRecipeInput input = null;
            bool main = recipe != null && recipe.TryGetInput(itemId, out input) && input.ConsumeProbability >= 0.9999f;
            int reserve = main && !passthrough ? input.Count : 0;
            state.InputProgress.TryGetValue(itemId, out double progress);
            int count = (int)Math.Max(0d, Math.Floor(progress) - reserve);
            if (count <= 0) { continue; }
            int inc = TakeInput(state, itemId, count);
            AddCount(state.FlowOutputs, itemId, count);
            AddCount(state.FlowOutputInc, itemId, inc);
            if (main) { AddCount(state.InputFlowCounts, itemId, count); }
            moved = true;
        }
        state.SnapshotDirty |= moved;
        return moved;
    }

    /// <summary>公布同一时间窗口内的主料实物流量与配对轮速率；只统计运行器实际搬出的物品。</summary>
    private static void UpdateFlowRates(AdvancedFractionatorState state, WorldviewRecipe recipe, int rounds) {
        state.FlowSampleRounds += rounds;
        if (++state.FlowSampleTicks < FlowSampleInterval) { return; }
        double scale = 60d / state.FlowSampleTicks;
        double processingRate = state.FlowSampleRounds * scale;
        state.SnapshotDirty |= state.ProcessingRate != processingRate;
        state.ProcessingRate = processingRate;
        if (recipe != null) {
            foreach (WorldviewRecipeInput input in recipe.InputArray) {
                if (input.ConsumeProbability < 0.9999f) { continue; }
                double rate = GetCount(state.InputFlowCounts, input.ItemId) * scale;
                state.InputRates.TryGetValue(input.ItemId, out double previous);
                state.SnapshotDirty |= previous != rate;
                state.InputRates[input.ItemId] = rate;
            }
        }
        state.InputFlowCounts.Clear();
        state.FlowSampleTicks = state.FlowSampleRounds = 0;
    }

    /// <summary>换配方或恢复快照前清除推进与采样，不改动任何实物缓存。</summary>
    private static void ResetInputFlow(AdvancedFractionatorState state) {
        state.InputProgress.Clear();
        state.InputRates.Clear();
        state.InputFlowCounts.Clear();
        state.ProcessingRate = 0d;
        state.FlowSampleTicks = state.FlowSampleRounds = 0;
        state.FlowOutputCredit = 0d;
    }
}
