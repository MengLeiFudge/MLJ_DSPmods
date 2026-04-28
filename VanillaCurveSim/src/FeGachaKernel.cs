using System;
using System.Collections.Generic;

namespace VanillaCurveSim;

internal sealed class FeGachaKernel {
    private enum FePoolId {
        OpeningLine = 0,
        ProtoLoop = 1,
    }

    private enum RewardToken {
        Fragments,
        GrowthFragments50,
        GrowthCurrentStageMatrix4,
        PreviousStageRecipe,
        CurrentStageRecipe,
        LockedCurrentStageRecipe,
        InteractionEmbryo,
        MineralEmbryo,
        PointEmbryo,
        ConversionEmbryo,
        RectificationEmbryo,
        FocusedEmbryo,
        DirectionalEmbryo,
    }

    private sealed class PoolDefinition {
        public List<RewardToken> PoolC { get; } = [];
        public List<RewardToken> PoolB { get; } = [];
        public List<RewardToken> PoolA { get; } = [];
        public List<RewardToken> PoolS { get; } = [];
        public double RateA { get; init; } = 0.035;
        public double RateB { get; init; } = 0.15;
        public double RateS { get; init; } = 0.006;
    }

    public void RunDraws(FractionationConfigSnapshot config, FeWarehouse warehouse, int stageIndex, int openingDraws,
        int protoDraws, Random rng, out double expectedSOpening, out double expectedSProto) {
        expectedSOpening = 0.0;
        expectedSProto = 0.0;

        PoolDefinition openingPool = BuildPool(config, FePoolId.OpeningLine, stageIndex);
        PoolDefinition protoPool = BuildPool(config, FePoolId.ProtoLoop, stageIndex);

        int openingPity = 0;
        for (int i = 0; i < openingDraws; i++) {
            if (!warehouse.TryConsumeMatrix(stageIndex, 1.0)) {
                break;
            }

            bool isS = DrawOne(config, warehouse, openingPool, FePoolId.OpeningLine, stageIndex, rng, ref openingPity);
            if (isS) {
                expectedSOpening += 1.0;
            }
        }

        int protoPity = 0;
        for (int i = 0; i < protoDraws; i++) {
            if (!warehouse.TryConsumeMatrix(stageIndex, 1.0)) {
                break;
            }

            bool isS = DrawOne(config, warehouse, protoPool, FePoolId.ProtoLoop, stageIndex, rng, ref protoPity);
            if (isS) {
                expectedSProto += 1.0;
            }
        }
    }

    public GrowthExchangeEstimate ExecuteGrowthPlan(FractionationConfigSnapshot config, FeWarehouse warehouse,
        int stageIndex) {
        double poolPointsBefore = warehouse.GrowthPoolPoints;
        double fragmentsBefore = warehouse.Fragments;
        double utilityBefore = warehouse.GetNormalizedUtility();
        int guard = 0;

        while (guard++ < 128) {
            GrowthOffer bestOffer = SelectBestOffer(config, warehouse, stageIndex);
            if (bestOffer == null) {
                break;
            }

            if (!warehouse.TryConsumePoolPoints(bestOffer.PointCost)) {
                break;
            }
            if (!warehouse.TryConsumeFragments(bestOffer.FragmentCost)) {
                warehouse.GrowthPoolPoints += bestOffer.PointCost;
                break;
            }

            ApplyReward(bestOffer.Token, warehouse, stageIndex);
        }

        double poolPointsConsumed = poolPointsBefore - warehouse.GrowthPoolPoints;
        double fragmentsConsumed = fragmentsBefore - warehouse.Fragments;
        double utilityDelta = warehouse.GetNormalizedUtility() - utilityBefore;
        return new GrowthExchangeEstimate {
            ConsumedPoolPoints = poolPointsConsumed,
            ConsumedFragments = fragmentsConsumed,
            GrowthUtility = utilityDelta,
        };
    }

    private bool DrawOne(FractionationConfigSnapshot config, FeWarehouse warehouse, PoolDefinition pool,
        FePoolId poolId,
        int stageIndex, Random rng, ref int pityCount) {
        double currentSRate = EstimateCurrentSRate(pityCount, pool.RateS);
        GachaRarity rarity = RollRarity(pool, currentSRate, pityCount >= 89, rng);
        RewardToken token = PickRewardToken(pool, rarity, rng);
        ApplyReward(token, warehouse, stageIndex);
        warehouse.GrowthPoolPoints += 1.0;

        bool isS = rarity == GachaRarity.S;
        pityCount = isS ? 0 : Math.Min(89, pityCount + 1);
        return isS;
    }

