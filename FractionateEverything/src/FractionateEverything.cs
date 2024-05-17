using BepInEx;
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
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
        public const string VERSION = "1.3.1";

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

        private static readonly Regex regex = new(".+-.+-v[1-3]");
        internal static string ModPath;

        public static int tab分馏1;
        public static int tab分馏2;
        public static ResourceData fracicons;

        /// <summary>
        /// 所有分馏配方概率
        /// </summary>
        public static readonly Dictionary<int, Dictionary<int, double>> fracRecipeNumRatioDic = [];
        /// <summary>
        /// 所有分馏产物为自身的配方
        /// </summary>
        public static readonly List<int> fracSelfRecipeList = [];
#if DEBUG
        /// <summary>
        /// sprite名称将被记录在该文件中。
        /// </summary>
        public const string SPRITE_CSV_PATH =
            @"D:\project\csharp\DSP MOD\MLJ_DSPmods\GetDspData\gamedata\fracIconPath.csv";
#endif

        #endregion

        public void Awake() {
            using (ProtoRegistry.StartModLoad(GUID)) {
                logger = Logger;

                Translation.RegisterTranslations();

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
                    new(typeof(AddFractionateRecipes),
                        nameof(AddFractionateRecipes.AddFracRecipesAfterLDBToolPostAddData)) {
                        after = [LDBToolPlugin.MODGUID]
                    }
                );
            }
        }

        public void LoadConfig() {
            configFile = Config;

            DisableMessageBoxEntry = Config.Bind("config", "DisableMessageBox", false,
                new ConfigDescription(
                    "Don't show message when FractionateEverything is loaded.\n"
                    + "是否禁用首次加载时的提示信息。",
                    new AcceptableBoolValue(false), null));

            IconVersionEntry = Config.Bind("config", "IconVersion", 3,
                new ConfigDescription(
                    "Which version of the fractionation icon to use.\n"
                    + "1 for the original deuterium fractionation style, 2 for the straight line segmentation style, 3 for the circular segmentation style (recommended).\n"
                    + "使用哪个版本的分馏图标。\n"
                    + "1表示原版重氢分馏样式，2表示直线分割样式，3表示圆弧分割样式（推荐）。",
                    new AcceptableIntValue(3, 1, 3), null));

            EnableDestroyEntry = Config.Bind("config", "EnableDestroy", true,
                new ConfigDescription(
                    "Whether or not to enable the probability of damage in a fractionated recipe.\n"
                    + "Fractionation recipes with a probability of destruction (usually matrix) fractionate with a probability of destruction of the feedstock when enabled (recommended).\n"
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
            //替换图标样式与分馏，不生效，似乎不能动态修改
            // foreach (var r in LDB.recipes.dataArray) {
            //     if (r.Type != ERecipeType.Fractionate) {
            //         continue;
            //     }
            //     if (regex.IsMatch(r.IconPath)) {
            //         string newIconPath = r.IconPath.Substring(0, r.IconPath.Length - 1) + iconVersion;
            //         Sprite sprite = Resources.Load<Sprite>(r.IconPath);
            //         if (sprite != null) {
            //             r.IconPath = newIconPath;
            //             Traverse.Create(r).Field("_iconSprite").SetValue(sprite);
            //         }
            //         var inputItem = LDB.items.Select(r.Items[0]);
            //         var outputItem = LDB.items.Select(r.Results[0]);
            //         if (!fracRecipeNumRatioDic.TryGetValue(r.Items[0], out var dic)) {
            //             dic = new() { { 1, 0.01 } };
            //         }
            //         string description =
            //             $"{"从".Translate()}{inputItem.name}{"中分馏出".Translate()}{outputItem.name}{"。".Translate()}";
            //         foreach (var p in dic.Where(p => p.Key > 0)) {
            //             description += $"\n{p.Value:0.###%}{"分馏出".Translate()}{p.Key}{"个产物".Translate()}";
            //         }
            //         if (dic.TryGetValue(-1, out double destroyRatio)) {
            //             description += $"\n{"损毁分馏警告1".Translate()}{destroyRatio:0.###%}{"损毁分馏警告2".Translate()}";
            //         }
            //         r.description = description;
            //     }
            // }
        }

        public void PreAddData() {
            //添加新科技
            Tech.AddTechs();
            //调整原版分馏塔，移动部分物品、配方的位置
            FractionatorBuilding.OriginFractionatorAdaptation();
            //创建新的分馏塔
            FractionatorBuilding.CreateAndPreAddNewFractionators();
        }

        public void PostAddData() {
#if DEBUG
            if (File.Exists(SPRITE_CSV_PATH)) {
                File.Delete(SPRITE_CSV_PATH);
            }
#endif
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
            FractionatorBuilding.SetUnlockInfo();
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
