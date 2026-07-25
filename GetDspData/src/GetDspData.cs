using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using CommonAPI;
using crecheng.DSPModSave;
using FE.Logic.Fractionation.Process;
using FE.Logic.Fractionation.FracRecipes;
using FE.Logic.Items;
using GetDspData.Utils;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using xiaoye97;
using static BepInEx.BepInDependency.DependencyFlags;
using static FE.Logic.Fractionation.FracRecipes.RecipeManager;
using static FE.Logic.Fractionation.FracRecipes.ERecipeExtension;
using static GetDspData.Utils.Utils;

namespace GetDspData;
//item.UnlockKey
//UnlockKey>0：跟随解锁，例如蓄电器（满）是跟随蓄电器解锁的
//UnlockKey=0：由科技解锁
//UnlockKey=-1：直接解锁
//UnlockKey=-2：由黑雾掉落

//LDB.ItemName 等价于 item.name，item.name 等价于 item.Name.Translate()
//name: 推进器  name.Translate: <0xa0>-<0xa0>推进器  Name: 推进器2  Name.Translate: 推进器
//name: Thruster  name.Translate: Thruster  Name: 推进器2  Name.Translate: Thruster
//name: 制造台<0xa0>Mk.I  name.Translate: 制造台<0xa0>Mk.I  Name: 制造台 Mk.I  Name.Translate: 制造台<0xa0>Mk.I
//name: Assembling Machine Mk.I  name.Translate: Assembling Machine Mk.I  Name: 制造台 Mk.I  Name.Translate: Assembling Machine Mk.I

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency(LDBToolPlugin.MODGUID)]
[BepInDependency(DSPModSavePlugin.MODGUID)]
[BepInDependency(CommonAPIPlugin.GUID)]
[BepInDependency(MoreMegaStructureGUID, SoftDependency)]
[BepInDependency(TheyComeFromVoidGUID, SoftDependency)]
[BepInDependency(GenesisBookGUID, SoftDependency)]
[BepInDependency(OrbitalRingGUID, SoftDependency)]
[BepInDependency(FractionateEverythingGUID, SoftDependency)]
public class GetDspData : BaseUnityPlugin {
    private const string DynamicIconPathPrefix = "Assets/texpack/";
    private const int ExportIconSize = 80;
    private const string SolutionDir = @"D:\project\dsp\MLJ_DSPmods";
    private const string IconExportRequestPath =
        SolutionDir + @"\gamedata\calc-icon-export-request.json";

    #region Logger

    private static ManualLogSource logger;

    public static void LogDebug(object data) {
        if (logger == null) {
            Debug.Log($"[{PluginInfo.PLUGIN_NAME}][Debug]{data}");
        } else {
            logger.LogDebug(data);
        }
    }

    public static void LogInfo(object data) {
        if (logger == null) {
            Debug.Log($"[{PluginInfo.PLUGIN_NAME}][Info]{data}");
        } else {
            logger.LogInfo(data);
        }
    }

    public static void LogWarning(object data) {
        if (logger == null) {
            Debug.Log($"[{PluginInfo.PLUGIN_NAME}][Warning]{data}");
        } else {
            logger.LogWarning(data);
        }
    }

    public static void LogError(object data) {
        if (logger == null) {
            Debug.Log($"[{PluginInfo.PLUGIN_NAME}][Error]{data}");
        } else {
            logger.LogError(data);
        }
    }

    public static void LogFatal(object data) {
        if (logger == null) {
            Debug.Log($"[{PluginInfo.PLUGIN_NAME}][Fatal]{data}");
        } else {
            logger.LogFatal(data);
        }
    }

    #endregion

    private static string dir = null;
    public const string MoreMegaStructureGUID = "Gnimaerd.DSP.plugin.MoreMegaStructure";
    public static bool MoreMegaStructureEnable;
    public const string TheyComeFromVoidGUID = "com.ckcz123.DSP_Battle";
    public static bool TheyComeFromVoidEnable;
    public const string GenesisBookGUID = "org.LoShin.GenesisBook";
    public const string GBMSHarmonyPatchID = "ProjectGenesis.Compatibility.Gnimaerd.DSP.plugin.MoreMegaStructure";
    public static bool GenesisBookEnable;
    public const string OrbitalRingGUID = "org.ProfessorCat305.OrbitalRing";
    public const string ORMSHarmonyPatchID = "ProjectOrbitalRing.Compatibility.Gnimaerd.DSP.plugin.MoreMegaStructure";
    public static bool OrbitalRingEnable;
    public const string FractionateEverythingGUID = "com.menglei.dsp.fe";
    public static bool FractionateEverythingEnable;

    public void Awake() {
        logger = Logger;

        if (!Directory.Exists(SolutionDir)) {
            return;
        }
        dir = $@"{SolutionDir}\gamedata";
        if (!Directory.Exists(dir)) {
            Directory.CreateDirectory(dir);
        }
        MoreMegaStructureEnable = Chainloader.PluginInfos.ContainsKey(MoreMegaStructureGUID);
        TheyComeFromVoidEnable = Chainloader.PluginInfos.ContainsKey(TheyComeFromVoidGUID);
        GenesisBookEnable = Chainloader.PluginInfos.ContainsKey(GenesisBookGUID);
        OrbitalRingEnable = Chainloader.PluginInfos.ContainsKey(OrbitalRingGUID);
        FractionateEverythingEnable = Chainloader.PluginInfos.ContainsKey(FractionateEverythingGUID);

        Harmony harmony = new(PluginInfo.PLUGIN_GUID);
        harmony.Patch(
            AccessTools.Method(typeof(VFPreload), nameof(VFPreload.InvokeOnLoadWorkEnded)),
            null,
            new(typeof(GetDspData), nameof(WriteDataToFile)) {
                after = [
                    LDBToolPlugin.MODGUID,
                    MoreMegaStructureGUID,
                    TheyComeFromVoidGUID,
                    GenesisBookGUID,
                    GBMSHarmonyPatchID,
                    OrbitalRingGUID,
                    ORMSHarmonyPatchID,
                    FractionateEverythingGUID,
                ],
                priority = Priority.Last,
            }
        );
    }

    /// <summary>
    /// 在全部依赖模组加载完成后导出开发与计算器所需数据文件。
    /// </summary>
    private static void WriteDataToFile() {
        if (Localization.CurrentLanguageLCID != Localization.LCID_ZHCN) {
            Localization.CurrentLanguageLCID = Localization.LCID_ZHCN;
        }

        //如果当前语言索引非中文，直接返回
        if (Localization.CurrentLanguageLCID != Localization.LCID_ZHCN) {
            LogWarning("当前语言非中文，不生成数据文件");
            return;
        }

        //冲突的mod同时启用时，直接返回
        if (GenesisBookEnable && OrbitalRingEnable) {
            LogWarning("创世与星环同时启用");
            return;
        }

        //检测兼容补丁是否已执行完毕
        if (MoreMegaStructureEnable) {
            if (GenesisBookEnable && !Harmony.HasAnyPatches(GBMSHarmonyPatchID)) {
                LogFatal("未能正常patch");
                return;
            }
            if (OrbitalRingEnable && !Harmony.HasAnyPatches(ORMSHarmonyPatchID)) {
                LogFatal("未能正常patch");
                return;
            }
        }

        InitMiningData();

        try {
            var (fullName, shortName) = ResolveAndLogEnabledModCombination();
            string baseDir = EnsureExportDirectory(shortName);
            WriteProtoIdFile(baseDir);
            if (FractionateEverythingEnable) {
                SaveFEItemValue(baseDir);
            }
            WriteGameDataCsv(baseDir);
            string dirCalc = EnsureCalculatorJsonDirectory();
            JObject dataObj = BuildCalculatorData();
            WriteCalculatorJson(dirCalc, fullName, dataObj);
            ExportRequestedIcons();
        }
        catch (Exception ex) {
            LogError(ex.ToString());
        }
    }

