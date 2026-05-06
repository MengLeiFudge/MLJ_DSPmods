using System;
using FE.Logic.Fractionation.Growth;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Logic.Progression;

/// <summary>
/// 添加科技后，需要Preload、Preload2。
/// Preload2会初始化unlockRecipeArray，之后LDBTool添加就不会报空指针异常。
/// </summary>
public static partial class TechManager {
    private static readonly bool[] techUnlockFlags = new bool[7];

    public static void ResetTechUnlockFlags() {
        Array.Clear(techUnlockFlags, 0, techUnlockFlags.Length);
        pendingLoadTimeRecipeBaselineApply = false;
    }

    public static void CheckTechUnlockCondition(int itemId) {
        if (itemId >= IFE交互塔 && itemId <= IFE精馏塔) {
            techUnlockFlags[itemId - IFE交互塔] = true;
        }
    }

    /// <summary>
    /// 对于所有解锁标记为true的分馏塔，解锁对应科技。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.GameTick))]
    public static void Player_GameTick_Postfix() {
        for (int i = 0; i < techUnlockFlags.Length; i++) {
            if (techUnlockFlags[i]) {
                if (!GameMain.history.TechUnlocked(TFE物品交互 + i)) {
                    GameMain.history.UnlockTechUnlimited(TFE物品交互 + i, false);
                } else {
                    techUnlockFlags[i] = false;
                }
            }
        }

        TryApplyLoadTimeRecipeBaselines();

        RecipeGrowthManager.SyncRuntimeUnlocks();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TechProto), nameof(TechProto.UnlockFunctionText))]
    public static bool TechProto_UnlockFunctionText_Prefix(ref TechProto __instance, ref string __result) {
        if (__instance.ID == TFE分馏数据中心) {
            __result = $"{"允许连接到分馏数据中心".Translate()}\r\n"
                       + $"{"给予一些分馏塔原胚".Translate()}";
            return false;
        }
        if (__instance.ID >= TFE超值礼包1 && __instance.ID <= TFE超值礼包9) {
            __result = $"{"一个物超所值的礼包".Translate()}";
            return false;
        }
        if (__instance.ID == TFE分馏塔原胚) {
            __result = $"{"解锁全部建筑培养配方".Translate()}\r\n"
                       + $"{"给予一个交互塔".Translate()}\r\n"
                       + $"{"给予一些分馏塔原胚".Translate()}";
            return false;
        }
        if (__instance.ID == TFE物品交互) {
            __result = $"{"自动上传被扔掉的物品".Translate()}\r\n"
                       + $"{"双击背包排序按钮，自动上传背包内物品".Translate()}";
            return false;
        }
        if (__instance.ID == TFE矿物复制) {
            __result = $"{"解锁部分矿物复制配方".Translate()}";
            return false;
        }
        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.NotifyTechUnlock))]
    public static void GameHistoryData_NotifyTechUnlock_Postfix(int _techId) {
        if (_techId == TFE分馏塔原胚) {
            EnsureBuildingTrainRecipeBaseline();
        } else if (_techId == TFE矿物复制) {
            EnsureInitialMineralCopyRecipeBaseline();
        } else if (_techId == TFE物品精馏) {
            EnsureRectificationRecipeBaseline();
        }

        EnsureGuaranteedConversionRecipeBaselines();
    }
}
