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
}
