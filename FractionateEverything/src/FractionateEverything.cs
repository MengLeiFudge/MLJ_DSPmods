using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CommonAPI;
using CommonAPI.Systems;
using CommonAPI.Systems.ModLocalization;
using crecheng.DSPModSave;
using FractionateEverything.Compatibility;
using FractionateEverything.Logic;
using FractionateEverything.UI;
using FractionateEverything.UI.Components;
using FractionateEverything.UI.Shop;
using FractionateEverything.Utils;
using HarmonyLib;
using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using xiaoye97;
using static FractionateEverything.Utils.ProtoID;

namespace FractionateEverything;

[BepInPlugin(GUID, NAME, VERSION)]
[BepInDependency(CommonAPIPlugin.GUID)]
[CommonAPISubmoduleDependency(nameof(ProtoRegistry), nameof(TabSystem), nameof(LocalizationModule))]
[BepInDependency(CheckPlugins.GUID)]
public class FractionateEverything : BaseUnityPlugin, IModCanSave {
    public const string GUID = "com.menglei.dsp." + NAME;
    public const string NAME = "FractionateEverything";
#if DEBUG
    public const string VERSION = "2.0.0.05191023";
#else
    public const string VERSION = "2.0.0";
#endif
    public static int versionNumber;

    #region Logger

    private static ManualLogSource logger;
    public static void LogDebug(object data) => logger.LogDebug(data);
    public static void LogInfo(object data) => logger.LogInfo(data);
    public static void LogWarning(object data) => logger.LogWarning(data);
    public static void LogError(object data) => logger.LogError(data);
    public static void LogFatal(object data) => logger.LogFatal(data);

    #endregion

    #region Fields

    public const string Tech1134IconPath = "Icons/Tech/1134";
    // public static int tab分馏1;
    // public static int tab分馏2;
    public static string ModPath;
    public static ResourceData fracicons;
    public static readonly Harmony harmony = new(GUID);
    private static bool _finished;

    #endregion

    #region Config

    private static ConfigFile configFile;
    public static ConfigEntry<string> CurrentVersionEntry;
    /// <summary>
    /// 判断是否有版本更新，以便于弹窗提示MOD更新内容。
    /// 如果是第一次运行，CurrentVersionEntry.Value为""，与VERSION不同；
    /// 如果是版本更新，CurrentVersionEntry.Value为旧的版本号，与VERSION不同。
    /// </summary>
    public static bool isVersionChanged => CurrentVersionEntry.Value != VERSION;
    public static ConfigEntry<bool> DisableMessageBoxEntry;
    /// <summary>
    /// 是否在游戏加载时禁用提示信息。
    /// </summary>
    public static bool disableMessageBox => DisableMessageBoxEntry.Value;
    public static ConfigEntry<bool> AddedBlueprintsEntry;
    /// <summary>
    /// 是否已经添加过蓝图。
    /// </summary>
    public static bool addedBlueprints => AddedBlueprintsEntry.Value;
    public static ConfigEntry<int> IconVersionEntry;
    /// <summary>
    /// 分馏图标样式。
    /// </summary>
    public static int iconVersion => IconVersionEntry.Value;
    // public static ConfigEntry<bool> EnableDestroyEntry;
    // /// <summary>
    // /// 是否启用分馏配方中的损毁概率。
    // /// </summary>
    // public static bool enableDestroy => EnableDestroyEntry.Value;
    // public static ConfigEntry<bool> EnableFuelRodFracEntry;
    // /// <summary>
    // /// 是否启用燃料棒分馏。
    // /// </summary>
    // public static bool enableFuelRodFrac => EnableFuelRodFracEntry.Value;
    // public static ConfigEntry<bool> EnableMatrixFracEntry;
    // /// <summary>
    // /// 是否启用矩阵分馏。
    // /// </summary>
    // public static bool enableMatrixFrac => EnableMatrixFracEntry.Value;
    public static ConfigEntry<bool> EnableBuildingAsTrashEntry;
    /// <summary>
    /// 垃圾回收分馏塔能否输入建筑。
    /// </summary>
    public static bool enableBuildingAsTrash => EnableBuildingAsTrashEntry.Value;

    public static ConfigEntry<bool> SingleWindow;
    public static ConfigEntry<KeyCode> OpenWindowHotKey;
    public static ConfigEntry<KeyCode> SwitchWindowSizeHotKey;
    public static ConfigEntry<int> OpenWindowModifier;
    public static ConfigEntry<int> SwitchWindowModifier;

