using System;
using System.Linq;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.RecipeGrowth;
using FE.UI.View.DrawGrowth;
using UnityEngine;
using static FE.Logic.Manager.GachaManager;
using static FE.Logic.Manager.ProcessManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Logic.Recipe.ERecipeExtension;
using static FE.Utils.Utils;

namespace FE.UI.View.ProgressTask;

public static partial class MainTask {
    private readonly struct RouteMap(string routeName, string centerTitle, TaskBranch[] branches) {
        public readonly string RouteName = routeName;
        public readonly string CenterTitle = centerTitle;
        public readonly TaskBranch[] Branches = branches;
    }

    private readonly struct TaskBranch(string id, string name, Vector2 labelPosition, TaskNode[] nodes) {
        public readonly string Id = id;
        public readonly string Name = name;
        public readonly Vector2 LabelPosition = labelPosition;
        public readonly TaskNode[] Nodes = nodes;
    }

    private readonly struct TaskNode(
        string id,
        string name,
        string desc,
        int iconItemId,
        int rewardItemId,
        int rewardCount,
        Vector2 position,
        Func<bool> isCompleted,
        Func<string> progressText) {
        public readonly string Id = id;
        public readonly string Name = name;
        public readonly string Desc = desc;
        public readonly int IconItemId = iconItemId;
        public readonly int RewardItemId = rewardItemId;
        public readonly int RewardCount = rewardCount;
        public readonly Vector2 Position = position;
        public readonly Func<bool> IsCompleted = isCompleted;
        public readonly Func<string> ProgressText = progressText;
    }

    private static readonly RouteMap[] RouteMaps = [
        BuildNormalRoute(),
        BuildSpeedrunRoute(),
    ];

    private static RouteMap GetRouteByModeIndex(int modeIndex) => RouteMaps[Math.Max(0, Math.Min(RouteMaps.Length - 1, modeIndex))];

    private static RouteMap GetCurrentRoute() => GetRouteByModeIndex(GetModeIndex());

