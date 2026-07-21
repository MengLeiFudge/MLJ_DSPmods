using System;
using System.IO;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.Logic.Progression;

/// <summary>
/// 将旧 FE 科技树入口迁移为文明恢复流程。
/// 旧 TFE 科技 ID 继续作为存档、配方解锁和兼容状态使用，但不再作为玩家可研究的主脑科技树节点展示。
/// </summary>
public static class CivilizationRecoveryManager {
    private const long StarterSignalStartTick = 720L;
    private const long StarterSignalTipIntervalTicks = 1800L;
    private const float StarterSignalDistance = 160f;
    private const float StarterSignalRecoverRadius = 35f;

    private static readonly int[] matrixStageTechIds = [
        T电磁矩阵,
        T能量矩阵,
        T结构矩阵,
        T信息矩阵,
        T引力矩阵,
        T宇宙矩阵,
    ];

    private static readonly int[] matrixStageSupplyTechIds = [
        TFE阶段补给1,
        TFE阶段补给2,
        TFE阶段补给3,
        TFE阶段补给4,
        TFE阶段补给5,
        TFE阶段补给6,
    ];

    private static readonly int[] logisticsInteractionTechIds = [
        TFE行星内物流交互,
        TFE星际物流交互,
    ];

    private static readonly int[] vanillaLogisticsTechIds = [
        T行星物流系统,
        T星际物流系统,
    ];

    private static readonly int[] internalRecoveryTechIds = [
        TFE分馏数据中心,
        TFE分馏塔原胚,
        TFE物品交互,
        TFE文明解析,
        TFE资源复制,
        TFE物品转化,
        TFE行星内物流交互,
        TFE星际物流交互,
        TFE阶段补给1,
        TFE阶段补给2,
        TFE阶段补给3,
        TFE阶段补给4,
        TFE阶段补给5,
        TFE阶段补给6,
    ];

    private static readonly bool[] matrixStageSupplyRecovered = new bool[matrixStageSupplyTechIds.Length];
    private static readonly bool[] logisticsInteractionRecovered = new bool[logisticsInteractionTechIds.Length];
    private static bool starterKitRecovered;
    private static bool starterSignalNotified;
    private static long nextStarterSignalTipTick;

    public static bool HasDataCenterCommunication =>
        GameMain.history != null
        && (starterKitRecovered || GameMain.history.TechUnlocked(TFE分馏数据中心, true));

    public static void SuppressInternalTechTreeEntries() {
        foreach (int techId in internalRecoveryTechIds) {
            TechProto tech = LDB.techs.Select(techId);
            if (tech == null) {
                continue;
            }

            tech.IsHiddenTech = true;
            tech.IsObsolete = true;
            tech.IsLabTech = false;
        }
    }

    public static bool IsInternalRecoveryTech(int techId) {
        foreach (int internalTechId in internalRecoveryTechIds) {
            if (internalTechId == techId) {
                return true;
            }
        }
        return false;
    }

    public static void Tick() {
        if (GameMain.history == null || GameMain.mainPlayer == null) {
            return;
        }

        RecoverStarterKit();
        RecoverMatrixStageSupplies();
        RecoverLogisticsInteractionProtocols();
    }

