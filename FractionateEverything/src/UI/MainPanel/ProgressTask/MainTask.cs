using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Components.GridDsl;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.ProgressTask;

public static partial class MainTask {
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
}
