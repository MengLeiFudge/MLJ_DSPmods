using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Logic.Manager;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using FE.Logic.Economy;
using static FE.UI.Components.GridDsl;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.ResourceInteraction;

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
        BuildLayout(wnd, tab,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), Px(190f), 1],
                rowGap: PageLayout.Gap,
                children: [
                    Header("残片兑换", objectName: "fragment-exchange-header", pos: (0, 0), onBuilt: refs => header = refs),
                    Grid(
                        pos: (1, 0),
                        cols: [2, 3],
                        columnGap: PageLayout.Gap,
                        children: [
                            ContentCard(
                                pos: (0, 0),
                                objectName: "fragment-exchange-info-card",
                                strong: true,
                                rows: [Px(24f), 1],
                                children: [
                                    CardTitleNode("目标物品", onBuilt: text => txtInfoTitle = text,
                                        pos: (0, 0), objectName: "fragment-exchange-info-title"),
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
                                                pos: (0, 0), span: (2, 1),
                                                objectName: "fragment-exchange-selected-item"),
                                            TextNode("", 13, onBuilt: text => txtQuote = text,
                                                pos: (0, 1), objectName: "fragment-exchange-quote"),
                                            TextNode("", 13, onBuilt: text => txtBalance = text,
                                                pos: (1, 1), objectName: "fragment-exchange-balance"),
                                        ]),
                                ]),
                            ContentCard(
                                pos: (0, 1),
                                objectName: "fragment-exchange-action-card",
                                strong: true,
                                rows: [Px(24f), 1],
                                children: [
                                    CardTitleNode("快速兑换", onBuilt: text => txtActionTitle = text,
                                        pos: (0, 0), objectName: "fragment-exchange-action-title"),
                                    Grid(
                                        pos: (1, 0),
                                        cols: [1, 1, 1],
                                        columnGap: PageLayout.InnerGap,
                                        children: [
                                            ButtonNode("买1", onClick: () => ExchangeItems(1),
                                                onBuilt: btn => btnBuy1 = btn,
                                                pos: (0, 0), objectName: "fragment-exchange-buy-1"),
                                            ButtonNode("买10", onClick: () => ExchangeItems(10),
                                                onBuilt: btn => btnBuy10 = btn,
                                                pos: (0, 1), objectName: "fragment-exchange-buy-10"),
                                            ButtonNode("买100", onClick: () => ExchangeItems(100),
                                                onBuilt: btn => btnBuy100 = btn,
                                                pos: (0, 2), objectName: "fragment-exchange-buy-100"),
                                        ]),
                                ]),
                        ]),
                    ContentCard(
                        pos: (2, 0),
                        objectName: "fragment-exchange-quote-card",
                        rows: [Px(24f), 1],
                        children: [
                            CardTitleNode("兑换摘要", onBuilt: text => txtQuoteTitle = text,
                                pos: (0, 0), objectName: "fragment-exchange-quote-title"),
                            TextNode("", 13, anchor: TextAnchor.UpperLeft, wrap: true,
                                onBuilt: text => txtQuoteSummary = text,
                                pos: (1, 0), objectName: "fragment-exchange-quote-summary"),
                        ]),
                ]));
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
        header.Summary.text = item == null ? string.Empty : $"{"目标物品".Translate()}：{item.name}".WithColor(White);
        txtInfoTitle.text = "目标物品".Translate().WithColor(Orange);
        txtActionTitle.text = "快速兑换".Translate().WithColor(Orange);
        txtQuoteTitle.text = "兑换摘要".Translate().WithColor(Orange);
        btnSelectedItem.Proto = item;
        btnSelectedItem.SetCount(GetItemTotalCount(selectedItemId));
        txtQuote.text = $"{"兑换价格".Translate()}：残片 x{quote.FragmentCost}";
        txtBalance.text =
            $"{"当前持有".Translate()}：残片 {GetItemTotalCount(IFE残片)}";
        txtQuoteSummary.text =
            $"{"当前持有".Translate()}：残片 {GetItemTotalCount(IFE残片)}\n"
            + $"批量兑换预估：10 次需要 {quote.FragmentCost * 10} 残片，100 次需要 {quote.FragmentCost * 100} 残片";
        btnBuy1.SetText($"{"买1".Translate()} ({quote.FragmentCost})");
        btnBuy10.SetText($"{"买10".Translate()} ({quote.FragmentCost * 10})");
        btnBuy100.SetText($"{"买100".Translate()} ({quote.FragmentCost * 100})");
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
