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

public static class LimitedTimeStore {
    private sealed class ExchangeRowUi {
        public int TicketId;
        public int PointCost;
        public int OutputId;
        public int OutputCount;
        public MySlider Slider;
        public Text MaxText;
        public UIButton ExchangeButton;
    }

    private static RectTransform normalTab;
    private static RectTransform featuredTab;

    private static readonly List<ExchangeRowUi> normalRows = [];
    private static readonly List<ExchangeRowUi> featuredRows = [];

    private static Text txtNormalTabShardCount;
    private static Text txtNormalTabTicketCount;
    private static Text txtNormalTabPointCount;
    private static Text txtFeaturedTabShardCount;
    private static Text txtFeaturedTabTicketCount;
    private static Text txtFeaturedTabPointCount;

    private const float RowH = 46f;

    private static RectTransform pageRoot;

    private static readonly (int ticketId, int pointCost, int outputId, int outputCount)[] PointRates = [
        (IFE普通抽卡券, 1, I电磁矩阵, 1),
        (IFE普通抽卡券, 10, I能量矩阵, 1),
        (IFE普通抽卡券, 60, I结构矩阵, 1),
        (IFE精选抽卡券, 1, I能量矩阵, 1),
        (IFE精选抽卡券, 8, I结构矩阵, 1),
        (IFE精选抽卡券, 30, I信息矩阵, 1),
    ];

    public static void AddTranslations() {
        Register("积分商店", "Point Store");
        Register("普通积分商店", "Normal Point Store");
        Register("精选积分商店", "Featured Point Store");
        Register("兑换分类", "Exchange Categories");
        Register("积分商店导航", "Point Store Navigation");
        Register("持有", "Held");
        Register("最多", "Max");
        Register("兑换", "Exchange");
        Register("普通积分", "Normal Points");
        Register("精选积分", "Featured Points");
        Register("积分兑换", "Points Exchange");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        pageRoot = trans;
        normalRows.Clear();
        featuredRows.Clear();

        normalTab = wnd.AddTab(trans, "普通积分商店");
        featuredTab = wnd.AddTab(trans, "精选积分商店");

        BuildTabTopInfo(wnd, normalTab, true);
        BuildTabTopInfo(wnd, featuredTab, false);
        BuildPointRows(wnd, normalTab, normalRows, IFE普通抽卡券, 42f);
        BuildPointRows(wnd, featuredTab, featuredRows, IFE精选抽卡券, 42f);
    }

    private static void BuildTabTopInfo(MyConfigWindow wnd, RectTransform tab, bool isNormalTab) {
        wnd.AddButton(860f, 8f, 100f, tab, "前往抽奖".Translate(), 13,
            onClick: () => {
                int targetTab = isNormalTab ? 0 : 2;
                MainWindow.NavigateToPage(MainWindowPageRegistry.GachaCategoryName, targetTab);
            });

        if (isNormalTab) {
            wnd.AddImageButton(5f, 8f, tab, LDB.items.Select(IFE残片));
            txtNormalTabShardCount = MyWindow.AddText(48f, 8f, tab, "x 0", 13);
            wnd.AddImageButton(220f, 8f, tab, LDB.items.Select(IFE普通抽卡券));
            txtNormalTabTicketCount = MyWindow.AddText(263f, 8f, tab, "x 0", 13);
            txtNormalTabPointCount = MyWindow.AddText(420f, 8f, tab, $"{"普通积分".Translate()}: 0", 13);
            return;
        }
        wnd.AddImageButton(5f, 8f, tab, LDB.items.Select(IFE残片));
        txtFeaturedTabShardCount = MyWindow.AddText(48f, 8f, tab, "x 0", 13);
        wnd.AddImageButton(220f, 8f, tab, LDB.items.Select(IFE精选抽卡券));
        txtFeaturedTabTicketCount = MyWindow.AddText(263f, 8f, tab, "x 0", 13);
        txtFeaturedTabPointCount = MyWindow.AddText(420f, 8f, tab, $"{"精选积分".Translate()}: 0", 13);
    }

    private static void BuildPointRows(MyConfigWindow wnd, RectTransform page, List<ExchangeRowUi> rows, int ticketId,
        float startY) {
        float y = startY;
        BuildSectionTitle(page, y, "积分兑换");
        y += 34f;

        foreach (var (rateTicketId, pointCost, outputId, outputCount) in PointRates) {
            if (rateTicketId != ticketId) continue;
            rows.Add(CreateRow(wnd, page, y, rateTicketId, pointCost, outputId, outputCount));
            y += RowH;
        }
    }

    private static void BuildSectionTitle(RectTransform page, float y, string textKey) {
        MyWindow.AddText(0f, y, page, textKey.Translate(), 14);
    }

