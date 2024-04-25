using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CommonAPI;
using CommonAPI.Systems;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using xiaoye97;

namespace GetDspData
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency(CommonAPIPlugin.GUID)]
    [CommonAPISubmoduleDependency(nameof(ProtoRegistry))]
    public class GetDspData : BaseUnityPlugin
    {
        private const string GUID = "com.menglei.dsp.GetDspData";
        private const string NAME = "Get DSP Data";
        private const string VERSION = "1.0.0";
        private static ManualLogSource logger;

        private static string dir;

        public void Awake()
        {
            logger = Logger;

            ConfigEntry<string> BaseDir = Config.Bind("config", "BaseDir", "", "在哪个目录生成文件，为空表示使用桌面");
            dir = string.IsNullOrEmpty(BaseDir.Value)
                ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
                : BaseDir.Value;

            LDBTool.PostAddDataAction += WriteDataToFile;
            Harmony.CreateAndPatchAll(typeof(GetDspData), GUID);
        }

        Dictionary<int, string> itemIdNameDic = new();
        Dictionary<string, int> modelNameIdDic = new();
        private readonly Regex regex = new Regex(".+分馏.+");

        private void WriteDataToFile()
        {
            try
            {
                //代码中使用
                using (var sw = new StreamWriter(dir + "\\DSP_ProtoID.cs"))
                {
                    sw.WriteLine("static class ProtoID");
                    sw.WriteLine("{");

                    foreach (var item in LDB.items.dataArray)
                    {
                        int id = item.ID;
                        string name = FormatName(item.name);
                        sw.WriteLine($"    internal const int I{name} = {id};");
                        itemIdNameDic.Add(id, name);
                        int modelID = item.ModelIndex;
                        if (modelID > 0)
                        {
                            modelNameIdDic.Add(name, modelID);
                        }
                    }

                    sw.WriteLine();

                    foreach (var p in modelNameIdDic)
                    {
                        sw.WriteLine($"    internal const int M{p.Key} = {p.Value};");
                    }

                    sw.WriteLine();

                    foreach (var recipe in LDB.recipes.dataArray)
                    {
                        int id = recipe.ID;
                        string name = FormatName(recipe.name);
                        if (regex.Matches(name).Count == 0)
                        {
                            sw.WriteLine($"    internal const int R{name} = {id};");
                        }
                    }

                    sw.WriteLine();

                    string lastTechName = "";
                    foreach (var tech in LDB.techs.dataArray)
                    {
                        int id = tech.ID;
                        string name = FormatName(tech.name);
                        if (name == lastTechName)
                        {
                            continue;
                        }
                        lastTechName = name;
                        sw.WriteLine($"    internal const int T{name} = {id};");
                    }

                    sw.Write("}");
                }

                //csv数据
                using (StreamWriter sw = new StreamWriter(dir + "\\DSP_DataInfo.csv"))
                {
                    sw.WriteLine("物品ID,物品名称,index(自动排序位置),BuildMode(建造类型),BuildIndex(建造栏位置)");
                    foreach (var item in LDB.items.dataArray)
                    {
                        sw.WriteLine(item.ID + "," + item.name + "," + item.index + "," + item.BuildMode + "," + item.BuildIndex);
                    }
                    sw.WriteLine();
                    sw.WriteLine();

                    sw.WriteLine("配方ID,配方名称,原料,产物,时间");
                    foreach (var recipe in LDB.recipes.dataArray)
                    {
                        int[] itemIDs = recipe.Items;
                        int[] itemCounts = recipe.ItemCounts;
                        int[] resultIDs = recipe.Results;
                        int[] resultCounts = recipe.ResultCounts;
                        double timeSpeed = recipe.TimeSpend / 60.0;
                        string s = recipe.ID + "," + recipe.name + ",";
                        for (int i = 0; i < itemIDs.Length; i++)
                        {
                            s += itemIDs[i] + "(" + itemIdNameDic[itemIDs[i]] + ")*" + itemCounts[i] + " + ";
                        }
                        s = s.Substring(0, s.Length - 3) + " -> ";
                        for (int i = 0; i < resultIDs.Length; i++)
                        {
                            s += resultIDs[i] + "(" + itemIdNameDic[resultIDs[i]] + ")*" + resultCounts[i] + " + ";
                        }
                        s = s.Substring(0, s.Length - 3) + ",";
                        s += recipe.TimeSpend + "(" + timeSpeed.ToString("F1") + "s)";
                        sw.WriteLine(s);
                    }
                    sw.WriteLine();
                    sw.WriteLine();

                    sw.WriteLine("科技ID,科技名称,解锁配方");
                    foreach (var tech in LDB.techs.dataArray)
                    {
                        sw.Write(tech.ID + "," + tech.name);
                        foreach (var recipeID in tech.UnlockRecipes)
                        {
                            RecipeProto recipe = LDB.recipes.Select(recipeID);
                            sw.Write("," + FormatName(recipe.name));
                        }
                        sw.WriteLine();
                    }
                    sw.WriteLine();
                    sw.WriteLine();

                    sw.WriteLine("模型ID,name,displayName,PrefabPath");
                    foreach (var model in LDB.models.dataArray)
                    {
                        sw.WriteLine(model.ID + "," + model.name + "," + model.displayName + "," + model.PrefabPath);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
            }
        }

        string FormatName(string str)
        {
            return str.Replace(" ", "")
                .Replace(" ", "")
                .Replace(" ", "")
                .Replace("!", "")
                .Replace("-", "")
                .Replace(".", "")
                .Replace("（", "")
                .Replace("）", "")
                .Replace("）", "");
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
    }
}
