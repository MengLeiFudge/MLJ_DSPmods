using BepInEx.Configuration;
using FE.Logic.Manager;
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
    private static readonly MyImageButton[] hotIcons = new MyImageButton[DisplayCount];
    private static readonly Text[] hotTexts = new Text[DisplayCount];
    private static readonly MyImageButton[] coldIcons = new MyImageButton[DisplayCount];
    private static readonly Text[] coldTexts = new Text[DisplayCount];

    public static void AddTranslations() {
        Register("资源统筹", "Resource Overview");
        Register("高需求物资", "Hot Demand");
        Register("低需求物资", "Cold Demand");
        Register("下次刷新", "Next refresh");
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
