using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.UI.View;

public static class TabPackage {
    public static RectTransform _windowTrans;

    #region 选择物品

    public static ItemProto SelectedItem { get; set; } = LDB.items.Select(I铁矿);
    public static int SelectedItemId => SelectedItem.ID;
    private static Text textCurrItem;
    private static MyImageButton btnSelectedItem;

    private static void OnButtonChangeItemClick() {
        //_windowTrans.anchoredPosition是窗口的中心点
        //Popup的位置是弹出窗口的左上角
        //所以要向右（x+）向上（y+）
        float x = _windowTrans.anchoredPosition.x + _windowTrans.rect.width / 2;
        float y = _windowTrans.anchoredPosition.y + _windowTrans.rect.height / 2;
        UIItemPickerExtension.Popup(new(x, y), item => {
            if (item == null) return;
            SelectedItem = item;
        }, true, item => true);
    }

    #endregion

    private static Text textItemCountInfo;
    private static UIButton[] btnGetModDataItem = new UIButton[3];
    private static UIButton btnGetModDataProto;
    private static UIButton btnGetModDataBuilding;

    public static void AddTranslations() {
        Register("FE1.1-1", "", "2024年3月");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        _windowTrans = trans;
        float x;
        float y;
        wnd.AddTabGroup(trans, "物品管理");
        {
            var tab = wnd.AddTab(trans, "物品交互");
            x = 0f;
            y = 10f;
            textCurrItem = wnd.AddText2(x, y + 5f, tab, "当前物品：", 15, "textCurrItem");
            btnSelectedItem = wnd.AddImageButton(x + textCurrItem.preferredWidth + 5f, y, tab,
                SelectedItem.ID, "button-change-item",
                OnButtonChangeItemClick, OnButtonChangeItemClick,
                "切换说明", "左键选择需要查询或提取的物品");
            //todo: 修复按钮提示窗后移除该内容
            wnd.AddTipsButton2(x + textCurrItem.preferredWidth + 5f + 60, y + 11f, tab,
                "切换说明", "左键选择需要查询或提取的物品");
            y += 50f;
            textItemCountInfo = wnd.AddText2(x, y, tab, "mod：xx 物流：xx 背包：xx", 15, "textItemCountInfo");
            y += 36f;
            btnGetModDataItem[0] = wnd.AddButton(x, y, 200, tab, "提取1组物品", 16, "button-get-item0",
                () => GetModDataItem(1));
            btnGetModDataItem[1] = wnd.AddButton(x + 220, y, 200, tab, "提取10组物品", 16, "button-get-item1",
                () => GetModDataItem(10));
            btnGetModDataItem[2] = wnd.AddButton(x + 440, y, 200, tab, "提取全部物品", 16, "button-get-item2",
                () => GetModDataItem(-1));
            y += 36f;
            btnGetModDataProto = wnd.AddButton(x, y, 300, tab, "提取所有分馏塔原胚", 16, "button-get-proto",
                GetModDataFracBuildingProto);
            btnGetModDataBuilding = wnd.AddButton(x + 320, y, 300, tab, "提取所有分馏塔", 16, "button-get-building",
                GetModDataFractionator);
            y += 36f;
            btnGetModDataProto = wnd.AddButton(x, y, 600, tab, "查看分馏数据中心当前持有的所有物品", 16, "button-get-mod-data-info",
                GetModDataItemInfo);
        }
        {
            var tab = wnd.AddTab(trans, "重要物品");
            x = 0f;
            y = 10f;
        }
    }

