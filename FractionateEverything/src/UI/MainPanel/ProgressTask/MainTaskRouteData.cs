using System;
using FE.Logic.Building;
using FE.Logic.Manager;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Fractionation.Growth;
using FE.UI.MainPanel.DrawGrowth;
using FE.Logic.DarkFog;
using FE.Logic.Economy;
using FE.Logic.Gacha;
using static FE.Logic.Fractionation.Process.ProcessManager;
using static FE.Logic.Fractionation.Recipes.RecipeManager;
using static FE.Logic.Fractionation.Recipes.ERecipeExtension;
using static FE.Utils.Utils;
using static FE.Logic.DataCenter.PlayerInventoryAccess;

namespace FE.UI.MainPanel.ProgressTask;

public static partial class MainTask {
    private readonly struct RouteMap(string routeName, string centerTitle, StageColumn[] stages, TaskBranch[] branches) {
        public readonly string RouteName = routeName;
        public readonly string CenterTitle = centerTitle;
        public readonly StageColumn[] Stages = stages;
        public readonly TaskBranch[] Branches = branches;
    }

    private readonly struct StageColumn(string id, string name, int iconItemId) {
        public readonly string Id = id;
        public readonly string Name = name;
        public readonly int IconItemId = iconItemId;
    }

    private readonly struct TaskBranch(string id, string name, TaskNode[] nodes) {
        public readonly string Id = id;
        public readonly string Name = name;
        public readonly TaskNode[] Nodes = nodes;
    }

    private readonly struct TaskNode(
        string id,
        string name,
        string desc,
        int stageIndex,
        int iconItemId,
        int rewardItemId,
        int rewardCount,
        Func<bool> isCompleted,
        Func<string> progressText) {
        public readonly string Id = id;
        public readonly string Name = name;
        public readonly string Desc = desc;
        public readonly int StageIndex = stageIndex;
        public readonly int IconItemId = iconItemId;
        public readonly int RewardItemId = rewardItemId;
        public readonly int RewardCount = rewardCount;
        public readonly Func<bool> IsCompleted = isCompleted;
        public readonly Func<string> ProgressText = progressText;
    }

    private static readonly StageColumn[] MainStages = [
        Stage("stage-start", "起步", IFE残片),
        Stage("stage-electromagnetic", "电磁矩阵", I电磁矩阵),
        Stage("stage-energy", "能量矩阵", I能量矩阵),
        Stage("stage-structure", "结构矩阵", I结构矩阵),
        Stage("stage-information", "信息矩阵", I信息矩阵),
        Stage("stage-gravity", "引力矩阵", I引力矩阵),
        Stage("stage-universe", "宇宙矩阵", I宇宙矩阵),
        Stage("stage-darkfog", "黑雾支线", I黑雾矩阵),
    ];

    private static readonly RouteMap[] RouteMaps = [
        BuildNormalRoute(),
        BuildSpeedrunRoute(),
    ];

    private static StageColumn Stage(string id, string name, int iconItemId) => new(id, name, iconItemId);

    private static RouteMap GetRouteByModeIndex(int modeIndex) =>
        RouteMaps[Math.Max(0, Math.Min(RouteMaps.Length - 1, modeIndex))];

    private static RouteMap GetCurrentRoute() => GetRouteByModeIndex(GetModeIndex());

    private static RouteMap BuildNormalRoute() {
        return new RouteMap("常规里程碑路线", "常规主线", MainStages, BuildSharedBranches(finalRecipeTarget: 100,
            finalFractionTarget: 5000));
    }

    private static RouteMap BuildSpeedrunRoute() {
        return new RouteMap("速通里程碑路线", "速通主线", MainStages, BuildSharedBranches(finalRecipeTarget: 60,
            finalFractionTarget: 3000));
    }

