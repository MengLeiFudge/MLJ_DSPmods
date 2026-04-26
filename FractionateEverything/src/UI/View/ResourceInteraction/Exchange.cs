using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Logic.Manager;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Components.GridDsl;
using static FE.Utils.Utils;

namespace FE.UI.View.ResourceInteraction;

public static class Exchange {
    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static MyImageButton btnSelectedItem;
    private static Text txtPrice;
    private static Text txtInventory;
    private static Text txtStats;
    private static Text txtInfoTitle;
    private static Text txtActionTitle;
    private static Text txtMarketTitle;
    private static UIButton btnBuy1;
    private static UIButton btnBuy10;
    private static UIButton btnBuy100;
    private static UIButton btnSell1;
    private static UIButton btnSell10;
    private static UIButton btnSell100;

    private static int selectedItemId = ExchangeManager.ListedItems.Count > 0 ? ExchangeManager.ListedItems[0] : 0;

    public static void AddTranslations() {
        Register("交易所", "Exchange");
        Register("买1", "Buy 1");
        Register("买10", "Buy 10");
        Register("买100", "Buy 100");
        Register("卖1", "Sell 1");
        Register("卖10", "Sell 10");
        Register("卖100", "Sell 100");
        Register("当前价格", "Price");
        Register("库存", "Inventory");
        Register("当前标的", "Selected Listing", "当前标的");
        Register("快捷操作", "Quick Actions", "快捷操作");
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
                    Header("交易所", objectName: "exchange-header", pos: (0, 0), onBuilt: refs => header = refs),
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
                                padding: Inset(18f, 14f, 18f, 18f),
                                children: [
                                    Node(pos: (0, 0), objectName: "exchange-info-title-node", build: (w, root) => {
                                        txtInfoTitle = PageLayout.AddCardTitle(w, root, 0f, 0f, "当前标的", 15, "exchange-info-title");
                                    }),
                                    Node(pos: (1, 0), objectName: "exchange-info-body", build: (w, root) => {
                                        float y = 28f;
                                        btnSelectedItem = w.AddImageButton(0f, y, root, null).WithSize(40f, 40f)
                                            .WithClickEvent(() => OpenItemPicker(y + 18f), () => OpenItemPicker(y + 18f));
                                        txtPrice = w.AddText2(60f, y, root, "", 13);
                                        txtPrice.rectTransform.sizeDelta = new Vector2(300f, 24f);
                                        y += 34f;
                                        txtInventory = w.AddText2(60f, y, root, "", 13);
                                        txtInventory.rectTransform.sizeDelta = new Vector2(300f, 24f);
                                    }),
                                ]),
                            ContentCard(
                                pos: (0, 1),
                                objectName: "exchange-action-card",
                                strong: true,
                                rows: [Px(24f), 1],
                                padding: Inset(18f, 14f, 18f, 18f),
                                children: [
                                    Node(pos: (0, 0), objectName: "exchange-action-title-node", build: (w, root) => {
                                        txtActionTitle = PageLayout.AddCardTitle(w, root, 0f, 0f, "快捷操作", 15, "exchange-action-title");
                                    }),
                                    Node(pos: (1, 0), objectName: "exchange-action-body", build: (w, root) => {
                                        float y = 28f;
                                        btnBuy1 = w.AddButton(0f, y, 150f, root, "买1", onClick: () => Trade(true, 1));
                                        btnBuy10 = w.AddButton(166f, y, 150f, root, "买10", onClick: () => Trade(true, 10));
                                        btnBuy100 = w.AddButton(332f, y, 150f, root, "买100", onClick: () => Trade(true, 100));
                                        y += 44f;
                                        btnSell1 = w.AddButton(0f, y, 150f, root, "卖1", onClick: () => Trade(false, 1));
                                        btnSell10 = w.AddButton(166f, y, 150f, root, "卖10", onClick: () => Trade(false, 10));
                                        btnSell100 = w.AddButton(332f, y, 150f, root, "卖100", onClick: () => Trade(false, 100));
                                    }),
                                ]),
                        ]),
                    ContentCard(
                        pos: (2, 0),
                        objectName: "exchange-market-card",
                        rows: [Px(24f), 1],
                        padding: Inset(18f, 14f, 18f, 18f),
                        children: [
                            Node(pos: (0, 0), objectName: "exchange-market-title-node", build: (w, root) => {
                                txtMarketTitle = PageLayout.AddCardTitle(w, root, 0f, 0f, "市场概览", 15, "exchange-market-title");
                            }),
                            Node(pos: (1, 0), objectName: "exchange-market-body", build: (w, root) => {
                                txtStats = w.AddText2(0f, 18f, root, "", 13);
                                txtStats.supportRichText = true;
                                txtStats.alignment = TextAnchor.UpperLeft;
                                txtStats.rectTransform.sizeDelta = new Vector2(PageLayout.DesignWidth - 36f, 320f);
                            }),
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
        header.Title.text = "交易所".Translate().WithColor(Orange);
        header.Summary.text = item == null ? string.Empty : $"当前标的：{item.name}".WithColor(White);
        txtInfoTitle.text = "当前标的".Translate().WithColor(Orange);
        txtActionTitle.text = "快捷操作".Translate().WithColor(Orange);
        txtMarketTitle.text = "市场概览".Translate().WithColor(Orange);
        btnSelectedItem.Proto = item;
        btnSelectedItem.SetCount(GetItemTotalCount(selectedItemId));

        if (ticker == null || item == null) {
            txtPrice.text = "";
            txtInventory.text = "";
            txtStats.text = "";
            return;
        }

        txtPrice.text = $"{ "当前价格".Translate() }：{ticker.LastPrice:F1}\n买入 {ticker.AskPrice:F1}    卖出 {ticker.BidPrice:F1}";
        txtInventory.text = $"{ "库存".Translate() }：物品 {GetItemTotalCount(selectedItemId)}    残片 {GetItemTotalCount(IFE残片)}";
        txtStats.text = $"日内开盘 {ticker.DayOpenPrice:F1}\n最新价格 {ticker.LastPrice:F1}\n日高 / 日低 {ticker.DayHighPrice:F1} / {ticker.DayLowPrice:F1}\n净成交量 {ticker.NetPlayerVolume:F1}";
        btnBuy1.SetText($"{ "买1".Translate() } ({Mathf.CeilToInt(ticker.AskPrice)})");
        btnBuy10.SetText($"{ "买10".Translate() } ({Mathf.CeilToInt(ticker.AskPrice * 10f)})");
        btnBuy100.SetText($"{ "买100".Translate() } ({Mathf.CeilToInt(ticker.AskPrice * 100f)})");
        btnSell1.SetText($"{ "卖1".Translate() } ({Mathf.FloorToInt(ticker.BidPrice)})");
        btnSell10.SetText($"{ "卖10".Translate() } ({Mathf.FloorToInt(ticker.BidPrice * 10f)})");
        btnSell100.SetText($"{ "卖100".Translate() } ({Mathf.FloorToInt(ticker.BidPrice * 100f)})");
        btnSell1.button.interactable = GetItemTotalCount(selectedItemId) >= 1;
        btnSell10.button.interactable = GetItemTotalCount(selectedItemId) >= 10;
        btnSell100.button.interactable = GetItemTotalCount(selectedItemId) >= 100;
    }

    private static void OpenItemPicker(float y) {
        float popupX = tab.anchoredPosition.x - tab.rect.width / 2;
        float popupY = tab.anchoredPosition.y + tab.rect.height / 2 - y;
        UIItemPickerExtension.Popup(new(popupX, popupY), item => {
            if (item != null && ExchangeManager.IsListed(item.ID)) {
                selectedItemId = item.ID;
            }
        }, true, item => item != null && ExchangeManager.IsListed(item.ID));
    }

    private static void Trade(bool isBuy, int count) {
        bool changed = isBuy
            ? ExchangeManager.TryBuy(selectedItemId, count)
            : ExchangeManager.TrySell(selectedItemId, count);
        if (changed) {
            UpdateUI();
        }
    }
}
