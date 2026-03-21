using System;
using System.Collections.Generic;

namespace FE.Logic.Manager;

/// <summary>稀有度等级</summary>
public enum GachaRarity { C, B, A, S }

/// <summary>单次抽卡结果</summary>
public readonly struct GachaResult {
    public readonly int ItemId;        // 物品ID（配方用inputID，物品用itemID）
    public readonly GachaRarity Rarity;
    public readonly bool IsUp;         // 是否为UP物品
    public readonly bool IsRecipe;     // 是否为配方（true=配方，false=物品）

    public GachaResult(int itemId, GachaRarity rarity, bool isUp, bool isRecipe) {
        ItemId = itemId;
        Rarity = rarity;
        IsUp = isUp;
        IsRecipe = isRecipe;
    }
}

/// <summary>卡池定义</summary>
public class GachaPool {
    public const int PoolIdPermanentRecipe = 0;
    public const int PoolIdPermanentBuilding = 1;
    public const int PoolIdUp = 2;
    public const int PoolIdLimited = 3;

    public readonly int PoolId;
    public readonly string NameKey;    // 翻译key
    public readonly bool RequiresPremiumTicket; // 是否需要精选券

    // 各稀有度基础概率（0~1）
    public float RateC = 0.80f;
    public float RateB = 0.15f;
    public float RateA = 0.045f;
    public float RateS = 0.005f;

    // 各稀有度物品池（itemId列表）
    public List<int> PoolC = [];
    public List<int> PoolB = [];
    public List<int> PoolA = [];
    public List<int> PoolS = [];

    // UP物品（仅UP池使用）
    public List<int> UpItems = [];

    public GachaPool(int poolId, string nameKey, bool requiresPremium) {
        PoolId = poolId;
        NameKey = nameKey;
        RequiresPremiumTicket = requiresPremium;
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