    private static TaskBranch[] BuildSharedBranches(int finalRecipeTarget, long finalFractionTarget) {
        return [
            Branch("matrix-stage", "矩阵阶段",
                Node("matrix-data-center", "分馏启示", "解锁分馏数据中心科技", 0, IFE残片, IFE残片, 200,
                    () => IsTechUnlocked(TFE分馏数据中心), () => GetTechProgressText(TFE分馏数据中心)),
                Node("matrix-em", "电磁入门", "解锁电磁矩阵", 1, I电磁矩阵, IFE残片, 200,
                    () => IsTechUnlocked(T电磁矩阵), () => GetTechProgressText(T电磁矩阵)),
                Node("matrix-gift-1", "电磁礼包", "研究超值礼包1", 1, I电磁矩阵, IFE残片, 200,
                    () => IsTechUnlocked(TFE超值礼包1), () => GetTechProgressText(TFE超值礼包1)),
                Node("matrix-energy", "能量阶段", "解锁能量矩阵", 2, I能量矩阵, IFE残片, 300,
                    () => IsTechUnlocked(T能量矩阵), () => GetTechProgressText(T能量矩阵)),
                Node("matrix-gift-2", "能量礼包", "研究超值礼包2", 2, I能量矩阵, IFE残片, 300,
                    () => IsTechUnlocked(TFE超值礼包2), () => GetTechProgressText(TFE超值礼包2)),
                Node("matrix-structure", "结构阶段", "解锁结构矩阵", 3, I结构矩阵, IFE残片, 400,
                    () => IsTechUnlocked(T结构矩阵), () => GetTechProgressText(T结构矩阵)),
                Node("matrix-gift-3", "结构礼包", "研究超值礼包3", 3, I结构矩阵, IFE残片, 400,
                    () => IsTechUnlocked(TFE超值礼包3), () => GetTechProgressText(TFE超值礼包3)),
                Node("matrix-information", "信息阶段", "解锁信息矩阵", 4, I信息矩阵, IFE残片, 500,
                    () => IsTechUnlocked(T信息矩阵), () => GetTechProgressText(T信息矩阵)),
                Node("matrix-gift-4", "信息礼包", "研究超值礼包4", 4, I信息矩阵, IFE残片, 500,
                    () => IsTechUnlocked(TFE超值礼包4), () => GetTechProgressText(TFE超值礼包4)),
                Node("matrix-gravity", "引力阶段", "解锁引力矩阵", 5, I引力矩阵, IFE残片, 600,
                    () => IsTechUnlocked(T引力矩阵), () => GetTechProgressText(T引力矩阵)),
                Node("matrix-gift-5", "引力礼包", "研究超值礼包5", 5, I引力矩阵, IFE残片, 600,
                    () => IsTechUnlocked(TFE超值礼包5), () => GetTechProgressText(TFE超值礼包5)),
                Node("matrix-universe", "宇宙阶段", "解锁宇宙矩阵", 6, I宇宙矩阵, IFE残片, 800,
                    () => IsTechUnlocked(T宇宙矩阵), () => GetTechProgressText(T宇宙矩阵)),
                Node("matrix-gift-6", "宇宙礼包", "研究超值礼包6", 6, I宇宙矩阵, IFE残片, 800,
                    () => IsTechUnlocked(TFE超值礼包6), () => GetTechProgressText(TFE超值礼包6))),
            Branch("fraction-low", "低档分馏",
                CountNode("frac-first", "首次分馏", "累计完成 1 次分馏成功", 0, IFE残片, 1, totalFractionSuccesses,
                    () => totalFractionSuccesses, 100),
                CountNode("frac-10", "分馏十次", "累计完成 10 次分馏成功", 1, IFE残片, 10, totalFractionSuccesses,
                    () => totalFractionSuccesses, 150),
                CountNode("frac-50", "分馏五十次", "累计完成 50 次分馏成功", 1, IFE残片, 50, totalFractionSuccesses,
                    () => totalFractionSuccesses, 200),
                CountNode("frac-100", "分馏百次", "累计完成 100 次分馏成功", 2, IFE残片, 100, totalFractionSuccesses,
                    () => totalFractionSuccesses, 300),
                CountNode("frac-200", "分馏两百次", "累计完成 200 次分馏成功", 2, IFE残片, 200,
                    totalFractionSuccesses, () => totalFractionSuccesses, 400),
                CountNode("frac-300", "分馏三百次", "累计完成 300 次分馏成功", 3, IFE残片, 300,
                    totalFractionSuccesses, () => totalFractionSuccesses, 500),
                CountNode("frac-final", "主线闭环", $"累计解锁 {finalRecipeTarget} 个配方并完成 {finalFractionTarget} 次分馏成功",
                    6, IFE残片, finalFractionTarget, totalFractionSuccesses,
                    () => totalFractionSuccesses, 1000,
                    () => GetUnlockedRecipeCount() >= finalRecipeTarget && totalFractionSuccesses >= finalFractionTarget,
                    () => $"{GetRecipeProgressText(finalRecipeTarget)} / {GetCountProgressText("分馏次数", totalFractionSuccesses, finalFractionTarget)}")),
            Branch("draw-low", "低档抽取",
                DrawNode("draw-opening-1", "首次开线", "累计完成 1 次开线抽取", 1, IFE残片, 1, true, 100),
                DrawNode("draw-opening-5", "开线五次", "累计完成 5 次开线抽取", 1, IFE残片, 5, true, 150),
                DrawNode("draw-opening-10", "开线十次", "累计完成 10 次开线抽取", 2, IFE残片, 10, true, 200),
                DrawNode("draw-opening-20", "开线二十次", "累计完成 20 次开线抽取", 2, IFE残片, 20, true, 300),
                DrawNode("draw-opening-50", "开线五十次", "累计完成 50 次开线抽取", 3, IFE残片, 50, true, 500),
                DrawNode("draw-proto-1", "首次原胚", "累计完成 1 次原胚抽取", 2, IFE交互塔原胚, 1, false, 1),
                DrawNode("draw-proto-5", "原胚五次", "累计完成 5 次原胚抽取", 3, IFE矿物复制塔原胚, 5, false, 1),
                DrawNode("draw-proto-10", "原胚十次", "累计完成 10 次原胚抽取", 4, IFE转化塔原胚, 10, false, 1)),
            Branch("growth-plan", "成长规划",
                Node("growth-recipe-1", "首次配方", "解锁第 1 个分馏配方", 1, IFE残片, IFE残片, 150,
                    () => GetUnlockedRecipeCount() >= 1, () => GetRecipeProgressText(1)),
                Node("growth-recipe-upgrade", "首次升级", "任意分馏配方达到 2 级", 2, IFE残片, IFE残片, 250,
                    () => GetMaxRecipeLevel() >= 2, () => GetRecipeLevelProgressText(2)),
                Node("growth-focus", "主线节点-流派聚焦", "切换到任意非均衡聚焦流派", 2, IFE残片, IFE残片, 250,
                    () => GachaManager.CurrentFocus != GachaFocusType.Balanced, () => GetFocusProgressText()),
                Node("growth-offer", "主线节点-成长报价", "解锁至少 1 项黑雾成长报价", 7, I黑雾矩阵, IFE残片, 400,
                    () => DarkFogCombatManager.GetUnlockedGrowthOfferCount() >= 1,
                    () => GetCountProgressText("主线统计-成长报价", DarkFogCombatManager.GetUnlockedGrowthOfferCount(), 1)),
                Node("growth-catchup", "首次补差", "完成至少 1 次市场板订单", 7, I黑雾矩阵, IFE残片, 500,
                    () => MarketBoardManager.TotalCompletedOfferCount >= 1,
                    () => GetCountProgressText("主线统计-市场订单", MarketBoardManager.TotalCompletedOfferCount, 1))),
            Branch("proto-building", "原胚建筑",
                ProtoNode("proto-first", "首获原胚", "持有任意 1 个分馏塔原胚", 1, IFE交互塔原胚, 1),
                ProtoNode("proto-three", "原胚整备", "累计持有 3 类分馏塔原胚", 2, IFE矿物复制塔原胚, 3),
                ProtoNode("proto-five", "五类原胚", "累计持有 5 类分馏塔原胚", 4, IFE分馏塔定向原胚, 5),
                Node("building-train-one", "首次培养", "任意万物分馏建筑等级达到 1", 1, IFE交互塔, IFE残片, 200,
                    () => GetMaxBuildingLevel() >= 1, () => GetBuildingLevelProgressText(1)),
                Node("building-train-five", "五塔培养", "五类万物分馏建筑均达到 1 级", 4, IFE点数聚集塔, IFE残片, 600,
                    () => GetMinBuildingLevel() >= 1, () => GetMinBuildingLevelProgressText(1)),
                Node("building-upload-one", "首次上传", "分馏数据中心内持有任意万物分馏建筑", 2, IFE交互塔, IFE残片, 200,
                    () => GetUploadedBuildingTypeCount() >= 1, () => GetCountProgressText("上传建筑", GetUploadedBuildingTypeCount(), 1)),
                Node("building-upload-five", "五塔上传", "分馏数据中心内持有五类万物分馏建筑", 5, IFE精馏塔, IFE残片, 800,
                    () => GetUploadedBuildingTypeCount() >= 5, () => GetCountProgressText("上传建筑", GetUploadedBuildingTypeCount(), 5))),
            Branch("building-level-low", "低档建筑等级",
                BuildingLevelNode("building-level-1", "建筑 1 级", 1, 1, 200),
                BuildingLevelNode("building-level-2", "建筑 2 级", 2, 2, 300),
                BuildingLevelNode("building-level-3", "建筑 3 级", 3, 3, 500),
                BuildingLevelNode("building-level-4", "建筑 4 级", 4, 4, 800),
                BuildingLevelNode("building-level-6-guide", "建筑 6 级", 5, 6, 1000)),
            Branch("recipe-low", "低档配方",
                RecipeCountNode("recipe-1", "配方 1 个", 1, 1, 100),
                RecipeCountNode("recipe-3", "配方 3 个", 1, 3, 150),
                RecipeCountNode("recipe-5", "配方 5 个", 2, 5, 200),
                RecipeCountNode("recipe-10", "配方 10 个", 2, 10, 300),
                RecipeCountNode("recipe-20", "配方 20 个", 3, 20, 500),
                RecipeCountNode("recipe-30", "配方 30 个", 4, 30, 700),
                RecipeCountNode("recipe-40", "配方 40 个", 5, 40, 900)),
            Branch("resource-interaction", "资源交互",
                Node("resource-tech", "物品交互", "解锁物品交互科技", 1, IFE残片, IFE残片, 200,
                    () => IsTechUnlocked(TFE物品交互), () => GetTechProgressText(TFE物品交互)),
                Node("resource-extract", "首次提取", "从分馏数据中心提取至少 1 次物品", 1, IFE残片, IFE残片, 200,
                    () => ItemManager.ManualExtractCount >= 1, () => GetCountProgressText("提取次数", ItemManager.ManualExtractCount, 1)),
                Node("resource-upload", "首次上传", "向分馏数据中心手动上传至少 1 次物品", 2, IFE残片, IFE残片, 200,
                    () => ItemManager.ManualUploadCount >= 1, () => GetCountProgressText("上传次数", ItemManager.ManualUploadCount, 1)),
                Node("resource-trade", "首次交易", "在交易所完成至少 1 次买入或卖出", 3, IFE残片, IFE残片, 300,
                    () => ExchangeManager.TotalTradeCount >= 1, () => GetCountProgressText("交易次数", ExchangeManager.TotalTradeCount, 1)),
                Node("resource-board", "主线节点-市场订单", "完成至少 1 次市场板订单", 4, IFE残片, IFE残片, 400,
                    () => MarketBoardManager.TotalCompletedOfferCount >= 1,
                    () => GetCountProgressText("主线统计-市场订单", MarketBoardManager.TotalCompletedOfferCount, 1)),
                Node("resource-fragment", "主线节点-残片兑换", "完成至少 1 次残片兑换", 5, IFE残片, IFE残片, 400,
                    () => FragmentExchangeManager.TotalExchangeCount >= 1,
                    () => GetCountProgressText("主线统计-残片兑换", FragmentExchangeManager.TotalExchangeCount, 1))),
            Branch("recurring-entry", "循环任务入门",
                Node("recurring-first", "首次循环", "领取第 1 次循环任务奖励", 2, IFE残片, IFE残片, 200,
                    () => RecurringTask.TotalClaimedCount >= 1, () => GetCountProgressText("主线统计-循环任务", RecurringTask.TotalClaimedCount, 1)),
                Node("recurring-five", "循环五次", "累计领取 5 次循环任务奖励", 3, IFE残片, IFE残片, 300,
                    () => RecurringTask.TotalClaimedCount >= 5, () => GetCountProgressText("主线统计-循环任务", RecurringTask.TotalClaimedCount, 5)),
                Node("recurring-all-types", "六类循环", "六类循环任务各领取至少 1 次", 4, IFE残片, IFE残片, 500,
                    () => RecurringTask.HasClaimedAllTaskTypes, () => GetRecurringTypeProgressText())),
            Branch("darkfog-early", "黑雾早期",
                Node("darkfog-matrix", "黑雾矩阵", "持有或解锁黑雾矩阵", 7, I黑雾矩阵, I黑雾矩阵, 2,
                    () => GameMain.history != null && (GameMain.history.ItemUnlocked(I黑雾矩阵) || GetItemTotalCount(I黑雾矩阵) > 0),
                    () => GetItemUnlockProgressText(I黑雾矩阵)),
                Node("darkfog-resource-1", "资源层 1", "黑雾资源层级达到 1", 7, I能量碎片, I黑雾矩阵, 2,
                    () => DarkFogCombatManager.GetDarkFogResourceTier() >= 1,
                    () => GetCountProgressText("资源层级", DarkFogCombatManager.GetDarkFogResourceTier(), 1)),
                Node("darkfog-signal", "信号接触", "将黑雾支线推进到“信号接触”阶段", 7, I黑雾矩阵, I黑雾矩阵, 4,
                    () => DarkFogCombatManager.GetCurrentStage() >= EDarkFogCombatStage.Signal,
                    () => GetDarkFogStageProgressText(EDarkFogCombatStage.Signal)),
                Node("darkfog-resource-2", "资源层 2", "黑雾资源层级达到 2", 7, I物质重组器, I黑雾矩阵, 4,
                    () => DarkFogCombatManager.GetDarkFogResourceTier() >= 2,
                    () => GetCountProgressText("资源层级", DarkFogCombatManager.GetDarkFogResourceTier(), 2)),
                Node("darkfog-ground", "地面压制", "将黑雾支线推进到“地面压制”阶段", 7, I黑雾矩阵, I黑雾矩阵, 8,
                    () => DarkFogCombatManager.GetCurrentStage() >= EDarkFogCombatStage.GroundSuppression,
                    () => GetDarkFogStageProgressText(EDarkFogCombatStage.GroundSuppression)))
        ];
    }