    private static RouteMap BuildNormalRoute() {
        return new RouteMap(
            "常规里程碑路线",
            "常规主线",
            [
                Branch("normal-start", "基础起步线", 458f, 154f,
                    Node("normal-tech-data", "分馏启示", "解锁分馏数据中心科技", IFE残片, IFE残片, 200, 503f, 184f,
                        () => IsTechUnlocked(TFE分馏数据中心),
                        () => GetTechProgressText(TFE分馏数据中心)),
                    Node("normal-frac-10", "初次量产", "累计完成 10 次分馏成功", IFE残片, IFE残片, 300, 503f, 126f,
                        () => totalFractionSuccesses >= 10,
                        () => GetCountProgressText("分馏次数", totalFractionSuccesses, 10)),
                    Node("normal-frac-50", "万物之始", "累计完成 50 次分馏成功", IFE残片, IFE残片, 500, 503f, 68f,
                        () => totalFractionSuccesses >= 50,
                        () => GetCountProgressText("分馏次数", totalFractionSuccesses, 50)),
                    Node("normal-frac-200", "分馏热潮", "累计完成 200 次分馏成功", IFE残片, IFE残片, 500, 503f, 10f,
                        () => totalFractionSuccesses >= 200,
                        () => GetCountProgressText("分馏次数", totalFractionSuccesses, 200))
                ),
                Branch("normal-opening", "开线扩张线", 44f, 250f,
                    Node("normal-draw-5", "开线热身", "累计完成 5 次开线抽取", IFE残片, IFE残片, 200, 341f, 298f,
                        () => TicketRaffle.openingLineDraws >= 5,
                        () => GetCountProgressText("开线抽取", TicketRaffle.openingLineDraws, 5)),
                    Node("normal-draw-20", "开线之门", "累计完成 20 次开线抽取", IFE残片, IFE残片, 500, 265f, 298f,
                        () => TicketRaffle.openingLineDraws >= 20,
                        () => GetCountProgressText("开线抽取", TicketRaffle.openingLineDraws, 20)),
                    Node("normal-draw-50", "开线热手", "累计完成 50 次开线抽取", IFE残片, IFE残片, 500, 189f, 298f,
                        () => TicketRaffle.openingLineDraws >= 50,
                        () => GetCountProgressText("开线抽取", TicketRaffle.openingLineDraws, 50)),
                    Node("normal-draw-100", "开线先锋", "累计完成 100 次开线抽取", IFE残片, IFE残片, 1000, 113f, 298f,
                        () => TicketRaffle.openingLineDraws >= 100,
                        () => GetCountProgressText("开线抽取", TicketRaffle.openingLineDraws, 100)),
                    Node("normal-draw-200", "开线专家", "累计完成 200 次开线抽取", IFE交互塔原胚, IFE交互塔原胚, 10, 37f, 298f,
                        () => TicketRaffle.openingLineDraws >= 200,
                        () => GetCountProgressText("开线抽取", TicketRaffle.openingLineDraws, 200))
                ),
                Branch("normal-craft", "建筑工艺线", 872f, 250f,
                    Node("normal-mineral", "矿物新生", "解锁矿物复制科技", IFE矿物复制塔原胚, IFE矿物复制塔原胚, 10, 605f, 298f,
                        () => IsTechUnlocked(TFE矿物复制),
                        () => GetTechProgressText(TFE矿物复制)),
                    Node("normal-proto", "原胚萌发", "解锁分馏塔原胚科技", IFE交互塔原胚, IFE交互塔原胚, 10, 669f, 298f,
                        () => IsTechUnlocked(TFE分馏塔原胚),
                        () => GetTechProgressText(TFE分馏塔原胚)),
                    Node("normal-conversion", "物品转化", "解锁物品转化科技", IFE转化塔原胚, IFE转化塔原胚, 10, 733f, 298f,
                        () => IsTechUnlocked(TFE物品转化),
                        () => GetTechProgressText(TFE物品转化)),
                    Node("normal-level-3", "工艺稳态", "任意万物分馏建筑等级达到 3", IFE残片, IFE残片, 500, 797f, 298f,
                        () => GetMaxBuildingLevel() >= 3,
                        () => GetBuildingLevelProgressText(3)),
                    Node("normal-level-6", "工艺优化", "任意万物分馏建筑等级达到 6", IFE残片, IFE残片, 1000, 861f, 298f,
                        () => GetMaxBuildingLevel() >= 6,
                        () => GetBuildingLevelProgressText(6)),
                    Node("normal-rectification", "精馏经济", "解锁物品精馏科技", IFE精馏塔原胚, IFE精馏塔原胚, 5, 925f, 298f,
                        () => IsTechUnlocked(TFE物品精馏),
                        () => GetTechProgressText(TFE物品精馏)),
                    Node("normal-interstellar", "星际互联", "解锁星际物流交互科技", IFE星际物流交互站, IFE星际物流交互站, 2, 989f, 298f,
                        () => IsTechUnlocked(TFE星际物流交互),
                        () => GetTechProgressText(TFE星际物流交互))
                ),
                Branch("normal-endgame", "黑雾终盘线", 458f, 624f,
                    Node("normal-darkfog-signal", "黑雾信号", "将黑雾支线推进到“信号接触”阶段", I黑雾矩阵, I黑雾矩阵, 4, 503f, 414f,
                        () => DarkFogCombatManager.GetCurrentStage() >= EDarkFogCombatStage.Signal,
                        () => GetDarkFogStageProgressText(EDarkFogCombatStage.Signal)),
                    Node("normal-darkfog-ground", "黑雾接战", "将黑雾支线推进到“地面压制”阶段", I黑雾矩阵, I黑雾矩阵, 8, 503f, 462f,
                        () => DarkFogCombatManager.GetCurrentStage() >= EDarkFogCombatStage.GroundSuppression,
                        () => GetDarkFogStageProgressText(EDarkFogCombatStage.GroundSuppression)),
                    Node("normal-recipes-40", "配方扩编", "累计解锁 40 个分馏配方", IFE残片, IFE残片, 500, 503f, 510f,
                        () => GetUnlockedRecipeCount() >= 40,
                        () => GetRecipeProgressText(40)),
                    Node("normal-recipes-80", "工艺总览", "累计解锁 80 个分馏配方", IFE残片, IFE残片, 1000, 503f, 558f,
                        () => GetUnlockedRecipeCount() >= 80,
                        () => GetRecipeProgressText(80)),
                    Node("normal-end", "万物归一", "累计解锁 100 个分馏配方并完成 5000 次分馏成功", IFE残片, IFE残片, 2000, 503f, 606f,
                        () => GetUnlockedRecipeCount() >= 100 && totalFractionSuccesses >= 5000,
                        () => $"{GetRecipeProgressText(100)} / {GetCountProgressText("分馏次数", totalFractionSuccesses, 5000)}")
                )
            ]
        );
    }