    private static ExchangeRowUi CreateRow(MyConfigWindow wnd, RectTransform page, float y,
        int ticketId, int pointCost, int outputId, int outputCount) {
        var row = new ExchangeRowUi {
            TicketId = ticketId,
            PointCost = pointCost,
            OutputId = outputId,
            OutputCount = outputCount
        };

        wnd.AddImageButton(0f, y, page, LDB.items.Select(ticketId));
        MyWindow.AddText(43f, y + 8f, page, $"积分x{pointCost} ->", 13);
        wnd.AddImageButton(108f, y, page, LDB.items.Select(outputId));
        MyWindow.AddText(151f, y + 8f, page, $"x{outputCount}", 13);

        row.Slider = wnd.AddSlider(220f, y + 8f, page, 0f, 0f, 1f, "F0", 280f);
        row.Slider.WithSmallerHandle(6f, 0f);
        row.MaxText = MyWindow.AddText(510f, y + 8f, page, "", 13);
        row.ExchangeButton = wnd.AddButton(620f, y, 120f, page, "", 13, onClick: () => ExchangeRow(row));
        row.Slider.OnValueChanged += () => RefreshRow(row);

        RefreshRow(row);
        return row;
    }

    private static int GetItemCount(int itemId) {
        long count = GetItemTotalCount(itemId);
        return count > int.MaxValue ? int.MaxValue : (int)count;
    }

    private static void RefreshRow(ExchangeRowUi row) {
        int points = GachaManager.GetPoolPointsByTicket(row.TicketId);
        int maxExchange = row.PointCost > 0 ? points / row.PointCost : 0;
        row.Slider.WithMinMaxValue(0f, maxExchange);
        int selected = Mathf.RoundToInt(row.Slider.Value);
        if (selected > maxExchange) {
            selected = maxExchange;
            row.Slider.Value = selected;
        }
        if (row.MaxText != null) {
            row.MaxText.text = $"{"最多".Translate()}: {maxExchange}";
        }
        if (row.ExchangeButton != null) {
            row.ExchangeButton.SetText($"{"兑换".Translate()} x{selected}");
            if (row.ExchangeButton.button != null) {
                row.ExchangeButton.button.interactable = selected > 0;
            }
        }
    }

    private static void ExchangeRow(ExchangeRowUi row) {
        int selected = Mathf.RoundToInt(row.Slider.Value);
        if (selected <= 0) return;
        int pointNeed = row.PointCost * selected;
        int outputCount = row.OutputCount * selected;
        if (!GachaManager.TryConsumePoolPointsByTicket(row.TicketId, pointNeed)) return;
        AddItemToModData(row.OutputId, outputCount, 0, true);
        RefreshRow(row);
    }

    private static bool IsPageVisible() {
        if (MainWindow.OpenedMainPanelType == FEMainPanelType.None) return false;
        if (MainWindow.OpenedMainPanelType == FEMainPanelType.Analysis) {
            return pageRoot != null && pageRoot.gameObject.activeInHierarchy;
        }
        return true;
    }

    public static void UpdateUI() {
        if (!IsPageVisible()) return;

        int shardCount = GetItemCount(IFE残片);
        int normalCount = GetItemCount(IFE普通抽卡券);
        int featuredCount = GetItemCount(IFE精选抽卡券);
        int normalPoints = GachaManager.GetPoolPointsByTicket(IFE普通抽卡券);
        int featuredPoints = GachaManager.GetPoolPointsByTicket(IFE精选抽卡券);

        if (normalTab != null && normalTab.gameObject.activeSelf) {
            if (txtNormalTabShardCount != null) txtNormalTabShardCount.text = $"x {shardCount}";
            if (txtNormalTabTicketCount != null) txtNormalTabTicketCount.text = $"x {normalCount}";
            if (txtNormalTabPointCount != null) txtNormalTabPointCount.text = $"{"普通积分".Translate()}: {normalPoints}";
            for (int i = 0; i < normalRows.Count; i++) {
                RefreshRow(normalRows[i]);
            }
        }
        if (featuredTab != null && featuredTab.gameObject.activeSelf) {
            if (txtFeaturedTabShardCount != null) txtFeaturedTabShardCount.text = $"x {shardCount}";
            if (txtFeaturedTabTicketCount != null) txtFeaturedTabTicketCount.text = $"x {featuredCount}";
            if (txtFeaturedTabPointCount != null) txtFeaturedTabPointCount.text = $"{"精选积分".Translate()}: {featuredPoints}";
            for (int i = 0; i < featuredRows.Count; i++) {
                RefreshRow(featuredRows[i]);
            }
        }
    }

    public static void Import(BinaryReader r) { r.ReadBlocks(); }
    public static void Export(BinaryWriter w) { w.WriteBlocks(); }
    public static void IntoOtherSave() { }
}
