using System;
using FE.Logic.Fractionation.Fractionators;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Logic.Progression;

/// <summary>
/// 添加科技后，需要Preload、Preload2。
/// Preload2会初始化unlockRecipeArray，之后LDBTool添加就不会报空指针异常。
/// </summary>
public static partial class TechManager {
    private static readonly bool[] techUnlockFlags = new bool[4];

    public static void ResetTechUnlockFlags() {
        Array.Clear(techUnlockFlags, 0, techUnlockFlags.Length);
    }

    public static void CheckTechUnlockCondition(int itemId) {
        int index = FractionatorTowerCatalog.GetActiveFractionatorIndex(itemId);
        if (index >= 0) {
            techUnlockFlags[index] = true;
        }
    }

    /// <summary>
    /// 对于所有恢复标记为 true 的分馏塔，恢复对应旧文明协议。
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.GameTick))]
    public static void Player_GameTick_Postfix() {
        for (int i = 0; i < techUnlockFlags.Length; i++) {
            if (techUnlockFlags[i]) {
                int techId = GetActiveFractionatorUnlockTechId(i);
                if (techId <= 0) {
                    techUnlockFlags[i] = false;
                    continue;
                }
                if (!GameMain.history.TechUnlocked(techId)) {
                    GameMain.history.UnlockTechUnlimited(techId, false);
                    CivilizationRecoveryManager.ShowProtocolRecoveredTip(techId);
                    techUnlockFlags[i] = false;
                } else {
                    techUnlockFlags[i] = false;
                }
            }
        }

        StackingManager.SyncRuntimeState();
        CivilizationRecoveryManager.Tick();
    }

    private static int GetActiveFractionatorUnlockTechId(int index) {
        return index switch {
            0 => TFE物品交互,
            1 => TFE矿物复制,
            2 => TFE物品转化,
            3 => TFE物品精馏,
            _ => 0,
        };
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TechProto), nameof(TechProto.UnlockFunctionText))]
    public static bool TechProto_UnlockFunctionText_Prefix(ref TechProto __instance, ref string __result) {
        if (__instance.ID == TFE分馏数据中心) {
            __result = $"{"允许连接到分馏数据中心".Translate()}\r\n"
                       + $"{"给予一些分馏塔原胚".Translate()}";
            return false;
        }
        if (__instance.ID >= TFE超值礼包1 && __instance.ID <= TFE超值礼包6) {
            __result = $"{"一个物超所值的礼包".Translate()}";
            return false;
        }
        if (__instance.ID == TFE分馏塔原胚) {
            __result = $"{"恢复全部建筑培养配方".Translate()}\r\n"
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
            __result = $"{"恢复部分矿物复制配方".Translate()}";
            return false;
        }
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIGeneralTips), nameof(UIGeneralTips.OnTechUnlocked))]
    public static bool UIGeneralTips_OnTechUnlocked_Prefix(int techId) {
        return !CivilizationRecoveryManager.IsInternalRecoveryTech(techId);
    }
}
