using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.View.GetItemRecipe;

public static class TicketExchange {
    private static RectTransform window;
    private static RectTransform tab;
    
    // 兑换比例表：(矩阵ID, 普通券比例, 精选券比例)
    // 0表示不可兑换
    private static readonly (int matrixId, int normalRate, int premiumRate)[] ExchangeRates = [
        (I电磁矩阵, 20, 0),
        (I能量矩阵, 10, 0),
        (I结构矩阵, 5, 50),
        (I信息矩阵, 2, 20),
        (I引力矩阵, 1, 8),
        (I宇宙矩阵, 0, 2),
    ];
    
    // 残片兑换比例：(残片数量, 奖券ID)
    private static readonly (int fragmentCost, int ticketId)[] FragmentRates = [
        (10, IFE普通抽卡券),   // 10残片=普通券×1（电磁层）
        (50, IFE普通抽卡券),   // 50残片=普通券×1（能量层）
    ];
    
    public static void AddTranslations() {
        Register("奖券兑换", "Ticket Exchange");
        Register("矩阵兑换", "Matrix Exchange");
        Register("残片兑换", "Fragment Exchange");
        Register("兑换", "Exchange");
    }
    
    public static void LoadConfig(ConfigFile configFile) { }
    
    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = wnd.rectTrans;
        tab = trans;
        
        // 矩阵兑换区
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
        
        // 残片兑换区
        y += 20;
        wnd.AddText2(20, y, tab, "残片兑换".Translate());
        y += 40;
        
        wnd.AddImageButton(20, y, tab, LDB.items.Select(IFE残片));
        wnd.AddText2(60, y, tab, () => GameMain.mainPlayer?.package.GetItemCount(IFE残片).ToString() ?? "0");
        
        int x = 120;
        foreach (var rate in FragmentRates) {
            wnd.AddButton(x, y, tab, $"{rate.fragmentCost} -> 1", 100, 30, () => ExchangeFragment(rate.fragmentCost, rate.ticketId));
            x += 110;
        }
    }
    
    private static void CreateExchangeRow(MyConfigWindow wnd, RectTransform tab, int y, int matrixId, int rate, int ticketId) {
        wnd.AddImageButton(20, y, tab, LDB.items.Select(matrixId));
        wnd.AddText2(60, y, tab, LDB.items.Select(matrixId).name);
        wnd.AddText2(160, y, tab, "→");
        wnd.AddImageButton(190, y, tab, LDB.items.Select(ticketId));
        wnd.AddText2(230, y, tab, $"{rate} : 1");
        
        wnd.AddButton(300, y, tab, "兑换".Translate() + " 1", 80, 30, () => ExchangeMatrix(matrixId, ticketId, rate, 1));
        wnd.AddButton(390, y, tab, "兑换".Translate() + " 10", 80, 30, () => ExchangeMatrix(matrixId, ticketId, rate * 10, 10));
    }
    
    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) return;
    }
    
    private static void ExchangeMatrix(int matrixId, int ticketId, int matrixCount, int ticketCount) {
        if (!TakeItemWithTip(matrixId, matrixCount, out _)) return;
        AddItemToModData(ticketId, ticketCount, 0, true);
    }
    
    private static void ExchangeFragment(int fragmentCost, int ticketId) {
        if (!TakeItemWithTip(IFE残片, fragmentCost, out _)) return;
        AddItemToModData(ticketId, 1, 0, true);
    }
    
    // IModCanSave
    public static void Import(BinaryReader r) { r.ReadBlocks(); }
    public static void Export(BinaryWriter w) { w.WriteBlocks(); }
    public static void IntoOtherSave() { }
}
