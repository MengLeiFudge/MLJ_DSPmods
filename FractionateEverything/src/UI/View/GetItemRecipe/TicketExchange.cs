using System.IO;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.View.GetItemRecipe;

public static class TicketExchange {
    private static RectTransform tab;
    private static Text txtOverview;
    private static Text txtMode;
    private static Text txtCostOpening;
    private static Text txtCostProto;
    private static Text txtCostFocus;

    public static void AddTranslations() {
        Register("资源统筹", "Resource Overview");
        Register("资源统筹说明",
            "Version 2.3 no longer uses physical tickets. Draws consume the current stage Matrix directly, while Growth / Focus use Fragments and pool points.",
            "2.3 版本不再使用实体奖券。抽取直接消耗当前阶段矩阵，成长与聚焦则消耗残片和池积分。");
        Register("开线池成本", "Opening Pool Cost");
        Register("原胚池成本", "Proto Pool Cost");
        Register("聚焦切换成本", "Focus Switch Cost");
        Register("黑雾支线说明", "Dark Fog Branch", "黑雾支线");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        tab = wnd.AddTab(trans, "资源统筹");
        float y = 18f;
        txtOverview = wnd.AddText2(0f, y, tab, "资源统筹说明", 14);
        txtOverview.supportRichText = true;
        txtOverview.rectTransform.sizeDelta = new Vector2(960f, 48f);

        y += 64f;
        txtMode = wnd.AddText2(0f, y, tab, "", 14);
        y += 30f;
        txtCostOpening = wnd.AddText2(0f, y, tab, "", 14);
        y += 30f;
        txtCostProto = wnd.AddText2(0f, y, tab, "", 14);
        y += 30f;
        txtCostFocus = wnd.AddText2(0f, y, tab, "", 14);
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        int matrixId = GachaService.GetCurrentDrawMatrixId();
        string matrixName = LDB.items.Select(matrixId)?.name ?? matrixId.ToString();
        txtOverview.text =
            $"{ "资源统筹说明".Translate() }\n"
            + $"当前阶段矩阵：{matrixName} x{GetItemTotalCount(matrixId)}    残片 x{GetItemTotalCount(IFE残片)}";
        txtMode.text = $"当前模式：{GachaService.GetModeNameKey().Translate()}    黑雾矩阵 x{GetItemTotalCount(I黑雾矩阵)}";
        txtCostOpening.text =
            $"{ "开线池成本".Translate() }：{matrixName} x{GachaService.GetDrawMatrixCost(GachaPool.PoolIdOpeningLine, 1)} / 抽";
        txtCostProto.text =
            $"{ "原胚池成本".Translate() }：{matrixName} x{GachaService.GetDrawMatrixCost(GachaPool.PoolIdProtoLoop, 1)} / 抽";
        txtCostFocus.text =
            $"{ "聚焦切换成本".Translate() }：残片 x{GachaService.GetFocusSwitchFragmentCost(GachaFocusType.MineralExpansion)} 起    黑雾支线通过成长池中的黑雾矩阵报价接入";
    }

    public static void Import(BinaryReader r) { r.ReadBlocks(); }
    public static void Export(BinaryWriter w) { w.WriteBlocks(); }
    public static void IntoOtherSave() { }
}
