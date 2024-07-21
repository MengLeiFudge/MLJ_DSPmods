using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using CommonAPI;
using CommonAPI.Systems;
using FractionateEverything.Main;
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
using static GetDspData.ProtoID;
using static FractionateEverything.Main.FractionateRecipes;

namespace GetDspData {
    //item.UnlockKey
    //UnlockKey>0：跟随解锁，例如蓄电器（满）是跟随蓄电器解锁的
    //UnlockKey=0：由科技解锁
    //UnlockKey=-1：直接解锁
    //UnlockKey=-2：由黑雾掉落

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

        private static string dir;
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

            dir = @"D:\project\csharp\DSP MOD\MLJ_DSPmods\GetDspData\gamedata";
            MoreMegaStructureEnable = Chainloader.PluginInfos.ContainsKey(MoreMegaStructureGUID);
            TheyComeFromVoidEnable = Chainloader.PluginInfos.ContainsKey(TheyComeFromVoidGUID);
            GenesisBookEnable = Chainloader.PluginInfos.ContainsKey(GenesisBookGUID);
            FractionateEverythingEnable = Chainloader.PluginInfos.ContainsKey(FractionateEverythingGUID);

            Harmony harmony = new(GUID);
            harmony.Patch(
                AccessTools.Method(typeof(VFPreload), "InvokeOnLoadWorkEnded"),
                null,
                new(typeof(GetDspData), nameof(WriteDataToFile)) {
                    after = [LDBToolPlugin.MODGUID, FractionateEverythingGUID]
                }
            );
        }

        static Dictionary<int, string> itemIdNameDic = new();
        static Dictionary<string, int> modelNameIdDic = new();

        private static void WriteDataToFile() {
            try {

                #region 代码中使用

                using (var sw = new StreamWriter(dir + "\\DSP_ProtoID.txt", false, Encoding.UTF8)) {
                    sw.WriteLine("static class ProtoID");
                    sw.WriteLine("{");

                    foreach (var item in LDB.items.dataArray) {
                        int id = item.ID;
                        string name = FormatName(item.name, item.Name);
                        sw.WriteLine($"    internal const int I{name} = {id};");
                        itemIdNameDic.Add(id, name);
                        int modelID = item.ModelIndex;
                        if (modelID > 0) {
                            modelNameIdDic.Add(name, modelID);
                        }
                    }

                    sw.WriteLine();

                    foreach (var p in modelNameIdDic) {
                        sw.WriteLine($"    internal const int M{p.Key} = {p.Value};");
                    }

                    sw.WriteLine();

                    foreach (var recipe in LDB.recipes.dataArray) {
                        int id = recipe.ID;
                        string name = FormatName(recipe.name, recipe.Name);
                        //if (!regex.IsMatch(name)) {
                        sw.WriteLine($"    internal const int R{name} = {id};");
                        //}
                    }

                    sw.WriteLine();

                    string lastTechName = "";
                    foreach (var tech in LDB.techs.dataArray) {
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

                using (var sw = new StreamWriter(dir + "\\DSP_DataInfo.csv", false, Encoding.UTF8)) {
                    sw.WriteLine("物品ID,物品名称,物品类型,index(自动排序位置),BuildMode(建造类型),BuildIndex(建造栏位置)");
                    foreach (var item in LDB.items.dataArray) {
                        sw.WriteLine(item.ID
                                     + ","
                                     + itemIdNameDic[item.ID]
                                     + ","
                                     + Enum.GetName(typeof(EItemType), (int)item.Type)
                                     + ","
                                     + item.index
                                     + ","
                                     + item.BuildMode
                                     + ","
                                     + item.BuildIndex);
                    }
                    sw.WriteLine();
                    sw.WriteLine();

                    sw.WriteLine("配方ID,配方名称,配方类型,原料,产物,时间");
                    foreach (var recipe in LDB.recipes.dataArray) {
                        int[] itemIDs = recipe.Items;
                        int[] itemCounts = recipe.ItemCounts;
                        int[] resultIDs = recipe.Results;
                        int[] resultCounts = recipe.ResultCounts;
                        float timeSpeed = recipe.TimeSpend / 60.0f;
                        string s = recipe.ID
                                   + ","
                                   + FormatName(recipe.name, recipe.Name)
                                   + ","
                                   + Enum.GetName(typeof(Utils_ERecipeType), (int)recipe.Type)
                                   + ",";
                        for (int i = 0; i < itemIDs.Length; i++) {
                            s += itemIDs[i] + "(" + itemIdNameDic[itemIDs[i]] + ")*" + itemCounts[i] + " + ";
                        }
                        s = s.Substring(0, s.Length - 3) + " -> ";
                        for (int i = 0; i < resultIDs.Length; i++) {
                            s += resultIDs[i] + "(" + itemIdNameDic[resultIDs[i]] + ")*" + resultCounts[i] + " + ";
                        }
                        s = s.Substring(0, s.Length - 3) + ",";
                        s += recipe.TimeSpend + "(" + timeSpeed.ToString("F1") + "s)";
                        sw.WriteLine(s);
                    }
                    sw.WriteLine();
                    sw.WriteLine();

                    sw.WriteLine("科技ID,科技名称,解锁配方");
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

                string dirCalc = dir + "\\calc json";
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
                List<RecipeProto> notAddRecipes = [];
                foreach (var recipe in LDB.recipes.dataArray) {
                    //这里先排除自分馏配方，后面再添加
                    if (recipe.Items.Length > 0 && recipe.Results.Length > 0 && recipe.Items[0] != recipe.Results[0]) {
                        addRecipe(recipe, recipes);
                    } else {
                        notAddRecipes.Add(recipe);
                    }
                }
                //物品
                var items = new JArray();
                dataObj.Add("items", items);
                foreach (var item in LDB.items.dataArray) {
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
                    addItem(item, items);
                    //添加特殊配方，游戏中不存在但是计算器需要它们来计算
                    List<int> factorySpecial = [];//可以直接采集，Type=-1，ID+10000
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
                    if (item.canMiningByRayReceiver()) {
                        //不带透镜的公式
                        factorySpecial = [..factorySpecial, I射线接收站];
                    }
                    if (item.canMiningByMS()) {
                        factorySpecial = [..factorySpecial, I巨构星际组装厂];
                    }
                    if (item.canDropFromEnemy()) {
                        factorySpecial = [..factorySpecial, I行星基地];
                    }
                    if (factorySpecial.Count > 0) {
                        recipes.Add(new JObject {
                            { "ID", item.ID + 10000 },
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
                    }
                    //添加为了调整顺序而之前没加的配方
                    foreach (var recipe in notAddRecipes) {
                        if (recipe.Items.Length > 0
                            && recipe.Results.Length > 0
                            && recipe.Items[0] != recipe.Results[0]
                            && recipe.Items[0] == item.ID) {
                            addRecipe(recipe, recipes);
                            notAddRecipes.Remove(recipe);
                            break;
                        }
                    }
                    if (item.canMiningByRayReceiver()) {
                        //带透镜的公式
                        recipes.Add(new JObject {
                            { "ID", item.ID + 20000 },
                            { "Type", -1 },
                            { "Factories", new JArray(new[] { I射线接收站 }) },
                            { "Name", $"[射线接收带透镜]{item.name}-" },
                            { "Items", new JArray(new[] { I引力透镜 }) },
                            { "ItemCounts", new JArray(new[] { 1.0 / 120.0 }) },
                            { "Results", new JArray(new[] { item.ID }) },
                            { "ResultCounts", new JArray(new[] { 1 }) },
                            { "TimeSpend", 60 },
                            { "Proliferator", 4 },
                            { "IconName", item.iconSprite.name },
                        });
                    }
                    if (item.ID == I蓄电器满) {
                        //蓄电器->蓄电器满，两个公式，一个放下充电，一个通过能量枢纽充电
                        recipes.Add(new JObject {
                            { "ID", item.ID + 10000 },
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
                            { "ID", item.ID + 10001 },
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
                    //创世有满燃料棒变空燃料棒的配方
                    if (GenesisBookEnable) {
                        int[] factoryID = [
                            I火力发电厂, I火力发电厂, I火力发电厂,
                            I微型聚变发电站_GB裂变能源发电站, I微型聚变发电站_GB裂变能源发电站, I微型聚变发电站_GB裂变能源发电站,
                            I人造恒星_GB人造恒星MKI, I人造恒星_GB人造恒星MKI, I人造恒星_GB人造恒星MKI,
                            IGB人造恒星MKII, IGB人造恒星MKII,
                        ];
                        int[] itemID = [
                            I液氢燃料棒, IGB煤油燃料棒, IGB四氢双环戊二烯燃料棒,
                            IGB铀燃料棒, IGB钚燃料棒, IGBMOX燃料棒,
                            I氘核燃料棒, IGB氦三燃料棒, IGB氘氦混合燃料棒,
                            I反物质燃料棒, I奇异湮灭燃料棒,
                        ];
                        if (itemID.Contains(item.ID)) {
                            int index = itemID.ToList().IndexOf(item.ID);
                            recipes.Add(new JObject {
                                { "ID", item.ID + 10000 },
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
                    //分馏启用时，添加增产塔分馏配方
                    if (FractionateEverythingEnable && item.ID != I沙土) {
                        addIncFractorRecipe(item, recipes);
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
                string jsonPath = dirCalc + $"\\{fileName}.json";
                using (var sw = new StreamWriter(jsonPath, false, Encoding.UTF8)) {
                    sw.WriteLine(dataObj.ToString(Formatting.Indented));
                }
                LogInfo($"已生成{jsonPath}");

                #endregion

            }
            catch (Exception ex) {
                LogError(ex.ToString());
            }
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
                if (proto.prefabDesc.isAssembler) {
                    obj.Add("Speed", proto.prefabDesc.assemblerSpeed);
                } else if (proto.prefabDesc.isLab) {
                    obj.Add("Speed", proto.prefabDesc.labAssembleSpeed);
                } else if (proto.ID == I采矿机) {
                    obj.Add("Speed", 5000);
                } else {
                    //大型采矿机、分馏塔等等都是10000速度
                    obj.Add("Speed", 10000);
                }
                //obj.Add("MultipleOutput", proto.ID == I负熵熔炉 && GenesisBookEnable ? 2 : 1);
                obj.Add("Space", proto.GetSpace());
            }
            add.Add(obj);
        }

        static void addIncFractorRecipe(ItemProto item, JArray recipes) {
            Dictionary<int, Dictionary<int, float>> IPFDic = Traverse.Create(typeof(FractionatorLogic)).Field("IPFDic")
                .GetValue<Dictionary<int, Dictionary<int, float>>>();
            float ratio = IPFDic[item.ID].Values.First();//这就是10点数情况下，对应的比例
            recipes.Add(new JObject {
                { "ID", item.ID + 30000 },
                { "Type", -1 },
                { "Factories", new JArray(new[] { IFE增产分馏塔 }) },
                { "Name", $"[增产分馏塔]{item.name}" },
                { "Items", new JArray(new[] { item.ID }) },
                { "ItemCounts", new JArray(new[] { 1 }) },
                { "Results", new JArray(new[] { item.ID }) },
                { "ResultCounts", new JArray(new[] { 2 }) },
                { "TimeSpend", (1.0 / ratio) * 60 },//按照10点数情况计算time
                { "Proliferator", 8 },//新的模式
                { "IconName", item.iconSprite.name },
            });
        }

        static void addRecipe(RecipeProto proto, JArray add) {
            if (proto.Type == ERecipeType.Fractionate) {
                //115重氢分馏
                if (proto.ID == R重氢分馏_GB氦闪约束器) {
                    RecipeProto proto2 = CopyRecipeProto(proto);
                    proto2.ItemCounts[0] = 1;
                    proto2.TimeSpend = (int)(0.01 * 100000);
                    addRecipe(proto2, add, [I分馏塔]);
                    return;
                }
                //剩下必然是万物分馏的配方
                //判定条件不能用proto.Items[0] == proto.Results[0]，因为还有增产剂MK3自分馏、微型粒子对撞机自分馏等
                if (isResourceFrac(proto.Items[0])) {
                    RecipeProto proto2 = CopyRecipeProto(proto);
                    adjustRecipeFEFrac(proto2, IFE自然资源分馏塔);
                    addRecipe(proto2, add, [IFE自然资源分馏塔]);
                } else {
                    RecipeProto proto2 = CopyRecipeProto(proto);
                    adjustRecipeFEFrac(proto2, IFE升级分馏塔);
                    //如果原料与产物相同，则不添加
                    if (proto.Items[0] != proto.Results[0]) {
                        addRecipe(proto, add, [IFE升级分馏塔]);
                    }

                    RecipeProto proto3 = CopyRecipeProto(proto);
                    adjustRecipeFEFrac(proto3, IFE降级分馏塔);
                    addRecipe(proto3, add, [IFE降级分馏塔]);
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
                    proto2.ID += 40000;
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
            //Proliferator bit3：增产分馏
            //Proliferator=0：无
            //Proliferator=1：无，加速
            //Proliferator=2：无，增产
            //Proliferator=3：无，加速，增产
            //Proliferator=4：无，使用引力透镜
            bool flag2 = proto.productive;
            bool flag4 = proto.Type == ERecipeType.Fractionate;
            var obj = new JObject {
                { "ID", proto.ID },
                { "Type", (int)proto.Type },
                { "Factories", new JArray(Factories) },
                { "Name", proto.name },
                { "Items", new JArray(proto.Items) },
                { "ItemCounts", new JArray(proto.ItemCounts) },
                { "Results", new JArray(proto.Results) },
                { "ResultCounts", new JArray(proto.ResultCounts) },
                //分馏配方TimeSpend=ratio*100000
                { "TimeSpend", flag4 ? (1.0 / (proto.TimeSpend / 100000.0)) * 60 : proto.TimeSpend },
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

        static bool isResourceFrac(int id) {
            return GetItemNaturalResource(id) != 0;
        }

        static void adjustRecipeFEFrac(RecipeProto recipe, int factory) {
            if (factory == IFE自然资源分馏塔) {
                recipe.name += "-自然资源分馏塔";
                recipe.ItemCounts[0] = 1;
                recipe.ResultCounts[0] = 2;
                Dictionary<int, float> dic = GetNumRatioNaturalResource(recipe.Items[0]);
                float ratio = dic[2];
                recipe.TimeSpend = (int)(ratio * 100000);
            } else if (factory == IFE升级分馏塔) {
                recipe.name += "-升级分馏塔";
                recipe.ItemCounts[0] = 1;
                recipe.ResultCounts[0] = 1;
                // Dictionary<int, float> dic = GetNumRatioUpgrade(recipe.Items[0]);
                // //注意，如果没有启用矩阵分馏或者燃料棒分馏，这里会出问题，返回的值不对
                // float ratio = dic[1];
                float ratio = 0.04f;
                //暂时不考虑损毁的影响，按照无损毁来计算
                recipe.TimeSpend = (int)(ratio * 100000);
            } else if (factory == IFE降级分馏塔) {
                recipe.name += "-降级分馏塔";
                recipe.ID += 1000;
                (recipe.Items[0], recipe.Results[0]) = (recipe.Results[0], recipe.Items[0]);
                recipe.ItemCounts[0] = 1;
                recipe.ResultCounts[0] = 2;
                // Dictionary<int, float> dic = GetNumRatioDowngrade(recipe.Items[0]);
                // //注意，如果没有启用矩阵分馏或者燃料棒分馏，这里会出问题，没有key=2
                // float ratio = dic[2];
                float ratio = 0.02f;
                recipe.TimeSpend = (int)(ratio * 100000);
            } else {
                throw new($"异常万物分馏配方，ID {recipe.ID}，factory {factory}");
            }
        }

        static string FormatName(string name, string Name) {
            if (Name == null) {
                return "Name is null!";
            }
            return Name.Translate()
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
                .Replace("）", "");
        }

        private void EditData(Proto proto) {
            // if (proto is ItemProto item) {
            //     switch (item.ID) {
            //         case I分馏塔:
            //
            //             break;
            //         case I化工厂:
            //         case I量子化工厂:
            //         case I微型粒子对撞机:
            //             item.GridIndex--;
            //             item.maincraft.GridIndex--;
            //             break;
            //     }
            // }else if (proto is RecipeProto recipe) {
            //     switch (recipe.ID) {
            //         case I分馏塔:
            //             item.Name = "通用分馏塔";
            //             item.Description = "I通用分馏塔";
            //             item.Preload(item.index);
            //             item.GridIndex = 2603;
            //             item.maincraft.GridIndex = 2603;
            //             item.BuildIndex = 408;
            //             LDBTool.SetBuildBar(item.BuildIndex / 100, item.BuildIndex % 100, item.ID);
            //             break;
            //         case I化工厂:
            //         case I量子化工厂:
            //         case I微型粒子对撞机:
            //             item.GridIndex--;
            //             item.maincraft.GridIndex--;
            //             break;
            //     }
            // }
        }

        #region 分馏原版逻辑梳理

        // public uint InternalUpdate(
        //     PlanetFactory factory,
        //     float power,
        //     SignData[] signPool,
        //     int[] productRegister,
        //     int[] consumeRegister) {
        //     //如果没电就不工作
        //     if ((float)power < 0.1f)
        //         return 0;
        //     //要处理的物品数目？一次只能处理0.001-4.0个物品。注意这是个float
        //     float num1 = 1.0;
        //     //fluidInputCount输入物品的数目  fluidInputCargoCount平均堆叠个数
        //     if (this.fluidInputCount == 0)
        //         //没有物品，平均堆叠自然是0
        //         this.fluidInputCargoCount = 0.0f;
        //     else
        //         //因为堆叠科技最大是4，所以fluidInputCount不可能大于4倍的fluidInputCargoCount
        //         num1 = (float)this.fluidInputCargoCount > 0.0001
        //             ? (float)this.fluidInputCount / (float)this.fluidInputCargoCount
        //             : 4.0;
        //     //运行分馏的条件：输入个数>0，流动输出个数未达缓存上限，产品输出个数未达缓存上限
        //     if (this.fluidInputCount > 0
        //         && this.productOutputCount < this.productOutputMax
        //         && this.fluidOutputCount < this.fluidOutputMax) {
        //         //反正是根据电力、要处理的数目（num1）来增加处理进度
        //         this.progress += (int)((float)power
        //                                * (500.0 / 3.0)
        //                                * ((float)this.fluidInputCargoCount < 30.0
        //                                    ? (float)this.fluidInputCargoCount
        //                                    : 30.0)
        //                                * num1
        //                                + 0.75);
        //         //最多一次性进行10次分馏判定
        //         if (this.progress > 100000)
        //             this.progress = 100000;
        //         //每10000进度，判定一次分馏，直至进度小于10000
        //         for (; this.progress >= 10000; this.progress -= 10000) {
        //             //fluidInputInc总输入增产点数  num2平均增产点数，注意这是个int
        //             int num2 = this.fluidInputInc <= 0 || this.fluidInputCount <= 0
        //                 ? 0
        //                 : this.fluidInputInc / this.fluidInputCount;
        //             //伪随机数种子
        //             this.seed = (uint)((ulong)(this.seed % 2147483646U + 1U) * 48271UL % (ulong)int.MaxValue) - 1U;
        //             //seed / 2147483646是一个0-1之间的数
        //             //produceProb是基础概率0.01，不过在万物分馏mod里面不用这个基础概率
        //             //1.0 + Cargo.accTableMilli[num2 < 10 ? num2 : 10]这个是平均增产点数对于速率的加成
        //             //增产点数越高，分馏成功率越高
        //             this.fractionSuccess = (float)this.seed / 2147483646.0
        //                                    < (float)this.produceProb
        //                                    * (1.0 + Cargo.accTableMilli[num2 < 10 ? num2 : 10]);
        //             if (this.fractionSuccess) {
        //                 //分馏成功
        //                 //产物+1（当前的实际产物个数）
        //                 ++this.productOutputCount;
        //                 //产物总数+1（仅用于分馏页面的显示，无实际效果）
        //                 ++this.productOutputTotal;
        //
        //                 //统计数目相关的东西
        //                 lock (productRegister)
        //                     //全局这个产物的生成数+1
        //                     ++productRegister[this.productId];
        //                 lock (consumeRegister)
        //                     //全局这个原料的消耗数+1
        //                     ++consumeRegister[this.fluidId];
        //             }
        //             else {
        //                 //分馏失败
        //                 //流动输出+1（当前的实际流动输出个数）
        //                 ++this.fluidOutputCount;
        //                 //流动总数+1（仅用于分馏页面的显示，无实际效果）
        //                 ++this.fluidOutputTotal;
        //                 //输出的产物增产总点数增加
        //                 this.fluidOutputInc += num2;
        //             }
        //
        //             //无论分馏是否成功，原料都被处理了
        //             //原料-1
        //             --this.fluidInputCount;
        //             //原料增产点数减少
        //             this.fluidInputInc -= num2;
        //             //原料平均堆叠数减少？这段没太看懂
        //             //num1是fluidInputCount / fluidInputCargoCount，
        //             //1.0 / num1 就是 fluidInputCargoCount / fluidInputCount
        //             //emm先不管了
        //             this.fluidInputCargoCount -= (float)(1.0 / num1);
        //             if ((float)this.fluidInputCargoCount < 0.0)
        //                 this.fluidInputCargoCount = 0.0f;
        //         }
        //     }
        //     else
        //         //未满足运行分馏的条件
        //         this.fractionSuccess = false;
        //     //货物流量
        //     CargoTraffic cargoTraffic = factory.cargoTraffic;
        //     byte stack;
        //     byte inc1;
        //
        //     //下面两个类似，只不过是传送带进出方向不同。
        //     //如果有传送带
        //     if (this.belt1 > 0) {
        //         //如果这个口是流动出口
        //         if (this.isOutput1) {
        //             //如果流动货物大于0
        //             //这样看来，处理多次后可能有多个流动货物了
        //             if (this.fluidOutputCount > 0) {
        //                 //平均增产点数
        //                 int inc2 = this.fluidOutputInc / this.fluidOutputCount;
        //                 CargoPath cargoPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[this.belt1].segPathId);
        //                 if (cargoPath != null
        //                     &&
        //                     //itemID，maxstack，stack，inc
        //                     cargoPath.TryUpdateItemAtHeadAndFillBlank(
        //                         this.fluidId,
        //                         Mathf.CeilToInt((float)(num1 - 0.1)),//平均每一块货物数目的上封顶
        //                         (byte)1,//仅输出一个
        //                         (byte)inc2)) {
        //                     //总输出-1，总增产点数也减少
        //                     --this.fluidOutputCount;
        //                     this.fluidOutputInc -= inc2;
        //                     //继续判断流动货物。为什么不写成while循环？
        //                     //奇怪，如果只判断两次，怎么做到输出货物也是堆叠4的？
        //                     //游戏60帧，传送带最大30/s，所以只需要一帧判断两次，即可输出4堆叠货物
        //                     if (this.fluidOutputCount > 0) {
        //                         int inc3 = this.fluidOutputInc / this.fluidOutputCount;
        //                         if (cargoPath.TryUpdateItemAtHeadAndFillBlank(
        //                                 this.fluidId,
        //                                 Mathf.CeilToInt((float)(num1 - 0.1)),
        //                                 (byte)1,
        //                                 (byte)inc3)) {
        //                             --this.fluidOutputCount;
        //                             this.fluidOutputInc -= inc3;
        //                         }
        //                     }
        //                 }
        //             }
        //         }
        //         //如果这个口是流动输入口，且输入缓存没满
        //         else if (!this.isOutput1 && (float)this.fluidInputCargoCount < (float)this.fluidInputMax) {
        //             //取货这部分不用看了
        //             if (this.fluidId > 0) {
        //                 if (cargoTraffic.TryPickItemAtRear(this.belt1, this.fluidId, (int[])null, out stack, out inc1)
        //                     > 0) {
        //                     this.fluidInputCount += (int)stack;
        //                     this.fluidInputInc += (int)inc1;
        //                     ++this.fluidInputCargoCount;
        //                 }
        //             }
        //             else {
        //                 int needId = cargoTraffic.TryPickItemAtRear(this.belt1, 0, RecipeProto.fractionatorNeeds,
        //                     out stack, out inc1);
        //                 if (needId > 0) {
        //                     this.fluidInputCount += (int)stack;
        //                     this.fluidInputInc += (int)inc1;
        //                     ++this.fluidInputCargoCount;
        //                     this.SetRecipe(needId, signPool);
        //                 }
        //             }
        //         }
        //     }
        //     if (this.belt2 > 0) {
        //         if (this.isOutput2) {
        //             if (this.fluidOutputCount > 0) {
        //                 int inc4 = this.fluidOutputInc / this.fluidOutputCount;
        //                 CargoPath cargoPath = cargoTraffic.GetCargoPath(cargoTraffic.beltPool[this.belt2].segPathId);
        //                 if (cargoPath != null
        //                     && cargoPath.TryUpdateItemAtHeadAndFillBlank(this.fluidId,
        //                         Mathf.CeilToInt((float)(num1 - 0.1)), (byte)1, (byte)inc4)) {
        //                     --this.fluidOutputCount;
        //                     this.fluidOutputInc -= inc4;
        //                     if (this.fluidOutputCount > 0) {
        //                         int inc5 = this.fluidOutputInc / this.fluidOutputCount;
        //                         if (cargoPath.TryUpdateItemAtHeadAndFillBlank(this.fluidId,
        //                                 Mathf.CeilToInt((float)(num1 - 0.1)), (byte)1, (byte)inc5)) {
        //                             --this.fluidOutputCount;
        //                             this.fluidOutputInc -= inc5;
        //                         }
        //                     }
        //                 }
        //             }
        //         }
        //         else if (!this.isOutput2 && (float)this.fluidInputCargoCount < (float)this.fluidInputMax) {
        //             if (this.fluidId > 0) {
        //                 if (cargoTraffic.TryPickItemAtRear(this.belt2, this.fluidId, (int[])null, out stack, out inc1)
        //                     > 0) {
        //                     this.fluidInputCount += (int)stack;
        //                     this.fluidInputInc += (int)inc1;
        //                     ++this.fluidInputCargoCount;
        //                 }
        //             }
        //             else {
        //                 int needId = cargoTraffic.TryPickItemAtRear(this.belt2, 0, RecipeProto.fractionatorNeeds,
        //                     out stack, out inc1);
        //                 if (needId > 0) {
        //                     this.fluidInputCount += (int)stack;
        //                     this.fluidInputInc += (int)inc1;
        //                     ++this.fluidInputCargoCount;
        //                     this.SetRecipe(needId, signPool);
        //                 }
        //             }
        //         }
        //     }
        //     //如果产品出口有传送带
        //     if (this.belt0 > 0
        //         &&
        //         //并且是输出口
        //         this.isOutput0
        //         &&
        //         //并且产物数目大于0
        //         this.productOutputCount > 0
        //         &&
        //         //尝试往传送带上塞一个物品
        //         cargoTraffic.TryInsertItemAtHead(this.belt0, this.productId, (byte)1, (byte)0))
        //         --this.productOutputCount;
        //     if (this.fluidInputCount == 0 && this.fluidOutputCount == 0 && this.productOutputCount == 0)
        //         this.fluidId = 0;
        //     //工作条件：有原料并且俩输出都不堵
        //     this.isWorking = this.fluidInputCount > 0
        //                      && this.productOutputCount < this.productOutputMax
        //                      && this.fluidOutputCount < this.fluidOutputMax;
        //     return !this.isWorking ? 0U : 1U;
        // }

        #endregion

        private void test() {
            string jsonText =
                "[{\"a\": \"aaa\",\"b\": \"bbb\",\"c\": \"ccc\"},{\"a\": \"aa\",\"b\": \"bb\",\"c\": \"cc\"}]";
            var arr = JArray.Parse(jsonText);
            //需求，删除列表里的a节点的值为\"aa\"的项
            IList<JToken> _ILIST = new List<JToken>();//存储需要删除的项
            foreach (var item in arr)//查找某个字段与值
            {
                if (((JObject)item)["a"].Value<string>() == "aa") {
                    _ILIST.Add(item);
                }
            }
            foreach (var item in _ILIST)//移除mJObj  有效
            {
                arr.Remove(item);
            }
        }

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
        //         ? LDB.items.Select(IFE增产分馏塔)
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
}