    /// <summary>
    /// 根据当前启用的计算器相关模组拼接导出命名，并记录启用日志。
    /// </summary>
    /// <returns>fullName 为完整模组名下划线组合；shortName 为短名组合或 Vanilla。</returns>
    private static (string fullName, string shortName) ResolveAndLogEnabledModCombination() {
        string modFullStr = "";
        string modShortStr = "";
        (bool enabled, string fullName, string shortName)[] calcMods = [
            (MoreMegaStructureEnable, "MoreMegaStructure", "MS"),
            (TheyComeFromVoidEnable, "TheyComeFromVoid", "VD"),
            (GenesisBookEnable, "GenesisBook", "GB"),
            (OrbitalRingEnable, "OrbitalRing", "OR"),
            (FractionateEverythingEnable, "FractionateEverything", "FE"),
        ];
        foreach (var calcMod in calcMods) {
            if (calcMod.enabled) {
                LogInfo($"已启用{calcMod.fullName}");
                modFullStr += "_" + calcMod.fullName;
                modShortStr += "_" + calcMod.shortName;
            }
        }
        modShortStr = modShortStr == "" ? "Vanilla" : modShortStr.Substring(1);
        modFullStr = modFullStr == "" ? "Vanilla" : modFullStr.Substring(1);
        return (modFullStr, modShortStr);
    }

    /// <summary>
    /// 确保当前模组组合的导出根目录存在。
    /// </summary>
    /// <param name="modShortStr">短名组合目录名。</param>
    /// <returns>导出根目录绝对路径。</returns>
    private static string EnsureExportDirectory(string modShortStr) {
        string baseDir = $@"{dir}\{modShortStr}";
        if (!Directory.Exists(baseDir)) {
            Directory.CreateDirectory(baseDir);
        }
        return baseDir;
    }

    /// <summary>
    /// 写出代码中使用的 protoID 常量快照到 protoID.txt。
    /// </summary>
    /// <param name="baseDir">当前模组组合导出目录。</param>
    private static void WriteProtoIdFile(string baseDir) {
        string filePath = $@"{baseDir}\protoID.txt";
        if (!File.Exists(filePath)) {
            File.Create(filePath).Close();
        }
        using (var sw = new StreamWriter(filePath, false, Encoding.UTF8)) {
            List<ItemProto> itemList = [..LDB.items.dataArray];
            itemList.Sort((p1, p2) => p1.ID - p2.ID);
            List<(string, int)> modelNameIdList = new();
            foreach (var item in itemList) {
                int id = item.ID;
                string name = item.FName();
                sw.WriteLine($"    internal const int I{name} = {id};");
                int modelID = item.ModelIndex;
                if (modelID > 0) {
                    modelNameIdList.Add((name, modelID));
                }
            }
            sw.WriteLine();

            modelNameIdList.Sort((p1, p2) => p1.Item2 - p2.Item2);
            foreach (var p in modelNameIdList) {
                sw.WriteLine($"    internal const int M{p.Item1} = {p.Item2};");
            }
            sw.WriteLine();

            List<RecipeProto> recipeList = [..LDB.recipes.dataArray];
            recipeList.Sort((p1, p2) => p1.ID - p2.ID);
            foreach (var recipe in recipeList) {
                int id = recipe.ID;
                string name = recipe.FName();
                sw.WriteLine($"    internal const int R{name} = {id};");
            }
            sw.WriteLine();

            List<TechProto> techList = [..LDB.techs.dataArray];
            techList.Sort((p1, p2) => p1.ID - p2.ID);
            string lastTechName = "";
            foreach (var tech in techList) {
                int id = tech.ID;
                string name = tech.FName();
                if (name == lastTechName) {
                    continue;
                }
                lastTechName = name;
                sw.WriteLine($"    internal const int T{name} = {id};");
            }
        }
    }

    /// <summary>
    /// 写出物品、配方、科技与模型的 gameData.csv 文本快照。
    /// </summary>
    /// <param name="baseDir">当前模组组合导出目录。</param>
    private static void WriteGameDataCsv(string baseDir) {
        string filePath = $@"{baseDir}\gameData.csv";
        if (!File.Exists(filePath)) {
            File.Create(filePath).Close();
        }
        using (var sw = new StreamWriter(filePath, false, Encoding.UTF8)) {
            sw.WriteLine("物品ID,GridIndex,name,IconPath,EItemType,BuildMode,BuildIndex,MainCraft,UnlockKey,PreTech");
            //先处理有配方的物品
            List<ItemProto> itemProtoList1 = [..LDB.items.dataArray];
            itemProtoList1.RemoveAll(item => !LDB.recipes.dataArray
                .Any(recipe => recipe.Items.Contains(item.ID) || recipe.Results.Contains(item.ID)));
            //再处理无配方的物品
            List<ItemProto> itemProtoList2 = [..LDB.items.dataArray];
            itemProtoList2.RemoveAll(item => itemProtoList1.Contains(item));
            //拼起来，处理
            List<ItemProto> itemProtoList = itemProtoList1.Concat(itemProtoList2).ToList();
            foreach (var item in itemProtoList) {
                sw.Write($"I{item.ID},");
                sw.Write($"{item.GridIndex},");
                sw.Write($"{item.FName()},");
                sw.Write($"{item.IconPath},");
                sw.Write($"{Enum.GetName(typeof(EItemType), (int)item.Type)},");
                sw.Write($"{item.BuildMode},");
                sw.Write($"{item.BuildIndex},");
                sw.Write($"{(item.maincraft == null ? "null" : ("R" + item.maincraft.ID))},");
                sw.Write($"{item.UnlockKey},");
                sw.Write($"{(item.preTech == null ? "null" : ("T" + item.preTech.ID))},");
                sw.WriteLine();
            }
            sw.WriteLine();
            sw.WriteLine();

            sw.WriteLine(
                "配方ID,GridIndex,name,IconPath,ERecipeType,Items,Results,TimeSpend,Handcraft,Productive,PreTech");
            foreach (var recipe in LDB.recipes.dataArray) {
                sw.Write($"R{recipe.ID},");
                sw.Write($"{recipe.GridIndex},");
                sw.Write($"{recipe.FName()},");
                sw.Write($"{recipe.IconPath},");
                if (GenesisBookEnable) {
                    sw.Write($"{Enum.GetName(typeof(ERecipeType_GB), (int)recipe.Type)},");
                } else if (OrbitalRingEnable) {
                    sw.Write($"{Enum.GetName(typeof(ERecipeType_OR), (int)recipe.Type)},");
                } else {
                    sw.Write($"{Enum.GetName(typeof(ERecipeType_DSP), (int)recipe.Type)},");
                }
                StringBuilder sb = new();
                bool first = true;
                for (int i = 0; i < recipe.Items.Length; i++) {
                    if (!first) {
                        sb.Append(" + ");
                    }
                    first = false;
                    sb.Append(
                        $"{LDB.items.Select(recipe.Items[i]).FName()}(I{recipe.Items[i]})*{recipe.ItemCounts[i]}");
                }
                sw.Write($"{sb},");
                sb = new();
                first = true;
                for (int i = 0; i < recipe.Results.Length; i++) {
                    if (!first) {
                        sb.Append(" + ");
                    }
                    first = false;
                    sb.Append(
                        $"{LDB.items.Select(recipe.Results[i]).FName()}(I{recipe.Results[i]})*{recipe.ResultCounts[i]}");
                }
                sw.Write($"{sb},");
                sw.Write($"{recipe.TimeSpend}({(recipe.TimeSpend / 60.0f):F1}s),");
                sw.Write($"{recipe.Handcraft},");
                sw.Write($"{recipe.productive},");
                sw.Write($"{(recipe.preTech == null ? "null" : ("T" + recipe.preTech.ID))},");
                sw.WriteLine();
            }
            sw.WriteLine();
            sw.WriteLine();

            sw.WriteLine(
                "科技ID,name,IconPath,PreTechs,PreTechsImplicit,IsHiddenTech,PreItem,Items,HashNeeded,UnlockRecipes");
            foreach (var tech in LDB.techs.dataArray) {
                sw.Write($"T{tech.ID},");
                sw.Write($"{tech.FName()},");
                sw.Write($"{tech.IconPath},");
                if (tech.PreTechs != null && tech.PreTechs.Length > 0) {
                    StringBuilder sb = new();
                    bool first = true;
                    for (int i = 0; i < tech.PreTechs.Length; i++) {
                        if (!first) {
                            sb.Append("_");
                        }
                        first = false;
                        sb.Append($"{LDB.techs.Select(tech.PreTechs[i]).FName()}(T{tech.PreTechs[i]})");
                    }
                    sw.Write($"{sb},");
                } else {
                    sw.Write("empty,");
                }
                if (tech.PreTechsImplicit != null && tech.PreTechsImplicit.Length > 0) {
                    StringBuilder sb = new();
                    bool first = true;
                    for (int i = 0; i < tech.PreTechsImplicit.Length; i++) {
                        if (!first) {
                            sb.Append("_");
                        }
                        first = false;
                        sb.Append(
                            $"{LDB.techs.Select(tech.PreTechsImplicit[i]).FName()}(T{tech.PreTechsImplicit[i]})");
                    }
                    sw.Write($"{sb},");
                } else {
                    sw.Write("empty,");
                }
                sw.Write($"{tech.IsHiddenTech},");
                if (tech.PreItem != null && tech.PreItem.Length > 0) {
                    StringBuilder sb = new();
                    bool first = true;
                    for (int i = 0; i < tech.PreItem.Length; i++) {
                        if (!first) {
                            sb.Append("_");
                        }
                        first = false;
                        sb.Append($"{LDB.items.Select(tech.PreItem[i]).FName()}(I{tech.PreItem[i]})");
                    }
                    sw.Write($"{sb},");
                } else {
                    sw.Write("empty,");
                }
                float countMultiple = tech.HashNeeded / 3600f;
                if (tech.Items != null && tech.Items.Length > 0) {
                    StringBuilder sb = new();
                    bool first = true;
                    for (int i = 0; i < tech.Items.Length; i++) {
                        if (!first) {
                            sb.Append(" + ");
                        }
                        first = false;
                        sb.Append(
                            $"{LDB.items.Select(tech.Items[i]).FName()}(I{tech.Items[i]})*{tech.ItemPoints[i] * countMultiple:F0}");
                    }
                    sw.Write($"{sb},");
                } else {
                    sw.Write("empty,");
                }
                sw.Write($"{tech.HashNeeded},");
                if (tech.UnlockRecipes != null && tech.UnlockRecipes.Length > 0) {
                    StringBuilder sb = new();
                    bool first = true;
                    for (int i = 0; i < tech.UnlockRecipes.Length; i++) {
                        if (!first) {
                            sb.Append("_");
                        }
                        first = false;
                        sb.Append($"{LDB.recipes.Select(tech.UnlockRecipes[i]).FName()}(I{tech.UnlockRecipes[i]})");
                    }
                    sw.Write($"{sb},");
                } else {
                    sw.Write("empty,");
                }
                sw.WriteLine();
            }
            sw.WriteLine();
            sw.WriteLine();

            sw.WriteLine("模型ID,name,displayName,PrefabPath");
            foreach (var model in LDB.models.dataArray) {
                sw.WriteLine(model.ID
                             + ","
                             + model.FName()
                             + ","
                             + model.displayName
                             + ","
                             + model.PrefabPath);
            }
        }
    }

