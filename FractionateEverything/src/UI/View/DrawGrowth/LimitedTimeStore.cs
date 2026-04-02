using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.UI.Components;
using FE.UI.View;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.View.DrawGrowth;

public static class LimitedTimeStore {
    private const int GrowthRowCount = 8;

    private sealed class GrowthRowUi {
        public bool HasOffer;
        public GachaGrowthOffer Offer;
        public MyImageButton BtnFragmentCostIcon;
        public Text TxtCost;
        public MyImageButton BtnExtraCostIcon;
        public Text TxtExtraCost;
        public MyImageButton BtnRewardIcon;
        public Text TxtReward;
        public UIButton BtnExchange;
    }

    private sealed class GrowthPageUi {
        public RectTransform Tab;
        public Text TxtResource;
        public MyImageButton BtnMatrixIcon;
        public MyImageButton BtnFragmentIcon;
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
        Register("流派聚焦", "Focus Control");
        Register("资源统筹", "Resource Overview");
        Register("前往抽取", "Go Draw");
        Register("兑换", "Exchange");
        Register("当前资源", "Resource");
        Register("当前模式", "Mode");
        Register("当前聚焦", "Current Focus");
        Register("切换聚焦", "Switch Focus");
        Register("已生效", "Active");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateRecipeUI(MyConfigWindow wnd, RectTransform trans) => CreateGrowthUI(wnd, trans, "成长规划");
    public static void CreateProtoUI(MyConfigWindow wnd, RectTransform trans) => CreateFocusUI(wnd, trans, "流派聚焦");
    public static void CreateUpUI(MyConfigWindow wnd, RectTransform trans) => CreateGrowthUI(wnd, trans, "成长规划");
    public static void CreateLimitedUI(MyConfigWindow wnd, RectTransform trans) => CreateFocusUI(wnd, trans, "流派聚焦");

