using System.Linq;
using System.IO;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.Logic.RecipeGrowth;
using FE.UI.Components;
using FE.UI.View;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Components.GridDsl;
using static FE.Utils.Utils;

namespace FE.UI.View.DrawGrowth;

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
            "Version 2.3 no longer uses physical tickets. Draws consume the current stage Matrix directly, while Growth / Focus use Fragments and pool points.",
            "2.3 版本不再使用实体奖券。抽取直接消耗当前阶段矩阵，成长与聚焦则消耗残片和池积分。");
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
                                children: [
                                    Node(pos: (0, 0), objectName: "ticket-exchange-resource-body", build: (w, resourceCard) => {
                                        float cardW = resourceCard.sizeDelta.x;
                                        txtResourceTitle = PageLayout.AddCardTitle(w, resourceCard, 0f, 0f, "当前资源", 15, "ticket-exchange-resource-title");
                                        float y = 36f;
                                        btnCurrentMatrix = MyImageButton.CreateImageButton(0f, y, resourceCard, null).WithSize(40f, 40f);
                                        btnFragment = MyImageButton.CreateImageButton(160f, y, resourceCard, LDB.items.Select(IFE残片)).WithSize(40f, 40f);
                                        btnDarkFogMatrix = MyImageButton.CreateImageButton(320f, y, resourceCard, LDB.items.Select(I黑雾矩阵)).WithSize(40f, 40f);
                                        y += 44f;
                                        txtMode = w.AddText2(0f, y, resourceCard, "", 13);
                                        txtMode.rectTransform.sizeDelta = new Vector2(cardW, 24f);
                                    }),
                                ]),
                            ContentCard(pos: (0, 1), objectName: "ticket-exchange-cost-card", strong: true,
                                children: [
                                    Node(pos: (0, 0), objectName: "ticket-exchange-cost-body", build: (w, costCard) => {
                                        float cardW = costCard.sizeDelta.x;
                                        txtCostTitle = PageLayout.AddCardTitle(w, costCard, 0f, 0f, "核心成本", 15, "ticket-exchange-cost-title");
                                        float y = 36f;
                                        txtCostOpening = w.AddText2(0f, y, costCard, "", 13);
                                        txtCostOpening.rectTransform.sizeDelta = new Vector2(cardW, 24f);
                                        txtCostOpening.horizontalOverflow = HorizontalWrapMode.Wrap;
                                        y += 34f;
                                        txtCostProto = w.AddText2(0f, y, costCard, "", 13);
                                        txtCostProto.rectTransform.sizeDelta = new Vector2(cardW, 24f);
                                        txtCostProto.horizontalOverflow = HorizontalWrapMode.Wrap;
                                        y += 34f;
                                        txtCostFocus = w.AddText2(0f, y, costCard, "", 13);
                                        txtCostFocus.rectTransform.sizeDelta = new Vector2(cardW, 68f);
                                        txtCostFocus.horizontalOverflow = HorizontalWrapMode.Wrap;
                                        txtCostFocus.alignment = TextAnchor.UpperLeft;
                                    }),
                                ]),
                        ]),
                    ContentCard(pos: (2, 0), objectName: "ticket-exchange-darkfog-card",
                        children: [
                            Node(pos: (0, 0), objectName: "ticket-exchange-darkfog-body", build: (w, darkFogCard) => {
                                float cardW = darkFogCard.sizeDelta.x;
                                txtDarkFogTitle = PageLayout.AddCardTitle(w, darkFogCard, 0f, 0f, "黑雾支线说明", 15, "ticket-exchange-darkfog-title");
                                txtDarkFogStatus = w.AddText2(0f, 38f, darkFogCard, "", 13);
                                txtDarkFogStatus.supportRichText = true;
                                txtDarkFogStatus.alignment = TextAnchor.UpperLeft;
                                txtDarkFogStatus.rectTransform.sizeDelta = new Vector2(cardW, 220f);
                            }),
                        ]),
                    FooterCard(pos: (3, 0), objectName: "ticket-exchange-footer-card",
                        children: [
                            Node(pos: (0, 0), objectName: "ticket-exchange-footer-body", build: (w, footerCard) => {
                                w.AddButton(0f, 0f, 160f, footerCard, "前往成长规划".Translate(), 13,
                                    onClick: () => MainWindow.NavigateToPage(MainWindowPageRegistry.DrawGrowthCategoryName, 2));
                                w.AddButton(176f, 0f, 160f, footerCard, "前往市场板".Translate(), 13,
                                    onClick: () => MainWindow.NavigateToPage(MainWindowPageRegistry.ResourceInteractionCategoryName, 2));
                            }),
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
        txtOverview.text = $"{ "抽取总览说明".Translate() }";
        txtResourceTitle.text = "当前资源".Translate().WithColor(Orange);
        txtCostTitle.text = "核心成本".Translate().WithColor(Orange);
        txtDarkFogTitle.text = "黑雾支线说明".Translate().WithColor(Orange);
        txtMode.text = $"当前模式：{GachaService.GetModeNameKey().Translate()}";
        txtCostOpening.text =
            $"{ "开线池成本".Translate() }：x{GachaService.GetDrawMatrixCost(GachaPool.PoolIdOpeningLine, 1)} / 抽";
        txtCostProto.text =
            $"{ "原胚池成本".Translate() }：x{GachaService.GetDrawMatrixCost(GachaPool.PoolIdProtoLoop, 1)} / 抽";
        txtCostFocus.text =
            $"{ "聚焦切换成本".Translate() }：残片 x{GachaService.GetFocusSwitchFragmentCost(GachaFocusType.MineralExpansion)} 起    成长积分统一进入成长池";
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
            : $"节点 {DarkFogCombatManager.GetEnhancedNodeCount()}/4    遗物 {DarkFogCombatManager.GetRelicCount()}    Rank {DarkFogCombatManager.GetMeritRank()}    技能 {DarkFogCombatManager.GetAssignedSkillPointCount()}".WithColor(Green);
        string nextTarget = stage switch {
            EDarkFogCombatStage.Dormant when !DarkFogCombatManager.IsCombatModeEnabled() => "启用战斗模式".WithColor(Orange),
            EDarkFogCombatStage.Dormant when DarkFogCombatManager.GetProgressStageIndex() < 3 => $"解锁 {LDB.items.Select(I信息矩阵).name}".WithColor(Orange),
            EDarkFogCombatStage.Dormant => "建立黑雾矩阵库存或首次接触黑雾掉落".WithColor(Blue),
            EDarkFogCombatStage.Signal => $"{LDB.items.Select(I引力矩阵).name} + 物资层级 2/4".WithColor(Blue),
            EDarkFogCombatStage.GroundSuppression => $"{LDB.items.Select(I宇宙矩阵).name} + 物资层级 3/4 或接触蜂巢".WithColor(Blue),
            EDarkFogCombatStage.StellarHunt when DarkFogCombatManager.IsEnhancedLayerEnabled() => "物资层级 4/4 或增强节点 2/4".WithColor(Gold),
            EDarkFogCombatStage.StellarHunt => "物资层级 4/4".WithColor(Gold),
            _ => "已到最终阶段".WithColor(Gold),
        };

        return $"{ "黑雾支线说明".Translate() }：当前黑雾矩阵 x{GetItemTotalCount(I黑雾矩阵)}    阶段 {stageName}\n"
               + $"战况：地面基地 {DarkFogCombatManager.GetAliveGroundBaseCount()}    星域蜂巢 {DarkFogCombatManager.GetAliveHiveCount()}    物资层级 {DarkFogCombatManager.GetDarkFogResourceTier()}/4\n"
               + $"成长页报价 {DarkFogCombatManager.GetUnlockedGrowthOfferCount()} 项    市场板特单 {DarkFogCombatManager.GetUnlockedSpecialOrderCount()} 条    黑雾配方 {unlockedRecipes}/{totalRecipes} 已解锁，满级 {maxedRecipes}    增强层 {enhancedText}\n"
               + $"下一阶段：{nextTarget}";
    }

    public static void Import(BinaryReader r) { r.ReadBlocks(); }
    public static void Export(BinaryWriter w) { w.WriteBlocks(); }
    public static void IntoOtherSave() { }
}
