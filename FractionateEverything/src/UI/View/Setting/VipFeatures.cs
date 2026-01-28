using System;
using System.IO;
using BepInEx.Configuration;
using FE.UI.Components;
using FE.UI.View.GetItemRecipe;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.UI.View.Setting;

public static class VipFeatures {
    private static RectTransform window;
    private static RectTransform tab;

    #region VIP相关数据

    /// <summary>
    /// 当前VIP等级
    /// </summary>
    private static int Level = 0;
    /// <summary>
    /// 当前VIP经验值
    /// </summary>
    private static float Exp = 0;
    /// <summary>
    /// 经验达到多少可以升级VIP
    /// </summary>
    private static float ExpLevelUp => Level == 0 ? 1 : 1000 * (float)Math.Pow(2.0f, Level);

    /// <summary>
    /// 获取VIP经验，并检测能否升级。注意，VIP升级不清空现有经验。
    /// </summary>
    public static void AddExp(float exp) {
        Exp += exp / itemValue[IFE宇宙奖券] * 50;
        while (Exp >= ExpLevelUp) {
            Level++;
            for (int i = 0; i < 4; i++) {
                TicketRaffle.FreshPool(i);
            }
        }
    }

    /// <summary>
    /// 抽奖时，奖券价值视为 原有价值*TicketValueMulti
    /// </summary>
    public static float TicketValueMulti => (float)Math.Pow(1.07f, Level);
    /// <summary>
    /// 限时商店免费兑换项数
    /// </summary>
    public static int FreeExchangeCount => (Level + 1) / 2;
    /// <summary>
    /// 限时商店购买折扣
    /// </summary>
    public static float ExchangeDiscount => 1.0f / TicketValueMulti;

    #endregion

    private static Text txtVipInfo;
    private static Text[] txtVipBonus = new Text[3];

    public static void AddTranslations() {
        Register("VIP特权", "VIP Features");

        Register("VIP等级：", "VIP level: ");
        Register("VIP加成如下：", "VIP bonus are as follows:");
        Register("奖券抽奖加成",
            "When using lottery tickets for prize draws, the rewards obtained x{0:P2}",
            "使用奖券抽奖时，获得的奖励 x{0:P2}");
        Register("限时商店免费兑换项数",
            "Free exchange for the first {0} recipes/items in the limited-time shop",
            "免费兑换限时商店的前{0}项配方/物品");
        Register("限时商店购买折扣",
            "Exchange recipes/items from the limited-time shop at the price of {0:P2}",
            "以{0:P2}的价格兑换限时商店的配方/物品");
        //todo: 商店刷新间隔减少，刷新需要矩阵减少
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "VIP特权");
        float x = 0f;
        float y = 18f;
        txtVipInfo = wnd.AddText2(x, y, tab, "动态刷新");
        txtVipInfo.supportRichText = true;
        y += 36f;
        wnd.AddText2(x, y, tab, "VIP加成如下：");
        for (int i = 0; i < 3; i++) {
            y += 36f;
            txtVipBonus[i] = wnd.AddText2(x, y, tab, "动态刷新");
            txtVipBonus[i].supportRichText = true;
        }
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        txtVipInfo.text = $"{"VIP等级：".Translate()}{Level} ({Exp:F0} / {Math.Ceiling(ExpLevelUp):F0})"
            .WithColor((Level - 1) / 3 + 1);
        txtVipBonus[0].text = string.Format("奖券抽奖加成".Translate(), TicketValueMulti)
            .WithColor((Level - 1) / 3 + 1);
        txtVipBonus[1].text = string.Format("限时商店免费兑换项数".Translate(), FreeExchangeCount)
            .WithColor((Level - 1) / 3 + 1);
        txtVipBonus[2].text = string.Format("限时商店购买折扣".Translate(), ExchangeDiscount)
            .WithColor((Level - 1) / 3 + 1);
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
        Exp = r.ReadSingle();
        AddExp(0);
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
        w.Write(Exp);
    }

    public static void IntoOtherSave() {
        Exp = 0;
        Level = 0;
    }

    #endregion
}
