using System.IO;
using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;

namespace FE.UI.View;

public static class TabTask {
    public static RectTransform _windowTrans;

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        _windowTrans = trans;
        float x;
        float y;
        wnd.AddTabGroup(trans, "任务", "tab-group-fe4");
        {
            var tab = wnd.AddTab(trans, "所有任务");
            x = 0f;
            y = 10f;
        }
        {
            var tab = wnd.AddTab(trans, "标记任务");
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
