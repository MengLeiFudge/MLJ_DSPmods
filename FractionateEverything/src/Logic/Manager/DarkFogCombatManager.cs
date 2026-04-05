using System.Linq;
using FE.Compatibility;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public enum EDarkFogCombatStage {
    Dormant = 0,
    Signal = 1,
    GroundSuppression = 2,
    StellarHunt = 3,
    Singularity = 4,
}

/// <summary>
/// 黑雾战斗支线统一状态聚合层。
/// 这里只负责读取原版战斗状态与可选的 TCFV 增强层状态，不直接发奖励或输出 UI 文案。
/// </summary>
public static class DarkFogCombatManager {
    private readonly struct Snapshot(
        bool combatMode,
        bool baseLayerEnabled,
        bool enhancedLayerEnabled,
        int progressStageIndex,
        int groundBaseCount,
        int hiveCount,
        int resourceTier,
        long resourceScore,
        int relicCount,
        int meritRank,
        int assignedSkillPoints,
        bool hasActiveEvent,
        int enhancedNodeCount,
        EDarkFogCombatStage stage) {
        public readonly bool CombatMode = combatMode;
        public readonly bool BaseLayerEnabled = baseLayerEnabled;
        public readonly bool EnhancedLayerEnabled = enhancedLayerEnabled;
        public readonly int ProgressStageIndex = progressStageIndex;
        public readonly int GroundBaseCount = groundBaseCount;
        public readonly int HiveCount = hiveCount;
        public readonly int ResourceTier = resourceTier;
        public readonly long ResourceScore = resourceScore;
        public readonly int RelicCount = relicCount;
        public readonly int MeritRank = meritRank;
        public readonly int AssignedSkillPoints = assignedSkillPoints;
        public readonly bool HasActiveEvent = hasActiveEvent;
        public readonly int EnhancedNodeCount = enhancedNodeCount;
        public readonly EDarkFogCombatStage Stage = stage;
    }

    private static int cachedFrame = -1;
    private static long cachedGameTick = long.MinValue;
    private static Snapshot cachedSnapshot;
    private static bool groundBaseObserved;
    private static bool hiveObserved;

    private static Snapshot GetSnapshot() {
        int frame = Time.frameCount;
        long gameTick = GameMain.gameTick;
        if (cachedFrame == frame || gameTick >= 0 && cachedGameTick >= 0 && gameTick - cachedGameTick < 60) {
            return cachedSnapshot;
        }

        int progressStageIndex = ItemManager.GetCurrentProgressStageIndex();
        bool combatMode = GameMain.logic?.isCombatMode ?? GameMain.data?.gameDesc?.isCombatMode ?? false;
        bool baseLayerEnabled = combatMode && progressStageIndex >= 3;
        int groundBaseCount = CountAliveGroundBases();
        int hiveCount = CountAliveHives();
        if (groundBaseCount > 0) {
            groundBaseObserved = true;
        }
        if (hiveCount > 0) {
            hiveObserved = true;
        }
        int resourceTier = GetUnlockedDarkFogResourceTier();
        long resourceScore = GetCurrentDarkFogInventoryScore();
        bool enhancedLayerEnabled = baseLayerEnabled && TheyComeFromVoid.Enable;
        int relicCount = TheyComeFromVoid.GetRelicCount();
        int meritRank = TheyComeFromVoid.GetMeritRank();
        int assignedSkillPoints = TheyComeFromVoid.GetAssignedSkillPointCount();
        bool hasActiveEvent = TheyComeFromVoid.HasActiveEventChain();
        int enhancedNodeCount = 0;
        if (relicCount > 0) {
            enhancedNodeCount++;
        }
        if (meritRank >= 4) {
            enhancedNodeCount++;
        }
        if (assignedSkillPoints >= 8) {
            enhancedNodeCount++;
        }
        if (hasActiveEvent) {
            enhancedNodeCount++;
        }

        EDarkFogCombatStage stage = DetermineStage(baseLayerEnabled, progressStageIndex, resourceTier,
            enhancedLayerEnabled, enhancedNodeCount);
        cachedSnapshot = new Snapshot(combatMode, baseLayerEnabled, enhancedLayerEnabled, progressStageIndex,
            groundBaseCount, hiveCount, resourceTier, resourceScore, relicCount, meritRank, assignedSkillPoints,
            hasActiveEvent, enhancedNodeCount, stage);
        cachedFrame = frame;
        cachedGameTick = gameTick;
        return cachedSnapshot;
    }

