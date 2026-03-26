using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.View.GetItemRecipe;

public static class TicketExchange {
    private static RectTransform tab;
    private static Text fragmentCountText;
    
    private static readonly (int matrixId, int normalRate, int premiumRate)[] ExchangeRates = [
        (I电磁矩阵, 20, 0),
        (I能量矩阵, 10, 0),
        (I结构矩阵, 5, 50),
        (I信息矩阵, 2, 20),
        (I引力矩阵, 1, 8),
        (I宇宙矩阵, 0, 2),
    ];
    
    private static readonly (int fragmentCost, int ticketId)[] FragmentRates = [
        (10, IFE普通抽卡券),
        (50, IFE精选抽卡券),
    ];
    
    public static void AddTranslations() {
        Register("奖券兑换", "Ticket Exchange");
        Register("矩阵兑换", "Matrix Exchange");
        Register("残片兑换", "Fragment Exchange");
        Register("兑换", "Exchange");
    }
    
    public static void LoadConfig(ConfigFile configFile) { }
    
    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        tab = wnd.AddTab(trans, "奖券兑换");
        
        wnd.AddText2(20, 20, tab, "矩阵兑换".Translate());
        
        int y = 60;
        foreach (var rate in ExchangeRates) {
            if (rate.normalRate > 0) {
                CreateExchangeRow(wnd, tab, y, rate.matrixId, rate.normalRate, IFE普通抽卡券);
                y += 40;
            }
            if (rate.premiumRate > 0) {
                CreateExchangeRow(wnd, tab, y, rate.matrixId, rate.premiumRate, IFE精选抽卡券);
                y += 40;
            }
        }
        
        y += 20;
        wnd.AddText2(20, y, tab, "残片兑换".Translate());
        y += 40;
        
        wnd.AddImageButton(20, y, tab, LDB.items.Select(IFE残片));
        fragmentCountText = wnd.AddText2(60, y, tab, "0");
        
        int x = 120;
        foreach (var rate in FragmentRates) {
            string buttonText = $"{rate.fragmentCost} -> 1";
            wnd.AddButton(x, y, 180f, tab, buttonText, 14, "btn", () => ExchangeFragment(rate.fragmentCost, rate.ticketId));
            x += 190;
        }
    }
    
    private static void CreateExchangeRow(MyConfigWindow wnd, RectTransform tab, int y, int matrixId, int rate, int ticketId) {
        wnd.AddImageButton(20, y, tab, LDB.items.Select(matrixId));
        wnd.AddText2(80, y, tab, "→");
        wnd.AddImageButton(110, y, tab, LDB.items.Select(ticketId));
        wnd.AddText2(150, y, tab, $"{rate} : 1");
        
        wnd.AddButton(300, y, 80f, tab, "兑换".Translate() + " 1", 16, "btn", () => ExchangeMatrix(matrixId, ticketId, rate, 1));
        wnd.AddButton(390, y, 80f, tab, "兑换".Translate() + " 10", 16, "btn", () => ExchangeMatrix(matrixId, ticketId, rate * 10, 10));
    }
    
    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) return;
        if (fragmentCountText != null) {
            fragmentCountText.text = GameMain.mainPlayer?.package.GetItemCount(IFE残片).ToString() ?? "0";
        }
    }
    
    private static void ExchangeMatrix(int matrixId, int ticketId, int matrixCount, int ticketCount) {
        if (!TakeItemWithTip(matrixId, matrixCount, out _)) return;
        AddItemToModData(ticketId, ticketCount, 0, true);
    }
    
    private static void ExchangeFragment(int fragmentCost, int ticketId) {
        if (!TakeItemWithTip(IFE残片, fragmentCost, out _)) return;
        AddItemToModData(ticketId, 1, 0, true);
    }
    
    public static void Import(BinaryReader r) { r.ReadBlocks(); }
    public static void Export(BinaryWriter w) { w.WriteBlocks(); }
    public static void IntoOtherSave() { }
}
