using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Logic.Economy;
using FE.UI.Controls;
using FE.UI.Foundation.Window;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Layout.GridDsl;
using static FE.Utils.Utils;
using static FE.Logic.DataCenter.PlayerInventoryAccess;
using static FE.UI.Foundation.RectTransformUtils;

namespace FE.UI.MainPanel.ResourceInteraction;

/// <summary>
/// 市场指数与供需观察页面。
/// </summary>
public static class Exchange {
    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static MyImageButton btnSelectedItem;
    private static Text txtPrice;
    private static Text txtInventory;
    private static Text txtStats;
    private static Text txtInfoTitle;
    private static Text txtActionTitle;
    private static Text txtActionSummary;
    private static Text txtMarketTitle;

    private static int selectedItemId = ExchangeManager.ListedItems.Count > 0 ? ExchangeManager.ListedItems[0] : 0;

    public static void AddTranslations() {
        Register("交易所", "Market Index", "市场指数");
        Register("市场指数", "Market Index", "市场指数");
        Register("当前价格", "Index Price", "指数价格");
        Register("库存", "Inventory");
        Register("当前标的", "Selected Listing", "当前标的");
        Register("快捷操作", "Supply Snapshot", "供需快照");
        Register("市场概览", "Market Overview", "市场概览");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        tab = trans;
        BuildLayout(wnd, tab,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), Px(190f), 1],
                rowGap: PageLayout.Gap,
                children: [
                    Header("市场指数", objectName: "exchange-header", pos: (0, 0), onBuilt: refs => header = refs),
                    Grid(
                        pos: (1, 0),
                        cols: [2, 3],
                        columnGap: PageLayout.Gap,
                        children: [
                            ContentCard(
                                pos: (0, 0),
                                objectName: "exchange-info-card",
                                strong: true,
                                rows: [Px(24f), 1],
                                children: [
                                    CardTitleNode("当前标的", onBuilt: text => txtInfoTitle = text,
                                        pos: (0, 0), objectName: "exchange-info-title"),
                                    Grid(
                                        pos: (1, 0),
                                        rows: [1, 1],
                                        cols: [Px(50f), 1],
                                        rowGap: PageLayout.InnerGap,
                                        columnGap: 10f,
                                        children: [
                                            ImageButtonNode(size: 40f,
                                                onBuilt: btn => btnSelectedItem = btn.WithClickEvent(
                                                    () => OpenItemPicker(46f), () => OpenItemPicker(46f)),
                                                pos: (0, 0), span: (2, 1), objectName: "exchange-selected-item"),
                                            TextNode("", 13, onBuilt: text => txtPrice = text,
                                                pos: (0, 1), objectName: "exchange-price"),
                                            TextNode("", 13, onBuilt: text => txtInventory = text,
                                                pos: (1, 1), objectName: "exchange-inventory"),
                                        ]),
                                ]),
                            ContentCard(
                                pos: (0, 1),
                                objectName: "exchange-action-card",
                                strong: true,
                                rows: [Px(24f), 1],
                                children: [
                                    CardTitleNode("快捷操作", onBuilt: text => txtActionTitle = text,
                                        pos: (0, 0), objectName: "exchange-action-title"),
                                    TextNode("", 13, anchor: TextAnchor.UpperLeft, wrap: true,
                                        onBuilt: text => txtActionSummary = text,
                                        pos: (1, 0), objectName: "exchange-action-summary"),
                                ]),
                        ]),
                    ContentCard(
                        pos: (2, 0),
                        objectName: "exchange-market-card",
                        rows: [Px(24f), 1],
                        children: [
                            CardTitleNode("市场概览", onBuilt: text => txtMarketTitle = text,
                                pos: (0, 0), objectName: "exchange-market-title"),
                            TextNode("", 13, anchor: TextAnchor.UpperLeft, wrap: true,
                                onBuilt: text => txtStats = text,
                                pos: (1, 0), objectName: "exchange-market-stats"),
                        ]),
                ]));
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        if (!ExchangeManager.IsListed(selectedItemId) && ExchangeManager.ListedItems.Count > 0) {
            selectedItemId = ExchangeManager.ListedItems[0];
        }
        ExchangeManager.ExchangeTicker ticker = ExchangeManager.GetTicker(selectedItemId);
        ItemProto item = LDB.items.Select(selectedItemId);
        header.Title.text = "市场指数".Translate().WithColor(Orange);
        header.Summary.text = item == null ? string.Empty : $"当前标的：{item.name}".WithColor(White);
        txtInfoTitle.text = "当前标的".Translate().WithColor(Orange);
        txtActionTitle.text = "快捷操作".Translate().WithColor(Orange);
        txtMarketTitle.text = "市场概览".Translate().WithColor(Orange);
        btnSelectedItem.Proto = item;
        btnSelectedItem.SetCount(GetItemTotalCount(selectedItemId));

        if (ticker == null || item == null) {
            txtPrice.text = "";
            txtInventory.text = "";
            txtActionSummary.text = "";
            txtStats.text = "";
            return;
        }

        txtPrice.text =
            $"{"当前价格".Translate()}：{ticker.LastPrice:F2} ({ticker.ChangePercent:+0.00;-0.00;0.00}%)\n买盘指数 {ticker.AskPrice:F2}    卖盘指数 {ticker.BidPrice:F2}";
        txtInventory.text =
            $"{"库存".Translate()}：数据中心 {GetItemTotalCount(selectedItemId)}";
        txtActionSummary.text =
            $"基础价值 {MarketValueManager.GetBaseValue(selectedItemId):F2}\n"
            + $"市场倍率 {MarketValueManager.GetMultiplier(selectedItemId):F2}\n"
            + $"生产速率 {MarketValueManager.LastCurrentRate[selectedItemId]:F1}    消耗速率 {MarketValueManager.LastConsumeRate[selectedItemId]:F1}";
        txtStats.text =
            $"日内开盘 {ticker.DayOpenPrice:F2}\n最新价格 {ticker.LastPrice:F2}\n日高 / 日低 {ticker.DayHighPrice:F2} / {ticker.DayLowPrice:F2}\n市场净流量 {ticker.NetMarketVolume}";
    }

    private static void OpenItemPicker(float y) {
        float popupX = tab.anchoredPosition.x - tab.rect.width / 2;
        float popupY = tab.anchoredPosition.y + tab.rect.height / 2 - y;
        UIItemPickerExtension.Popup(new(popupX, popupY), item => {
            if (item != null && ExchangeManager.IsListed(item.ID)) {
                selectedItemId = item.ID;
                ExchangeManager.RecordObservation(selectedItemId);
            }
        }, true, item => item != null && ExchangeManager.IsListed(item.ID));
    }
}
