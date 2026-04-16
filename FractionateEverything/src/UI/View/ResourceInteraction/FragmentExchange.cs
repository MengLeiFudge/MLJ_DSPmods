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
    private static PageLayout.HeaderRefs header;
    private static MyImageButton btnSelectedItem;
    private static Text txtQuote;
    private static Text txtBalance;
    private static Text txtQuoteSummary;
    private static Text txtInfoTitle;
    private static Text txtActionTitle;
    private static Text txtQuoteTitle;
    private static UIButton btnBuy1;
    private static UIButton btnBuy10;
    private static UIButton btnBuy100;

    private static int selectedItemId = I电磁矩阵;

    public static void AddTranslations() {
        Register("残片兑换", "Fragment Exchange");
        Register("兑换价格", "Quote");
        Register("当前持有", "Balance");
        Register("目标物品", "Target Item", "目标物品");
        Register("快速兑换", "Quick Exchange", "快速兑换");
        Register("兑换摘要", "Exchange Summary", "兑换摘要");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        tab = trans;
        header = PageLayout.CreatePageHeader(wnd, tab, "残片兑换", "", "fragment-exchange-header");

        float top = PageLayout.HeaderHeight + PageLayout.Gap;
        RectTransform infoCard = PageLayout.CreateContentCard(tab, "fragment-exchange-info-card", 0f, top, 410f, 190f,
            true);
        RectTransform actionCard = PageLayout.CreateContentCard(tab, "fragment-exchange-action-card",
            410f + PageLayout.Gap, top, PageLayout.DesignWidth - 410f - PageLayout.Gap, 190f, true);
        RectTransform quoteCard = PageLayout.CreateContentCard(tab, "fragment-exchange-quote-card", 0f,
            top + 190f + PageLayout.Gap, PageLayout.DesignWidth, 455f);

        txtInfoTitle = PageLayout.AddCardTitle(wnd, infoCard, 18f, 14f, "目标物品", 15, "fragment-exchange-info-title");
        txtActionTitle = PageLayout.AddCardTitle(wnd, actionCard, 18f, 14f, "快速兑换", 15, "fragment-exchange-action-title");
        txtQuoteTitle = PageLayout.AddCardTitle(wnd, quoteCard, 18f, 14f, "兑换摘要", 15, "fragment-exchange-quote-title");

        float y = 60f;
        btnSelectedItem = wnd.AddImageButton(18f, y, infoCard, null).WithSize(40f, 40f)
            .WithClickEvent(() => OpenItemPicker(y + 18f), () => OpenItemPicker(y + 18f));
        txtQuote = wnd.AddText2(78f, y, infoCard, "", 13);
        txtQuote.rectTransform.sizeDelta = new Vector2(300f, 24f);
        y += 34f;
        txtBalance = wnd.AddText2(78f, y, infoCard, "", 13);
        txtBalance.rectTransform.sizeDelta = new Vector2(300f, 24f);

        y = 60f;
        btnBuy1 = wnd.AddButton(18f, y, 150f, actionCard, "买1", onClick: () => ExchangeItems(1));
        btnBuy10 = wnd.AddButton(184f, y, 150f, actionCard, "买10", onClick: () => ExchangeItems(10));
        btnBuy100 = wnd.AddButton(350f, y, 150f, actionCard, "买100", onClick: () => ExchangeItems(100));

        txtQuoteSummary = wnd.AddText2(18f, 56f, quoteCard, "", 13, "fragment-exchange-quote-summary");
        txtQuoteSummary.supportRichText = true;
        txtQuoteSummary.alignment = TextAnchor.UpperLeft;
        txtQuoteSummary.rectTransform.sizeDelta = new Vector2(PageLayout.DesignWidth - 36f, 320f);
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
        header.Title.text = "残片兑换".Translate().WithColor(Orange);
        header.Summary.text = item == null ? string.Empty : $"{ "目标物品".Translate() }：{item.name}".WithColor(White);
        txtInfoTitle.text = "目标物品".Translate().WithColor(Orange);
        txtActionTitle.text = "快速兑换".Translate().WithColor(Orange);
        txtQuoteTitle.text = "兑换摘要".Translate().WithColor(Orange);
        btnSelectedItem.Proto = item;
        btnSelectedItem.SetCount(GetItemTotalCount(selectedItemId));
        txtQuote.text = $"{ "兑换价格".Translate() }：残片 x{quote.FragmentCost}";
        txtBalance.text = $"{ "当前持有".Translate() }：物品 {GetItemTotalCount(selectedItemId)}    残片 {GetItemTotalCount(IFE残片)}";
        txtQuoteSummary.text = $"{ "当前持有".Translate() }：物品 {GetItemTotalCount(selectedItemId)}    残片 {GetItemTotalCount(IFE残片)}\n"
            + $"批量兑换预估：10 次需要 {quote.FragmentCost * 10} 残片，100 次需要 {quote.FragmentCost * 100} 残片";
        btnBuy1.SetText($"{ "买1".Translate() } ({quote.FragmentCost})");
        btnBuy10.SetText($"{ "买10".Translate() } ({quote.FragmentCost * 10})");
        btnBuy100.SetText($"{ "买100".Translate() } ({quote.FragmentCost * 100})");
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
