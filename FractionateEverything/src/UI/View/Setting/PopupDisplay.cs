using System.IO;
using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;
using static FE.Logic.Recipe.ERecipeExtension;
using static FE.Utils.Utils;

namespace FE.UI.View.Setting;

public static class PopupDisplay {
    private static RectTransform window;
    private static RectTransform tab;

    public static void AddTranslations() {
        Register("弹窗显示", "Popup Display");
    }

    public static void LoadConfig(ConfigFile configFile) { }

    // private static ConfigEntry<int> RecipeTypeEntry;
    // public static void LoadConfig(ConfigFile configFile) {
    //     RecipeTypeEntry = configFile.Bind("TabRecipeAndBuilding", "Recipe Type", 0, "想要查看的配方类型。");
    //     if (RecipeTypeEntry.Value < 0 || RecipeTypeEntry.Value >= RecipeTypes.Length) {
    //         RecipeTypeEntry.Value = 0;
    //     }
    // }

    public static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        window = trans;
        tab = wnd.AddTab(trans, "弹窗显示");
        float x = 0f;
        float y = 20f;

        // for (int x = 0; x < 1000; x += 100) {
        //     for (int y = 0; y < 1000; y += 50) {
        //         if (x == 100 && y == 100) {
        //             wnd.AddButton(x, y, 50, tab, "test");
        //         } else if (x == 200 && y == 200) {
        //             wnd.AddComboBox(x, y, tab, "test").WithItems(RecipeTypeShortNames).WithSize(150f, 0f)
        //                 .WithConfigEntry(RecipeTypeEntry);
        //         } else if (x == 300 && y == 300) {
        //             wnd.AddImageButton(x, y, tab);
        //         } else if (x == 400 && y == 400) {
        //             wnd.AddText2(x, y, tab, "test");
        //         } else if (x == 500 && y == 500) {
        //             wnd.AddTipsButton2(x, y, tab, "111", "222");
        //         } else {
        //             wnd.AddButton(x, y, 50, tab, x + "," + y, topLeft: true);
        //         }
        //     }
        // }
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
