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
    private static MyImageButton btnCurrentMatrix;
    private static Text txtCurrentMatrixCount;
    private static MyImageButton btnFragment;
    private static Text txtFragmentCount;
    private static MyImageButton btnDarkFogMatrix;
    private static Text txtDarkFogMatrixCount;

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
        btnCurrentMatrix = MyImageButton.CreateImageButton(0f, y, tab, null).WithSize(24f, 24f);
        txtCurrentMatrixCount = wnd.AddText2(32f, y, tab, "", 14);
        btnFragment = MyImageButton.CreateImageButton(170f, y, tab, LDB.items.Select(IFE残片)).WithSize(24f, 24f);
        txtFragmentCount = wnd.AddText2(202f, y, tab, "", 14);
        btnDarkFogMatrix = MyImageButton.CreateImageButton(340f, y, tab, LDB.items.Select(I黑雾矩阵)).WithSize(24f, 24f);
        txtDarkFogMatrixCount = wnd.AddText2(372f, y, tab, "", 14);
        y += 30f;
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
        btnCurrentMatrix.Proto = LDB.items.Select(matrixId);
        txtCurrentMatrixCount.text = $"x {GetItemTotalCount(matrixId)}";
        txtFragmentCount.text = $"x {GetItemTotalCount(IFE残片)}";
        txtDarkFogMatrixCount.text = $"x {GetItemTotalCount(I黑雾矩阵)}";
        txtOverview.text = $"{ "资源统筹说明".Translate() }";
        txtMode.text = $"当前模式：{GachaService.GetModeNameKey().Translate()}";
        txtCostOpening.text =
            $"{ "开线池成本".Translate() }：x{GachaService.GetDrawMatrixCost(GachaPool.PoolIdOpeningLine, 1)} / 抽";
        txtCostProto.text =
            $"{ "原胚池成本".Translate() }：x{GachaService.GetDrawMatrixCost(GachaPool.PoolIdProtoLoop, 1)} / 抽";
        txtCostFocus.text =
            $"{ "聚焦切换成本".Translate() }：残片 x{GachaService.GetFocusSwitchFragmentCost(GachaFocusType.MineralExpansion)} 起    黑雾支线通过成长池中的黑雾矩阵报价接入";
    }

    public static void Import(BinaryReader r) { r.ReadBlocks(); }
    public static void Export(BinaryWriter w) { w.WriteBlocks(); }
    public static void IntoOtherSave() { }
}
