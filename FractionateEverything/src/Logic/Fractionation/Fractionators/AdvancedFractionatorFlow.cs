using System;
using FE.Logic.Fractionation.Recipes;
using static FE.Logic.Fractionation.Fractionators.ProcessManager;

namespace FE.Logic.Fractionation.Fractionators;

public static partial class AdvancedFractionatorRuntime {
    /// <summary>按游戏刻累计半秒实物流量，避免逐帧整数结算让窗口速率跳动。</summary>
    private const int FlowSampleInterval = 30;

    /// <summary>主料分别推进件数进度；流出堵塞的物品不推进，其余物品继续流动。</summary>
    /// <returns>已推进且能共同配对的完整轮数；是否允许反应由调用方检查。</returns>
    private static int AdvanceInputFlows(AdvancedFractionatorState state, WorldviewRecipe recipe, double speed) {
        int rounds = int.MaxValue;
        foreach (WorldviewRecipeInput input in recipe.InputArray) {
            if (input.ConsumeProbability < 0.9999f) { continue; }
            int count = GetCount(state.Inputs, input.ItemId);
            state.InputProgress.TryGetValue(input.ItemId, out double previous);
            double progress = Math.Min(count, previous);
            if (speed > 0d && GetCount(state.FlowOutputs, input.ItemId) < OutputCapacity) {
                // 进度只标记现有实物，先限制为库存再取整；高倍率不会生成虚拟物品或溢出。
                progress = Math.Min(count, progress + GetInputProcessingRate(state, input.ItemId) * speed / 60d);
            }
            if (progress != previous) {
                state.InputProgress[input.ItemId] = progress;
                state.SnapshotDirty = true;
            }
            rounds = Math.Min(rounds, (int)(progress / input.Count));
        }
        return rounds == int.MaxValue ? 0 : rounds;
    }

    /// <summary>未配对的已推进主料原样侧流；保留一轮配对储备，避免零散供料永远凑不齐。</summary>
    private static bool DrainUnpairedInputs(AdvancedFractionatorState state, WorldviewRecipe recipe) {
        bool moved = false;
        foreach (WorldviewRecipeInput input in recipe.InputArray) {
            if (input.ConsumeProbability < 0.9999f
                || GetCount(state.FlowOutputs, input.ItemId) >= OutputCapacity) { continue; }
            state.InputProgress.TryGetValue(input.ItemId, out double progress);
            int count = (int)Math.Max(0d, Math.Floor(progress) - input.Count);
            if (count <= 0) { continue; }
            int inc = TakeInput(state, input.ItemId, count);
            AddCount(state.FlowOutputs, input.ItemId, count);
            AddCount(state.FlowOutputInc, input.ItemId, inc);
            AddCount(state.InputFlowCounts, input.ItemId, count);
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
    }
}