    public static ConfigEntry<bool> AutoCruiseEnabled;
    public static ConfigEntry<bool> AutoCruiseEnabled1;

    // UI相关ConfigEntry
    public static ConfigEntry<bool> EnableFractionateShop;
    public static ConfigEntry<bool> EnableFractionateWindow;
    public static ConfigEntry<bool> ShowFractionateNotification;
    public static ConfigEntry<bool> EnableExtendedUI;
    public static ConfigEntry<bool> EnableFractionateStats;
    public static ConfigEntry<bool> EnableRecipePreview;
    public static ConfigEntry<bool> EnableUIAnimations;
    public static ConfigEntry<bool> CompactUIMode;
    public static ConfigEntry<int> FractionateWindowSize;
    public static ConfigEntry<int> FractionateUITheme;
    public static ConfigEntry<bool> EnableFractionateItems;
    public static ConfigEntry<bool> EnableFractionateRecipes;
    public static ConfigEntry<bool> EnableFractionateUpgrades;

    // 商店和老虎机相关配置
    public static ConfigEntry<bool> EnableFractionateShopFeature;
    public static ConfigEntry<bool> EnableSlotMachineFractionator;
    public static ConfigEntry<bool> EnableRandomRecipeUnlock;
    public static ConfigEntry<bool> EnableSpecialRecipes;
    public static ConfigEntry<bool> EnableShopItemsPurchase;
    public static ConfigEntry<bool> EnableMatrixCurrency;
    public static ConfigEntry<bool> EnableUpgradeSystem;
    public static ConfigEntry<bool> EnableRecipeStar;
    public static ConfigEntry<int> MaxRecipeStarLevel;
    public static ConfigEntry<int> ShopMaxLevel;
    public static ConfigEntry<int> MaxSelectionChanges;
    public static ConfigEntry<float> SelfSelectEfficiency;
    public static ConfigEntry<float> UpgradeChance;
    public static ConfigEntry<float> RareRecipeChance;

    // 货币和碎片相关
    public static ConfigEntry<int> RedMatrixCurrency;
    public static ConfigEntry<int> BlueMatrixCurrency;
    public static ConfigEntry<int> YellowMatrixCurrency;
    public static ConfigEntry<int> PurpleMatrixCurrency;
    public static ConfigEntry<int> GreenMatrixCurrency;
    public static ConfigEntry<int> WhiteMatrixCurrency;
    public static ConfigEntry<int> FragmentAmount;
    public static ConfigEntry<int> RecipeShards;

    // 其他商店UI相关
    public static ConfigEntry<bool> EnableShopItemPreview;
    public static ConfigEntry<bool> ShowMatrixConversionRate;
    public static ConfigEntry<bool> EnableCraftingPreview;
    public static ConfigEntry<bool> CompactShopView;
    public static ConfigEntry<int> ShopUITheme;

    // 特殊功能
    public static ConfigEntry<bool> EnableAutoFractionate;
    public static ConfigEntry<bool> EnableBatchPurchase;
    public static ConfigEntry<bool> EnablePitySystem;
    public static ConfigEntry<bool> EnableRecipeHistory;
    public static ConfigEntry<bool> EnableFavoritesSystem;

