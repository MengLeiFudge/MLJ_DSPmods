using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Logic.Manager.RuneManager;
using static FE.Utils.Utils;

namespace FE.UI.View.RuneSystem;

public static class RuneMenu {
    private const int RuneButtonColumns = 8;
    private const int RuneButtonRows = 4;
    private const int RuneButtonCount = RuneButtonColumns * RuneButtonRows;
    private static RectTransform tab;

    private static UIButton[] slotButtons = new UIButton[5];
    private static Text[] slotTexts = new Text[5];
    private static readonly MyImageButton[] runeButtons = new MyImageButton[RuneButtonCount];
    private static readonly Toggle[] runeCheckboxes = new Toggle[RuneButtonCount];
    private static readonly Text[] runeLevelTexts = new Text[RuneButtonCount];
    private static readonly HashSet<long> selectedRuneIds = new();
    private static Text runeCountText;

    private static Text runeDetailText;
    private static Text totalStatsText;
    private static UIButton upgradeButton;
    private static UIButton equipButton;
    private static UIButton disassembleButton;
    private static UIButton batchDisassembleButton;
    private static UIButton selectAllButton;

    private static Rune _selectedRune;
    private static int _selectedSlotIndex = -1;

    // Filter and sort state
    private static int filterStar = 0;// 0 = all, 1-5 = specific star
    private static int filterMainStat = -1;// -1 = all, 0-3 = specific stat
    private static int filterSubStat = -1;// -1 = all, 0-3 = specific stat
    private static int sortMode = 0;// 0 = none, 1 = level asc, 2 = level desc

    private static UIButton filterStarButton;
    private static UIButton filterMainStatButton;
    private static UIButton filterSubStatButton;
    private static UIButton sortButton;
    private static UIButton resetFilterButton;

