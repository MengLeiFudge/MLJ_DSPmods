using System;
using System.IO;
using FE.Logic.Fractionation.Fractionators;
using HarmonyLib;
using UnityEngine;
using static FE.Logic.DataCenter.DataCenterInventory;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Process;

/// <summary>
/// 交互塔献祭加成与矩阵消耗逻辑。
/// </summary>
public static partial class ProcessManager {
    private const int SacrificeTowerTypeCount = 4;
    private const float SacrificeBoostStep = 0.05f;
    private const float SacrificeBoostCapTrait1 = 0.75f;
    private const float SacrificeBoostCapTrait2 = 1.00f;
    private static readonly long[] lastSacrificeTowerCounts = new long[SacrificeTowerTypeCount];
    private static readonly int[] sacrificeStepIndex = new int[SacrificeTowerTypeCount];

    public static long GetSacrificedTowerCount(int buildingId) {
        int index = FractionatorTowerCatalog.GetActiveFractionatorIndex(buildingId);
        return index >= 0 && index < lastSacrificeTowerCounts.Length
            ? lastSacrificeTowerCounts[index]
            : 0L;
    }

    public static int GetSacrificedTowerTypeCount() {
        int count = 0;
        for (int i = 0; i < lastSacrificeTowerCounts.Length; i++) {
            if (lastSacrificeTowerCounts[i] > 0) {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// 交互塔特质
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameMain), nameof(GameMain.FixedUpdate))]
    public static void GameData_FixedUpdate_Postfix() {
        if (DSPGame.IsMenuDemo || GameMain.mainPlayer == null) {
            return;
        }
        if (GameMain.gameTick % 60 != 3) {
            return;
        }
        if (!InteractionTower.EnableSacrificeTrait) {
            ResetSacrificeBoostState();
            return;
        }
        int buffCount = 0;
        long[] effectiveCounts = new long[SacrificeTowerTypeCount];
        for (int i = 0; i < SacrificeTowerTypeCount; i++) {
            int itemId = FractionatorTowerCatalog.ActiveFractionatorBuildingIds[i];
            long sacrificedCount = Take10PercentTower(itemId);
            lastSacrificeTowerCounts[i] = sacrificedCount;
            effectiveCounts[i] = sacrificedCount;
            if (sacrificedCount > 0) {
                buffCount++;
            }
        }
        if (InteractionTower.EnableDimensionalResonance) {
            for (int i = 0; i < effectiveCounts.Length; i++) {
                effectiveCounts[i] = ScaleSacrificeCount(effectiveCounts[i], 1.0 + 0.1 * buffCount);
            }
        }
        UpdateSacrificeBoost(effectiveCounts);
    }

    private static void UpdateSacrificeBoost(long[] effectiveCounts) {
        float boostCap = InteractionTower.EnableDimensionalResonance
            ? SacrificeBoostCapTrait2
            : SacrificeBoostCapTrait1;
        for (int i = 0; i < SacrificeTowerTypeCount; i++) {
            float rawBoost = Mathf.Sqrt(effectiveCounts[i]) / 10.0f;
            float clampedBoost = Mathf.Min(rawBoost, boostCap);
            sacrificeStepIndex[i] = Math.Max(0, Mathf.FloorToInt(clampedBoost / SacrificeBoostStep));
        }

        InteractionTower.SuccessBoost = sacrificeStepIndex[0] * SacrificeBoostStep;
        MineralReplicationTower.SuccessBoost = sacrificeStepIndex[1] * SacrificeBoostStep;
        ConversionTower.SuccessBoost = sacrificeStepIndex[2] * SacrificeBoostStep;
        RectificationTower.SuccessBoost = sacrificeStepIndex[3] * SacrificeBoostStep;
        RefreshFractionatorRuntimeConfig();
    }

    private static long ScaleSacrificeCount(long count, double scale) {
        if (count <= 0) {
            return 0L;
        }
        double scaled = count * scale;
        return scaled >= long.MaxValue ? long.MaxValue : (long)scaled;
    }

    private static void ResetSacrificeBoostState() {
        Array.Clear(lastSacrificeTowerCounts, 0, lastSacrificeTowerCounts.Length);
        ResetSacrificeBoostOnly();
    }

    private static void ResetSacrificeBoostOnly() {
        Array.Clear(sacrificeStepIndex, 0, sacrificeStepIndex.Length);
        InteractionTower.SuccessBoost = 0f;
        MineralReplicationTower.SuccessBoost = 0f;
        ConversionTower.SuccessBoost = 0f;
        RectificationTower.SuccessBoost = 0f;
        RefreshFractionatorRuntimeConfig();
    }

    private static void SacrificeImport(BinaryReader r) {
        r.ReadBlocks(
            ("TowerCounts", br => {
                int size = br.ReadInt32();
                for (int i = 0; i < size; i++) {
                    _ = br.ReadInt32();
                    _ = br.ReadInt64();
                }
            })
        );
        ResetSacrificeBoostState();
    }

    private static void SacrificeExport(BinaryWriter w) {
        w.WriteBlocks();
    }
}