    public void LoadConfig() {
        configFile = Config;

        CurrentVersionEntry = Config.Bind("config", "CurrentVersion", "",
            new ConfigDescription(
                "Current game version, used to control whether or not to show the update pop-up window.\n"
                + "当前游戏版本，用于控制是否显示更新弹窗。",
                new AcceptableStringValue(""), null));

        DisableMessageBoxEntry = Config.Bind("config", "DisableMessageBox", false,
            new ConfigDescription(
                "Don't show message when FractionateEverything is loaded.\n"
                + "禁用游戏加载完成后显示的万物分馏提示信息。",
                new AcceptableBoolValue(false), null));

        AddedBlueprintsEntry = Config.Bind("config", "AddedBlueprints", false,
            new ConfigDescription(
                "Indicates whether the blueprint has been added. Change this to false to re-add the blueprint.\n"
                + "指示是否添加过蓝图。该项改为false即可重新添加蓝图。",
                new AcceptableBoolValue(false), null));

        IconVersionEntry = Config.Bind("config", "IconVersion", 3,
            new ConfigDescription(
                "Which style of the fractionate recipe icon to use.\n"
                + "1 for original deuterium fractionate style, 2 for slanting line segmentation style, 3 for circular segmentation style.\n"
                + "使用哪个样式的分馏配方图标。\n"
                + "1表示原版重氢分馏样式，2表示斜线分割样式，3表示圆弧分割样式。",
                new AcceptableIntValue(3, 1, 3), null));

        // EnableDestroyEntry = Config.Bind("config", "EnableDestroy", true,
        //     new ConfigDescription(
        //         "Whether to enable the probability of destruction in fractionate recipes (recommended enable).\n"
        //         + "When enabled, if the fractionation recipe has a probability of destruction, there is a certain probability that the input item will disappear during fractionation.\n"
        //         + "是否启用分馏配方中的损毁概率（建议开启）。\n"
        //         + "启用情况下，如果分馏配方具有损毁概率，则分馏时会有一定概率导致原料直接消失。",
        //         new AcceptableBoolValue(true), null));
        //
        // EnableFuelRodFracEntry = Config.Bind("config", "EnableFuelRodFracEntry", false,
        //     new ConfigDescription(
        //         "Whether to enable fuel rod fractionation.\n"
        //         + "是否启用燃料棒分馏。",
        //         new AcceptableBoolValue(false), null));
        //
        // EnableMatrixFracEntry = Config.Bind("config", "EnableMatrixFracEntry", false,
        //     new ConfigDescription(
        //         "Whether to enable matrix fractionation (recommended disable).\n"
        //         + "是否启用矩阵分馏（建议关闭）。",
        //         new AcceptableBoolValue(false), null));

        EnableBuildingAsTrashEntry = Config.Bind("config", "EnableBuildingAsTrashEntry", false,
            new ConfigDescription(
                "Whether buildings can input into Trash Recycle Fractionator (recommended disable).\n"
                + "建筑能否输入垃圾回收分馏塔（建议关闭）。",
                new AcceptableBoolValue(false), null));

        SingleWindow = Config.Bind<bool>("config", "SingleWindow", true,
            "单窗口模式启用时，打开和关闭窗口都将使用打开窗口的那个快捷键。这会使你无法开启多个窗口，除非你同时按住Ctrl+Shift+Alt。  When single window mode is enabled, both opening and closing calculator window will use the same shortcut key (i.e. the open the window hot key). But if you want to open multiple windows in this mode, you must hold down Ctrl+Shift+Alt at the same time.");
        OpenWindowHotKey = Config.Bind<KeyCode>("config", "OpenWindowHotKey", KeyCode.R,
            "打开分馏窗口的快捷键。HotKey to open calculator window.");
        SwitchWindowSizeHotKey = Config.Bind<KeyCode>("config", "SwitchWindowSizeHotKey", KeyCode.Tab,
            "将计算器窗口展开或缩小的快捷键。HotKey to fold or unfold calculator window.");
        OpenWindowModifier =
            Config.Bind<int>("config", "OpenWindowHKModifier", 0, "byte shift = 1, ctrl = 2, alt = 4");
        SwitchWindowModifier =
            Config.Bind<int>("config", "SwitchWindowHKModifier", 0, "byte shift = 1, ctrl = 2, alt = 4");

        // 初始化UI相关配置
        EnableFractionateShop = Config.Bind<bool>("UI", "EnableFractionateShop", true,
            "是否启用分馏商店。Whether to enable fractionation shop.");
        EnableFractionateWindow = Config.Bind<bool>("UI", "EnableFractionateWindow", true,
            "是否启用分馏窗口。Whether to enable fractionation window.");
        ShowFractionateNotification = Config.Bind<bool>("UI", "ShowFractionateNotification", true,
            "是否显示分馏相关通知。Whether to show fractionation related notifications.");
        EnableExtendedUI = Config.Bind<bool>("UI", "EnableExtendedUI", true,
            "是否启用扩展UI功能。Whether to enable extended UI features.");
        EnableFractionateStats = Config.Bind<bool>("UI", "EnableFractionateStats", true,
            "是否启用分馏统计。Whether to enable fractionation statistics.");
        EnableRecipePreview = Config.Bind<bool>("UI", "EnableRecipePreview", true,
            "是否启用配方预览。Whether to enable recipe preview.");
        EnableUIAnimations = Config.Bind<bool>("UI", "EnableUIAnimations", true,
            "是否启用UI动画效果。Whether to enable UI animations.");
        CompactUIMode = Config.Bind<bool>("UI", "CompactUIMode", false,
            "是否启用紧凑UI模式。Whether to enable compact UI mode.");
        FractionateWindowSize = Config.Bind<int>("UI", "FractionateWindowSize", 1,
            "分馏窗口大小(1-3)。Fractionation window size(1-3).");
        FractionateUITheme = Config.Bind<int>("UI", "FractionateUITheme", 1,
            "分馏UI主题(1-5)。Fractionation UI theme(1-5).");
        EnableFractionateItems = Config.Bind<bool>("UI", "EnableFractionateItems", true,
            "是否启用分馏物品功能。Whether to enable fractionation items.");
        EnableFractionateRecipes = Config.Bind<bool>("UI", "EnableFractionateRecipes", true,
            "是否启用分馏配方功能。Whether to enable fractionation recipes.");
        EnableFractionateUpgrades = Config.Bind<bool>("UI", "EnableFractionateUpgrades", true,
            "是否启用分馏升级功能。Whether to enable fractionation upgrades.");

        // 初始化商店和老虎机相关配置
        EnableFractionateShopFeature = Config.Bind<bool>("Shop", "EnableFractionateShopFeature", true,
            "是否启用分馏商店功能。Whether to enable fractionation shop feature.");
        EnableSlotMachineFractionator = Config.Bind<bool>("Shop", "EnableSlotMachineFractionator", true,
            "是否启用老虎机分馏塔。Whether to enable slot machine fractionator.");
        EnableRandomRecipeUnlock = Config.Bind<bool>("Shop", "EnableRandomRecipeUnlock", true,
            "是否启用随机配方解锁。Whether to enable random recipe unlock.");
        EnableSpecialRecipes = Config.Bind<bool>("Shop", "EnableSpecialRecipes", true,
            "是否启用特殊配方。Whether to enable special recipes.");
        EnableShopItemsPurchase = Config.Bind<bool>("Shop", "EnableShopItemsPurchase", true,
            "是否允许在商店购买物品。Whether to allow purchasing items in shop.");
        EnableMatrixCurrency = Config.Bind<bool>("Shop", "EnableMatrixCurrency", true,
            "是否启用矩阵作为货币。Whether to enable matrix as currency.");
        EnableUpgradeSystem = Config.Bind<bool>("Shop", "EnableUpgradeSystem", true,
            "是否启用升级系统。Whether to enable upgrade system.");
        EnableRecipeStar = Config.Bind<bool>("Shop", "EnableRecipeStar", true,
            "是否启用配方星级。Whether to enable recipe star level.");
        MaxRecipeStarLevel = Config.Bind<int>("Shop", "MaxRecipeStarLevel", 5,
            "配方最大星级。Maximum star level for recipes.");
        ShopMaxLevel = Config.Bind<int>("Shop", "ShopMaxLevel", 6,
            "商店最大等级。Maximum level for shop.");
        MaxSelectionChanges = Config.Bind<int>("Shop", "MaxSelectionChanges", 3,
            "自选配方最大切换次数。Maximum times to change self-selected recipe.");
        SelfSelectEfficiency = Config.Bind<float>("Shop", "SelfSelectEfficiency", 0.5f,
            "自选配方效率系数。Efficiency coefficient for self-selected recipe.");
        UpgradeChance = Config.Bind<float>("Shop", "UpgradeChance", 0.05f,
            "升级概率。Upgrade chance.");
        RareRecipeChance = Config.Bind<float>("Shop", "RareRecipeChance", 0.01f,
            "稀有配方概率。Rare recipe chance.");

        // 初始化货币和碎片相关
        RedMatrixCurrency = Config.Bind<int>("Currency", "RedMatrixCurrency", 0,
            "红矩阵货币数量。Red matrix currency amount.");
        BlueMatrixCurrency = Config.Bind<int>("Currency", "BlueMatrixCurrency", 0,
            "蓝矩阵货币数量。Blue matrix currency amount.");
        YellowMatrixCurrency = Config.Bind<int>("Currency", "YellowMatrixCurrency", 0,
            "黄矩阵货币数量。Yellow matrix currency amount.");
        PurpleMatrixCurrency = Config.Bind<int>("Currency", "PurpleMatrixCurrency", 0,
            "紫矩阵货币数量。Purple matrix currency amount.");
        GreenMatrixCurrency = Config.Bind<int>("Currency", "GreenMatrixCurrency", 0,
            "绿矩阵货币数量。Green matrix currency amount.");
        WhiteMatrixCurrency = Config.Bind<int>("Currency", "WhiteMatrixCurrency", 0,
            "白矩阵货币数量。White matrix currency amount.");
        FragmentAmount = Config.Bind<int>("Currency", "FragmentAmount", 0,
            "碎片数量。Fragment amount.");
        RecipeShards = Config.Bind<int>("Currency", "RecipeShards", 0,
            "配方碎片数量。Recipe shard amount.");

        // 初始化其他商店UI相关
        EnableShopItemPreview = Config.Bind<bool>("ShopUI", "EnableShopItemPreview", true,
            "是否启用商店物品预览。Whether to enable shop item preview.");
        ShowMatrixConversionRate = Config.Bind<bool>("ShopUI", "ShowMatrixConversionRate", true,
            "是否显示矩阵转换率。Whether to show matrix conversion rate.");
        EnableCraftingPreview = Config.Bind<bool>("ShopUI", "EnableCraftingPreview", true,
            "是否启用合成预览。Whether to enable crafting preview.");
        CompactShopView = Config.Bind<bool>("ShopUI", "CompactShopView", false,
            "是否启用紧凑商店视图。Whether to enable compact shop view.");
        ShopUITheme = Config.Bind<int>("ShopUI", "ShopUITheme", 1,
            "商店UI主题(1-5)。Shop UI theme(1-5).");

        // 初始化特殊功能
        EnableAutoFractionate = Config.Bind<bool>("Special", "EnableAutoFractionate", false,
            "是否启用自动分馏。Whether to enable auto fractionation.");
        EnableBatchPurchase = Config.Bind<bool>("Special", "EnableBatchPurchase", true,
            "是否启用批量购买。Whether to enable batch purchase.");
        EnablePitySystem = Config.Bind<bool>("Special", "EnablePitySystem", true,
            "是否启用保底系统。Whether to enable pity system.");
        EnableRecipeHistory = Config.Bind<bool>("Special", "EnableRecipeHistory", true,
            "是否启用配方历史。Whether to enable recipe history.");
        EnableFavoritesSystem = Config.Bind<bool>("Special", "EnableFavoritesSystem", true,
            "是否启用收藏系统。Whether to enable favorites system.");

        //移除之前多余的设置项，然后保存
        (Traverse.Create(Config).Property("OrphanedEntries").GetValue() as IDictionary)?.Clear();
        Config.Save();
    }

