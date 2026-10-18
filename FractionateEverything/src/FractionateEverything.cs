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
using FE.Lifecycle;
using FE.Compatibility;
using FE.Compatibility.Nebula;
using FE.Logic.Civilization;
using FE.UI.Foundation.Window;
using FE.UI.MainPanel;
using HarmonyLib;
using NebulaAPI;
using NebulaAPI.Interfaces;
using xiaoye97;
using static FE.Utils.Utils;

namespace FE;

/// <summary>
/// FE 模组入口插件。
/// </summary>
[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency(LDBToolPlugin.MODGUID)]
[BepInDependency(DSPModSavePlugin.MODGUID)]
[BepInDependency(CommonAPIPlugin.GUID)]
[BepInDependency(BuildBarToolPlugin.GUID)]
[BepInDependency(NebulaModAPI.API_GUID)]
[BepInDependency(CheckPlugins.GUID)]
[BepInDependency("AchievementsEnabler", BepInDependency.DependencyFlags.HardDependency)]
[CommonAPISubmoduleDependency(nameof(CustomKeyBindSystem), nameof(ProtoRegistry), nameof(TabSystem),
    nameof(LocalizationModule), nameof(AssemblerRecipeSystem), nameof(PickerExtensionsSystem))]
public class FractionateEverything : BaseUnityPlugin, IModCanSave, IMultiplayerModWithSettings {
    #region 字段

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

    #endregion

    #region 配置

    private static ConfigFile configFile;

    public void LoadConfig() {
        configFile = Config;
        PermanentProfileStore.Bind(Config);

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

            LoadTranslationsFromJson();

            LoadConfig();

            tab分馏 = TabSystem.RegisterTab($"{PluginInfo.PLUGIN_GUID}:{PluginInfo.PLUGIN_GUID}Tab",
                new("分馏页面".Translate(), Tech1134IconPath));

            var executingAssembly = Assembly.GetExecutingAssembly();
            ModPath = Path.GetDirectoryName(executingAssembly.Location);
            FEAssets = new(PluginInfo.PLUGIN_GUID, "fe", ModPath);
            FEAssets.LoadAssetBundle("fe");
            ProtoRegistry.AddResource(FEAssets);

            NebulaModAPI.RegisterPackets(executingAssembly);

            //加载顺序：
            //LDBTool.PreAddDataAction
            //VFPreload
            //  模型预加载
            //  物品预加载
            //  配方预加载
            //  科技预加载
            //  物品初始化
            //  模型初始化
            //  配方初始化
            //  RaycastLogic.LoadStatic
            //  第二阶段科技预加载
            //LDBTool.PostAddDataAction

            LDBTool.PreAddDataAction += FeatureBootstrap.PreAddData;
            LDBTool.PostAddDataAction += FeatureBootstrap.PostAddData;

            string CheckPluginsNamespace = typeof(CheckPlugins).Namespace;
            foreach (Type type in executingAssembly.GetTypes()) {
                //Compatibility内的类由自己patch，不在这里处理
                if (type.Namespace == null
                    || (CheckPluginsNamespace != null && type.Namespace.StartsWith(CheckPluginsNamespace))) {
                    continue;
                }
                harmony.PatchAll(type);
            }
            // 在LDBTool及兼容模组完成原型修改后，读取最终科技成本与配方基线。
            harmony.Patch(
                AccessTools.Method(typeof(VFPreload), nameof(VFPreload.InvokeOnLoadWorkEnded)),
                null,
                new(typeof(FeatureBootstrap), nameof(FeatureBootstrap.FinalAction)) {
                    after = [LDBToolPlugin.MODGUID, "org.ProfessorCat305.OrbitalRing",
                        "Gnimaerd.DSP.plugin.MoreMegaStructure",
                        "ProjectOrbitalRing.Compatibility.Gnimaerd.DSP.plugin.MoreMegaStructure"],
                    priority = Priority.Last
                }
            );
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
        CivilizationModule.Tick();
    }

    #region 存档与联机设置接口实现

    /// <summary>
    /// 载入存档时执行。
    /// </summary>
    public void Import(BinaryReader r) {
        FeatureSaveRegistry.IntoOtherSave();
        int version = r.ReadInt32();
        if (version != 13) {
            // 当前玩法使用独立格式，不转换旧版本的研究、探索或科技状态。
            if (r.BaseStream.CanSeek) {
                r.BaseStream.Seek(0, SeekOrigin.End);
            }
            ResearchManager.MarkLegacyGeneration();
            return;
        }
        FeatureSaveRegistry.Import(r);
    }

    /// <summary>
    /// 导出存档时执行。
    /// </summary>
    public void Export(BinaryWriter w) {
        w.Write(13);
        FeatureSaveRegistry.Export(w);
    }

    /// <summary>
    /// 新建存档时执行，此方法无需修改。
    /// </summary>
    public void IntoOtherSave() {
        // 联机时客户端会先执行Import（源于Nebula），再执行IntoOtherSave（源于DSPModSave），所以客户端需要跳过
        if (NebulaMultiplayerModAPI.IsClient) {
            return;
        }
        FeatureSaveRegistry.IntoOtherSave();
        ResearchManager.InitializeGeneration3StarterState();
    }

    /// <inheritdoc />
    public string Version => $"{PluginInfo.PLUGIN_VERSION}.{NebulaMultiplayerModAPI.ProtocolVersion}";

    public bool CheckVersion(string hostVersion, string clientVersion) {
        return hostVersion.Equals(clientVersion);
    }

    #endregion
}
