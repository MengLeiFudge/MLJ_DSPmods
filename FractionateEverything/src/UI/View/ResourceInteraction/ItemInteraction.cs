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

namespace FE.UI.View.ResourceInteraction;

public static class ItemInteraction {
    private const int RowCount = 9;
    private const int ColumnCount = 8;
    private const int ItemsPerPage = RowCount * ColumnCount;
    private const int FilterColumnCount = 4;
    private const float FilterLineHeight = 36f + 7f;
    private static readonly (EItemType type, string labelKey)[] ItemTypeFilters = [
        (EItemType.Resource, "自然资源"),
        (EItemType.Material, "材料"),
        (EItemType.Component, "组件"),
        (EItemType.Product, "成品"),
        (EItemType.Logistics, "物流运输"),
        (EItemType.Production, "生产设备"),
        (EItemType.Decoration, "装饰物"),
        (EItemType.Turret, "武器"),
        (EItemType.Defense, "防御设施"),
        (EItemType.DarkFog, "黑雾物品"),
        (EItemType.Matrix, "科学矩阵"),
    ];
    private static readonly int[][] FractionateGroupItemIdGroups = [
        [IFE交互塔原胚, IFE矿物复制塔原胚, IFE点数聚集塔原胚, IFE转化塔原胚, IFE精馏塔原胚, IFE分馏塔定向原胚],
        [IFE残片],
        [IFE交互塔, IFE行星内物流交互站, IFE星际物流交互站],
        [IFE矿物复制塔, IFE点数聚集塔, IFE转化塔, IFE精馏塔],
        [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵],
        [I能量碎片, I黑雾矩阵, I物质重组器, I硅基神经元, I负熵奇点, I核心素],
    ];
    private static readonly HashSet<int> FractionateGroupItemIds =
        new(FractionateGroupItemIdGroups.SelectMany(group => group));

    private static RectTransform window;
    private static RectTransform tab;

    private static ConfigEntry<bool> ShowNotStoredItemEntry;
    private static int SelectedItemID;
    private static int _currentPage;