    public static void AddTranslations() {
        Register("符文系统", "Rune System");
        Register("已装备符文", "Equipped Runes");
        Register("持有符文", "Owned Runes");
        Register("符文详情", "Rune Details");
        Register("强化", "Upgrade");
        Register("装备", "Equip");
        Register("卸下", "Unequip");
        Register("分解", "Disassemble");
        Register("符文等级上限", "Max Level reached");
        Register("精华不足", "Insufficient Essence");
        Register("总加成", "Total Bonus");
        Register("符文槽位未解锁", "Slot Locked");
        Register("确认分解符文", "Are you sure you want to disassemble this rune? 80% of materials will be returned.");
        Register("空槽位", "Empty Slot");
        Register("符文列表为空", "Rune list is empty");
        Register("请选择一个符文查看详情", "Select a rune to view details");
        Register("符文加成", "Rune Bonus");
        Register("制作速度", "Speed");
        Register("产品产能", "Productivity");
        Register("电力消耗", "Energy Saving");
        Register("增产效果", "Proliferator");
        Register("词条", "Stat");
        Register("主词条", "Main Stat");
        Register("副词条", "Sub Stat");
        Register("没有空余槽位", "No empty slots");
        Register("星符文", "-Star Rune", "星符文");
        Register("筛选星级", "Filter by Star");
        Register("筛选主词条", "Filter by Main Stat");
        Register("筛选副词条", "Filter by Sub Stat");
        Register("排序", "Sort");
        Register("重置筛选", "Reset Filters");
        Register("全选", "Select All");
        Register("取消全选", "Deselect All");
        Register("批量分解", "Batch Disassemble");
        Register("全部", "All");
        Register("等级升序", "Level ↑");
        Register("等级降序", "Level ↓");
        Register("无排序", "No Sort");
        Register("确认批量分解", "Are you sure you want to disassemble {0} runes? 80% of materials will be returned.");
        Register("未选择符文", "No runes selected");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    private static List<Rune> GetFilteredAndSortedRunes() {
        List<Rune> filtered = allRunes.Where(rune => {
            // Filter by star
            if (filterStar > 0 && rune.star != filterStar) {
                return false;
            }

            // Filter by main stat
            if (filterMainStat >= 0 && (int)rune.mainStat != filterMainStat) {
                return false;
            }

            // Filter by sub stat
            if (filterSubStat >= 0 && !rune.subStats.Any(s => (int)s == filterSubStat)) {
                return false;
            }

            return true;
        }).ToList();

        // Apply sorting
        if (sortMode == 1) {
            filtered = filtered.OrderBy(r => r.level).ToList();
        } else if (sortMode == 2) {
            filtered = filtered.OrderByDescending(r => r.level).ToList();
        }

        return filtered;
    }

    private static void OnFilterStarClick() {
        filterStar = (filterStar + 1) % 7;// 0=all, 1-5=star, 6=back to 0
        if (filterStar == 6) {
            filterStar = 0;
        }
        UpdateFilterButtonTexts();
        UpdateRuneListUI();
    }

    private static void OnFilterMainStatClick() {
        filterMainStat = (filterMainStat + 1) % 5;// -1=all, 0-3=stats
        if (filterMainStat == 4) {
            filterMainStat = -1;
        }
        UpdateFilterButtonTexts();
        UpdateRuneListUI();
    }

    private static void OnFilterSubStatClick() {
        filterSubStat = (filterSubStat + 1) % 5;// -1=all, 0-3=stats
        if (filterSubStat == 4) {
            filterSubStat = -1;
        }
        UpdateFilterButtonTexts();
        UpdateRuneListUI();
    }

    private static void OnSortClick() {
        sortMode = (sortMode + 1) % 3;// 0=none, 1=asc, 2=desc
        UpdateFilterButtonTexts();
        UpdateRuneListUI();
    }

    private static void OnResetFilterClick() {
        filterStar = 0;
        filterMainStat = -1;
        filterSubStat = -1;
        sortMode = 0;
        UpdateFilterButtonTexts();
        UpdateRuneListUI();
    }

    private static void OnSelectAllClick() {
        List<Rune> filteredRunes = GetFilteredAndSortedRunes();
        if (selectedRuneIds.Count == filteredRunes.Count) {
            selectedRuneIds.Clear();
        } else {
            selectedRuneIds.Clear();
            foreach (Rune rune in filteredRunes) {
                selectedRuneIds.Add(rune.id);
            }
        }
        UpdateRuneListUI();
    }

    private static void OnBatchDisassembleClick() {
        if (selectedRuneIds.Count == 0) {
            UIMessageBox.Show("提示".Translate(), "未选择符文".Translate(), "确定".Translate(), UIMessageBox.WARNING, null);
            return;
        }

        string message = string.Format("确认批量分解".Translate(), selectedRuneIds.Count);
        UIMessageBox.Show("批量分解".Translate(), message, "确定".Translate(), "取消".Translate(),
            UIMessageBox.QUESTION, () => {
                List<Rune> runesToRemove = allRunes.Where(r => selectedRuneIds.Contains(r.id)).ToList();
                foreach (Rune rune in runesToRemove) {
                    for (int i = 0; i < 5; i++) {
                        if (equippedRuneIds[i] == rune.id) {
                            equippedRuneIds[i] = 0;
                            break;
                        }
                    }
                    DeconstructRune(rune);
                }
                selectedRuneIds.Clear();
                _selectedRune = null;
                UpdateUI();
                UpdateDetailUI();
                UpdateRuneListUI();
            }, null);
    }

    private static void UpdateFilterButtonTexts() {
        if (filterStarButton != null) {
            string text = filterStar == 0 ? "全部".Translate() : $"{filterStar}{"星".Translate()}";
            filterStarButton.transform.Find("button-text").GetComponent<Text>().text = $"{"筛选星级".Translate()}: {text}";
        }
        if (filterMainStatButton != null) {
            string text = filterMainStat == -1
                ? "全部".Translate()
                : ((ERuneStatType)filterMainStat).ToString().Translate();
            filterMainStatButton.transform.Find("button-text").GetComponent<Text>().text =
                $"{"筛选主词条".Translate()}: {text}";
        }
        if (filterSubStatButton != null) {
            string text = filterSubStat == -1
                ? "全部".Translate()
                : ((ERuneStatType)filterSubStat).ToString().Translate();
            filterSubStatButton.transform.Find("button-text").GetComponent<Text>().text =
                $"{"筛选副词条".Translate()}: {text}";
        }
        if (sortButton != null) {
            string text = sortMode == 0 ? "无排序".Translate() :
                sortMode == 1 ? "等级升序".Translate() : "等级降序".Translate();
            sortButton.transform.Find("button-text").GetComponent<Text>().text = $"{"排序".Translate()}: {text}";
        }
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        tab = wnd.AddTab(trans, "符文系统");

        float x = 0f;
        float y = 18f;

        // Total Stats
        totalStatsText = wnd.AddText2(x, y, tab, "总加成：");
        totalStatsText.fontSize = 16;
        y += 60f;

        // Equipped Runes
        wnd.AddText2(x, y, tab, "已装备符文").fontSize = 16;
        y += 35f;
        for (int i = 0; i < 5; i++) {
            int index = i;
            (float px, float width) = GetPosition(i, 5);
            slotButtons[i] = wnd.AddButton(px, y, width - 10f, tab, "空槽位", 14, "slot-" + i, () => OnSlotClick(index));
            slotTexts[i] = wnd.AddText2(px, y + 30f, tab, "", 12);
            slotTexts[i].alignment = TextAnchor.MiddleCenter;
            slotTexts[i].rectTransform.sizeDelta = new Vector2(width - 10f, 20f);
        }
        y += 85f;

        // Filter and Sort Controls
        runeCountText = wnd.AddText2(x, y, tab, "持有符文");
        runeCountText.fontSize = 16;
        y += 35f;

        float filterY = y;
        filterStarButton = wnd.AddButton(x, filterY, 140f, tab, "筛选星级: 全部", 12, "filter-star", OnFilterStarClick);
        filterMainStatButton = wnd.AddButton(x + 150f, filterY, 140f, tab, "筛选主词条: 全部", 12, "filter-main",
            OnFilterMainStatClick);
        filterSubStatButton =
            wnd.AddButton(x + 300f, filterY, 140f, tab, "筛选副词条: 全部", 12, "filter-sub", OnFilterSubStatClick);
        sortButton = wnd.AddButton(x + 450f, filterY, 120f, tab, "排序: 无排序", 12, "sort-btn", OnSortClick);
        y += 40f;

        resetFilterButton = wnd.AddButton(x, y, 100f, tab, "重置筛选", 12, "reset-filter", OnResetFilterClick);
        selectAllButton = wnd.AddButton(x + 110f, y, 100f, tab, "全选", 12, "select-all", OnSelectAllClick);
        batchDisassembleButton =
            wnd.AddButton(x + 220f, y, 100f, tab, "批量分解", 12, "batch-disassemble", OnBatchDisassembleClick);
        y += 45f;

        // Rune Buttons Grid
        float buttonSize = 60f;
        float buttonSpacing = 20f;

        for (int i = 0; i < RuneButtonCount; i++) {
            int row = i / RuneButtonColumns;
            int col = i % RuneButtonColumns;
            float px = x + col * (buttonSize + buttonSpacing);
            float py = y + row * (buttonSize + buttonSpacing);

            // Create image button with essence icon (initially null)
            runeButtons[i] = wnd.AddImageButton(px, py, tab, null, "rune-btn-" + i)
                .WithSize(buttonSize, buttonSize);

            // Add checkbox for batch selection
            var checkboxObj = new GameObject("Checkbox");
            checkboxObj.transform.SetParent(runeButtons[i].transform, false);
            RectTransform checkboxRect = checkboxObj.AddComponent<RectTransform>();
            checkboxRect.anchorMin = new(0, 1);
            checkboxRect.anchorMax = new(0, 1);
            checkboxRect.pivot = new(0, 1);
            checkboxRect.anchoredPosition = new(3, -3);
            checkboxRect.sizeDelta = new(15, 15);

            runeCheckboxes[i] = checkboxObj.AddComponent<Toggle>();
            Image checkboxBg = checkboxObj.AddComponent<Image>();
            checkboxBg.color = Color.white;

            var checkmarkObj = new GameObject("Checkmark");
            checkmarkObj.transform.SetParent(checkboxObj.transform, false);
            RectTransform checkmarkRect = checkmarkObj.AddComponent<RectTransform>();
            checkmarkRect.anchorMin = Vector2.zero;
            checkmarkRect.anchorMax = Vector2.one;
            checkmarkRect.sizeDelta = Vector2.zero;
            Image checkmark = checkmarkObj.AddComponent<Image>();
            checkmark.color = Color.green;
            runeCheckboxes[i].graphic = checkmark;

            // Add level text
            runeLevelTexts[i] = wnd.AddText2(px, py + buttonSize - 20f, tab, "", 12);
            runeLevelTexts[i].alignment = TextAnchor.MiddleCenter;
            runeLevelTexts[i].rectTransform.sizeDelta = new(buttonSize, 20f);
        }

        y += RuneButtonRows * (buttonSize + buttonSpacing) + 10f;

        // Details Area
        wnd.AddText2(x, y, tab, "符文详情").fontSize = 16;
        y += 35f;
        runeDetailText = wnd.AddText2(x, y, tab, "请选择一个符文查看详情", 14);
        runeDetailText.rectTransform.sizeDelta = new Vector2(400, 180);
        runeDetailText.alignment = TextAnchor.UpperLeft;

        float bx = 420f;
        upgradeButton = wnd.AddButton(bx, y, 160f, tab, "强化", 16, "upgrade-btn", OnUpgradeClick);
        y += 45f;
        equipButton = wnd.AddButton(bx, y, 160f, tab, "装备", 16, "equip-btn", OnEquipClick);
        y += 45f;
        disassembleButton = wnd.AddButton(bx, y, 160f, tab, "分解", 16, "disassemble-btn", OnDisassembleClick);

        UpdateFilterButtonTexts();
        UpdateDetailUI();
        UpdateRuneListUI();
    }

    private static void OnSlotClick(int index) {
        if (index >= slotCount) {
            UIMessageBox.Show("提示".Translate(), "符文槽位未解锁".Translate(), "确定".Translate(), UIMessageBox.WARNING, null);
            return;
        }
        _selectedSlotIndex = index;
        long id = equippedRuneIds[index];
        _selectedRune = allRunes.FirstOrDefault(r => r.id == id);
        UpdateDetailUI();
    }

    private static void OnRuneButtonClick(int index) {
        List<Rune> filteredRunes = GetFilteredAndSortedRunes();
        if (index >= 0 && index < filteredRunes.Count) {
            _selectedRune = filteredRunes[index];
            _selectedSlotIndex = -1;
            UpdateDetailUI();
        }
    }

    private static void OnRuneCheckboxClick(int index) {
        List<Rune> filteredRunes = GetFilteredAndSortedRunes();
        if (index >= 0 && index < filteredRunes.Count) {
            long runeId = filteredRunes[index].id;
            if (selectedRuneIds.Contains(runeId)) {
                selectedRuneIds.Remove(runeId);
            } else {
                selectedRuneIds.Add(runeId);
            }
            UpdateRuneListUI();
        }
    }

    private static void OnRuneClick(Rune rune) {
        _selectedRune = rune;
        _selectedSlotIndex = -1;
        UpdateDetailUI();
    }

    private static void OnUpgradeClick() {
        if (_selectedRune == null) return;
        if (_selectedRune.level >= _selectedRune.MaxLevel) {
            UIMessageBox.Show("提示".Translate(), "符文等级上限".Translate(), "确定".Translate(), UIMessageBox.INFO, null);
            return;
        }
        long cost = GetUpgradeCost(_selectedRune.level, _selectedRune.star);
        int essenceId = _selectedRune.GetEssenceId();
        if (GetItemTotalCount(essenceId) < cost) {
            UIMessageBox.Show("提示".Translate(), "精华不足".Translate(), "确定".Translate(), UIMessageBox.WARNING, null);
            return;
        }

        if (UpgradeRune(_selectedRune)) {
            UpdateDetailUI();
            UpdateUI();
            UpdateRuneListUI();
        }
    }

    private static void OnEquipClick() {
        if (_selectedRune == null) return;

        bool isEquipped = false;
        int currentSlot = -1;
        for (int i = 0; i < 5; i++) {
            if (equippedRuneIds[i] == _selectedRune.id) {
                isEquipped = true;
                currentSlot = i;
                break;
            }
        }

        if (isEquipped) {
            equippedRuneIds[currentSlot] = 0;
        } else {
            int targetSlot = -1;
            if (_selectedSlotIndex >= 0 && _selectedSlotIndex < slotCount) {
                targetSlot = _selectedSlotIndex;
            } else {
                for (int i = 0; i < slotCount; i++) {
                    if (equippedRuneIds[i] == 0) {
                        targetSlot = i;
                        break;
                    }
                }
            }

            if (targetSlot != -1) {
                equippedRuneIds[targetSlot] = _selectedRune.id;
            } else {
                UIMessageBox.Show("提示".Translate(), "没有空余槽位".Translate(), "确定".Translate(), UIMessageBox.WARNING, null);
            }
        }
        UpdateUI();
        UpdateDetailUI();
    }

    private static void OnDisassembleClick() {
        if (_selectedRune == null) return;

        UIMessageBox.Show("分解".Translate(), "确认分解符文".Translate(), "确定".Translate(), "取消".Translate(),
            UIMessageBox.QUESTION, () => {
                for (int i = 0; i < 5; i++) {
                    if (equippedRuneIds[i] == _selectedRune.id) {
                        equippedRuneIds[i] = 0;
                        break;
                    }
                }
                DeconstructRune(_selectedRune);
                _selectedRune = null;
                UpdateUI();
                UpdateDetailUI();
                UpdateRuneListUI();
            }, null);
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) return;

        GetTotalStats(out float speed, out float power, out float productivity, out float yield);
        totalStatsText.text = $"{"总加成".Translate()}： "
                              + $"{"制作速度".Translate()} {speed.FormatPWithSymbol()}    "
                              + $"{"产品产能".Translate()} {productivity.FormatPWithSymbol()}    "
                              + $"{"电力消耗".Translate()} {power.FormatPWithSymbol()}    "
                              + $"{"增产效果".Translate()} {yield.FormatPWithSymbol()}";

        for (int i = 0; i < 5; i++) {
            if (i >= slotCount) {
                slotButtons[i].button.interactable = false;
                slotButtons[i].transform.Find("button-text").GetComponent<Text>().text = "符文槽位未解锁".Translate();
                slotButtons[i].transform.Find("button-text").GetComponent<Text>().color = Color.red;
                slotTexts[i].text = "";
            } else {
                slotButtons[i].button.interactable = true;
                long id = equippedRuneIds[i];
                if (id == 0) {
                    slotButtons[i].transform.Find("button-text").GetComponent<Text>().text = "空槽位".Translate();
                    slotButtons[i].transform.Find("button-text").GetComponent<Text>().color =
                        new Color(1f, 1f, 1f, 0.4f);
                    slotTexts[i].text = "";
                } else {
                    Rune rune = allRunes.FirstOrDefault(r => r.id == id);
                    if (rune != null) {
                        slotButtons[i].transform.Find("button-text").GetComponent<Text>().text =
                            $"{rune.star}{"星".Translate()} Lv.{rune.level}";
                        slotButtons[i].transform.Find("button-text").GetComponent<Text>().color =
                            GetColorByStar(rune.star);
                        slotTexts[i].text = rune.mainStat.ToString().Translate();
                    } else {
                        equippedRuneIds[i] = 0;
                    }
                }
            }
        }
        UpdateRuneListUI();
        // UpdateDetailUI();
    }