    /// <summary>
    /// 确保计算器 JSON 输出目录存在。
    /// </summary>
    /// <returns>calc json 目录绝对路径。</returns>
    private static string EnsureCalculatorJsonDirectory() {
        string dirCalc = $@"{dir}\calc json";
        if (!Directory.Exists(dirCalc)) {
            Directory.CreateDirectory(dirCalc);
        }
        return dirCalc;
    }

    /// <summary>
    /// 组装计算器 JSON 根对象，顺序固定为 recipes、items、techs。
    /// </summary>
    /// <returns>含 recipes/items/techs 的计算器数据对象。</returns>
    private static JObject BuildCalculatorData() {
        var dataObj = new JObject();
        //配方
        var recipes = new JArray();
        dataObj.Add("recipes", recipes);
        foreach (var recipe in LDB.recipes.dataArray) {
            AddRecipe(recipe, recipes);
        }
        //物品
        var items = new JArray();
        dataObj.Add("items", items);
        foreach (var item in LDB.items.dataArray) {
            //添加与这个物品相关的特殊配方（游戏中不存在，但是计算器需要它们来计算）
            AddSpecialRecipesForItem(recipes, item);
        }
        AddReferencedItems(items, recipes);
        //科技
        var techs = new JArray();
        dataObj.Add("techs", techs);
        foreach (var tech in LDB.techs.dataArray) {
            AddTech(tech, techs);
        }
        //特殊物品（无实体的工厂）
        AddVirtualFactoryItems(items);
        return dataObj;
    }

    /// <summary>
    /// 按注释 0..5 的固定顺序为单个物品追加计算器特殊配方。
    /// </summary>
    /// <param name="recipes">计算器配方数组，允许 Insert/Add 修改。</param>
    /// <param name="item">当前遍历到的物品原型。</param>
    private static void AddSpecialRecipesForItem(JArray recipes, ItemProto item) {
        //0.可直接采集的物品（黑雾不算）对应开采配方需要插入到最前，以尽量规避线性规划无解
        int firstIdx = ResolveSpecialRecipeInsertIndex(recipes, item);
        //1.可直接采集
        firstIdx = AddAcquisitionRecipe(recipes, item, firstIdx);
        //2.射线接受站，以及巨构的射线重构站
        firstIdx = AddRayReceiverSpecialRecipes(recipes, item, firstIdx);
        //3.蓄电器空与蓄电器满的相互转化
        AddAccumulatorChargeSpecialRecipes(recipes, item);
        //4.创世满燃料棒烧完变空燃料棒
        AddFuelExhaustRecipe(recipes, item);
        //5.所有万物分馏的配方
        if (FractionateEverythingEnable) {
            AddFracRecipes(recipes, item);
        }
    }

    /// <summary>
    /// 解析可采集物品特殊配方应插入的 recipes 下标。
    /// </summary>
    /// <param name="recipes">当前已生成的计算器配方数组。</param>
    /// <param name="item">目标输出物品。</param>
    /// <returns>插入位置；默认 recipes.Count。</returns>
    private static int ResolveSpecialRecipeInsertIndex(JArray recipes, ItemProto item) {
        int firstIdx = recipes.Count;
        if (item.canMiningFromVein()
            || item.canMiningFromSea()
            || item.canMiningFromOilWell()
            || item.canMiningFromGasGiant()
            || (GenesisBookEnable && item.canMiningFromAtmosphere())
            || item.canMiningByRayReceiver()) {
            foreach (var recipe in recipes) {
                var resultsArr = ((JArray)((JObject)recipe)["Results"]).Values<int>();
                bool contains = false;
                foreach (var id in resultsArr) {
                    if (id == item.ID) {
                        contains = true;
                    }
                }
                if (contains) {
                    firstIdx = recipes.IndexOf(recipe);
                    break;
                }
            }
        }
        return firstIdx;
    }