    private static TaskBranch Branch(string id, string name, params TaskNode[] nodes) => new(id, name, nodes);

    private static TaskNode Node(string id, string name, string desc, int stageIndex, int iconItemId, int rewardItemId,
        int rewardCount, Func<bool> isCompleted, Func<string> progressText) {
        return new TaskNode(id, name, desc, stageIndex, iconItemId, rewardItemId, rewardCount, isCompleted,
            progressText);
    }

    private static TaskNode CountNode(string id, string name, string desc, int stageIndex, int iconItemId, long target,
        long currentSnapshot, Func<long> currentGetter, int rewardCount, Func<bool> customCondition = null,
        Func<string> customProgress = null) {
        return Node(id, name, desc, stageIndex, iconItemId, IFE残片, rewardCount,
            customCondition ?? (() => currentGetter() >= target),
            customProgress ?? (() => GetCountProgressText("数量", currentGetter(), target)));
    }

    private static TaskNode DrawNode(string id, string name, string desc, int stageIndex, int iconItemId, long target,
        bool opening, int rewardCount) {
        return Node(id, name, desc, stageIndex, iconItemId, IFE残片, rewardCount,
            () => GetDrawCount(opening) >= target,
            () => GetCountProgressText(opening ? "主线统计-开线抽取" : "主线统计-原胚抽取", GetDrawCount(opening), target));
    }

