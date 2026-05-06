using System;
using System.IO;
using BepInEx.Configuration;
using FE.Logic.Buildings.Definitions;
using FE.Logic.DarkFog;
using FE.Logic.Economy;
using FE.Logic.Fractionation.Growth;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Gacha;
using FE.Logic.Manager;
using FE.UI.Controls;
using FE.UI.Foundation.Window;
using FE.UI.MainPanel.DrawGrowth;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static FE.UI.Layout.GridDsl;
using static FE.Utils.Utils;
using static FE.Logic.Fractionation.Process.ProcessManager;
using static FE.Logic.Fractionation.Recipes.RecipeManager;
using static FE.Logic.Fractionation.Recipes.ERecipeExtension;
using static FE.Logic.DataCenter.PlayerInventoryAccess;
using static FE.Logic.Gacha.GachaManager;
using static FE.Logic.DataCenter.DataCenterInventory;
using static FE.UI.Foundation.RectTransformUtils;

namespace FE.UI.MainPanel.ProgressTask;

/// <summary>
/// 主线阶段目标、路线图和奖励展示页面。
/// </summary>
public static class MainTask {
    private static RectTransform tab;
    private static RectTransform roadmapPanel;

    private static Text txtModeTitle;
    private static Text txtOverallSummary;
    private static Text txtBranchSummary;

    public static void AddTranslations() {
        Register("主线任务", "Main Task");
        Register("主线里程碑", "Main Milestones", "主线里程碑");
        Register("路线总进度", "Route progress: {0}/{1}", "路线进度：{0}/{1}");
        Register("分支完成数", "Branches complete: {0}/{1}", "分支完成：{0}/{1}");
        Register("类别", "Category", "类别");
        Register("节点状态-未解锁", "Locked", "未解锁");
        Register("节点状态-进行中", "In Progress", "进行中");
        Register("节点状态-已完成", "Completed", "已完成");
        Register("节点详情-条件", "Condition:", "条件：");
        Register("节点详情-奖励", "Reward:", "奖励：");
        Register("节点详情-状态", "State:", "状态：");
        Register("节点详情-推荐阶段", "Recommended stage:", "推荐阶段：");
        Register("节点详情-推荐说明", "Recommendation only. It does not block other milestones.",
            "这只是推荐阶段，不限制实际完成顺序。");
        Register("主线里程碑达成提示", "Main milestone unlocked: {0}", "主线里程碑达成：{0}");
        Register("是", "Yes");
        Register("否", "No");
        Register("无", "None", "无");
        Register("数量", "Count", "数量");
        Register("分馏次数", "Fractionations", "分馏次数");
        Register("主线统计-开线抽取", "Opening draws", "开线抽取");
        Register("主线统计-原胚抽取", "Proto draws", "原胚抽取");
        Register("科技解锁", "Tech unlocked", "科技解锁");
        Register("物品解锁", "Item unlocked", "物品解锁");
        Register("建筑等级", "Building level", "建筑等级");
        Register("五塔最低等级", "Min tower level", "五塔最低等级");
        Register("解锁配方", "Unlocked recipes", "解锁配方");
        Register("配方等级", "Recipe level", "配方等级");
        Register("聚焦流派", "Focus style", "聚焦流派");
        Register("原胚种类", "Proto types", "原胚种类");
        Register("上传建筑", "Uploaded buildings", "上传建筑");
        Register("提取次数", "Extracts", "提取次数");
        Register("上传次数", "Uploads", "上传次数");
        Register("交易次数", "Trades", "交易次数");
        Register("主线统计-市场订单", "Market orders", "市场订单");
        Register("主线统计-残片兑换", "Fragment exchanges", "残片兑换");
        Register("主线统计-成长报价", "Growth offers", "成长报价");
        Register("主线统计-循环任务", "Recurring tasks", "循环任务");
        Register("主线节点-流派聚焦", "Focus Style", "流派聚焦");
        Register("主线节点-成长报价", "Growth Offer", "成长报价");
        Register("主线节点-市场订单", "Market Order", "市场订单");
        Register("主线节点-残片兑换", "Fragment Exchange", "残片兑换");
        Register("循环类型", "Recurring types", "循环类型");
        Register("资源层级", "Resource tier", "资源层级");
        Register("黑雾阶段", "Dark Fog stage", "黑雾阶段");
        Register("休眠观察", "Dormant", "休眠观察");
        Register("信号接触", "Signal Contact", "信号接触");
        Register("地面压制", "Ground Suppression", "地面压制");
        Register("星域围猎", "Stellar Hunt", "星域围猎");
        Register("奇点收束", "Singularity Convergence", "奇点收束");

        (string key, string en)[] routeTexts = [
            ("常规里程碑路线", "Normal Milestone Route"),
            ("常规主线", "Normal Main Route"),
            ("速通里程碑路线", "Speedrun Milestone Route"),
            ("速通主线", "Speedrun Main Route"),
            ("起步", "Start"),
            ("电磁矩阵", "Electromagnetic Matrix"),
            ("能量矩阵", "Energy Matrix"),
            ("结构矩阵", "Structure Matrix"),
            ("信息矩阵", "Information Matrix"),
            ("引力矩阵", "Gravity Matrix"),
            ("宇宙矩阵", "Universe Matrix"),
            ("黑雾支线", "Dark Fog Branch"),
            ("矩阵阶段", "Matrix Stages"),
            ("低档分馏", "Early Fractionation"),
            ("低档抽取", "Early Draws"),
            ("成长规划", "Growth Planning"),
            ("原胚建筑", "Proto Buildings"),
            ("低档建筑等级", "Early Building Levels"),
            ("低档配方", "Early Recipes"),
            ("资源交互", "Resource Interaction"),
            ("循环任务入门", "Recurring Intro"),
            ("黑雾早期", "Early Dark Fog"),
            ("分馏启示", "Fractionation Insight"),
            ("电磁入门", "Electromagnetic Start"),
            ("电磁礼包", "Electromagnetic Gift"),
            ("能量阶段", "Energy Stage"),
            ("能量礼包", "Energy Gift"),
            ("结构阶段", "Structure Stage"),
            ("结构礼包", "Structure Gift"),
            ("信息阶段", "Information Stage"),
            ("信息礼包", "Information Gift"),
            ("引力阶段", "Gravity Stage"),
            ("引力礼包", "Gravity Gift"),
            ("宇宙阶段", "Universe Stage"),
            ("宇宙礼包", "Universe Gift"),
            ("首次分馏", "First Fractionation"),
            ("分馏十次", "Ten Fractionations"),
            ("分馏五十次", "Fifty Fractionations"),
            ("分馏百次", "Hundred Fractionations"),
            ("分馏两百次", "Two Hundred Fractionations"),
            ("分馏三百次", "Three Hundred Fractionations"),
            ("主线闭环", "Main Closure"),
            ("首次开线", "First Opening"),
            ("开线五次", "Five Openings"),
            ("开线十次", "Ten Openings"),
            ("开线二十次", "Twenty Openings"),
            ("开线五十次", "Fifty Openings"),
            ("首次原胚", "First Proto Draw"),
            ("原胚五次", "Five Proto Draws"),
            ("原胚十次", "Ten Proto Draws"),
            ("首次配方", "First Recipe"),
            ("首次升级", "First Upgrade"),
            ("首次补差", "First Catch-up"),
            ("首获原胚", "First Proto"),
            ("原胚整备", "Proto Setup"),
            ("五类原胚", "Five Proto Types"),
            ("首次培养", "First Cultivation"),
            ("五塔培养", "Five Tower Cultivation"),
            ("首次上传", "First Upload"),
            ("五塔上传", "Five Tower Upload"),
            ("建筑 1 级", "Building Level 1"),
            ("建筑 2 级", "Building Level 2"),
            ("建筑 3 级", "Building Level 3"),
            ("建筑 4 级", "Building Level 4"),
            ("建筑 6 级", "Building Level 6"),
            ("配方 1 个", "1 Recipe"),
            ("配方 3 个", "3 Recipes"),
            ("配方 5 个", "5 Recipes"),
            ("配方 10 个", "10 Recipes"),
            ("配方 20 个", "20 Recipes"),
            ("配方 30 个", "30 Recipes"),
            ("配方 40 个", "40 Recipes"),
            ("物品交互", "Item Interaction"),
            ("首次提取", "First Extract"),
            ("首次交易", "First Trade"),
            ("首次循环", "First Recurring"),
            ("循环五次", "Five Recurring"),
            ("六类循环", "Six Recurring Types"),
            ("黑雾矩阵", "Dark Fog Matrix"),
            ("资源层 1", "Resource Tier 1"),
            ("资源层 2", "Resource Tier 2"),
            ("黑雾信号", "Dark Fog Signal"),
            ("地面压制", "Ground Suppression"),
        ];
        foreach ((string key, string en) in routeTexts) {
            Register(key, en, key);
        }

        (string key, string en)[] routeDescs = [
            ("解锁分馏数据中心科技", "Unlock the Fractionation Data Center technology"),
            ("累计完成 10 次分馏成功", "Reach 10 successful fractionations"),
            ("累计完成 50 次分馏成功", "Reach 50 successful fractionations"),
            ("累计完成 200 次分馏成功", "Reach 200 successful fractionations"),
            ("累计完成 5 次开线抽取", "Perform 5 opening draws"),
            ("累计完成 20 次开线抽取", "Perform 20 opening draws"),
            ("累计完成 50 次开线抽取", "Perform 50 opening draws"),
            ("累计完成 100 次开线抽取", "Perform 100 opening draws"),
            ("累计完成 200 次开线抽取", "Perform 200 opening draws"),
            ("解锁矿物复制科技", "Unlock Mineral Replication technology"),
            ("解锁分馏塔原胚科技", "Unlock Fractionator Proto technology"),
            ("解锁物品转化科技", "Unlock Item Conversion technology"),
            ("任意万物分馏建筑等级达到 3", "Raise any FE building to level 3"),
            ("任意万物分馏建筑等级达到 6", "Raise any FE building to level 6"),
            ("解锁物品精馏科技", "Unlock Rectification technology"),
            ("解锁星际物流交互科技", "Unlock Interstellar Logistics Interaction technology"),
            ("将黑雾支线推进到“信号接触”阶段", "Advance the Dark Fog branch to Signal Contact"),
            ("将黑雾支线推进到“地面压制”阶段", "Advance the Dark Fog branch to Ground Suppression"),
            ("累计解锁 40 个分馏配方", "Unlock 40 fractionation recipes"),
            ("累计解锁 80 个分馏配方", "Unlock 80 fractionation recipes"),
            ("累计解锁 100 个分馏配方并完成 5000 次分馏成功", "Unlock 100 recipes and reach 5000 successful fractionations"),
            ("累计完成 800 次分馏成功", "Reach 800 successful fractionations"),
            ("累计完成 10 次开线抽取", "Perform 10 opening draws"),
            ("解锁 30 个分馏配方", "Unlock 30 fractionation recipes"),
            ("累计解锁 30 个分馏配方", "Unlock 30 fractionation recipes"),
            ("累计解锁 60 个分馏配方并完成 3000 次分馏成功", "Unlock 60 recipes and reach 3000 successful fractionations"),
        ];
        foreach ((string key, string en) in routeDescs) {
            Register(key, en, key);
        }
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        BuildLayout(wnd, trans,
            Grid(
                children: [
                    Grid(pos: (0, 0), objectName: "main-task-root", onBuilt: root => {
                        tab = root;
                        routeViewsByMode = new RouteViewCache[RouteMaps.Length];
                        BuildMilestonePage(wnd);
                        RefreshMilestonePage();
                    }),
                ]));
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        RefreshMilestonePage();
    }

