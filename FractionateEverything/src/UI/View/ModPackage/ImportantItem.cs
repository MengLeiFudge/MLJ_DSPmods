using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.UI.View.ModPackage;

public static class ImportantItem {
    private static RectTransform window;
    private static RectTransform tab;

    private static UIButton btnGetModDataProto;
    private static UIButton btnGetModDataBuilding;

    public static void AddTranslations() {
        Register("重要物品", "Important Item");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "重要物品");
        float x = 0f;
        float y = 20f;
        btnGetModDataProto = wnd.AddButton(0, 2, y, tab, "提取所有分馏塔原胚", 16, "button-get-proto",
            GetModDataFracBuildingProto);
        btnGetModDataBuilding = wnd.AddButton(1, 2, y, tab, "提取所有分馏塔", 16, "button-get-building",
            GetModDataFractionator);
        y += 36f;
        btnGetModDataProto = wnd.AddButton(0, 1, y, tab, "查看分馏数据中心当前持有的所有物品", 16, "button-get-mod-data-info",
            GetModDataItemInfo);
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
    }

    private static void GetModDataFracBuildingProto() {
        StringBuilder sb = new StringBuilder();
        int[] itemIDs = [IFE分馏塔原胚普通, IFE分馏塔原胚精良, IFE分馏塔原胚稀有, IFE分馏塔原胚史诗, IFE分馏塔原胚传说, IFE分馏塔原胚定向];
        int[] counts = new int[itemIDs.Length];
        for (int i = 0; i < itemIDs.Length; i++) {
            counts[i] = (int)Math.Min(int.MaxValue, GetModDataItemCount(itemIDs[i]));
            if (counts[i] > 0) {
                ItemProto item = LDB.items.Select(itemIDs[i]);
                sb.Append($"\n{item.name} x {counts[i]}");
            }
        }
        if (sb.Length == 0) {
            UIMessageBox.Show("提示".Translate(), "没有可提取的分馏塔原胚！", "确定".Translate(), UIMessageBox.WARNING);
            return;
        }
        UIMessageBox.Show("提示".Translate(), $"要提取以下物品{"吗？".Translate()}{sb}", "确定".Translate(), "取消".Translate(), UIMessageBox.WARNING, () => {
            sb = new("已提取以下物品：");
            foreach (int itemID in itemIDs) {
                int takeCount = TakeItemFromModData(itemID, int.MaxValue, out int inc);
                if (takeCount > 0) {
                    AddItemToPackage(itemID, takeCount, inc);
                    sb.Append($"\n{LDB.items.Select(itemID).name} x {takeCount}");
                }
            }
            UIMessageBox.Show("提示".Translate(), sb.ToString(), "确定".Translate(), UIMessageBox.INFO);
        }, null);
    }

    private static void GetModDataFractionator() {
        StringBuilder sb = new StringBuilder();
        int[] itemIDs = [IFE交互塔, IFE矿物复制塔, IFE点数聚集塔, IFE量子复制塔, IFE点金塔, IFE分解塔, IFE转化塔];
        int[] counts = new int[itemIDs.Length];
        for (int i = 0; i < itemIDs.Length; i++) {
            counts[i] = (int)Math.Min(int.MaxValue, GetModDataItemCount(itemIDs[i]));
            if (counts[i] > 0) {
                ItemProto item = LDB.items.Select(itemIDs[i]);
                sb.Append($"\n{item.name} x {counts[i]}");
            }
        }
        if (sb.Length == 0) {
            UIMessageBox.Show("提示".Translate(), "没有可提取的分馏塔！", "确定".Translate(), UIMessageBox.WARNING);
            return;
        }
        UIMessageBox.Show("提示".Translate(), $"要提取以下物品{"吗？".Translate()}{sb}", "确定".Translate(), "取消".Translate(), UIMessageBox.WARNING, () => {
            sb = new("已提取以下物品：");
            foreach (int itemID in itemIDs) {
                int takeCount = TakeItemFromModData(itemID, int.MaxValue, out int inc);
                if (takeCount > 0) {
                    AddItemToPackage(itemID, takeCount, inc);
                    sb.Append($"\n{LDB.items.Select(itemID).name} x {takeCount}");
                }
            }
            UIMessageBox.Show("提示".Translate(), sb.ToString(), "确定".Translate(), UIMessageBox.INFO);
        }, null);
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
            UIMessageBox.Show("提示".Translate(), "分馏数据中心当前没有物品！", "确定".Translate(), UIMessageBox.WARNING);
            return;
        }
        StringBuilder sb = new("分馏数据中心当前持有如下物品：\n");
        int oneLineMaxCount = Math.Min(10, Math.Max(5, (int)Math.Ceiling(itemCountDic.Count / 40.0)));
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
        UIMessageBox.Show("提示".Translate(), sb.ToString(), "确定".Translate(), UIMessageBox.INFO);
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
