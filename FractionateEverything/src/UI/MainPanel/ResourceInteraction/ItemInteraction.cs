using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Logic.Civilization;
using FE.Logic.DataCenter;
using FE.UI.Controls;
using FE.UI.Foundation.Window;
using FE.UI.Layout;
using FE.UI.MainPanel.Theme;
using UnityEngine;
using UnityEngine.UI;
using static FE.UI.Layout.GridDsl;
using static FE.Logic.Items.ItemManager;
using static FE.Logic.DataCenter.DataCenterInventory;
using static FE.Utils.Utils;

namespace FE.UI.MainPanel.ResourceInteraction;

/// <summary>
/// 亚空间物品上传、下载和筛选分页页面。
/// </summary>
public static class ItemInteraction {
    private const int RowCount = 8;
    private const int ColumnCount = 8;
    private const int ItemsPerPage = RowCount * ColumnCount;
    private const int FilterColumnCount = 4;
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
        [IFE通用原胚],
        [IFE残片, IFE记忆源点],
        [IFE基础矿物塔, IFE基础解析塔, IFE高级矿物塔, IFE高级交互塔, IFE高级转化塔],
        [IFE行星内物流交互站, IFE星际物流交互站],
        [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵],
    ];
    private static readonly HashSet<int> FractionateGroupItemIds =
        new(FractionateGroupItemIdGroups.SelectMany(group => group));

    private static RectTransform window;
    private static RectTransform tab;

    private static ConfigEntry<bool> ShowNotStoredItemEntry;
    private static ConfigEntry<int> SelectedFilterMaskEntry;
    private static int SelectedItemID;
    private static int _currentPage;

    private static readonly MyImageButton[,] btnItems = new MyImageButton[RowCount, ColumnCount];
    private static readonly MyCheckBox[] _typeFilterChecks = new MyCheckBox[ItemTypeFilters.Length];
    private static MyCheckBox _fractionateGroupCheckBox;
    private static UIButton _prevPageButton;
    private static UIButton _nextPageButton;
    private static Text _pageIndicator;
    private static Text _capacityText;
    private static readonly MyImageButton[] expansionIcons = new MyImageButton[6];
    private static readonly Text[] expansionLabels = new Text[6];
    private static bool ShowNotStoredItem => ShowNotStoredItemEntry.Value;

    public static void LoadConfig(ConfigFile configFile) {
        ShowNotStoredItemEntry = configFile.Bind("Item Interaction", "Show Not Stored Item", false, "是否显示未存储的物品。");
        SelectedFilterMaskEntry = configFile.Bind("Item Interaction", "Selected Filter Mask", 0,
            "物品交互页面的类型筛选位掩码。");
        if (SelectedFilterMaskEntry.Value < 0) {
            SelectedFilterMaskEntry.Value = 0;
        }
    }

    public static void CreateUI(MyWindow wnd, RectTransform trans) {
        window = trans;
        tab = trans;
        CreateUIInternal(wnd, trans);
    }

    private static void CreateUIInternal(MyWindow wnd, RectTransform parent) {
        BuildLayout(wnd, parent,
            Grid(
                rows: [Px(PageLayout.HeaderHeight), Px(146f), 1, Px(PageLayout.FooterHeight)],
                rowGap: PageLayout.Gap,
                children: [
                    Header("亚空间仓储", "亚空间仓储摘要", objectName: "item-interaction-header", pos: (0, 0)),
                    ContentCard(
                        pos: (1, 0),
                        objectName: "item-interaction-filter-card",
                        strong: true,
                        rows: [Px(24f), 1],
                        children: [
                            CardTitleNode("筛选条件", pos: (0, 0), objectName: "item-interaction-filter-title"),
                            Grid(
                                pos: (1, 0),
                                rows: [1, 1, 1, 1],
                                cols: [1, 1, 1, 1],
                                rowGap: 4f,
                                columnGap: PageLayout.InnerGap,
                                children: BuildFilterNodes()),
                        ]),
                    ContentCard(
                        pos: (2, 0),
                        objectName: "item-interaction-grid-card",
                        rows: [Px(24f), Px(52f), 1],
                        rowGap: 6f,
                        children: [
                            CardTitleNode("仓储物品", pos: (0, 0), objectName: "item-interaction-grid-title"),
                            Grid(
                                pos: (1, 0),
                                cols: [1, Px(330f), Px(28f), Px(28f)],
                                columnGap: 8f,
                                children: [
                                    TextNode("以下物品在分馏亚空间的存储量为：",
                                        onBuilt: text => {
                                            _capacityText = text;
                                            text.supportRichText = true;
                                        }, pos: (0, 0), objectName: "item-interaction-grid-summary"),
                                    Grid(pos: (0, 1), cols: [1, 1, 1, 1, 1, 1],
                                        children: BuildExpansionNodes()),
                                    TipsButtonNode("FE.Storage.Expansion", "FE.Storage.ExpansionHelp",
                                        pos: (0, 2), objectName: "item-interaction-expansion-tip"),
                                    TipsButtonNode("提取物品", "提取物品说明",
                                        pos: (0, 3), objectName: "item-interaction-grid-tip"),
                                ]),
                            Grid(
                                pos: (2, 0),
                                rows: BuildEqualRows(RowCount),
                                cols: BuildEqualRows(ColumnCount),
                                rowGap: 4f,
                                columnGap: 4f,
                                children: BuildItemGridNodes()),
                        ]),
                    FooterCard(
                        pos: (3, 0),
                        objectName: "item-interaction-footer-card",
                        cols: [1, 1, 1],
                        columnGap: 12f,
                        children: [
                            ButtonNode("上一页", onClick: PrevPage, onBuilt: btn => _prevPageButton = btn,
                                pos: (0, 0), objectName: "item-interaction-footer-prev"),
                            TextNode("", anchor: TextAnchor.MiddleCenter, onBuilt: text => _pageIndicator = text,
                                pos: (0, 1), objectName: "item-interaction-page-indicator"),
                            ButtonNode("下一页", onClick: NextPage, onBuilt: btn => _nextPageButton = btn,
                                pos: (0, 2), objectName: "item-interaction-footer-next"),
                        ]),
                ]));
        AssemblyPanel.Create(wnd, parent);
    }

    /// <summary>每档固定一个图标与状态，供料变化不改变布局。</summary>
    private static IReadOnlyList<LayoutNode> BuildExpansionNodes() {
        var nodes = new List<LayoutNode>();
        for (int i = 0; i < expansionIcons.Length; i++) {
            int index = i;
            nodes.Add(Grid(pos: (0, i), rows: [Px(30f), Px(20f)], children: [
                ImageButtonNode(size: 28f, onBuilt: button => expansionIcons[index] = button,
                    pos: (0, 0), objectName: $"expansion-matrix-{i}"),
                TextNode("", fontSize: 10, anchor: TextAnchor.MiddleCenter,
                    onBuilt: text => expansionLabels[index] = text, pos: (1, 0), objectName: $"expansion-state-{i}"),
            ]));
        }
        return nodes;
    }

    private static IReadOnlyList<LayoutNode> BuildFilterNodes() {
        var nodes = new List<LayoutNode> {
            CheckBoxNode(ShowNotStoredItemEntry, "显示未存储的物品",
                pos: (0, 0), span: (1, 2), objectName: "item-interaction-show-empty"),
            ButtonNode("查找指定物品", onClick: () => { SearchSpecifiedItem(150f); },
                pos: (0, 3), objectName: "item-interaction-search"),
        };

        for (int i = 0; i < ItemTypeFilters.Length; i++) {
            int row = i / FilterColumnCount;
            int col = i % FilterColumnCount;
            int filterIndex = i;
            nodes.Add(CheckBoxNode(IsFilterSelected(filterIndex), ItemTypeFilters[i].labelKey, 14,
                onBuilt: cb => {
                    _typeFilterChecks[filterIndex] = cb;
                    cb.OnChecked += () => {
                        SaveSelectedFilterMask();
                        _currentPage = 0;
                    };
                },
                pos: (row + 1, col), objectName: $"item-interaction-filter-{filterIndex}"));
        }

        nodes.Add(CheckBoxNode(IsFilterSelected(ItemTypeFilters.Length), "万物分馏", 14,
            onBuilt: cb => {
                _fractionateGroupCheckBox = cb;
                cb.OnChecked += () => {
                    SaveSelectedFilterMask();
                    _currentPage = 0;
                };
            },
            pos: (3, 3), objectName: "item-interaction-filter-fractionate"));

        return nodes;
    }

    private static IReadOnlyList<LayoutTrack> BuildEqualRows(int count) {
        var rows = new List<LayoutTrack>();
        for (int i = 0; i < count; i++) {
            rows.Add(1);
        }

        return rows;
    }

    private static IReadOnlyList<LayoutNode> BuildItemGridNodes() {
        var nodes = new List<LayoutNode>();
        for (int row = 0; row < RowCount; row++) {
            for (int col = 0; col < ColumnCount; col++) {
                int r = row;
                int c = col;
                nodes.Add(ImageButtonNode(size: 40f,
                    onBuilt: btn => {
                        btnItems[r, c] = btn
                            .WithTakeItemClickEvent()
                            .WithDeselectOnHover(true, () => SelectedItemID = 0);
                    },
                    pos: (r, c), objectName: $"item-interaction-item-{r}-{c}"));
            }
        }

        return nodes;
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
        int hoveredItemId = 0;
        for (int idx = startIndex; idx < endIndex; idx++) {
            (ItemProto item, long count) = items[idx];
            int row = i / ColumnCount;
            int col = i % ColumnCount;
            btnItems[row, col].gameObject.SetActive(true);
            btnItems[row, col].Proto = item;
            btnItems[row, col].SetCount(count);
            btnItems[row, col].Selected = SelectedItemID > 0 && item.ID == SelectedItemID;
            if (btnItems[row, col].uiButton.isPointerEnter) { hoveredItemId = item.ID; }
            i++;
        }
        for (; i < ItemsPerPage; i++) {
            int row = i / ColumnCount;
            int col = i % ColumnCount;
            btnItems[row, col].gameObject.SetActive(false);
        }

        int activeMatrices = SubspaceExpansion.GetActiveMatrixMask();
        for (int stage = 0; stage < expansionIcons.Length; stage++) {
            bool active = (activeMatrices & (1 << stage)) != 0;
            expansionIcons[stage].Proto = LDB.items.Select(MainProgressMatrixIds[stage]);
            expansionIcons[stage].Selected = active;
            expansionLabels[stage].text = active ? "FE.Storage.Active".Translate() : "";
        }
        _capacityText.text = (hoveredItemId > 0
            ? string.Format("FE.Storage.ItemCapacity".Translate(), LDB.items.Select(hoveredItemId)?.name ?? "?", GetModDataItemCount(hoveredItemId),
                GrowthTechnologyManager.UltimateCapacity ? "∞" : GetItemCapacity(hoveredItemId).ToString("N0"))
            : "以下物品在分馏亚空间的存储量为：".Translate()).WithColor(Gray);
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

    private static bool IsFilterSelected(int index) {
        int mask = SelectedFilterMaskEntry?.Value ?? 0;
        return (mask & (1 << index)) != 0;
    }

    private static void SaveSelectedFilterMask() {
        if (SelectedFilterMaskEntry == null) {
            return;
        }

        int mask = 0;
        for (int i = 0; i < _typeFilterChecks.Length; i++) {
            if (_typeFilterChecks[i] != null && _typeFilterChecks[i].Checked) {
                mask |= 1 << i;
            }
        }

        if (_fractionateGroupCheckBox != null && _fractionateGroupCheckBox.Checked) {
            mask |= 1 << ItemTypeFilters.Length;
        }

        SelectedFilterMaskEntry.Value = mask;
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
        SaveSelectedFilterMask();
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
            _pageIndicator.text = string.Format("第{0}页 / 共{1}页".Translate(), _currentPage + 1, totalPages);
        }
        if (_prevPageButton != null) {
            _prevPageButton.button.interactable = _currentPage > 0;
        }
        if (_nextPageButton != null) {
            _nextPageButton.button.interactable = _currentPage < totalPages - 1;
        }
    }

    #region 模组存档接口实现

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
