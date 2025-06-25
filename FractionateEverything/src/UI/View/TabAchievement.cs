using System.IO;
using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;

namespace FE.UI.View;

public static class TabAchievement {
    public static RectTransform _windowTrans;

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        _windowTrans = trans;
        float x;
        float y;
        wnd.AddTabGroup(trans, "成就", "tab-group-fe4");
        {
            var tab = wnd.AddTab(trans, "成就详情");
            x = 0f;
            y = 10f;
        }
        {
            var tab = wnd.AddTab(trans, "开发日记");
            x = 0f;
            y = 10f;
        }
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
