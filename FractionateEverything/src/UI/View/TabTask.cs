using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;

namespace FE.UI.View;

public static class TabTask {
    public static RectTransform _windowTrans;

    public static void LoadConfig(ConfigFile configFile) {

    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        wnd.AddTabGroup(trans, "任务", "tab-group-fe4");
        {

        }
    }

    public static void UpdateUI() {

    }
}