    private static TaskNode ProtoNode(string id, string name, string desc, int stageIndex, int iconItemId,
        int targetTypeCount) {
        return Node(id, name, desc, stageIndex, iconItemId, IFE残片, 200 + targetTypeCount * 100,
            () => GetProtoTypeCount() >= targetTypeCount,
            () => GetCountProgressText("原胚种类", GetProtoTypeCount(), targetTypeCount));
    }

    private static TaskNode BuildingLevelNode(string id, string name, int stageIndex, int targetLevel, int rewardCount) {
        return Node(id, name, $"任意万物分馏建筑等级达到 {targetLevel}", stageIndex, IFE残片, IFE残片, rewardCount,
            () => GetMaxBuildingLevel() >= targetLevel, () => GetBuildingLevelProgressText(targetLevel));
    }

    private static TaskNode RecipeCountNode(string id, string name, int stageIndex, int targetCount, int rewardCount) {
        return Node(id, name, $"累计解锁 {targetCount} 个分馏配方", stageIndex, IFE残片, IFE残片, rewardCount,
            () => GetUnlockedRecipeCount() >= targetCount, () => GetRecipeProgressText(targetCount));
    }

    private static bool IsTechUnlocked(int techId) {
        return GameMain.history != null && GameMain.history.TechUnlocked(techId);
    }