    /**
     * 禁用首次弹窗，并更新版本号。
     * 在主界面弹窗关闭后执行。
     */
    public static void SetConfig() {
        DisableMessageBoxEntry.Value = true;
        CurrentVersionEntry.Value = VERSION;
        AddedBlueprintsEntry.Value = true;
        configFile.Save();
    }

    /**
     * 更新商店相关设置项
     */
    public static void SetShopConfig(bool enableFractionateShopFeature, bool enableSlotMachineFractionator,
        bool enableRandomRecipeUnlock, bool enableSpecialRecipes, bool enableShopItemsPurchase,
        bool enableMatrixCurrency, bool enableUpgradeSystem, bool enableRecipeStar,
        int maxRecipeStarLevel, int shopMaxLevel, int maxSelectionChanges,
        float selfSelectEfficiency, float upgradeChance, float rareRecipeChance) {

        EnableFractionateShopFeature.Value = enableFractionateShopFeature;
        EnableSlotMachineFractionator.Value = enableSlotMachineFractionator;
        EnableRandomRecipeUnlock.Value = enableRandomRecipeUnlock;
        EnableSpecialRecipes.Value = enableSpecialRecipes;
        EnableShopItemsPurchase.Value = enableShopItemsPurchase;
        EnableMatrixCurrency.Value = enableMatrixCurrency;
        EnableUpgradeSystem.Value = enableUpgradeSystem;
        EnableRecipeStar.Value = enableRecipeStar;
        MaxRecipeStarLevel.Value = maxRecipeStarLevel;
        ShopMaxLevel.Value = shopMaxLevel;
        MaxSelectionChanges.Value = maxSelectionChanges;
        SelfSelectEfficiency.Value = selfSelectEfficiency;
        UpgradeChance.Value = upgradeChance;
        RareRecipeChance.Value = rareRecipeChance;

        logger.LogInfo("Fractionate Everything shop settings changed.");
        configFile.Save();
    }

