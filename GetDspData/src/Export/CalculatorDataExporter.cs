using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FE.Logic.Fractionation.Fractionators;
using FE.Logic.Fractionation.Recipes;
using GetDspData.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static FE.Logic.Fractionation.Recipes.RecipeManager;
using static GetDspData.Utils.Utils;
using static global::GetDspData.GetDspData;
using static GetDspData.Export.IconExporter;

namespace GetDspData.Export;

/// <summary>
/// 组装并写出计算器数据。
/// </summary>
internal static class CalculatorDataExporter {
    /// <summary>
    /// 组装计算器 JSON 根对象，顺序固定为 recipes、items、techs。
    /// </summary>
    /// <returns>含 recipes/items/techs 的计算器数据对象。</returns>
    internal static JObject BuildCalculatorData() {
        var dataObj = new JObject();
        //配方
        var recipes = new JArray();
        dataObj.Add("recipes", recipes);
        foreach (var recipe in LDB.recipes.dataArray) {
            AddRecipe(recipe, recipes);
        }
        if (FractionateEverythingEnable) {
            AddFracRecipes(recipes);
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
    /// 按注释 0..4 的固定顺序为单个物品追加计算器特殊配方。
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
            // 高级配方已从物品的普通配方关联中移除，不能因此推断为伊卡洛斯采集来源。
            bool hasFractionationRecipe = FractionateEverythingEnable
                && GetRecipesProducing(item.ID).Any(recipe => recipe.IsAdvanced);
            bool noNormalRecipe = (item.recipes == null || item.recipes.Count == 0) && !hasFractionationRecipe;
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
    internal static void WriteCalculatorJson(string dirCalc, string modFullStr, JObject dataObj) {
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
    /// 按稳定目录导出 FE 生产配方，每张配方只写一次；解析推进研究，不作为固定物料生产。
    /// 数量按成功轮的期望实耗与实产导出，保留最低投入和消耗概率；速率仍需计算器结合供养与流量求解。
    /// 特质采用导出时的等级，成功率不含献祭，增产点按未喷涂处理。
    /// </summary>
    /// <param name="recipes">计算器配方数组。</param>
    private static void AddFracRecipes(JArray recipes) {
        foreach (WorldviewRecipe recipe in WorldviewRecipes.OrderBy(recipe => recipe.PersistentId)) {
            if (recipe.Kind == WorldviewRecipeKind.BasicAnalysis) { continue; }
            int buildingId = recipe.RecipeType.GetSpriteItemId();
            if (!FractionatorTowerCatalog.IsActiveFractionator(buildingId) || !LDB.items.Exist(buildingId)) {
                throw new InvalidOperationException($"FE配方生产建筑不存在：{recipe.StableId} / {buildingId}");
            }
            var consumeProbabilities = recipe.Inputs
                .Select(input => AdvancedFractionatorRuntime.GetInputConsumeProbability(input, buildingId)).ToArray();
            var outputCounts = recipe.Products
                .Select(output => AdvancedFractionatorRuntime.GetOutputPerSuccess(recipe, output)).ToArray();
            var outputMain = new JArray();
            for (int i = 0; i < recipe.Products.Count; i++) {
                outputMain.Add(new JObject {
                    { "OutputID", recipe.Products[i].ItemId },
                    { "OutputCount", outputCounts[i] },
                    { "SuccessRatio", 1.0 },
                });
            }
            int protoId = AdvancedRecipeCatalog.GetProtoId(recipe.PersistentId);
            RecipeProto proto = protoId > 0 ? LDB.recipes.Select(protoId) : null;
            recipes.Add(new JObject {
                { "ID", proto?.ID ?? recipe.PersistentId },
                { "StableID", recipe.StableId },
                { "Type", -1 },
                { "Factories", new JArray(buildingId) },
                { "Name", proto?.name ?? recipe.DisplayName.Translate() },
                { "Items", new JArray(recipe.Inputs.Select(input => input.ItemId)) },
                { "ItemCounts", new JArray(recipe.Inputs.Select((input, i) => input.Count * (double)consumeProbabilities[i])) },
                { "InputCountsPerRound", new JArray(recipe.Inputs.Select(input => input.Count)) },
                { "InputConsumeProbabilities", new JArray(consumeProbabilities) },
                { "Results", new JArray(recipe.Products.Select(output => output.ItemId)) },
                { "ResultCounts", new JArray(outputCounts) },
                { "TimeSpend", 0 },
                { "Proliferator", recipe.IsAdvanced ? 0 : 1 },
                { "SuccessRatio", recipe.CycleSuccessProbability },
                { "DestroyRatio", 0.0 },
                { "RemainInputRatio", 0.0 },
                { "DoubleOutputRatio", 0.0 },
                { "PointsBonus", 0.0 },
                { "RequiresFood", recipe.IsAdvanced },
                { "OutputMain", outputMain },
                { "OutputAppend", new JArray() },
                { "IconName", proto != null ? ResolveIconName(proto) : ResolveIconName(LDB.items.Select(recipe.PrimaryProductId)) },
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
                // 分馏塔、接收站和蓄电器没有制造台倍率字段，使用中性倍率。
                LogDebug($"{proto.name}制造速度倍率使用1.0");
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
        // FE高级原型仅供选择器展示，真实生产数据由稳定目录统一导出。
        if (FractionateEverythingEnable && AdvancedRecipeCatalog.IsAdvancedProto(proto.ID)) { return; }
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