    private static void UpdateRuneListUI() {
        if (runeButtons == null || runeButtons.Length == 0) {
            return;
        }

        // Get filtered and sorted runes
        List<Rune> filteredRunes = GetFilteredAndSortedRunes();
        int runeCount = filteredRunes.Count;

        // Update rune count text
        if (runeCountText != null) {
            runeCountText.text = $"{"持有符文".Translate()} ({runeCount}/{allRunes.Count})";
        }

        // Update each button
        for (int i = 0; i < RuneButtonCount; i++) {
            if (i < runeCount) {
                // Show button with rune data
                Rune rune = filteredRunes[i];

                // Set button visible
                runeButtons[i].gameObject.SetActive(true);

                // Set essence icon based on main stat
                int essenceId = rune.GetEssenceId();
                runeButtons[i].Proto = LDB.items.Select(essenceId);

                // Update button click handler
                int index = i;
                runeButtons[i].uiButton.button.onClick.RemoveAllListeners();
                runeButtons[i].uiButton.button.onClick.AddListener(() => OnRuneButtonClick(index));

                // Update level text
                runeLevelTexts[i].text = $"{rune.star}{"星".Translate()} Lv.{rune.level}";
                runeLevelTexts[i].color = GetColorByStar(rune.star);
                runeLevelTexts[i].gameObject.SetActive(true);

                // Update checkbox
                runeCheckboxes[i].onValueChanged.RemoveAllListeners();
                runeCheckboxes[i].isOn = selectedRuneIds.Contains(rune.id);
                runeCheckboxes[i].onValueChanged.AddListener(isOn => OnRuneCheckboxClick(index));
                runeCheckboxes[i].gameObject.SetActive(true);
            } else {
                // Hide unused button
                runeButtons[i].gameObject.SetActive(false);
                runeLevelTexts[i].gameObject.SetActive(false);
                runeCheckboxes[i].gameObject.SetActive(false);
            }
        }

        // Update select all button text
        if (selectAllButton != null) {
            bool allSelected = runeCount > 0 && selectedRuneIds.Count == runeCount;
            selectAllButton.transform.Find("button-text").GetComponent<Text>().text =
                allSelected ? "取消全选".Translate() : "全选".Translate();
        }
    }


