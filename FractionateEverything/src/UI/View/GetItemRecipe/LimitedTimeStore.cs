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
    private sealed class GrowthRowUi {
        public GachaGrowthOffer Offer;
        public Text TxtCost;
        public Text TxtReward;
        public UIButton BtnExchange;
    }

    private sealed class GrowthPageUi {
        public RectTransform Tab;
        public Text TxtResource;
        public Text TxtFocus;
        public readonly List<GrowthRowUi> Rows = [];
    }

    private sealed class FocusPageUi {
        public RectTransform Tab;
        public Text TxtCurrentFocus;
        public readonly List<UIButton> Buttons = [];
    }

    private static RectTransform pageRoot;
    private static GrowthPageUi growthPage;
    private static FocusPageUi focusPage;

    public static void AddTranslations() {
        Register("成长规划", "Growth Planning");
        Register("流派聚焦页", "Focus Control");
        Register("资源统筹", "Resource Overview");
        Register("前往抽取", "Go Draw");
        Register("兑换", "Exchange");
        Register("当前资源", "Resource");
        Register("当前聚焦", "Current Focus");
        Register("切换聚焦", "Switch Focus");
        Register("已生效", "Active");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateRecipeUI(MyConfigWindow wnd, RectTransform trans) => CreateGrowthUI(wnd, trans, "成长规划");
    public static void CreateProtoUI(MyConfigWindow wnd, RectTransform trans) => CreateFocusUI(wnd, trans, "流派聚焦页");
    public static void CreateUpUI(MyConfigWindow wnd, RectTransform trans) => CreateGrowthUI(wnd, trans, "成长规划");
    public static void CreateLimitedUI(MyConfigWindow wnd, RectTransform trans) => CreateFocusUI(wnd, trans, "流派聚焦页");

    private static void CreateGrowthUI(MyConfigWindow wnd, RectTransform trans, string pageName) {
        pageRoot = trans;
        growthPage = new GrowthPageUi {
            Tab = wnd.AddTab(trans, pageName)
        };

        float y = 8f;
        growthPage.TxtResource = MyWindow.AddText(0f, y, growthPage.Tab, "", 13);
        y += 24f;
        growthPage.TxtFocus = MyWindow.AddText(0f, y, growthPage.Tab, "", 13);
        y += 36f;

        wnd.AddButton(780f, 8f, 140f, growthPage.Tab, "前往抽取".Translate(), 13,
            onClick: () => MainWindow.NavigateToPage(MainWindowPageRegistry.GachaCategoryName, 0));

        foreach (var offer in GachaService.GetGrowthOffers()) {
            var row = new GrowthRowUi {
                Offer = offer,
                TxtCost = MyWindow.AddText(0f, y, growthPage.Tab, "", 13),
                TxtReward = MyWindow.AddText(330f, y, growthPage.Tab, "", 13),
            };
            row.BtnExchange = wnd.AddButton(760f, y - 4f, 120f, growthPage.Tab, "兑换".Translate(), 13,
                onClick: () => ExchangeOffer(row.Offer));
            row.TxtCost.rectTransform.sizeDelta = new Vector2(320f, 24f);
            row.TxtReward.rectTransform.sizeDelta = new Vector2(420f, 24f);
            growthPage.Rows.Add(row);
            y += 30f;
        }
    }

    private static void CreateFocusUI(MyConfigWindow wnd, RectTransform trans, string pageName) {
        pageRoot = trans;
        focusPage = new FocusPageUi {
            Tab = wnd.AddTab(trans, pageName)
        };

        float y = 8f;
        focusPage.TxtCurrentFocus = MyWindow.AddText(0f, y, focusPage.Tab, "", 14);
        y += 40f;

        wnd.AddButton(780f, 8f, 140f, focusPage.Tab, "前往抽取".Translate(), 13,
            onClick: () => MainWindow.NavigateToPage(MainWindowPageRegistry.GachaCategoryName, 0));

        foreach (var focus in GachaService.FocusDefinitions) {
            float currentY = y;
            var button = wnd.AddButton(0f, currentY, 280f, focusPage.Tab, "", 13,
                onClick: () => ChangeFocus(focus.FocusType));
            MyWindow.AddText(300f, currentY + 4f, focusPage.Tab,
                $"{focus.NameKey.Translate()}：{focus.DescKey.Translate()}", 13)
                .rectTransform.sizeDelta = new Vector2(620f, 24f);
            focusPage.Buttons.Add(button);
            y += 34f;
        }
    }

    private static void ExchangeOffer(GachaGrowthOffer offer) {
        if (offer.PointCost > 0 && !GachaManager.TryConsumePoolPoints(GachaPool.PoolIdGrowth, offer.PointCost)) {
            return;
        }
        if (offer.FragmentCost > 0 && !TakeItemWithTip(IFE残片, offer.FragmentCost, out _)) {
            GachaManager.AddPoolPoints(GachaPool.PoolIdGrowth, offer.PointCost);
            return;
        }

        AddItemToModData(offer.OutputId, offer.OutputCount, 0, true);
        UIRealtimeTip.Popup($"获得 {GetItemName(offer.OutputId)} x{offer.OutputCount}");
        UpdateUI();
    }

    private static void ChangeFocus(GachaFocusType focusType) {
        if (!GachaService.TryChangeFocus(focusType)) {
            return;
        }

        UpdateUI();
    }

    private static string GetItemName(int itemId) {
        return itemId > 0 ? (LDB.items.Select(itemId)?.name ?? itemId.ToString()) : "-";
    }

    public static void UpdateUI() {
        int matrixId = GachaService.GetCurrentDrawMatrixId();
        string resourceText =
            $"{"当前资源".Translate()}：{GetItemName(matrixId)} x{GetItemTotalCount(matrixId)}    "
            + $"残片 x{GetItemTotalCount(IFE残片)}    "
            + $"成长积分 x{GachaManager.GetPoolPoints(GachaPool.PoolIdGrowth)}";
        string focusText = $"{"当前聚焦".Translate()}：{GetCurrentFocusName()}";

        if (growthPage?.Tab != null && growthPage.Tab.gameObject.activeSelf) {
            growthPage.TxtResource.text = resourceText;
            growthPage.TxtFocus.text = focusText;
            for (int i = 0; i < growthPage.Rows.Count; i++) {
                GachaGrowthOffer offer = growthPage.Rows[i].Offer;
                growthPage.Rows[i].TxtCost.text =
                    $"积分 {offer.PointCost} + 残片 {offer.FragmentCost}";
                growthPage.Rows[i].TxtReward.text =
                    $"{GetItemName(offer.OutputId)} x{offer.OutputCount}";
                bool canBuy = GachaManager.GetPoolPoints(GachaPool.PoolIdGrowth) >= offer.PointCost
                              && GetItemTotalCount(IFE残片) >= offer.FragmentCost;
                growthPage.Rows[i].BtnExchange.button.interactable = canBuy;
            }
        }

        if (focusPage?.Tab != null && focusPage.Tab.gameObject.activeSelf) {
            focusPage.TxtCurrentFocus.text = focusText;
            for (int i = 0; i < focusPage.Buttons.Count && i < GachaService.FocusDefinitions.Count; i++) {
                var focus = GachaService.FocusDefinitions[i];
                int fragmentCost = GachaService.GetFocusSwitchFragmentCost(focus.FocusType);
                bool active = focus.FocusType == GachaManager.CurrentFocus;
                string buttonText = active
                    ? $"{focus.NameKey.Translate()} ({ "已生效".Translate() })"
                    : $"{focus.NameKey.Translate()} ({ "切换聚焦".Translate() }: {fragmentCost})";
                focusPage.Buttons[i].SetText(buttonText);
                focusPage.Buttons[i].button.interactable = active || GetItemTotalCount(IFE残片) >= fragmentCost;
            }
        }
    }

    private static string GetCurrentFocusName() {
        foreach (var focus in GachaService.FocusDefinitions) {
            if (focus.FocusType == GachaManager.CurrentFocus) {
                return focus.NameKey.Translate();
            }
        }
        return GachaManager.CurrentFocus.ToString();
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        pageRoot = trans;
        CreateGrowthUI(wnd, trans, "成长规划");
        CreateFocusUI(wnd, trans, "流派聚焦页");
    }

    public static void Import(BinaryReader r) { r.ReadBlocks(); }
    public static void Export(BinaryWriter w) { w.WriteBlocks(); }
    public static void IntoOtherSave() { }
}
