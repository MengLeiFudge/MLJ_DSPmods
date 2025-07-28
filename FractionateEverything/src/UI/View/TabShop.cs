using System;
using System.IO;
using System.Text;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.View.TabRecipeAndBuilding;
using static FE.Utils.Utils;

namespace FE.UI.View;

public static class TabShop {
    public static RectTransform _windowTrans;

    #region 选择物品

    private static Text textCurrItem;
    private static MyImageButton btnSelectedItem;

    private static void OnButtonChangeItemClick() {
        //_windowTrans.anchoredPosition是窗口的中心点
        //Popup的位置是弹出窗口的左上角
        //所以要向右（x+）向上（y+）
        float x = _windowTrans.anchoredPosition.x + _windowTrans.rect.width / 2 + 5;
        float y = _windowTrans.anchoredPosition.y + _windowTrans.rect.height / 2 + 5;
        UIItemPickerExtension.Popup(new(x, y), item => {
            if (item == null) return;
            SelectedItem = item;
        }, false, item => true);
    }

    #endregion

    private static Text textItemCountInfo;
    private static UIButton[] btnGetModDataItem = new UIButton[3];
    private static UIButton btnGetModDataProto;
    private static UIButton btnGetModDataBuilding;

    private static DateTime nextFreshTime;
    private static Text textLeftTime;
    private static Text[] textItemInfo = new Text[3];

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        _windowTrans = trans;
        float x;
        float y;
        wnd.AddTabGroup(trans, "商店", "tab-group-fe3");
        {
            var tab = wnd.AddTab(trans, "数据中心");
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
                GetModDataProto);
            btnGetModDataBuilding = wnd.AddButton(x + 320, y, 300, tab, "提取所有分馏塔", 16, "button-get-building",
                GetModDataBuilding);
        }
        {
            var tab = wnd.AddTab(trans, "限时商店");
            nextFreshTime = DateTime.Now.Date.AddHours(DateTime.Now.Hour)
                .AddMinutes(DateTime.Now.Minute / 10 * 10 + 10);
            x = 0f;
            y = 10f;
            textLeftTime = wnd.AddText2(x, y, tab, "剩余刷新时间：xx s", 15, "textLeftTime");
            y += 36f;
            textItemInfo[0] = wnd.AddText2(x, y, tab, "物品0信息", 15, "textLeftTime0");
            wnd.AddButton(x + 350, y, 400, tab, "兑换", 16, "btn-buy-time1",
                () => ExchangeItem(0));
            y += 36f;
            textItemInfo[1] = wnd.AddText2(x, y, tab, "物品1信息", 15, "textLeftTime1");
            wnd.AddButton(x + 350, y, 400, tab, "兑换", 16, "btn-buy-time2",
                () => ExchangeItem(1));
            y += 36f;
            textItemInfo[2] = wnd.AddText2(x, y, tab, "物品2信息", 15, "textLeftTime2");
            wnd.AddButton(x + 350, y, 400, tab, "兑换", 16, "btn-buy-time3",
                () => ExchangeItem(2));
            y += 36f;
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
        t = btnGetModDataItem[1].gameObject.transform.Find("button-text").GetComponent<Text>();
        if (t != null) {
            t.text = $"提取10组（{SelectedItem.StackSize * 10}）";
        }
        btnGetModDataItem[1].enabled = GetModDataItemCount(SelectedItem.ID) >= SelectedItem.StackSize * 10;
        t = btnGetModDataItem[2].gameObject.transform.Find("button-text").GetComponent<Text>();
        if (t != null) {
            t.text = $"提取全部（{GetModDataItemCount(SelectedItem.ID)}）";
        }
        btnGetModDataItem[2].enabled = GetModDataItemCount(SelectedItem.ID) > 0;

        if (DateTime.Now >= nextFreshTime) {
            nextFreshTime = nextFreshTime.AddMinutes(10);
            //更新三份限时购买物品的信息
            // textItemInfo[0].text = GetTimeLimitedItemInfo(0);
            // textItemInfo[1].text = GetTimeLimitedItemInfo(1);
            // textItemInfo[2].text = GetTimeLimitedItemInfo(2);
        }
        TimeSpan ts = nextFreshTime - DateTime.Now;
        textLeftTime.text = $"还有 {(int)ts.TotalMinutes} min {ts.Seconds} s 刷新";
    }

    /// <summary>
    /// 从ModData背包提取指定堆叠数的物品。
    /// 如果groupCount为-1，表示提取所有物品；否则表示提取groupCount组物品。
    /// </summary>
    private static void GetModDataItem(int groupCount) {
        int takeCount = groupCount == -1 ? int.MaxValue : SelectedItem.StackSize * groupCount;
        int realTakeCount = TakeItemFromModData(SelectedItem.ID, takeCount);
        if (realTakeCount <= 0) {
            UIMessageBox.Show("提示", $"分馏数据中心没有物品 {SelectedItem.name} ！", "确认", UIMessageBox.WARNING);
        } else {
            AddItemToPackage(SelectedItem.ID, realTakeCount, false);
            UIMessageBox.Show("提示", $"已从分馏数据中心提取 {SelectedItem.name} x {realTakeCount} ！", "确认", UIMessageBox.INFO);
        }
    }

    private static void GetModDataProto() {
        StringBuilder sb = new StringBuilder();
        int[] itemIDs = [IFE分馏塔原胚普通, IFE分馏塔原胚精良, IFE分馏塔原胚稀有, IFE分馏塔原胚史诗, IFE分馏塔原胚传说, IFE分馏塔原胚定向];
        int[] counts = new int[itemIDs.Length];
        for (int i = 0; i < itemIDs.Length; i++) {
            counts[i] = GetModDataItemCount(itemIDs[i]);
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
            for (int i = 0; i < itemIDs.Length; i++) {
                if (counts[i] > 0) {
                    TakeItemFromModData(itemIDs[i], counts[i]);
                }
            }
        }, null);
    }

    private static void GetModDataBuilding() {
        StringBuilder sb = new StringBuilder();
        int[] itemIDs = [IFE交互塔, IFE矿物复制塔, IFE点数聚集塔, IFE量子复制塔, IFE点金塔, IFE分解塔, IFE转化塔];
        int[] counts = new int[itemIDs.Length];
        for (int i = 0; i < itemIDs.Length; i++) {
            counts[i] = GetModDataItemCount(itemIDs[i]);
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
            for (int i = 0; i < itemIDs.Length; i++) {
                if (counts[i] > 0) {
                    TakeItemFromModData(itemIDs[i], counts[i]);
                }
            }
        }, null);
    }

    public static void ExchangeItem(int index) { }

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
