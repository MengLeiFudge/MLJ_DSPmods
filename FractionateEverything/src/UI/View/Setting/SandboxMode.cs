using System;
using System.IO;
using BepInEx.Configuration;
using FE.Logic.Manager;
using FE.UI.Components;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.UI.View.Setting;

public static class SandboxMode {
    private static RectTransform window;
    private static RectTransform tab;

    private static ConfigEntry<float> ExpMultiRateEntry;
    public static float ExpMultiRate => GameMain.sandboxToolsEnabled ? ExpMultiRateEntry.Value : 1;

    private class MultiRateMapper() : MyWindow.RangeValueMapper<float>(0, 40) {
        public override float IndexToValue(int index) => (float)Math.Pow(10, (index - 10) / 10.0);
        public override int ValueToIndex(float value) => (int)(Math.Log10(value) * 10.0 + 10);
    }

    public static void AddTranslations() {
        Register("沙盒模式", "Sandbox Mode");

        Register("解锁所有分馏配方", "Unlock all fractionation recipes");
        Register("经验获取倍率", "Experience gain multiplier");
        Register("经验获取倍率说明",
            "Adjust the speed at which recipe experience is gained by processing items.",
            "调整通过处理物品获取配方经验的速度。");
    }

    public static void LoadConfig(ConfigFile configFile) {
        ExpMultiRateEntry = configFile.Bind("TabSetting", "ExpMultiRate", 1.0f, "经验获取倍率");
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "沙盒模式");
        float x = 0f;
        float y = 18f;
        wnd.AddButton(0, 2, y, tab, "解锁所有分馏配方", 16, "button-unlock-all-recipes",
            RecipeManager.UnlockAllFracRecipes);
        y += 36f;
        var txt = wnd.AddText2(x, y, tab, "经验获取倍率", 15, "text-exp-multi-rate");
        wnd.AddSlider(x + 5 + txt.preferredWidth, y, tab,
            ExpMultiRateEntry, new MultiRateMapper(), "0.#", 200f);
        wnd.AddTipsButton2(x + 5 + txt.preferredWidth + 200 + 5, y, tab,
            "经验获取倍率", "经验获取倍率说明");
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
        //enabled -> 启用/禁用    gameObject.SetActive -> 显示/隐藏
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
