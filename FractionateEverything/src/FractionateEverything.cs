using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BuildBarTool;
using CommonAPI;
using CommonAPI.Systems;
using CommonAPI.Systems.ModLocalization;
using crecheng.DSPModSave;
using FE.Compatibility;
using FE.Logic.Manager;
using FE.Logic.Recipe;
using FE.UI.Components;
using FE.UI.View;
using HarmonyLib;
using xiaoye97;
using static FE.Utils.Utils;

namespace FE;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency(LDBToolPlugin.MODGUID)]
[BepInDependency(DSPModSavePlugin.MODGUID)]
[BepInDependency(CommonAPIPlugin.GUID)]
[BepInDependency(BuildBarToolPlugin.GUID)]
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

    public void LoadConfig() {
        configFile = Config;

        CheckPlugins.DisableMessageBox = Config.Bind("other", "DisableMessageBox", false,
            "Don't show messagebox when FractionateEverything loaded.");
        MainWindow.LoadConfig(Config);

        //清除无用项目
        Traverse.Create(Config).Property("OrphanedEntries").GetValue<Dictionary<ConfigDefinition, string>>().Clear();
        Config.Save();
    }

    public static void SaveConfig() {
        configFile.Save();
    }

    #endregion

    public void Awake() {
        using (ProtoRegistry.StartModLoad(PluginInfo.PLUGIN_GUID)) {
            InitLogger(Logger);

            Register("分馏页面", "Fractionate", "分馏");
            Register("分馏与插件页面", "Frac&Beacon", "分馏&插件");
            ERecipeExtension.AddTranslations();
            OutputInfo.AddTranslations();
            AddTranslations();
            BuildingManager.AddTranslations();
            ItemManager.AddTranslations();
            ProcessManager.AddTranslations();
            TechManager.AddTranslations();
            MainWindow.AddTranslations();

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

            string CheckPluginsNamespace = typeof(CheckPlugins).Namespace;
            foreach (Type type in executingAssembly.GetTypes()) {
                //Compatibility内的类由自己patch，不在这里处理
                if (type.Namespace == null
                    || (CheckPluginsNamespace != null && type.Namespace.StartsWith(CheckPluginsNamespace))) {
                    continue;
                }
                harmony.PatchAll(type);
            }
            //在LDBTool已执行完毕所有PostAddData、EditData后，执行最终修改操作
            harmony.Patch(
                AccessTools.Method(typeof(VFPreload), nameof(VFPreload.InvokeOnLoadWorkEnded)),
                null,
                new(typeof(FractionateEverything), nameof(FinalAction)) {
                    after = [LDBToolPlugin.MODGUID]
                }
            );
            // //在载入语言时、CommonAPIPlugin添加翻译后，添加额外的所有翻译
            // harmony.Patch(
            //     AccessTools.Method(typeof(Localization), nameof(Localization.LoadLanguage)),
            //     null,
            //     new(typeof(Utils), nameof(Utils.LoadLanguagePostfixAfterCommonApi)) {
            //         after = [CommonAPIPlugin.GUID]
            //     }
            // );

            MainWindow.Init();
        }
    }

    private void Start() {
        MyWindowManager.InitBaseObjects();
        MyWindowManager.Enable(true);
    }

    private void OnDestroy() {
        MyWindowManager.Enable(false);
    }

    private void Update() {
        MainWindow.OnInputUpdate();
    }

    public void PreAddData() {
        //添加分馏塔原胚、精华
        ItemManager.AddFractionalPrototypeAndEssence();
        //初步添加分馏塔
        BuildingManager.AddFractionators();
        //添加科技
        TechManager.AddTechs();
    }

    public void PostAddData() {
        //设置分馏塔、物流交互站颜色
        BuildingManager.SetFractionatorMaterial();
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
        //将物品分类到各个矩阵层级中
        ItemManager.ClassifyItemsToMatrix();
        //UpdateHpAndEnergy用到了Init生成的数据
        BuildingManager.UpdateHpAndEnergy();
        //SetFractionatorCacheSize用到了Init生成的数据
        BuildingManager.SetFractionatorCacheSize();
        //AddFracRecipes用到了Init生成的数据
        RecipeManager.AddFracRecipes();
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
        ItemProto.InitFighterIndices();
        ItemProto.InitPowerFacilityIndices();
        ItemProto.InitProductionMask();
        ModelProto.InitMaxModelIndex();
        ModelProto.InitModelIndices();
        ModelProto.InitModelOrders();
        RecipeProto.InitFractionatorNeeds();
        RaycastLogic.LoadStatic();
        //重新设定堆叠大小
        StorageComponent.staticLoaded = false;
        StorageComponent.LoadStatic();
    }

    #region IModCanSave

    /// <summary>
    /// 载入存档时执行。
    /// </summary>
    public void Import(BinaryReader r) {
        IntoOtherSave();
        int version = r.ReadInt32();
        RecipeManager.Import(r);
        BuildingManager.Import(r);
        ItemManager.Import(r);
        MainWindow.Import(r);
        TechManager.CheckRecipesWhenImport();
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
