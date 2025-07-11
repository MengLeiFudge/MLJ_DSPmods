using System.IO;
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

    public static void OnButtonChangeItemClick() {
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

    /// <summary>
    /// 显示指定物品分别在MOD数据、背包、物流背包中的数量
    /// </summary>
    private static Text[] textItemCount = new Text[3];
    /// <summary>
    /// 显示指定物品在MOD数据、背包、物流背包中的总数
    /// </summary>
    private static Text textItemTotalCount;
    private static UIButton[] btnGetModDataItem = new UIButton[3];

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
            wnd.AddButton(x, y, 200, tab, "切换物品", 16, "button-change-item", OnButtonChangeItemClick);
            y += 36f;
            textItemCount[0] = wnd.AddText2(x, y, tab, "", 15, "textItemCount0");
            y += 36f;
            textItemCount[1] = wnd.AddText2(x, y, tab, "", 15, "textItemCount1");
            y += 36f;
            textItemCount[2] = wnd.AddText2(x, y, tab, "", 15, "textItemCount2");
            y += 36f;
            textItemTotalCount = wnd.AddText2(x, y, tab, "", 15, "textItemTotalCount");
            y += 36f;
            btnGetModDataItem[0] = wnd.AddButton(x, y, 200, tab, "提取1组物品", 16, "button-get-item",
                () => GetModDataItem(1));
            btnGetModDataItem[1] = wnd.AddButton(x + 220, y, 200, tab, "提取10组物品", 16, "button-get-item",
                () => GetModDataItem(10));
            btnGetModDataItem[2] = wnd.AddButton(x + 440, y, 200, tab, "提取全部物品", 16, "button-get-item",
                () => GetModDataItem(-1));
            y += 36f;
        }
        {
            var tab = wnd.AddTab(trans, "矩阵商店");
            x = 0f;
            y = 10f;
            wnd.AddButton(x, y, 400, tab, "200蓝糖兑换1交互塔", 16, "btn-blue1",
                () => { ExchangeItemsWithQuestion(I电磁矩阵, 200, IFE交互塔, 1); });
            y += 36f;
            wnd.AddButton(x, y, 400, tab, "10蓝糖兑换1分馏原胚（普通）", 16, "btn-blue2",
                () => { ExchangeItemsWithQuestion(I电磁矩阵, 10, IFE分馏原胚普通, 1); });
            y += 36f;
        }
        {
            var tab = wnd.AddTab(trans, "黑雾商店");
            x = 0f;
            y = 10f;
        }
    }

    public static void UpdateUI() {
        textItemCount[0].text = $"数据中心有 {GetModDataItemCount(SelectedItem.ID)} 个 {SelectedItem.name}";
        textItemCount[1].text = $"背包有 {GetPackageItemCount(SelectedItem.ID)} 个 {SelectedItem.name}";
        textItemCount[2].text = $"物流背包有 {GetDeliveryPackageItemCount(SelectedItem.ID)} 个 {SelectedItem.name}";
        textItemTotalCount.text = $"总计有 {GetItemTotalCount(SelectedItem.ID)} 个 {SelectedItem.name}";
        var t = btnGetModDataItem[0].gameObject.transform.Find("button-text").GetComponent<Text>();
        if (t != null) {
            t.text = $"提取 {SelectedItem.name} x {SelectedItem.StackSize}";
        }
        t = btnGetModDataItem[1].gameObject.transform.Find("button-text").GetComponent<Text>();
        if (t != null) {
            t.text = $"提取 {SelectedItem.name} x {SelectedItem.StackSize * 10}";
        }
        t = btnGetModDataItem[2].gameObject.transform.Find("button-text").GetComponent<Text>();
        if (t != null) {
            t.text = $"提取 {SelectedItem.name} x {GetModDataItemCount(SelectedItem.ID)}";
        }
    }

    /// <summary>
    /// 从ModData背包提取指定堆叠数的物品。
    /// 如果groupCount为-1，表示提取所有物品；否则表示提取groupCount组物品。
    /// </summary>
    public static void GetModDataItem(int groupCount) {
        int realTakeCount = groupCount == -1
            ? int.MaxValue
            : TakeItemFromModData(SelectedItem.ID, SelectedItem.StackSize * groupCount);
        if (realTakeCount <= 0) {
            UIMessageBox.Show("提示", $"分馏中心没有物品 {SelectedItem.name}", "确认", UIMessageBox.WARNING);
        } else {
            AddItemToPackage(SelectedItem.ID, realTakeCount, false);
            UIMessageBox.Show("提示", $"已从分馏中心提取 {SelectedItem.name} x {realTakeCount} ！", "确认", UIMessageBox.INFO);
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
