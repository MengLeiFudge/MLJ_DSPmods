using System;
using System.IO;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;

namespace FE.UI.View;

public static class TabOtherSetting {
    public static RectTransform _windowTrans;

    // public static ConfigEntry<bool> EnableGod;
    public static ConfigEntry<float> ExpMultiRateEntry;
    public static Text textExpMultiRate;
    public static MySlider sliderExpMultiRate;

    public static void LoadConfig(ConfigFile configFile) {
        // EnableGod = configFile.Bind("TabOtherSetting", "EnableGod", false, "启用上帝模式。");
        ExpMultiRateEntry = configFile.Bind("TabOtherSetting", "ExpMultiRate", 1.0f, "经验获取倍率");
    }

    private class MultiRateMapper() : MyWindow.RangeValueMapper<float>(0, 40) {
        public override float IndexToValue(int index) => (float)Math.Pow(10, (index - 10) / 10.0);
        public override int ValueToIndex(float value) => (int)(Math.Log10(value) * 10.0 + 10);
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        float x;
        float y;
        wnd.AddTabGroup(trans, "其他", "tab-group-fe4");
        {
            var tab = wnd.AddTab(trans, "其他设置");
            x = 0f;
            y = 10f;
            // var checkBox = wnd.AddCheckBox(x, y, tab, EnableGod, "启用上帝模式(未实装)");
            // wnd.AddTipsButton2(checkBox.Width + 5f, y + 6f, tab, "启用上帝模式", "可以大幅提升经验获取速度。", "");
            // y += 36f;
            wnd.AddButton(x, y, 200, tab, "解锁所有配方", 16, "button-unlock-all-recipes",
                RecipeManager.UnlockAll);
            y += 30f;
            textExpMultiRate = wnd.AddText2(x, y, tab, "经验获取倍率", 15, "text-multi-rate");
            sliderExpMultiRate = wnd.AddSlider(x + textExpMultiRate.preferredWidth + 5f, y + 6f, tab,
                ExpMultiRateEntry, new MultiRateMapper(), "0.#", 160f);
            y += 30f;
        }
    }

    public static void UpdateUI() {
        textExpMultiRate.enabled = GameMain.sandboxToolsEnabled;
        sliderExpMultiRate.enabled = GameMain.sandboxToolsEnabled;
        sliderExpMultiRate.slider.enabled = GameMain.sandboxToolsEnabled;
        sliderExpMultiRate.labelText.enabled = GameMain.sandboxToolsEnabled;
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
