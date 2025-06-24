using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;
using static FE.Utils.ProtoID;
using static FE.Utils.PackageUtils;

namespace FE.UI.View;

public static class TabShop {
    public static RectTransform _windowTrans;

    public static void LoadConfig(ConfigFile configFile) { }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        float x;
        float y;
        wnd.AddTabGroup(trans, "商店", "tab-group-fe3");
        {
            var tab = wnd.AddTab(trans, "矩阵商店");
            x = 0f;
            y = 10f;
            wnd.AddButton(x, y, 200, tab, "200蓝糖兑换1交互塔", 16, "btn-blue1", () => {
                ExchangeItemsWithQuestion(I电磁矩阵, 200, IFE交互塔, 1);
            });
        }
        {
            var tab = wnd.AddTab(trans, "黑雾商店");
            x = 0f;
            y = 10f;
        }
    }

    public static void UpdateUI() { }
}