    /// <summary>
    /// 追加至多一条聚合获取来源配方，覆盖矿脉、海洋、油井、气态、大气、伊卡洛斯、巨构传递与敌人掉落。
    /// </summary>
    /// <param name="recipes">计算器配方数组。</param>
    /// <param name="item">目标输出物品。</param>
    /// <param name="firstIdx">当前插入下标。</param>
    /// <returns>更新后的插入下标。</returns>
    private static int AddAcquisitionRecipe(JArray recipes, ItemProto item, int firstIdx) {
        List<int> factorySpecial = [];
        if (item.canMiningFromVein()) {
            factorySpecial = [..factorySpecial, I采矿机, I大型采矿机];
        }
        if (item.canMiningFromSea()) {
            if (GenesisBookEnable) {
                factorySpecial = [..factorySpecial, I抽水站, IGB聚束液体汲取设施];
            } else if (OrbitalRingEnable) {
                factorySpecial = [..factorySpecial, IOR流体抽取平台];
            } else {
                factorySpecial = [..factorySpecial, I抽水站];
            }
        }
        if (item.canMiningFromOilWell()) {
            if (OrbitalRingEnable) {
                factorySpecial = [..factorySpecial, IOR激光钻井平台];
            } else {
                factorySpecial = [..factorySpecial, I原油萃取站];
            }
        }
        if (item.canMiningFromGasGiant()) {
            factorySpecial = [..factorySpecial, I轨道采集器];
        }
        if (GenesisBookEnable && item.canMiningFromAtmosphere()) {
            factorySpecial = [..factorySpecial, IGB大气采集站];
        }
        if (factorySpecial.Count == 0) {
            bool noNormalRecipe = item.recipes == null || item.recipes.Count == 0;
            if (item.canMiningByIcarus() || (item.GridIndexValid() && noNormalRecipe)) {
                factorySpecial = [..factorySpecial, I伊卡洛斯];
            }
        }
        if (item.canMiningByMS()) {
            factorySpecial = [..factorySpecial, I巨构星际组装厂];
        }
        if (item.GridIndexValid() && item.canDropFromEnemy()) {
            factorySpecial = [..factorySpecial, I行星基地];
        }
        if (factorySpecial.Count > 0) {
            recipes.Insert(firstIdx, new JObject {
                { "Type", -1 },
                { "Factories", new JArray(factorySpecial) },
                { "Name", $"[无中生有]{item.name}" },
                { "Items", new JArray(Array.Empty<int>()) },
                { "ItemCounts", new JArray(Array.Empty<int>()) },
                { "Results", new JArray(new[] { item.ID }) },
                { "ResultCounts", new JArray(new[] { 1 }) },
                { "TimeSpend", 60 },
                { "Proliferator", 0 },
                { "IconName", ResolveIconName(item) },
            });
            firstIdx++;
        }
        return firstIdx;
    }

    /// <summary>
    /// 追加射线接收站/射线重构站相关特殊配方。
    /// </summary>
    /// <param name="recipes">计算器配方数组。</param>
    /// <param name="item">目标输出物品。</param>
    /// <param name="firstIdx">当前插入下标。</param>
    /// <returns>更新后的插入下标。</returns>
    private static int AddRayReceiverSpecialRecipes(JArray recipes, ItemProto item, int firstIdx) {
        if (item.canMiningByRayReceiver()) {
            int buildingID = MoreMegaStructureEnable ? IMS射线重构站 : I射线接收站;
            //不带透镜的公式
            //此公式比较特殊，有明确速率0.1/s，此处不使用大部分“无中生有”的1/s速率
            recipes.Insert(firstIdx, new JObject {
                { "Type", -1 },
                { "Factories", new JArray(new[] { buildingID }) },
                { "Name", $"[无中生有]{item.name}" },
                { "Items", new JArray(Array.Empty<int>()) },
                { "ItemCounts", new JArray(Array.Empty<int>()) },
                { "Results", new JArray(new[] { item.ID }) },
                { "ResultCounts", new JArray(new[] { 1 }) },
                { "TimeSpend", 600 },
                { "Proliferator", 0 },
                { "IconName", ResolveIconName(item) },
            });
            firstIdx++;
            //带透镜的公式
            //此公式比较特殊，有明确的速率0.2/s
            recipes.Insert(firstIdx, new JObject {
                { "Type", -1 },
                { "Factories", new JArray(new[] { buildingID }) },
                { "Name", $"[射线接收带透镜]{item.name}" },
                { "Items", new JArray(new[] { I引力透镜 }) },
                { "ItemCounts", new JArray(new[] { 1.0 / 120.0 }) },
                { "Results", new JArray(new[] { item.ID }) },
                { "ResultCounts", new JArray(new[] { 1 }) },
                { "TimeSpend", 300 },
                { "Proliferator", 4 },
                { "IconName", ResolveIconName(item) },
            });
            firstIdx++;
        }
        return firstIdx;
    }

    /// <summary>
    /// 追加蓄电器空/满相互转化的充电特殊配方。
    /// </summary>
    /// <param name="recipes">计算器配方数组。</param>
    /// <param name="item">当前物品；仅满蓄电器命中时写入。</param>
    private static void AddAccumulatorChargeSpecialRecipes(JArray recipes, ItemProto item) {
        //蓄电器->蓄电器满，两个公式，一个放下充电，一个通过能量枢纽充电
        if (OrbitalRingEnable) {
            //todo: 更新充电速率
            if (item.ID == IOR蓄电器满) {
                recipes.Add(new JObject {
                    { "Type", -1 },
                    { "Factories", new JArray(new[] { IOR蓄电器满 }) },
                    { "Name", $"[充电]{item.name}-放置充电" },
                    { "Items", new JArray(new[] { IOR蓄电器 }) },
                    { "ItemCounts", new JArray(new[] { 1 }) },
                    { "Results", new JArray(new[] { IOR蓄电器满 }) },
                    { "ResultCounts", new JArray(new[] { 1 }) },
                    { "TimeSpend", 21600 },
                    { "Proliferator", 0 },
                    { "IconName", ResolveIconName(item) },
                });
                recipes.Add(new JObject {
                    { "Type", -1 },
                    { "Factories", new JArray(new[] { I能量枢纽 }) },
                    { "Name", $"[充电]{item.name}-能量枢纽充电" },
                    { "Items", new JArray(new[] { IOR蓄电器 }) },
                    { "ItemCounts", new JArray(new[] { 1 }) },
                    { "Results", new JArray(new[] { IOR蓄电器满 }) },
                    { "ResultCounts", new JArray(new[] { 1 }) },
                    { "TimeSpend", 600 },
                    { "Proliferator", 1 },
                    { "IconName", ResolveIconName(item) },
                });
            } else if (item.ID == IOR蓄电器mk2满) {
                recipes.Add(new JObject {
                    { "Type", -1 },
                    { "Factories", new JArray(new[] { IOR蓄电器mk2满 }) },
                    { "Name", $"[充电]{item.name}-放置充电" },
                    { "Items", new JArray(new[] { IOR蓄电器mk2 }) },
                    { "ItemCounts", new JArray(new[] { 1 }) },
                    { "Results", new JArray(new[] { IOR蓄电器mk2满 }) },
                    { "ResultCounts", new JArray(new[] { 1 }) },
                    { "TimeSpend", 21600 },
                    { "Proliferator", 0 },
                    { "IconName", ResolveIconName(item) },
                });
                recipes.Add(new JObject {
                    { "Type", -1 },
                    { "Factories", new JArray(new[] { I能量枢纽 }) },
                    { "Name", $"[充电]{item.name}-能量枢纽充电" },
                    { "Items", new JArray(new[] { IOR蓄电器mk2 }) },
                    { "ItemCounts", new JArray(new[] { 1 }) },
                    { "Results", new JArray(new[] { IOR蓄电器mk2满 }) },
                    { "ResultCounts", new JArray(new[] { 1 }) },
                    { "TimeSpend", 600 },
                    { "Proliferator", 1 },
                    { "IconName", ResolveIconName(item) },
                });
            }
        } else {
            if (item.ID == I蓄电器满) {
                recipes.Add(new JObject {
                    { "Type", -1 },
                    { "Factories", new JArray(new[] { I蓄电器满 }) },
                    { "Name", $"[充电]{item.name}-放置充电" },
                    { "Items", new JArray(new[] { I蓄电器 }) },
                    { "ItemCounts", new JArray(new[] { 1 }) },
                    { "Results", new JArray(new[] { I蓄电器满 }) },
                    { "ResultCounts", new JArray(new[] { 1 }) },
                    { "TimeSpend", 21600 },
                    { "Proliferator", 0 },
                    { "IconName", ResolveIconName(item) },
                });
                recipes.Add(new JObject {
                    { "Type", -1 },
                    { "Factories", new JArray(new[] { I能量枢纽 }) },
                    { "Name", $"[充电]{item.name}-能量枢纽充电" },
                    { "Items", new JArray(new[] { I蓄电器 }) },
                    { "ItemCounts", new JArray(new[] { 1 }) },
                    { "Results", new JArray(new[] { I蓄电器满 }) },
                    { "ResultCounts", new JArray(new[] { 1 }) },
                    { "TimeSpend", 600 },
                    { "Proliferator", 1 },
                    { "IconName", ResolveIconName(item) },
                });
            }
        }
    }