    /**
     * 更新货币和特殊功能相关设置项
     */
    public static void SetCurrencyAndSpecialConfig(int redMatrixCurrency, int blueMatrixCurrency,
        int yellowMatrixCurrency, int purpleMatrixCurrency, int greenMatrixCurrency, int whiteMatrixCurrency,
        int fragmentAmount, int recipeShards, bool enableAutoFractionate, bool enableBatchPurchase,
        bool enablePitySystem, bool enableRecipeHistory, bool enableFavoritesSystem) {

        RedMatrixCurrency.Value = redMatrixCurrency;
        BlueMatrixCurrency.Value = blueMatrixCurrency;
        YellowMatrixCurrency.Value = yellowMatrixCurrency;
        PurpleMatrixCurrency.Value = purpleMatrixCurrency;
        GreenMatrixCurrency.Value = greenMatrixCurrency;
        WhiteMatrixCurrency.Value = whiteMatrixCurrency;
        FragmentAmount.Value = fragmentAmount;
        RecipeShards.Value = recipeShards;
        EnableAutoFractionate.Value = enableAutoFractionate;
        EnableBatchPurchase.Value = enableBatchPurchase;
        EnablePitySystem.Value = enablePitySystem;
        EnableRecipeHistory.Value = enableRecipeHistory;
        EnableFavoritesSystem.Value = enableFavoritesSystem;

        logger.LogInfo("Fractionate Everything currency and special settings changed.");
        configFile.Save();
    }

