using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using FE.UI.Components;
using FE.UI.View;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.View.GetItemRecipe;

public static class LimitedTimeStore {
    private sealed class ExchangeRowUi {
        public int InputId;
        public int InputCost;
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
    private static Text txtFeaturedTabShardCount;
    private static Text txtFeaturedTabTicketCount;

    private const float RowH = 46f;

    private static RectTransform pageRoot;

    public static void AddTranslations() {
        Register("奖券兑换", "Ticket Exchange");
        Register("普通奖券兑换", "Normal Ticket Exchange");
        Register("精选奖券兑换", "Featured Ticket Exchange");
        Register("兑换分类", "Exchange Categories");
        Register("奖券兑换导航", "Ticket Exchange Navigation");
        Register("持有", "Held");
        Register("最多", "Max");
        Register("兑换", "Exchange");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        pageRoot = trans;
        normalRows.Clear();
        featuredRows.Clear();

        normalTab = wnd.AddTab(trans, "普通奖券兑换");
        featuredTab = wnd.AddTab(trans, "精选奖券兑换");

        BuildTabTopInfo(wnd, normalTab, true);
        BuildTabTopInfo(wnd, featuredTab, false);
        BuildTicketRows(wnd, normalTab, normalRows, IFE普通抽卡券, 42f);
        BuildTicketRows(wnd, featuredTab, featuredRows, IFE精选抽卡券, 42f);
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
            return;
        }
        wnd.AddImageButton(5f, 8f, tab, LDB.items.Select(IFE残片));
        txtFeaturedTabShardCount = MyWindow.AddText(48f, 8f, tab, "x 0", 13);
        wnd.AddImageButton(220f, 8f, tab, LDB.items.Select(IFE精选抽卡券));
        txtFeaturedTabTicketCount = MyWindow.AddText(263f, 8f, tab, "x 0", 13);
    }

    private static void BuildTicketRows(MyConfigWindow wnd, RectTransform page, List<ExchangeRowUi> rows, int ticketId,
        float startY) {
        float y = startY;
        BuildSectionTitle(page, y, ticketId == IFE普通抽卡券 ? "矩阵兑换普通抽卡券" : "矩阵兑换精选抽卡券");
        y += 34f;

        foreach (var (matrixId, matrixCost, outTicketId, ticketCount) in GachaExchangeRate.MatrixRates) {
            if (outTicketId != ticketId) continue;
            rows.Add(CreateRow(wnd, page, y, matrixId, matrixCost, outTicketId, ticketCount));
            y += RowH;
        }

        y += 6f;
        BuildSectionTitle(page, y, "残片兑换抽卡券");
        y += 34f;

        foreach (var (shardCost, outTicketId, ticketCount) in GachaExchangeRate.ShardRates) {
            if (outTicketId != ticketId) continue;
            rows.Add(CreateRow(wnd, page, y, IFE残片, shardCost, outTicketId, ticketCount));
            y += RowH;
        }
    }

    private static void BuildSectionTitle(RectTransform page, float y, string textKey) {
        MyWindow.AddText(0f, y, page, textKey.Translate(), 14);
    }

    private static ExchangeRowUi CreateRow(MyConfigWindow wnd, RectTransform page, float y,
        int inputId, int inputCost, int outputId, int outputCount) {
        var row = new ExchangeRowUi {
            InputId = inputId,
            InputCost = inputCost,
            OutputId = outputId,
            OutputCount = outputCount
        };

        wnd.AddImageButton(0f, y, page, LDB.items.Select(inputId));
        MyWindow.AddText(43f, y + 8f, page, $"x{inputCost} ->", 13);
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
        int inputCount = GetItemCount(row.InputId);
        int maxExchange = row.InputCost > 0 ? inputCount / row.InputCost : 0;
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
        int inputNeed = row.InputCost * selected;
        int outputCount = row.OutputCount * selected;
        if (!TakeItemWithTip(row.InputId, inputNeed, out _)) return;
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

        if (normalTab != null && normalTab.gameObject.activeSelf) {
            if (txtNormalTabShardCount != null) txtNormalTabShardCount.text = $"x {shardCount}";
            if (txtNormalTabTicketCount != null) txtNormalTabTicketCount.text = $"x {normalCount}";
            for (int i = 0; i < normalRows.Count; i++) {
                RefreshRow(normalRows[i]);
            }
        }
        if (featuredTab != null && featuredTab.gameObject.activeSelf) {
            if (txtFeaturedTabShardCount != null) txtFeaturedTabShardCount.text = $"x {shardCount}";
            if (txtFeaturedTabTicketCount != null) txtFeaturedTabTicketCount.text = $"x {featuredCount}";
            for (int i = 0; i < featuredRows.Count; i++) {
                RefreshRow(featuredRows[i]);
            }
        }
    }

    public static void Import(BinaryReader r) { r.ReadBlocks(); }
    public static void Export(BinaryWriter w) { w.WriteBlocks(); }
    public static void IntoOtherSave() { }
}
