﻿using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CommonAPI;
using CommonAPI.Systems;
using CommonAPI.Systems.ModLocalization;
using FractionateEverything.Compatibility;
using FractionateEverything.Main;
using FractionateEverything.Utils;
using HarmonyLib;
using System;
using System.Collections;
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
    public class FractionateEverything : BaseUnityPlugin {
        public const string GUID = "com.menglei.dsp." + NAME;
        public const string NAME = "FractionateEverything";
        public const string VERSION = "1.3.5";

        #region Logger

        private static ManualLogSource logger;
        public static void LogDebug(object data) => logger.LogDebug(data);
        public static void LogInfo(object data) => logger.LogInfo(data);
        public static void LogWarning(object data) => logger.LogWarning(data);
        public static void LogError(object data) => logger.LogError(data);
        public static void LogFatal(object data) => logger.LogFatal(data);

        #endregion

        #region Fields

        private static ConfigFile configFile;
        private static ConfigEntry<bool> DisableMessageBoxEntry;
        /// <summary>
        /// 是否在游戏加载时禁用提示信息。
        /// </summary>
        public static bool disableMessageBox => DisableMessageBoxEntry.Value;
        private static ConfigEntry<int> IconVersionEntry;
        /// <summary>
        /// 分馏图标样式。
        /// </summary>
        public static int iconVersion => IconVersionEntry.Value;
        private static ConfigEntry<bool> EnableDestroyEntry;
        /// <summary>
        /// 是否启用分馏配方中的损毁概率。
        /// </summary>
        public static bool enableDestroy => EnableDestroyEntry.Value;

        public static int tab分馏1;
        public static int tab分馏2;
        public static string ModPath;
        public static ResourceData fracicons;

        #endregion

        public void Awake() {
            using (ProtoRegistry.StartModLoad(GUID)) {
                logger = Logger;

                Translation.AddTranslations();

                LoadConfig();

                string iconPath = LDB.techs.Select(T重氢分馏_GB强相互作用力材料).IconPath;
                tab分馏1 = TabSystem.RegisterTab($"{GUID}:{GUID}Tab1", new("分馏页面1".Translate(), iconPath));
                tab分馏2 = TabSystem.RegisterTab($"{GUID}:{GUID}Tab2", new("分馏页面2".Translate(), iconPath));

                var executingAssembly = Assembly.GetExecutingAssembly();
                ModPath = Path.GetDirectoryName(executingAssembly.Location);
                fracicons = new(GUID, "fracicons", ModPath);
                fracicons.LoadAssetBundle("fracicons");
                ProtoRegistry.AddResource(fracicons);

                LDBTool.PreAddDataAction += PreAddData;
                LDBTool.PostAddDataAction += PostAddData;

                Harmony harmony = new(GUID);
                foreach (Type type in executingAssembly.GetTypes()) {
                    //FractionateEverything.Compatibility内的类由自己patch，不在这里处理
                    if (type.Namespace == null
                        || type.Namespace.StartsWith("FractionateEverything.Compatibility")) {
                        continue;
                    }
                    harmony.PatchAll(type);
                }
                //在所有物品、配方添加结束后（即LDBTool已执行PostAddData），添加分馏配方
                harmony.Patch(
                    AccessTools.Method(typeof(VFPreload), "InvokeOnLoadWorkEnded"),
                    null,
                    new(typeof(FractionateRecipes), nameof(FractionateRecipes.AddFracRecipesAfterLDBToolPostAddData)) {
                        after = [LDBToolPlugin.MODGUID]
                    }
                );
                //在载入语言时、CommonAPIPlugin添加翻译后，添加额外的所有翻译
                harmony.Patch(
                    AccessTools.Method(typeof(Localization), "LoadLanguage"),
                    null,
                    new(typeof(TranslationUtils),
                        nameof(TranslationUtils.LoadLanguagePostfixAfterCommonApi)) {
                        after = [CommonAPIPlugin.GUID]
                    }
                );
            }
        }

        public void LoadConfig() {
            configFile = Config;

            DisableMessageBoxEntry = Config.Bind("config", "DisableMessageBox", false,
                new ConfigDescription(
                    "Don't show message when FractionateEverything is loaded.\n"
                    + "禁用游戏加载完成后显示的万物分馏提示信息。",
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
                    "Whether to enable the probability of destruction in fractionate recipes.\n"
                    + "When enabled, Fractionation recipes with a probability of destruction (usually matrix) fractionate with a probability of destruction of the feedstock (recommended).\n"
                    + "是否启用分馏配方中的损毁概率。\n"
                    + "启用情况下，有损毁概率的分馏配方（通常为矩阵）分馏时原料有概率损毁（推荐）。",
                    new AcceptableBoolValue(true), null));

            //移除之前多余的设置项，然后保存
            (Traverse.Create(Config).Property("OrphanedEntries").GetValue() as IDictionary)?.Clear();
            Config.Save();
        }

        internal static void SetConfig(bool disableMessageBox, int iconVersion, bool enableDestroy) {
            DisableMessageBoxEntry.Value = disableMessageBox;
            IconVersionEntry.Value = iconVersion;
            EnableDestroyEntry.Value = enableDestroy;
            logger.LogInfo($"Fractionate Everything setting changed.\n"
                           + $"disableMessageBox:{disableMessageBox}"
                           + $" iconVersion:{iconVersion}"
                           + $" enableDestroy:{enableDestroy}");
            configFile.Save();
            //替换图标样式与分馏，需要重新载入存档
            TranslationUtils.LoadLanguagePostfixAfterCommonApi();
            foreach (var r in LDB.recipes.dataArray) {
                if (r.Type != ERecipeType.Fractionate) {
                    continue;
                }
                r.ModifyIconAndDesc();
                r.Preload(r.index);
            }
        }

        public void PreAddData() {
            //添加新科技
            Tech.AddTechs();
            //调整原版分馏塔，移动部分物品、配方的位置
            FractionatorBuildings.OriginFractionatorAdaptation();
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
        }
    }
}