    public static void Import(BinaryReader r) {
        IntoOtherSave();
        r.ReadBlocks(
            ("StarterKitRecovered", br => starterKitRecovered = br.ReadBoolean()),
            ("StarterSignalNotified", br => starterSignalNotified = br.ReadBoolean()),
            ("MatrixStageSupplyRecovered", br => {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++) {
                    bool recovered = br.ReadBoolean();
                    if (i < matrixStageSupplyRecovered.Length) {
                        matrixStageSupplyRecovered[i] = recovered;
                    }
                }
            }),
            ("LogisticsInteractionRecovered", br => {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++) {
                    bool recovered = br.ReadBoolean();
                    if (i < logisticsInteractionRecovered.Length) {
                        logisticsInteractionRecovered[i] = recovered;
                    }
                }
            })
        );
        SyncRecoveredFlagsFromHistory();
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("StarterKitRecovered", bw => bw.Write(starterKitRecovered)),
            ("StarterSignalNotified", bw => bw.Write(starterSignalNotified)),
            ("MatrixStageSupplyRecovered", bw => {
                bw.Write(matrixStageSupplyRecovered.Length);
                foreach (bool recovered in matrixStageSupplyRecovered) {
                    bw.Write(recovered);
                }
            }),
            ("LogisticsInteractionRecovered", bw => {
                bw.Write(logisticsInteractionRecovered.Length);
                foreach (bool recovered in logisticsInteractionRecovered) {
                    bw.Write(recovered);
                }
            })
        );
    }

    public static void IntoOtherSave() {
        starterKitRecovered = false;
        starterSignalNotified = false;
        nextStarterSignalTipTick = 0L;
        Array.Clear(matrixStageSupplyRecovered, 0, matrixStageSupplyRecovered.Length);
        Array.Clear(logisticsInteractionRecovered, 0, logisticsInteractionRecovered.Length);
    }

    public static void ShowProtocolRecoveredTip(int techId) {
        ShowRecoveryTip(techId is TFE行星内物流交互 or TFE星际物流交互
            ? "物流交互协议恢复提示"
            : "旧文明协议恢复提示");
    }

    private static void RecoverStarterKit() {
        if (starterKitRecovered) {
            return;
        }

        bool hasDataCenter = IsTechRecovered(TFE分馏数据中心);
        bool hasProtoKit = IsTechRecovered(TFE分馏塔原胚);
        if (hasDataCenter || hasProtoKit) {
            if (!hasDataCenter) {
                UnlockInternalRecoveryTech(TFE分馏数据中心);
            }
            if (!hasProtoKit) {
                UnlockInternalRecoveryTech(TFE分馏塔原胚);
            }
            starterKitRecovered = true;
            return;
        }

        if (GameMain.gameTick < StarterSignalStartTick) {
            return;
        }

        if (!TryGetStarterSignalPosition(out Vector3 signalPosition)) {
            ShowStarterSignalTip("异常旧文明信号出生星提示");
            return;
        }

        float distance = Vector3.Distance(GameMain.mainPlayer.position, signalPosition);
        if (distance > StarterSignalRecoverRadius) {
            ShowStarterSignalDistanceTip(distance);
            return;
        }

        bool recovered = UnlockInternalRecoveryTech(TFE分馏数据中心);
        recovered |= UnlockInternalRecoveryTech(TFE分馏塔原胚);
        starterKitRecovered = true;
        if (recovered) {
            ShowRecoveryTip("旧文明启动套件回收提示");
        }
    }

    private static bool TryGetStarterSignalPosition(out Vector3 signalPosition) {
        signalPosition = default;
        GameData gameData = GameMain.data;
        PlanetData planet = gameData?.localPlanet;
        if (planet == null || gameData.galaxy == null || planet.id != gameData.galaxy.birthPlanetId) {
            return false;
        }

        Vector3 birthPoint = planet.birthPoint;
        if (birthPoint.sqrMagnitude < 1f) {
            return false;
        }

        Vector3 up = birthPoint.normalized;
        Vector3 east = Vector3.Cross(Vector3.up, up);
        if (east.sqrMagnitude < 0.01f) {
            east = Vector3.Cross(Vector3.right, up);
        }
        east.Normalize();
        Vector3 north = Vector3.Cross(up, east).normalized;
        Vector3 tangent = (east * 0.72f + north * 0.69f).normalized;
        float arc = StarterSignalDistance / Mathf.Max(planet.realRadius, 1f);
        Vector3 signalDirection = (up * Mathf.Cos(arc) + tangent * Mathf.Sin(arc)).normalized;
        signalPosition = signalDirection * planet.realRadius;
        return true;
    }

    private static void ShowStarterSignalDistanceTip(float distance) {
        ShowStarterSignalTip(string.Format("异常旧文明信号距离提示".Translate(), Mathf.CeilToInt(distance)));
    }

    private static void ShowStarterSignalTip(string text) {
        if (starterSignalNotified && GameMain.gameTick < nextStarterSignalTipTick) {
            return;
        }

        starterSignalNotified = true;
        nextStarterSignalTipTick = GameMain.gameTick + StarterSignalTipIntervalTicks;
        ShowRecoveryTip(text);
    }

    private static void RecoverMatrixStageSupplies() {
        if (!starterKitRecovered) {
            return;
        }

        bool anyRecovered = false;
        for (int i = 0; i < matrixStageSupplyTechIds.Length; i++) {
            if (matrixStageSupplyRecovered[i]) {
                continue;
            }

            int supplyTechId = matrixStageSupplyTechIds[i];
            if (IsTechRecovered(supplyTechId)) {
                matrixStageSupplyRecovered[i] = true;
                continue;
            }

            if (!IsTechRecovered(matrixStageTechIds[i])) {
                continue;
            }

            anyRecovered |= UnlockInternalRecoveryTech(supplyTechId);
            matrixStageSupplyRecovered[i] = true;
        }
        if (anyRecovered) {
            ShowRecoveryTip("文明阶段补给恢复提示");
        }
    }

    private static void RecoverLogisticsInteractionProtocols() {
        if (!starterKitRecovered || !IsTechRecovered(TFE物品交互)) {
            return;
        }

        for (int i = 0; i < logisticsInteractionTechIds.Length; i++) {
            if (logisticsInteractionRecovered[i]) {
                continue;
            }

            int techId = logisticsInteractionTechIds[i];
            if (IsTechRecovered(techId)) {
                logisticsInteractionRecovered[i] = true;
                continue;
            }

            if (!IsTechRecovered(vanillaLogisticsTechIds[i])) {
                continue;
            }

            if (UnlockInternalRecoveryTech(techId)) {
                ShowProtocolRecoveredTip(techId);
            }
            logisticsInteractionRecovered[i] = true;
        }
    }

    private static bool UnlockInternalRecoveryTech(int techId) {
        if (GameMain.history == null || IsTechRecovered(techId)) {
            return false;
        }

        TechProto tech = LDB.techs.Select(techId);
        if (tech == null) {
            return false;
        }

        GameMain.history.UnlockTechUnlimited(techId, true);
        return IsTechRecovered(techId);
    }

    private static bool IsTechRecovered(int techId) {
        return GameMain.history != null && GameMain.history.TechUnlocked(techId, true);
    }

    private static void SyncRecoveredFlagsFromHistory() {
        if (GameMain.history == null) {
            return;
        }

        starterKitRecovered |= IsTechRecovered(TFE分馏数据中心) && IsTechRecovered(TFE分馏塔原胚);
        for (int i = 0; i < matrixStageSupplyTechIds.Length; i++) {
            matrixStageSupplyRecovered[i] |= IsTechRecovered(matrixStageSupplyTechIds[i]);
        }
        for (int i = 0; i < logisticsInteractionTechIds.Length; i++) {
            logisticsInteractionRecovered[i] |= IsTechRecovered(logisticsInteractionTechIds[i]);
        }
    }

    private static void ShowRecoveryTip(string key) {
        if (UIRoot.instance?.uiGame?.generalTips == null || !UIRoot.instance.uiGame.active) {
            return;
        }
        UIRealtimeTip.Popup(key.Translate(), true, 2);
    }
}
