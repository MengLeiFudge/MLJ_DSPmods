using System.Linq;
using System.IO;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.Logic.RecipeGrowth;
using FE.UI.Components;
using FE.UI.View;
using UnityEngine;
using UnityEngine.UI;
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
        Register("资源统筹", "Resource Overview");
        Register("资源统筹说明",
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
        header = PageLayout.CreatePageHeader(wnd, tab, "资源统筹", "", "ticket-exchange-header");
        txtOverview = header.Summary;

        float top = PageLayout.HeaderHeight + PageLayout.Gap;
        float columnWidth = (PageLayout.DesignWidth - PageLayout.Gap) / 2f;
        RectTransform resourceCard = PageLayout.CreateContentCard(tab, "ticket-exchange-resource-card", 0f, top,
            columnWidth, 180f, true);
        RectTransform costCard = PageLayout.CreateContentCard(tab, "ticket-exchange-cost-card",
            columnWidth + PageLayout.Gap, top, columnWidth, 180f, true);
        RectTransform darkFogCard = PageLayout.CreateContentCard(tab, "ticket-exchange-darkfog-card", 0f,
            top + 180f + PageLayout.Gap, PageLayout.DesignWidth, 293f);
        RectTransform footerCard = PageLayout.CreateFooterCard(tab, "ticket-exchange-footer-card",
            top + 180f + PageLayout.Gap + 293f + PageLayout.Gap);

        txtResourceTitle = PageLayout.AddCardTitle(wnd, resourceCard, 18f, 14f, "当前资源", 15, "ticket-exchange-resource-title");
        txtCostTitle = PageLayout.AddCardTitle(wnd, costCard, 18f, 14f, "核心成本", 15, "ticket-exchange-cost-title");
        txtDarkFogTitle = PageLayout.AddCardTitle(wnd, darkFogCard, 18f, 14f, "黑雾支线说明", 15, "ticket-exchange-darkfog-title");

        float y = 64f;
        btnCurrentMatrix = MyImageButton.CreateImageButton(18f, y, resourceCard, null).WithSize(40f, 40f);
        btnFragment = MyImageButton.CreateImageButton(178f, y, resourceCard, LDB.items.Select(IFE残片)).WithSize(40f, 40f);
        btnDarkFogMatrix = MyImageButton.CreateImageButton(338f, y, resourceCard, LDB.items.Select(I黑雾矩阵)).WithSize(40f, 40f);
        y += 44f;
        txtMode = wnd.AddText2(18f, y, resourceCard, "", 14);
        txtMode.rectTransform.sizeDelta = new Vector2(columnWidth - 36f, 24f);

        y = 64f;
        txtCostOpening = wnd.AddText2(18f, y, costCard, "", 14);
        txtCostOpening.rectTransform.sizeDelta = new Vector2(columnWidth - 36f, 24f);
        y += 34f;
        txtCostProto = wnd.AddText2(18f, y, costCard, "", 14);
        txtCostProto.rectTransform.sizeDelta = new Vector2(columnWidth - 36f, 24f);
        y += 34f;
        txtCostFocus = wnd.AddText2(18f, y, costCard, "", 14);
        txtCostFocus.rectTransform.sizeDelta = new Vector2(columnWidth - 36f, 68f);
        txtCostFocus.alignment = TextAnchor.UpperLeft;

        txtDarkFogStatus = wnd.AddText2(18f, 56f, darkFogCard, "", 13);
        txtDarkFogStatus.supportRichText = true;
        txtDarkFogStatus.alignment = TextAnchor.UpperLeft;
        txtDarkFogStatus.rectTransform.sizeDelta = new Vector2(PageLayout.DesignWidth - 36f, 220f);

        wnd.AddButton(18f, 10f, 160f, footerCard, "前往成长规划".Translate(), 13,
            onClick: () => MainWindow.NavigateToPage(MainWindowPageRegistry.DrawGrowthCategoryName, 2));
        wnd.AddButton(194f, 10f, 160f, footerCard, "前往市场板".Translate(), 13,
            onClick: () => MainWindow.NavigateToPage(MainWindowPageRegistry.ResourceInteractionCategoryName, 3));
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
        header.Title.text = "资源统筹".Translate().WithColor(Orange);
        txtOverview.text = $"{ "资源统筹说明".Translate() }";
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
