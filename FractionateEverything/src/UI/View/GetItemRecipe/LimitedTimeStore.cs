using System.IO;
using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.UI.View.GetItemRecipe;

/// <summary>
/// 矩阵与残片兑换抽卡券窗口。
/// 固定汇率，无随机刷新，无定时器。
/// </summary>
public static class LimitedTimeStore {
    private static RectTransform tab;

    // 矩阵→普通抽卡券：电磁20:1, 能量10:1, 结构5:1, 信息2:1, 引力1:1
    private static readonly (int matrixId, int matrixCost)[] NormalTicketRates = [
        (I电磁矩阵, 20),
        (I能量矩阵, 10),
        (I结构矩阵, 5),
        (I信息矩阵, 2),
        (I引力矩阵, 1),
    ];

    // 矩阵→精选抽卡券：结构50:1, 信息20:1, 引力8:1, 宇宙2:1
    private static readonly (int matrixId, int matrixCost)[] FeaturedTicketRates = [
        (I结构矩阵, 50),
        (I信息矩阵, 20),
        (I引力矩阵, 8),
        (I宇宙矩阵, 2),
    ];

    // 残片→抽卡券：10/50/200→普通, 500/1000/2000→精选
    private static readonly (int shardCost, int ticketId, int ticketCount)[] ShardRates = [
        (10,   IFE普通抽卡券, 1),
        (50,   IFE普通抽卡券, 1),
        (200,  IFE普通抽卡券, 1),
        (500,  IFE精选抽卡券, 1),
        (1000, IFE精选抽卡券, 1),
        (2000, IFE精选抽卡券, 1),
    ];

    // 动态文本
    private static Text _txtShardCount;
    private static Text _txtNormalCount;
    private static Text _txtFeaturedCount;

    private const float RowH = 40f;
    private const float ColW = 220f;

    public static void AddTranslations() {
        Register("奖券兑换", "Ticket Exchange");
        Register("矩阵兑换普通抽卡券", "Matrix → Normal Ticket");
        Register("矩阵兑换精选抽卡券", "Matrix → Featured Ticket");
        Register("残片兑换抽卡券", "Shard → Ticket");
        Register("兑换", "Exchange");
        Register("持有", "Held");
        Register("兑换确认", "Exchange Confirmation");
        Register("消耗", "Consume");
        Register("兑换为", "to exchange for");
        Register("吗？", "?");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        tab = wnd.AddTab(trans, "奖券兑换");

        float y = 10f;

        // 持有量显示
        _txtShardCount = MyWindow.AddText(5f, y, tab, "残片: 0", 13);
        _txtNormalCount = MyWindow.AddText(250f, y, tab, "普通券: 0", 13);
        _txtFeaturedCount = MyWindow.AddText(500f, y, tab, "精选券: 0", 13);
        y += 36f;

        // 区域1：矩阵→普通抽卡券
        MyWindow.AddText(5f, y, tab, "矩阵兑换普通抽卡券".Translate(), 14);
        y += 28f;
        foreach (var (matrixId, cost) in NormalTicketRates) {
            int mid = matrixId;
            int c = cost;
            wnd.AddImageButton(5f, y, tab, LDB.items.Select(mid));
            MyWindow.AddText(48f, y + 8f, tab, $"x{c}  →  ", 13);
            wnd.AddImageButton(120f, y, tab, LDB.items.Select(IFE普通抽卡券));
            MyWindow.AddText(163f, y + 8f, tab, "x1", 13);
            wnd.AddButton(220f, y, 80f, tab, "兑换".Translate(), 13,
                onClick: () => DoMatrixExchange(mid, c, IFE普通抽卡券, 1));
            y += RowH;
        }

        y += 10f;

        // 区域2：矩阵→精选抽卡券
        MyWindow.AddText(5f, y, tab, "矩阵兑换精选抽卡券".Translate(), 14);
        y += 28f;
        foreach (var (matrixId, cost) in FeaturedTicketRates) {
            int mid = matrixId;
            int c = cost;
            wnd.AddImageButton(5f, y, tab, LDB.items.Select(mid));
            MyWindow.AddText(48f, y + 8f, tab, $"x{c}  →  ", 13);
            wnd.AddImageButton(120f, y, tab, LDB.items.Select(IFE精选抽卡券));
            MyWindow.AddText(163f, y + 8f, tab, "x1", 13);
            wnd.AddButton(220f, y, 80f, tab, "兑换".Translate(), 13,
                onClick: () => DoMatrixExchange(mid, c, IFE精选抽卡券, 1));
            y += RowH;
        }

        y += 10f;

        // 区域3：残片→抽卡券
        MyWindow.AddText(5f, y, tab, "残片兑换抽卡券".Translate(), 14);
        y += 28f;
        foreach (var (shardCost, ticketId, ticketCount) in ShardRates) {
            int sc = shardCost;
            int tid = ticketId;
            int tc = ticketCount;
            wnd.AddImageButton(5f, y, tab, LDB.items.Select(IFE残片));
            MyWindow.AddText(48f, y + 8f, tab, $"x{sc}  →  ", 13);
            wnd.AddImageButton(120f, y, tab, LDB.items.Select(tid));
            MyWindow.AddText(163f, y + 8f, tab, $"x{tc}", 13);
            wnd.AddButton(220f, y, 80f, tab, "兑换".Translate(), 13,
                onClick: () => DoShardExchange(sc, tid, tc));
            y += RowH;
        }
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) return;
        int shards = GameMain.mainPlayer?.package.GetItemCount(IFE残片) ?? 0;
        int normal = GameMain.mainPlayer?.package.GetItemCount(IFE普通抽卡券) ?? 0;
        int featured = GameMain.mainPlayer?.package.GetItemCount(IFE精选抽卡券) ?? 0;
        if (_txtShardCount != null) _txtShardCount.text = $"{"持有".Translate()} 残片: {shards}";
        if (_txtNormalCount != null) _txtNormalCount.text = $"{"持有".Translate()} 普通券: {normal}";
        if (_txtFeaturedCount != null) _txtFeaturedCount.text = $"{"持有".Translate()} 精选券: {featured}";
    }

    private static void DoMatrixExchange(int matrixId, int matrixCost, int ticketId, int ticketCount) {
        ItemProto matrix = LDB.items.Select(matrixId);
        ItemProto ticket = LDB.items.Select(ticketId);
        if (matrix == null || ticket == null) return;
        string msg = $"{"消耗".Translate()} {matrix.name} x{matrixCost} {"兑换为".Translate()} {ticket.name} x{ticketCount}{"吗？".Translate()}";
        UIMessageBox.Show("兑换确认".Translate(), msg,
            "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => {
                if (!TakeItemWithTip(matrixId, matrixCost, out _)) return;
                AddItemToModData(ticketId, ticketCount, 0, true);
            }, null);
    }

    private static void DoShardExchange(int shardCost, int ticketId, int ticketCount) {
        ItemProto ticket = LDB.items.Select(ticketId);
        if (ticket == null) return;
        string shardName = LDB.items.Select(IFE残片)?.name ?? "残片";
        string msg = $"{"消耗".Translate()} {shardName} x{shardCost} {"兑换为".Translate()} {ticket.name} x{ticketCount}{"吗？".Translate()}";
        UIMessageBox.Show("兑换确认".Translate(), msg,
            "确定".Translate(), "取消".Translate(), UIMessageBox.QUESTION,
            () => {
                if (!TakeItemWithTip(IFE残片, shardCost, out _)) return;
                AddItemToModData(ticketId, ticketCount, 0, true);
            }, null);
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
