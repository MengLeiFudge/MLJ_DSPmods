using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.Logic.RecipeGrowth;
using FE.UI.Components;
using FE.UI.View.DrawGrowth;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Components.GridDsl;
using static FE.Logic.Manager.ProcessManager;
using static FE.Logic.Manager.RecipeManager;
using static FE.Logic.Recipe.ERecipeExtension;
using static FE.Utils.Utils;

namespace FE.UI.View.Archive;

public static class FracStatistic {
    private static RectTransform tab;
    private static PageLayout.HeaderRefs header;
    private static Text txtSummaryTitle;
    private static Text txtGrowthTitle;
    private static Text txtStockTitle;
    private static Text txtEconomyTitle;
    private static readonly Text[] summaryLines = new Text[7];
    private static readonly Text[] growthLines = new Text[6];
    private static readonly Text[] stockLines = new Text[6];
    private static readonly Text[] economyLines = new Text[6];
    private static readonly int[] trackedBuildingIds = [
        IFE交互塔, IFE矿物复制塔, IFE点数聚集塔, IFE转化塔, IFE精馏塔, IFE行星内物流交互站
    ];

    public static void AddTranslations() {
        Register("分馏统计", "Frac Statistic");
        Register("统计-分馏成功总数", "Total Fraction Successes", "分馏成功总数");
        Register("统计-抽取总次数", "Total Draw Count", "抽取总次数");
        Register("统计-配方解锁", "Unlocked Recipes", "配方解锁");
        Register("统计-最高建筑等级", "Max Building Level", "最高建筑等级");
        Register("统计-当前模式", "Current Mode", "当前模式");
        Register("统计-当前聚焦", "Current Focus", "当前聚焦");
        Register("统计-当前阶段矩阵", "Current Stage Matrix", "当前阶段矩阵");
        Register("统计-黑雾矩阵库存", "Dark Fog Matrix Stock", "黑雾矩阵库存");
        Register("统计-建筑成长经验", "Building Growth EXP", "建筑成长经验");
        Register("统计-原胚库存", "Proto Stock", "原胚库存");
        Register("统计-总览", "Overview", "总览");
        Register("统计-建筑成长", "Building Growth", "建筑成长");
        Register("统计-资源库存", "Resource Stock", "资源库存");
        Register("统计-动态经济", "Dynamic Economy", "动态经济");
        Register("统计-残片余额", "Fragment Stock", "残片余额");
        Register("统计-成长池积分", "Growth Points", "成长池积分");
        Register("统计-市场下次刷新", "Market Refresh", "市场下次刷新");
        Register("统计-市场热度", "Market Heat", "市场热度");
        Register("统计-交易所概览", "Exchange Overview", "交易所概览");
        Register("统计-页头摘要", "Growth, stock and economy overview", "成长、库存与动态经济总览");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        tab = trans;
        BuildLayout(wnd, tab,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), 1, 1],
                rowGap: PageLayout.Gap,
                cols: [1, 1],
                columnGap: PageLayout.Gap,
                children: [
                    Header("分馏统计", objectName: "frac-statistic-header", pos: (0, 0), span: (1, 2), onBuilt: refs => header = refs),
                    ContentCard(pos: (1, 0), objectName: "frac-stat-summary-card", strong: true,
                        children: [Node(pos: (0, 0), objectName: "frac-stat-summary-body", build: (w, summaryCard) => {
                            txtSummaryTitle = PageLayout.AddCardTitle(w, summaryCard, 18f, 14f, "统计-总览", 16, "frac-stat-summary-title");
                            CreateLineGroup(w, summaryLines, summaryCard, 18f, 52f, "txtSummary");
                        })]),
                    ContentCard(pos: (1, 1), objectName: "frac-stat-stock-card", strong: true,
                        children: [Node(pos: (0, 0), objectName: "frac-stat-stock-body", build: (w, stockCard) => {
                            txtStockTitle = PageLayout.AddCardTitle(w, stockCard, 18f, 14f, "统计-资源库存", 16, "frac-stat-stock-title");
                            CreateLineGroup(w, stockLines, stockCard, 18f, 52f, "txtStock");
                        })]),
                    ContentCard(pos: (2, 0), objectName: "frac-stat-growth-card",
                        children: [Node(pos: (0, 0), objectName: "frac-stat-growth-body", build: (w, growthCard) => {
                            txtGrowthTitle = PageLayout.AddCardTitle(w, growthCard, 18f, 14f, "统计-建筑成长", 16, "frac-stat-growth-title");
                            CreateLineGroup(w, growthLines, growthCard, 18f, 52f, "txtGrowth");
                        })]),
                    ContentCard(pos: (2, 1), objectName: "frac-stat-economy-card",
                        children: [Node(pos: (0, 0), objectName: "frac-stat-economy-body", build: (w, economyCard) => {
                            txtEconomyTitle = PageLayout.AddCardTitle(w, economyCard, 18f, 14f, "统计-动态经济", 16, "frac-stat-economy-title");
                            CreateLineGroup(w, economyLines, economyCard, 18f, 52f, "txtEconomy");
                        })]),
                ]));
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        header.Title.text = "分馏统计".Translate().WithColor(Orange);
        header.Summary.text = "统计-页头摘要".Translate().WithColor(White);
        txtSummaryTitle.text = "统计-总览".Translate().WithColor(Orange);
        txtGrowthTitle.text = "统计-建筑成长".Translate().WithColor(Orange);
        txtStockTitle.text = "统计-资源库存".Translate().WithColor(Orange);
        txtEconomyTitle.text = "统计-动态经济".Translate().WithColor(Orange);

        long drawCount = MainWindow.SharedPanelState?.TicketRaffleTotalDraws ?? TicketRaffle.totalDraws;
        int unlockedRecipes = GetUnlockedRecipeCount();
        int totalRecipes = RecipeTypes.Sum(type => GetRecipesByType(type).Count);
        int maxBuildingLevel = trackedBuildingIds
            .Select(id => LDB.items.Select(id)?.Level() ?? 0)
            .Max();
        int currentMatrixId = ItemManager.GetCurrentProgressMatrixId();
        int currentStageIndex = ItemManager.GetCurrentProgressStageIndex();
        string currentMatrixName = LDB.items.Select(currentMatrixId)?.name ?? currentMatrixId.ToString();
        string focusName = GachaService.GetFocusName(GachaManager.CurrentFocus);

        summaryLines[0].text = $"{ "统计-分馏成功总数".Translate() }：{totalFractionSuccesses}";
        summaryLines[1].text = $"{ "统计-抽取总次数".Translate() }：{drawCount}";
        summaryLines[2].text = $"{ "统计-配方解锁".Translate() }：{unlockedRecipes}/{totalRecipes}";
        summaryLines[3].text = $"{ "统计-最高建筑等级".Translate() }：{maxBuildingLevel}";
        summaryLines[4].text = $"{ "统计-当前模式".Translate() }：{GachaService.GetModeNameKey().Translate()}";
        summaryLines[5].text = $"{ "统计-当前聚焦".Translate() }：{focusName}";
        summaryLines[6].text = $"{ "统计-当前阶段矩阵".Translate() }：{currentMatrixName}  (阶段 {currentStageIndex + 1})";

        stockLines[0].text = $"{ "统计-残片余额".Translate() }：{GetItemTotalCount(IFE残片)}";
        stockLines[1].text = $"{ "统计-成长池积分".Translate() }：{GachaManager.GetPoolPoints(GachaPool.PoolIdGrowth)}";
        stockLines[2].text = $"{ "统计-当前阶段矩阵".Translate() }：{currentMatrixName} x{GetItemTotalCount(currentMatrixId)}";
        stockLines[3].text = $"{ "统计-黑雾矩阵库存".Translate() }：{GetItemTotalCount(I黑雾矩阵)}";
        stockLines[4].text = $"{ "统计-原胚库存".Translate() }：{GetProtoSummary()}";
        stockLines[5].text = $"{ "统计-建筑成长经验".Translate() }：{GetBuildingExpTotal()}";

        RefreshGrowthLines();
        RefreshEconomyLines();
    }

    private static void CreateLineGroup(MyWindow wnd, Text[] lines, RectTransform parent, float x, float startY,
        string keyPrefix) {
        float y = startY;
        for (int i = 0; i < lines.Length; i++) {
            lines[i] = wnd.AddText2(x, y, parent, "", 13, $"{keyPrefix}{i}");
            lines[i].supportRichText = true;
            lines[i].rectTransform.sizeDelta = new Vector2(470f, 28f);
            y += 32f;
        }
    }

    private static void RefreshGrowthLines() {
        for (int i = 0; i < growthLines.Length; i++) {
            if (i >= trackedBuildingIds.Length) {
                growthLines[i].text = "";
                continue;
            }
            int buildingId = trackedBuildingIds[i];
            ItemProto building = LDB.items.Select(buildingId);
            int level = building?.Level() ?? 0;
            if (building == null) {
                growthLines[i].text = string.Empty;
                continue;
            }
            if (level >= MaxLevel) {
                growthLines[i].text = $"{building.name}  Lv{level}  已满级".WithColor(Gold);
                continue;
            }
            if (BuildingManager.NeedsBreakthrough(buildingId)) {
                (int matrixId, int matrixCount, int fragmentCount) = BuildingManager.GetBreakthroughCost(level);
                string matrixName = LDB.items.Select(matrixId)?.name ?? matrixId.ToString();
                growthLines[i].text = $"{building.name}  Lv{level}  突破：{matrixName} x{matrixCount} + 残片 x{fragmentCount}".WithColor(Orange);
                continue;
            }
            long currentExp = BuildingManager.GetBuildingExp(buildingId);
            long nextExp = BuildingManager.GetRequiredExpForNextLevel(buildingId);
            growthLines[i].text = $"{building.name}  Lv{level}  经验 {currentExp}/{nextExp}".WithColor(level / 3 + 1);
        }
    }

    private static void RefreshEconomyLines() {
        int hotItemId = MarketValueManager.GetTopMarketItems(1, descending: true).FirstOrDefault();
        int coldItemId = MarketValueManager.GetTopMarketItems(1, descending: false).FirstOrDefault();
        ExchangeManager.ExchangeTicker hotTicker = ExchangeManager.ListedItems
            .Select(ExchangeManager.GetTicker)
            .Where(ticker => ticker != null)
            .OrderByDescending(ticker => Mathf.Abs(ticker.NetPlayerVolume))
            .ThenByDescending(ticker => ticker.LastTradeTick)
            .FirstOrDefault();

        economyLines[0].text = $"{ "统计-市场下次刷新".Translate() }：{FormatSeconds(MarketValueManager.GetRefreshRemainingSeconds())}  (v{MarketValueManager.RefreshVersion})";
        economyLines[1].text = $"{ "统计-市场热度".Translate() }：最热 {FormatMarketItem(hotItemId)}";
        economyLines[2].text = $"{ "统计-市场热度".Translate() }：最冷 {FormatMarketItem(coldItemId)}";
        economyLines[3].text = $"{ "统计-交易所概览".Translate() }：上市 {ExchangeManager.ListedItems.Count} 项 / 订单 {MarketBoardManager.ActiveOffers.Count} 条";
        economyLines[4].text = hotTicker == null
            ? $"{ "统计-交易所概览".Translate() }：暂无活跃成交"
            : $"{ "统计-交易所概览".Translate() }：{LDB.items.Select(hotTicker.ItemId)?.name} 现价 {hotTicker.LastPrice:F1}  净量 {hotTicker.NetPlayerVolume}";
        economyLines[5].text = $"{ "统计-当前阶段矩阵".Translate() }：生产 {MarketValueManager.GetCurrentProductionRate(ItemManager.GetCurrentProgressMatrixId()):F1}/m  消耗 {MarketValueManager.GetCurrentConsumeRate(ItemManager.GetCurrentProgressMatrixId()):F1}/m";
    }

    private static string GetProtoSummary() {
        long interaction = GetItemTotalCount(IFE交互塔原胚);
        long mineral = GetItemTotalCount(IFE矿物复制塔原胚);
        long point = GetItemTotalCount(IFE点数聚集塔原胚);
        long conversion = GetItemTotalCount(IFE转化塔原胚);
        long rectification = GetItemTotalCount(IFE精馏塔原胚);
        long directed = GetItemTotalCount(IFE分馏塔定向原胚);
        long total = interaction + mineral + point + conversion + rectification + directed;
        return $"{total}  (交互 {interaction} / 复制 {mineral} / 聚集 {point} / 转化 {conversion} / 精馏 {rectification} / 定向 {directed})";
    }

    private static long GetBuildingExpTotal() {
        long total = 0L;
        foreach (int buildingId in trackedBuildingIds) {
            total += BuildingManager.GetBuildingExp(buildingId);
        }
        return total;
    }

    private static int GetUnlockedRecipeCount() {
        return RecipeGrowthQueries.GetUnlockedCount(RecipeTypes);
    }

    private static string FormatMarketItem(int itemId) {
        if (itemId <= 0 || !LDB.items.Exist(itemId)) {
            return "-";
        }
        return $"{LDB.items.Select(itemId).name}  ×{MarketValueManager.GetMultiplier(itemId):F2}";
    }

    private static string FormatSeconds(int totalSeconds) {
        totalSeconds = Mathf.Max(0, totalSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        r.ReadBlocks();
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks();
    }

    public static void IntoOtherSave() { }

    #endregion
}
