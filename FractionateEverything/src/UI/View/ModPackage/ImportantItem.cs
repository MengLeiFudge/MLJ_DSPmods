using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.UI.View.ModPackage;

public static class ImportantItem {
    private static RectTransform window;
    private static RectTransform tab;

    private static readonly int[][] itemIdOriArr = [
        [IFE电磁奖券, IFE能量奖券, IFE结构奖券, IFE信息奖券, IFE引力奖券, IFE宇宙奖券, IFE黑雾奖券],
        [IFE分馏塔原胚普通, IFE分馏塔原胚精良, IFE分馏塔原胚稀有, IFE分馏塔原胚史诗, IFE分馏塔原胚传说, IFE分馏塔原胚定向],
        [IFE分馏配方通用核心, IFE分馏塔增幅芯片],
        [IFE交互塔, IFE矿物复制塔, IFE点数聚集塔, IFE量子复制塔, IFE点金塔, IFE分解塔, IFE转化塔],
        //[IFE行星交互塔, IFE行星矿物复制塔, IFE行星点数聚集塔, IFE行星量子复制塔, IFE行星点金塔, IFE行星分解塔, IFE行星转化塔],
        [IFE复制精华, IFE点金精华, IFE分解精华, IFE转化精华],
        [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵],
        [I能量碎片, I黑雾矩阵, I物质重组器, I硅基神经元, I负熵奇点, I核心素],
    ];
    private static readonly int[] itemIdArr = itemIdOriArr.SelectMany(arr => arr).ToArray();
    private static readonly Text[] itemCountTextArr = new Text[itemIdArr.Length];

    public static void AddTranslations() {
        Register("重要物品", "Important Item");

        Register("以下物品在分馏数据中心的存储量为：",
            "The storage capacity of the following items in the Fractionation data centre are: ");
        Register("重要物品提示按钮说明1",
            "Left-click to extract a set of items, right-click to extract all items.",
            "左键提取一组物品，右键提取全部物品");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "重要物品");
        float x = 0f;
        float y = 18f;
        wnd.AddButton(0, 1, y, tab, "查看分馏数据中心存储的所有物品", 16, "button-get-mod-data-info",
            GetModDataItemInfo);
        y += 36f;
        Text txt = wnd.AddText2(x, y, tab, "以下物品在分馏数据中心的存储量为：");
        wnd.AddTipsButton2(x + txt.preferredWidth + 5, y, tab, "提示", "重要物品提示按钮说明1");
        y += 36f + 7f;
        int index = 0;
        for (int i = 0; i < itemIdOriArr.Length; i++) {
            int xIndex = 0;
            for (int j = 0; j < itemIdOriArr[i].Length; j++) {
                if (xIndex > 3) {
                    y += 36f + 7f;
                    xIndex = 0;
                }
                (float, float) position = GetPosition(xIndex, 4);
                xIndex++;
                int itemId = itemIdOriArr[i][j];
                wnd.AddImageButtonWithDefAction(position.Item1, y, tab, itemId);
                itemCountTextArr[index] = wnd.AddText2(position.Item1 + 45, y, tab, "动态刷新");
                index++;
            }
            y += 36f + 7f;
        }
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        for (int i = 0; i < itemCountTextArr.Length; i++) {
            itemCountTextArr[i].text = $"x {GetModDataItemCount(itemIdArr[i])}";
        }
    }

    private static void GetModDataItemInfo() {
        Dictionary<ItemProto, long> itemCountDic = [];
        foreach (ItemProto item in LDB.items.dataArray) {
            long count = GetModDataItemCount(item.ID);
            if (count <= 0) {
                continue;
            }
            itemCountDic[item] = count;
        }
        if (itemCountDic.Count == 0) {
            UIMessageBox.Show("提示".Translate(),
                $"{"分馏数据中心没有".Translate()} {"任何物品".Translate()}{"！".Translate()}",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
            return;
        }
        StringBuilder sb = new("分馏数据中心存储的物品有：".Translate() + "\n");
        int oneLineMaxCount = (int)Math.Ceiling(itemCountDic.Count / 40.0);
        if (oneLineMaxCount < 5) {
            oneLineMaxCount = 5;
        } else if (oneLineMaxCount > 10) {
            oneLineMaxCount = 10;
        }
        int oneLineCount = 0;
        foreach (var p in itemCountDic.OrderByDescending(kvp => itemValue[kvp.Key.ID])) {
            sb.Append($"{p.Key.name} x {p.Value}".WithValueColor(p.Key.ID));
            oneLineCount++;
            if (oneLineCount >= oneLineMaxCount) {
                sb.Append("\n");
                oneLineCount = 0;
            } else {
                sb.Append("          ");
            }
        }
        UIMessageBox.Show("提示".Translate(),
            sb.ToString(),
            "确定".Translate(), UIMessageBox.INFO,
            null);
    }

    private static void GetModDataItem(int itemId, int groupCount) {
        ItemProto item = LDB.items.Select(itemId);
        int count = groupCount == -1 ? int.MaxValue : item.StackSize * groupCount;
        count = TakeItemFromModData(item.ID, count, out int inc);
        if (count == 0) {
            UIMessageBox.Show("提示".Translate(),
                $"{"分馏数据中心没有".Translate()} {item.name} {"！".Translate()}",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
        } else {
            AddItemToPackage(item.ID, count, inc, false);
        }
    }

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
