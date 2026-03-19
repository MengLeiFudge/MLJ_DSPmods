using System.IO;
using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.UI.View.Statistic;

public static class FracStatistic {
    public static void AddTranslations() {
        Register("分馏统计", "Frac Statistic");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        // window = trans;
        // tab = wnd.AddTab(trans, "分馏统计");
        // float x = 0f;
        // float y = 18f;
    }

    public static void UpdateUI() {
        // if (!tab.gameObject.activeSelf) {
        //     return;
        // }
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        r.ReadBlocks();
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks();
    }

    public static void IntoOtherSave() { }

    #endregion
}
