using System;
using System.IO;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Progression;
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
    private static readonly long[] sacrificeTowerCounts = new long[SacrificeTowerTypeCount];
    private static readonly int[] sacrificeStepIndex = new int[SacrificeTowerTypeCount];

    public static long GetSacrificedTowerCount(int buildingId) {
        int index = FractionatorTowerCatalog.GetActiveFractionatorIndex(buildingId);
        return index >= 0 && index < sacrificeTowerCounts.Length
            ? sacrificeTowerCounts[index]
            : 0L;
    }

    public static int GetSacrificedTowerTypeCount() {
        int count = 0;
        for (int i = 0; i < sacrificeTowerCounts.Length; i++) {
            if (sacrificeTowerCounts[i] > 0) {
                count++;
            }
        }
        return count;
    }

    public static void AddSacrificedTowers(int buildingId, long count) {
        if (count <= 0) {
            return;
        }
        int index = FractionatorTowerCatalog.GetActiveFractionatorIndex(buildingId);
        if (index < 0 || index >= sacrificeTowerCounts.Length) {
            return;
        }
        sacrificeTowerCounts[index] = long.MaxValue - sacrificeTowerCounts[index] < count
            ? long.MaxValue
            : sacrificeTowerCounts[index] + count;
        if (InteractionTower.EnableSacrificeTrait) {
            UpdateSacrificeBoost();
        }
    }

    public static void AbsorbDataCenterFractionatorStock() {
        for (int i = 0; i < SacrificeTowerTypeCount; i++) {
            int itemId = FractionatorTowerCatalog.ActiveFractionatorBuildingIds[i];
            long count = GetModDataItemCount(itemId);
            if (count <= 0) {
                continue;
            }
            TakeItemFromModData(itemId, count, out _);
            TechManager.CheckTechUnlockCondition(itemId);
            AddSacrificedTowers(itemId, count);
        }
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
            ResetSacrificeBoostOnly();
            return;
        }
        UpdateSacrificeBoost();
    }

    private static void UpdateSacrificeBoost() {
        int buffCount = 0;
        long[] effectiveCounts = new long[SacrificeTowerTypeCount];
        for (int i = 0; i < effectiveCounts.Length; i++) {
            effectiveCounts[i] = sacrificeTowerCounts[i];
            if (effectiveCounts[i] > 0) {
                buffCount++;
            }
        }
        if (InteractionTower.EnableDimensionalResonance) {
            for (int i = 0; i < effectiveCounts.Length; i++) {
                effectiveCounts[i] = ScaleSacrificeCount(effectiveCounts[i], 1.0 + 0.1 * buffCount);
            }
        }
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
        Array.Clear(sacrificeTowerCounts, 0, sacrificeTowerCounts.Length);
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
        Array.Clear(sacrificeTowerCounts, 0, sacrificeTowerCounts.Length);
        r.ReadBlocks(
            ("TowerCounts", br => {
                int size = br.ReadInt32();
                for (int i = 0; i < size; i++) {
                    int index = br.ReadInt32();
                    long count = br.ReadInt64();
                    if (index >= 0 && index < sacrificeTowerCounts.Length) {
                        sacrificeTowerCounts[index] = Math.Max(0L, count);
                    }
                }
            })
        );
        if (InteractionTower.EnableSacrificeTrait) {
            UpdateSacrificeBoost();
        } else {
            ResetSacrificeBoostOnly();
        }
    }

    private static void SacrificeExport(BinaryWriter w) {
        w.WriteBlocks(
            ("TowerCounts", bw => {
                bw.Write(sacrificeTowerCounts.Length);
                for (int i = 0; i < sacrificeTowerCounts.Length; i++) {
                    bw.Write(i);
                    bw.Write(sacrificeTowerCounts[i]);
                }
            })
        );
    }
}