    /// <summary>
    /// 在创世启用时追加燃料棒耗尽变为空燃料棒的特殊配方。
    /// </summary>
    /// <param name="recipes">计算器配方数组。</param>
    /// <param name="item">当前物品；命中燃料棒表时写入。</param>
    private static void AddFuelExhaustRecipe(JArray recipes, ItemProto item) {
        if (GenesisBookEnable) {
            int[] factoryIds = [
                IGB燃料电池发电厂, IGB燃料电池发电厂, IGB燃料电池发电厂,
                IGB裂变能源发电站, IGB裂变能源发电站, IGB裂变能源发电站,
                IGB朱曦K型人造恒星, IGB朱曦K型人造恒星, IGB朱曦K型人造恒星,
                IGB湛曦O型人造恒星, IGB湛曦O型人造恒星,
            ];
            int[] itemIds = [
                I液氢燃料棒, IGB焦油燃料棒, IGB四氢双环戊二烯燃料棒,
                IGB铀燃料棒, IGB钚燃料棒, IGBMOX燃料棒,
                I氘核燃料棒, IGB氦三燃料棒, IGB氘氦混合燃料棒,
                I反物质燃料棒, I奇异湮灭燃料棒,
            ];
            if (itemIds.Contains(item.ID)) {
                int index = itemIds.ToList().IndexOf(item.ID);
                recipes.Add(new JObject {
                    { "Type", -1 },
                    { "Factories", new JArray(new[] { factoryIds[index] }) },
                    { "Name", $"[燃料棒耗尽]{item.name}" },
                    { "Items", new JArray(new[] { item.ID }) },
                    { "ItemCounts", new JArray(new[] { 1 }) },
                    { "Results", new JArray(new[] { IGB空燃料棒 }) },
                    { "ResultCounts", new JArray(new[] { 1 }) },
                    { "TimeSpend", 60 * 60 },//暂时设为60s
                    { "Proliferator", 1 },//暂时先设为1，以便正确计算增产剂使用数目
                    { "IconName", ResolveIconName(item) },
                });
            }
        }
    }

    /// <summary>
    /// 追加计算器使用的虚拟工厂物品（伊卡洛斯、行星基地、巨构星际组装厂）。
    /// </summary>
    /// <param name="items">计算器物品数组。</param>
    private static void AddVirtualFactoryItems(JArray items) {
        items.Add(new JObject {
            { "ID", I伊卡洛斯 },
            { "Type", -1 },
            { "Name", "伊卡洛斯" },
            { "GridIndex", -1 },
            { "IconName", "伊卡洛斯" },
            { "WorkEnergyPerTick", 0.08 / 0.00006 },
            { "Speed", 1.0 / 0.0001 },
            { "MultipleOutput", 1 },
            { "Space", 0 },
        });
        items.Add(new JObject {
            { "ID", I行星基地 },
            { "Type", -1 },
            { "Name", "行星基地" },
            { "GridIndex", -1 },
            { "IconName", "行星基地" },
            { "WorkEnergyPerTick", 0.0 / 0.00006 },
            { "Speed", 1.0 / 0.0001 },
            { "MultipleOutput", 1 },
            { "Space", 0 },
        });
        if (MoreMegaStructureEnable) {
            items.Add(new JObject {
                { "ID", I巨构星际组装厂 },
                { "Type", -1 },
                { "Name", "巨构星际组装厂" },
                { "GridIndex", -1 },
                { "IconName", "巨构星际组装厂" },
                { "WorkEnergyPerTick", 0.0 / 0.00006 },
                { "Speed", 1.0 / 0.0001 },
                { "MultipleOutput", 1 },
                { "Space", 0 },
            });
        }
    }

    /// <summary>
    /// 将计算器数据对象写成 UTF-8 JSON 文件。
    /// </summary>
    /// <param name="dirCalc">calc json 目录。</param>
    /// <param name="modFullStr">完整模组名组合文件名主体。</param>
    /// <param name="dataObj">待写出的 JSON 根对象。</param>
    private static void WriteCalculatorJson(string dirCalc, string modFullStr, JObject dataObj) {
        //保存json到本项目内。文件不复制到戴森球计算器，而是在AfterBuildEvent复制
        string filePath = $@"{dirCalc}\{modFullStr}.json";
        if (!File.Exists(filePath)) {
            File.Create(filePath).Close();
        }
        using (var sw = new StreamWriter(filePath, false, Encoding.UTF8)) {
            sw.WriteLine(dataObj.ToString(Formatting.Indented));
        }
        LogInfo($"已生成{filePath}");
    }

    /// <summary>
    /// 导出 FE 物品价值表，按价值升序写入 itemValue.csv。
    /// </summary>
    /// <param name="baseDir">当前启用模组组合对应的导出目录。</param>
    private static void SaveFEItemValue(string baseDir) {
        //按照从小到大的顺序输出所有物品的价值
        string filePath = $@"{baseDir}\itemValue.csv";
        if (!File.Exists(filePath)) {
            File.Create(filePath).Close();
        }
        using (var sw = new StreamWriter(filePath, false, Encoding.UTF8)) {
            sw.WriteLine("ID,名称,价值");
            Dictionary<int, float> dic = [];
            for (int i = 0; i < ItemManager.itemValue.Length; i++) {
                if (LDB.items.Exist(i)) {
                    dic[i] = ItemManager.itemValue[i];
                }
            }
            foreach (var p in dic.OrderBy(p => p.Value)) {
                ItemProto item = LDB.items.Select(p.Key);
                sw.WriteLine($"{p.Key},{item.name},{p.Value:F2}");
            }
        }
    }

