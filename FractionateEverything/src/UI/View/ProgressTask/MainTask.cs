using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Components.GridDsl;
using static FE.Utils.Utils;

namespace FE.UI.View.ProgressTask;

public static partial class MainTask {
    private static RectTransform tab;
    private static RectTransform roadmapPanel;
    private static RectTransform centerPanel;
    private static RectTransform detailPanel;

    private static Text txtModeTitle;
    private static Text txtOverallSummary;
    private static Text txtBranchSummary;
    private static Text txtCenterTitle;
    private static Text txtCenterSummary;
    private static Text txtDetailBranch;
    private static Text txtDetailName;
    private static Text txtDetailDesc;
    private static Text txtDetailCondition;
    private static Text txtDetailReward;
    private static Text txtDetailState;
    private static MyImageButton btnDetailRewardIcon;

    public static void AddTranslations() {
        Register("主线任务", "Main Task");
        Register("主线里程碑", "Main Milestones", "主线里程碑");
        Register("路线总进度", "Route progress: {0}/{1}", "路线进度：{0}/{1}");
        Register("分支完成数", "Branches complete: {0}/{1}", "分支完成：{0}/{1}");
        Register("节点状态-未解锁", "Locked", "未解锁");
        Register("节点状态-进行中", "In Progress", "进行中");
        Register("节点状态-已完成", "Completed", "已完成");
        Register("节点详情-条件", "Condition:", "条件：");
        Register("节点详情-奖励", "Reward:", "奖励：");
        Register("节点详情-状态", "State:", "状态：");
        Register("节点详情-前置未完成", "Previous milestone not completed", "前置里程碑未完成");
        Register("主线里程碑达成提示", "Main milestone unlocked: {0}", "主线里程碑达成：{0}");
        Register("是", "Yes");
        Register("否", "No");
        Register("无", "None", "无");
        Register("分馏次数", "Fractionations", "分馏次数");
        Register("科技解锁", "Tech unlocked", "科技解锁");
        Register("建筑等级", "Building level", "建筑等级");
        Register("解锁配方", "Unlocked recipes", "解锁配方");
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
            ("基础起步线", "Bootstrap Branch"),
            ("开线扩张线", "Opening Expansion Branch"),
            ("建筑工艺线", "Craft Branch"),
            ("黑雾终盘线", "Dark Fog Endgame Branch"),
            ("速启线", "Rapid Start Branch"),
            ("开线冲刺线", "Opening Sprint Branch"),
            ("核心产能线", "Core Output Branch"),
            ("收官冲刺线", "Final Sprint Branch"),
            ("分馏启示", "Fractionation Insight"),
            ("初次量产", "First Throughput"),
            ("万物之始", "Origin of Everything"),
            ("分馏热潮", "Fractionation Surge"),
            ("开线热身", "Opening Warmup"),
            ("开线之门", "Opening Gate"),
            ("开线热手", "Opening Hot Hand"),
            ("开线先锋", "Opening Pioneer"),
            ("开线专家", "Opening Expert"),
            ("矿物新生", "Mineral Awakening"),
            ("原胚萌发", "Proto Sprout"),
            ("物品转化", "Item Conversion"),
            ("工艺稳态", "Craft Stability"),
            ("工艺优化", "Craft Optimization"),
            ("精馏经济", "Rectification Economy"),
            ("星际互联", "Interstellar Link"),
            ("黑雾信号", "Dark Fog Signal"),
            ("黑雾接战", "Dark Fog Engagement"),
            ("配方扩编", "Recipe Expansion"),
            ("工艺总览", "Craft Overview"),
            ("万物归一", "All into One"),
            ("速通启程", "Speedrun Departure"),
            ("速通量产", "Speedrun Throughput"),
            ("速通效率", "Speedrun Efficiency"),
            ("高速闭环", "High-speed Loop"),
            ("开线预热", "Opening Warmup"),
            ("极速开线", "Rapid Opening"),
            ("开线推进", "Opening Push"),
            ("开线冲刺", "Opening Sprint"),
            ("矿物快启", "Rapid Mineral Start"),
            ("转化连锁", "Conversion Chain"),
            ("精馏冲刺", "Rectification Sprint"),
            ("星际跃迁", "Interstellar Leap"),
            ("速通接敌", "Speedrun First Contact"),
            ("地面压制", "Ground Suppression"),
            ("配方冲线", "Recipe Finish Push"),
            ("速通闭环", "Speedrun Closure"),
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
                    FE.UI.Components.GridDsl.Node(pos: (0, 0), objectName: "main-task-root", build: (w, root) => {
                        tab = root;
                        routeViewsByMode = new RouteViewCache[RouteMaps.Length];
                        BuildMilestonePage(w);
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
