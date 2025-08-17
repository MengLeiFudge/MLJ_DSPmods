using System.IO;
using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.UI.View.ProgressSystem;

public static class Achievements {
    private static RectTransform window;
    private static RectTransform tab;

    public static void AddTranslations() {
        Register("成就详情", "Achievements");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "成就详情");
        float x = 0f;
        float y = 18f;
    }

    public static void UpdateUI() {
        if (!tab.gameObject.activeSelf) {
            return;
        }
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
