using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using FE.Logic.Building;
using FE.Logic.Recipe;
using FE.Logic.RecipeGrowth;
using FE.UI.MainPanel.ProgressTask;
using HarmonyLib;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static partial class ProcessManager {
    private const int SacrificeTowerTypeCount = IFE精馏塔 - IFE交互塔 + 1;
    private const float SacrificeBoostStep = 0.05f;
    private const float SacrificeBoostCapTrait1 = 0.75f;
    private const float SacrificeBoostCapTrait2 = 1.00f;
    private static readonly int[] sacrificeStepIndex = new int[SacrificeTowerTypeCount];

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
        int[] takeCounts = new int[SacrificeTowerTypeCount];
        for (int i = 0; i < takeCounts.Length; i++) {
            takeCounts[i] = Take10PercentTower(IFE交互塔 + i);
            if (takeCounts[i] > 0) {
                buffCount++;
            }
        }
        if (InteractionTower.EnableDimensionalResonance) {
            for (int i = 0; i < takeCounts.Length; i++) {
                takeCounts[i] = (int)(takeCounts[i] * (1 + 0.1 * buffCount));
            }
        }
        UpdateSacrificeBoost(takeCounts);
    }

    private static void UpdateSacrificeBoost(int[] takeCounts) {
        float boostCap = InteractionTower.EnableDimensionalResonance
            ? SacrificeBoostCapTrait2
            : SacrificeBoostCapTrait1;
        for (int i = 0; i < SacrificeTowerTypeCount; i++) {
            float rawBoost = Mathf.Sqrt(takeCounts[i]) / 10.0f;
            float clampedBoost = Mathf.Min(rawBoost, boostCap);
            sacrificeStepIndex[i] = Math.Max(0, Mathf.FloorToInt(clampedBoost / SacrificeBoostStep));
        }

        InteractionTower.SuccessBoost = sacrificeStepIndex[0] * SacrificeBoostStep;
        MineralReplicationTower.SuccessBoost = sacrificeStepIndex[1] * SacrificeBoostStep;
        PointAggregateTower.SuccessBoost = sacrificeStepIndex[2] * SacrificeBoostStep;
        ConversionTower.SuccessBoost = sacrificeStepIndex[3] * SacrificeBoostStep;
        RectificationTower.SuccessBoost = sacrificeStepIndex[4] * SacrificeBoostStep;
        RefreshFractionatorRuntimeConfig();
    }

    private static void ResetSacrificeBoostState() {
        Array.Clear(sacrificeStepIndex, 0, sacrificeStepIndex.Length);
        InteractionTower.SuccessBoost = 0f;
        MineralReplicationTower.SuccessBoost = 0f;
        PointAggregateTower.SuccessBoost = 0f;
        ConversionTower.SuccessBoost = 0f;
        RectificationTower.SuccessBoost = 0f;
        RefreshFractionatorRuntimeConfig();
    }

}
