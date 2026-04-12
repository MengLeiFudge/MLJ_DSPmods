using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Logic.Manager;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
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
        header = PageLayout.CreatePageHeader(wnd, tab, "交易所", "", "exchange-header");

        float top = PageLayout.HeaderHeight + PageLayout.Gap;
        RectTransform infoCard = PageLayout.CreateContentCard(tab, "exchange-info-card", 0f, top, 410f, 190f, true);
        RectTransform actionCard = PageLayout.CreateContentCard(tab, "exchange-action-card", 410f + PageLayout.Gap,
            top, PageLayout.DesignWidth - 410f - PageLayout.Gap, 190f, true);
        RectTransform marketCard = PageLayout.CreateContentCard(tab, "exchange-market-card", 0f,
            top + 190f + PageLayout.Gap, PageLayout.DesignWidth, 455f);

        txtInfoTitle = PageLayout.AddCardTitle(wnd, infoCard, 18f, 14f, "当前标的", 15, "exchange-info-title");
        txtActionTitle = PageLayout.AddCardTitle(wnd, actionCard, 18f, 14f, "快捷操作", 15, "exchange-action-title");
        txtMarketTitle = PageLayout.AddCardTitle(wnd, marketCard, 18f, 14f, "市场概览", 15, "exchange-market-title");

        float y = 60f;
        btnSelectedItem = wnd.AddImageButton(18f, y, infoCard, null).WithSize(40f, 40f)
            .WithClickEvent(() => OpenItemPicker(y + 18f), () => OpenItemPicker(y + 18f));
        txtPrice = wnd.AddText2(78f, y, infoCard, "", 13);
        txtPrice.rectTransform.sizeDelta = new Vector2(300f, 24f);
        y += 34f;
        txtInventory = wnd.AddText2(78f, y, infoCard, "", 13);
        txtInventory.rectTransform.sizeDelta = new Vector2(300f, 24f);

        y = 60f;
        btnBuy1 = wnd.AddButton(18f, y, 150f, actionCard, "买1", onClick: () => Trade(true, 1));
        btnBuy10 = wnd.AddButton(184f, y, 150f, actionCard, "买10", onClick: () => Trade(true, 10));
        btnBuy100 = wnd.AddButton(350f, y, 150f, actionCard, "买100", onClick: () => Trade(true, 100));
        y += 44f;
        btnSell1 = wnd.AddButton(18f, y, 150f, actionCard, "卖1", onClick: () => Trade(false, 1));
        btnSell10 = wnd.AddButton(184f, y, 150f, actionCard, "卖10", onClick: () => Trade(false, 10));
        btnSell100 = wnd.AddButton(350f, y, 150f, actionCard, "卖100", onClick: () => Trade(false, 100));

        txtStats = wnd.AddText2(18f, 56f, marketCard, "", 13);
        txtStats.supportRichText = true;
        txtStats.alignment = TextAnchor.UpperLeft;
        txtStats.rectTransform.sizeDelta = new Vector2(PageLayout.DesignWidth - 36f, 320f);
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
