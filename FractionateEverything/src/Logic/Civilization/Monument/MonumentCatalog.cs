using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Configuration;
using CommonAPI.Systems;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Items;
using HarmonyLib;
using UnityEngine;
using xiaoye97;
using static FE.FractionateEverything;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization.Monument;

/// <summary>按矩阵来源注册独立弹药；身份表保存到FE配置，增删矩阵不会重排已有弹药。</summary>
internal static class MonumentCatalog {
    private static ConfigEntry<string> identityConfig;
    private static readonly Dictionary<int, int> SailSources = [];
    private static readonly List<WorldviewRecipe> Recipes = [];
    internal static void Bind(ConfigFile config) => identityConfig = config.Bind("Monument", "SailIdentities", "",
        "Stable source-matrix:item identities. Keep this mapping with saves; multiplayer peers must use the same mapping.");

    /// <summary>固定物品随预注册添加，来源帆等最终外部矩阵进入LDB后再添加。</summary>
    internal static void PreAdd() {
        ItemProto rocket = ProtoRegistry.RegisterItem(IFE丰碑火箭, "FE.Monument.Rocket", "FE.Monument.RocketDescription",
            "Assets/fe/copy-essence", tab分馏 * 1000 + 112, 100, EItemType.Product,
            ProtoRegistry.GetDefaultIconDesc(new Color(0.8f, 0.65f, 1f), Color.gray));
        rocket.UnlockKey = -1;
        ItemProto material = ProtoRegistry.RegisterItem(IFE丰碑结晶, "FE.Monument.Crystal", "FE.Monument.CrystalDescription",
            "Assets/fe/monument-crystal", tab分馏 * 1000 + 113, 100, EItemType.Material,
            ProtoRegistry.GetDefaultIconDesc(new Color(1f, 0.8f, 0.3f), Color.black));
        material.UnlockKey = -1;
        ItemAvailability.RegisterExternalSource(IFE丰碑结晶);
    }

