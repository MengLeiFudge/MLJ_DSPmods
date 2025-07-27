using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using CommonAPI;
using CommonAPI.Systems;
using FE.Logic.Recipe;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using xiaoye97;
using static BepInEx.BepInDependency.DependencyFlags;
using static GetDspData.Utils;

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

[BepInPlugin(GUID, NAME, VERSION)]
[BepInDependency(CommonAPIPlugin.GUID)]
[BepInDependency(MoreMegaStructureGUID, SoftDependency)]
[BepInDependency(TheyComeFromVoidGUID, SoftDependency)]
[BepInDependency(GenesisBookGUID, SoftDependency)]
[BepInDependency(FractionateEverythingGUID, SoftDependency)]
[CommonAPISubmoduleDependency(nameof(ProtoRegistry))]
public class GetDspData : BaseUnityPlugin {
    private const string GUID = "com.menglei.dsp.GetDspData";
    private const string NAME = "Get DSP Data";
    private const string VERSION = "1.0.0";

    #region Logger

    private static ManualLogSource logger;
    public static void LogDebug(object data) => logger.LogDebug(data);
    public static void LogInfo(object data) => logger.LogInfo(data);
    public static void LogWarning(object data) => logger.LogWarning(data);
    public static void LogError(object data) => logger.LogError(data);
    public static void LogFatal(object data) => logger.LogFatal(data);

    #endregion

    private static string dir = null;
    public const string MoreMegaStructureGUID = "Gnimaerd.DSP.plugin.MoreMegaStructure";
    public static bool MoreMegaStructureEnable;
    public const string TheyComeFromVoidGUID = "com.ckcz123.DSP_Battle";
    public static bool TheyComeFromVoidEnable;
    public const string GenesisBookGUID = "org.LoShin.GenesisBook";
    public static bool GenesisBookEnable;
    public const string FractionateEverythingGUID = "com.menglei.dsp.FractionateEverything";
    public static bool FractionateEverythingEnable;

    public void Awake() {
        logger = Logger;

        string GetDspDataPath = $@"D:\project\csharp\DSP MOD\MLJ_DSPmods\GetDspData";
        if (!Directory.Exists(GetDspDataPath)) {
            return;
        }
        dir = $@"{GetDspDataPath}\gamedata";
        if (!Directory.Exists(dir)) {
            Directory.CreateDirectory(dir);
        }
        MoreMegaStructureEnable = Chainloader.PluginInfos.ContainsKey(MoreMegaStructureGUID);
        TheyComeFromVoidEnable = Chainloader.PluginInfos.ContainsKey(TheyComeFromVoidGUID);
        GenesisBookEnable = Chainloader.PluginInfos.ContainsKey(GenesisBookGUID);
        FractionateEverythingEnable = Chainloader.PluginInfos.ContainsKey(FractionateEverythingGUID);

        Harmony harmony = new(GUID);
        harmony.Patch(
            AccessTools.Method(typeof(VFPreload), "InvokeOnLoadWorkEnded"),
            null,
            new(typeof(GetDspData), nameof(WriteDataToFile)) {
                after = [
                    LDBToolPlugin.MODGUID,
                    MoreMegaStructureGUID,
                    TheyComeFromVoidGUID,
                    GenesisBookGUID,
                    FractionateEverythingGUID,
                    "ProjectGenesis.Compatibility.Gnimaerd.DSP.plugin.MoreMegaStructure"
                ],
                priority = Priority.Last,
            }
        );
    }