    private static void UpdateDetailUI() {
        if (_selectedRune == null) {
            runeDetailText.text = "请选择一个符文查看详情".Translate();
            upgradeButton.gameObject.SetActive(false);
            equipButton.gameObject.SetActive(false);
            disassembleButton.gameObject.SetActive(false);
            return;
        }

        upgradeButton.gameObject.SetActive(true);
        equipButton.gameObject.SetActive(true);
        disassembleButton.gameObject.SetActive(true);

        StringBuilder sb = new();
        sb.AppendLine($"{_selectedRune.star}{"星符文".Translate()} Lv.{_selectedRune.level}/{_selectedRune.MaxLevel}");
        sb.AppendLine($"{"主词条".Translate()}:");
        sb.AppendLine($"  {GetStatDescription(_selectedRune.mainStat, _selectedRune.level + 1)}");
        sb.AppendLine($"{"副词条".Translate()}:");
        if (_selectedRune.subStats.Count == 0) {
            sb.AppendLine($"  - {"无".Translate()}");
        } else {
            for (int i = 0; i < _selectedRune.subStats.Count; i++) {
                sb.AppendLine($"  - {GetStatDescription(_selectedRune.subStats[i], _selectedRune.subStatRolls[i])}");
            }
        }

        long cost = GetUpgradeCost(_selectedRune.level, _selectedRune.star);
        upgradeButton.transform.Find("button-text").GetComponent<Text>().text =
            _selectedRune.level >= _selectedRune.MaxLevel ? "符文等级上限".Translate() : $"{"强化".Translate()} ({cost})";

        bool isEquipped = false;
        for (int i = 0; i < 5; i++) {
            if (equippedRuneIds[i] == _selectedRune.id) {
                isEquipped = true;
                break;
            }
        }
        equipButton.transform.Find("button-text").GetComponent<Text>().text =
            isEquipped ? "卸下".Translate() : "装备".Translate();

        runeDetailText.text = sb.ToString();
    }