    private static int GetUnlockedRecipeCount() {
        return RecipeGrowthQueries.GetUnlockedCount(RecipeTypes);
    }

    private static int GetMaxRecipeLevel() {
        int maxLevel = 0;
        foreach (BaseRecipe recipe in AllRecipes) {
            maxLevel = Math.Max(maxLevel, RecipeGrowthQueries.GetLevel(recipe));
        }
        return maxLevel;
    }

    private static int GetMaxBuildingLevel() {
        return Math.Max(InteractionTower.Level, Math.Max(MineralReplicationTower.Level,
            Math.Max(PointAggregateTower.Level, Math.Max(ConversionTower.Level, RectificationTower.Level))));
    }

    private static int GetMinBuildingLevel() {
        return Math.Min(InteractionTower.Level, Math.Min(MineralReplicationTower.Level,
            Math.Min(PointAggregateTower.Level, Math.Min(ConversionTower.Level, RectificationTower.Level))));
    }

    private static int GetProtoTypeCount() {
        int count = 0;
        if (GetItemTotalCount(IFE交互塔原胚) > 0) count++;
        if (GetItemTotalCount(IFE矿物复制塔原胚) > 0) count++;
        if (GetItemTotalCount(IFE点数聚集塔原胚) > 0) count++;
        if (GetItemTotalCount(IFE转化塔原胚) > 0) count++;
        if (GetItemTotalCount(IFE精馏塔原胚) > 0) count++;
        if (GetItemTotalCount(IFE分馏塔定向原胚) > 0) count++;
        return count;
    }

