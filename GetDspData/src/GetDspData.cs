using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using CommonAPI;
using CommonAPI.Systems;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using xiaoye97;
using static BepInEx.BepInDependency.DependencyFlags;
using static GetDspData.ProtoID;

namespace GetDspData {
    //item.UnlockKey
    //UnlockKey>0：跟随解锁，例如蓄电器（满）是跟随蓄电器解锁的
    //UnlockKey=0：由科技解锁
    //UnlockKey=-1：直接解锁
    //UnlockKey=-2：由黑雾掉落

    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency(CommonAPIPlugin.GUID)]
    [BepInDependency(MoreMegaStructureGUID, SoftDependency)]
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
                foreach (var recipe in LDB.recipes.dataArray) {
                    addRecipe(recipe, recipes);
                }
                //物品
                var items = new JArray();
                dataObj.Add("items", items);
                foreach (var item in LDB.items.dataArray) {
                    //如果该物品是“该版本尚未加入”，则移除物品
                    bool removeItem = !GameMain.history.ItemUnlocked(item.ID)
                                      && item.preTech == null
                                      && item.missingTech;
                    //如果是创世之书，需要额外移除部分物品
                    if (!removeItem && GenesisBookEnable) {
                        removeItem = (item.ID >= 6506 && item.ID <= 6508)//创世之书、虚空之书、起源之书
                                     || (item.ID >= 6511 && item.ID <= 6521)//开发者日志1-11
                                     || (item.ID >= 6522 && item.ID <= 6534);//行星策略
                    }
                    if (removeItem) {
                        LogInfo($"移除物品 {item.ID} {item.name}");
                        //移除原料包含该物品，或产物包含该物品的所有配方
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
                        factorySpecial = [..factorySpecial, item.getMiningByRayReceiverItemID()];
                        //带透镜的公式
                        recipes.Add(new JObject {
                            { "ID", item.ID + 20000 },
                            { "Type", -2 },
                            { "Factories", new JArray(new[] { item.getMiningByRayReceiverItemID() }) },
                            { "Name", $"[特殊]{item.name}" },
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
                            { "ID", item.ID + 30000 },
                            { "Type", -3 },
                            { "Factories", new JArray(new[] { I蓄电器满 }) },
                            { "Name", $"[特殊]{item.name}" },
                            { "Items", new JArray(new[] { I蓄电器 }) },
                            { "ItemCounts", new JArray(new[] { 1 }) },
                            { "Results", new JArray(new[] { I蓄电器满 }) },
                            { "ResultCounts", new JArray(new[] { 1 }) },
                            { "TimeSpend", 21600 },
                            { "Proliferator", 0 },
                            { "IconName", item.iconSprite.name },
                        });
                        recipes.Add(new JObject {
                            { "ID", item.ID + 40000 },
                            { "Type", -4 },
                            { "Factories", new JArray(new[] { I能量枢纽 }) },
                            { "Name", $"[特殊]{item.name}" },
                            { "Items", new JArray(new[] { I蓄电器 }) },
                            { "ItemCounts", new JArray(new[] { 1 }) },
                            { "Results", new JArray(new[] { I蓄电器满 }) },
                            { "ResultCounts", new JArray(new[] { 1 }) },
                            { "TimeSpend", 600 },
                            { "Proliferator", 1 },
                            { "IconName", item.iconSprite.name },
                        });
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
                            { "Name", $"[特殊]{item.name}" },
                            { "Items", new JArray(Array.Empty<int>()) },
                            { "ItemCounts", new JArray(Array.Empty<int>()) },
                            { "Results", new JArray(new[] { item.ID }) },
                            { "ResultCounts", new JArray(new[] { 1 }) },
                            { "TimeSpend", 60 },
                            { "Proliferator", 0 },
                            { "IconName", item.iconSprite.name },
                        });
                    }
                    //分馏启用时，添加增产塔分馏配方
                    if (FractionateEverythingEnable) {
                        recipes.Add(new JObject {
                            { "ID", item.ID + 50000 },
                            { "Type", -5 },
                            { "Factories", new JArray(new[] { IFE增产分馏塔 }) },
                            { "Name", $"[特殊]{item.name}" },
                            { "Items", new JArray(new[] { item.ID }) },
                            { "ItemCounts", new JArray(new[] { 1 }) },
                            { "Results", new JArray(new[] { item.ID }) },
                            { "ResultCounts", new JArray(new[] { 2 }) },
                            { "TimeSpend", 100 * 10000 },
                            { "Proliferator", 2 },
                            { "IconName", item.iconSprite.name },
                        });
                    }
                    //创世还有满燃料棒变空燃料棒的配方
                    if (GenesisBookEnable && item.ID == IGB空燃料棒) {
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
                        for (int i = 0; i < factoryID.Length; i++) {
                            recipes.Add(new JObject {
                                { "ID", 60000 + i },
                                { "Type", -6 },
                                { "Factories", new JArray(new[] { factoryID[i] }) },
                                { "Name", $"[特殊]{item.name}" },
                                { "Items", new JArray(new[] { itemID[i] }) },
                                { "ItemCounts", new JArray(new[] { 1 }) },
                                { "Results", new JArray(new[] { IGB空燃料棒 }) },
                                { "ResultCounts", new JArray(new[] { 1 }) },
                                { "TimeSpend", 60 * 10000 },//暂时设为60s
                                { "Proliferator", 0 },//暂时先设为0
                                { "IconName", item.iconSprite.name },
                            });
                        }
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

                //保存json到本项目内，再复制到计算器项目里面
                string jsonPath = dirCalc + $"\\{fileName}.json";
                using (var sw = new StreamWriter(jsonPath, false, Encoding.UTF8)) {
                    sw.WriteLine(dataObj.ToString(Formatting.Indented));
                }
                LogInfo($"已生成{jsonPath}");
                string jsonPath2 = $"D:\\project\\js\\dsp-calc\\data\\{fileName}.json";
                File.Copy(jsonPath, jsonPath2, true);
                LogInfo($"已将json文件复制到{jsonPath2}");

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
                }
                else if (proto.prefabDesc.isLab) {
                    obj.Add("Speed", proto.prefabDesc.labAssembleSpeed);
                }
                else if (proto.ID == I采矿机) {
                    obj.Add("Speed", 5000);
                }
                else {
                    //大型采矿机、分馏塔等等都是10000速度
                    obj.Add("Speed", 10000);
                }
                obj.Add("MultipleOutput", proto.ID == I负熵熔炉 && GenesisBookEnable ? 2 : 1);
                obj.Add("Space", proto.GetSpace());
            }
            add.Add(obj);
        }

        static void addRecipe(RecipeProto proto, JArray add) {
            int[] Factories;
            try {
                Factories = proto.getAcceptFactories();
            }
            catch (Exception ex) {
                //创世+巨构情况下，多功能集成组件被专门设计为抛出异常，因为canMiningByMS已添加对应配方
                LogError(ex.ToString());
                return;
            }
            //UIItemTip 313-351行
            //增产公式描述1：加速或增产
            //增产公式描述2：加速
            //增产公式描述3：提升分馏概率
            //Proliferator bit0：加速
            //Proliferator bit1：增产
            //Proliferator bit2：接收射线使用引力透镜
            //Proliferator=0：无法加速或增产
            //Proliferator=1：加速
            //Proliferator=3：加速或增产
            //Proliferator=4：接收射线使用引力透镜
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
                { "TimeSpend", proto.Type == ERecipeType.Fractionate ? 100 * 10000 : proto.TimeSpend },
                { "Proliferator", flag4 || !flag2 ? 1 : 3 },
                { "IconName", proto.iconSprite.name },
            };
            add.Add(obj);
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


        // if (cargoPath != null) {
        //     //原版传送带最大速率为30，如果每次尝试放1个物品到传送带上，需要每帧判定2次（30速*4堆叠/60帧）
        //     //创世传送带最大速率为60，如果每次尝试放1个物品到传送带上，需要每帧判定4次（60速*4堆叠/60帧）
        //     //将原版逻辑改为2-1，创世逻辑改为4-2-1，以减少放东西的次数
        //     lock (cargoPath.buffer) {
        //         int fluidOutputAvgInc = __instance.fluidOutputInc / __instance.fluidOutputCount;
        //         int maxStack = Mathf.CeilToInt((float)(fluidInputCountPerCargo - 0.1));
        //         if (MaxInputTimes == 2) {
        //             if (maxStack == 1) {
        //                 if (__instance.fluidOutputCount >= 1
        //                     && cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 1,
        //                         (byte)__instance.fluidOutputInc)) {
        //                     __instance.fluidOutputCount -= 1;
        //                     __instance.fluidOutputInc -= fluidOutputAvgInc;
        //                 }
        //             }
        //             else {
        //                 if (__instance.fluidOutputCount > 2) {
        //                     if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 2,
        //                             (byte)(fluidOutputAvgInc * 2))) {
        //                         __instance.fluidOutputCount -= 2;
        //                         __instance.fluidOutputInc -= fluidOutputAvgInc * 2;
        //                     }
        //                     else if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 1,
        //                                  (byte)fluidOutputAvgInc)) {
        //                         __instance.fluidOutputCount -= 1;
        //                         __instance.fluidOutputInc -= fluidOutputAvgInc;
        //                     }
        //                 }
        //                 else if (__instance.fluidOutputCount == 2) {
        //                     if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 2,
        //                             (byte)__instance.fluidOutputInc)) {
        //                         __instance.fluidOutputCount = 0;
        //                         __instance.fluidOutputInc = 0;
        //                     }
        //                     else {
        //                         if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 1,
        //                                 (byte)fluidOutputAvgInc)) {
        //                             __instance.fluidOutputCount -= 1;
        //                             __instance.fluidOutputInc -= fluidOutputAvgInc;
        //                         }
        //                     }
        //                 }
        //                 else if (__instance.fluidOutputCount == 1) {
        //                     if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 1,
        //                             (byte)__instance.fluidOutputInc)) {
        //                         __instance.fluidOutputCount = 0;
        //                         __instance.fluidOutputInc = 0;
        //                     }
        //                 }
        //             }
        //         }
        //         else if (MaxInputTimes == 4) {
        //             if (maxStack == 1) {
        //                 if (__instance.fluidOutputCount >= 1
        //                     && cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 1,
        //                         (byte)__instance.fluidOutputInc)) {
        //                     __instance.fluidOutputCount -= 1;
        //                     __instance.fluidOutputInc -= fluidOutputAvgInc;
        //                 }
        //             }
        //             else if (maxStack == 2) {
        //                 if (__instance.fluidOutputCount > 2) {
        //                     if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 2,
        //                             (byte)(fluidOutputAvgInc * 2))) {
        //                         __instance.fluidOutputCount -= 2;
        //                         __instance.fluidOutputInc -= fluidOutputAvgInc * 2;
        //                     }
        //                     else if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 1,
        //                                  (byte)fluidOutputAvgInc)) {
        //                         __instance.fluidOutputCount -= 1;
        //                         __instance.fluidOutputInc -= fluidOutputAvgInc;
        //                     }
        //                 }
        //                 else if (__instance.fluidOutputCount == 2) {
        //                     if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 2,
        //                             (byte)__instance.fluidOutputInc)) {
        //                         __instance.fluidOutputCount = 0;
        //                         __instance.fluidOutputInc = 0;
        //                     }
        //                     else {
        //                         if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 1,
        //                                 (byte)fluidOutputAvgInc)) {
        //                             __instance.fluidOutputCount -= 1;
        //                             __instance.fluidOutputInc -= fluidOutputAvgInc;
        //                         }
        //                     }
        //                 }
        //                 else if (__instance.fluidOutputCount == 1) {
        //                     if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 1,
        //                             (byte)__instance.fluidOutputInc)) {
        //                         __instance.fluidOutputCount = 0;
        //                         __instance.fluidOutputInc = 0;
        //                     }
        //                 }
        //             }
        //             else if (maxStack == 3) {
        //                 if (__instance.fluidOutputCount >= 3) {
        //                     if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 2,
        //                             (byte)(fluidOutputAvgInc * 2))) {
        //                         __instance.fluidOutputCount -= 2;
        //                         __instance.fluidOutputInc -= fluidOutputAvgInc * 2;
        //                     }
        //                     fluidOutputAvgInc = __instance.fluidOutputInc / __instance.fluidOutputCount;
        //                     if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 1,
        //                             (byte)fluidOutputAvgInc)) {
        //                         __instance.fluidOutputCount -= 1;
        //                         __instance.fluidOutputInc -= fluidOutputAvgInc;
        //                     }
        //                 }
        //                 else if (__instance.fluidOutputCount == 2) {
        //                     if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 2,
        //                             (byte)__instance.fluidOutputInc)) {
        //                         __instance.fluidOutputCount = 0;
        //                         __instance.fluidOutputInc = 0;
        //                     }
        //                     else {
        //                         if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 1,
        //                                 (byte)fluidOutputAvgInc)) {
        //                             __instance.fluidOutputCount -= 1;
        //                             __instance.fluidOutputInc -= fluidOutputAvgInc;
        //                         }
        //                     }
        //                 }
        //                 else if (__instance.fluidOutputCount == 1) {
        //                     if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 1,
        //                             (byte)__instance.fluidOutputInc)) {
        //                         __instance.fluidOutputCount = 0;
        //                         __instance.fluidOutputInc = 0;
        //                     }
        //                 }
        //             }
        //             else {
        //                 if (__instance.fluidOutputCount >= 4) {
        //                     if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 4,
        //                             (byte)(fluidOutputAvgInc * 4))) {
        //                         __instance.fluidOutputCount -= 4;
        //                         __instance.fluidOutputInc -= fluidOutputAvgInc * 4;
        //                     }
        //                     else {
        //                         if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 2,
        //                                 (byte)(fluidOutputAvgInc * 2))) {
        //                             __instance.fluidOutputCount -= 2;
        //                             __instance.fluidOutputInc -= fluidOutputAvgInc * 2;
        //                         }
        //                         fluidOutputAvgInc = __instance.fluidOutputInc
        //                                             / __instance.fluidOutputCount;
        //                         if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 1,
        //                                 (byte)fluidOutputAvgInc)) {
        //                             __instance.fluidOutputCount -= 1;
        //                             __instance.fluidOutputInc -= fluidOutputAvgInc;
        //                         }
        //                     }
        //                 }
        //                 else if (__instance.fluidOutputCount == 3) {
        //                     if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 2,
        //                             (byte)(fluidOutputAvgInc * 2))) {
        //                         __instance.fluidOutputCount -= 2;
        //                         __instance.fluidOutputInc -= fluidOutputAvgInc * 2;
        //                     }
        //                     fluidOutputAvgInc = __instance.fluidOutputInc / __instance.fluidOutputCount;
        //                     if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 1,
        //                             (byte)fluidOutputAvgInc)) {
        //                         __instance.fluidOutputCount -= 1;
        //                         __instance.fluidOutputInc -= fluidOutputAvgInc;
        //                     }
        //                 }
        //                 else if (__instance.fluidOutputCount == 2) {
        //                     if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 2,
        //                             (byte)__instance.fluidOutputInc)) {
        //                         __instance.fluidOutputCount = 0;
        //                         __instance.fluidOutputInc = 0;
        //                     }
        //                     else {
        //                         if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 1,
        //                                 (byte)fluidOutputAvgInc)) {
        //                             __instance.fluidOutputCount -= 1;
        //                             __instance.fluidOutputInc -= fluidOutputAvgInc;
        //                         }
        //                     }
        //                 }
        //                 else if (__instance.fluidOutputCount == 1) {
        //                     if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID, maxStack, 1,
        //                             (byte)__instance.fluidOutputInc)) {
        //                         __instance.fluidOutputCount = 0;
        //                         __instance.fluidOutputInc = 0;
        //                     }
        //                 }
        //             }
        //         }
        //         else {
        //             for (int i = 0; i < MaxOutputTimes; i++) {
        //                 if (__instance.fluidOutputCount <= 0) {
        //                     break;
        //                 }
        //                 fluidOutputAvgInc = __instance.fluidOutputInc / __instance.fluidOutputCount;
        //                 if (cargoPath.TryUpdateItemAtHeadAndFillBlank(inputItemID,
        //                         Mathf.CeilToInt((float)(fluidInputCountPerCargo - 0.1)), 1,
        //                         (byte)fluidOutputAvgInc)) {
        //                     __instance.fluidOutputCount--;
        //                     __instance.fluidOutputInc -= fluidOutputAvgInc;
        //                 }
        //                 else {
        //                     break;
        //                 }
        //             }
        //         }
        //     }
        // }

        // [HarmonyTranspiler]
        // [HarmonyPatch(typeof(BuildTool_Path), "DeterminePreviews")]
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
