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
    private static RectTransform tab;

    private static UIButton[] slotButtons = new UIButton[5];
    private static Text[] slotTexts = new Text[5];

    private static UIButton[] runeListButtons = new UIButton[24];
    private static Text[] runeListTexts = new Text[24];

    private static Text runeDetailText;
    private static Text totalStatsText;
    private static UIButton upgradeButton;
    private static UIButton equipButton;
    private static UIButton disassembleButton;

    private static Rune _selectedRune;
    private static int _selectedSlotIndex = -1;

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
        Register("Speed", "Speed", "速度");
        Register("Productivity", "Productivity", "产能");
        Register("EnergySaving", "Energy Saving", "节能");
        Register("Yield", "Yield", "增产");
        Register("词条", "Stat");
        Register("主词条", "Main Stat");
        Register("副词条", "Sub Stat");
        Register("没有空余槽位", "No empty slots");
        Register("星符文", "-Star Rune", "星符文");
    }

    public static void LoadConfig(ConfigFile configFile) { }

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

        // Owned Runes Grid
        wnd.AddText2(x, y, tab, "持有符文").fontSize = 16;
        y += 35f;
        for (int i = 0; i < 24; i++) {
            int index = i;
            int row = i / 6;
            int col = i % 6;
            (float px, float width) = GetPosition(col, 6);
            float py = y + row * 65f;
            runeListButtons[i] =
                wnd.AddButton(px, py, width - 8f, tab, "---", 12, "rune-" + i, () => OnRuneClick(index));
            runeListTexts[i] = wnd.AddText2(px, py + 28f, tab, "", 11);
            runeListTexts[i].alignment = TextAnchor.MiddleCenter;
            runeListTexts[i].rectTransform.sizeDelta = new Vector2(width - 8f, 20f);
        }
        y += 4 * 65f + 20f;

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

        UpdateDetailUI();
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

    private static void OnRuneClick(int index) {
        if (index >= allRunes.Count) return;
        _selectedRune = allRunes[index];
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
            }, null);
    }

    public static void UpdateUI() {
        if (tab == null || !tab.gameObject.activeSelf) return;

        GetTotalStats(out float speed, out float power, out float productivity, out float yield);
        totalStatsText.text = $"{"总加成".Translate()}： "
                              + $"{"速度".Translate()} +{speed.FormatP()}    "
                              + $"{"产能".Translate()} +{productivity.FormatP()}    "
                              + $"{"节能".Translate()} +{power.FormatP()}    "
                              + $"{"增产".Translate()} +{yield.FormatP()}";

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

        for (int i = 0; i < 24; i++) {
            if (i >= allRunes.Count) {
                runeListButtons[i].gameObject.SetActive(false);
                runeListTexts[i].text = "";
            } else {
                runeListButtons[i].gameObject.SetActive(true);
                Rune rune = allRunes[i];
                runeListButtons[i].transform.Find("button-text").GetComponent<Text>().text =
                    $"{rune.star}{"星".Translate()} Lv.{rune.level}";
                runeListButtons[i].transform.Find("button-text").GetComponent<Text>().color = GetColorByStar(rune.star);
                runeListTexts[i].text = rune.mainStat.ToString().Translate();
            }
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
                $"{"速度".Translate()} +{(multiplier * 0.5f).FormatP()}, {"电力消耗".Translate()} +{(multiplier * 0.7f).FormatP()}",
            ERuneStatType.Productivity =>
                $"{"产能".Translate()} +{(multiplier * 0.1f).FormatP()}, {"电力消耗".Translate()} +{(multiplier * 0.8f).FormatP()}, {"速度".Translate()} -{(multiplier * 0.15f).FormatP()}",
            ERuneStatType.EnergySaving => $"{"电力消耗".Translate()} -{(multiplier * 0.5f).FormatP()}",
            ERuneStatType.Yield => $"{"增产".Translate()} +{(multiplier * 0.05f).FormatP()}",
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
    }
}
