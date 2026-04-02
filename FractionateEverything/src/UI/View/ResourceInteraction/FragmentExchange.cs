using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Logic.Manager;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.View.ResourceInteraction;

public static class FragmentExchange {
    private static RectTransform tab;
    private static MyImageButton btnSelectedItem;
    private static Text txtQuote;
    private static Text txtBalance;
    private static UIButton btnBuy1;
    private static UIButton btnBuy10;
    private static UIButton btnBuy100;

    private static int selectedItemId = I电磁矩阵;

    public static void AddTranslations() {
        Register("残片兑换", "Fragment Exchange");
        Register("买1", "Buy 1");
        Register("买10", "Buy 10");
        Register("买100", "Buy 100");
        Register("兑换价格", "Quote");
        Register("当前持有", "Balance");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        tab = wnd.AddTab(trans, "残片兑换");
        float y = 18f;
        btnSelectedItem = wnd.AddImageButton(0f, y, tab, null).WithSize(40f, 40f)
            .WithClickEvent(() => OpenItemPicker(y + 18f), () => OpenItemPicker(y + 18f));
        txtQuote = wnd.AddText2(60f, y, tab, "", 13);
        y += 32f;
        txtBalance = wnd.AddText2(60f, y, tab, "", 13);
        y += 48f;
        btnBuy1 = wnd.AddButton(0f, y, 120f, tab, "买1", onClick: () => ExchangeItems(1));
        btnBuy10 = wnd.AddButton(130f, y, 120f, tab, "买10", onClick: () => ExchangeItems(10));
        btnBuy100 = wnd.AddButton(260f, y, 120f, tab, "买100", onClick: () => ExchangeItems(100));
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        if (!FragmentExchangeManager.CanExchangeItem(selectedItemId)) {
            selectedItemId = I电磁矩阵;
        }
        FragmentExchangeManager.FragmentQuote quote = FragmentExchangeManager.GetQuote(selectedItemId);
        ItemProto item = LDB.items.Select(selectedItemId);
        btnSelectedItem.Proto = item;
        btnSelectedItem.SetCount(GetItemTotalCount(selectedItemId));
        txtQuote.text = $"{ "兑换价格".Translate() }：残片 x{quote.FragmentCost}";
        txtBalance.text = $"{ "当前持有".Translate() }：物品 {GetItemTotalCount(selectedItemId)}    残片 {GetItemTotalCount(IFE残片)}";
        btnBuy1.button.interactable = GetItemTotalCount(IFE残片) >= quote.FragmentCost;
        btnBuy10.button.interactable = GetItemTotalCount(IFE残片) >= (long)quote.FragmentCost * 10;
        btnBuy100.button.interactable = GetItemTotalCount(IFE残片) >= (long)quote.FragmentCost * 100;
    }

    private static void OpenItemPicker(float y) {
        float popupX = tab.anchoredPosition.x - tab.rect.width / 2;
        float popupY = tab.anchoredPosition.y + tab.rect.height / 2 - y;
        UIItemPickerExtension.Popup(new(popupX, popupY), item => {
            if (item != null && FragmentExchangeManager.CanExchangeItem(item.ID)) {
                selectedItemId = item.ID;
            }
        }, true, item => item != null && FragmentExchangeManager.CanExchangeItem(item.ID));
    }

    private static void ExchangeItems(int count) {
        if (FragmentExchangeManager.TryExchange(selectedItemId, count)) {
            UpdateUI();
        }
    }
}