    private static void WriteDataToFile() {
        if (MoreMegaStructureEnable && GenesisBookEnable) {
            if (Harmony.HasAnyPatches("ProjectGenesis.Compatibility.Gnimaerd.DSP.plugin.MoreMegaStructure")) {
                LogInfo("已正常patch");
            } else {
                LogFatal("未能正常patch");
                return;
            }
        }

        try {
            Dictionary<int, string> itemIdNameDic = new();

            #region 代码中使用

            string filePath = $@"{dir}\DSP_ProtoID.txt";
            if (!File.Exists(filePath)) {
                File.Create(filePath).Close();
            }
            using (var sw = new StreamWriter(filePath, false, Encoding.UTF8)) {
                sw.WriteLine("static class Utils");
                sw.WriteLine("{");

                List<ItemProto> itemList = [..LDB.items.dataArray];
                itemList.Sort((p1, p2) => p1.ID - p2.ID);
                List<(string, int)> modelNameIdList = new();
                foreach (var item in itemList) {
                    int id = item.ID;
                    string name = FormatName(item.name, item.Name);
                    sw.WriteLine($"    internal const int I{name} = {id};");
                    itemIdNameDic.Add(id, name);
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
                    string name = FormatName(recipe.name, recipe.Name);
                    //if (!regex.IsMatch(name)) {
                    sw.WriteLine($"    internal const int R{name} = {id};");
                    //}
                }

                sw.WriteLine();

                List<TechProto> techList = [..LDB.techs.dataArray];
                techList.Sort((p1, p2) => p1.ID - p2.ID);
                string lastTechName = "";
                foreach (var tech in techList) {
                    int id = tech.ID;
                    string name = FormatName(tech.name, tech.Name);
                    if (name == lastTechName) {
                        continue;
                    }
                    lastTechName = name;
                    sw.WriteLine($"    internal const int T{name} = {id};");
                }

                sw.Write("}");
            }

            #endregion

            #region csv数据

            filePath = $@"{dir}\DSP_DataInfo.csv";
            if (!File.Exists(filePath)) {
                File.Create(filePath).Close();
            }
            using (var sw = new StreamWriter(filePath, false, Encoding.UTF8)) {
                sw.WriteLine("物品ID,GridIndex,name,EItemType,BuildMode,BuildIndex,MainCraft,UnlockKey,PreTech");
                foreach (var item in LDB.items.dataArray) {
                    sw.WriteLine(item.ID
                                 + ","
                                 + item.GridIndex
                                 + ","
                                 + itemIdNameDic[item.ID]
                                 + ","
                                 + Enum.GetName(typeof(EItemType), (int)item.Type)
                                 + ","
                                 + item.BuildMode
                                 + ","
                                 + item.BuildIndex
                                 + ","
                                 + (item.maincraft == null ? "null" : item.maincraft.ID)
                                 + ","
                                 + item.UnlockKey
                                 + ","
                                 + (item.preTech == null ? "null" : item.preTech.ID));
                }
                sw.WriteLine();
                sw.WriteLine();

                sw.WriteLine("配方ID,GridIndex,name,ERecipeType,Items,Results,TimeSpend,Handcraft,Productive,PreTech");
                foreach (var recipe in LDB.recipes.dataArray) {
                    int[] itemIDs = recipe.Items;
                    int[] itemCounts = recipe.ItemCounts;
                    int[] resultIDs = recipe.Results;
                    int[] resultCounts = recipe.ResultCounts;
                    float timeSpeed = recipe.TimeSpend / 60.0f;
                    string s = recipe.ID
                               + ","
                               + recipe.GridIndex
                               + ","
                               + FormatName(recipe.name, recipe.Name)
                               + ","
                               + Enum.GetName(typeof(Utils_ERecipeType), (int)recipe.Type)
                               + ",";
                    for (int i = 0; i < itemIDs.Length; i++) {
                        s += itemIdNameDic[itemIDs[i]] + "(" + itemIDs[i] + ")*" + itemCounts[i] + " + ";
                    }
                    s = s.Substring(0, s.Length - 3) + ",";
                    for (int i = 0; i < resultIDs.Length; i++) {
                        s += itemIdNameDic[resultIDs[i]] + "(" + resultIDs[i] + ")*" + resultCounts[i] + " + ";
                    }
                    s = s.Substring(0, s.Length - 3) + ",";
                    s += recipe.TimeSpend
                         + "("
                         + timeSpeed.ToString("F1")
                         + "s)"
                         + ","
                         + recipe.Handcraft
                         + ","
                         + recipe.productive
                         + ","
                         + (recipe.preTech == null ? "null" : recipe.preTech.ID);
                    sw.WriteLine(s);
                }
                sw.WriteLine();
                sw.WriteLine();

                sw.WriteLine("科技ID,name,UnlockRecipes");
                foreach (var tech in LDB.techs.dataArray) {
                    sw.Write(tech.ID + "," + FormatName(tech.name, tech.Name));
                    if (tech.UnlockRecipes != null) {
                        foreach (var recipeID in tech.UnlockRecipes) {
                            RecipeProto recipe = LDB.recipes.Select(recipeID);
                            if (recipe == null) {
                                LogError($"科技{tech.ID}解锁的配方ID{recipeID}不存在");
                                continue;
                            }
                            sw.Write("," + FormatName(recipe.name, recipe.Name));
                        }
                    }
                    sw.WriteLine();
                }
                sw.WriteLine();
                sw.WriteLine();

                sw.WriteLine("模型ID,name,displayName,PrefabPath");
                foreach (var model in LDB.models.dataArray) {
                    sw.WriteLine(model.ID
                                 + ","
                                 + FormatName(model.name, model.Name)
                                 + ","
                                 + model.displayName
                                 + ","
                                 + model.PrefabPath);
                }
            }

            #endregion

            #region 计算器所需数据

            string dirCalc = $@"{dir}\calc json";
            if (!Directory.Exists(dirCalc)) {
                Directory.CreateDirectory(dirCalc);
            }

            string fileName = "";
            bool[] enable =
                [MoreMegaStructureEnable, TheyComeFromVoidEnable, GenesisBookEnable, FractionateEverythingEnable];
            string[] mod = ["MoreMegaStructure", "TheyComeFromVoid", "GenesisBook", "FractionateEverything"];
            for (int i = 0; i < enable.Length; i++) {
                if (enable[i]) {
                    fileName += "_" + mod[i];
                    LogInfo($"已启用{mod[i]}");
                }
            }
            bool isVanilla = fileName == "";
            fileName = isVanilla ? "Vanilla" : fileName.Substring(1);

            var dataObj = new JObject();
            //配方
            var recipes = new JArray();
            dataObj.Add("recipes", recipes);
            foreach (var recipe in LDB.recipes.dataArray) {
                addRecipe(recipe, recipes);
            }
            //物品
            var items = new JArray();
            dataObj.Add("items", items);
            foreach (var item in LDB.items.dataArray) {
                if (item.ID < IFE电磁奖券 || item.ID > IBC品质插件MK3) {
                    //如果该物品是“该版本尚未加入”
                    if ((!GameMain.history.ItemUnlocked(item.ID) && item.preTech == null && item.missingTech)
                        //或无法选中这个物品（9998是星河卫士勋章，13000之后是巨构旧的接收器）
                        || (item.GridIndex == 0 || item.GridIndex == 9998 || item.GridIndex > 13000)
                        //或没有配方可以制造这个物品，且不是原始矿物或蓄电器满
                        || ((item.recipes == null || item.recipes.Count == 0)
                            && !item.canMining()
                            && item.ID != I蓄电器满)) {
                        //则移除物品，并移除原料包含该物品，或产物包含该物品的所有配方
                        LogInfo($"移除物品 {item.ID} {item.name}");
                        IList<JToken> recipeToBeRemoved = new List<JToken>();
                        foreach (var recipe in recipes) {
                            var itemsArr = ((JArray)((JObject)recipe)["Items"]).Values<int>();
                            var resultsArr = ((JArray)((JObject)recipe)["Results"]).Values<int>();
                            bool remove = false;
                            foreach (var id in itemsArr) {
                                if (id == item.ID) {
                                    remove = true;
                                }
                            }
                            foreach (var id in resultsArr) {
                                if (id == item.ID) {
                                    remove = true;
                                }
                            }
                            if (remove) {
                                recipeToBeRemoved.Add(recipe);
                            }
                        }
                        foreach (var recipe in recipeToBeRemoved) {
                            recipes.Remove(recipe);
                            LogInfo($"移除配方 {recipe["ID"]} {recipe["Name"]}");
                        }
                        continue;
                    }
                }
                addItem(item, items);
                //添加与这个物品相关的特殊配方（游戏中不存在，但是计算器需要它们来计算）
                //0.可直接采集的物品（黑雾不算）对应开采配方需要插入到最前，以尽量规避线性规划无解
                int firstIdx = recipes.Count;
                if (item.canMiningFromVein()
                    || item.canMiningFromSea()
                    || item.canMiningFromOilWell()
                    || item.canMiningFromGasGiant()
                    || item.canMiningFromAtmosphere()
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
                //1.可直接采集
                List<int> factorySpecial = [];
                if (item.canMiningFromVein()) {
                    factorySpecial = [..factorySpecial, I采矿机, I大型采矿机];
                }
                if (item.canMiningFromSea()) {
                    factorySpecial = [..factorySpecial, I抽水站];
                    if (GenesisBookEnable) {
                        factorySpecial = [..factorySpecial, IGB聚束液体汲取设施];
                    }
                }
                if (item.canMiningFromOilWell()) {
                    factorySpecial = [..factorySpecial, I原油萃取站];
                }
                if (item.canMiningFromGasGiant()) {
                    factorySpecial = [..factorySpecial, I轨道采集器];
                }
                if (item.canMiningFromAtmosphere()) {
                    factorySpecial = [..factorySpecial, IGB大气采集站];
                }
                if (item.canMiningByIcarus()) {
                    factorySpecial = [..factorySpecial, I伊卡洛斯];
                }
                if (item.canMiningByMS()) {
                    factorySpecial = [..factorySpecial, I巨构星际组装厂];
                }
                if (item.canDropFromEnemy()) {
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
                        { "IconName", item.iconSprite.name },
                    });
                    firstIdx++;
                }
                //2.射线接受站，以及巨构的射线重构站
                if (item.canMiningByRayReceiver()) {
                    //不带透镜的公式
                    //此公式比较特殊，有明确速率0.1/s，此处不使用大部分“无中生有”的1/s速率
                    recipes.Insert(firstIdx, new JObject {
                        { "Type", -1 },
                        { "Factories", new JArray(new[] { I射线接收站_MS射线重构站 }) },
                        { "Name", $"[无中生有]{item.name}" },
                        { "Items", new JArray(Array.Empty<int>()) },
                        { "ItemCounts", new JArray(Array.Empty<int>()) },
                        { "Results", new JArray(new[] { item.ID }) },
                        { "ResultCounts", new JArray(new[] { 1 }) },
                        { "TimeSpend", 600 },
                        { "Proliferator", 0 },
                        { "IconName", item.iconSprite.name },
                    });
                    firstIdx++;
                    //带透镜的公式
                    //此公式比较特殊，有明确的速率0.2/s
                    recipes.Insert(firstIdx, new JObject {
                        { "Type", -1 },
                        { "Factories", new JArray(new[] { I射线接收站_MS射线重构站 }) },
                        { "Name", $"[射线接收带透镜]{item.name}" },
                        { "Items", new JArray(new[] { I引力透镜 }) },
                        { "ItemCounts", new JArray(new[] { 1.0 / 120.0 }) },
                        { "Results", new JArray(new[] { item.ID }) },
                        { "ResultCounts", new JArray(new[] { 1 }) },
                        { "TimeSpend", 300 },
                        { "Proliferator", 4 },
                        { "IconName", item.iconSprite.name },
                    });
                    firstIdx++;
                }
                //3.蓄电器空与蓄电器满的相互转化
                if (item.ID == I蓄电器满) {
                    //蓄电器->蓄电器满，两个公式，一个放下充电，一个通过能量枢纽充电
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
                        { "IconName", item.iconSprite.name },
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
                        { "IconName", item.iconSprite.name },
                    });
                }
                //4.创世满燃料棒烧完变空燃料棒
                if (GenesisBookEnable) {
                    int[] factoryID = [
                        I火力发电厂_GB燃料电池发电厂, I火力发电厂_GB燃料电池发电厂, I火力发电厂_GB燃料电池发电厂,
                        I微型聚变发电站_GB裂变能源发电站, I微型聚变发电站_GB裂变能源发电站, I微型聚变发电站_GB裂变能源发电站,
                        I人造恒星_GB朱曦K型人造恒星, I人造恒星_GB朱曦K型人造恒星, I人造恒星_GB朱曦K型人造恒星,
                        IGB湛曦O型人造恒星, IGB湛曦O型人造恒星,
                    ];
                    int[] itemID = [
                        I液氢燃料棒, IGB焦油燃料棒, IGB四氢双环戊二烯燃料棒,
                        IGB铀燃料棒, IGB钚燃料棒, IGBMOX燃料棒,
                        I氘核燃料棒, IGB氦三燃料棒, IGB氘氦混合燃料棒,
                        I反物质燃料棒, I奇异湮灭燃料棒,
                    ];
                    if (itemID.Contains(item.ID)) {
                        int index = itemID.ToList().IndexOf(item.ID);
                        recipes.Add(new JObject {
                            { "Type", -1 },
                            { "Factories", new JArray(new[] { factoryID[index] }) },
                            { "Name", $"[燃料棒耗尽]{item.name}" },
                            { "Items", new JArray(new[] { item.ID }) },
                            { "ItemCounts", new JArray(new[] { 1 }) },
                            { "Results", new JArray(new[] { IGB空燃料棒 }) },
                            { "ResultCounts", new JArray(new[] { 1 }) },
                            { "TimeSpend", 60 * 60 },//暂时设为60s
                            { "Proliferator", 1 },//暂时先设为1，以便正确计算增产剂使用数目
                            { "IconName", item.iconSprite.name },
                        });
                    }
                }
                //5.矿物复制、物品转化、量子复制
                if (FractionateEverythingEnable) {
                    AddBaseRecipe(recipes, item);
                }
            }
            //特殊物品（无实体的工厂）
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

            //保存json到本项目内。文件不复制到戴森球计算器，而是在AfterBuildEvent复制
            filePath = $@"{dirCalc}\{fileName}.json";
            if (!File.Exists(filePath)) {
                File.Create(filePath).Close();
            }
            using (var sw = new StreamWriter(filePath, false, Encoding.UTF8)) {
                sw.WriteLine(dataObj.ToString(Formatting.Indented));
            }
            LogInfo($"已生成{filePath}");

            #endregion

        }
        catch (Exception ex) {
            LogError(ex.ToString());
        }
    }

    static void AddBaseRecipe(JArray recipes, ItemProto item) {
        // BaseRecipe baseRecipe;
        // if ((baseRecipe = RecipeManager.GetNaturalResourceRecipe(item.ID))
        //     != null) {
        //     var res = processBaseRecipe(baseRecipe);
        //     recipes.Add(new JObject {
        //         { "Type", -1 },
        //         { "Factories", new JArray(new[] { IFE矿物复制塔 }) },
        //         { "Name", $"[矿物复制]{item.name}" },
        //         { "Items", new JArray(new[] { item.ID }) },
        //         { "ItemCounts", new JArray(new[] { res.Item1 }) },
        //         { "Results", new JArray(res.Item2) },
        //         { "ResultCounts", new JArray(res.Item3) },
        //         { "TimeSpend", 6000 },
        //         { "Proliferator", 1 },
        //         { "IconName", item.iconSprite.name },
        //     });
        // }
        // if ((baseRecipe = RecipeManager.GetUpgradeRecipe(item.ID)) != null) {
        //     var res = processBaseRecipe(baseRecipe);
        //     recipes.Add(new JObject {
        //         { "Type", -1 },
        //         { "Factories", new JArray(new[] { IFE转化塔MK1 }) },
        //         { "Name", $"[升级分馏]{item.name}" },
        //         { "Items", new JArray(new[] { item.ID }) },
        //         { "ItemCounts", new JArray(new[] { res.Item1 }) },
        //         { "Results", new JArray(res.Item2) },
        //         { "ResultCounts", new JArray(res.Item3) },
        //         { "TimeSpend", 6000 },
        //         { "Proliferator", 1 },
        //         { "IconName", item.iconSprite.name },
        //     });
        // }
        // if ((baseRecipe = RecipeManager.GetDowngradeRecipe(item.ID)) != null) {
        //     var res = processBaseRecipe(baseRecipe);
        //     recipes.Add(new JObject {
        //         { "Type", -1 },
        //         { "Factories", new JArray(new[] { IFE转化塔MK1 }) },
        //         { "Name", $"[转化]{item.name}" },
        //         { "Items", new JArray(new[] { item.ID }) },
        //         { "ItemCounts", new JArray(new[] { res.Item1 }) },
        //         { "Results", new JArray(res.Item2) },
        //         { "ResultCounts", new JArray(res.Item3) },
        //         { "TimeSpend", 6000 },
        //         { "Proliferator", 1 },
        //         { "IconName", item.iconSprite.name },
        //     });
        // }
        // if (item.ID != I沙土
        //     && (baseRecipe = RecipeManager.GetIncreaseRecipe(item.ID)) != null) {
        //     // Dictionary<int, float> itemRatio = Traverse.Create(typeof(ProcessManager)).Field("itemRatio")
        //     //     .GetValue<Dictionary<int, float>>();
        //     // float ratio = itemRatio[item.ID];//增产10点情况下的概率
        //     // var res = processBaseRecipe(new BaseRecipe());
        //     var res = processBaseRecipe(baseRecipe);
        //     recipes.Add(new JObject {
        //         { "Type", -1 },
        //         { "Factories", new JArray(new[] { IFE量子复制塔 }) },
        //         { "Name", $"[量子复制]{item.name}" },
        //         { "Items", new JArray(new[] { item.ID }) },
        //         { "ItemCounts", new JArray(new[] { res.Item1 }) },
        //         { "Results", new JArray(res.Item2) },
        //         { "ResultCounts", new JArray(res.Item3) },
        //         { "TimeSpend", 6000 },
        //         { "Proliferator", 8 },//新的模式
        //         { "IconName", item.iconSprite.name },
        //     });
        // }
    }

    static (float, List<int>, List<float>) processBaseRecipe(BaseRecipe baseRecipe) {
        float inputNum = 0;
        List<int> outputID = [baseRecipe.InputID];
        List<float> outputNum = [baseRecipe.InputID];
        List<float> outputRatio = [baseRecipe.InputID];
        //1个->5%->2个 等价于 1个->20s->2个 等价于 5个->100s->10个
        //1个->x%->y个 等价于 x个->100s->xy个
        // if (baseRecipe.destroyRatio > 0) {
        //     inputNum += baseRecipe.destroyRatio * 100;
        // }
        outputID.RemoveAt(0);
        outputNum.RemoveAt(0);
        outputRatio.RemoveAt(0);
        for (int i = 0; i < outputID.Count; i++) {
            inputNum += outputRatio[i] * 100;
            outputNum[i] *= outputRatio[i] * 100;
        }
        return (inputNum, outputID, outputNum);
    }

    static void addItem(ItemProto proto, JArray add) {
        var obj = new JObject {
            { "ID", proto.ID },
            { "Type", (int)proto.Type },
            { "Name", proto.name },
            { "GridIndex", proto.GridIndex },
            { "IconName", proto.iconSprite.name },
        };
        if (proto.GetSpace() >= 0) {
            //对于生产建筑，添加耗能、倍率、占地
            obj.Add("WorkEnergyPerTick", proto.prefabDesc.workEnergyPerTick);
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
            //obj.Add("MultipleOutput", proto.ID == I负熵熔炉 && GenesisBookEnable ? 2 : 1);
            obj.Add("Space", proto.GetSpace());
        }
        add.Add(obj);
    }

    /// <summary>
    /// TimeSpend：帧数，一秒对应60帧。也就是说TimeSpend为60时，表示1秒内可以完成制造。
    /// </summary>
    static void addRecipe(RecipeProto proto, JArray add) {
        if (proto.Type == ERecipeType.Fractionate) {
            //115重氢分馏
            if (proto.ID == R重氢分馏_GB氦闪约束器) {
                RecipeProto proto2 = CopyRecipeProto(proto);
                //1%概率分馏出1个重氢，假设传送带速度为x每秒，显然重氢生成速率为x/100每秒
                //可以转为等价配方：1氢->100s->1重氢
                proto2.ItemCounts[0] = 1;
                proto2.TimeSpend = 6000;
                proto2.ResultCounts[0] = 1;
                addRecipe(proto2, add, [I分馏塔]);
            } else {
                LogError($"发现非重氢分馏的分馏配方！ ID：{proto.ID}");
            }
            return;
        }
        int[] Factories;
        try {
            Factories = proto.getAcceptFactories();
        }
        catch (Exception ex) {
            //创世+巨构情况下，多功能集成组件被专门设计为抛出异常，因为canMiningByMS已添加对应配方
            LogWarning(ex.ToString());
            return;
        }
        if (GenesisBookEnable && Factories.Contains(I负熵熔炉)) {
            if ((int)proto.Type is (int)Utils_ERecipeType.Smelt or (int)Utils_ERecipeType.矿物处理) {
                Factories = Factories.Where(x => x != I负熵熔炉).ToArray();
                addRecipe(proto, add, Factories);

                RecipeProto proto2 = new RecipeProto();
                proto.CopyPropsTo(ref proto2);
                proto2.Type = (ERecipeType)(-1);
                proto2.name = $"[负熵翻倍]{proto.name}";
                for (int i = 0; i < proto2.ResultCounts.Length; i++) {
                    proto2.ResultCounts[i] *= 2;
                }
                addRecipe(proto2, add, [I负熵熔炉]);
                return;
            }
        }
        addRecipe(proto, add, Factories);
    }

    static void addRecipe(RecipeProto proto, JArray add, int[] Factories) {
        if (Factories == null || Factories.Length == 0) {
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
        var obj = new JObject {
            { "Type", (int)proto.Type },
            { "Factories", new JArray(Factories) },
            { "Name", proto.name },
            { "Items", new JArray(proto.Items) },
            { "ItemCounts", new JArray(proto.ItemCounts) },
            { "Results", new JArray(proto.Results) },
            { "ResultCounts", new JArray(proto.ResultCounts) },
            //分馏配方TimeSpend=ratio*100000
            //{ "TimeSpend", flag4 ? (1.0 / (proto.TimeSpend / 100000.0)) * 60 : proto.TimeSpend },
            { "TimeSpend", proto.TimeSpend },
            { "Proliferator", flag4 || !flag2 ? 1 : 3 },
            { "IconName", proto.iconSprite.name },
        };
        add.Add(obj);
    }

    static RecipeProto CopyRecipeProto(RecipeProto ori) {
        return new() {
            ID = ori.ID,
            Type = ori.Type,
            name = ori.name,
            Items = new List<int>(ori.Items).ToArray(),
            ItemCounts = new List<int>(ori.ItemCounts).ToArray(),
            Results = new List<int>(ori.Results).ToArray(),
            ResultCounts = new List<int>(ori.ResultCounts).ToArray(),
            TimeSpend = ori.TimeSpend,
            productive = ori.productive,
            _iconSprite = ori.iconSprite,
        };
    }

    static string FormatName(string name, string Name) {
        //优先使用Name，例如分馏的“超值礼包1”
        string str = string.IsNullOrEmpty(Name) ? name : Name;
        return str.Translate()
            .Replace(" ", "")
            .Replace(" ", "")
            .Replace(" ", "")
            .Replace("“", "")
            .Replace("”", "")
            .Replace(":", "")
            .Replace("：", "")
            .Replace("!", "")
            .Replace("-", "")
            .Replace(".", "")
            .Replace("（", "")
            .Replace("）", "")
            .Replace("Recipe", "");
    }

    // private void test() {
    //     string jsonText =
    //         "[{\"a\": \"aaa\",\"b\": \"bbb\",\"c\": \"ccc\"},{\"a\": \"aa\",\"b\": \"bb\",\"c\": \"cc\"}]";
    //     var arr = JArray.Parse(jsonText);
    //     //需求，删除列表里的a节点的值为\"aa\"的项
    //     IList<JToken> _ILIST = new List<JToken>();//存储需要删除的项
    //     foreach (var item in arr)//查找某个字段与值
    //     {
    //         if (((JObject)item)["a"].Value<string>() == "aa") {
    //             _ILIST.Add(item);
    //         }
    //     }
    //     foreach (var item in _ILIST)//移除mJObj  有效
    //     {
    //         arr.Remove(item);
    //     }
    // }

    // [HarmonyTranspiler]
    // [HarmonyPatch(typeof(ItemComboBox), nameof(ItemComboBox.OnItemIndexChange))]
    // static IEnumerable<CodeInstruction> ItemComboBox_OnItemIndexChange_Transpiler(
    //     IEnumerable<CodeInstruction> instructions) {
    //     var matcher = new CodeMatcher(instructions);
    //     matcher.MatchForward(false, new CodeMatch(OpCodes.Ldarg_0));
    //     //第1行ldarg.0不变
    //     matcher.Advance(1);
    //     //第2行改成call
    //     matcher.SetAndAdvance(OpCodes.Call,
    //         AccessTools.Method(typeof(GenesisBook), nameof(ItemComboBox_OnItemIndexChange_InsertMethod)));
    //     //3-5行改成nop
    //     while (matcher.Opcode != OpCodes.Stloc_0) {
    //         matcher.SetAndAdvance(OpCodes.Nop, null);
    //     }
    //     //第6行stloc.0不变
    //     return matcher.InstructionEnumeration();
    // }
    //
    // public static ItemProto ItemComboBox_OnItemIndexChange_InsertMethod(ItemComboBox __instance) {
    //     return __instance.selectIndex == -1
    //         ? LDB.items.Select(IFE量子复制塔)
    //         : __instance._items[__instance.selectIndex];
    // }

    // [HarmonyTranspiler]
    // [HarmonyPatch(typeof(BuildTool_Path), nameof(BuildTool_Path.DeterminePreviews))]
    // private static IEnumerable<CodeInstruction> test1(IEnumerable<CodeInstruction> instructions) {
    //     var matcher = new CodeMatcher(instructions);
    //     // matcher.MatchForward(false, new CodeMatch(OpCodes.Ldarg_0));
    //     // //第1行ldarg.0不变
    //     // matcher.Advance(1);
    //     // //第2行改成call
    //     // matcher.SetAndAdvance(OpCodes.Call,
    //     //     AccessTools.Method(typeof(GenesisBook), nameof(ItemComboBox_OnItemIndexChange_InsertMethod)));
    //     // //3-5行改成nop
    //     // while (matcher.Opcode != OpCodes.Stloc_0) {
    //     //     matcher.SetAndAdvance(OpCodes.Nop, null);
    //     // }
    //     // //第6行stloc.0不变
    //     // return matcher.InstructionEnumeration();
    //     matcher.MatchForward(false,
    //         new CodeMatch(OpCodes.Call,
    //             AccessTools.PropertyGetter(typeof(VFInput), nameof(VFInput._switchBeltsPath)))
    //     );
    //     int endPos = matcher.Clone().MatchForward(true,
    //         new CodeMatch(OpCodes.Ldarg_0),
    //         new CodeMatch(OpCodes.Ldc_I4_0),
    //         new CodeMatch(OpCodes.Stfld,
    //             AccessTools.Field(typeof(BuildTool_Path), nameof(BuildTool_Path.geodesic)))
    //     ).Pos;
    //     matcher.RemoveInstructionsInRange(matcher.Pos + 2, endPos);
    //     matcher.SetAndAdvance(OpCodes.Ldarg_0, null);
    //     matcher.SetAndAdvance(OpCodes.Call, AccessTools.Method(typeof(FractionatorLogic), nameof(test2)));
    //     // matcher.RemoveInstructionsInRange(matcher.Pos, endPos);
    //     // matcher.InsertAndAdvance(new CodeInstruction[] {
    //     //     new CodeInstruction(OpCodes.Ldarg_0),
    //     //     new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FractionatorLogic), nameof(test2))),
    //     // });
    //     matcher.Advance(-5);
    //
    //     LogError($"len={matcher.Length}");
    //     for (int i = 1840; i < 1860; i++) {
    //         LogError($"{matcher.Opcode} {matcher.Operand}");
    //         matcher.Advance(1);
    //     }
    //
    //     return matcher.InstructionEnumeration();
    // }
    //
    // public static void test2(BuildTool_Path path) {
    //     LogError("test2 ok");
    //     if (VFInput._switchBeltsPath) {
    //         if (!path.geodesic) {
    //             if (path.pathSuggest > 0) {
    //                 path.geodesic = true;
    //             }
    //             if (path.pathAlternative == 1) {
    //                 path.pathAlternative = 2;
    //             }
    //             else if (path.pathAlternative == 2) {
    //                 path.pathAlternative = 1;
    //                 path.geodesic = true;
    //             }
    //         }
    //         else {
    //             if (path.pathSuggest > 0)
    //                 path.geodesic = false;
    //             if (path.pathAlternative == 1)
    //                 path.pathAlternative = 2;
    //             else {
    //                 path.pathAlternative = 1;
    //                 path.geodesic = false;
    //             }
    //         }
    //     }
    // }
}
