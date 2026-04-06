using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
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
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        tab = wnd.AddTab(trans, "主线任务");
        routeViewsByMode = new RouteViewCache[RouteMaps.Length];
        BuildMilestonePage(wnd);
        RefreshMilestonePage();
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        RefreshMilestonePage();
    }
}