    /**
     * 更新商店UI相关设置项
     */
    public static void SetShopUIConfig(bool enableShopItemPreview, bool showMatrixConversionRate,
        bool enableCraftingPreview, bool compactShopView, int shopUITheme) {

        EnableShopItemPreview.Value = enableShopItemPreview;
        ShowMatrixConversionRate.Value = showMatrixConversionRate;
        EnableCraftingPreview.Value = enableCraftingPreview;
        CompactShopView.Value = compactShopView;
        ShopUITheme.Value = shopUITheme;

        logger.LogInfo("Fractionate Everything shop UI settings changed.");
        configFile.Save();
    }

    /**
     * 获取矩阵货币数量
     */
    public static int GetMatrixCurrency(int matrixType) {
        switch (matrixType) {
            case 1:
                return RedMatrixCurrency.Value;
            case 2:
                return BlueMatrixCurrency.Value;
            case 3:
                return YellowMatrixCurrency.Value;
            case 4:
                return PurpleMatrixCurrency.Value;
            case 5:
                return GreenMatrixCurrency.Value;
            case 6:
                return WhiteMatrixCurrency.Value;
            default:
                return 0;
        }
    }

    /**
     * 设置矩阵货币数量
     */
    public static void SetMatrixCurrency(int matrixType, int amount) {
        switch (matrixType) {
            case 1:
                RedMatrixCurrency.Value = amount;
                break;
            case 2:
                BlueMatrixCurrency.Value = amount;
                break;
            case 3:
                YellowMatrixCurrency.Value = amount;
                break;
            case 4:
                PurpleMatrixCurrency.Value = amount;
                break;
            case 5:
                GreenMatrixCurrency.Value = amount;
                break;
            case 6:
                WhiteMatrixCurrency.Value = amount;
                break;
        }
        configFile.Save();
    }

    /**
     * 更新自定义设置项。
     * 在点击设置-杂项的应用按钮时执行。
     */
    [SuppressMessage("ReSharper", "ParameterHidesMember")]
    public static void SetConfig(int iconVersion, bool enableBuildingAsTrash) {
        bool iconVersionChanged = iconVersion != IconVersionEntry.Value;
        bool enableBuildingAsTrashChanged = enableBuildingAsTrash != EnableBuildingAsTrashEntry.Value;
        //修改配置文件里面的内容
        IconVersionEntry.Value = iconVersion;
        logger.LogInfo($"Fractionate Everything setting changed.\n"
                       + $" iconVersion:{iconVersion}");
        configFile.Save();
        // //重新加载所有分馏配方，玩家需要重新载入存档
        // if (iconVersionChanged || enableDestroyChanged || enableFuelRodFracChanged || enableMatrixFracChanged) {
        //     foreach (RecipeProto r in LDB.recipes.dataArray) {
        //         if (r.Type != ERecipeType.Fractionate || r.ID == R重氢分馏_GB氦闪约束器) {
        //             continue;
        //         }
        //         r.ModifyIconAndDesc();
        //         r.Preload(r.index);
        //     }
        // }
        //调整垃圾回收分馏塔描述
        if (enableBuildingAsTrashChanged) {
            ItemProto trashRecycleFractionator = LDB.items.Select(IFE垃圾回收分馏塔);
            trashRecycleFractionator.Description = enableBuildingAsTrash ? "I垃圾回收分馏塔2" : "I垃圾回收分馏塔";
            trashRecycleFractionator.Preload(trashRecycleFractionator.index);
        }
    }

