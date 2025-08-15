using System.IO;
using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;

namespace FE.UI.View.GetItemRecipe;

public static class SelectedRaffle {
    public static RectTransform _windowTrans;

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        _windowTrans = trans;
        var tab = wnd.AddTab(trans, "自选抽奖");
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