    private static readonly MyImageButton[,] btnItems = new MyImageButton[RowCount, ColumnCount];
    private static readonly MyCheckBox[] _typeFilterChecks = new MyCheckBox[ItemTypeFilters.Length];
    private static MyCheckBox _fractionateGroupCheckBox;
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
        Register("万物分馏", "Fractionate Everything");
        Register("按类型筛选可见物品", "Filter visible items by type");

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

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        window = trans;
        tab = trans;
        CreateUIInternal(wnd, trans);
    }

    private static void CreateUIInternal(MyWindow wnd, RectTransform parent) {
        float x = 0f;
        float y = 18f;
        wnd.AddCheckBox(x, y, parent, ShowNotStoredItemEntry, "显示未存储的物品");
        float popupY = y + 36f / 2;
        wnd.AddButton(3, 4, y, parent, "查找指定物品",
            onClick: () => { SearchSpecifiedItem(popupY); });

        y += FilterLineHeight;
        CreateFilterCheckBoxes(parent, y);

        y += FilterLineHeight * 3f;
        Text txt = wnd.AddText2(x, y, parent, "以下物品在分馏数据中心的存储量为：");
        wnd.AddTipsButton2(x + 5 + txt.preferredWidth, y, parent, "提取物品", "提取物品说明");
        y += 36f + 7f;
        for (int i = 0; i < RowCount; i++) {
            for (int j = 0; j < ColumnCount; j++) {
                btnItems[i, j] = wnd.AddImageButton(GetPosition(j, ColumnCount).Item1, y, parent)
                    .WithSize(40f, 40f)
                    .WithTakeItemClickEvent()
                    .WithDeselectOnHover(true, () => SelectedItemID = 0);
            }
            y += 36f + 7f;
        }

        float paginationY = y;
        _prevPageButton = wnd.AddButton(GetPosition(0, 3).Item1, paginationY, parent, "上一页", onClick: PrevPage);
        _pageIndicator = wnd.AddText2(GetPosition(1, 3).Item1, paginationY + 6f, parent, "");
        _pageIndicator.alignment = TextAnchor.MiddleCenter;
        RectTransform pageIndicatorRect = _pageIndicator.rectTransform;
        pageIndicatorRect.sizeDelta = new(200f, pageIndicatorRect.sizeDelta.y);
        _nextPageButton = wnd.AddButton(GetPosition(2, 3).Item1, paginationY, parent, "下一页", onClick: NextPage);
    }

    private static void CreateFilterCheckBoxes(RectTransform parent, float startY) {
        for (int i = 0; i < ItemTypeFilters.Length; i++) {
            int row = i / FilterColumnCount;
            int col = i % FilterColumnCount;
            (float posX, _) = GetPosition(col, FilterColumnCount);
            _typeFilterChecks[i] = CreateFilterCheckBox(posX, startY + row * FilterLineHeight, parent,
                ItemTypeFilters[i].labelKey);
        }

        _fractionateGroupCheckBox = CreateFilterCheckBox(GetPosition(3, FilterColumnCount).Item1,
            startY + 2f * FilterLineHeight, parent, "万物分馏");
    }

    private static MyCheckBox CreateFilterCheckBox(float x, float y, RectTransform parent, string label) {
        MyCheckBox checkBox = MyCheckBox.CreateCheckBox(x, y, parent, false, label, 14);
        checkBox.OnChecked += () => { _currentPage = 0; };
        return checkBox;
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
            btnItems[row, col].SetCount(count);
            btnItems[row, col].Selected = SelectedItemID > 0 && item.ID == SelectedItemID;
            i++;
        }
        for (; i < ItemsPerPage; i++) {
            int row = i / ColumnCount;
            int col = i % ColumnCount;
            btnItems[row, col].gameObject.SetActive(false);
        }

        UpdatePagination(totalPages);
    }

    private static void SearchSpecifiedItem(float y) {
        ClearAllGroupFilters();

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
        bool hasSelectedFilter = HasSelectedGroupFilter();
        foreach (ItemProto item in LDB.items.dataArray) {
            long count = GetModDataItemCount(item.ID);
            bool inFractionateGroup = FractionateGroupItemIds.Contains(item.ID);
            bool matchFractionateGroup = _fractionateGroupCheckBox != null
                                         && _fractionateGroupCheckBox.Checked
                                         && inFractionateGroup;

            if (!matchFractionateGroup && itemValue[item.ID] >= maxValue && count <= 0) {
                continue;
            }
            if (!ShouldDisplayItemByFilter(item, matchFractionateGroup, hasSelectedFilter)) {
                continue;
            }
            if (!matchFractionateGroup && count <= 0 && !ShowNotStoredItem) {
                continue;
            }
            itemCountList.Add((item, count));
        }
        return itemCountList.OrderBy(tuple => itemValue[tuple.Item1.ID]).ToList();
    }

    /// <summary>
    /// 当顶部有任意勾选时，仅显示命中的类型或“万物分馏”分组；全部取消勾选时回退到原来的全量列表。
    /// </summary>
    private static bool ShouldDisplayItemByFilter(ItemProto item, bool matchFractionateGroup, bool hasSelectedFilter) {
        if (!hasSelectedFilter) {
            return true;
        }

        if (matchFractionateGroup) {
            return true;
        }

        for (int i = 0; i < ItemTypeFilters.Length; i++) {
            if (_typeFilterChecks[i] != null
                && _typeFilterChecks[i].Checked
                && item.Type == ItemTypeFilters[i].type) {
                return true;
            }
        }
        return false;
    }

    private static bool HasSelectedGroupFilter() {
        if (_fractionateGroupCheckBox != null && _fractionateGroupCheckBox.Checked) {
            return true;
        }

        for (int i = 0; i < _typeFilterChecks.Length; i++) {
            if (_typeFilterChecks[i] != null && _typeFilterChecks[i].Checked) {
                return true;
            }
        }
        return false;
    }

    private static void ClearAllGroupFilters() {
        for (int i = 0; i < _typeFilterChecks.Length; i++) {
            if (_typeFilterChecks[i] != null) {
                _typeFilterChecks[i].Checked = false;
            }
        }

        if (_fractionateGroupCheckBox != null) {
            _fractionateGroupCheckBox.Checked = false;
        }
        _currentPage = 0;
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
        r.ReadBlocks();
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks();
    }

    public static void IntoOtherSave() {
        SelectedItemID = 0;
        _currentPage = 0;
        ClearAllGroupFilters();
    }

    #endregion
}
