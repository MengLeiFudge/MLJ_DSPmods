using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CommonAPI;
using CommonAPI.Systems;
using CommonAPI.Systems.ModLocalization;
using crecheng.DSPModSave;
using FractionateEverything.Compatibility;
using FractionateEverything.Main;
using FractionateEverything.Utils;
using HarmonyLib;
using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using xiaoye97;
using static FractionateEverything.Utils.ProtoID;

namespace FractionateEverything {
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency(CommonAPIPlugin.GUID)]
    [CommonAPISubmoduleDependency(nameof(ProtoRegistry), nameof(TabSystem), nameof(LocalizationModule))]
    [BepInDependency(CheckPlugins.GUID)]
    public class FractionateEverything : BaseUnityPlugin, IModCanSave {
        public const string GUID = "com.menglei.dsp." + NAME;
        public const string NAME = "FractionateEverything";
        public const string VERSION_Main = "1.4.3";
        public const string VERSION_Debug = "";
        public const string VERSION = VERSION_Main + VERSION_Debug;

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
        public static int tab分馏2;
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
        public static ConfigEntry<bool> EnableDestroyEntry;
        /// <summary>
        /// 是否启用分馏配方中的损毁概率。
        /// </summary>
        public static bool enableDestroy => EnableDestroyEntry.Value;
        public static ConfigEntry<bool> EnableFuelRodFracEntry;
        /// <summary>
        /// 是否启用燃料棒分馏。
        /// </summary>
        public static bool enableFuelRodFrac => EnableFuelRodFracEntry.Value;
        public static ConfigEntry<bool> EnableMatrixFracEntry;
        /// <summary>
        /// 是否启用矩阵分馏。
        /// </summary>
        public static bool enableMatrixFrac => EnableMatrixFracEntry.Value;
        public static ConfigEntry<bool> EnableBuildingAsTrashEntry;
        /// <summary>
        /// 垃圾回收分馏塔能否输入建筑。
        /// </summary>
        public static bool enableBuildingAsTrash => EnableBuildingAsTrashEntry.Value;

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

            EnableDestroyEntry = Config.Bind("config", "EnableDestroy", true,
                new ConfigDescription(
                    "Whether to enable the probability of destruction in fractionate recipes (recommended enable).\n"
                    + "When enabled, if the fractionation recipe has a probability of destruction, there is a certain probability that the input item will disappear during fractionation.\n"
                    + "是否启用分馏配方中的损毁概率（建议开启）。\n"
                    + "启用情况下，如果分馏配方具有损毁概率，则分馏时会有一定概率导致原料直接消失。",
                    new AcceptableBoolValue(true), null));

            EnableFuelRodFracEntry = Config.Bind("config", "EnableFuelRodFracEntry", false,
                new ConfigDescription(
                    "Whether to enable fuel rod fractionation.\n"
                    + "是否启用燃料棒分馏。",
                    new AcceptableBoolValue(false), null));

            EnableMatrixFracEntry = Config.Bind("config", "EnableMatrixFracEntry", false,
                new ConfigDescription(
                    "Whether to enable matrix fractionation (recommended disable).\n"
                    + "是否启用矩阵分馏（建议关闭）。",
                    new AcceptableBoolValue(false), null));

            EnableBuildingAsTrashEntry = Config.Bind("config", "EnableBuildingAsTrashEntry", false,
                new ConfigDescription(
                    "Whether buildings can input into Trash Recycle Fractionator (recommended disable).\n"
                    + "建筑能否输入垃圾回收分馏塔（建议关闭）。",
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
            CurrentVersionEntry.Value = VERSION;
            AddedBlueprintsEntry.Value = true;
            configFile.Save();
        }

        /**
         * 更新自定义设置项。
         * 在点击设置-杂项的应用按钮时执行。
         */
        [SuppressMessage("ReSharper", "ParameterHidesMember")]
        public static void SetConfig(int iconVersion, bool enableDestroy,
            bool enableFuelRodFrac, bool enableMatrixFrac, bool enableBuildingAsTrash) {
            bool iconVersionChanged = iconVersion != IconVersionEntry.Value;
            bool enableDestroyChanged = enableDestroy != EnableDestroyEntry.Value;
            bool enableFuelRodFracChanged = enableFuelRodFrac != EnableFuelRodFracEntry.Value;
            bool enableMatrixFracChanged = enableMatrixFrac != EnableMatrixFracEntry.Value;
            bool enableBuildingAsTrashChanged = enableBuildingAsTrash != EnableBuildingAsTrashEntry.Value;
            //修改配置文件里面的内容
            IconVersionEntry.Value = iconVersion;
            EnableDestroyEntry.Value = enableDestroy;
            EnableFuelRodFracEntry.Value = enableFuelRodFrac;
            EnableMatrixFracEntry.Value = enableMatrixFrac;
            logger.LogInfo($"Fractionate Everything setting changed.\n"
                           + $" iconVersion:{iconVersion}"
                           + $" enableDestroy:{enableDestroy}"
                           + $" enableFuelRodFrac:{enableFuelRodFrac}"
                           + $" enableMatrixFrac:{enableMatrixFrac}");
            configFile.Save();
            //重新加载所有分馏配方，玩家需要重新载入存档
            if (iconVersionChanged || enableDestroyChanged || enableFuelRodFracChanged || enableMatrixFracChanged) {
                foreach (RecipeProto r in LDB.recipes.dataArray) {
                    if (r.Type != ERecipeType.Fractionate || r.ID == R重氢分馏_GB氦闪约束器) {
                        continue;
                    }
                    r.ModifyIconAndDesc();
                    r.Preload(r.index);
                }
            }
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

                Translation.AddTranslations();

                LoadConfig();

                tab分馏1 = TabSystem.RegisterTab($"{GUID}:{GUID}Tab1", new("分馏页面1".Translate(), Tech1134IconPath));
                tab分馏2 = TabSystem.RegisterTab($"{GUID}:{GUID}Tab2", new("分馏页面2".Translate(), Tech1134IconPath));

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
            }
        }

        public void PreAddData() {
            //添加新科技
            Tech.AddTechs();
            //创建新的分馏塔
            FractionatorBuildings.CreateAndPreAddNewFractionators();
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
            FractionatorBuildings.SetUnlockInfo();
        }

        /// <summary>
        /// 在所有内容添加完毕后，再执行的代码。
        /// </summary>
        public static void FinalAction() {
            if (_finished) return;
            PreloadAndInitAll();
            //↓↓↓这两个顺序不能变，SetFractionatorCacheSize用到了Init生成的数据↓↓↓
            FractionatorLogic.Init();
            FractionatorBuildings.SetFractionatorCacheSize();
            //↑↑↑这两个顺序不能变，SetFractionatorCacheSize用到了Init生成的数据↑↑↑
            FractionateRecipes.AddFracRecipes();
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

        /*
         * 在设想中，每个物品都有其对应的分馏配方。
         * 数据结构如下：
         *
         *
         *
         *
         */

        public void Import(BinaryReader binaryReader) {
            if (DSPGame.Game == null) {
                return;
            }
            // string version = binaryReader.ReadString();
            // LogError("Frac Import()");
        }

        public void Export(BinaryWriter binaryWriter) {
            if (DSPGame.Game == null) {
                return;
            }
            // binaryWriter.Write(VERSION);
            // LogError("Frac Export()");
        }

        public void IntoOtherSave() {
            // 在标题演示的工厂加载中也会调用它，但不应该使用该模块的功能，因此不要使用 ResetNextIds() 以降低成本。
            if (DSPGame.Game == null || DSPGame.IsMenuDemo) {
                return;
            }
            // LogError("Frac IntoOtherSave()");
        }

        #endregion
    }
}
