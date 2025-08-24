using System;
using System.IO;
using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;
using static FE.Utils.Utils;

namespace FE.UI.View.Setting;

public static class VipFeatures {
    private static RectTransform window;
    private static RectTransform tab;

    public static int vipLevel = 0;
    public static int vipFreeCount => (vipLevel + 2) / 3 + 2;//todo: 去除结尾+2
    public static float vipDiscount => 1.0f - 0.05f * Math.Min(10, vipLevel);

    public static void AddTranslations() {
        Register("VIP功能", "VIP Features");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "VIP功能");
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
