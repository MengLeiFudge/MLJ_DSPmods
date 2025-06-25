using System.IO;
using BepInEx.Configuration;
using FE.UI.Components;
using UnityEngine;

namespace FE.UI.View;

public static class MainWindow {
    private static RectTransform _windowTrans;

    public static void LoadConfig(ConfigFile configFile) {
        TabRecipeAndBuilding.LoadConfig(configFile);
        TabRaffle.LoadConfig(configFile);
        TabShop.LoadConfig(configFile);
        TabTask.LoadConfig(configFile);
        TabAchievement.LoadConfig(configFile);
        TabOtherSetting.LoadConfig(configFile);
    }

    public static void Init() {
        MyConfigWindow.OnUICreated += CreateUI;
        MyConfigWindow.OnUpdateUI += UpdateUI;
    }

    private static void CreateUI(MyConfigWindow wnd, RectTransform trans) {
        _windowTrans = trans;
        TabRecipeAndBuilding.CreateUI(wnd, trans);
        TabRaffle.CreateUI(wnd, trans);
        TabShop.CreateUI(wnd, trans);
        TabTask.CreateUI(wnd, trans);
        TabAchievement.CreateUI(wnd, trans);
        TabOtherSetting.CreateUI(wnd, trans);
    }

    private static void UpdateUI() {
        TabRecipeAndBuilding.UpdateUI();
        TabRaffle.UpdateUI();
        TabShop.UpdateUI();
        TabTask.UpdateUI();
        TabAchievement.UpdateUI();
        TabOtherSetting.UpdateUI();
    }

    #region IModCanSave

    public static void Import(BinaryReader r) {
        int version = r.ReadInt32();
        TabRecipeAndBuilding.Import(r);
        TabRaffle.Import(r);
        TabShop.Import(r);
        TabTask.Import(r);
        TabAchievement.Import(r);
        TabOtherSetting.Import(r);
    }

    public static void Export(BinaryWriter w) {
        w.Write(1);
        TabRecipeAndBuilding.Export(w);
        TabRaffle.Export(w);
        TabShop.Export(w);
        TabTask.Export(w);
        TabAchievement.Export(w);
        TabOtherSetting.Export(w);
    }

    public static void IntoOtherSave() {
        TabRecipeAndBuilding.IntoOtherSave();
        TabRaffle.IntoOtherSave();
        TabShop.IntoOtherSave();
        TabTask.IntoOtherSave();
        TabAchievement.IntoOtherSave();
        TabOtherSetting.IntoOtherSave();
    }

    #endregion
}
