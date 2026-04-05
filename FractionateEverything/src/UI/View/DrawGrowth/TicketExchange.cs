using System.Linq;
using System.IO;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.UI.Components;
using FE.UI.View;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.View.DrawGrowth;

public static class TicketExchange {
    private static RectTransform tab;
    private static Text txtOverview;
    private static Text txtMode;
    private static Text txtCostOpening;
    private static Text txtCostProto;
    private static Text txtCostFocus;
    private static Text txtDarkFogStatus;
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
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        tab = wnd.AddTab(trans, "资源统筹");
        float y = 18f;
        txtOverview = wnd.AddText2(0f, y, tab, "资源统筹说明", 14);
        txtOverview.supportRichText = true;
        txtOverview.rectTransform.sizeDelta = new Vector2(960f, 48f);

        y += 64f;
        btnCurrentMatrix = MyImageButton.CreateImageButton(0f, y, tab, null).WithSize(40f, 40f);
        btnFragment = MyImageButton.CreateImageButton(170f, y, tab, LDB.items.Select(IFE残片)).WithSize(40f, 40f);
        btnDarkFogMatrix = MyImageButton.CreateImageButton(340f, y, tab, LDB.items.Select(I黑雾矩阵)).WithSize(40f, 40f);
        y += 40f;
        txtMode = wnd.AddText2(0f, y, tab, "", 14);
        y += 30f;
        txtCostOpening = wnd.AddText2(0f, y, tab, "", 14);
        y += 30f;
        txtCostProto = wnd.AddText2(0f, y, tab, "", 14);
        y += 30f;
        txtCostFocus = wnd.AddText2(0f, y, tab, "", 14);
        y += 40f;
        txtDarkFogStatus = wnd.AddText2(0f, y, tab, "", 13);
        txtDarkFogStatus.supportRichText = true;
        txtDarkFogStatus.rectTransform.sizeDelta = new Vector2(960f, 70f);

        y += 84f;
        wnd.AddButton(0f, y, 140f, tab, "前往成长规划".Translate(), 13,
            onClick: () => MainWindow.NavigateToPage(MainWindowPageRegistry.DrawGrowthCategoryName, 2));
        wnd.AddButton(150f, y, 140f, tab, "前往市场板".Translate(), 13,
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
        txtOverview.text = $"{ "资源统筹说明".Translate() }";
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
        int stageIndex = ItemManager.GetCurrentProgressStageIndex();
        bool growthUnlocked = stageIndex >= 3;
        bool marketUnlocked = stageIndex >= 4;
        int darkFogOfferCount = GachaService.GetGrowthOffers().Count(offer => offer.ExtraCostItemId == I黑雾矩阵);
        int darkFogSpecialOrders = MarketBoardManager.ActiveOffers.Count(offer =>
            offer.OfferType == MarketBoardManager.MarketOfferType.Special
            && (offer.InputItemId == I黑雾矩阵 || offer.ExtraInputItemId == I黑雾矩阵 || offer.OutputItemId == I黑雾矩阵));

        return $"{ "黑雾支线说明".Translate() }：当前黑雾矩阵 x{GetItemTotalCount(I黑雾矩阵)}"
               + $"    成长页报价 {(growthUnlocked ? $"已开放 {darkFogOfferCount} 项".WithColor(Green) : "未开放".WithColor(Orange))}"
               + $"    市场板特单 {(marketUnlocked ? $"已开放 {darkFogSpecialOrders} 项".WithColor(Green) : "未开放".WithColor(Orange))}";
    }

    public static void Import(BinaryReader r) { r.ReadBlocks(); }
    public static void Export(BinaryWriter w) { w.WriteBlocks(); }
    public static void IntoOtherSave() { }
}
