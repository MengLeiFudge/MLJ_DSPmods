using System;
using System.IO;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.UI.Components;
using UnityEngine;
using UnityEngine.UI;
using static FE.Utils.Utils;

namespace FE.UI.View.Setting;

public static class SandboxMode {
    private static RectTransform window;
    private static RectTransform tab;

    private static UIButton btnUnlockAll;
    private static ConfigEntry<float> ExpMultiRateEntry;
    public static float ExpMultiRate { get; private set; }
    private static Text textExpMultiRate;
    private static MySlider sliderExpMultiRate;

    public static void AddTranslations() {
        Register("沙盒模式", "Sandbox Mode");
    }

    public static void LoadConfig(ConfigFile configFile) {
        ExpMultiRateEntry = configFile.Bind("TabSetting", "ExpMultiRate", 1.0f, "经验获取倍率");
        ExpMultiRate = ExpMultiRateEntry.Value;
    }

    private class MultiRateMapper() : MyWindow.RangeValueMapper<float>(0, 40) {
        public override float IndexToValue(int index) => (float)Math.Pow(10, (index - 10) / 10.0);
        public override int ValueToIndex(float value) => (int)(Math.Log10(value) * 10.0 + 10);
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "沙盒模式");
        float x = 0f;
        float y = 18f;
        btnUnlockAll = wnd.AddButton(0, 2, y, tab, "解锁所有分馏配方", 16, "button-unlock-all-recipes",
            RecipeManager.UnlockAllFracRecipes);
        y += 36f;
        textExpMultiRate = wnd.AddText2(x, y, tab, "经验获取倍率", 15, "text-exp-multi-rate");
        sliderExpMultiRate = wnd.AddSlider(x + textExpMultiRate.preferredWidth + 5, y, tab,
            ExpMultiRateEntry, new MultiRateMapper(), "0.#", 200f);
        wnd.AddTipsButton2(x + textExpMultiRate.preferredWidth + 5 + 200 + 5, y, tab,
            "经验获取倍率", "调整经验获取的速度，便于调试使用。\n仅在沙盒模式下生效。", "");
        y += 36f;
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        //enabled -> 启用/禁用    gameObject.SetActive -> 显示/隐藏
        bool sandboxMode = GameMain.sandboxToolsEnabled;
        btnUnlockAll.enabled = sandboxMode;
        btnUnlockAll.button.enabled = sandboxMode;
        ExpMultiRate = sandboxMode ? ExpMultiRateEntry.Value : 1;
        sliderExpMultiRate.slider.enabled = sandboxMode;
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