    /// <summary>
    /// 为指定物品追加 FE 四塔分馏配方到计算器 recipes 数组。
    /// 遍历顺序固定为 ERecipe.Cultivation/Analysis/Replication/Conversion（1/2/3/4）。
    /// </summary>
    /// <param name="recipes">计算器配方数组。</param>
    /// <param name="item">输入物品原型。</param>
    private static void AddFracRecipes(JArray recipes, ItemProto item) {
        if (!item.GridIndexValid()) {
            return;
        }

        foreach (var type in RecipeTypes) {
            BaseRecipe recipe = GetRecipe<BaseRecipe>(type, item.ID);
            if (recipe == null) {
                continue;
            }
            ItemProto building = LDB.items.Select(recipe.RecipeType.GetSpriteItemId());
            int fluidInputIncAvg = 0;
            float pointsBonus = (float)ProcessManager.MaxTableMilli(fluidInputIncAvg);
            List<int> Items = [item.ID];
            List<float> ItemCounts = [1];
            List<int> Results = [];
            List<float> ResultCounts = [];
            foreach (var info in recipe.OutputMain) {
                Results.Add(info.OutputID);
                ResultCounts.Add(info.OutputCount);
            }
            foreach (var info in recipe.OutputAppend) {
                Results.Add(info.OutputID);
                ResultCounts.Add(info.OutputCount);
            }
            recipes.Add(new JObject {
                { "Type", -1 },
                { "Factories", new JArray(new[] { building.ID }) },
                { "Name", $"[{recipe.RecipeType.GetShortName()}]{item.name}" },
                { "Items", new JArray(Items) },
                { "ItemCounts", new JArray(ItemCounts) },
                { "Results", new JArray(Results) },
                { "ResultCounts", new JArray(ResultCounts) },
                { "TimeSpend", 0 },
                { "Proliferator", 1 },
                { "SuccessRatio", recipe.SuccessRatio },
                { "DestroyRatio", recipe.DestroyRatio },
                { "RemainInputRatio", 0.0 },
                { "DoubleOutputRatio", 0.0 },
                { "PointsBonus", pointsBonus },
                { "OutputMain", BuildOutputInfoArray(recipe.OutputMain) },
                { "OutputAppend", BuildOutputInfoArray(recipe.OutputAppend) },
                { "IconName", ResolveIconName(item) },
            });
        }
    }

    /// <summary>
    /// 汇总全部计算器配方引用的输入、输出和工厂物品，并按物品 ID 顺序写入 items 数组。
    /// </summary>
    /// <param name="items">待补齐的计算器物品数组。</param>
    /// <param name="recipes">已完成普通与特殊配方组装的计算器配方数组。</param>
    private static void AddReferencedItems(JArray items, JArray recipes) {
        HashSet<int> requiredItemIds = [];
        foreach (JObject recipe in recipes.OfType<JObject>()) {
            AddIds(requiredItemIds, recipe["Items"]);
            AddIds(requiredItemIds, recipe["Results"]);
            AddIds(requiredItemIds, recipe["Factories"]);
        }

        foreach (ItemProto item in LDB.items.dataArray.OrderBy(item => item.ID)) {
            if (requiredItemIds.Contains(item.ID)) {
                AddItem(item, items);
            }
        }
    }

    /// <summary>
    /// 把 JSON 整数数组中的有效 ID 合并到去重集合；非数组或空 token 不产生结果。
    /// </summary>
    /// <param name="target">接收 ID 的去重集合。</param>
    /// <param name="token">可能为配方 Items、Results 或 Factories 的 JSON token。</param>
    private static void AddIds(HashSet<int> target, JToken token) {
        if (token is not JArray array) {
            return;
        }

        foreach (JToken item in array) {
            int? id = item.Value<int?>();
            if (id.HasValue) {
                target.Add(id.Value);
            }
        }
    }

    /// <summary>
    /// 将 FE 输出配置投影为计算器 JSON 使用的 OutputID、OutputCount、SuccessRatio 数组。
    /// </summary>
    /// <param name="outputs">保持原顺序的 FE 输出配置列表。</param>
    /// <returns>与输入列表逐项对应的计算器输出描述数组。</returns>
    private static JArray BuildOutputInfoArray(List<OutputInfo> outputs) {
        var arr = new JArray();
        foreach (var output in outputs) {
            arr.Add(new JObject {
                { "OutputID", output.OutputID },
                { "OutputCount", output.OutputCount },
                { "SuccessRatio", output.SuccessRatio },
            });
        }
        return arr;
    }

    /// <summary>
    /// 解析物品计算器图标名；动态 Assets/texpack 路径优先，否则使用 Sprite 名。
    /// </summary>
    /// <param name="proto">物品原型。</param>
    /// <returns>不含扩展名的图标键；无可用名称时为空字符串。</returns>
    private static string ResolveIconName(ItemProto proto) =>
        ResolveIconName(proto?.IconPath, proto?.iconSprite?.name);

    /// <summary>
    /// 解析配方计算器图标名；动态 Assets/texpack 路径优先，否则使用 Sprite 名。
    /// </summary>
    /// <param name="proto">配方原型。</param>
    /// <returns>不含扩展名的图标键；无可用名称时为空字符串。</returns>
    private static string ResolveIconName(RecipeProto proto) =>
        ResolveIconName(proto?.IconPath, proto?.iconSprite?.name);

    /// <summary>
    /// 从动态图标路径提取最终文件名；路径不匹配时回退到已加载 Sprite 名。
    /// </summary>
    /// <param name="iconPath">原型 IconPath；动态资源以 Assets/texpack/ 开头。</param>
    /// <param name="fallbackIconName">非动态资源使用的 Sprite 名。</param>
    /// <returns>计算器所需图标键；两个来源均不可用时为空字符串。</returns>
    private static string ResolveIconName(string iconPath, string fallbackIconName) {
        if (!string.IsNullOrEmpty(iconPath)
            && iconPath.StartsWith(DynamicIconPathPrefix, StringComparison.OrdinalIgnoreCase)) {
            string dynamicIconName = Path.GetFileName(iconPath.Substring(DynamicIconPathPrefix.Length).Trim());
            if (!string.IsNullOrEmpty(dynamicIconName)) {
                return Path.GetFileNameWithoutExtension(dynamicIconName);
            }
        }
        return fallbackIconName ?? "";
    }

    /// <summary>
    /// 处理 AfterBuildEvent 写入的单次图标请求，将当前 LDB 图标渲染为 PNG 并写出成功/失败 marker。
    /// 请求文件不存在时保持无副作用返回。
    /// </summary>
    private static void ExportRequestedIcons() {
        if (!File.Exists(IconExportRequestPath)) {
            return;
        }

        string markerPath = "";
        try {
            JObject request = JObject.Parse(File.ReadAllText(IconExportRequestPath));
            string targetMod = request.Value<string>("TargetMod") ?? "";
            string outputDir = request.Value<string>("OutputDir") ?? "";
            markerPath = request.Value<string>("MarkerPath") ?? "";
            if (string.IsNullOrWhiteSpace(targetMod)
                || string.IsNullOrWhiteSpace(outputDir)
                || string.IsNullOrWhiteSpace(markerPath)) {
                LogWarning("图标导出请求缺少 TargetMod / OutputDir / MarkerPath");
                return;
            }

            HashSet<string> lowerPriorityIcons = CollectLowerPriorityIcons(request["LowerPriorityDirs"] as JArray);
            HashSet<string> requestedIcons = CollectRequestedIcons(request["IconNames"] as JArray);
            Directory.CreateDirectory(outputDir);

            int exported = 0;
            int skippedNotRequested = 0;
            int skippedLowerPriority = 0;
            int skippedDuplicate = 0;
            int failed = 0;
            HashSet<string> handledIcons = new(StringComparer.OrdinalIgnoreCase);
            foreach ((string iconName, Sprite sprite) in EnumerateCurrentIconSprites()) {
                if (string.IsNullOrWhiteSpace(iconName) || sprite == null) {
                    continue;
                }
                if (requestedIcons.Count > 0 && !requestedIcons.Contains(iconName)) {
                    skippedNotRequested++;
                    continue;
                }
                if (!handledIcons.Add(iconName)) {
                    skippedDuplicate++;
                    continue;
                }
                if (lowerPriorityIcons.Contains(iconName)) {
                    skippedLowerPriority++;
                    continue;
                }

                string outputPath = Path.Combine(outputDir, $"{SanitizeFileName(iconName)}.png");
                if (TryWriteSpritePng(sprite, outputPath)) {
                    exported++;
                } else {
                    failed++;
                }
            }

            JObject marker = new() {
                { "TargetMod", targetMod },
                { "OutputDir", outputDir },
                { "Exported", exported },
                { "SkippedNotRequested", skippedNotRequested },
                { "SkippedLowerPriority", skippedLowerPriority },
                { "SkippedDuplicate", skippedDuplicate },
                { "Failed", failed },
                { "Handled", handledIcons.Count },
            };
            Directory.CreateDirectory(Path.GetDirectoryName(markerPath) ?? ".");
            File.WriteAllText(markerPath, marker.ToString(Formatting.Indented), Encoding.UTF8);
            LogInfo(
                $"已导出 {targetMod} 图标：{exported} 个，跳过非请求 {skippedNotRequested} 个，跳过已存在/低优先级 {skippedLowerPriority} 个，失败 {failed} 个");
        }
        catch (Exception ex) {
            LogError($"导出图标失败：{ex}");
            if (!string.IsNullOrWhiteSpace(markerPath)) {
                JObject marker = new() {
                    { "Failed", true },
                    { "Error", ex.ToString() },
                };
                Directory.CreateDirectory(Path.GetDirectoryName(markerPath) ?? ".");
                File.WriteAllText(markerPath, marker.ToString(Formatting.Indented), Encoding.UTF8);
            }
        }
    }