    /// <summary>
    /// 分馏主线路线图定义。
    /// </summary>
    private readonly struct RouteMap(
        string routeName,
        string centerTitle,
        StageColumn[] stages,
        TaskBranch[] branches) {
        public readonly string RouteName = routeName;
        public readonly string CenterTitle = centerTitle;
        public readonly StageColumn[] Stages = stages;
        public readonly TaskBranch[] Branches = branches;
    }

    /// <summary>
    /// 主线路线阶段列定义。
    /// </summary>
    private readonly struct StageColumn(string id, string name, int iconItemId) {
        public readonly string Id = id;
        public readonly string Name = name;
        public readonly int IconItemId = iconItemId;
    }

    /// <summary>
    /// 主线任务分支定义。
    /// </summary>
    private readonly struct TaskBranch(string id, string name, TaskNode[] nodes) {
        public readonly string Id = id;
        public readonly string Name = name;
        public readonly TaskNode[] Nodes = nodes;
    }

    /// <summary>
    /// 主线任务节点定义。
    /// </summary>
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
                    () => GetUnlockedRecipeCount() >= finalRecipeTarget
                          && totalFractionSuccesses >= finalFractionTarget,
                    () =>
                        $"{GetRecipeProgressText(finalRecipeTarget)} / {GetCountProgressText("分馏次数", totalFractionSuccesses, finalFractionTarget)}")),
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
                    () => CurrentFocus != GachaFocusType.Balanced, () => GetFocusProgressText()),
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
                    () => GetUploadedBuildingTypeCount() >= 1,
                    () => GetCountProgressText("上传建筑", GetUploadedBuildingTypeCount(), 1)),
                Node("building-upload-five", "五塔上传", "分馏数据中心内持有五类万物分馏建筑", 5, IFE精馏塔, IFE残片, 800,
                    () => GetUploadedBuildingTypeCount() >= 5,
                    () => GetCountProgressText("上传建筑", GetUploadedBuildingTypeCount(), 5))),
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
                    () => ItemManager.ManualExtractCount >= 1,
                    () => GetCountProgressText("提取次数", ItemManager.ManualExtractCount, 1)),
                Node("resource-upload", "首次上传", "向分馏数据中心手动上传至少 1 次物品", 2, IFE残片, IFE残片, 200,
                    () => ItemManager.ManualUploadCount >= 1,
                    () => GetCountProgressText("上传次数", ItemManager.ManualUploadCount, 1)),
                Node("resource-trade", "首次交易", "在交易所完成至少 1 次买入或卖出", 3, IFE残片, IFE残片, 300,
                    () => ExchangeManager.TotalTradeCount >= 1,
                    () => GetCountProgressText("交易次数", ExchangeManager.TotalTradeCount, 1)),
                Node("resource-board", "主线节点-市场订单", "完成至少 1 次市场板订单", 4, IFE残片, IFE残片, 400,
                    () => MarketBoardManager.TotalCompletedOfferCount >= 1,
                    () => GetCountProgressText("主线统计-市场订单", MarketBoardManager.TotalCompletedOfferCount, 1)),
                Node("resource-fragment", "主线节点-残片兑换", "完成至少 1 次残片兑换", 5, IFE残片, IFE残片, 400,
                    () => FragmentExchangeManager.TotalExchangeCount >= 1,
                    () => GetCountProgressText("主线统计-残片兑换", FragmentExchangeManager.TotalExchangeCount, 1))),
            Branch("recurring-entry", "循环任务入门",
                Node("recurring-first", "首次循环", "领取第 1 次循环任务奖励", 2, IFE残片, IFE残片, 200,
                    () => RecurringTask.TotalClaimedCount >= 1,
                    () => GetCountProgressText("主线统计-循环任务", RecurringTask.TotalClaimedCount, 1)),
                Node("recurring-five", "循环五次", "累计领取 5 次循环任务奖励", 3, IFE残片, IFE残片, 300,
                    () => RecurringTask.TotalClaimedCount >= 5,
                    () => GetCountProgressText("主线统计-循环任务", RecurringTask.TotalClaimedCount, 5)),
                Node("recurring-all-types", "六类循环", "六类循环任务各领取至少 1 次", 4, IFE残片, IFE残片, 500,
                    () => RecurringTask.HasClaimedAllTaskTypes, () => GetRecurringTypeProgressText())),
            Branch("darkfog-early", "黑雾早期",
                Node("darkfog-matrix", "黑雾矩阵", "持有或解锁黑雾矩阵", 7, I黑雾矩阵, I黑雾矩阵, 2,
                    () => GameMain.history != null
                          && (GameMain.history.ItemUnlocked(I黑雾矩阵) || GetItemTotalCount(I黑雾矩阵) > 0),
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

    private static TaskNode
        BuildingLevelNode(string id, string name, int stageIndex, int targetLevel, int rewardCount) {
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
        return opening
            ? TicketRaffle.openingLineDraws
            : Math.Max(0L, TicketRaffle.totalDraws - TicketRaffle.openingLineDraws);
    }

    private static string GetTechProgressText(int techId) {
        return $"{"科技解锁".Translate()}：{(IsTechUnlocked(techId) ? "是".Translate() : "否".Translate())}";
    }

    private static string GetItemUnlockProgressText(int itemId) {
        bool unlocked = GameMain.history != null && GameMain.history.ItemUnlocked(itemId);
        return
            $"{"物品解锁".Translate()}：{(unlocked || GetItemTotalCount(itemId) > 0 ? "是".Translate() : "否".Translate())}";
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
        return
            $"{"聚焦流派".Translate()}：{(CurrentFocus == GachaFocusType.Balanced ? "否".Translate() : "是".Translate())}";
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

    private static bool[][][] completedByMode;
    private static bool[][][] rewardedByMode;
    private static int[] selectedBranchByMode = [-1, -1];
    private static int[] selectedNodeByMode = [-1, -1];
    private static int _lastGlobalTickFrame = -1;
    private static readonly string[][] LegacyStageNodeIdsByMode = [
        [
            "normal-tech-data",
            "normal-frac-50",
            "normal-draw-20",
            "normal-mineral",
            "normal-proto",
            "normal-conversion",
            "normal-level-6",
            "normal-rectification",
            "normal-interstellar",
            "normal-darkfog-ground",
            "normal-end",
        ],
        [
            "speed-tech-data",
            "speed-draw-10",
            "speed-mineral",
            "speed-conversion",
            "speed-frac-800",
            "speed-rectification",
            "speed-interstellar",
            "speed-darkfog-signal",
            "speed-end",
        ],
    ];

    private static int GetModeIndex() {
        return IsSpeedrunMode ? 1 : 0;
    }

    private static void EnsureRouteState() {
        completedByMode ??= CreateStateMatrix();
        rewardedByMode ??= CreateStateMatrix();
        ResizeStateMatrix(ref completedByMode);
        ResizeStateMatrix(ref rewardedByMode);
    }

    private static bool[][][] CreateStateMatrix() {
        bool[][][] matrix = new bool[RouteMaps.Length][][];
        for (int modeIndex = 0; modeIndex < RouteMaps.Length; modeIndex++) {
            RouteMap route = GetRouteByModeIndex(modeIndex);
            matrix[modeIndex] = new bool[route.Branches.Length][];
            for (int branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
                matrix[modeIndex][branchIndex] = new bool[route.Branches[branchIndex].Nodes.Length];
            }
        }
        return matrix;
    }

    private static void ResizeStateMatrix(ref bool[][][] matrix) {
        if (matrix == null || matrix.Length != RouteMaps.Length) {
            matrix = CreateStateMatrix();
            return;
        }

        for (int modeIndex = 0; modeIndex < RouteMaps.Length; modeIndex++) {
            RouteMap route = GetRouteByModeIndex(modeIndex);
            if (matrix[modeIndex] == null || matrix[modeIndex].Length != route.Branches.Length) {
                bool[][] oldBranches = matrix[modeIndex];
                matrix[modeIndex] = new bool[route.Branches.Length][];
                for (int branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
                    int nodeCount = route.Branches[branchIndex].Nodes.Length;
                    matrix[modeIndex][branchIndex] = new bool[nodeCount];
                    if (oldBranches == null || branchIndex >= oldBranches.Length || oldBranches[branchIndex] == null) {
                        continue;
                    }
                    Array.Copy(oldBranches[branchIndex], matrix[modeIndex][branchIndex],
                        Math.Min(oldBranches[branchIndex].Length, nodeCount));
                }
                continue;
            }

            for (int branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
                int nodeCount = route.Branches[branchIndex].Nodes.Length;
                if (matrix[modeIndex][branchIndex] != null && matrix[modeIndex][branchIndex].Length == nodeCount) {
                    continue;
                }
                bool[] oldNodes = matrix[modeIndex][branchIndex];
                matrix[modeIndex][branchIndex] = new bool[nodeCount];
                if (oldNodes != null) {
                    Array.Copy(oldNodes, matrix[modeIndex][branchIndex], Math.Min(oldNodes.Length, nodeCount));
                }
            }
        }
    }

    private static void ResetRouteState() {
        completedByMode = CreateStateMatrix();
        rewardedByMode = CreateStateMatrix();
        selectedBranchByMode = [-1, -1];
        selectedNodeByMode = [-1, -1];
        _lastGlobalTickFrame = -1;
    }

    private static void ClampSelections() {
        for (int modeIndex = 0; modeIndex < RouteMaps.Length; modeIndex++) {
            RouteMap route = GetRouteByModeIndex(modeIndex);
            if (selectedBranchByMode[modeIndex] < 0 || selectedBranchByMode[modeIndex] >= route.Branches.Length) {
                selectedBranchByMode[modeIndex] = -1;
                selectedNodeByMode[modeIndex] = -1;
                continue;
            }
            int branchIndex = selectedBranchByMode[modeIndex];
            int nodeCount = route.Branches[branchIndex].Nodes.Length;
            if (selectedNodeByMode[modeIndex] < 0 || selectedNodeByMode[modeIndex] >= nodeCount) {
                selectedNodeByMode[modeIndex] = -1;
            }
        }
    }

    public static void Tick() {
        if (Time.frameCount == _lastGlobalTickFrame) {
            return;
        }
        _lastGlobalTickFrame = Time.frameCount;
        if (GameMain.data == null || GameMain.history == null) {
            return;
        }

        RefreshRouteProgress(showPopup: true);
        GrantPendingRewards(showPopup: true);
    }

    private static void RefreshRouteProgress(bool showPopup, bool allowRewardGrant = true) {
        EnsureRouteState();
        for (int modeIndex = 0; modeIndex < RouteMaps.Length; modeIndex++) {
            RefreshRouteProgress(modeIndex, showPopup, allowRewardGrant);
        }
    }

    private static void RefreshRouteProgress(int modeIndex, bool showPopup, bool allowRewardGrant) {
        RouteMap route = GetRouteByModeIndex(modeIndex);
        for (int branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
            TaskBranch branch = route.Branches[branchIndex];
            for (int nodeIndex = 0; nodeIndex < branch.Nodes.Length; nodeIndex++) {
                if (completedByMode[modeIndex][branchIndex][nodeIndex]) {
                    continue;
                }

                TaskNode node = branch.Nodes[nodeIndex];
                bool completed = false;
                try {
                    completed = node.IsCompleted();
                }
                catch (Exception ex) {
                    LogWarning($"[MainTask] 节点条件检查失败 {node.Id}: {ex.Message}");
                }

                if (!completed) {
                    continue;
                }

                completedByMode[modeIndex][branchIndex][nodeIndex] = true;
                GrantNodeReward(modeIndex, branchIndex, nodeIndex, showPopup, allowRewardGrant);
            }
        }
    }

    private static bool CanShowRealtimeTip() {
        // 旧存档导入后的前几帧可能已经开始跑主线奖励检查，但游戏内提示 UI 还没完成创建。
        return UIRoot.instance?.uiGame?.generalTips != null && UIRoot.instance.uiGame.active;
    }

    private static bool CanShowItemupTip() {
        return GameMain.mainPlayer != null && UIRoot.instance?.uiGame?.itemupTips != null;
    }

    private static int GetMainTaskRewardTipId() {
        return GameMain.gameScenario?.advisorLogic != null ? 2 : 0;
    }

    private static void GrantNodeReward(int modeIndex, int branchIndex, int nodeIndex, bool showPopup,
        bool allowRewardGrant) {
        if (rewardedByMode[modeIndex][branchIndex][nodeIndex]) {
            return;
        }

        // 奖励发放与去重绑定在节点状态里，避免读档或窗口刷新时重复发奖。
        TaskNode node = GetRouteByModeIndex(modeIndex).Branches[branchIndex].Nodes[nodeIndex];
        if (!allowRewardGrant) {
            return;
        }
        if (node.RewardItemId > 0 && node.RewardCount > 0) {
            AddItemToModData(node.RewardItemId, node.RewardCount, 0, true);
            if (showPopup && CanShowItemupTip()) {
                UIItemup.Up(node.RewardItemId, node.RewardCount);
            }
        }
        rewardedByMode[modeIndex][branchIndex][nodeIndex] = true;

        if (showPopup && CanShowRealtimeTip()) {
            UIRealtimeTip.Popup(string.Format("主线里程碑达成提示".Translate(), node.Name.Translate()), true,
                GetMainTaskRewardTipId());
        }
    }

    private static void GrantPendingRewards(bool showPopup) {
        EnsureRouteState();
        for (int modeIndex = 0; modeIndex < RouteMaps.Length; modeIndex++) {
            for (int branchIndex = 0; branchIndex < completedByMode[modeIndex].Length; branchIndex++) {
                for (int nodeIndex = 0; nodeIndex < completedByMode[modeIndex][branchIndex].Length; nodeIndex++) {
                    if (!completedByMode[modeIndex][branchIndex][nodeIndex]
                        || rewardedByMode[modeIndex][branchIndex][nodeIndex]) {
                        continue;
                    }

                    GrantNodeReward(modeIndex, branchIndex, nodeIndex, showPopup, allowRewardGrant: true);
                }
            }
        }
    }

    private static void EnsureSelectedNode(int modeIndex) {
        ClampSelections();
        if (IsSelectionValid(modeIndex)) {
            return;
        }

        if (TryGetFirstActiveIncompleteNode(modeIndex, out int branchIndex, out int nodeIndex)) {
            selectedBranchByMode[modeIndex] = branchIndex;
            selectedNodeByMode[modeIndex] = nodeIndex;
            return;
        }

        if (TryGetLastCompletedNode(modeIndex, out branchIndex, out nodeIndex)) {
            selectedBranchByMode[modeIndex] = branchIndex;
            selectedNodeByMode[modeIndex] = nodeIndex;
            return;
        }

        selectedBranchByMode[modeIndex] = 0;
        selectedNodeByMode[modeIndex] = 0;
    }

    private static bool IsSelectionValid(int modeIndex) {
        RouteMap route = GetRouteByModeIndex(modeIndex);
        int branchIndex = selectedBranchByMode[modeIndex];
        if (branchIndex < 0 || branchIndex >= route.Branches.Length) {
            return false;
        }
        int nodeIndex = selectedNodeByMode[modeIndex];
        return nodeIndex >= 0 && nodeIndex < route.Branches[branchIndex].Nodes.Length;
    }

    private static bool TryGetFirstActiveIncompleteNode(int modeIndex, out int branchIndex, out int nodeIndex) {
        RouteMap route = GetRouteByModeIndex(modeIndex);
        for (branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
            TaskBranch branch = route.Branches[branchIndex];
            for (nodeIndex = 0; nodeIndex < branch.Nodes.Length; nodeIndex++) {
                if (GetNodeVisualState(modeIndex, branchIndex, nodeIndex) == NodeVisualState.Available) {
                    return true;
                }
            }
        }
        branchIndex = 0;
        nodeIndex = 0;
        return false;
    }

    private static bool TryGetLastCompletedNode(int modeIndex, out int branchIndex, out int nodeIndex) {
        RouteMap route = GetRouteByModeIndex(modeIndex);
        for (branchIndex = route.Branches.Length - 1; branchIndex >= 0; branchIndex--) {
            TaskBranch branch = route.Branches[branchIndex];
            for (nodeIndex = branch.Nodes.Length - 1; nodeIndex >= 0; nodeIndex--) {
                if (completedByMode[modeIndex][branchIndex][nodeIndex]) {
                    return true;
                }
            }
        }
        branchIndex = 0;
        nodeIndex = 0;
        return false;
    }

    private static NodeVisualState GetNodeVisualState(int modeIndex, int branchIndex, int nodeIndex) {
        if (completedByMode[modeIndex][branchIndex][nodeIndex]) {
            return NodeVisualState.Completed;
        }
        return IsNodeUnlocked(modeIndex, branchIndex, nodeIndex) ? NodeVisualState.Available : NodeVisualState.Locked;
    }

    private static bool IsNodeUnlocked(int modeIndex, int branchIndex, int nodeIndex) {
        return true;
    }

    private static int CountCompletedNodes(int modeIndex) {
        EnsureRouteState();
        int count = 0;
        for (int branchIndex = 0; branchIndex < completedByMode[modeIndex].Length; branchIndex++) {
            for (int nodeIndex = 0; nodeIndex < completedByMode[modeIndex][branchIndex].Length; nodeIndex++) {
                if (completedByMode[modeIndex][branchIndex][nodeIndex]) {
                    count++;
                }
            }
        }
        return count;
    }

    private static int CountTotalNodes(int modeIndex) {
        RouteMap route = GetRouteByModeIndex(modeIndex);
        int count = 0;
        for (int branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
            count += route.Branches[branchIndex].Nodes.Length;
        }
        return count;
    }

    private static int CountCompletedBranches(int modeIndex) {
        EnsureRouteState();
        int count = 0;
        for (int branchIndex = 0; branchIndex < completedByMode[modeIndex].Length; branchIndex++) {
            bool branchCompleted = true;
            for (int nodeIndex = 0; nodeIndex < completedByMode[modeIndex][branchIndex].Length; nodeIndex++) {
                if (!completedByMode[modeIndex][branchIndex][nodeIndex]) {
                    branchCompleted = false;
                    break;
                }
            }
            if (branchCompleted) {
                count++;
            }
        }
        return count;
    }

    public static void Import(BinaryReader r) {
        ResetRouteState();
        EnsureRouteState();

        bool loadedCompletedState = false;
        bool loadedRewardedState = false;
        int legacyCurrentStage = 0;
        bool legacyRewardClaimed = true;
        int[] legacyCurrentStageByMode = new int[RouteMaps.Length];
        bool[] legacyRewardClaimedByMode = [true, true];
        bool loadedLegacyStagesByMode = false;
        bool loadedLegacyRewardsByMode = false;

        // 兼容旧档：旧版只有单线 stage / reward 标记，新版优先读取节点状态矩阵。
        r.ReadBlocks(
            ("CurrentStage", br => legacyCurrentStage = br.ReadInt32()),
            ("RewardClaimed", br => legacyRewardClaimed = br.ReadBoolean()),
            ("CurrentStageByMode", br => {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++) {
                    int value = br.ReadInt32();
                    if (i < legacyCurrentStageByMode.Length) {
                        legacyCurrentStageByMode[i] = value;
                    }
                }
                loadedLegacyStagesByMode = true;
            }),
            ("RewardClaimedByMode", br => {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++) {
                    bool value = br.ReadBoolean();
                    if (i < legacyRewardClaimedByMode.Length) {
                        legacyRewardClaimedByMode[i] = value;
                    }
                }
                loadedLegacyRewardsByMode = true;
            }),
            ("NodeCompletedStates", br => {
                ReadStateMatrix(br, completedByMode);
                loadedCompletedState = true;
            }),
            ("NodeRewardedStates", br => {
                ReadStateMatrix(br, rewardedByMode);
                loadedRewardedState = true;
            })
        );

        bool isLegacyImport = !loadedCompletedState || !loadedRewardedState;
        if (isLegacyImport) {
            ResetRouteState();
            if (!loadedLegacyStagesByMode) {
                legacyCurrentStageByMode[0] = legacyCurrentStage;
            }
            if (!loadedLegacyRewardsByMode) {
                legacyRewardClaimedByMode[0] = legacyRewardClaimed;
            }
            ApplyLegacyProgress(legacyCurrentStageByMode, legacyRewardClaimedByMode);
        }

        // 导入完成后按当前真实游戏状态重算节点完成度：
        // 1. 旧档首迁先静默重建完成状态，保留 legacy 节点的已领奖标记；
        // 2. 对“已完成但未领奖”的节点统一静默补发，避免把新增节点奖励吞掉；
        // 3. 已进入新系统的存档仍会为新增且未发奖节点自动补发奖励。
        EnsureRouteState();
        RefreshRouteProgress(showPopup: false, allowRewardGrant: !isLegacyImport);
        GrantPendingRewards(showPopup: false);
        ClampSelections();
    }

    public static void Export(BinaryWriter w) {
        EnsureRouteState();
        BuildLegacyExportState(0, out int legacyCurrentStage, out bool legacyRewardClaimed);
        BuildLegacyExportState(1, out int legacySpeedrunStage, out bool legacySpeedrunRewardClaimed);
        w.WriteBlocks(
            ("CurrentStage", bw => bw.Write(legacyCurrentStage)),
            ("RewardClaimed", bw => bw.Write(legacyRewardClaimed)),
            ("CurrentStageByMode", bw => {
                bw.Write(RouteMaps.Length);
                bw.Write(legacyCurrentStage);
                bw.Write(legacySpeedrunStage);
            }),
            ("RewardClaimedByMode", bw => {
                bw.Write(RouteMaps.Length);
                bw.Write(legacyRewardClaimed);
                bw.Write(legacySpeedrunRewardClaimed);
            }),
            ("NodeCompletedStates", bw => WriteStateMatrix(bw, completedByMode)),
            ("NodeRewardedStates", bw => WriteStateMatrix(bw, rewardedByMode))
        );
    }

    public static void IntoOtherSave() {
        ResetRouteState();
    }

    private static void WriteStateMatrix(BinaryWriter w, bool[][][] matrix) {
        w.Write(matrix.Length);
        for (int modeIndex = 0; modeIndex < matrix.Length; modeIndex++) {
            w.Write(matrix[modeIndex].Length);
            for (int branchIndex = 0; branchIndex < matrix[modeIndex].Length; branchIndex++) {
                w.Write(matrix[modeIndex][branchIndex].Length);
                for (int nodeIndex = 0; nodeIndex < matrix[modeIndex][branchIndex].Length; nodeIndex++) {
                    w.Write(matrix[modeIndex][branchIndex][nodeIndex]);
                }
            }
        }
    }

    private static void ReadStateMatrix(BinaryReader r, bool[][][] matrix) {
        int modeCount = r.ReadInt32();
        for (int modeIndex = 0; modeIndex < modeCount; modeIndex++) {
            int branchCount = r.ReadInt32();
            for (int branchIndex = 0; branchIndex < branchCount; branchIndex++) {
                int nodeCount = r.ReadInt32();
                for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++) {
                    bool value = r.ReadBoolean();
                    if (modeIndex < matrix.Length
                        && branchIndex < matrix[modeIndex].Length
                        && nodeIndex < matrix[modeIndex][branchIndex].Length) {
                        matrix[modeIndex][branchIndex][nodeIndex] = value;
                    }
                }
            }
        }
    }

    private static void ApplyLegacyProgress(int[] currentStageByModeLegacy, bool[] rewardClaimedByModeLegacy) {
        for (int modeIndex = 0; modeIndex < Math.Min(RouteMaps.Length, LegacyStageNodeIdsByMode.Length); modeIndex++) {
            string[] legacyNodeIds = LegacyStageNodeIdsByMode[modeIndex];
            int currentStage = Math.Max(0, currentStageByModeLegacy[modeIndex]);
            bool rewardClaimed = rewardClaimedByModeLegacy[modeIndex];

            int completedStageCount = Math.Min(currentStage, legacyNodeIds.Length);
            for (int i = 0; i < completedStageCount; i++) {
                MarkNodeById(modeIndex, legacyNodeIds[i], rewarded: true);
            }

            if (rewardClaimed && currentStage < legacyNodeIds.Length) {
                MarkNodeById(modeIndex, legacyNodeIds[currentStage], rewarded: true);
            }
        }
    }

    private static void BuildLegacyExportState(int modeIndex, out int currentStage, out bool rewardClaimed) {
        string[] legacyNodeIds =
            LegacyStageNodeIdsByMode[Math.Max(0, Math.Min(LegacyStageNodeIdsByMode.Length - 1, modeIndex))];
        currentStage = legacyNodeIds.Length;
        rewardClaimed = true;
        for (int i = 0; i < legacyNodeIds.Length; i++) {
            if (TryFindNodeIndex(modeIndex, legacyNodeIds[i], out int branchIndex, out int nodeIndex)
                && completedByMode[modeIndex][branchIndex][nodeIndex]) {
                continue;
            }

            currentStage = i;
            rewardClaimed = false;
            return;
        }
    }

    private static void MarkNodeById(int modeIndex, string nodeId, bool rewarded) {
        if (!TryFindNodeIndex(modeIndex, nodeId, out int branchIndex, out int nodeIndex)) {
            return;
        }
        completedByMode[modeIndex][branchIndex][nodeIndex] = true;
        rewardedByMode[modeIndex][branchIndex][nodeIndex] = rewarded;
    }

    private static bool TryFindNodeIndex(int modeIndex, string nodeId, out int branchIndex, out int nodeIndex) {
        RouteMap route = GetRouteByModeIndex(modeIndex);
        for (branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
            TaskBranch branch = route.Branches[branchIndex];
            for (nodeIndex = 0; nodeIndex < branch.Nodes.Length; nodeIndex++) {
                if (branch.Nodes[nodeIndex].Id == nodeId) {
                    return true;
                }
            }
        }
        branchIndex = -1;
        nodeIndex = -1;
        return false;
    }

    private const float RoutePanelWidth = 1082f;
    private const float RoutePanelHeight = 650f;
    private const float LeftColumnWidth = 156f;
    private const float StageColumnWidth = 154f;
    private const float StageHeaderHeight = 54f;
    private const float CategoryRowHeight = 54f;
    private const int NodesPerCellRow = 3;
    private const float NodeRowGap = 2f;
    private const float NodeCellBottomPadding = 10f;
    private const float NodeSize = 30f;
    private const float NodeGap = 6f;
    private const float NodeCellLeftPadding = 10f;
    private const float NodeCellTopPadding = 13f;
    private static readonly Vector2 NodeTipOffset = new(15f, -50f);

    private static readonly Color RoutePanelFillColor = new(20f / 255f, 24f / 255f, 30f / 255f, 0.5f);
    private static readonly Color RoutePanelBorderColor = new(1f, 1f, 1f, 0.10f);
    private static readonly Color LockedNodeColor = new(1f, 1f, 1f, 0.22f);
    private static readonly Color AvailableNodeColor = new(0.62f, 0.8f, 1f, 0.92f);
    private static readonly Color CompletedNodeColor = new(1f, 0.72f, 0.31f, 1f);
    private static readonly Color RowOddColor = new(1f, 1f, 1f, 0.035f);
    private static readonly Color RowEvenColor = new(1f, 1f, 1f, 0.018f);
    private static readonly Color HeaderFillColor = new(0.05f, 0.07f, 0.11f, 0.72f);
    private static readonly Color NodeBgLocked = new(0.08f, 0.10f, 0.14f, 0.55f);
    private static readonly Color NodeBgAvailable = new(0.08f, 0.16f, 0.28f, 0.7f);
    private static readonly Color NodeBgCompleted = new(0.18f, 0.13f, 0.05f, 0.78f);
    private static readonly Color NodeBorderSelected = new(1f, 0.72f, 0.31f, 0.72f);
    private static readonly Color NodeBorderAvailable = new(0.42f, 0.73f, 1f, 0.28f);

    /// <summary>
    /// 路线图视图缓存。
    /// </summary>
    private sealed class RouteViewCache {
        public RectTransform Root;
        public RectTransform ScrollContent;
        public ScrollRect Scroll;
        public Text[] BranchLabels;
        public Text[] StageLabels;
        public NodeView[][] NodeViews;
    }

    /// <summary>
    /// 任务节点视图引用。
    /// </summary>
    private sealed class NodeView {
        public MyImageButton Button;
        public Image Background;
        public Image BackgroundBorder;
        public int BranchIndex;
        public int NodeIndex;
    }

    /// <summary>
    /// 任务节点视觉状态。
    /// </summary>
    private enum NodeVisualState {
        Locked,
        Available,
        Completed,
    }

    // partial 类静态字段的初始化顺序不稳定，不能在这里提前依赖 RouteMaps。
    private static RouteViewCache[] routeViewsByMode = [];

    private static void BuildMilestonePage(MyWindow wnd) {
        BuildLayout(wnd, tab,
            Grid(
                rows: [Px(70f), Px(RoutePanelHeight)],
                rowGap: 12f,
                children: [
                    Grid(pos: (0, 0), rows: [Px(34f), Px(24f)], cols: [Px(250f), Px(260f), Fr(1)],
                        children: [
                            TextNode("主线里程碑", 20, Orange,
                                onBuilt: text => {
                                    txtModeTitle = text;
                                    text.supportRichText = true;
                                },
                                pos: (0, 0), objectName: "txt-main-task-mode"),
                            TextNode("动态刷新", 13,
                                onBuilt: text => {
                                    txtOverallSummary = text;
                                    text.supportRichText = true;
                                },
                                pos: (1, 0), objectName: "txt-main-task-overall"),
                            TextNode("动态刷新", 13,
                                onBuilt: text => {
                                    txtBranchSummary = text;
                                    text.supportRichText = true;
                                },
                                pos: (1, 1), objectName: "txt-main-task-branch"),
                            TextNode("节点详情-推荐说明", 13, Gray,
                                onBuilt: text => text.supportRichText = true,
                                pos: (1, 2), objectName: "txt-main-task-hint"),
                        ]),
                    Grid(pos: (1, 0), objectName: "main-task-route-panel",
                        onBuilt: root => {
                            roadmapPanel = root;
                            AddRoundedPanel(root, RoutePanelFillColor, RoutePanelBorderColor);
                        }),
                ]));
    }

    private static void AddRoundedPanel(RectTransform root, Color fillColor, Color borderColor) {
        Image fill = root.gameObject.AddComponent<Image>();
        fill.sprite = RoundedSpriteFactory.GetFillSprite();
        fill.type = Image.Type.Sliced;
        fill.color = fillColor;
        fill.raycastTarget = false;

        var borderObj = new GameObject("border", typeof(RectTransform), typeof(Image));
        RectTransform borderRect = borderObj.GetComponent<RectTransform>();
        borderRect.SetParent(root, false);
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;
        borderRect.localScale = Vector3.one;

        Image borderImg = borderObj.GetComponent<Image>();
        borderImg.sprite = RoundedSpriteFactory.GetBorderSprite();
        borderImg.type = Image.Type.Sliced;
        borderImg.color = borderColor;
        borderImg.raycastTarget = false;
    }

    private static void RefreshMilestonePage() {
        int modeIndex = GetModeIndex();
        EnsureRouteViewCacheCapacity();
        EnsureRouteViewBuilt(modeIndex);

        for (int i = 0; i < routeViewsByMode.Length; i++) {
            if (routeViewsByMode[i]?.Root != null) {
                routeViewsByMode[i].Root.gameObject.SetActive(i == modeIndex);
            }
        }

        RouteMap route = GetRouteByModeIndex(modeIndex);
        int completedNodes = CountCompletedNodes(modeIndex);
        int totalNodes = CountTotalNodes(modeIndex);
        int completedBranches = CountCompletedBranches(modeIndex);

        txtModeTitle.text = route.RouteName.Translate().WithColor(Orange);
        txtOverallSummary.text = string.Format("路线总进度".Translate(), completedNodes, totalNodes).WithColor(Orange);
        txtBranchSummary.text =
            string.Format("分支完成数".Translate(), completedBranches, route.Branches.Length).WithColor(Blue);

        EnsureSelectedNode(modeIndex);
        RefreshRouteView(modeIndex);
    }

    private static void EnsureRouteViewCacheCapacity() {
        if (routeViewsByMode == null || routeViewsByMode.Length != RouteMaps.Length) {
            routeViewsByMode = new RouteViewCache[RouteMaps.Length];
        }
    }

    private static void EnsureRouteViewBuilt(int modeIndex) {
        if (routeViewsByMode[modeIndex] != null) {
            return;
        }

        RouteMap route = GetRouteByModeIndex(modeIndex);
        RectTransform root = CreateFillRect($"main-task-route-root-{modeIndex}", roadmapPanel);
        root.gameObject.SetActive(false);

        RouteViewCache cache = new() {
            Root = root,
            BranchLabels = new Text[route.Branches.Length],
            StageLabels = new Text[route.Stages.Length],
            NodeViews = new NodeView[route.Branches.Length][],
        };
        float[] branchRowTops = BuildBranchRowTops(route, out float contentHeight);

        RectTransform leftRoot = CreatePanelRect("main-task-left-fixed", root, 10f, 10f, LeftColumnWidth - 10f,
            RoutePanelHeight - 20f, Color.clear);
        AddRowBackground(leftRoot, 0f, 0f, HeaderFillColor, LeftColumnWidth, StageHeaderHeight);
        Text categoryTitle = MyWindow.AddText(12f, 18f, leftRoot, "类别".Translate(), 14,
            $"txt-main-task-category-title-{modeIndex}");
        categoryTitle.color = Orange;
        categoryTitle.supportRichText = true;

        for (int branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
            float y = branchRowTops[branchIndex];
            float rowHeight = GetBranchRowHeight(route, branchIndex);
            AddRowBackground(leftRoot, 0f, y, branchIndex % 2 == 0 ? RowEvenColor : RowOddColor, LeftColumnWidth,
                rowHeight);
            TaskBranch branch = route.Branches[branchIndex];
            Text branchLabel = MyWindow.AddText(12f, y + rowHeight / 2f, leftRoot, branch.Name.Translate(), 13,
                $"txt-main-task-branch-{modeIndex}-{branchIndex}");
            branchLabel.supportRichText = true;
            branchLabel.color = White;
            cache.BranchLabels[branchIndex] = branchLabel;
        }

        RectTransform viewport = CreateViewport("main-task-scroll-viewport", root, LeftColumnWidth, 10f,
            RoutePanelWidth - LeftColumnWidth - 10f, RoutePanelHeight - 20f);
        float contentWidth = Math.Max(RoutePanelWidth - LeftColumnWidth - 12f, route.Stages.Length * StageColumnWidth);
        RectTransform content = CreateScrollContent("main-task-scroll-content", viewport, contentWidth,
            contentHeight);
        cache.ScrollContent = content;

        ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = true;
        scroll.vertical = true;
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 34f;
        scroll.inertia = true;
        scroll.decelerationRate = 0.135f;
        cache.Scroll = scroll;

        BuildStageHeaders(route, cache, content, modeIndex);
        BuildMatrixRows(route, content, branchRowTops);
        BuildNodeViews(route, cache, content, modeIndex, branchRowTops);

        routeViewsByMode[modeIndex] = cache;
    }

    private static void BuildStageHeaders(RouteMap route, RouteViewCache cache, RectTransform content, int modeIndex) {
        AddRowBackground(content, 0f, 0f, HeaderFillColor, route.Stages.Length * StageColumnWidth, StageHeaderHeight);
        for (int stageIndex = 0; stageIndex < route.Stages.Length; stageIndex++) {
            StageColumn stage = route.Stages[stageIndex];
            float x = stageIndex * StageColumnWidth + 10f;
            MyImageButton icon = MyImageButton.CreateImageButton(x, 12f, content,
                LDB.items.Exist(stage.IconItemId) ? LDB.items.Select(stage.IconItemId) : null, 28f, 28f);
            icon.backgroundImage.color = Color.clear;
            icon.countText.gameObject.SetActive(false);

            Text label = MyWindow.AddText(x + 36f, 17f, content, stage.Name.Translate(), 13,
                $"txt-main-task-stage-{modeIndex}-{stageIndex}");
            label.supportRichText = true;
            label.color = Orange;
            cache.StageLabels[stageIndex] = label;
        }
    }

    private static void BuildMatrixRows(RouteMap route, RectTransform content, float[] branchRowTops) {
        float contentWidth = route.Stages.Length * StageColumnWidth;
        for (int branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
            float y = branchRowTops[branchIndex];
            float rowHeight = GetBranchRowHeight(route, branchIndex);
            AddRowBackground(content, 0f, y, branchIndex % 2 == 0 ? RowEvenColor : RowOddColor, contentWidth,
                rowHeight);
            for (int stageIndex = 1; stageIndex < route.Stages.Length; stageIndex++) {
                AddColumnSeparator(content, stageIndex * StageColumnWidth, y, rowHeight);
            }
        }
    }

    private static void BuildNodeViews(RouteMap route, RouteViewCache cache, RectTransform content, int modeIndex,
        float[] branchRowTops) {
        for (int branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
            TaskBranch branch = route.Branches[branchIndex];
            cache.NodeViews[branchIndex] = new NodeView[branch.Nodes.Length];
            for (int nodeIndex = 0; nodeIndex < branch.Nodes.Length; nodeIndex++) {
                TaskNode node = branch.Nodes[nodeIndex];
                int stageIndex = Math.Max(0, Math.Min(route.Stages.Length - 1, node.StageIndex));
                int cellIndex = CountPreviousNodesInCell(branch, nodeIndex, stageIndex);
                int cellColumn = cellIndex % NodesPerCellRow;
                int cellRow = cellIndex / NodesPerCellRow;
                float x = stageIndex * StageColumnWidth + NodeCellLeftPadding + cellColumn * (NodeSize + NodeGap);
                float y = branchRowTops[branchIndex]
                          + NodeCellTopPadding
                          + cellRow * (NodeSize + NodeRowGap);

                float bgSize = NodeSize + 6f;
                Image nodeBg = new GameObject("node-bg", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                nodeBg.sprite = RoundedSpriteFactory.GetFillSprite();
                nodeBg.type = Image.Type.Sliced;
                nodeBg.color = NodeBgLocked;
                nodeBg.raycastTarget = false;
                NormalizeRectWithTopLeft(nodeBg, x - 3f, y - 3f, content);
                nodeBg.rectTransform.sizeDelta = new Vector2(bgSize, bgSize);

                Image nodeBorder =
                    new GameObject("node-border", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                nodeBorder.sprite = RoundedSpriteFactory.GetBorderSprite();
                nodeBorder.type = Image.Type.Sliced;
                nodeBorder.color = Color.clear;
                nodeBorder.raycastTarget = false;
                NormalizeRectWithTopLeft(nodeBorder, x - 3f, y - 3f, content);
                nodeBorder.rectTransform.sizeDelta = new Vector2(bgSize, bgSize);

                int capturedModeIndex = modeIndex;
                int capturedBranchIndex = branchIndex;
                int capturedNodeIndex = nodeIndex;
                MyImageButton nodeButton = MyImageButton.CreateImageButton(x, y, content,
                    LDB.items.Exist(node.IconItemId) ? LDB.items.Select(node.IconItemId) : null, NodeSize, NodeSize);
                nodeButton.gameObject.name = $"btn-main-task-node-{modeIndex}-{branchIndex}-{nodeIndex}";
                nodeButton.spriteImage.raycastTarget = true;
                nodeButton.backgroundImage.raycastTarget = false;
                nodeButton.countText.gameObject.SetActive(false);
                nodeButton.backgroundImage.color = Color.clear;
                nodeButton.WithClickEvent(() => SelectNode(capturedModeIndex, capturedBranchIndex, capturedNodeIndex),
                    () => SelectNode(capturedModeIndex, capturedBranchIndex, capturedNodeIndex));
                AttachHoverSelection(nodeButton,
                    () => SelectNode(capturedModeIndex, capturedBranchIndex, capturedNodeIndex));

                cache.NodeViews[branchIndex][nodeIndex] = new NodeView {
                    Button = nodeButton,
                    Background = nodeBg,
                    BackgroundBorder = nodeBorder,
                    BranchIndex = branchIndex,
                    NodeIndex = nodeIndex,
                };
            }
        }
    }

    private static float[] BuildBranchRowTops(RouteMap route, out float contentHeight) {
        float[] rowTops = new float[route.Branches.Length];
        float y = StageHeaderHeight;
        for (int branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
            rowTops[branchIndex] = y;
            y += GetBranchRowHeight(route, branchIndex);
        }
        contentHeight = Math.Max(RoutePanelHeight - 20f, y);
        return rowTops;
    }

    private static float GetBranchRowHeight(RouteMap route, int branchIndex) {
        int maxCellRows = 1;
        for (int stageIndex = 0; stageIndex < route.Stages.Length; stageIndex++) {
            int nodeCount = CountNodesInCell(route.Branches[branchIndex], stageIndex);
            if (nodeCount > 0) {
                maxCellRows = Math.Max(maxCellRows, (nodeCount + NodesPerCellRow - 1) / NodesPerCellRow);
            }
        }
        return Math.Max(CategoryRowHeight,
            NodeCellTopPadding + maxCellRows * NodeSize + (maxCellRows - 1) * NodeRowGap + NodeCellBottomPadding);
    }

    private static int CountNodesInCell(TaskBranch branch, int stageIndex) {
        int count = 0;
        for (int i = 0; i < branch.Nodes.Length; i++) {
            if (branch.Nodes[i].StageIndex == stageIndex) {
                count++;
            }
        }
        return count;
    }

    private static int CountPreviousNodesInCell(TaskBranch branch, int nodeIndex, int stageIndex) {
        int count = 0;
        for (int i = 0; i < nodeIndex; i++) {
            if (branch.Nodes[i].StageIndex == stageIndex) {
                count++;
            }
        }
        return count;
    }

    private static void RefreshRouteView(int modeIndex) {
        RouteMap route = GetRouteByModeIndex(modeIndex);
        RouteViewCache cache = routeViewsByMode[modeIndex];
        int selectedBranch = selectedBranchByMode[modeIndex];
        int selectedNode = selectedNodeByMode[modeIndex];

        for (int branchIndex = 0; branchIndex < route.Branches.Length; branchIndex++) {
            bool branchCompleted = true;
            for (int nodeIndex = 0; nodeIndex < route.Branches[branchIndex].Nodes.Length; nodeIndex++) {
                if (!completedByMode[modeIndex][branchIndex][nodeIndex]) {
                    branchCompleted = false;
                    break;
                }
            }
            cache.BranchLabels[branchIndex].color = branchCompleted ? Orange : White;

            for (int nodeIndex = 0; nodeIndex < route.Branches[branchIndex].Nodes.Length; nodeIndex++) {
                TaskNode node = route.Branches[branchIndex].Nodes[nodeIndex];
                NodeView view = cache.NodeViews[branchIndex][nodeIndex];
                NodeVisualState visualState = GetNodeVisualState(modeIndex, branchIndex, nodeIndex);
                UpdateNodeVisual(view, visualState, branchIndex == selectedBranch && nodeIndex == selectedNode);
                UpdateNodeTip(view.Button, node, modeIndex, branchIndex, nodeIndex);
            }
        }
    }

    private static void UpdateNodeVisual(NodeView view, NodeVisualState visualState, bool selected) {
        Color iconColor = visualState switch {
            NodeVisualState.Completed => CompletedNodeColor,
            NodeVisualState.Available => AvailableNodeColor,
            _ => LockedNodeColor,
        };
        view.Button.spriteImage.color = iconColor;
        view.Button.backgroundImage.color = Color.clear;

        if (view.Background != null) {
            view.Background.color = visualState switch {
                NodeVisualState.Completed => NodeBgCompleted,
                NodeVisualState.Available => NodeBgAvailable,
                _ => NodeBgLocked,
            };
        }

        if (view.BackgroundBorder != null) {
            view.BackgroundBorder.color = selected ? NodeBorderSelected :
                visualState == NodeVisualState.Available ? NodeBorderAvailable : Color.clear;
        }
    }

    private static void UpdateNodeTip(MyImageButton button, TaskNode node, int modeIndex, int branchIndex,
        int nodeIndex) {
        RouteMap route = GetRouteByModeIndex(modeIndex);
        string progressText = GetNodeProgressText(modeIndex, branchIndex, nodeIndex);
        string rewardText = GetRewardText(node);
        string stageName = route.Stages[Math.Max(0, Math.Min(route.Stages.Length - 1, node.StageIndex))].Name
            .Translate();

        button.uiButton.tips.type = UIButton.ItemTipType.Other;
        button.uiButton.tips.itemId = 0;
        button.uiButton.tips.topLevel = true;
        button.uiButton.tips.delay = 0.25f;
        button.uiButton.tips.corner = 7;
        button.uiButton.tips.offset = NodeTipOffset;
        button.uiButton.tips.tipTitle = node.Name.Translate();
        button.uiButton.tips.tipText =
            $"{node.Desc.Translate()}\n\n{"节点详情-推荐阶段".Translate()} {stageName}\n{"节点详情-条件".Translate()} {progressText}\n{"节点详情-奖励".Translate()} {rewardText}\n{"节点详情-状态".Translate()} {GetNodeStateText(modeIndex, branchIndex, nodeIndex)}\n\n{"节点详情-推荐说明".Translate().WithColor(Gray)}";
        button.uiButton.UpdateTip();
    }

    private static string GetNodeProgressText(int modeIndex, int branchIndex, int nodeIndex) {
        if (completedByMode[modeIndex][branchIndex][nodeIndex]) {
            return "节点状态-已完成".Translate();
        }
        try {
            return GetRouteByModeIndex(modeIndex).Branches[branchIndex].Nodes[nodeIndex].ProgressText();
        }
        catch (Exception ex) {
            return $"条件检查失败：{ex.Message}";
        }
    }

    private static string GetNodeStateText(int modeIndex, int branchIndex, int nodeIndex) {
        return GetNodeVisualState(modeIndex, branchIndex, nodeIndex) switch {
            NodeVisualState.Completed => "节点状态-已完成".Translate(),
            NodeVisualState.Available => "节点状态-进行中".Translate(),
            _ => "节点状态-未解锁".Translate(),
        };
    }

    private static string GetRewardText(TaskNode node) {
        if (node.RewardItemId > 0 && LDB.items.Exist(node.RewardItemId)) {
            return $"{LDB.items.Select(node.RewardItemId).name} x{node.RewardCount}";
        }
        return "无".Translate();
    }

    private static void SelectNode(int modeIndex, int branchIndex, int nodeIndex) {
        if (modeIndex < 0
            || modeIndex >= RouteMaps.Length
            || modeIndex >= selectedBranchByMode.Length
            || modeIndex >= selectedNodeByMode.Length) {
            return;
        }

        RouteMap route = GetRouteByModeIndex(modeIndex);
        if (branchIndex < 0 || branchIndex >= route.Branches.Length) {
            return;
        }
        if (nodeIndex < 0 || nodeIndex >= route.Branches[branchIndex].Nodes.Length) {
            return;
        }

        selectedBranchByMode[modeIndex] = branchIndex;
        selectedNodeByMode[modeIndex] = nodeIndex;
        if (modeIndex == GetModeIndex()) {
            RefreshRouteView(modeIndex);
        }
    }

    private static void AttachHoverSelection(MyImageButton button, Action onHover) {
        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null) {
            trigger = button.gameObject.AddComponent<EventTrigger>();
        }
        EventTrigger.Entry pointerEnter = new() { eventID = EventTriggerType.PointerEnter };
        pointerEnter.callback.AddListener(_ => onHover?.Invoke());
        trigger.triggers.Add(pointerEnter);
    }

    private static void AddRowBackground(RectTransform parent, float left, float top, Color color,
        float width = LeftColumnWidth, float height = CategoryRowHeight) {
        Image image = new GameObject("row-bg", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        NormalizeRectWithTopLeft(image, left, top, parent);
        image.rectTransform.sizeDelta = new Vector2(width, height);
    }

    private static void AddColumnSeparator(RectTransform parent, float left, float top, float height) {
        Image image = new GameObject("column-separator", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.06f);
        image.raycastTarget = false;
        NormalizeRectWithTopLeft(image, left, top + 6f, parent);
        image.rectTransform.sizeDelta = new Vector2(1f, height - 12f);
    }

    private static RectTransform CreatePanelRect(string name, RectTransform parent, float left, float top, float width,
        float height, Color color) {
        Image image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        NormalizeRectWithTopLeft(image, left, top, parent);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static RectTransform CreateViewport(string name, RectTransform parent, float left, float top, float width,
        float height) {
        var obj = new GameObject(name, typeof(RectTransform), typeof(RectMask2D));
        RectTransform rect = obj.GetComponent<RectTransform>();
        NormalizeRectWithTopLeft(rect, left, top, parent);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static RectTransform CreateScrollContent(string name, RectTransform parent, float width, float height) {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
        return rect;
    }

    private static RectTransform CreateFillRect(string name, RectTransform parent) {
        RectTransform rect = new GameObject(name).AddComponent<RectTransform>();
        NormalizeRectWithMargin(rect, 0f, 0f, 0f, 0f, parent);
        return rect;
    }
}