    private static RouteMap BuildSpeedrunRoute() {
        return new RouteMap(
            "速通里程碑路线",
            "速通主线",
            [
                Branch("speed-start", "速启线", 484f, 154f,
                    Node("speed-tech-data", "速通启程", "解锁分馏数据中心科技", IFE残片, IFE残片, 200, 503f, 184f,
                        () => IsTechUnlocked(TFE分馏数据中心),
                        () => GetTechProgressText(TFE分馏数据中心)),
                    Node("speed-frac-50", "速通量产", "累计完成 50 次分馏成功", IFE残片, IFE残片, 300, 503f, 126f,
                        () => totalFractionSuccesses >= 50,
                        () => GetCountProgressText("分馏次数", totalFractionSuccesses, 50)),
                    Node("speed-frac-200", "速通效率", "累计完成 200 次分馏成功", IFE残片, IFE残片, 500, 503f, 68f,
                        () => totalFractionSuccesses >= 200,
                        () => GetCountProgressText("分馏次数", totalFractionSuccesses, 200)),
                    Node("speed-frac-800", "高速闭环", "累计完成 800 次分馏成功", IFE残片, IFE残片, 1000, 503f, 10f,
                        () => totalFractionSuccesses >= 800,
                        () => GetCountProgressText("分馏次数", totalFractionSuccesses, 800))
                ),
                Branch("speed-opening", "开线冲刺线", 56f, 250f,
                    Node("speed-draw-5", "开线预热", "累计完成 5 次开线抽取", IFE残片, IFE残片, 200, 341f, 298f,
                        () => TicketRaffle.openingLineDraws >= 5,
                        () => GetCountProgressText("开线抽取", TicketRaffle.openingLineDraws, 5)),
                    Node("speed-draw-10", "极速开线", "累计完成 10 次开线抽取", IFE残片, IFE残片, 500, 241f, 298f,
                        () => TicketRaffle.openingLineDraws >= 10,
                        () => GetCountProgressText("开线抽取", TicketRaffle.openingLineDraws, 10)),
                    Node("speed-draw-20", "开线推进", "累计完成 20 次开线抽取", IFE残片, IFE残片, 500, 141f, 298f,
                        () => TicketRaffle.openingLineDraws >= 20,
                        () => GetCountProgressText("开线抽取", TicketRaffle.openingLineDraws, 20)),
                    Node("speed-draw-50", "开线冲刺", "累计完成 50 次开线抽取", IFE交互塔原胚, IFE交互塔原胚, 10, 41f, 298f,
                        () => TicketRaffle.openingLineDraws >= 50,
                        () => GetCountProgressText("开线抽取", TicketRaffle.openingLineDraws, 50))
                ),
                Branch("speed-craft", "核心产能线", 872f, 250f,
                    Node("speed-mineral", "矿物快启", "解锁矿物复制科技", IFE矿物复制塔原胚, IFE矿物复制塔原胚, 10, 605f, 298f,
                        () => IsTechUnlocked(TFE矿物复制),
                        () => GetTechProgressText(TFE矿物复制)),
                    Node("speed-conversion", "转化连锁", "解锁物品转化科技", IFE转化塔原胚, IFE转化塔原胚, 10, 727f, 298f,
                        () => IsTechUnlocked(TFE物品转化),
                        () => GetTechProgressText(TFE物品转化)),
                    Node("speed-rectification", "精馏冲刺", "解锁物品精馏科技", IFE精馏塔原胚, IFE精馏塔原胚, 5, 849f, 298f,
                        () => IsTechUnlocked(TFE物品精馏),
                        () => GetTechProgressText(TFE物品精馏)),
                    Node("speed-interstellar", "星际跃迁", "解锁星际物流交互科技", IFE星际物流交互站, IFE星际物流交互站, 2, 971f, 298f,
                        () => IsTechUnlocked(TFE星际物流交互),
                        () => GetTechProgressText(TFE星际物流交互))
                ),
                Branch("speed-finish", "收官冲刺线", 458f, 612f,
                    Node("speed-darkfog-signal", "速通接敌", "将黑雾支线推进到“信号接触”阶段", I黑雾矩阵, I黑雾矩阵, 4, 503f, 426f,
                        () => DarkFogCombatManager.GetCurrentStage() >= EDarkFogCombatStage.Signal,
                        () => GetDarkFogStageProgressText(EDarkFogCombatStage.Signal)),
                    Node("speed-darkfog-ground", "地面压制", "将黑雾支线推进到“地面压制”阶段", I黑雾矩阵, I黑雾矩阵, 8, 503f, 486f,
                        () => DarkFogCombatManager.GetCurrentStage() >= EDarkFogCombatStage.GroundSuppression,
                        () => GetDarkFogStageProgressText(EDarkFogCombatStage.GroundSuppression)),
                    Node("speed-recipes-30", "配方冲线", "累计解锁 30 个分馏配方", IFE残片, IFE残片, 500, 503f, 546f,
                        () => GetUnlockedRecipeCount() >= 30,
                        () => GetRecipeProgressText(30)),
                    Node("speed-end", "速通闭环", "累计解锁 60 个分馏配方并完成 3000 次分馏成功", IFE残片, IFE残片, 2000, 503f, 606f,
                        () => GetUnlockedRecipeCount() >= 60 && totalFractionSuccesses >= 3000,
                        () => $"{GetRecipeProgressText(60)} / {GetCountProgressText("分馏次数", totalFractionSuccesses, 3000)}")
                )
            ]
        );
    }