    private static EDarkFogCombatStage DetermineStage(bool baseLayerEnabled, int progressStageIndex, int resourceTier,
        bool enhancedLayerEnabled, int enhancedNodeCount) {
        if (!baseLayerEnabled) {
            return EDarkFogCombatStage.Dormant;
        }

        EDarkFogCombatStage stage = EDarkFogCombatStage.Signal;
        bool reachedGround = progressStageIndex >= 4
                             && (resourceTier >= 2 || groundBaseObserved);
        if (!reachedGround) {
            return stage;
        }

        stage = EDarkFogCombatStage.GroundSuppression;
        bool reachedStellar = progressStageIndex >= 5
                              && (resourceTier >= 3 || hiveObserved);
        if (!reachedStellar) {
            return stage;
        }

        stage = EDarkFogCombatStage.StellarHunt;
        bool reachedSingularity = resourceTier >= 4
                                  || enhancedLayerEnabled && enhancedNodeCount >= 2;
        return reachedSingularity ? EDarkFogCombatStage.Singularity : stage;
    }

    private static int CountAliveGroundBases() {
        if (GameMain.data?.factories == null) {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < GameMain.data.factoryCount; i++) {
            PlanetFactory factory = GameMain.data.factories[i];
            if (factory?.enemySystem?.bases == null) {
                continue;
            }

            var bases = factory.enemySystem.bases;
            for (int baseIndex = 1; baseIndex < bases.cursor; baseIndex++) {
                DFGBaseComponent current = bases.buffer[baseIndex];
                if (current != null && current.id == baseIndex) {
                    count++;
                }
            }
        }
        return count;
    }

    private static int CountAliveHives() {
        if (GameMain.data?.spaceSector?.dfHives == null) {
            return 0;
        }

        int count = 0;
        EnemyDFHiveSystem[] hives = GameMain.data.spaceSector.dfHives;
        for (int i = 0; i < hives.Length; i++) {
            for (EnemyDFHiveSystem current = hives[i]; current != null; current = current.nextSibling) {
                count++;
            }
        }
        return count;
    }

    private static int GetUnlockedDarkFogResourceTier() {
        if (IsDarkFogItemUnlocked(I核心素)) {
            return 4;
        }
        if (IsDarkFogItemUnlocked(I负熵奇点)) {
            return 3;
        }
        if (IsDarkFogItemUnlocked(I物质重组器) || IsDarkFogItemUnlocked(I硅基神经元)) {
            return 2;
        }
        if (IsDarkFogItemUnlocked(I黑雾矩阵) || IsDarkFogItemUnlocked(I能量碎片)) {
            return 1;
        }
        return 0;
    }

    private static bool IsDarkFogItemUnlocked(int itemId) {
        return (GameMain.history != null && GameMain.history.ItemUnlocked(itemId)) || GetItemTotalCount(itemId) > 0;
    }

    public static long GetCurrentDarkFogInventoryScore() {
        long score = 0;
        score += GetItemTotalCount(I黑雾矩阵);
        score += GetItemTotalCount(I能量碎片) / 4;
        score += GetItemTotalCount(I物质重组器) * 2;
        score += GetItemTotalCount(I硅基神经元) * 3;
        score += GetItemTotalCount(I负熵奇点) * 5;
        score += GetItemTotalCount(I核心素) * 10;
        return score;
    }