    private static int GetUploadedBuildingTypeCount() {
        int count = 0;
        if (GetItemTotalCount(IFE交互塔) > 0) count++;
        if (GetItemTotalCount(IFE矿物复制塔) > 0) count++;
        if (GetItemTotalCount(IFE点数聚集塔) > 0) count++;
        if (GetItemTotalCount(IFE转化塔) > 0) count++;
        if (GetItemTotalCount(IFE精馏塔) > 0) count++;
        return count;
    }

    private static long GetDrawCount(bool opening) {
        return opening ? TicketRaffle.openingLineDraws : Math.Max(0L, TicketRaffle.totalDraws - TicketRaffle.openingLineDraws);
    }

    private static string GetTechProgressText(int techId) {
        return $"{"科技解锁".Translate()}：{(IsTechUnlocked(techId) ? "是".Translate() : "否".Translate())}";
    }

    private static string GetItemUnlockProgressText(int itemId) {
        bool unlocked = GameMain.history != null && GameMain.history.ItemUnlocked(itemId);
        return $"{"物品解锁".Translate()}：{(unlocked || GetItemTotalCount(itemId) > 0 ? "是".Translate() : "否".Translate())}";
    }

    private static string GetCountProgressText(string label, long current, long target) {
        return $"{label.Translate()}：{current}/{target}";
    }

    private static string GetBuildingLevelProgressText(int targetLevel) {
        return $"{"建筑等级".Translate()}：{GetMaxBuildingLevel()}/{targetLevel}";
    }

    private static string GetMinBuildingLevelProgressText(int targetLevel) {
        return $"{"五塔最低等级".Translate()}：{GetMinBuildingLevel()}/{targetLevel}";
    }

    private static string GetRecipeProgressText(int targetCount) {
        return $"{"解锁配方".Translate()}：{GetUnlockedRecipeCount()}/{targetCount}";
    }

    private static string GetRecipeLevelProgressText(int targetLevel) {
        return $"{"配方等级".Translate()}：{GetMaxRecipeLevel()}/{targetLevel}";
    }

    private static string GetFocusProgressText() {
        return $"{"聚焦流派".Translate()}：{(GachaManager.CurrentFocus == GachaFocusType.Balanced ? "否".Translate() : "是".Translate())}";
    }

    private static string GetRecurringTypeProgressText() {
        return $"{"循环类型".Translate()}：{RecurringTask.ClaimedTaskTypeCount}/6";
    }

    private static string GetDarkFogStageProgressText(EDarkFogCombatStage targetStage) {
        return
            $"{"黑雾阶段".Translate()}：{GetDarkFogStageName(DarkFogCombatManager.GetCurrentStage())} / {GetDarkFogStageName(targetStage)}";
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
