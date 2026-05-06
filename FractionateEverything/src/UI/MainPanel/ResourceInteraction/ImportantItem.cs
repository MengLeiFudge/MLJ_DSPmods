using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.UI.Controls;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Layout.GridDsl;
using static FE.Logic.DataCenter.DataCenterInventory;
using static FE.Utils.Utils;
using FE.UI.Foundation.Window;
using FE.UI.MainPanel.Theme;
using FE.UI.Layout;

namespace FE.UI.MainPanel.ResourceInteraction;
/// <summary>
/// 关键物品获取与说明的归档页面。
/// </summary>
public static class ImportantItem {
    private static RectTransform window;
    private static RectTransform tab;

    private static readonly int[][] itemIdOriArr = [
        [IFE交互塔原胚, IFE矿物复制塔原胚, IFE点数聚集塔原胚, IFE转化塔原胚, IFE精馏塔原胚, IFE分馏塔定向原胚],
        [IFE残片],
        [IFE交互塔, IFE行星内物流交互站, IFE星际物流交互站],
        [IFE矿物复制塔, IFE点数聚集塔, IFE转化塔, IFE精馏塔],
        [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵],
        [I能量碎片, I黑雾矩阵, I物质重组器, I硅基神经元, I负熵奇点, I核心素],
    ];
    private static readonly int[] itemIdArr = itemIdOriArr.SelectMany(arr => arr).ToArray();
    private static readonly MyImageButton[] itemButtons = new MyImageButton[itemIdArr.Length];

    public static void AddTranslations() {
        Register("重要物品", "Important Item");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        window = trans;
        tab = trans;
        CreateUIInternal(wnd, trans);
    }

    private static void CreateUIInternal(MyWindow wnd, RectTransform parent) {
        BuildLayout(wnd, parent,
            Grid(
                rows: [Px(32f), 1],
                rowGap: PageLayout.InnerGap,
                children: [
                    Grid(
                        pos: (0, 0),
                        cols: [1, Px(28f), 2],
                        columnGap: 8f,
                        children: [
                            TextNode("以下物品在分馏数据中心的存储量为：",
                                pos: (0, 0), objectName: "important-item-summary"),
                            TipsButtonNode("提取物品", "提取物品说明",
                                pos: (0, 1), objectName: "important-item-tip"),
                        ]),
                    Grid(
                        pos: (1, 0),
                        rows: BuildItemRows(),
                        cols: [1, 1, 1, 1],
                        rowGap: PageLayout.InnerGap,
                        columnGap: PageLayout.InnerGap,
                        children: BuildItemNodes()),
                ]));
    }

    private static IReadOnlyList<LayoutTrack> BuildItemRows() {
        var rows = new List<LayoutTrack>();
        int rowCount = (itemIdArr.Length + 3) / 4;
        for (int i = 0; i < rowCount; i++) {
            rows.Add(1);
        }

        return rows;
    }

    private static IReadOnlyList<LayoutNode> BuildItemNodes() {
        var nodes = new List<LayoutNode>();
        for (int i = 0; i < itemIdArr.Length; i++) {
            int index = i;
            int row = i / 4;
            int col = i % 4;
            nodes.Add(ImageButtonNode(LDB.items.Select(itemIdArr[index]), 40f,
                onBuilt: btn => itemButtons[index] = btn.WithTakeItemClickEvent(),
                pos: (row, col), objectName: $"important-item-{index}"));
        }

        return nodes;
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        for (int i = 0; i < itemButtons.Length; i++) {
            itemButtons[i].SetCount(GetModDataItemCount(itemIdArr[i]));
        }
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
