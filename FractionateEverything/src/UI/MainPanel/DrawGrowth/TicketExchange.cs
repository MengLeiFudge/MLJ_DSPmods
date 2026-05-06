using System.Linq;
using System.IO;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.Logic.Fractionation.Growth;
using FE.UI.Controls;
using FE.UI.MainPanel;
using UnityEngine;
using UnityEngine.UI;
using FE.Logic.DarkFog;
using FE.Logic.Gacha;
using static FE.UI.Layout.GridDsl;
using static FE.Utils.Utils;
using static FE.Logic.DataCenter.PlayerInventoryAccess;
using FE.UI.Foundation.Window;
using FE.UI.MainPanel.Theme;

namespace FE.UI.MainPanel.DrawGrowth;
/// <summary>
/// 抽取奖券兑换与模式说明页面。
/// </summary>
public static class TicketExchange {
    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static Text txtOverview;
    private static Text txtMode;
    private static Text txtCostOpening;
    private static Text txtCostProto;
    private static Text txtCostFocus;
    private static Text txtDarkFogStatus;
    private static Text txtResourceTitle;
    private static Text txtCostTitle;
    private static Text txtDarkFogTitle;
    private static MyImageButton btnCurrentMatrix;
    private static MyImageButton btnFragment;
    private static MyImageButton btnDarkFogMatrix;

