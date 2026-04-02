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
    private static MyImageButton btnSelectedItem;
    private static Text txtPrice;
    private static Text txtInventory;
    private static Text txtStats;
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
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        tab = wnd.AddTab(trans, "交易所");
        float y = 18f;
        btnSelectedItem = wnd.AddImageButton(0f, y, tab, null).WithSize(40f, 40f)
            .WithClickEvent(() => OpenItemPicker(y + 18f), () => OpenItemPicker(y + 18f));
        txtPrice = wnd.AddText2(60f, y, tab, "", 13);
        y += 32f;
        txtInventory = wnd.AddText2(60f, y, tab, "", 13);
        y += 32f;
        txtStats = wnd.AddText2(60f, y, tab, "", 13);
        txtStats.rectTransform.sizeDelta = new Vector2(700f, 24f);
        y += 48f;

        btnBuy1 = wnd.AddButton(0f, y, 120f, tab, "买1", onClick: () => Trade(true, 1));
        btnBuy10 = wnd.AddButton(130f, y, 120f, tab, "买10", onClick: () => Trade(true, 10));
        btnBuy100 = wnd.AddButton(260f, y, 120f, tab, "买100", onClick: () => Trade(true, 100));
        y += 40f;
        btnSell1 = wnd.AddButton(0f, y, 120f, tab, "卖1", onClick: () => Trade(false, 1));
        btnSell10 = wnd.AddButton(130f, y, 120f, tab, "卖10", onClick: () => Trade(false, 10));
        btnSell100 = wnd.AddButton(260f, y, 120f, tab, "卖100", onClick: () => Trade(false, 100));
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
        btnSelectedItem.Proto = item;
        btnSelectedItem.SetCount(GetItemTotalCount(selectedItemId));

        if (ticker == null || item == null) {
            txtPrice.text = "";
            txtInventory.text = "";
            txtStats.text = "";
            return;
        }

        txtPrice.text = $"{ "当前价格".Translate() }：{ticker.LastPrice:F1}    买入 {ticker.AskPrice:F1}    卖出 {ticker.BidPrice:F1}";
        txtInventory.text = $"{ "库存".Translate() }：物品 {GetItemTotalCount(selectedItemId)}    残片 {GetItemTotalCount(IFE残片)}";
        txtStats.text = $"日内 {ticker.DayOpenPrice:F1} -> {ticker.LastPrice:F1}    高 {ticker.DayHighPrice:F1} / 低 {ticker.DayLowPrice:F1}";
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
