using System;
using System.IO;
using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.View.GetItemRecipe;

public static class LimitedTimeStore {
    private static RectTransform window;
    private static RectTransform tab;

    private static DateTime nextFreshTime;
    private static Text textLeftTime;
    private static Text[] textItemInfo = new Text[3];

    public static void AddTranslations() {
        Register("限时商店", "Limited Time Store");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "限时商店");
        nextFreshTime = DateTime.Now.Date.AddHours(DateTime.Now.Hour)
            .AddMinutes(DateTime.Now.Minute / 10 * 10 + 10);
        float x = 0f;
        float y = 20f;
        textLeftTime = wnd.AddText2(x, y, tab, "剩余刷新时间：xx s", 15, "textLeftTime");
        y += 36f;
        textItemInfo[0] = wnd.AddText2(x, y, tab, "物品0信息", 15, "textLeftTime0");
        wnd.AddButton(2, 3, y, tab, "兑换", 16, "btn-buy-time1",
            () => ExchangeItem(0));
        y += 36f;
        textItemInfo[1] = wnd.AddText2(x, y, tab, "物品1信息", 15, "textLeftTime1");
        wnd.AddButton(2, 3, y, tab, "兑换", 16, "btn-buy-time2",
            () => ExchangeItem(1));
        y += 36f;
        textItemInfo[2] = wnd.AddText2(x, y, tab, "物品2信息", 15, "textLeftTime2");
        wnd.AddButton(2, 3, y, tab, "兑换", 16, "btn-buy-time3",
            () => ExchangeItem(2));
        y += 36f;
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        if (DateTime.Now >= nextFreshTime) {
            nextFreshTime = nextFreshTime.AddMinutes(10);
            //更新三份限时购买物品的信息
            // textItemInfo[0].text = GetTimeLimitedItemInfo(0);
            // textItemInfo[1].text = GetTimeLimitedItemInfo(1);
            // textItemInfo[2].text = GetTimeLimitedItemInfo(2);
        }
        TimeSpan ts = nextFreshTime - DateTime.Now;
        textLeftTime.text = $"还有 {(int)ts.TotalMinutes} min {ts.Seconds} s 刷新";
    }

    public static void ExchangeItem(int index) { }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
    }

    public static void IntoOtherSave() { }

    #endregion
}
