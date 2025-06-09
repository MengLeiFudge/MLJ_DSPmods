using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CommonAPI;
using CommonAPI.Systems;
using CommonAPI.Systems.ModLocalization;
using crecheng.DSPModSave;
using FE.Compatibility;
using FE.Logic;
using FE.Logic.Manager;
using FE.UI;
using FE.UI.Components;
using FE.UI.Shop;
using FE.Utils;
using HarmonyLib;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using xiaoye97;

namespace FE;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency(CommonAPIPlugin.GUID)]
[BepInDependency(DSPModSavePlugin.MODGUID)]
[CommonAPISubmoduleDependency(nameof(CustomKeyBindSystem), nameof(ProtoRegistry), nameof(TabSystem),
    nameof(LocalizationModule))]
[BepInDependency(CheckPlugins.GUID)]
public class FractionateEverything : BaseUnityPlugin, IModCanSave {
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
    public static int tab分馏1;
    // public static int tab分馏2;
    public static string ModPath;
    public static ResourceData fracicons;
    public static readonly Harmony harmony = new(PluginInfo.PLUGIN_GUID);
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
    public static bool isVersionChanged => CurrentVersionEntry.Value != PluginInfo.PLUGIN_VERSION;
    public static ConfigEntry<bool> DisableMessageBoxEntry;
    /// <summary>
    /// 是否在游戏加载时禁用提示信息。
    /// </summary>
    public static bool disableMessageBox => DisableMessageBoxEntry.Value;

    public static ConfigEntry<bool> AutoCruiseEnabled;

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
        CurrentVersionEntry.Value = PluginInfo.PLUGIN_VERSION;
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


        logger.LogInfo("Fractionate Everything currency and special settings changed.");
        configFile.Save();
    }

    /**
     * 更新商店UI相关设置项
     */
    public static void SetShopUIConfig(bool enableShopItemPreview, bool showMatrixConversionRate,
        bool enableCraftingPreview, bool compactShopView, int shopUITheme) {


        logger.LogInfo("Fractionate Everything shop UI settings changed.");
        configFile.Save();
    }

    /**
     * 更新自定义设置项。
     * 在点击设置-杂项的应用按钮时执行。
     */
    public static void SetConfig(int iconVersion, bool enableBuildingAsTrash) {
        logger.LogInfo($"Fractionate Everything setting changed.\n"
                       + $" iconVersion:{iconVersion}");
        configFile.Save();
    }

    #endregion

    public void Awake() {
        using (ProtoRegistry.StartModLoad(PluginInfo.PLUGIN_GUID)) {
            logger = Logger;

            Version version = new();
            version.FromFullString(PluginInfo.PLUGIN_VERSION);
            versionNumber = version.sig;

            Translation.AddTranslations();

            LoadConfig();

            tab分馏1 = TabSystem.RegisterTab($"{PluginInfo.PLUGIN_GUID}:{PluginInfo.PLUGIN_GUID}Tab",
                new("分馏页面".Translate(), Tech1134IconPath));

            var executingAssembly = Assembly.GetExecutingAssembly();
            ModPath = Path.GetDirectoryName(executingAssembly.Location);
            fracicons = new(PluginInfo.PLUGIN_GUID, "fracicons", ModPath);
            fracicons.LoadAssetBundle("fracicons");
            ProtoRegistry.AddResource(fracicons);

            //加载顺序：
            //LDBTool.PreAddDataAction
            //VFPreload
            //  model preload
            //  item preload
            //  recipe preload
            //  tech preload
            //  item init
            //  model init
            //  recipe init
            //  RaycastLogic LoadStatic
            //  tech preload2
            //LDBTool.PostAddDataAction

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
            // //在载入语言时、CommonAPIPlugin添加翻译后，添加额外的所有翻译
            // harmony.Patch(
            //     AccessTools.Method(typeof(Localization), "LoadLanguage"),
            //     null,
            //     new(typeof(TranslationUtils), nameof(TranslationUtils.LoadLanguagePostfixAfterCommonApi)) {
            //         after = [CommonAPIPlugin.GUID]
            //     }
            // );

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
        //添加分馏原胚、精华
        ItemManager.AddFractionalPrototypeAndEssence();
        //添加分馏塔
        BuildingManager.AddFractionators();
        //添加科技
        TechManager.AddTechs();
    }

    public void PostAddData() {
        // LDB.models.OnAfterDeserialize();
        // ModelProto.InitMaxModelIndex();
        // ModelProto.InitModelIndices();
        // ModelProto.InitModelOrders();
        // foreach (TechProto proto in LDB.techs.dataArray) {
        //     proto.Preload();
        // }
        // foreach (TechProto proto in LDB.techs.dataArray) {
        //     proto.PreTechsImplicit = proto.PreTechsImplicit.Except(proto.PreTechs).ToArray();
        //     proto.UnlockRecipes = proto.UnlockRecipes.Distinct().ToArray();
        //     proto.Preload2();
        // }
        // BuildingManager.SetUnlockInfo();
    }

    /// <summary>
    /// 在所有内容添加完毕后，再执行的代码。
    /// </summary>
    public static void FinalAction() {
        if (_finished) return;
        // PreloadAndInitAll();
        //获取部分数据，例如传送带最大速度等
        ProcessManager.Init();
        //计算物品价值
        ItemManager.CalculateItemValues();
        //SetFractionatorCacheSize用到了Init生成的数据
        BuildingManager.SetFractionatorCacheSize();
        //AddBaseRecipes用到了Init生成的数据
        RecipeManager.AddBaseRecipes();
        _finished = true;
    }

    public static void PreloadAndInitAll() {
        ItemProto.InitFuelNeeds();
        ItemProto.InitTurretNeeds();
        ItemProto.InitFluids();
        ItemProto.InitTurrets();
        ItemProto.InitEnemyDropTables();
        ItemProto.InitConstructableItems();
        ItemProto.InitItemIds();
        ItemProto.InitItemIndices();
        ItemProto.InitMechaMaterials();
        //ItemProto.InitFighterIndices();
        //ItemProto.InitPowerFacilityIndices();
        //ItemProto.InitProductionMask();
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
        RecipeManager.Import(r);
        ProcessManager.Import(r);
    }

    public void Export(BinaryWriter w) {
        LogInfo("FE Export");
        w.Write(versionNumber);
        RecipeManager.Export(w);
        ProcessManager.Export(w);
    }

    public void IntoOtherSave() {
        LogInfo("FE IntoOtherSave");
        RecipeManager.IntoOtherSave();
        ProcessManager.IntoOtherSave();
    }

    #endregion
}
