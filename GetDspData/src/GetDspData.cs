using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CommonAPI;
using CommonAPI.Systems;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using xiaoye97;

namespace GetDspData {
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency(CommonAPIPlugin.GUID)]
    [CommonAPISubmoduleDependency(nameof(ProtoRegistry))]
    public class GetDspData : BaseUnityPlugin {
        private const string GUID = "com.menglei.dsp.GetDspData";
        private const string NAME = "Get DSP Data";
        private const string VERSION = "1.0.0";
        private static ManualLogSource logger;

        private static string dir;

        public void Awake() {
            logger = Logger;

            dir = @"D:\project\csharp\DSP MOD\MLJ_DSPmods\GetDspData\gamedata";

            Harmony harmony = new(GUID);
            harmony.Patch(
                AccessTools.Method(typeof(VFPreload), "InvokeOnLoadWorkEnded"),
                null,
                new(typeof(GetDspData), nameof(WriteDataToFile)) {
                    after = [LDBToolPlugin.MODGUID]
                }
            );
        }

        static Dictionary<int, string> itemIdNameDic = new();
        static Dictionary<string, int> modelNameIdDic = new();
        private static readonly Regex regex = new Regex(".+分馏");

        private static void WriteDataToFile() {
            try {
                //代码中使用
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

                //csv数据
                using (StreamWriter sw = new StreamWriter(dir + "\\DSP_DataInfo.csv", false, Encoding.UTF8)) {
                    sw.WriteLine("物品ID,物品名称,index(自动排序位置),BuildMode(建造类型),BuildIndex(建造栏位置)");
                    foreach (var item in LDB.items.dataArray) {
                        sw.WriteLine(item.ID
                                     + ","
                                     + itemIdNameDic[item.ID]
                                     + ","
                                     + item.index
                                     + ","
                                     + item.BuildMode
                                     + ","
                                     + item.BuildIndex);
                    }
                    sw.WriteLine();
                    sw.WriteLine();

                    sw.WriteLine("配方ID,配方名称,原料,产物,时间");
                    foreach (var recipe in LDB.recipes.dataArray) {
                        int[] itemIDs = recipe.Items;
                        int[] itemCounts = recipe.ItemCounts;
                        int[] resultIDs = recipe.Results;
                        int[] resultCounts = recipe.ResultCounts;
                        double timeSpeed = recipe.TimeSpend / 60.0;
                        string s = recipe.ID + "," + FormatName(recipe.name, recipe.Name) + ",";
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
            }
            catch (Exception ex) {
                logger.LogError(ex.ToString());
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

        #region 邪教修改建筑耗电

        // //乐，虽然是邪教，但是确实管用
        // //代码源于SmelterMiner-jinxOAO
        //
        // //下面两个prefix+postfix联合作用。由于新版游戏实际执行的能量消耗、采集速率等属性都使用映射到的modelProto的prefabDesc中的数值，而不是itemProto的PrefabDesc，而修改/新增modelProto我还不会改，会报错（貌似是和模型读取不到有关）
        // //因此，提前修改设定建筑信息时读取的PrefabDesc的信息，在存储建筑属性前先修改一下（改成itemProto的PrefabDesc中对应的某些值），建造建筑设定完成后再改回去
        // //并且，原始item和model执向的貌似是同一个PrefabDesc，所以不能直接改model的，然后再还原成oriItem的prefabDesc，因为改了model的oriItem的也变了，还原不回去了。所以得Copy一个出来改。
        // [HarmonyPrefix]
        // [HarmonyPatch(typeof(PlanetFactory), "AddEntityDataWithComponents")]
        // public static bool AddEntityDataPrePatch(EntityData entity, out PrefabDesc __state)
        // {
        //     //不相关建筑直接返回（123、456是建筑的itemID）
        //     int gmProtoId = entity.protoId;
        //     if (gmProtoId != 123 && gmProtoId != 456)
        //     {
        //         __state = null;
        //         return true;
        //     }
        //     ItemProto itemProto = LDB.items.Select(entity.protoId);
        //     if (itemProto == null || !itemProto.IsEntity)
        //     {
        //         __state = null;
        //         return true;
        //     }
        //     //拷贝PrefabDesc然后修改
        //     ModelProto modelProto = LDB.models.Select(entity.modelIndex);
        //     __state = modelProto.prefabDesc;
        //     modelProto.prefabDesc = __state.Copy();
        //     modelProto.prefabDesc.workEnergyPerTick = itemProto.prefabDesc.workEnergyPerTick;
        //     modelProto.prefabDesc.idleEnergyPerTick = itemProto.prefabDesc.idleEnergyPerTick;
        //     return true;
        // }
        //
        // [HarmonyPostfix]
        // [HarmonyPatch(typeof(PlanetFactory), "AddEntityDataWithComponents")]
        // public static void AddEntityDataPostPatch(EntityData entity, PrefabDesc __state)
        // {
        //     if (__state == null)
        //     {
        //         return;
        //     }
        //     int gmProtoId = entity.protoId;
        //     if (gmProtoId != 123 && gmProtoId != 456)
        //     {
        //         return;
        //     }
        //     //还原PrefabDesc
        //     ModelProto modelProto = LDB.models.Select(entity.modelIndex);
        //     modelProto.prefabDesc = __state;
        // }

        #endregion

        #region 分馏原版逻辑梳理

        // public uint InternalUpdate(
        //     PlanetFactory factory,
        //     float power,
        //     SignData[] signPool,
        //     int[] productRegister,
        //     int[] consumeRegister) {
        //     //如果没电就不工作
        //     if ((double)power < 0.1f)
        //         return 0;
        //     //要处理的物品数目？一次只能处理0.001-4.0个物品。注意这是个double
        //     double num1 = 1.0;
        //     //fluidInputCount输入物品的数目  fluidInputCargoCount平均堆叠个数
        //     if (this.fluidInputCount == 0)
        //         //没有物品，平均堆叠自然是0
        //         this.fluidInputCargoCount = 0.0f;
        //     else
        //         //因为堆叠科技最大是4，所以fluidInputCount不可能大于4倍的fluidInputCargoCount
        //         num1 = (double)this.fluidInputCargoCount > 0.0001
        //             ? (double)this.fluidInputCount / (double)this.fluidInputCargoCount
        //             : 4.0;
        //     //运行分馏的条件：输入个数>0，流动输出个数未达缓存上限，产品输出个数未达缓存上限
        //     if (this.fluidInputCount > 0
        //         && this.productOutputCount < this.productOutputMax
        //         && this.fluidOutputCount < this.fluidOutputMax) {
        //         //反正是根据电力、要处理的数目（num1）来增加处理进度
        //         this.progress += (int)((double)power
        //                                * (500.0 / 3.0)
        //                                * ((double)this.fluidInputCargoCount < 30.0
        //                                    ? (double)this.fluidInputCargoCount
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
        //             this.fractionSuccess = (double)this.seed / 2147483646.0
        //                                    < (double)this.produceProb
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
        //             if ((double)this.fluidInputCargoCount < 0.0)
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
        //         else if (!this.isOutput1 && (double)this.fluidInputCargoCount < (double)this.fluidInputMax) {
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
        //         else if (!this.isOutput2 && (double)this.fluidInputCargoCount < (double)this.fluidInputMax) {
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
    }
}
