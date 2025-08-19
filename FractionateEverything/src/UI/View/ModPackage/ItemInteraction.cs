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

namespace FE.UI.View.ModPackage;

public static class ItemInteraction {
    private static RectTransform window;
    private static RectTransform tab;

    private static Text textCurrItem;
    private static ItemProto SelectedItem { get; set; } = LDB.items.Select(I铁矿);
    private static MyImageButton btnSelectedItem;

    private static void OnButtonChangeItemClick() {
        //_windowTrans.anchoredPosition是窗口的中心点
        //Popup的位置是弹出窗口的左上角
        //所以要向右（x+）向上（y+）
        float x = window.anchoredPosition.x + window.rect.width / 2;
        float y = window.anchoredPosition.y + window.rect.height / 2;
        UIItemPickerExtension.Popup(new(x, y), item => {
            if (item == null) return;
            SelectedItem = item;
        }, true, item => true);
    }

    private static Text textItemCountInfo;
    private static UIButton[] btnGetModDataItem = new UIButton[3];

    public static void AddTranslations() {
        Register("物品交互", "Item Interaction");

        Register("查看分馏数据中心存储的所有物品", "View all items stored in the fractionation data center");
        Register("分馏数据中心没有", "Fractionation data center does not have");
        Register("任何物品", "any item");
        Register("！", "!");
        Register("分馏数据中心存储的物品有：", "The fractionation data center stores the following items:");

        Register("物品交互提示按钮说明1",
            "Left-click to select the items you want to query or extract.",
            "左键选择需要查询或提取的物品。");

        Register("当前共有", "There are currently");
        Register("，其中分馏数据中心", " items, including fractionation data center");
        Register("，物流清单", ", logistics list");
        Register("，个人背包", ", personal package");

        Register("提取1组（", "Extract 1 group (");
        Register("提取10组（", "Extract 10 group (");
        Register("提取全部（", "Extract all (");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "物品交互");
        float x = 0f;
        float y = 18f;
        wnd.AddButton(0, 1, y, tab, "查看分馏数据中心存储的所有物品", 16, "button-get-mod-data-info",
            GetModDataItemInfo);
        y += 36f + 7f;
        textCurrItem = wnd.AddText2(x, y, tab, "当前物品", 15, "textCurrItem");
        btnSelectedItem = wnd.AddImageButton(x + textCurrItem.preferredWidth + 5, y, tab,
            SelectedItem.ID, "button-change-item",
            OnButtonChangeItemClick, OnButtonChangeItemClick,
            "提示", "物品交互提示按钮说明1");
        //todo: 修复按钮提示窗后移除该内容
        wnd.AddTipsButton2(x + textCurrItem.preferredWidth + 5 + 40 + 5, y, tab,
            "提示", "物品交互提示按钮说明1");
        y += 36f + 7f;
        textItemCountInfo = wnd.AddText2(x, y, tab, "动态刷新", 15, "textItemCountInfo");
        y += 36f;
        btnGetModDataItem[0] = wnd.AddButton(0, 3, y, tab, "动态刷新", 16, "button-get-item0",
            () => GetModDataItem(1));
        btnGetModDataItem[1] = wnd.AddButton(1, 3, y, tab, "动态刷新", 16, "button-get-item1",
            () => GetModDataItem(10));
        btnGetModDataItem[2] = wnd.AddButton(2, 3, y, tab, "动态刷新", 16, "button-get-item2",
            () => GetModDataItem(-1));
        y += 36f;
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        btnSelectedItem.SetSprite(SelectedItem.iconSprite);
        textItemCountInfo.text = $"{"当前共有".Translate()} {GetItemTotalCount(SelectedItem.ID)}"
                                 + $"{"，其中分馏数据中心".Translate()} {GetModDataItemCount(SelectedItem.ID)}"
                                 + $"{"，物流清单".Translate()} {GetDeliveryPackageItemCount(SelectedItem.ID)}"
                                 + $"{"，个人背包".Translate()} {GetPackageItemCount(SelectedItem.ID)}";
        var t = btnGetModDataItem[0].gameObject.transform.Find("button-text").GetComponent<Text>();
        if (t != null) {
            t.text = $"{"提取1组（".Translate()}{SelectedItem.StackSize}{"）".Translate()}";
        }
        //enabled -> 启用/禁用    gameObject.SetActive -> 显示/隐藏
        btnGetModDataItem[0].enabled = GetModDataItemCount(SelectedItem.ID) >= SelectedItem.StackSize;
        btnGetModDataItem[0].button.enabled = GetModDataItemCount(SelectedItem.ID) >= SelectedItem.StackSize;
        t = btnGetModDataItem[1].gameObject.transform.Find("button-text").GetComponent<Text>();
        if (t != null) {
            t.text = $"{"提取10组（".Translate()}{SelectedItem.StackSize * 10}{"）".Translate()}";
        }
        btnGetModDataItem[1].enabled = GetModDataItemCount(SelectedItem.ID) >= SelectedItem.StackSize * 10;
        btnGetModDataItem[1].button.enabled = GetModDataItemCount(SelectedItem.ID) >= SelectedItem.StackSize * 10;
        t = btnGetModDataItem[2].gameObject.transform.Find("button-text").GetComponent<Text>();
        if (t != null) {
            t.text = $"{"提取全部（".Translate()}{GetModDataItemCount(SelectedItem.ID)}{"）".Translate()}";
        }
        btnGetModDataItem[2].enabled = GetModDataItemCount(SelectedItem.ID) > 0;
        btnGetModDataItem[2].button.enabled = GetModDataItemCount(SelectedItem.ID) > 0;
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
        UIMessageBox.Show("提示".Translate(),
            sb.ToString(),
            "确定".Translate(), UIMessageBox.INFO,
            null);
    }

    /// <summary>
    /// 从ModData背包提取指定堆叠数的物品。
    /// 如果groupCount为-1，表示提取所有物品；否则表示提取groupCount组物品。
    /// </summary>
    private static void GetModDataItem(int groupCount) {
        int count = groupCount == -1 ? int.MaxValue : SelectedItem.StackSize * groupCount;
        count = TakeItemFromModData(SelectedItem.ID, count, out int inc);
        if (count == 0) {
            UIMessageBox.Show("提示".Translate(),
                $"{"分馏数据中心没有".Translate()} {SelectedItem.name} {"！".Translate()}",
                "确定".Translate(), UIMessageBox.WARNING,
                null);
        } else {
            AddItemToPackage(SelectedItem.ID, count, inc, false);
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
