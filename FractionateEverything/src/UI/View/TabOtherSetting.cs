using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.UI.Components;
using UnityEngine;

namespace FE.UI.View;

public static class TabOtherSetting {
    public static RectTransform _windowTrans;

    public static ConfigEntry<bool> EnableGod;
    public static ConfigEntry<float> MultiRateEntry;

    public static void LoadConfig(ConfigFile configFile) {
        EnableGod = configFile.Bind("TabOtherSetting", "EnableGod", false, "启用上帝模式。");
        MultiRateEntry = configFile.Bind("TabOtherSetting", "MultiRate", 1.0f, "加成倍数。");
    }

    private class MultiRateMapper() : MyWindow.RangeValueMapper<float>(1, 100) {
        public override float IndexToValue(int index) => index / 10.0f;
        public override int ValueToIndex(float value) => Mathf.RoundToInt(value * 10);
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        float x;
        float y;
        wnd.AddTabGroup(trans, "其他", "tab-group-fe4");
        {
            var tab = wnd.AddTab(trans, "其他设置");
            x = 0f;
            y = 10f;
            var checkBox = wnd.AddCheckBox(x, y, tab, EnableGod, "启用上帝模式(未实装)");
            wnd.AddTipsButton2(checkBox.Width + 5f, y + 6f, tab, "启用上帝模式", "可以大幅提升经验获取速度。", "");
            y += 36f;
            wnd.AddButton(x, y, 200, tab, "解锁所有配方", 16, "button-unlock-all-recipes",
                RecipeManager.UnlockAll);
            y += 30f;
            var txt = wnd.AddText2(x, y, tab, "处理倍率(未实装)", 15, "text-multi-rate");
            wnd.AddSlider(x + txt.preferredWidth + 5f, y + 6f, tab,
                MultiRateEntry, new MultiRateMapper(), "G", 160f);
            y += 30f;
        }
    }

    public static void UpdateUI() { }
}
