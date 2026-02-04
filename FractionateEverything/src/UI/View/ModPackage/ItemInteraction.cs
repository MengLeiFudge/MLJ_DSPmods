using System;
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
    private const int RowCount = 12;
    private const int ColumnCount = 5;
    private const int ItemsPerPage = RowCount * ColumnCount;

    private static RectTransform window;
    private static RectTransform tab;

    private static ConfigEntry<bool> ShowNotStoredItemEntry;
    private static int SelectedItemID;
    private static int _currentPage;

    private static readonly MyImageButton[,] btnItems = new MyImageButton[RowCount, ColumnCount];
    private static readonly Text[,] txtItemCounts = new Text[RowCount, ColumnCount];
    private static UIButton _prevPageButton;
    private static UIButton _nextPageButton;
    private static Text _pageIndicator;
    private static bool ShowNotStoredItem => ShowNotStoredItemEntry.Value;

    public static void AddTranslations() {
        Register("物品交互", "Item Interaction");

        Register("显示未存储的物品", "Display items not stored");
        Register("查找指定物品", "Search for a specified item");
        Register("上一页", "Previous page");
        Register("下一页", "Next page");

        Register("以下物品在分馏数据中心的存储量为：",
            "The storage capacity of the following items in the Fractionation data centre are: ");
        Register("提取物品", "Extract Item");
        Register("提取物品说明",
            "Left-click or right-click to extract items. The number of extraction groups can be adjusted on the settings page.",
            "左键单击、右键单击均可提取物品，提取组数可以在设置页面调整。");
    }

    public static void LoadConfig(ConfigFile configFile) {
        ShowNotStoredItemEntry = configFile.Bind("Item Interaction", "Show Not Stored Item", false, "是否显示未存储的物品。");
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "物品交互");
        float x = 0f;
        float y = 18f;
        wnd.AddCheckBox(x, y, tab, ShowNotStoredItemEntry, "显示未存储的物品");
        float popupY = y + 36f / 2;
        wnd.AddButton(3, 4, y, tab, "查找指定物品",
            onClick: () => { SearchSpecifiedItem(popupY); });
        y += 36f;
        Text txt = wnd.AddText2(x, y, tab, "以下物品在分馏数据中心的存储量为：");
        wnd.AddTipsButton2(x + 5 + txt.preferredWidth, y, tab, "提取物品", "提取物品说明");
        y += 36f + 7f;
        for (int i = 0; i < RowCount; i++) {
            for (int j = 0; j < ColumnCount; j++) {
                btnItems[i, j] = wnd.AddImageButton(GetPosition(j, ColumnCount).Item1, y, tab)
                    .WithTakeItemClickEvent().WithDeselectOnHover(true, () => SelectedItemID = 0);
                txtItemCounts[i, j] = wnd.AddText2(GetPosition(j, ColumnCount).Item1 + 40 + 5, y, tab, "动态刷新");
            }
            y += 36f + 7f;
        }

        float paginationY = y;
        _prevPageButton = wnd.AddButton(GetPosition(0, 3).Item1, paginationY, tab, "上一页", onClick: PrevPage);
        _pageIndicator = wnd.AddText2(GetPosition(1, 3).Item1, paginationY + 6f, tab, "");
        _pageIndicator.alignment = TextAnchor.MiddleCenter;
        RectTransform pageIndicatorRect = _pageIndicator.rectTransform;
        pageIndicatorRect.sizeDelta = new(200f, pageIndicatorRect.sizeDelta.y);
        _nextPageButton = wnd.AddButton(GetPosition(2, 3).Item1, paginationY, tab, "下一页", onClick: NextPage);
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }

        List<(ItemProto item, long count)> items = GetDisplayItems();
        int totalPages = Math.Max(1, (items.Count + ItemsPerPage - 1) / ItemsPerPage);
        if (_currentPage >= totalPages) {
            _currentPage = totalPages - 1;
        }

        int startIndex = _currentPage * ItemsPerPage;
        int endIndex = Math.Min(startIndex + ItemsPerPage, items.Count);

        int i = 0;
        for (int idx = startIndex; idx < endIndex; idx++) {
            (ItemProto item, long count) = items[idx];
            int row = i / ColumnCount;
            int col = i % ColumnCount;
            btnItems[row, col].gameObject.SetActive(true);
            btnItems[row, col].Proto = item;
            btnItems[row, col].Selected = SelectedItemID > 0 && item.ID == SelectedItemID;
            txtItemCounts[row, col].text = $"x {count}";
            i++;
        }
        for (; i < ItemsPerPage; i++) {
            int row = i / ColumnCount;
            int col = i % ColumnCount;
            btnItems[row, col].gameObject.SetActive(false);
            txtItemCounts[row, col].text = "";
        }

        UpdatePagination(totalPages);
    }

    private static void SearchSpecifiedItem(float y) {
        //物品选取窗口左上角的X值（anchoredPosition是中心点）
        float popupX = tab.anchoredPosition.x - tab.rect.width / 2;
        //物品选取窗口左上角的Y值（anchoredPosition是中心点）
        float popupY = tab.anchoredPosition.y + tab.rect.height / 2 - y;
        UIItemPickerExtension.Popup(new(popupX, popupY), item => {
            if (item == null) return;
            if (GetModDataItemCount(item.ID) == 0) {
                ShowNotStoredItemEntry.Value = true;
            }
            SelectedItemID = item.ID;

            List<(ItemProto item, long count)> items = GetDisplayItems();
            int index = items.FindIndex(tuple => tuple.item.ID == item.ID);
            if (index >= 0) {
                _currentPage = index / ItemsPerPage;
            }
        }, true, item => true);
    }

    private static List<(ItemProto item, long count)> GetDisplayItems() {
        List<(ItemProto, long)> itemCountList = [];
        foreach (ItemProto item in LDB.items.dataArray) {
            long count = GetModDataItemCount(item.ID);
            if (itemValue[item.ID] >= maxValue && count <= 0) {
                continue;
            }
            if (count <= 0 && !ShowNotStoredItem) {
                continue;
            }
            itemCountList.Add((item, count));
        }
        return itemCountList.OrderBy(tuple => itemValue[tuple.Item1.ID]).ToList();
    }

    private static void PrevPage() {
        if (_currentPage <= 0) {
            return;
        }
        _currentPage--;
    }

    private static void NextPage() {
        _currentPage++;
    }

    private static void UpdatePagination(int totalPages) {
        if (_pageIndicator != null) {
            _pageIndicator.text = $"第{_currentPage + 1}页 / 共{totalPages}页";
        }
        if (_prevPageButton != null) {
            _prevPageButton.button.interactable = _currentPage > 0;
        }
        if (_nextPageButton != null) {
            _nextPageButton.button.interactable = _currentPage < totalPages - 1;
        }
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
        _currentPage = 0;
    }

    #endregion
}
