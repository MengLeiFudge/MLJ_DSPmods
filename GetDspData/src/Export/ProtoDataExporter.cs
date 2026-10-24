using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FE.Logic.Items;
using GetDspData.Utils;
using static GetDspData.Utils.Utils;
using static global::GetDspData.GetDspData;

namespace GetDspData.Export;

/// <summary>
/// 导出原型编号、CSV 与 FE 物品估值。
/// </summary>
internal static class ProtoDataExporter {
    /// <summary>
    /// 写出代码中使用的 protoID 常量快照到 protoID.txt。
    /// </summary>
    /// <param name="baseDir">当前模组组合导出目录。</param>
    internal static void WriteProtoIdFile(string baseDir) {
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
    internal static void WriteGameDataCsv(string baseDir) {
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
    /// 导出 FE 物品价值表，按价值升序写入 itemValue.csv。
    /// </summary>
    /// <param name="baseDir">当前启用模组组合对应的导出目录。</param>
    internal static void SaveFEItemValue(string baseDir) {
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
}