    /// <summary>PostAdd阶段已包含其他模组预注册的矩阵；通过LDBTool正式后注册入口加入原型。</summary>
    private static void PostAdd(ItemProto[] allItems, HashSet<int> recipeIds) {
        Dictionary<int, int> saved = [];
        foreach (string part in identityConfig.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) {
            string[] pair = part.Split(':');
            if (pair.Length != 2 || !int.TryParse(pair[0], out int source) || !int.TryParse(pair[1], out int item)
                || source <= 0 || source >= 12000 || item < 10000 || item >= 11800 || saved.ContainsKey(source) || saved.ContainsValue(item)) {
                throw new InvalidOperationException("丰碑帆稳定身份配置无效。");
            }
            saved.Add(source, item);
        }
        HashSet<int> used = new(saved.Values);
        used.UnionWith(recipeIds);
        foreach (ItemProto existing in allItems) { if (existing != null) { used.Add(existing.ID); } }
        ItemProto[] matrices = allItems.Where(item => item != null && (item.Type == EItemType.Matrix || item.ID == 5201))
            .OrderBy(item => item.ID).ToArray();
        int column = 0;
        foreach (ItemProto source in matrices) {
            if (!saved.TryGetValue(source.ID, out int id)) {
                id = Enumerable.Range(10000, 1800).FirstOrDefault(candidate => !used.Contains(candidate));
                if (id == 0) { throw new InvalidOperationException("丰碑帆物品身份空间已用尽。"); }
                saved.Add(source.ID, id); used.Add(id);
            }
            if (recipeIds.Contains(id) || allItems.Any(existing => existing.ID == id)) { throw new InvalidOperationException($"丰碑帆物品身份冲突：{id}。"); }
            var item = new ItemProto {
                ID = id, Name = $"FE.Monument.Sail.{source.ID}", Description = "FE.Monument.SailDescription",
                Type = EItemType.Product, StackSize = 100, UnlockKey = -1, IconPath = LDB.items.Select(I太阳帆).IconPath,
                GridIndex = 0, DescFields = [],
            };
            LDBTool.PostAddProto(item);
            if (item.ID != id) { throw new InvalidOperationException($"丰碑帆LDBTool重映射与FE身份表不一致：{id}→{item.ID}。"); }
            SailSources.Add(id, source.ID);
            var recipe = new WorldviewRecipe($"monument-sail:{source.ID}", 400000 + source.ID, item.Name,
                ERecipe.AdvancedConversion, WorldviewRecipeKind.Monument, 0.08f,
                [new(source.ID, 1), new(I太阳帆, 1)], [new(id, 10)]) { ResearchStage = 6, AnalysisItemId = source.ID };
            Recipes.Add(recipe);
            AdvancedRecipeCatalog.RegisterPostProto(recipe, id,
                tab分馏 * 1000 + (8 + column / 14) * 100 + column % 14 + 1);
            column++;
        }
        identityConfig.Value = string.Join(";", saved.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"));
        identityConfig.ConfigFile.Save();
        var rocketRecipe = new WorldviewRecipe("monument-rocket", 399999, "FE.Monument.Rocket",
            ERecipe.AdvancedConversion, WorldviewRecipeKind.Monument, 0.08f,
            [new(IFE实体元数据, 1), new(I小型运载火箭, 1)], [new(IFE丰碑火箭, 1)]) { ResearchStage = 6, AnalysisItemId = IFE实体元数据 };
        Recipes.Add(rocketRecipe);
        if (recipeIds.Contains(11990)) { throw new InvalidOperationException("丰碑火箭配方身份冲突：11990。"); }
        AdvancedRecipeCatalog.RegisterPostProto(rocketRecipe, 11990, tab分馏 * 1000 + 714);
    }

    private static bool registered;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(LDBTool), "AddProtos")]
    private static void LDBTool_AddProtos_Prefix(List<List<Proto>> datas) {
        if (registered || !ReferenceEquals(datas, AccessTools.Field(typeof(LDBTool), "PostToAdd").GetValue(null))) { return; }
        registered = true;
        ItemProto[] all = LDB.items.dataArray.Concat(datas.SelectMany(list => list).OfType<ItemProto>()).ToArray();
        var recipes = new HashSet<int>(LDB.recipes.dataArray.Select(recipe => recipe.ID));
        recipes.UnionWith(datas.SelectMany(list => list).OfType<RecipeProto>().Select(recipe => recipe.ID));
        PostAdd(all, recipes);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(LDBTool), "AddProtos")]
    private static void LDBTool_AddProtos_Postfix(List<List<Proto>> datas) {
        if (!registered || !ReferenceEquals(datas, AccessTools.Field(typeof(LDBTool), "PostToAdd").GetValue(null))) { return; }
        foreach (var pair in SailSources) {
            ItemProto item = LDB.items.Select(pair.Key);
            item.Preload(item.index);
        }
        LDB.items.Select(IFE丰碑火箭)._iconSprite = LDB.items.Select(I小型运载火箭).iconSprite;
        foreach (WorldviewRecipe recipe in Recipes) {
            RecipeProto proto = LDB.recipes.Select(AdvancedRecipeCatalog.GetProtoId(recipe.PersistentId));
            proto.Preload(Array.IndexOf(LDB.recipes.dataArray, proto));
        }
        RefreshNames();
    }

    /// <summary>握手与存档共用稳定身份签名，不允许同一物品编号被双方解释成不同矩阵。</summary>
    internal static string IdentityHash {
        get {
            using var hash = SHA256.Create();
            string mapping = string.Join(";", SailSources.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"));
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(mapping))).Replace("-", "");
        }
    }

    internal static void ExportIdentities(BinaryWriter writer) {
        writer.Write(SailSources.Count);
        foreach (var pair in SailSources.OrderBy(pair => pair.Key)) { writer.Write(pair.Key); writer.Write(pair.Value); }
    }

    internal static void ValidateIdentities(BinaryReader reader) {
        int count = reader.ReadInt32();
        if (count < 0 || count > 1800) { throw new InvalidDataException("丰碑弹药身份数量无效。"); }
        HashSet<int> seen = [];
        for (int i = 0; i < count; i++) {
            int item = reader.ReadInt32(), matrix = reader.ReadInt32();
            if (!seen.Add(item) || !SailSources.TryGetValue(item, out int expected) || expected != matrix) {
                throw new InvalidDataException($"丰碑弹药身份与存档不一致：{item}/{matrix}。请恢复存档对应的SailIdentities配置。");
            }
        }
    }

    internal static bool TryGetSource(int itemId, out int matrixId) => SailSources.TryGetValue(itemId, out matrixId);
    internal static int GetRecipeProtoId(int persistentId) => persistentId == 399999 ? 11990
        : SailSources.FirstOrDefault(pair => pair.Value == persistentId - 400000).Key;
    internal static bool IsSail(int itemId) => SailSources.ContainsKey(itemId);
    internal static bool IsMonumentItem(int itemId) => itemId is IFE丰碑火箭 or IFE丰碑结晶 || IsSail(itemId);
    internal static void AddRecipes() { foreach (WorldviewRecipe recipe in Recipes) { RecipeManager.AddRecipe(recipe); } }

    /// <summary>唯一价值表按制造投入分摊，不另建容量价格；结晶按一份火箭投入估值。</summary>
    internal static void ApplyValues() {
        float[] values = ItemManager.itemValue;
        foreach (var pair in SailSources) { values[pair.Key] = (values[pair.Value] + values[I太阳帆]) / 10f; }
        values[IFE丰碑火箭] = values[IFE实体元数据] + values[I小型运载火箭];
        values[IFE丰碑结晶] = values[IFE丰碑火箭];
        RefreshNames();
    }

    /// <summary>动态名称由来源物品及通用五语模板生成，不把语言文本用作持久身份。</summary>
    internal static void RefreshNames() {
        foreach (var pair in SailSources) {
            string name = string.Format("FE.Monument.SailName".Translate(), LDB.items.Select(pair.Value)?.name ?? pair.Value.ToString());
            ItemProto item = LDB.items.Select(pair.Key);
            if (item != null) { item.name = name; }
            RecipeProto recipe = LDB.recipes.Select(pair.Key);
            if (recipe != null) { recipe.name = name; }
            WorldviewRecipe runtime = Recipes.Find(value => value.PersistentId == 400000 + pair.Value);
            if (runtime != null) { runtime.DisplayName = name; }
        }
        WorldviewRecipe rocket = Recipes.Find(value => value.PersistentId == 399999);
        if (rocket != null) { rocket.DisplayName = "FE.Monument.Rocket".Translate(); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RecipeProto), nameof(RecipeProto.RefreshTranslation))]
    private static void RecipeProto_RefreshTranslation_Postfix(RecipeProto __instance) {
        if (TryGetSource(__instance.ID, out int matrix)) {
            __instance.name = string.Format("FE.Monument.SailName".Translate(), LDB.items.Select(matrix)?.Name.Translate() ?? matrix.ToString());
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemProto), nameof(ItemProto.RefreshTranslation))]
    private static void ItemProto_RefreshTranslation_Postfix(ItemProto __instance) {
        if (TryGetSource(__instance.ID, out int matrix)) {
            __instance.name = string.Format("FE.Monument.SailName".Translate(), LDB.items.Select(matrix)?.Name.Translate() ?? matrix.ToString());
            WorldviewRecipe runtime = Recipes.Find(value => value.PersistentId == 400000 + matrix);
            if (runtime != null) { runtime.DisplayName = __instance.name; }
        }
    }
}