    public static void AddTranslations() {
        Register("抽取总览", "Draw Overview");
        Register("抽取总览说明",
            "Review draw costs, current resources, focus switch costs and Dark Fog branch progress.",
            "查看抽取成本、当前资源、聚焦切换成本与黑雾支线进度。");
        Register("开线池成本", "Opening Pool Cost");
        Register("原胚池成本", "Proto Pool Cost");
        Register("聚焦切换成本", "Focus Switch Cost");
        Register("黑雾支线说明", "Dark Fog Branch", "黑雾支线");
        Register("前往成长规划", "Go Growth Planning");
        Register("前往市场板", "Go Market Board");
        Register("当前资源", "Current Resources", "当前资源");
        Register("核心成本", "Core Costs", "核心成本");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        tab = trans;
        BuildLayout(wnd, tab,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), Px(180f), 1, Px(PageLayout.FooterHeight)],
                rowGap: PageLayout.Gap,
                children: [
                    Header("抽取总览", objectName: "ticket-exchange-header", pos: (0, 0), onBuilt: refs => {
                        header = refs;
                        txtOverview = refs.Summary;
                    }),
                    Grid(
                        pos: (1, 0),
                        cols: [1, 1],
                        columnGap: PageLayout.Gap,
                        children: [
                            ContentCard(pos: (0, 0), objectName: "ticket-exchange-resource-card", strong: true,
                                rows: [Px(24f), Px(50f), 1],
                                children: [
                                    CardTitleNode("当前资源", onBuilt: text => txtResourceTitle = text,
                                        pos: (0, 0), objectName: "ticket-exchange-resource-title"),
                                    Grid(
                                        pos: (1, 0),
                                        cols: [1, 1, 1],
                                        columnGap: PageLayout.InnerGap,
                                        children: [
                                            ImageButtonNode(size: 40f, onBuilt: btn => btnCurrentMatrix = btn,
                                                pos: (0, 0), objectName: "ticket-exchange-current-matrix"),
                                            ImageButtonNode(LDB.items.Select(IFE残片), 40f, onBuilt: btn => btnFragment = btn,
                                                pos: (0, 1), objectName: "ticket-exchange-fragment"),
                                            ImageButtonNode(LDB.items.Select(I黑雾矩阵), 40f,
                                                onBuilt: btn => btnDarkFogMatrix = btn,
                                                pos: (0, 2), objectName: "ticket-exchange-darkfog-matrix"),
                                        ]),
                                    TextNode("", 13, onBuilt: text => txtMode = text,
                                        pos: (2, 0), objectName: "ticket-exchange-mode"),
                                ]),
                            ContentCard(pos: (0, 1), objectName: "ticket-exchange-cost-card", strong: true,
                                rows: [Px(24f), 1, 1, 2],
                                rowGap: 6f,
                                children: [
                                    CardTitleNode("核心成本", onBuilt: text => txtCostTitle = text,
                                        pos: (0, 0), objectName: "ticket-exchange-cost-title"),
                                    TextNode("", 13, wrap: true, onBuilt: text => txtCostOpening = text,
                                        pos: (1, 0), objectName: "ticket-exchange-cost-opening"),
                                    TextNode("", 13, wrap: true, onBuilt: text => txtCostProto = text,
                                        pos: (2, 0), objectName: "ticket-exchange-cost-proto"),
                                    TextNode("", 13, anchor: TextAnchor.UpperLeft, wrap: true,
                                        onBuilt: text => txtCostFocus = text,
                                        pos: (3, 0), objectName: "ticket-exchange-cost-focus"),
                                ]),
                        ]),
                    ContentCard(pos: (2, 0), objectName: "ticket-exchange-darkfog-card",
                        rows: [Px(24f), 1],
                        children: [
                            CardTitleNode("黑雾支线说明", onBuilt: text => txtDarkFogTitle = text,
                                pos: (0, 0), objectName: "ticket-exchange-darkfog-title"),
                            TextNode("", 13, anchor: TextAnchor.UpperLeft, wrap: true,
                                onBuilt: text => txtDarkFogStatus = text,
                                pos: (1, 0), objectName: "ticket-exchange-darkfog-status"),
                        ]),
                    FooterCard(pos: (3, 0), objectName: "ticket-exchange-footer-card",
                        cols: [1, 1, 4],
                        columnGap: PageLayout.InnerGap,
                        children: [
                            ButtonNode("前往成长规划",
                                onClick: () =>
                                    MainWindow.NavigateToPage(MainWindowPageRegistry.DrawGrowthCategoryName, 2),
                                pos: (0, 0), objectName: "ticket-exchange-go-growth"),
                            ButtonNode("前往市场板",
                                onClick: () =>
                                    MainWindow.NavigateToPage(MainWindowPageRegistry.ResourceInteractionCategoryName, 2),
                                pos: (0, 1), objectName: "ticket-exchange-go-market"),
                        ]),
                ]));
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        int matrixId = GachaService.GetCurrentDrawMatrixId();
        btnCurrentMatrix.Proto = LDB.items.Select(matrixId);
        btnCurrentMatrix.SetCount(GetItemTotalCount(matrixId));
        btnFragment.SetCount(GetItemTotalCount(IFE残片));
        btnDarkFogMatrix.SetCount(GetItemTotalCount(I黑雾矩阵));
        header.Title.text = "抽取总览".Translate().WithColor(Orange);
        txtOverview.text = $"{"抽取总览说明".Translate()}";
        txtResourceTitle.text = "当前资源".Translate().WithColor(Orange);
        txtCostTitle.text = "核心成本".Translate().WithColor(Orange);
        txtDarkFogTitle.text = "黑雾支线说明".Translate().WithColor(Orange);
        txtMode.text = $"当前模式：{GachaService.GetModeNameKey().Translate()}";
        txtCostOpening.text =
            $"{"开线池成本".Translate()}：x{GachaService.GetDrawMatrixCost(GachaPool.PoolIdOpeningLine, 1)} / 抽";
        txtCostProto.text =
            $"{"原胚池成本".Translate()}：x{GachaService.GetDrawMatrixCost(GachaPool.PoolIdProtoLoop, 1)} / 抽";
        txtCostFocus.text =
            $"{"聚焦切换成本".Translate()}：残片 x{GachaService.GetFocusSwitchFragmentCost(GachaFocusType.MineralExpansion)} 起    成长积分统一进入成长池";
        txtDarkFogStatus.text = BuildDarkFogStatusText();
    }

    private static string BuildDarkFogStatusText() {
        EDarkFogCombatStage stage = DarkFogCombatManager.GetCurrentStage();
        var snapshots = RecipeGrowthQueries.GetDarkFogProgressSnapshots();
        int totalRecipes = snapshots.Count;
        int unlockedRecipes = snapshots.Count(snapshot => snapshot.IsUnlocked);
        int maxedRecipes = snapshots.Count(snapshot => snapshot.IsMaxed);
        string stageName = stage switch {
            EDarkFogCombatStage.Dormant => "休眠观察".WithColor(Orange),
            EDarkFogCombatStage.Signal => "信号接触".WithColor(Blue),
            EDarkFogCombatStage.GroundSuppression => "地面压制".WithColor(Green),
            EDarkFogCombatStage.StellarHunt => "星域围猎".WithColor(Blue),
            _ => "奇点收束".WithColor(Gold),
        };
        string enhancedText = !DarkFogCombatManager.IsEnhancedLayerEnabled()
            ? "未接入".WithColor(Orange)
            : $"节点 {DarkFogCombatManager.GetEnhancedNodeCount()}/4    遗物 {DarkFogCombatManager.GetRelicCount()}    Rank {DarkFogCombatManager.GetMeritRank()}    技能 {DarkFogCombatManager.GetAssignedSkillPointCount()}"
                .WithColor(Green);
        string nextTarget = stage switch {
            EDarkFogCombatStage.Dormant when !DarkFogCombatManager.IsCombatModeEnabled() => "启用战斗模式".WithColor(Orange),
            EDarkFogCombatStage.Dormant when DarkFogCombatManager.GetProgressStageIndex() < 3 =>
                $"解锁 {LDB.items.Select(I信息矩阵).name}".WithColor(Orange),
            EDarkFogCombatStage.Dormant => "建立黑雾矩阵库存或首次接触黑雾掉落".WithColor(Blue),
            EDarkFogCombatStage.Signal => $"{LDB.items.Select(I引力矩阵).name} + 物资层级 2/4".WithColor(Blue),
            EDarkFogCombatStage.GroundSuppression => $"{LDB.items.Select(I宇宙矩阵).name} + 物资层级 3/4 或接触蜂巢".WithColor(Blue),
            EDarkFogCombatStage.StellarHunt when DarkFogCombatManager.IsEnhancedLayerEnabled() => "物资层级 4/4 或增强节点 2/4"
                .WithColor(Gold),
            EDarkFogCombatStage.StellarHunt => "物资层级 4/4".WithColor(Gold),
            _ => "已到最终阶段".WithColor(Gold),
        };

        return $"{"黑雾支线说明".Translate()}：阶段 {stageName}\n"
               + $"战况：地面基地 {DarkFogCombatManager.GetAliveGroundBaseCount()}    星域蜂巢 {DarkFogCombatManager.GetAliveHiveCount()}    物资层级 {DarkFogCombatManager.GetDarkFogResourceTier()}/4\n"
               + $"成长页报价 {DarkFogCombatManager.GetUnlockedGrowthOfferCount()} 项    市场板特单 {DarkFogCombatManager.GetUnlockedSpecialOrderCount()} 条    黑雾配方 {unlockedRecipes}/{totalRecipes} 已解锁，满级 {maxedRecipes}    增强层 {enhancedText}\n"
               + $"下一阶段：{nextTarget}";
    }

    public static void Import(BinaryReader r) {
        r.ReadBlocks();
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks();
    }

    public static void IntoOtherSave() { }
}