    #endregion

    public void Awake() {
        using (ProtoRegistry.StartModLoad(GUID)) {
            logger = Logger;

            Version version = new();
            version.FromFullString(VERSION);
            versionNumber = version.sig;

            Translation.AddTranslations();

            LoadConfig();

            // tab分馏1 = TabSystem.RegisterTab($"{GUID}:{GUID}Tab1", new("分馏页面1".Translate(), Tech1134IconPath));
            // tab分馏2 = TabSystem.RegisterTab($"{GUID}:{GUID}Tab2", new("分馏页面2".Translate(), Tech1134IconPath));

            var executingAssembly = Assembly.GetExecutingAssembly();
            ModPath = Path.GetDirectoryName(executingAssembly.Location);
            fracicons = new(GUID, "fracicons", ModPath);
            fracicons.LoadAssetBundle("fracicons");
            ProtoRegistry.AddResource(fracicons);

            LDBTool.PreAddDataAction += PreAddData;
            LDBTool.PostAddDataAction += PostAddData;

            foreach (Type type in executingAssembly.GetTypes()) {
                //Compatibility内的类由自己patch，不在这里处理
                if (type.Namespace == null
                    || type.Namespace.StartsWith("FractionateEverything.Compatibility")) {
                    continue;
                }
                harmony.PatchAll(type);
            }
            //在LDBTool已执行完毕所有PostAddData、EditData后，执行最终修改操作
            harmony.Patch(
                AccessTools.Method(typeof(VFPreload), "InvokeOnLoadWorkEnded"),
                null,
                new(typeof(FractionateEverything), nameof(FinalAction)) {
                    after = [LDBToolPlugin.MODGUID]
                }
            );
            //在载入语言时、CommonAPIPlugin添加翻译后，添加额外的所有翻译
            harmony.Patch(
                AccessTools.Method(typeof(Localization), "LoadLanguage"),
                null,
                new(typeof(TranslationUtils), nameof(TranslationUtils.LoadLanguagePostfixAfterCommonApi)) {
                    after = [CommonAPIPlugin.GUID]
                }
            );

            GameLogic.Enable(true);
            UIShop.Init();
            UIFunctions.Init();
        }
    }

    private void Start() {
        MyWindowManager.InitBaseObjects();
        MyWindowManager.Enable(true);

        // _patches?.Do(type => type.GetMethod("Start")?.Invoke(null, null));
        //
        // object[] parameters = [UIPatch.GetHarmony()];
        // _compats?.Do(type => type.GetMethod("Start")?.Invoke(null, parameters));
        // WindowFunctions.Start();
    }

    private void OnDestroy() {
        // _patches?.Do(type => type.GetMethod("Uninit")?.Invoke(null, null));
        //
        // UIPatch.Enable(false);
        MyWindowManager.Enable(false);
        GameLogic.Enable(false);
    }

    private void Update() {
        if (VFInput.inputing) return;
        if (DSPGame.IsMenuDemo) {
            UIFunctions.OnInputUpdate();
            return;
        }
        // LogisticsPatch.OnInputUpdate();
        UIFunctions.OnInputUpdate();
        // GamePatch.OnInputUpdate();
        // FactoryPatch.OnInputUpdate();
        // PlayerPatch.OnInputUpdate();
    }

    public void PreAddData() {
        //添加新科技
        FracTechManager.AddTechs();
        //创建新的分馏塔
        FracItemManager.CreateAndPreAddNewFractionators();
    }

