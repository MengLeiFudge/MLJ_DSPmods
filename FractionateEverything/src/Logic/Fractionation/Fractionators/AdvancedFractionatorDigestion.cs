using System;
using FE.Compatibility.Nebula;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>高级塔逐件消化矩阵；备用库存、当前消化物与能量扣减共用实例锁。</summary>
public static partial class AdvancedFractionatorRuntime {
    /// <summary>以原版满带30件/秒的一倍单堆主料速率为基准，待机消耗其二十分之一。</summary>
    private const double IdleFoodEnergyPerSecond = 30d / 20d;
    private static readonly double[] FoodShare = [0.16d, 0.11d, 0.07d, 0.04d, 0.02d, 0.01d];
    private static readonly float[] FoodSpeed = [1f, 1.4f, 1.8f, 2.2f, 2.6f, 3f];

    /// <summary>按矩阵档位和单件增产点计算点燃能量，不受Productive标志影响。</summary>
    public static double GetFoodEnergy(int itemId, int inc) {
        int tier = itemId - I电磁矩阵;
        if (tier < 0 || tier >= FoodShare.Length) { return 0d; }
        return (1d / FoodShare[tier] - 1d) * (1d + Cargo.incTableMilli[Math.Max(0, Math.Min(10, inc))]);
    }

    /// <summary>供进度条显示当前件的剩余比例；调用方须持有实例锁。</summary>
    public static float GetDigestionProgress(AdvancedFractionatorState state) {
        double capacity = GetFoodEnergy(state.DigestingItemId, state.DigestingInc);
        return capacity <= 0d ? 0f : (float)Math.Max(0d, Math.Min(1d, state.DigestingEnergy / capacity));
    }

    private static float GetFoodSpeed(AdvancedFractionatorState state) {
        int tier = state.DigestingItemId - I电磁矩阵;
        return state.DigestingEnergy > 0d && tier >= 0 && tier < FoodSpeed.Length ? FoodSpeed[tier] : 1f;
    }

    /// <summary>只在空槽点燃一件；旧档若有多种备用食物，依矩阵层次逐种排空，不混合能量。</summary>
    private static bool EnsureDigestingFood(AdvancedFractionatorState state, int[] consumeRegister) {
        if (state.DigestingEnergy > 0d) { return true; }
        for (int tier = FoodShare.Length - 1; tier >= 0; tier--) {
            int itemId = I电磁矩阵 + tier;
            if (GetCount(state.Foods, itemId) <= 0) { continue; }
            int inc = TakeCount(state.Foods, state.FoodInc, itemId, 1);
            state.DigestingItemId = itemId;
            state.DigestingInc = Math.Max(0, Math.Min(10, inc));
            state.DigestingEnergy = GetFoodEnergy(itemId, state.DigestingInc);
            state.DigestionVersion++;
            state.SnapshotDirty = true;
            lock (consumeRegister) { consumeRegister[itemId]++; }
            return true;
        }
        return false;
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
                ClearDigestion(state);
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

    /// <summary>长按完成后只销毁按下时的同一件食物；换件、切档或重建实例均使操作失效。</summary>
    public static bool DiscardDigestion(PlanetFactory factory, int fractionatorId,
        AdvancedFractionatorState expectedState, long expectedVersion) {
        if (NebulaMultiplayerModAPI.IsClient) { return false; }
        AdvancedFractionatorState state = GetState(factory, fractionatorId);
        if (state == null || !ReferenceEquals(state, expectedState)) { return false; }
        lock (state) {
            if (state.DigestingEnergy <= 0d || state.DigestionVersion != expectedVersion) { return false; }
            ClearDigestion(state);
            return true;
        }
    }
}
