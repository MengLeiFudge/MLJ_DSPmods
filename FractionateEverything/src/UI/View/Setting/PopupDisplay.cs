using System.IO;
using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.UI.View.Setting;

public static class PopupDisplay {
    private static RectTransform window;
    private static RectTransform tab;

    public static void AddTranslations() {
        Register("弹窗显示", "Popup Display");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "弹窗显示");
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