    public static void UpdateUI() {
        btnSelectedItem.SetSprite(SelectedItem.iconSprite);
        textItemCountInfo.text = $"当前共有 {GetItemTotalCount(SelectedItem.ID)}，其中"
                                 + $"数据中心 {GetModDataItemCount(SelectedItem.ID)}，"
                                 + $"物流背包 {GetDeliveryPackageItemCount(SelectedItem.ID)}，"
                                 + $"个人背包 {GetPackageItemCount(SelectedItem.ID)}";
        var t = btnGetModDataItem[0].gameObject.transform.Find("button-text").GetComponent<Text>();
        if (t != null) {
            t.text = $"提取1组（{SelectedItem.StackSize}）";
        }
        //enabled -> 启用/禁用    gameObject.SetActive -> 显示/隐藏
        btnGetModDataItem[0].enabled = GetModDataItemCount(SelectedItem.ID) >= SelectedItem.StackSize;
        btnGetModDataItem[0].button.enabled = GetModDataItemCount(SelectedItem.ID) >= SelectedItem.StackSize;
        t = btnGetModDataItem[1].gameObject.transform.Find("button-text").GetComponent<Text>();
        if (t != null) {
            t.text = $"提取10组（{SelectedItem.StackSize * 10}）";
        }
        btnGetModDataItem[1].enabled = GetModDataItemCount(SelectedItem.ID) >= SelectedItem.StackSize * 10;
        btnGetModDataItem[1].button.enabled = GetModDataItemCount(SelectedItem.ID) >= SelectedItem.StackSize * 10;
        t = btnGetModDataItem[2].gameObject.transform.Find("button-text").GetComponent<Text>();
        if (t != null) {
            t.text = $"提取全部（{GetModDataItemCount(SelectedItem.ID)}）";
        }
        btnGetModDataItem[2].enabled = GetModDataItemCount(SelectedItem.ID) > 0;
        btnGetModDataItem[2].button.enabled = GetModDataItemCount(SelectedItem.ID) > 0;
    }

    /// <summary>
    /// 从ModData背包提取指定堆叠数的物品。
    /// 如果groupCount为-1，表示提取所有物品；否则表示提取groupCount组物品。
    /// </summary>
    private static void GetModDataItem(int groupCount) {
        int count = groupCount == -1 ? int.MaxValue : SelectedItem.StackSize * groupCount;
        count = TakeItemFromModData(SelectedItem.ID, count, out int inc);
        if (count == 0) {
            UIMessageBox.Show("提示", $"分馏数据中心没有物品 {SelectedItem.name} ！", "确认", UIMessageBox.WARNING);
        } else {
            AddItemToPackage(SelectedItem.ID, count, inc, false);
            UIMessageBox.Show("提示", $"已从分馏数据中心提取 {SelectedItem.name} x {count} ！", "确认", UIMessageBox.INFO);
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
            UIMessageBox.Show("提示", "没有可提取的分馏塔原胚！", "确认", UIMessageBox.WARNING);
            return;
        }
        UIMessageBox.Show("提示", $"确认提取以下物品吗？{sb}", "确认", "取消", UIMessageBox.WARNING, () => {
            sb = new("已提取以下物品：");
            foreach (int itemID in itemIDs) {
                int takeCount = TakeItemFromModData(itemID, int.MaxValue, out int inc);
                if (takeCount > 0) {
                    AddItemToPackage(itemID, takeCount, inc);
                    sb.Append($"\n{LDB.items.Select(itemID).name} x {takeCount}");
                }
            }
            UIMessageBox.Show("提示", sb.ToString(), "确认", UIMessageBox.INFO);
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
            UIMessageBox.Show("提示", "没有可提取的分馏塔！", "确认", UIMessageBox.WARNING);
            return;
        }
        UIMessageBox.Show("提示", $"确认提取以下物品吗？{sb}", "确认", "取消", UIMessageBox.WARNING, () => {
            sb = new("已提取以下物品：");
            foreach (int itemID in itemIDs) {
                int takeCount = TakeItemFromModData(itemID, int.MaxValue, out int inc);
                if (takeCount > 0) {
                    AddItemToPackage(itemID, takeCount, inc);
                    sb.Append($"\n{LDB.items.Select(itemID).name} x {takeCount}");
                }
            }
            UIMessageBox.Show("提示", sb.ToString(), "确认", UIMessageBox.INFO);
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
            UIMessageBox.Show("提示", "分馏数据中心当前没有物品！", "确认", UIMessageBox.WARNING);
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
        UIMessageBox.Show("提示", sb.ToString(), "确认", UIMessageBox.INFO);
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
