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
using FE.Bootstrap;
using FE.Compatibility;
using FE.Logic.Manager;
using FE.Persistence;
using FE.UI.Components;
using FE.UI.MainPanel;
using FE.UI.MainPanel.ProgressTask;
using HarmonyLib;
using NebulaAPI;
using NebulaAPI.Interfaces;
using xiaoye97;
using FE.Logic.Gacha;
using static FE.Logic.DataCenter.DataCenterInventory;
using static FE.Utils.Utils;

namespace FE;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency(LDBToolPlugin.MODGUID)]
[BepInDependency(DSPModSavePlugin.MODGUID)]
[BepInDependency(CommonAPIPlugin.GUID)]
[BepInDependency(BuildBarToolPlugin.GUID)]
[BepInDependency(NebulaModAPI.API_GUID)]
[BepInDependency(CheckPlugins.GUID)]
[CommonAPISubmoduleDependency(nameof(CustomKeyBindSystem), nameof(ProtoRegistry), nameof(TabSystem),
    nameof(LocalizationModule))]
public class FractionateEverything : BaseUnityPlugin, IModCanSave, IMultiplayerModWithSettings {
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

            FeatureBootstrap.AddTranslations();

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
            //在LDBTool已执行完毕所有PostAddData、EditData后，执行最终修改操作
            harmony.Patch(
                AccessTools.Method(typeof(VFPreload), nameof(VFPreload.InvokeOnLoadWorkEnded)),
                null,
                new(typeof(FeatureBootstrap), nameof(FeatureBootstrap.FinalAction)) {
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
        GachaService.InitPools();
    }

    private void OnDestroy() {
        MyWindowManager.Enable(false);
    }

    private void Update() {
        MainWindow.OnInputUpdate();
        MainTask.Tick();
    }

    #region IModCanSave & IMultiplayerModWithSettings

    /// <summary>
    /// 载入存档时执行。
    /// </summary>
    public void Import(BinaryReader r) {
        FeatureSaveRegistry.IntoOtherSave();
        int version = r.ReadInt32();
        if (version < 10) {
            // 旧版存档不兼容，读取流中剩余所有字节
            if (r.BaseStream.CanSeek) {
                r.BaseStream.Seek(0, SeekOrigin.End);
            }
            UIMessageBox.Show(
                "FE存档版本不兼容标题".Translate(),
                "FE存档版本不兼容内容".Translate(),
                "确定".Translate(),
                UIMessageBox.WARNING,
                () => AddItemToModData(IFE残片, 5000));
            return;
        }
        FeatureSaveRegistry.Import(r);
    }

    /// <summary>
    /// 导出存档时执行。
    /// </summary>
    public void Export(BinaryWriter w) {
        w.Write(10);// version，固定为10
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
    }

    public string Version => PluginInfo.PLUGIN_VERSION;

    public bool CheckVersion(string hostVersion, string clientVersion) {
        return hostVersion.Equals(clientVersion);
    }

    #endregion
}