    private static RewardToken PickRewardToken(PoolDefinition pool, GachaRarity rarity, Random rng) {
        List<RewardToken> source = rarity switch {
            GachaRarity.C => pool.PoolC,
            GachaRarity.B => pool.PoolB,
            GachaRarity.A => pool.PoolA,
            GachaRarity.S => pool.PoolS,
            _ => pool.PoolC,
        };
        if (source.Count == 0) {
            return RewardToken.Fragments;
        }
        return source[rng.Next(source.Count)];
    }

    private static GachaRarity RollRarity(PoolDefinition pool, double currentSRate, bool forceS, Random rng) {
        if (forceS) {
            return GachaRarity.S;
        }

        double value = rng.NextDouble();
        if (value < currentSRate) {
            return GachaRarity.S;
        }

        value -= currentSRate;
        if (value < pool.RateA) {
            return GachaRarity.A;
        }

        value -= pool.RateA;
        if (value < pool.RateB) {
            return GachaRarity.B;
        }
        return GachaRarity.C;
    }

    private static double EstimateCurrentSRate(int pityCount, double baseRateS) {
        int nextDraw = pityCount + 1;
        if (nextDraw >= 90) {
            return 1.0;
        }
        if (nextDraw < 74) {
            return baseRateS;
        }
        int boostedDrawCount = nextDraw - 74 + 1;
        double rate = baseRateS + boostedDrawCount * 0.06;
        return rate > 1.0 ? 1.0 : rate;
    }

    private static PoolDefinition BuildPool(FractionationConfigSnapshot config, FePoolId poolId, int stageIndex) {
        var pool = new PoolDefinition();
        if (poolId == FePoolId.OpeningLine) {
            BuildOpeningPool(config, pool);
        } else {
            BuildProtoPool(config, pool);
        }
        return pool;
    }

    private static void BuildOpeningPool(FractionationConfigSnapshot config, PoolDefinition pool) {
        if (config.IsSpeedrun) {
            pool.PoolC.Add(RewardToken.CurrentStageRecipe);
            pool.PoolB.Add(RewardToken.CurrentStageRecipe);
            pool.PoolA.Add(RewardToken.CurrentStageRecipe);
            pool.PoolS.Add(RewardToken.LockedCurrentStageRecipe);
            return;
        }

        pool.PoolC.Add(RewardToken.Fragments);
        pool.PoolB.Add(RewardToken.PreviousStageRecipe);
        pool.PoolA.Add(RewardToken.CurrentStageRecipe);
        pool.PoolS.Add(RewardToken.LockedCurrentStageRecipe);
    }

    private static void BuildProtoPool(FractionationConfigSnapshot config, PoolDefinition pool) {
        if (config.IsSpeedrun) {
            RewardToken focused = GetFocusedEmbryoReward(config.Focus);
            pool.PoolC.Add(focused);
            pool.PoolB.Add(focused);
            pool.PoolA.Add(focused);
            pool.PoolA.Add(RewardToken.DirectionalEmbryo);
            pool.PoolS.Add(RewardToken.DirectionalEmbryo);
            pool.PoolS.Add(focused);
            return;
        }

        pool.PoolC.Add(RewardToken.InteractionEmbryo);
        pool.PoolC.Add(RewardToken.MineralEmbryo);
        pool.PoolB.Add(RewardToken.PointEmbryo);
        pool.PoolB.Add(RewardToken.InteractionEmbryo);
        pool.PoolA.Add(RewardToken.ConversionEmbryo);
        pool.PoolA.Add(RewardToken.RectificationEmbryo);
        pool.PoolS.Add(RewardToken.DirectionalEmbryo);
        pool.PoolS.Add(GetFocusedEmbryoReward(config.Focus));
    }

    private static RewardToken GetFocusedEmbryoReward(FeGachaFocus focus) {
        return focus switch {
            FeGachaFocus.MineralExpansion => RewardToken.MineralEmbryo,
            FeGachaFocus.ConversionLeap => RewardToken.ConversionEmbryo,
            FeGachaFocus.LogisticsInteraction => RewardToken.InteractionEmbryo,
            FeGachaFocus.EmbryoCycle => RewardToken.DirectionalEmbryo,
            FeGachaFocus.ProcessOptimization => RewardToken.PointEmbryo,
            FeGachaFocus.RectificationEconomy => RewardToken.RectificationEmbryo,
            _ => RewardToken.InteractionEmbryo,
        };
    }

