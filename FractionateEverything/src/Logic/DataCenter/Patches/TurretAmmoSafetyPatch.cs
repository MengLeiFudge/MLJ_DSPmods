using System;
using HarmonyLib;
using static FE.Utils.Utils;

namespace FE.Logic.DataCenter.Patches;

/// <summary>
/// 炮台弹药需求数组访问前的安全补齐补丁。
/// </summary>
public static class TurretAmmoSafetyPatch {
    /// <summary>
    /// 兼容旧版炮台窗口与战斗模组对 turretNeeds 的直接索引访问。
    /// 某些炮台（玩家反馈为电浆塔）在异常 ammoType/空需求数组状态下会让原版 UI 直接越界，
    /// 因此在窗口打开、刷新、手动补弹前，先把当前 ammoType 对应的槽位补齐到可安全读取的长度。
    /// </summary>
    private static void EnsureTurretNeedsSafe(UITurretWindow turretWindow) {
        if (turretWindow == null || turretWindow.turretId <= 0 || turretWindow.defenseSystem == null) {
            return;
        }

        var turrets = turretWindow.defenseSystem.turrets;
        if (turrets.buffer == null || turretWindow.turretId >= turrets.buffer.Length) {
            return;
        }

        ref TurretComponent turret = ref turrets.buffer[turretWindow.turretId];
        if (turret.id != turretWindow.turretId) {
            return;
        }

        int ammoType = (int)turret.ammoType;
        if (ammoType < 0) {
            LogWarning($"UITurretWindow 检测到非法 ammoType={ammoType}，已回退为 0。");
            turret.ammoType = 0;
            ammoType = 0;
        }

        if (ItemProto.turretNeeds == null) {
            ItemProto.turretNeeds = new int[Math.Max(16, ammoType + 1)][];
        } else if (ammoType >= ItemProto.turretNeeds.Length) {
            Array.Resize(ref ItemProto.turretNeeds, Math.Max(ItemProto.turretNeeds.Length * 2, ammoType + 1));
        }

        int[] needs = ItemProto.turretNeeds[ammoType];
        if (needs != null && needs.Length >= PackageAccessRules.MinTurretAmmoNeedCount) {
            return;
        }

        int[] padded = new int[PackageAccessRules.MinTurretAmmoNeedCount];
        if (needs != null && needs.Length > 0) {
            Array.Copy(needs, padded, needs.Length);
        }
        ItemProto.turretNeeds[ammoType] = padded;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UITurretWindow), nameof(UITurretWindow.OnTurretIdChange))]
    [HarmonyPatch(typeof(UITurretWindow), nameof(UITurretWindow._OnUpdate))]
    [HarmonyPatch(typeof(UITurretWindow), nameof(UITurretWindow.OnHandFillAmmoButtonClick))]
    private static void UITurretWindow_EnsureTurretNeedsSafe(UITurretWindow __instance) {
        EnsureTurretNeedsSafe(__instance);
    }
}
