using System;
using FE.Compatibility.Nebula;
using FE.Logic.Civilization;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Items;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>高级塔逐件消化矩阵；备用库存、当前消化物与能量扣减共用实例锁。</summary>
public static partial class AdvancedFractionatorRuntime {
    /// <summary>以原版满带30件/秒的一倍单堆主料速率为基准，待机消耗其二十分之一。</summary>
    private const double IdleFoodEnergyPerSecond = 30d / 20d;

    /// <summary>按统一矩阵估值和单件增产点计算点燃能量，不受Productive标志影响。</summary>
    public static double GetFoodEnergy(int itemId, int inc) => MatrixNutrition.GetEnergy(itemId)
        * (1d + Cargo.incTableMilli[Math.Max(0, Math.Min(10, inc))]);

    /// <summary>供进度条显示当前件的剩余比例；调用方须持有实例锁。</summary>
    public static float GetDigestionProgress(AdvancedFractionatorState state) {
        double capacity = GetFoodEnergy(state.DigestingItemId, state.DigestingInc);
        return capacity <= 0d ? 0f : (float)Math.Max(0d, Math.Min(1d, state.DigestingEnergy / capacity));
    }

    /// <summary>读取当前正在消化的矩阵速度倍率，供运行器与窗口共用。</summary>
    /// <param name="state">调用方已持有实例锁的消化状态。</param>
    /// <returns>当前矩阵对应的速度；没有消化物时为1。</returns>
    public static float GetFoodSpeed(AdvancedFractionatorState state) => state.DigestingEnergy > 0d
        ? MatrixNutrition.GetSpeed(state.DigestingItemId) : 1f;

    /// <summary>读取当前矩阵的预计消耗与工作配比，不将失败回流计入供养消耗。</summary>
    /// <param name="state">调用方已持有实例锁的消化与运行状态。</param>
    /// <param name="recipe">当前配方；未选择时按待机消耗计算。</param>
    /// <param name="buildingId">当前塔体物品ID，用于读取献祭成功率。</param>
    /// <returns>矩阵件数/分钟，以及食物在工作实际消耗的食物与主料总件数中的占比；空消化槽均为0。</returns>
    public static (double ConsumptionPerMinute, double Share) GetFoodRates(AdvancedFractionatorState state,
        WorldviewRecipe recipe, int buildingId) {
        double energy = GetFoodEnergy(state.DigestingItemId, state.DigestingInc);
        if (state.DigestingEnergy <= 0d || energy <= 0d) { return (0d, 0d); }
        double consumption = IdleFoodEnergyPerSecond * 60d;
        if (recipe != null && state.ProcessingRate > 0d) {
            int mainPerRound = 0;
            foreach (WorldviewRecipeInput input in recipe.InputArray) {
                if (input.ConsumeProbability >= 0.9999f) { mainPerRound += input.Count; }
            }
            float success = GetSuccessProbability(recipe, ResearchManager.GetAdvancedTowerSacrificeBoost(buildingId));
            consumption = state.ProcessingRate * 60d * success * mainPerRound;
        }
        return (consumption / energy, 1d / (1d + energy));
    }

    /// <summary>只在空槽点燃一件，同种备用优先；旧档残留多种时按营养价值选择。</summary>
    private static bool EnsureDigestingFood(AdvancedFractionatorState state, int[] consumeRegister, int preferredItemId = 0) {
        if (state.DigestingEnergy > 0d) { return true; }
        int itemId = GetCount(state.Foods, preferredItemId) > 0 && IsFoodItem(preferredItemId)
            ? preferredItemId : SelectReserveFood(state);
        if (itemId == 0) { return false; }
        int inc = TakeCount(state.Foods, state.FoodInc, itemId, 1);
        state.DigestingItemId = itemId;
        state.DigestingInc = Math.Max(0, Math.Min(10, inc));
        state.DigestingEnergy = GetFoodEnergy(itemId, state.DigestingInc);
        state.DigestionVersion++;
        state.SnapshotDirty = true;
        lock (consumeRegister) { consumeRegister[itemId]++; }
        return true;
    }

    /// <summary>从真实备用库存选最高营养的有效矩阵，同价值按ID稳定排序。</summary>
    private static int SelectReserveFood(AdvancedFractionatorState state) {
        int selected = 0;
        double energy = 0d;
        foreach (var pair in state.Foods) {
            if (pair.Value <= 0) { continue; }
            double candidate = MatrixNutrition.GetEnergy(pair.Key);
            if (candidate > energy || candidate > 0d && candidate == energy && pair.Key < selected) {
                selected = pair.Key;
                energy = candidate;
            }
        }
        return selected;
    }

    /// <summary>按实际消费逐件扣能量；耗尽后才换件，最后一件的余量不丢失，也不保存断粮欠账。</summary>
    private static void ConsumeFoodEnergy(AdvancedFractionatorState state, double amount, int[] consumeRegister) {
        while (amount > 0d && EnsureDigestingFood(state, consumeRegister)) {
            double capacity = GetFoodEnergy(state.DigestingItemId, state.DigestingInc);
            int beforePercent = (int)Math.Floor(state.DigestingEnergy / capacity * 100d);
            double consumed = Math.Min(amount, state.DigestingEnergy);
            amount -= consumed;
            state.DigestingEnergy -= consumed;
            if (state.DigestingEnergy <= 0d) {
                int itemId = state.DigestingItemId;
                ClearDigestion(state);
                EnsureDigestingFood(state, consumeRegister, itemId);
            } else {
                state.SnapshotDirty |= beforePercent != (int)Math.Floor(state.DigestingEnergy / capacity * 100d);
            }
        }
    }

    /// <summary>销毁当前件，不返还已点燃的矩阵。</summary>
    private static void ClearDigestion(AdvancedFractionatorState state) {
        state.DigestingItemId = 0;
        state.DigestingEnergy = 0d;
        state.DigestingInc = 0;
        state.DigestionVersion++;
        state.SnapshotDirty = true;
    }

    /// <summary>从备用槽取出指定矩阵并保留对应增产点；客户端不修改主机库存。</summary>
    public static int TakeFood(PlanetFactory factory, int fractionatorId, int itemId, out int takenInc) {
        takenInc = 0;
        if (NebulaMultiplayerModAPI.IsClient || !IsFoodItem(itemId)) { return 0; }
        AdvancedFractionatorState state = GetState(factory, fractionatorId);
        if (state == null) { return 0; }
        lock (state) {
            int count = GetCount(state.Foods, itemId);
            if (count <= 0) { return 0; }
            takenInc = TakeCount(state.Foods, state.FoodInc, itemId, count);
            state.SnapshotDirty = true;
            return count;
        }
    }

    /// <summary>空备用时只销毁按下时的同一件；补入备用、换件、切档或重建实例使长按失效。</summary>
    public static bool DiscardDigestion(PlanetFactory factory, int fractionatorId,
        AdvancedFractionatorState expectedState, long expectedVersion) {
        if (NebulaMultiplayerModAPI.IsClient) { return false; }
        AdvancedFractionatorState state = GetState(factory, fractionatorId);
        if (state == null || !ReferenceEquals(state, expectedState)) { return false; }
        lock (state) {
            if (state.DigestingEnergy <= 0d || state.DigestionVersion != expectedVersion
                || SumCounts(state.Foods) > 0) { return false; }
            ClearDigestion(state);
            return true;
        }
    }
}
