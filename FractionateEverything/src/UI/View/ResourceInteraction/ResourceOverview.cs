using System.Linq;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.Logic.RecipeGrowth;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.View.ResourceInteraction;

public static class ResourceOverview {
    private const int DisplayCount = 5;

    private static RectTransform tab;
    private static Text txtTitle;
    private static Text txtRefresh;
    private static Text txtDarkFogTitle;
    private static readonly Text[] darkFogLines = new Text[5];
    private static readonly MyImageButton[] hotIcons = new MyImageButton[DisplayCount];
    private static readonly Text[] hotTexts = new Text[DisplayCount];
    private static readonly MyImageButton[] coldIcons = new MyImageButton[DisplayCount];
    private static readonly Text[] coldTexts = new Text[DisplayCount];

    public static void AddTranslations() {
        Register("资源统筹", "Resource Overview");
        Register("高需求物资", "Hot Demand");
        Register("低需求物资", "Cold Demand");
        Register("下次刷新", "Next refresh");
        Register("黑雾支线", "Dark Fog Branch");
        Register("黑雾阶段", "Branch Stage", "支线阶段");
        Register("黑雾战况", "Combat Status", "战况概览");
        Register("黑雾成长报价", "Growth Offers", "成长报价");
        Register("黑雾市场特单", "Special Orders", "市场特单");
        Register("黑雾增强层", "Enhanced Layer", "增强层");
        Register("黑雾下一阶段", "Next Milestone", "下一阶段");
        Register("黑雾地面基地", "Ground Bases", "地面基地");
        Register("黑雾星域蜂巢", "Stellar Hives", "星域蜂巢");
        Register("黑雾物资层级", "Resource Tier", "物资层级");
        Register("黑雾增强层-未接入", "Offline", "未接入");
        Register("黑雾增强层-已接入", "Online", "已接入");
        Register("黑雾增强层-事件活跃", "Event Active", "事件活跃");
        Register("黑雾阶段-休眠观察", "Dormant", "休眠观察");
        Register("黑雾阶段-信号接触", "Signal", "信号接触");
        Register("黑雾阶段-地面压制", "Ground Suppression", "地面压制");
        Register("黑雾阶段-星域围猎", "Stellar Hunt", "星域围猎");
        Register("黑雾阶段-奇点收束", "Singularity", "奇点收束");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        tab = wnd.AddTab(trans, "资源统筹");
        float y = 18f;
        txtTitle = wnd.AddText2(0f, y, tab, "资源统筹", 16);
        y += 30f;
        txtRefresh = wnd.AddText2(0f, y, tab, "", 13);
        y += 36f;

        wnd.AddText2(0f, y, tab, "高需求物资", 15);
        wnd.AddText2(560f, y, tab, "低需求物资", 15);
        y += 34f;

        for (int i = 0; i < DisplayCount; i++) {
            hotIcons[i] = wnd.AddImageButton(0f, y, tab, null).WithSize(40f, 40f);
            hotTexts[i] = wnd.AddText2(50f, y, tab, "", 13);
            hotTexts[i].rectTransform.sizeDelta = new Vector2(460f, 24f);

            coldIcons[i] = wnd.AddImageButton(560f, y, tab, null).WithSize(40f, 40f);
            coldTexts[i] = wnd.AddText2(610f, y, tab, "", 13);
            coldTexts[i].rectTransform.sizeDelta = new Vector2(420f, 24f);
            y += 34f;
        }

        y += 16f;
        txtDarkFogTitle = wnd.AddText2(0f, y, tab, "黑雾支线", 15);
        y += 30f;
        for (int i = 0; i < darkFogLines.Length; i++) {
            darkFogLines[i] = wnd.AddText2(0f, y, tab, "", 13, $"txtDarkFog{i}");
            darkFogLines[i].supportRichText = true;
            darkFogLines[i].rectTransform.sizeDelta = new Vector2(960f, 24f);
            y += 26f;
        }
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) {
            return;
        }

        txtTitle.text = "资源统筹".Translate();
        long leftTicks = MarketValueManager.RefreshIntervalTicks - (GameMain.gameTick - MarketValueManager.LastRefreshTick);
        if (leftTicks < 0) {
            leftTicks = 0;
        }
        txtRefresh.text = $"{ "下次刷新".Translate() }：{FormatTicks(leftTicks)}";