    public void PostAddData() {
        LDB.models.OnAfterDeserialize();
        ModelProto.InitMaxModelIndex();
        ModelProto.InitModelIndices();
        ModelProto.InitModelOrders();
        foreach (TechProto proto in LDB.techs.dataArray) {
            proto.Preload();
        }
        foreach (TechProto proto in LDB.techs.dataArray) {
            proto.PreTechsImplicit = proto.PreTechsImplicit.Except(proto.PreTechs).ToArray();
            proto.UnlockRecipes = proto.UnlockRecipes.Distinct().ToArray();
            proto.Preload2();
        }
        FracTechManager.PreloadAll();
        FracItemManager.PreloadAll();
    }

    /// <summary>
    /// 在所有内容添加完毕后，再执行的代码。
    /// </summary>
    public static void FinalAction() {
        if (_finished) return;
        PreloadAndInitAll();
        FracProcess.Init();
        //SetFractionatorCacheSize用到了Init生成的数据
        FracItem.SetFractionatorCacheSize();
        //AddFracRecipes用到了Init生成的数据
        FracRecipeManager.AddFracRecipes();
        _finished = true;
    }

    public static void PreloadAndInitAll() {
        // LDB.items.OnAfterDeserialize();
        // LDB.recipes.OnAfterDeserialize();
        // LDB.techs.OnAfterDeserialize();
        // LDB.models.OnAfterDeserialize();
        // LDB.milestones.OnAfterDeserialize();
        // LDB.journalPatterns.OnAfterDeserialize();
        // LDB.themes.OnAfterDeserialize();
        // LDB.veins.OnAfterDeserialize();
        // foreach (MilestoneProto milestone in LDB.milestones.dataArray) {
        //     milestone.Preload();
        // }
        // foreach (JournalPatternProto journalPattern in LDB.journalPatterns.dataArray) {
        //     journalPattern.Preload();
        // }
        // foreach (VeinProto proto in LDB.veins.dataArray) {
        //     proto.Preload();
        //     proto.name = proto.Name.Translate();
        // }
        // foreach (ModelProto proto in LDB.models.dataArray) {
        //     proto.Preload();
        // }
        // foreach (TechProto proto in LDB.techs.dataArray) {
        //     proto.Preload();
        // }
        // for (var i = 0; i < LDB.items.dataArray.Length; ++i) {
        //     LDB.items.dataArray[i].recipes = null;
        //     LDB.items.dataArray[i].rawMats = null;
        //     LDB.items.dataArray[i].Preload(i);
        // }
        // for (var i = 0; i < LDB.recipes.dataArray.Length; ++i) {
        //     LDB.recipes.dataArray[i].Preload(i);
        // }
        // foreach (TechProto proto in LDB.techs.dataArray) {
        //     proto.PreTechsImplicit = proto.PreTechsImplicit.Except(proto.PreTechs).ToArray();
        //     proto.UnlockRecipes = proto.UnlockRecipes.Distinct().ToArray();
        //     proto.Preload2();
        // }
        ItemProto.InitFuelNeeds();
        ItemProto.InitTurretNeeds();
        ItemProto.InitFluids();
        ItemProto.InitTurrets();
        ItemProto.InitEnemyDropTables();
        ItemProto.InitConstructableItems();
        ItemProto.InitItemIds();
        ItemProto.InitItemIndices();
        ItemProto.InitMechaMaterials();
        ItemProto.InitFighterIndices();
        ModelProto.InitMaxModelIndex();
        ModelProto.InitModelIndices();
        ModelProto.InitModelOrders();
        RecipeProto.InitFractionatorNeeds();
        RaycastLogic.LoadStatic();
        StorageComponent.staticLoaded = false;
        StorageComponent.LoadStatic();
    }

    #region IModCanSave

    public void Import(BinaryReader r) {
        LogInfo("FE Import");
        int savedVersion = r.ReadInt32();
        FracItem.Import(r);
        FracRecipeManager.Import(r);
        FracProcess.Import(r);
    }

    public void Export(BinaryWriter w) {
        LogInfo("FE Export");
        w.Write(versionNumber);
        FracItem.Export(w);
        FracRecipeManager.Export(w);
        FracProcess.Export(w);
    }

    public void IntoOtherSave() {
        LogInfo("FE IntoOtherSave");
        FracItem.IntoOtherSave();
        FracRecipeManager.IntoOtherSave();
        FracProcess.IntoOtherSave();
    }

    #endregion
}
