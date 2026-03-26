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
    private sealed class ExchangeRowUi {
        public StorePageUi Page;
        public int PoolId;
        public int PointCost;
        public int OutputId;
        public int OutputCount;
        public MySlider Slider;
        public Text MaxText;
        public UIButton ExchangeButton;
    }

    private sealed class StorePageUi {
        public string PageName;
        public int PoolId;
        public RectTransform Tab;
        public readonly List<ExchangeRowUi> Rows = [];
        public Text TxtShardCount;
        public Text TxtTicketCount;
        public Text TxtPointCount;
        public Text TxtLastExchange;
    }

    private static readonly Dictionary<string, StorePageUi> PageUis = [];
    private const float RowH = 46f;
    private static RectTransform pageRoot;

    private static void BuildTabTopInfo(MyConfigWindow wnd, StorePageUi ui) {
        bool isNormal = ui.PoolId == GachaPool.PoolIdPermanentRecipe || ui.PoolId == GachaPool.PoolIdPermanentBuilding;
        wnd.AddButton(860f, 8f, 100f, ui.Tab, "前往抽奖".Translate(), 13,
            onClick: () => {
                MainWindow.NavigateToPage(MainWindowPageRegistry.GachaCategoryName, ui.PoolId);
            });

        wnd.AddImageButton(5f, 8f, ui.Tab, LDB.items.Select(IFE残片));
        ui.TxtShardCount = MyWindow.AddText(48f, 8f, ui.Tab, "x 0", 13);
        
        int ticketId = isNormal ? IFE普通抽卡券 : IFE精选抽卡券;
        wnd.AddImageButton(220f, 8f, ui.Tab, LDB.items.Select(ticketId));
        ui.TxtTicketCount = MyWindow.AddText(263f, 8f, ui.Tab, "x 0", 13);
        
        ui.TxtPointCount = MyWindow.AddText(420f, 8f, ui.Tab, $"{"池积分".Translate()}: 0", 13);
        ui.TxtLastExchange = MyWindow.AddText(5f, 30f, ui.Tab, "", 12);
        if (ui.TxtLastExchange != null) {
            ui.TxtLastExchange.rectTransform.sizeDelta = new Vector2(840f, 22f);
        }
    }

    private static void BuildPointRows(MyConfigWindow wnd, StorePageUi ui, float startY) {
        float y = startY;
        BuildSectionTitle(ui.Tab, y, "积分兑换");
        y += 34f;

        bool isNormal = ui.PoolId == GachaPool.PoolIdPermanentRecipe || ui.PoolId == GachaPool.PoolIdPermanentBuilding;
        int ticketId = isNormal ? IFE普通抽卡券 : IFE精选抽卡券;

        var rates = GetPoolExchangeRates(ui.PoolId);
        foreach (var (pointCost, outputId, outputCount) in rates) {
            ui.Rows.Add(CreateRow(wnd, ui, y, ui.PoolId, ticketId, pointCost, outputId, outputCount));
            y += RowH;
        }
    }

    private static List<(int pointCost, int outputId, int outputCount)> GetPoolExchangeRates(int poolId) {
        return poolId switch {
            GachaPool.PoolIdPermanentRecipe => [
                (5, IFE残片, 10),
                (10, I电磁矩阵, 1),
                (50, I能量矩阵, 1)
            ],
            GachaPool.PoolIdPermanentBuilding => [
                (5, IFE残片, 10),
                (20, IFE交互塔原胚, 1),
                (20, IFE矿物复制塔原胚, 1)
            ],
            GachaPool.PoolIdUp => [
                (5, IFE残片, 20),
                (30, I结构矩阵, 1),
                (100, I信息矩阵, 1)
            ],
            GachaPool.PoolIdLimited => [
                (5, IFE残片, 20),
                (50, I引力矩阵, 1),
                (200, I宇宙矩阵, 1)
            ],
            _ => []
        };
    }

    public static void AddTranslations() {

        Register("积分商店", "Point Store");
        Register("配方商店", "Recipe Store");
        Register("原胚商店", "Proto Store");
        Register("UP商店", "UP Store");
        Register("限定商店", "Limited Store");
        Register("兑换分类", "Exchange Categories");
        Register("积分商店导航", "Point Store Navigation");
        Register("持有", "Held");
        Register("最多", "Max");
        Register("兑换", "Exchange");
        Register("积分兑换", "Points Exchange");
        Register("池积分", "Pool Points");
        Register("最近兑换", "Recent Exchange");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateRecipeUI(MyConfigWindow wnd, RectTransform trans) => CreateStoreUI(wnd, trans, "配方商店", GachaPool.PoolIdPermanentRecipe);
    public static void CreateProtoUI(MyConfigWindow wnd, RectTransform trans) => CreateStoreUI(wnd, trans, "原胚商店", GachaPool.PoolIdPermanentBuilding);
    public static void CreateUpUI(MyConfigWindow wnd, RectTransform trans) => CreateStoreUI(wnd, trans, "UP商店", GachaPool.PoolIdUp);
    public static void CreateLimitedUI(MyConfigWindow wnd, RectTransform trans) => CreateStoreUI(wnd, trans, "限定商店", GachaPool.PoolIdLimited);

    private static void CreateStoreUI(MyConfigWindow wnd, RectTransform trans, string pageName, int poolId) {
        pageRoot = trans;
        var ui = new StorePageUi {
            PageName = pageName,
            PoolId = poolId,
            Tab = wnd.AddTab(trans, pageName)
        };
        PageUis[pageName] = ui;

        BuildTabTopInfo(wnd, ui);
        BuildPointRows(wnd, ui, 64f);
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        pageRoot = trans;
        PageUis.Clear();
        CreateRecipeUI(wnd, trans);
        CreateProtoUI(wnd, trans);
        CreateUpUI(wnd, trans);
        CreateLimitedUI(wnd, trans);
    }

    private static void BuildSectionTitle(RectTransform page, float y, string textKey) {
        MyWindow.AddText(0f, y, page, textKey.Translate(), 14);
    }

    private static ExchangeRowUi CreateRow(MyConfigWindow wnd, StorePageUi page, float y,
        int poolId, int ticketId, int pointCost, int outputId, int outputCount) {
        var row = new ExchangeRowUi {
            Page = page,
            PoolId = poolId,
            PointCost = pointCost,
            OutputId = outputId,
            OutputCount = outputCount
        };

        wnd.AddImageButton(0f, y, page.Tab, LDB.items.Select(ticketId));
        MyWindow.AddText(43f, y + 8f, page.Tab, $"积分x{pointCost} ->", 13);
        wnd.AddImageButton(108f, y, page.Tab, LDB.items.Select(outputId));
        MyWindow.AddText(151f, y + 8f, page.Tab, $"x{outputCount}", 13);

        row.Slider = wnd.AddSlider(220f, y + 8f, page.Tab, 0f, 0f, 1f, "F0", 280f);
        row.Slider.WithSmallerHandle(6f, 0f);
        row.MaxText = MyWindow.AddText(510f, y + 8f, page.Tab, "", 13);
        row.ExchangeButton = wnd.AddButton(620f, y, 120f, page.Tab, "", 13, onClick: () => ExchangeRow(row));
        row.Slider.OnValueChanged += () => RefreshRow(row);

        RefreshRow(row);
        return row;
    }

    private static int GetItemCount(int itemId) {
        long count = GetItemTotalCount(itemId);
        return count > int.MaxValue ? int.MaxValue : (int)count;
    }

    private static void RefreshRow(ExchangeRowUi row) {
        int points = GachaManager.GetPoolPoints(row.PoolId);
        int maxExchange = row.PointCost > 0 ? points / row.PointCost : 0;
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
        int pointNeed = row.PointCost * selected;
        int outputCount = row.OutputCount * selected;
        if (!GachaManager.TryConsumePoolPoints(row.PoolId, pointNeed)) return;
        AddItemToModData(row.OutputId, outputCount, 0, true);
        row.Slider.Value = 0f;
        string itemName = LDB.items.Select(row.OutputId)?.name ?? row.OutputId.ToString();
        if (row.Page?.TxtLastExchange != null) {
            row.Page.TxtLastExchange.text = $"{ "最近兑换".Translate() }: {itemName} x{outputCount}".WithColor(Green);
        }
        UIRealtimeTip.Popup($"获得 {itemName} x{outputCount}");
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
        foreach (var ui in PageUis.Values) {
            if (ui?.Tab == null || !ui.Tab.gameObject.activeSelf) continue;

            if (ui.TxtShardCount != null) ui.TxtShardCount.text = $"x {shardCount}";
            
            bool isNormal = ui.PoolId == GachaPool.PoolIdPermanentRecipe || ui.PoolId == GachaPool.PoolIdPermanentBuilding;
            int ticketCount = isNormal ? normalCount : featuredCount;
            if (ui.TxtTicketCount != null) ui.TxtTicketCount.text = $"x {ticketCount}";
            
            int points = GachaManager.GetPoolPoints(ui.PoolId);
            if (ui.TxtPointCount != null) ui.TxtPointCount.text = $"{"池积分".Translate()}: {points}";

            for (int i = 0; i < ui.Rows.Count; i++) {
                RefreshRow(ui.Rows[i]);
            }
        }
    }

    public static void Import(BinaryReader r) { r.ReadBlocks(); }
    public static void Export(BinaryWriter w) { w.WriteBlocks(); }
    public static void IntoOtherSave() { }
}