        var hot = MarketValueManager.GetTopMarketItems(DisplayCount, descending: true);
        var cold = MarketValueManager.GetTopMarketItems(DisplayCount, descending: false);
        RefreshColumn(hotIcons, hotTexts, hot);
        RefreshColumn(coldIcons, coldTexts, cold);
        RefreshDarkFogSection();
    }

    private static void RefreshDarkFogSection() {
        txtDarkFogTitle.text = "黑雾支线".Translate();
        string stageName = GetStageText(DarkFogCombatManager.GetCurrentStage());
        var snapshots = RecipeGrowthQueries.GetDarkFogProgressSnapshots();
        int totalRecipes = snapshots.Count;
        int unlockedRecipes = snapshots.Count(snapshot => snapshot.IsUnlocked);
        int maxedRecipes = snapshots.Count(snapshot => snapshot.IsMaxed);

        darkFogLines[0].text = $"{ "黑雾阶段".Translate() }：{stageName}";
        darkFogLines[1].text =
            $"{ "黑雾战况".Translate() }：{ "黑雾地面基地".Translate() } {DarkFogCombatManager.GetAliveGroundBaseCount()}    { "黑雾星域蜂巢".Translate() } {DarkFogCombatManager.GetAliveHiveCount()}    { "黑雾物资层级".Translate() } {DarkFogCombatManager.GetDarkFogResourceTier()}/4";
        darkFogLines[2].text = $"{ "黑雾成长报价".Translate() }：{DarkFogCombatManager.GetUnlockedGrowthOfferCount()} 项    { "黑雾市场特单".Translate() }：{DarkFogCombatManager.GetUnlockedSpecialOrderCount()} 条    配方 {unlockedRecipes}/{totalRecipes} 已解锁 / 满级 {maxedRecipes}";
        darkFogLines[3].text = $"{ "黑雾增强层".Translate() }：{BuildEnhancedLayerText()}";
        darkFogLines[4].text = $"{ "黑雾下一阶段".Translate() }：{BuildNextMilestoneText()}";
    }

    private static string GetStageText(EDarkFogCombatStage stage) {
        return stage switch {
            EDarkFogCombatStage.Dormant => "黑雾阶段-休眠观察".Translate().WithColor(Orange),
            EDarkFogCombatStage.Signal => "黑雾阶段-信号接触".Translate().WithColor(Blue),
            EDarkFogCombatStage.GroundSuppression => "黑雾阶段-地面压制".Translate().WithColor(Green),
            EDarkFogCombatStage.StellarHunt => "黑雾阶段-星域围猎".Translate().WithColor(Blue),
            _ => "黑雾阶段-奇点收束".Translate().WithColor(Gold),
        };
    }

    private static string BuildEnhancedLayerText() {
        if (!DarkFogCombatManager.IsEnhancedLayerEnabled()) {
            return "黑雾增强层-未接入".Translate().WithColor(Orange);
        }

        string eventText = DarkFogCombatManager.HasActiveEventChain()
            ? $"    { "黑雾增强层-事件活跃".Translate() }".WithColor(Green)
            : string.Empty;
        return $"{ "黑雾增强层-已接入".Translate().WithColor(Green) }    节点 {DarkFogCombatManager.GetEnhancedNodeCount()}/4    遗物 {DarkFogCombatManager.GetRelicCount()}    Rank {DarkFogCombatManager.GetMeritRank()}    技能 {DarkFogCombatManager.GetAssignedSkillPointCount()}{eventText}";
    }

    private static string BuildNextMilestoneText() {
        EDarkFogCombatStage stage = DarkFogCombatManager.GetCurrentStage();
        return stage switch {
            EDarkFogCombatStage.Dormant when !DarkFogCombatManager.IsCombatModeEnabled() => "启用战斗模式".WithColor(Orange),
            EDarkFogCombatStage.Dormant when DarkFogCombatManager.GetProgressStageIndex() < 3 => $"解锁 {LDB.items.Select(I信息矩阵).name}".WithColor(Orange),
            EDarkFogCombatStage.Dormant => "建立黑雾矩阵库存或首次接触黑雾掉落".WithColor(Blue),
            EDarkFogCombatStage.Signal => $"{LDB.items.Select(I引力矩阵).name} + { "黑雾物资层级".Translate() } 2/4".WithColor(Blue),
            EDarkFogCombatStage.GroundSuppression => $"{LDB.items.Select(I宇宙矩阵).name} + { "黑雾物资层级".Translate() } 3/4 或接触蜂巢".WithColor(Blue),
            EDarkFogCombatStage.StellarHunt when DarkFogCombatManager.IsEnhancedLayerEnabled() => $"{ "黑雾物资层级".Translate() } 4/4 或增强节点 2/4".WithColor(Gold),
            EDarkFogCombatStage.StellarHunt => $"{ "黑雾物资层级".Translate() } 4/4".WithColor(Gold),
            _ => "已到最终阶段".WithColor(Gold),
        };
    }

    private static void RefreshColumn(MyImageButton[] icons, Text[] texts, System.Collections.Generic.IReadOnlyList<int> items) {
        for (int i = 0; i < icons.Length; i++) {
            if (i >= items.Count) {
                icons[i].gameObject.SetActive(false);
                texts[i].text = "";
                continue;
            }
            int itemId = items[i];
            icons[i].gameObject.SetActive(true);
            icons[i].Proto = LDB.items.Select(itemId);
            icons[i].SetCount(GetItemTotalCount(itemId));
            float multiplier = MarketValueManager.GetMultiplier(itemId);
            float rate = MarketValueManager.LastCurrentRate[itemId];
            texts[i].text = $"{LDB.items.Select(itemId).name}  ×{multiplier:F2}  速率 {rate:F1}/m";
        }
    }

    private static string FormatTicks(long ticks) {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(ticks / 60f));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }
}
