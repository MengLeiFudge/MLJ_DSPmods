using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using CommonAPI;
using CommonAPI.Systems;
using CommonAPI.Systems.ModLocalization;
using crecheng.DSPModSave;
using FE.Compatibility;
using FE.Logic;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.UI;
using FE.UI.Components;
using FE.UI.View;
using FE.Utils;
using HarmonyLib;
using xiaoye97;
using static FE.Utils.LogUtils;

namespace FE;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency(CommonAPIPlugin.GUID)]
[BepInDependency(DSPModSavePlugin.MODGUID)]
[BepInDependency(CheckPlugins.GUID)]
[CommonAPISubmoduleDependency(nameof(CustomKeyBindSystem), nameof(ProtoRegistry), nameof(TabSystem),
    nameof(LocalizationModule))]
public class FractionateEverything : BaseUnityPlugin, IModCanSave {
    #region Fields

    /// <summary>
    /// 原版分馏科技图标，主要用于标签页。暂时无图标的新增科技也可以临时使用它作为图标路径。
    /// </summary>
    public const string Tech1134IconPath = "Icons/Tech/1134";
    /// <summary>
    /// 指示分馏标签是第几页。新增物品的GridIndex需要根据页面来确定。
    /// </summary>
    public static int tab分馏;
    public static string ModPath;
    public static ResourceData FEAssets;
    public static readonly Harmony harmony = new(PluginInfo.PLUGIN_GUID);
    private static bool _finished;

    #endregion

    #region Config

    private static ConfigFile configFile;
    /// <summary>
    /// 旧的版本号。
    /// </summary>
    public static ConfigEntry<string> CurrentVersion;
    /// <summary>
    /// 判断是否有版本更新，以便于弹窗提示MOD更新内容。
    /// </summary>
    public static bool isVersionChanged => CurrentVersion.Value != PluginInfo.PLUGIN_VERSION;
    /// <summary>
    /// 是否在游戏加载时禁用提示信息。
    /// </summary>
    public static ConfigEntry<bool> DisableMessageBox;

    public void LoadConfig() {
        configFile = Config;

        CurrentVersion = Config.Bind("config", "CurrentVersion", "",
            new ConfigDescription(
                "Current game version, used to control whether or not to show the update pop-up window.\n"
                + "当前游戏版本，用于控制是否显示更新弹窗。",
                new AcceptableStringValue(""), null));

        DisableMessageBox = Config.Bind("config", "DisableMessageBox", false,
            new ConfigDescription(
                "Don't show message when FractionateEverything is loaded.\n"
                + "禁用游戏加载完成后显示的万物分馏提示信息。",
                new AcceptableBoolValue(false), null));

        BuildingManager.LoadConfig(Config);
        MainWindow.LoadConfig(Config);

        (Traverse.Create(Config).Property("OrphanedEntries").GetValue() as IDictionary)?.Clear();
        Config.Save();
    }

    /**
     * 禁用首次弹窗，并更新版本号。
     * 在主界面弹窗关闭后执行。
     */
    public static void SetConfig() {
        DisableMessageBox.Value = true;
        CurrentVersion.Value = PluginInfo.PLUGIN_VERSION;
        configFile.Save();
    }

    #endregion

    public void Awake() {
        using (ProtoRegistry.StartModLoad(PluginInfo.PLUGIN_GUID)) {
            InitLogger(Logger);

            Translation.AddTranslations();
            BuildingManager.AddTranslations();
            ItemManager.AddTranslations();
            TechManager.AddTranslations();
            OutputInfo.AddTranslations();

            LoadConfig();

            tab分馏 = TabSystem.RegisterTab($"{PluginInfo.PLUGIN_GUID}:{PluginInfo.PLUGIN_GUID}Tab",
                new("分馏页面".Translate(), Tech1134IconPath));

            var executingAssembly = Assembly.GetExecutingAssembly();
            ModPath = Path.GetDirectoryName(executingAssembly.Location);
            FEAssets = new(PluginInfo.PLUGIN_GUID, "fe", ModPath);
            FEAssets.LoadAssetBundle("fe");
            ProtoRegistry.AddResource(FEAssets);

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
            //     new(typeof(I18NUtils), nameof(I18NUtils.LoadLanguagePostfixAfterCommonApi)) {
            //         after = [CommonAPIPlugin.GUID]
            //     }
            // );

            GameLogic.Enable(true);
            MainWindow.Init();
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
        //初步添加分馏塔
        BuildingManager.AddFractionators();
        //添加科技
        TechManager.AddTechs();
    }

    public void PostAddData() {
        //调整分馏塔模型颜色
        BuildingManager.SetFractionatorMaterials();
    }

    /// <summary>
    /// 在所有内容添加完毕后，再执行的代码。
    /// </summary>
    public static void FinalAction() {
        if (_finished) return;
        PreloadAndInitAll();
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
        // ItemProto.InitFuelNeeds();
        // ItemProto.InitTurretNeeds();
        // ItemProto.InitFluids();
        // ItemProto.InitTurrets();
        // ItemProto.InitEnemyDropTables();
        // ItemProto.InitConstructableItems();
        // ItemProto.InitItemIds();
        // ItemProto.InitItemIndices();
        // ItemProto.InitMechaMaterials();
        // //ItemProto.InitFighterIndices();
        // //ItemProto.InitPowerFacilityIndices();
        // //ItemProto.InitProductionMask();
        // ModelProto.InitMaxModelIndex();
        // ModelProto.InitModelIndices();
        // ModelProto.InitModelOrders();
        // RecipeProto.InitFractionatorNeeds();
        // RaycastLogic.LoadStatic();
        //重新设定堆叠大小
        StorageComponent.staticLoaded = false;
        StorageComponent.LoadStatic();
    }

    #region IModCanSave

    /// <summary>
    /// 载入存档时执行。
    /// </summary>
    public void Import(BinaryReader r) {
        int version = r.ReadInt32();
        RecipeManager.Import(r);
        BuildingManager.Import(r);
        ItemManager.Import(r);
        MainWindow.Import(r);
    }

    /// <summary>
    /// 导出存档时执行。
    /// </summary>
    public void Export(BinaryWriter w) {
        w.Write(1);
        RecipeManager.Export(w);
        BuildingManager.Export(w);
        ItemManager.Export(w);
        MainWindow.Export(w);
    }

    /// <summary>
    /// 新建存档时执行。
    /// </summary>
    public void IntoOtherSave() {
        RecipeManager.IntoOtherSave();
        BuildingManager.IntoOtherSave();
        ItemManager.IntoOtherSave();
        MainWindow.IntoOtherSave();
    }

    #endregion
}