    public static bool IsCombatModeEnabled() => GetSnapshot().CombatMode;
    public static bool IsBaseLayerEnabled() => GetSnapshot().BaseLayerEnabled;
    public static bool IsEnhancedLayerEnabled() => GetSnapshot().EnhancedLayerEnabled;
    public static int GetProgressStageIndex() => GetSnapshot().ProgressStageIndex;
    public static int GetAliveGroundBaseCount() => GetSnapshot().GroundBaseCount;
    public static int GetAliveHiveCount() => GetSnapshot().HiveCount;
    public static int GetDarkFogResourceTier() => GetSnapshot().ResourceTier;
    public static int GetRelicCount() => GetSnapshot().RelicCount;
    public static int GetMeritRank() => GetSnapshot().MeritRank;
    public static int GetAssignedSkillPointCount() => GetSnapshot().AssignedSkillPoints;
    public static bool HasActiveEventChain() => GetSnapshot().HasActiveEvent;
    public static int GetEnhancedNodeCount() => GetSnapshot().EnhancedNodeCount;
    public static EDarkFogCombatStage GetCurrentStage() => GetSnapshot().Stage;
    public static bool IsGrowthOfferUnlocked() => GetCurrentStage() >= EDarkFogCombatStage.Signal;
    public static bool IsSpecialOrderUnlocked() => GetCurrentStage() >= EDarkFogCombatStage.GroundSuppression;

    public static int GetUnlockedGrowthOfferCount() {
        return GachaService.GetGrowthOffers().Count(offer => offer.ExtraCostItemId == I黑雾矩阵);
    }

    public static int GetUnlockedSpecialOrderCount() {
        return MarketBoardManager.ActiveOffers.Count(IsDarkFogOffer);
    }

    public static bool IsDarkFogOffer(MarketBoardManager.MarketOffer offer) {
        return offer.InputItemId == I黑雾矩阵
               || offer.ExtraInputItemId == I黑雾矩阵
               || offer.OutputItemId == I黑雾矩阵
               || offer.OutputItemId == I能量碎片
               || offer.OutputItemId == I物质重组器
               || offer.OutputItemId == I硅基神经元
               || offer.OutputItemId == I负熵奇点
               || offer.OutputItemId == I核心素
               || offer.OutputItemId == IFE分馏配方核心
               || offer.OutputItemId == IFE分馏塔定向原胚;
    }

    public static bool IsEnhancedDarkFogOffer(MarketBoardManager.MarketOffer offer) {
        return IsDarkFogOffer(offer) && IsEnhancedRewardItem(offer.OutputItemId);
    }

    public static bool IsEnhancedRewardItem(int itemId) {
        return itemId == IFE分馏配方核心 || itemId == IFE分馏塔定向原胚;
    }

    public static int GetNextUnlockMatrixId() {
        return GetCurrentStage() switch {
            EDarkFogCombatStage.Dormant => I信息矩阵,
            EDarkFogCombatStage.Signal => I引力矩阵,
            EDarkFogCombatStage.GroundSuppression => I宇宙矩阵,
            _ => 0,
        };
    }

    public static int GetNextRequiredResourceTier() {
        return GetCurrentStage() switch {
            EDarkFogCombatStage.Dormant => 1,
            EDarkFogCombatStage.Signal => 2,
            EDarkFogCombatStage.GroundSuppression => 3,
            EDarkFogCombatStage.StellarHunt => 4,
            _ => 4,
        };
    }

    public static int GetNextRequiredEnhancedNodeCount() {
        return GetCurrentStage() == EDarkFogCombatStage.StellarHunt && IsEnhancedLayerEnabled() ? 2 : 0;
    }

    public static void IntoOtherSave() {
        cachedFrame = -1;
        cachedGameTick = long.MinValue;
        cachedSnapshot = default;
        groundBaseObserved = false;
        hiveObserved = false;
    }
}