    /// <summary>
    /// 收集低优先级目录中已存在的 PNG 文件名，用于避免当前模组重复导出。
    /// </summary>
    /// <param name="lowerPriorityDirs">目录路径 JSON 数组；缺失目录会被忽略。</param>
    /// <returns>按大小写不敏感去重的无扩展名图标集合。</returns>
    private static HashSet<string> CollectLowerPriorityIcons(JArray lowerPriorityDirs) {
        HashSet<string> result = new(StringComparer.OrdinalIgnoreCase);
        if (lowerPriorityDirs == null) {
            return result;
        }

        foreach (JToken token in lowerPriorityDirs) {
            string dirPath = token.Value<string>();
            if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath)) {
                continue;
            }

            foreach (string filePath in Directory.GetFiles(dirPath, "*.png")) {
                result.Add(Path.GetFileNameWithoutExtension(filePath));
            }
        }
        return result;
    }

    /// <summary>
    /// 从请求 JSON 中读取需要导出的图标键，并按大小写不敏感去重。
    /// </summary>
    /// <param name="iconNames">图标名 JSON 数组；为 null 时返回空集合。</param>
    /// <returns>非空图标键集合。</returns>
    private static HashSet<string> CollectRequestedIcons(JArray iconNames) {
        HashSet<string> result = new(StringComparer.OrdinalIgnoreCase);
        if (iconNames == null) {
            return result;
        }

        foreach (JToken token in iconNames) {
            string iconName = token.Value<string>();
            if (!string.IsNullOrWhiteSpace(iconName)) {
                result.Add(iconName);
            }
        }
        return result;
    }

    /// <summary>
    /// 按物品、配方、科技的 LDB 顺序枚举当前组合可见的图标键与 Sprite。
    /// </summary>
    /// <returns>允许名称或 Sprite 为空的图标候选序列，由调用方过滤。</returns>
    private static IEnumerable<(string iconName, Sprite sprite)> EnumerateCurrentIconSprites() {
        foreach (ItemProto item in LDB.items.dataArray) {
            yield return (ResolveIconName(item), item.iconSprite);
        }
        foreach (RecipeProto recipe in LDB.recipes.dataArray) {
            yield return (ResolveIconName(recipe), recipe.iconSprite);
        }
        foreach (TechProto tech in LDB.techs.dataArray) {
            yield return (ResolveIconName(tech.IconPath, tech.iconSprite?.name), tech.iconSprite);
        }
    }

    /// <summary>
    /// 将 Sprite 的纹理子矩形渲染到 80x80 透明画布并编码为 PNG；始终恢复 RenderTexture 并释放临时资源。
    /// </summary>
    /// <param name="sprite">待导出的游戏图标 Sprite。</param>
    /// <param name="outputPath">PNG 目标路径；父目录不存在时创建。</param>
    /// <returns>成功写出非空 PNG 时为 <c>true</c>；渲染或写文件失败时记录警告并返回 <c>false</c>。</returns>
    private static bool TryWriteSpritePng(Sprite sprite, string outputPath) {
        Texture2D texture = null;
        Material material = null;
        RenderTexture renderTexture = null;
        RenderTexture previous = RenderTexture.active;

        try {
            Texture2D sourceTexture = sprite.texture;
            Rect rect = sprite.textureRect;
            renderTexture = RenderTexture.GetTemporary(ExportIconSize, ExportIconSize, 0, RenderTextureFormat.ARGB32);
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));

            material = new Material(Shader.Find("Unlit/Transparent"));
            material.mainTextureScale =
                new Vector2(rect.width / sourceTexture.width, rect.height / sourceTexture.height);
            material.mainTextureOffset = new Vector2(rect.x / sourceTexture.width, rect.y / sourceTexture.height);
            Graphics.Blit(sourceTexture, renderTexture, material);

            texture = new Texture2D(ExportIconSize, ExportIconSize, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, ExportIconSize, ExportIconSize), 0, 0);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            if (png == null || png.Length == 0) {
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            File.WriteAllBytes(outputPath, png);
            return true;
        }
        catch (Exception ex) {
            LogWarning($"导出图标失败：{sprite.name} -> {outputPath}，{ex.Message}");
            return false;
        }
        finally {
            RenderTexture.active = previous;
            if (renderTexture != null) {
                RenderTexture.ReleaseTemporary(renderTexture);
            }
            if (material != null) {
                DestroyImmediate(material);
            }
            if (texture != null) {
                DestroyImmediate(texture);
            }
        }
    }

    /// <summary>
    /// 将平台非法文件名字符替换为下划线并移除首尾空白，供 PNG 文件名使用。
    /// </summary>
    /// <param name="fileName">原始图标键。</param>
    /// <returns>可作为单个文件名片段的清理结果。</returns>
    private static string SanitizeFileName(string fileName) {
        foreach (char invalidChar in Path.GetInvalidFileNameChars()) {
            fileName = fileName.Replace(invalidChar, '_');
        }
        return fileName.Trim();
    }

    /// <summary>
    /// 将物品原型序列化为计算器 items 数组元素。
    /// </summary>
    /// <param name="proto">物品原型。</param>
    /// <param name="add">目标 items 数组。</param>
    static void AddItem(ItemProto proto, JArray add) {
        var obj = new JObject {
            { "ID", proto.ID },
            { "Type", (int)proto.Type },
            { "Name", proto.name },
            { "GridIndex", proto.GridIndex },
            { "IconName", ResolveIconName(proto) },
            { "EnemyDropLevel", proto.EnemyDropLevel },
            { "EnemyDropRange", new JArray(proto.EnemyDropRange.x, proto.EnemyDropRange.y) },
            { "EnemyDropCount", proto.EnemyDropCount },
        };
        if (proto.prefabDesc != null) {
            obj.Add("ModelIndex", proto.prefabDesc.modelIndex);
            obj.Add("IsBelt", proto.prefabDesc.isBelt);
            obj.Add("IsInserter", proto.prefabDesc.isInserter);
            obj.Add("IsAssembler", proto.prefabDesc.isAssembler);
            obj.Add("IsLab", proto.prefabDesc.isLab);
            obj.Add("AssemblerRecipeType", (int)proto.prefabDesc.assemblerRecipeType);
            if (proto.prefabDesc.isBelt) {
                obj.Add("BeltSpeedPerSecond", proto.prefabDesc.beltSpeed * 6.0);
            }
            if (proto.prefabDesc.isInserter) {
                obj.Add("InserterGrade", proto.prefabDesc.inserterGrade);
                obj.Add("InserterSTT", proto.prefabDesc.inserterSTT);
            }
        }
        if (proto.GetSpace() >= 0) {
            //对于生产建筑，添加耗能、倍率、占地
            obj.Add("WorkEnergyPerTick", proto.prefabDesc.workEnergyPerTick);
            obj.Add("BuildIndex", proto.BuildIndex);
            obj.Add("MultiLevel", proto.prefabDesc.multiLevel);
            //生产设备速率以倍数显示，10000对应1x，20000对应2x
            //计算公式：(double) this.prefabDesc.assemblerSpeed / 10000.0，单位x（也就是倍数）
            //采矿设备速率以速度显示，600000对应1/s，300000对应2/s
            //计算公式：(60.0 / ((double) this.prefabDesc.minerPeriod / 600000.0) * 科技加成，单位每分钟
            //小矿机初始速度为600000，也就是60/min/矿脉。
            //但是量化计算器配方是1/s，所以需要传入的速度为10000
            if (proto.prefabDesc.isAssembler) {
                obj.Add("Speed", proto.prefabDesc.assemblerSpeed / 10000.0);
            } else if (proto.prefabDesc.isLab) {
                obj.Add("Speed", proto.prefabDesc.labAssembleSpeed / 10000.0);
            } else if (proto.prefabDesc.isCollectStation) {
                obj.Add("Speed", proto.prefabDesc.stationCollectSpeed / 1.0);
            } else if (proto.prefabDesc.minerType != EMinerType.None) {
                obj.Add("Speed", 600000.0 / proto.prefabDesc.minerPeriod);
            } else {
                //其余使用10000速度
                LogWarning($"{proto.name}制造速度设为1.0");
                obj.Add("Speed", 1.0);
            }
            obj.Add("MultipleOutput", GetBuildingMultipleOutput(proto));
            obj.Add("Space", proto.GetSpace());
        }
        add.Add(obj);
    }

    static int GetBuildingMultipleOutput(ItemProto proto) {
        // 这两个双倍产物来自模组 patch 行为，不是原版建筑通用数据，只能按已知建筑硬编码。
        if (GenesisBookEnable && proto.ID == I负熵熔炉) {
            return 2;
        }
        if (OrbitalRingEnable && proto.ID == I量子化工厂) {
            return 2;
        }
        return 1;
    }

    /// <summary>
    /// 将科技原型序列化为计算器 techs 数组元素。
    /// </summary>
    /// <param name="proto">科技原型。</param>
    /// <param name="add">目标 techs 数组。</param>
    static void AddTech(TechProto proto, JArray add) {
        if (proto == null) {
            return;
        }

        var obj = new JObject {
            { "ID", proto.ID },
            { "Name", proto.FName() },
            { "HashNeeded", proto.HashNeeded },
            { "Level", proto.Level },
            { "MaxLevel", proto.MaxLevel },
            { "IsLabTech", proto.IsLabTech },
            { "UnlockFunctions", new JArray(proto.UnlockFunctions ?? Array.Empty<int>()) },
            { "UnlockValues", new JArray(proto.UnlockValues ?? Array.Empty<double>()) },
            { "UnlockRecipes", new JArray(proto.UnlockRecipes ?? Array.Empty<int>()) },
            { "PreTechs", new JArray(proto.PreTechs ?? Array.Empty<int>()) },
            { "PreTechsImplicit", new JArray(proto.PreTechsImplicit ?? Array.Empty<int>()) },
        };
        add.Add(obj);
    }

    /// <summary>
    /// 将游戏配方序列化为计算器 recipes 元素；TimeSpend 字段单位为帧，60 帧对应 1 秒。
    /// </summary>
    /// <param name="proto">配方原型。</param>
    /// <param name="add">目标 recipes 数组。</param>
    static void AddRecipe(RecipeProto proto, JArray add) {
        if (!proto.GridIndexValid()) {
            LogWarning(
                $"proto.GridIndex is not valid, proto: {proto.FName()}({proto.ID}), GridIndex: {proto.GridIndex}");
            return;
        }
        if (proto.Type == ERecipeType.Fractionate) {
            float produceProb = proto.ResultCounts[0] / (float)proto.ItemCounts[0];
            var obj = new JObject {
                { "ID", proto.ID },
                { "Type", (int)proto.Type },
                { "Factories", new JArray(new[] { I分馏塔 }) },
                { "Name", proto.name },
                { "Items", new JArray(new[] { proto.Items[0] }) },
                { "ItemCounts", new JArray(new[] { 1.0 }) },
                { "Results", new JArray(new[] { proto.Results[0] }) },
                { "ResultCounts", new JArray(new[] { 1.0 }) },
                { "TimeSpend", 0 },
                { "Proliferator", 1 },
                { "SuccessRatio", produceProb },
                { "DestroyRatio", 0.0 },
                { "RemainInputRatio", 0.0 },
                { "DoubleOutputRatio", 0.0 }, {
                    "OutputMain", new JArray {
                        new JObject {
                            { "OutputID", proto.Results[0] },
                            { "OutputCount", 1.0 },
                            { "SuccessRatio", 1.0 },
                        }
                    }
                },
                { "OutputAppend", new JArray() },
                { "IconName", ResolveIconName(proto) },
            };
            add.Add(obj);
            return;
        }
        int[] factories;
        try {
            factories = proto.getAcceptFactories();
        }
        catch (Exception ex) {
            //创世+巨构情况下，多功能集成组件被专门设计为抛出异常，因为canMiningByMS已添加对应配方
            LogWarning(ex.ToString());
            return;
        }
        AddRecipe(proto, add, factories);
    }

    /// <summary>
    /// 使用指定工厂列表写出计算器配方；timeSpend 为 0 时沿用原型 TimeSpend。
    /// </summary>
    /// <param name="proto">配方原型。</param>
    /// <param name="add">目标 recipes 数组。</param>
    /// <param name="factories">可接受该配方的工厂物品 ID 列表。</param>
    /// <param name="timeSpend">覆盖用制造时间（帧）；0 表示使用原型值。</param>
    static void AddRecipe(RecipeProto proto, JArray add, int[] factories, float timeSpend = 0.0f) {
        if (factories == null || factories.Length == 0) {
            return;
        }
        if (proto.iconSprite == null) {
            LogError($"proto.iconSprite is null, proto: {proto.FName()}({proto.ID})");
            return;
        }
        //UIItemTip 313-351行
        //增产公式描述1：加速或增产
        //增产公式描述2：加速
        //增产公式描述3：提升分馏概率
        //Proliferator bit0：加速
        //Proliferator bit1：增产
        //Proliferator bit2：接收射线使用引力透镜
        //Proliferator bit3：量子复制
        //Proliferator=0：无
        //Proliferator=1：无，加速
        //Proliferator=2：无，增产
        //Proliferator=3：无，加速，增产
        //Proliferator=4：无，使用引力透镜
        bool flag2 = proto.productive;
        bool flag4 = proto.Type == ERecipeType.Fractionate;
        if (timeSpend == 0.0f) {
            var obj = new JObject {
                { "ID", proto.ID },
                { "Type", (int)proto.Type },
                { "Factories", new JArray(factories) },
                { "Name", proto.name },
                { "Items", new JArray(proto.Items) },
                { "ItemCounts", new JArray(proto.ItemCounts) },
                { "Results", new JArray(proto.Results) },
                { "ResultCounts", new JArray(proto.ResultCounts) },
                { "TimeSpend", proto.TimeSpend },
                { "Proliferator", flag4 || !flag2 ? 1 : 3 },
                { "IconName", ResolveIconName(proto) },
            };
            add.Add(obj);
        } else {
            var obj = new JObject {
                { "ID", proto.ID },
                { "Type", (int)proto.Type },
                { "Factories", new JArray(factories) },
                { "Name", proto.name },
                { "Items", new JArray(proto.Items) },
                { "ItemCounts", new JArray(proto.ItemCounts) },
                { "Results", new JArray(proto.Results) },
                { "ResultCounts", new JArray(proto.ResultCounts) },
                { "TimeSpend", timeSpend },
                { "Proliferator", flag4 || !flag2 ? 1 : 3 },
                { "IconName", ResolveIconName(proto) },
            };
            add.Add(obj);
        }
    }

}
