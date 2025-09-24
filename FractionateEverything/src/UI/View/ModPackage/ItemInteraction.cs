using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    private static float[] ItemValueRanges = [5, 20, 100, 500, 2500, 10000, 100000, maxValue];
    private static string[] ItemValueRangesStr = [
        "0-5", "5-20", "20-100", "100-500", "500-2500", "2500-10000", "10000-100000", "100000-∞"
    ];
    private static ConfigEntry<int> ItemValueRangeEntry;
    private static ConfigEntry<bool> ShowNotStoredItemEntry;
    private static bool ShowNotStoredItem => ShowNotStoredItemEntry.Value;
    private static int SelectedItemID = 0;

    private static MyImageButton[,] btnItems = new MyImageButton[12, 5];
    private static Text[,] txtItemCounts = new Text[12, 5];

    public static void AddTranslations() {
        Register("物品交互", "Item Interaction");

        Register("物品价值区间", "Item value range");
        Register("显示未存储的物品", "Display items not stored");
        Register("查找指定物品", "Search for a specified item");

        Register("以下物品在分馏数据中心的存储量为：",
            "The storage capacity of the following items in the Fractionation data centre are: ");
        Register("提取物品", "Extract Item");
        Register("提取物品说明",
            "Left-click or right-click to extract items. The number of extraction groups can be adjusted on the settings page.",
            "左键单击、右键单击均可提取物品，提取组数可以在设置页面调整。");
    }

    public static void LoadConfig(ConfigFile configFile) {
        ItemValueRangeEntry = configFile.Bind("Item Interaction", "Item Value Range", 0, "想要查看的物品价值区间。");
        if (ItemValueRangeEntry.Value < 0 || ItemValueRangeEntry.Value >= ItemValueRangesStr.Length) {
            ItemValueRangeEntry.Value = 0;
        }
        ShowNotStoredItemEntry = configFile.Bind("Item Interaction", "Show Not Stored Item", false, "是否显示未存储的物品。");
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "物品交互");
        float x = 0f;
        float y = 18f;
        wnd.AddComboBox(x, y, tab, "物品价值区间")
            .WithItems(ItemValueRangesStr).WithSize(200, 0).WithConfigEntry(ItemValueRangeEntry)
            .WithOnSelChanged(SelectedItemIDChanged);
        wnd.AddCheckBox(GetPosition(2, 4).Item1, y, tab, ShowNotStoredItemEntry, "显示未存储的物品");
        float popupY = y + 36f / 2;
        wnd.AddButton(3, 4, y, tab, "查找指定物品",
            onClick: () => { SearchSpecifiedItem(popupY); });
        y += 36f;
        Text txt = wnd.AddText2(x, y, tab, "以下物品在分馏数据中心的存储量为：");
        wnd.AddTipsButton2(x + txt.preferredWidth + 5, y, tab, "提取物品", "提取物品说明");
        y += 36f + 7f;
        for (int i = 0; i < 12; i++) {
            for (int j = 0; j < 5; j++) {
                btnItems[i, j] = wnd.AddImageButtonWithDefAction(GetPosition(j, 5).Item1, y, tab);
                txtItemCounts[i, j] = wnd.AddText2(GetPosition(j, 5).Item1 + 40 + 5, y, tab, "动态刷新");
            }
            y += 36f + 7f;
        }
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        //根据选择的物品价值层次，获取物品并更新UI
        Dictionary<ItemProto, long> itemCountDic = [];
        float valueRangeMin = ItemValueRangeEntry.Value == 0 ? 0 : ItemValueRanges[ItemValueRangeEntry.Value - 1];
        float valueRangeMax = ItemValueRanges[ItemValueRangeEntry.Value];
        if (ItemValueRangeEntry.Value == ItemValueRanges.Length - 1) {
            //如果一个无价值的物品在Mod背包数目大于0，则显示它
            foreach (ItemProto item in LDB.items.dataArray) {
                if (itemValue[item.ID] < valueRangeMin) {
                    continue;
                }
                long count = GetModDataItemCount(item.ID);
                if (itemValue[item.ID] < maxValue) {
                    if (count <= 0 && !ShowNotStoredItem) {
                        continue;
                    }
                } else {
                    if (count <= 0) {
                        continue;
                    }
                }
                itemCountDic[item] = count;
            }
        } else {
            //正常处理，只显示符合物品价值区间的物品
            foreach (ItemProto item in LDB.items.dataArray) {
                if (itemValue[item.ID] < valueRangeMin || itemValue[item.ID] >= valueRangeMax) {
                    continue;
                }
                long count = GetModDataItemCount(item.ID);
                if (count <= 0 && !ShowNotStoredItem) {
                    continue;
                }
                itemCountDic[item] = count;
            }
        }
        int i = 0;
        foreach (var p in itemCountDic.OrderBy(kvp => kvp.Key.GridIndex)) {
            btnItems[i / 5, i % 5].gameObject.SetActive(true);
            btnItems[i / 5, i % 5].ItemId = p.Key.ID;
            btnItems[i / 5, i % 5].IsSelected = SelectedItemID > 0 && p.Key.ID == SelectedItemID;
            txtItemCounts[i / 5, i % 5].text = $"x {p.Value}";
            i++;
        }
        for (; i < 12 * 5; i++) {
            btnItems[i / 5, i % 5].gameObject.SetActive(false);
            txtItemCounts[i / 5, i % 5].text = "";
        }
    }

    private static void SearchSpecifiedItem(float y) {
        //物品选取窗口左上角的X值（anchoredPosition是中心点）
        float popupX = tab.anchoredPosition.x - tab.rect.width / 2;
        //物品选取窗口左上角的Y值（anchoredPosition是中心点）
        float popupY = tab.anchoredPosition.y + tab.rect.height / 2 - y;
        UIItemPickerExtension.Popup(new(popupX, popupY), item => {
            if (item == null) return;
            float value = itemValue[item.ID];
            for (int i = 0; i < ItemValueRanges.Length; i++) {
                if (value < ItemValueRanges[i]) {
                    ItemValueRangeEntry.Value = i;
                    if (GetModDataItemCount(item.ID) == 0) {
                        ShowNotStoredItemEntry.Value = true;
                    }
                    SelectedItemID = item.ID;
                    return;
                }
            }
        }, true, item => true);
    }

    private static void SelectedItemIDChanged(int idx) {
        SelectedItemID = 0;
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
    }

    public static void IntoOtherSave() {
        SelectedItemID = 0;
    }

    #endregion
}