    private static string GetStatDescription(ERuneStatType stat, float multiplier) {
        return stat switch {
            ERuneStatType.Speed =>
                $"{"制作速度".Translate()} {(multiplier * 0.5f).FormatPWithSymbol()}, {"电力消耗".Translate()} {(multiplier * 0.7f).FormatPWithSymbol()}",
            ERuneStatType.Productivity =>
                $"{"产品产能".Translate()} {(multiplier * 0.1f).FormatPWithSymbol()}, {"电力消耗".Translate()} {(multiplier * 0.8f).FormatPWithSymbol()}, {"制作速度".Translate()} {(multiplier * 0.15f).FormatPWithSymbol()}",
            ERuneStatType.EnergySaving =>
                $"{"电力消耗".Translate()} {(multiplier * 0.5f).FormatPWithSymbol()}",
            ERuneStatType.Proliferator =>
                $"{"增产效果".Translate()} {(multiplier * 0.05f).FormatPWithSymbol()}",
            _ => ""
        };
    }

    private static Color GetColorByStar(int star) {
        return star switch {
            5 => Gold,
            4 => Purple,
            3 => Blue,
            2 => Green,
            _ => White,
        };
    }

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
    }

    public static void IntoOtherSave() {
        _selectedRune = null;
        _selectedSlotIndex = -1;
        selectedRuneIds.Clear();
        filterStar = 0;
        filterMainStat = -1;
        filterSubStat = -1;
        sortMode = 0;
    }
}
