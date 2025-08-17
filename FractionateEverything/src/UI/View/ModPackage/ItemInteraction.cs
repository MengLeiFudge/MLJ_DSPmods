using System.IO;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
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

        Register("物品交互提示按钮说明1",
            "Left-click to select the items you want to query or extract.",
            "左键选择需要查询或提取的物品。");

        Register("当前共有", "There are currently");
        Register("，其中分馏数据中心", "items, including Fractionation data center");
        Register("，物流清单", ", logistics list");
        Register("，个人背包", ", personal package");

        Register("提取1组（", "Extract 1 group (");
        Register("提取10组（", "Extract 10 group (");
        Register("提取全部（", "Extract all (");
        Register("分馏数据中心没有", "Fractionation data center have no");
        Register("已从分馏数据中心提取：", "Extracted from fractionation data center: ");
        Register("！", "!");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "物品交互");
        float x = 0f;
        float y = 20f;
        textCurrItem = wnd.AddText2(x, y, tab, "当前物品", 15, "textCurrItem");
        btnSelectedItem = wnd.AddImageButton(x + textCurrItem.preferredWidth + 5f, y, tab,
            SelectedItem.ID, "button-change-item",
            OnButtonChangeItemClick, OnButtonChangeItemClick,
            "提示", "物品交互提示按钮说明1");
        //todo: 修复按钮提示窗后移除该内容
        wnd.AddTipsButton2(x + textCurrItem.preferredWidth + 5f + 60, y, tab,
            "提示", "物品交互提示按钮说明1");
        y += 50f;
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
                "确定".Translate(),
                UIMessageBox.WARNING);
        } else {
            AddItemToPackage(SelectedItem.ID, count, inc, false);
            UIMessageBox.Show("提示".Translate(),
                $"{"已从分馏数据中心提取：".Translate()}{SelectedItem.name} x {count} {"！".Translate()}",
                "确定".Translate(),
                UIMessageBox.INFO);
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
