using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.UI.Components;
using FE.UI.View;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.View.GetItemRecipe;

public static class TicketRaffle {
    private sealed class RaffleTabUi {
        public int PoolId;
        public RectTransform Tab;
        public Text TxtPoolName;
        public Text TxtPoolDesc;
        public Text TxtResource;
        public Text TxtPity;
        public Text TxtPoints;
        public Text TxtFocus;
        public Text TxtResultSummary;
        public readonly Text[] TxtResultLines = new Text[8];
        public UIButton BtnDraw1;
        public UIButton BtnDraw10;
        public UIButton BtnGoGrowth;
        public UIButton BtnGoFocus;
    }

    public static long totalDraws;
    private static readonly List<RaffleTabUi> activeUis = [];

    private static void SyncTotalDrawsFromSharedState() {
        totalDraws = MainWindow.SharedPanelState?.TicketRaffleTotalDraws ?? 0;
    }

    private static void SyncTotalDrawsToSharedState() {
        if (MainWindow.SharedPanelState != null) {
            MainWindow.SharedPanelState.TicketRaffleTotalDraws = totalDraws;
        }
    }

    public static void AddTranslations() {
        Register("开线抽取", "Opening Draw");
        Register("原胚抽取", "Proto Draw");
        Register("成长说明", "Growth Info");
        Register("聚焦说明", "Focus Info");

        Register("开线池", "Opening Pool");
        Register("原胚闭环池", "Proto Loop Pool");
        Register("成长池", "Growth Pool");
        Register("流派聚焦", "Focus Layer");

        Register("开线池说明",
            "Spend the current stage Matrix to draw Mineral Replication / Conversion recipes. Focus raises matching directions.",
            "消耗当前阶段矩阵，抽取矿物复制/转化的新配方与回响。当前聚焦会提高对应方向的命中权重。");
        Register("原胚闭环池说明",
            "Spend the current stage Matrix to draw protos and directional protos. Focus raises matching tower embryos.",
            "消耗当前阶段矩阵，抽取各类原胚与定向原胚。当前聚焦会提高对应塔种原胚的出现权重。");
        Register("成长池说明",
            "Growth is deterministic. Use pool points and fragments on the Growth page.",
            "成长池为非随机成长入口。请前往成长页使用池积分与残片进行补差和定向成长。");
        Register("流派聚焦说明",
            "Focus is not a standalone draw pool. Select a focus on the Focus page to bias opening/proto rewards.",
            "流派聚焦不是独立抽卡池。请前往聚焦页选择方向，以偏置开线池和原胚闭环池的奖励分布。");

        Register("当前资源", "Resource");
        Register("当前阶段矩阵", "Current Stage Matrix");
        Register("残片余额", "Fragments");
        Register("保底进度", "Pity");
        Register("当前池积分", "Pool Points");
        Register("当前聚焦", "Focus");
        Register("抽1次", "Draw x1");
        Register("抽10次", "Draw x10");
        Register("前往成长池", "Go Growth");
        Register("前往聚焦页", "Go Focus");
        Register("结果摘要", "Summary");
        Register("暂无抽取结果", "No draws yet.", "暂无抽取结果");
        Register("更多结果已折叠", "More results folded.", "其余结果已折叠");

        Register("聚焦-平衡发展", "Balanced Growth");
        Register("聚焦描述-平衡发展", "No extra bias; both pools stay average.", "不过度偏置任何方向，适合长期稳步推进。");
        Register("聚焦-复制扩张", "Replication Expansion");
        Register("聚焦描述-复制扩张", "Bias Mineral Replication recipes and Mineral Replication Tower protos.", "提高矿物复制配方与矿物复制塔原胚的出现权重。");
        Register("聚焦-转化跃迁", "Conversion Leap");
        Register("聚焦描述-转化跃迁", "Bias Conversion recipes and Conversion Tower protos.", "提高转化配方与转化塔原胚的出现权重。");
        Register("聚焦-交互物流", "Interaction Logistics");
        Register("聚焦描述-交互物流", "Bias Interaction Tower protos for logistics and upload loops.", "提高交互塔原胚权重，强化上传与物流中枢。");
        Register("聚焦-原胚循环", "Embryo Cycle");
        Register("聚焦描述-原胚循环", "Bias directional protos and deterministic proto补差.", "提高定向原胚与原胚补差收益，强化原胚闭环。");
        Register("聚焦-工艺优化", "Process Optimization");
        Register("聚焦描述-工艺优化", "Bias current-stage recipes and Point Aggregate Tower protos.", "提高当前阶段配方与点数聚集塔原胚权重。");
        Register("聚焦-精馏经济", "Rectification Economy");
        Register("聚焦描述-精馏经济", "Bias Rectification Tower protos and growth support for fragment economy.", "提高精馏塔原胚与残片经济相关补差收益。");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateRecipeUI(MyConfigWindow wnd, RectTransform trans) => CreatePoolUI(wnd, trans, "开线抽取", GachaPool.PoolIdOpeningLine);
    public static void CreateProtoUI(MyConfigWindow wnd, RectTransform trans) => CreatePoolUI(wnd, trans, "原胚抽取", GachaPool.PoolIdProtoLoop);
    public static void CreateUpUI(MyConfigWindow wnd, RectTransform trans) => CreatePoolUI(wnd, trans, "成长说明", GachaPool.PoolIdGrowth);
    public static void CreateLimitedUI(MyConfigWindow wnd, RectTransform trans) => CreatePoolUI(wnd, trans, "聚焦说明", GachaPool.PoolIdFocus);

    private static void CreatePoolUI(MyConfigWindow wnd, RectTransform trans, string tabName, int poolId) {
        SyncTotalDrawsFromSharedState();
        var ui = new RaffleTabUi {
            PoolId = poolId,
            Tab = wnd.AddTab(trans, tabName)
        };
        activeUis.Add(ui);

        float y = 8f;
        ui.TxtPoolName = MyWindow.AddText(0f, y, ui.Tab, GetPoolName(poolId), 18);
        y += 28f;
        ui.TxtPoolDesc = MyWindow.AddText(0f, y, ui.Tab, GetPoolDesc(poolId), 13);
        ui.TxtPoolDesc.rectTransform.sizeDelta = new Vector2(960f, 56f);

        y += 72f;
        ui.TxtResource = MyWindow.AddText(0f, y, ui.Tab, "", 13);
        y += 24f;
        ui.TxtPity = MyWindow.AddText(0f, y, ui.Tab, "", 13);
        y += 24f;
        ui.TxtPoints = MyWindow.AddText(0f, y, ui.Tab, "", 13);
        y += 24f;
        ui.TxtFocus = MyWindow.AddText(0f, y, ui.Tab, "", 13);

        y += 34f;
        ui.BtnDraw1 = wnd.AddButton(0f, y, 140f, ui.Tab, "抽1次".Translate(), 14,
            onClick: () => StartDraw(ui, 1));
        ui.BtnDraw10 = wnd.AddButton(150f, y, 140f, ui.Tab, "抽10次".Translate(), 14,
            onClick: () => StartDraw(ui, 10));
        ui.BtnGoGrowth = wnd.AddButton(620f, y, 140f, ui.Tab, "前往成长池".Translate(), 14,
            onClick: () => MainWindow.NavigateToPage(MainWindowPageRegistry.StoreCategoryName, 0));
        ui.BtnGoFocus = wnd.AddButton(770f, y, 140f, ui.Tab, "前往聚焦页".Translate(), 14,
            onClick: () => MainWindow.NavigateToPage(MainWindowPageRegistry.StoreCategoryName, 1));

        y += 48f;
        ui.TxtResultSummary = MyWindow.AddText(0f, y, ui.Tab, "暂无抽取结果".Translate(), 13);
        ui.TxtResultSummary.rectTransform.sizeDelta = new Vector2(960f, 24f);

        y += 28f;
        for (int i = 0; i < ui.TxtResultLines.Length; i++) {
            ui.TxtResultLines[i] = MyWindow.AddText(0f, y, ui.Tab, "", 13);
            ui.TxtResultLines[i].rectTransform.sizeDelta = new Vector2(960f, 22f);
            y += 24f;
        }

        RefreshTabState(ui);
    }

    private static string GetPoolName(int poolId) {
        return poolId switch {
            GachaPool.PoolIdOpeningLine => "开线池".Translate(),
            GachaPool.PoolIdProtoLoop => "原胚闭环池".Translate(),
            GachaPool.PoolIdGrowth => "成长池".Translate(),
            GachaPool.PoolIdFocus => "流派聚焦".Translate(),
            _ => "开线池".Translate(),
        };
    }

    private static string GetPoolDesc(int poolId) {
        return poolId switch {
            GachaPool.PoolIdOpeningLine => "开线池说明".Translate(),
            GachaPool.PoolIdProtoLoop => "原胚闭环池说明".Translate(),
            GachaPool.PoolIdGrowth => "成长池说明".Translate(),
            GachaPool.PoolIdFocus => "流派聚焦说明".Translate(),
            _ => string.Empty,
        };
    }

    private static void StartDraw(RaffleTabUi ui, int count) {
        if (!GachaPool.IsDrawPool(ui.PoolId)) {
            return;
        }

        int matrixId = GachaService.GetCurrentDrawMatrixId();
        var results = GachaService.Draw(ui.PoolId, matrixId, count);
        if (results == null || results.Count == 0) {
            return;
        }

        totalDraws += results.Count;
        SyncTotalDrawsToSharedState();
        RenderResults(ui, matrixId, count, results);
        RefreshTabState(ui);
    }

    private static void RenderResults(RaffleTabUi ui, int matrixId, int count, List<GachaResult> results) {
        int sCount = 0;
        int aCount = 0;
        int bCount = 0;
        int cCount = 0;
        foreach (var result in results) {
            switch (result.Rarity) {
                case GachaRarity.S:
                    sCount++;
                    break;
                case GachaRarity.A:
                    aCount++;
                    break;
                case GachaRarity.B:
                    bCount++;
                    break;
                default:
                    cCount++;
                    break;
            }
        }

        int cost = GachaService.GetDrawMatrixCost(ui.PoolId, count);
        string matrixName = LDB.items.Select(matrixId)?.name ?? matrixId.ToString();
        ui.TxtResultSummary.text =
            $"{"结果摘要".Translate()}：{matrixName} x{cost}    "
            + $"S×{sCount}".WithColor(Gold)
            + $" / A×{aCount}".WithColor(Purple)
            + $" / B×{bCount}".WithColor(Blue)
            + $" / C×{cCount}".WithColor(White)
            + $"    积分 +{results.Count}".WithColor(Green);

        int lineCount = Mathf.Min(ui.TxtResultLines.Length, results.Count);
        for (int i = 0; i < lineCount; i++) {
            GachaResult result = results[i];
            string itemName = GetItemName(result.ItemId);
            string line = $"[{result.Rarity}] {itemName}";
            if (result.IsRecipe) {
                line += "  配方".WithColor(Orange);
            }
            if (result.WasHardPity) {
                line += "  保底".WithColor(Gold);
            }
            ui.TxtResultLines[i].text = line;
        }

        for (int i = lineCount; i < ui.TxtResultLines.Length; i++) {
            ui.TxtResultLines[i].text = i == lineCount && results.Count > ui.TxtResultLines.Length
                ? "更多结果已折叠".Translate().WithColor(Orange)
                : "";
        }
    }

    private static string GetItemName(int itemId) {
        return itemId > 0 ? (LDB.items.Select(itemId)?.name ?? itemId.ToString()) : "-";
    }

    private static void RefreshTabState(RaffleTabUi ui) {
        int matrixId = GachaService.GetCurrentDrawMatrixId();
        string matrixName = GetItemName(matrixId);
        long matrixCount = GetItemTotalCount(matrixId);
        int draw1Cost = GachaService.GetDrawMatrixCost(ui.PoolId, 1);
        int draw10Cost = GachaService.GetDrawMatrixCost(ui.PoolId, 10);

        ui.TxtResource.text =
            $"{"当前资源".Translate()}：{"当前阶段矩阵".Translate()} {matrixName} x{matrixCount}"
            + $"    {"残片余额".Translate()} x{GetItemTotalCount(IFE残片)}";
        ui.TxtPity.text = GachaPool.IsDrawPool(ui.PoolId)
            ? $"{"保底进度".Translate()}：{GachaManager.PityCount[ui.PoolId] + 1}/90"
            : $"{"保底进度".Translate()}：-";
        ui.TxtPoints.text = $"{"当前池积分".Translate()}：{GachaManager.GetPoolPoints(ui.PoolId)}";
        ui.TxtFocus.text = $"{"当前聚焦".Translate()}：{GetFocusName(GachaManager.CurrentFocus)}";

        bool canDraw1 = GachaPool.IsDrawPool(ui.PoolId) && matrixCount >= draw1Cost;
        bool canDraw10 = GachaPool.IsDrawPool(ui.PoolId) && matrixCount >= draw10Cost;

        if (ui.BtnDraw1?.button != null) {
            ui.BtnDraw1.button.interactable = canDraw1;
            ui.BtnDraw1.SetText($"{ "抽1次".Translate() } ({draw1Cost})");
        }
        if (ui.BtnDraw10?.button != null) {
            ui.BtnDraw10.button.interactable = canDraw10;
            ui.BtnDraw10.SetText($"{ "抽10次".Translate() } ({draw10Cost})");
        }
    }

    private static string GetFocusName(GachaFocusType focusType) {
        foreach (var focus in GachaService.FocusDefinitions) {
            if (focus.FocusType == focusType) {
                return focus.NameKey.Translate();
            }
        }
        return focusType.ToString();
    }

    public static void UpdateUI() {
        SyncTotalDrawsFromSharedState();
        foreach (var ui in activeUis) {
            if (ui?.Tab == null || !ui.Tab.gameObject.activeSelf) {
                continue;
            }
            RefreshTabState(ui);
        }
    }

    public static void Import(BinaryReader r) { r.ReadBlocks(); }
    public static void Export(BinaryWriter w) { w.WriteBlocks(); }
    public static void IntoOtherSave() { }
}