    private static void CreateGrowthUI(MyConfigWindow wnd, RectTransform trans, string pageName) {
        pageRoot = trans;
        growthPage = new GrowthPageUi {
            Tab = wnd.AddTab(trans, pageName)
        };

        float y = 8f;
        growthPage.TxtResource = MyWindow.AddText(0f, y, growthPage.Tab, "当前资源".Translate(), 13);
        y += 24f;
        growthPage.BtnMatrixIcon = MyImageButton.CreateImageButton(0f, y, growthPage.Tab, null).WithSize(40f, 40f);
        growthPage.BtnFragmentIcon = MyImageButton.CreateImageButton(180f, y, growthPage.Tab, LDB.items.Select(IFE残片)).WithSize(40f, 40f);
        y += 40f;
        growthPage.TxtFocus = MyWindow.AddText(0f, y, growthPage.Tab, "", 13);
        y += 36f;

        wnd.AddButton(780f, 8f, 140f, growthPage.Tab, "前往抽取".Translate(), 13,
            onClick: () => MainWindow.NavigateToPage(MainWindowPageRegistry.DrawGrowthCategoryName, 0));

        for (int i = 0; i < GrowthRowCount; i++) {
            var row = new GrowthRowUi {
                BtnFragmentCostIcon = MyImageButton.CreateImageButton(110f, y, growthPage.Tab, LDB.items.Select(IFE残片)).WithSize(40f, 40f),
                TxtCost = MyWindow.AddText(0f, y, growthPage.Tab, "", 13),
                BtnExtraCostIcon = MyImageButton.CreateImageButton(220f, y, growthPage.Tab, null).WithSize(40f, 40f),
                TxtExtraCost = MyWindow.AddText(262f, y, growthPage.Tab, "", 13),
                BtnRewardIcon = MyImageButton.CreateImageButton(420f, y, growthPage.Tab, null).WithSize(40f, 40f),
                TxtReward = MyWindow.AddText(462f, y, growthPage.Tab, "", 13),
            };
            row.BtnExchange = wnd.AddButton(760f, y - 4f, 120f, growthPage.Tab, "兑换".Translate(), 13,
                onClick: () => ExchangeOffer(row));
            row.TxtCost.rectTransform.sizeDelta = new Vector2(100f, 24f);
            row.TxtExtraCost.rectTransform.sizeDelta = new Vector2(120f, 24f);
            row.TxtReward.rectTransform.sizeDelta = new Vector2(220f, 24f);
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
            onClick: () => MainWindow.NavigateToPage(MainWindowPageRegistry.DrawGrowthCategoryName, 0));

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

    private static void ExchangeOffer(GrowthRowUi row) {
        if (!row.HasOffer) {
            return;
        }

        GachaGrowthOffer offer = row.Offer;
        if (offer.PointCost > 0 && !GachaManager.TryConsumePoolPoints(GachaPool.PoolIdGrowth, offer.PointCost)) {
            return;
        }
        if (offer.FragmentCost > 0 && GetItemTotalCount(IFE残片) < offer.FragmentCost) {
            GachaManager.AddPoolPoints(GachaPool.PoolIdGrowth, offer.PointCost);
            return;
        }
        if (offer.ExtraCostItemId > 0 && GetItemTotalCount(offer.ExtraCostItemId) < offer.ExtraCostCount) {
            GachaManager.AddPoolPoints(GachaPool.PoolIdGrowth, offer.PointCost);
            return;
        }
        if (offer.FragmentCost > 0 && !TakeItemWithTip(IFE残片, offer.FragmentCost, out _)) {
            GachaManager.AddPoolPoints(GachaPool.PoolIdGrowth, offer.PointCost);
            return;
        }
        if (offer.ExtraCostItemId > 0 && !TakeItemWithTip(offer.ExtraCostItemId, offer.ExtraCostCount, out _)) {
            GachaManager.AddPoolPoints(GachaPool.PoolIdGrowth, offer.PointCost);
            AddItemToModData(IFE残片, offer.FragmentCost, 0, true);
            return;
        }

        AddItemToModData(offer.OutputId, offer.OutputCount, 0, true);
        string itemName = LDB.items.Select(offer.OutputId)?.name ?? offer.OutputId.ToString();
        UIRealtimeTip.Popup($"获得 {itemName} x{offer.OutputCount}");
        UpdateUI();
    }

    private static void ChangeFocus(GachaFocusType focusType) {
        if (!GachaService.TryChangeFocus(focusType)) {
            return;
        }

        UpdateUI();
    }

    public static void UpdateUI() {
        int matrixId = GachaService.GetCurrentDrawMatrixId();
        string resourceText = $"成长积分 x{GachaManager.GetPoolPoints(GachaPool.PoolIdGrowth)}";
        string focusText = $"{"当前模式".Translate()}：{GachaService.GetModeNameKey().Translate()}    {"当前聚焦".Translate()}：{GetCurrentFocusName()}";

        if (growthPage?.Tab != null && growthPage.Tab.gameObject.activeSelf) {
            growthPage.TxtResource.text = resourceText;
            growthPage.BtnMatrixIcon.Proto = LDB.items.Select(matrixId);
            growthPage.BtnMatrixIcon.SetCount(GetItemTotalCount(matrixId));
            growthPage.BtnFragmentIcon.SetCount(GetItemTotalCount(IFE残片));
            growthPage.TxtFocus.text = focusText;
            IReadOnlyList<GachaGrowthOffer> offers = GachaService.GetGrowthOffers();
            for (int i = 0; i < growthPage.Rows.Count; i++) {
                bool visible = i < offers.Count;
                growthPage.Rows[i].HasOffer = visible;
                growthPage.Rows[i].BtnFragmentCostIcon.gameObject.SetActive(visible);
                growthPage.Rows[i].TxtCost.gameObject.SetActive(visible);
                growthPage.Rows[i].BtnExtraCostIcon.gameObject.SetActive(visible);
                growthPage.Rows[i].TxtExtraCost.gameObject.SetActive(visible);
                growthPage.Rows[i].BtnRewardIcon.gameObject.SetActive(visible);
                growthPage.Rows[i].TxtReward.gameObject.SetActive(visible);
                growthPage.Rows[i].BtnExchange.gameObject.SetActive(visible);
                if (!visible) {
                    continue;
                }

                GachaGrowthOffer offer = offers[i];
                growthPage.Rows[i].Offer = offer;
                growthPage.Rows[i].BtnFragmentCostIcon.SetCount(offer.FragmentCost);
                growthPage.Rows[i].TxtCost.text = $"积分 {offer.PointCost}";
                if (offer.ExtraCostItemId > 0) {
                    growthPage.Rows[i].BtnExtraCostIcon.Proto = LDB.items.Select(offer.ExtraCostItemId);
                    growthPage.Rows[i].BtnExtraCostIcon.gameObject.SetActive(true);
                    growthPage.Rows[i].BtnExtraCostIcon.SetCount(offer.ExtraCostCount);
                    growthPage.Rows[i].TxtExtraCost.text = "";
                } else {
                    growthPage.Rows[i].BtnExtraCostIcon.gameObject.SetActive(false);
                    growthPage.Rows[i].BtnExtraCostIcon.ClearCountText();
                    growthPage.Rows[i].TxtExtraCost.text = "";
                }
                growthPage.Rows[i].BtnRewardIcon.Proto = LDB.items.Select(offer.OutputId);
                growthPage.Rows[i].BtnRewardIcon.SetCount(offer.OutputCount);
                string focusSuffix = offer.FocusType == GachaFocusType.Balanced
                    ? string.Empty
                    : $"  [{offer.FocusType}]".WithColor(Green);
                growthPage.Rows[i].TxtReward.text = focusSuffix;
                bool canBuy = GachaManager.GetPoolPoints(GachaPool.PoolIdGrowth) >= offer.PointCost
                              && GetItemTotalCount(IFE残片) >= offer.FragmentCost
                              && (offer.ExtraCostItemId <= 0 || GetItemTotalCount(offer.ExtraCostItemId) >= offer.ExtraCostCount);
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
        CreateFocusUI(wnd, trans, "流派聚焦");
    }

    public static void Import(BinaryReader r) { r.ReadBlocks(); }
    public static void Export(BinaryWriter w) { w.WriteBlocks(); }
    public static void IntoOtherSave() { }
}
