using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;

namespace FE.UI.View;

public static class TabAchievement {
    public static RectTransform _windowTrans;

    public static void LoadConfig(ConfigFile configFile) {

    }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
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

    public static void UpdateUI() {

    }
}