    private static void ApplyReward(RewardToken token, FeWarehouse warehouse, int stageIndex) {
        switch (token) {
            case RewardToken.Fragments:
                warehouse.Fragments += 15.0;
                break;
            case RewardToken.GrowthFragments50:
                warehouse.Fragments += 50.0;
                break;
            case RewardToken.GrowthCurrentStageMatrix4:
                warehouse.AddMatrix(stageIndex, 4.0);
                break;
            case RewardToken.PreviousStageRecipe:
            case RewardToken.CurrentStageRecipe:
            case RewardToken.LockedCurrentStageRecipe:
                ResolveRecipeReward(warehouse);
                break;
            case RewardToken.InteractionEmbryo:
                warehouse.InteractionEmbryos += 1.0;
                break;
            case RewardToken.MineralEmbryo:
                warehouse.MineralEmbryos += 1.0;
                break;
            case RewardToken.PointEmbryo:
                warehouse.PointEmbryos += 1.0;
                break;
            case RewardToken.ConversionEmbryo:
                warehouse.ConversionEmbryos += 1.0;
                break;
            case RewardToken.RectificationEmbryo:
                warehouse.RectificationEmbryos += 1.0;
                break;
            case RewardToken.FocusedEmbryo:
                warehouse.PointEmbryos += 1.0;
                break;
            case RewardToken.DirectionalEmbryo:
                warehouse.DirectionalEmbryos += 1.0;
                break;
        }
    }

    private static void ResolveRecipeReward(FeWarehouse warehouse) {
        if (warehouse.OpeningLockedRecipes >= 1.0) {
            warehouse.OpeningLockedRecipes -= 1.0;
            warehouse.RecipeUnlockRewards += 1.0;
            warehouse.OpeningUpgradeableCharges += 2.0;
            return;
        }

        if (warehouse.OpeningUpgradeableCharges >= 1.0) {
            warehouse.OpeningUpgradeableCharges -= 1.0;
            warehouse.RecipeUpgradeRewards += 1.0;
            return;
        }

        warehouse.Fragments += 15.0;
    }

    private GrowthOffer SelectBestOffer(FractionationConfigSnapshot config, FeWarehouse warehouse, int stageIndex) {
        GrowthOffer[] offers = BuildGrowthOffers(config, stageIndex);
        GrowthOffer bestOffer = null;
        double bestRatio = 0.0;
        foreach (GrowthOffer offer in offers) {
            if (warehouse.GrowthPoolPoints + 1e-6 < offer.PointCost
                || warehouse.Fragments + 1e-6 < offer.FragmentCost) {
                continue;
            }
            double ratio = offer.Utility / Math.Max(1.0, offer.PointCost + offer.FragmentCost * 0.15);
            if (ratio > bestRatio) {
                bestRatio = ratio;
                bestOffer = offer;
            }
        }
        return bestOffer;
    }

    private static GrowthOffer[] BuildGrowthOffers(FractionationConfigSnapshot config, int stageIndex) {
        bool speedrun = config.IsSpeedrun;
        bool focused = config.Focus != FeGachaFocus.Balanced;
        double discount = focused ? FeReference.GetFocusedOfferDiscountFactor(speedrun) : 1.0;
        RewardToken focusedEmbryo = GetFocusedEmbryoReward(config.Focus);

        return [
            new GrowthOffer(5 * discount, 0, RewardToken.GrowthFragments50, 50 * 0.02 + (focused ? 0.2 : 0.0)),
            new GrowthOffer(10 * discount, 10 * discount, RewardToken.GrowthCurrentStageMatrix4,
                (4.0 + stageIndex * 0.2)),
            new GrowthOffer(20 * discount, 15 * discount, focusedEmbryo, speedrun ? 7.0 : 6.0),
            new GrowthOffer(36 * discount, 30 * discount, RewardToken.DirectionalEmbryo, speedrun ? 8.5 : 8.0),
        ];
    }

    private sealed class GrowthOffer {
        public GrowthOffer(double pointCost, double fragmentCost, RewardToken token, double utility) {
            PointCost = pointCost;
            FragmentCost = fragmentCost;
            Token = token;
            Utility = utility;
        }

        public double PointCost { get; }
        public double FragmentCost { get; }
        public RewardToken Token { get; }
        public double Utility { get; }
    }
}
