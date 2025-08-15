using System;
using System.IO;
using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;

namespace FE.UI.View.Setting;

public static class VipFeatures {
    public static RectTransform _windowTrans;

    public static void LoadConfig(ConfigFile configFile) { }

    private class MultiRateMapper() : MyWindow.RangeValueMapper<float>(0, 40) {
        public override float IndexToValue(int index) => (float)Math.Pow(10, (index - 10) / 10.0);
        public override int ValueToIndex(float value) => (int)(Math.Log10(value) * 10.0 + 10);
    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        _windowTrans = trans;
        var tab = wnd.AddTab(trans, "VIP功能");
        float x = 0f;
        float y = 10f;
    }

    public static void UpdateUI() { }

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