    private static TaskBranch Branch(string id, string name, float left, float top, params TaskNode[] nodes) {
        return new TaskBranch(id, name, new Vector2(left, top), nodes);
    }

    private static TaskNode Node(string id, string name, string desc, int iconItemId, int rewardItemId, int rewardCount,
        float left, float top, Func<bool> isCompleted, Func<string> progressText) {
        return new TaskNode(id, name, desc, iconItemId, rewardItemId, rewardCount, new Vector2(left, top), isCompleted, progressText);
    }

    private static bool IsTechUnlocked(int techId) {
        return GameMain.history != null && GameMain.history.TechUnlocked(techId);
    }

    private static int GetUnlockedRecipeCount() {
        return RecipeGrowthQueries.GetUnlockedCount(RecipeTypes);
    }

    private static int GetMaxBuildingLevel() {
        return Math.Max(InteractionTower.Level, Math.Max(MineralReplicationTower.Level,
            Math.Max(PointAggregateTower.Level, Math.Max(ConversionTower.Level, RectificationTower.Level))));
    }

    private static string GetTechProgressText(int techId) {
        return $"{"科技解锁".Translate()}：{(IsTechUnlocked(techId) ? "是".Translate() : "否".Translate())}";
    }

    private static string GetCountProgressText(string label, long current, long target) {
        return $"{label.Translate()}：{current}/{target}";
    }

    private static string GetBuildingLevelProgressText(int targetLevel) {
        return $"{"建筑等级".Translate()}：{GetMaxBuildingLevel()}/{targetLevel}";
    }

    private static string GetRecipeProgressText(int targetCount) {
        return $"{"解锁配方".Translate()}：{GetUnlockedRecipeCount()}/{targetCount}";
    }

    private static string GetDarkFogStageProgressText(EDarkFogCombatStage targetStage) {
        return $"{"黑雾阶段".Translate()}：{GetDarkFogStageName(DarkFogCombatManager.GetCurrentStage())} / {GetDarkFogStageName(targetStage)}";
    }

    private static string GetDarkFogStageName(EDarkFogCombatStage stage) {
        return stage switch {
            EDarkFogCombatStage.Dormant => "休眠观察".Translate(),
            EDarkFogCombatStage.Signal => "信号接触".Translate(),
            EDarkFogCombatStage.GroundSuppression => "地面压制".Translate(),
            EDarkFogCombatStage.StellarHunt => "星域围猎".Translate(),
            _ => "奇点收束".Translate(),
        };
    }
}
