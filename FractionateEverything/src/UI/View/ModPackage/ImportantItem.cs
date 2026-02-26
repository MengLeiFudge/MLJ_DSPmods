using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.View.ModPackage;

public static class ImportantItem {
    private static RectTransform window;
    private static RectTransform tab;

    private static readonly int[][] itemIdOriArr = [
        [IFE电磁奖券, IFE能量奖券, IFE结构奖券, IFE信息奖券, IFE引力奖券, IFE宇宙奖券, IFE黑雾奖券],
        [IFE分馏塔原胚I型, IFE分馏塔原胚II型, IFE分馏塔原胚III型, IFE分馏塔原胚IV型, IFE分馏塔原胚V型, IFE分馏塔定向原胚],
        [IFE分馏配方核心, IFE分馏塔增幅芯片],
        [IFE交互塔, IFE行星内物流交互站, IFE星际物流交互站],
        [IFE矿物复制塔, IFE点数聚集塔, IFE量子复制塔, IFE点金塔, IFE分解塔, IFE转化塔],
        //[IFE行星交互塔, IFE行星矿物复制塔, IFE行星点数聚集塔, IFE行星量子复制塔, IFE行星点金塔, IFE行星分解塔, IFE行星转化塔],
        [IFE复制精华, IFE点金精华, IFE分解精华, IFE转化精华],
        [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵],
        [I能量碎片, I黑雾矩阵, I物质重组器, I硅基神经元, I负熵奇点, I核心素],
    ];
    private static readonly int[] itemIdArr = itemIdOriArr.SelectMany(arr => arr).ToArray();
    private static readonly Text[] itemCountTextArr = new Text[itemIdArr.Length];

    public static void AddTranslations() {
        Register("重要物品", "Important Item");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "重要物品");
        float x = 0f;
        float y = 18f;
        Text txt = wnd.AddText2(x, y, tab, "以下物品在分馏数据中心的存储量为：");
        wnd.AddTipsButton2(x + 5 + txt.preferredWidth, y, tab, "提取物品", "提取物品说明");
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
                wnd.AddImageButton(position.Item1, y, tab, LDB.items.Select(itemId)).WithTakeItemClickEvent();
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
