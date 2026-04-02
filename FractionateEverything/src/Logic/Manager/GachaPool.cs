using System;
using System.Collections.Generic;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

/// <summary>稀有度等级</summary>
public enum GachaRarity { C, B, A, S }

/// <summary>单次抽卡结果</summary>
public readonly struct GachaResult {
    public readonly int ItemId;        // 物品ID（配方用inputID，物品用itemID）
    public readonly GachaRarity Rarity;
    public readonly bool IsFocusHit;   // 是否命中当前聚焦方向
    public readonly bool IsRecipe;     // 是否为配方（true=配方，false=物品）
    public readonly bool WasHardPity;  // 是否由硬保底直接产出
    public readonly bool HitFocusMainTarget; // 是否命中当前聚焦主目标

    public GachaResult(int itemId, GachaRarity rarity, bool isFocusHit, bool isRecipe, bool wasHardPity = false,
        bool hitFocusMainTarget = false) {
        ItemId = itemId;
        Rarity = rarity;
        IsFocusHit = isFocusHit;
        IsRecipe = isRecipe;
        WasHardPity = wasHardPity;
        HitFocusMainTarget = hitFocusMainTarget;
    }
}

/// <summary>卡池定义</summary>
public class GachaPool {
    /// <summary>
    /// 卡池ID语义合同：
    /// 0=开线池（抽到配方时走配方奖励逻辑）
    /// 1=原胚闭环池（抽到物品时入数据中心）
    /// 2=成长池（非随机，主要承载积分/定向补差）
    /// 3=流派聚焦层（不直接抽卡，只负责方向加权）
    /// </summary>
    public const int PoolIdOpeningLine = 0;
    public const int PoolIdProtoLoop = 1;
    public const int PoolIdGrowth = 2;
    public const int PoolIdFocus = 3;
    public const int PoolCount = 4;

    public readonly int PoolId;
    public readonly string NameKey;    // 翻译key

    // 各稀有度基础概率（0~1）
    public float RateC = 0.809f;
    public float RateB = 0.15f;
    public float RateA = 0.035f;
    public float RateS = 0.006f;

    // 各稀有度物品池（itemId列表）
    public List<int> PoolC = [];
    public List<int> PoolB = [];
    public List<int> PoolA = [];
    public List<int> PoolS = [];

    public GachaPool(int poolId, string nameKey) {
        PoolId = poolId;
        NameKey = nameKey;
    }

    /// <summary>统一的poolId有效性判断入口。</summary>
    public static bool IsValidPoolId(int poolId) {
        return poolId >= 0 && poolId < PoolCount;
    }

    /// <summary>仅开线池在发奖时走配方奖励逻辑。</summary>
    public static bool IsRecipePool(int poolId) {
        return poolId == PoolIdOpeningLine;
    }

    public static bool IsOpeningLinePool(int poolId) {
        return poolId == PoolIdOpeningLine;
    }

    public static bool IsProtoLoopPool(int poolId) {
        return poolId == PoolIdProtoLoop;
    }

    public static bool IsGrowthPool(int poolId) {
        return poolId == PoolIdGrowth;
    }

    public static bool IsFocusPool(int poolId) {
        return poolId == PoolIdFocus;
    }

    public static bool IsDrawPool(int poolId) {
        return poolId == PoolIdOpeningLine || poolId == PoolIdProtoLoop;
    }

    /// <summary>2.3 起抽卡只直耗当前阶段矩阵。</summary>
    public static bool CanUseTicket(int poolId, int ticketId) {
        if (!IsDrawPool(poolId)) {
            return false;
        }

        return ticketId == GetCurrentProgressMatrixId();
    }

    /// <summary>从对应稀有度池随机选一个物品</summary>
    public int PickRandom(GachaRarity rarity, Random rng) {
        var pool = rarity switch {
            GachaRarity.C => PoolC,
            GachaRarity.B => PoolB,
            GachaRarity.A => PoolA,
            GachaRarity.S => PoolS,
            _ => PoolC,
        };

        if (pool.Count == 0) return 0;
        return pool[rng.Next(pool.Count)];
    }
}
