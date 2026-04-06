using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace VanillaCurveSim;

internal static class DataLoader {
    private static readonly Regex AmountRegex = new(@"([^\(]+)\(([IRT])(\d+)\)\*(.+)", RegexOptions.Compiled);

    public static GameDataSet Load(string solutionDir) {
        var dataSet = new GameDataSet();
        string csvPath = Path.Combine(solutionDir, "gamedata", "Vanilla", "gameData.csv");
        string jsonPath = Path.Combine(solutionDir, "gamedata", "calc json", "Vanilla.json");
        string itemJsonPath = Path.Combine(solutionDir, "gamedata", "DecompiledSource", "ProjectOrbitalRing",
            "ProjectOrbitalRing.data.items_vanilla.json");
        LoadGameDataCsv(csvPath, dataSet);
        LoadCalcJson(jsonPath, dataSet);
        LoadItemDetails(itemJsonPath, dataSet);
        return dataSet;
    }

    private static void LoadGameDataCsv(string csvPath, GameDataSet dataSet) {
        foreach (string rawLine in File.ReadLines(csvPath)) {
            string line = rawLine.TrimStart('\uFEFF');
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("物品ID")) {
                continue;
            }

            string[] parts = line.Split(',');
            string code = parts[0];
            if (code.Length < 2) {
                continue;
            }

            switch (code[0]) {
                case 'I':
                    ParseItem(parts, dataSet);
                    break;
                case 'R':
                    ParseRecipe(parts, dataSet);
                    break;
                case 'T':
                    ParseTech(parts, dataSet);
                    break;
            }
        }
    }

    private static void ParseItem(string[] parts, GameDataSet dataSet) {
        var item = new VanillaItem {
            Code = parts[0],
            Id = ParseNumericCode(parts[0]),
            Name = parts.ElementAtOrDefault(2) ?? string.Empty,
            ItemType = parts.ElementAtOrDefault(4) ?? string.Empty,
            BuildMode = ParseInt(parts.ElementAtOrDefault(5)),
            BuildIndex = ParseInt(parts.ElementAtOrDefault(6)),
            MainCraftCode = NormalizeNullable(parts.ElementAtOrDefault(7)),
            PreTechCode = NormalizeNullable(parts.ElementAtOrDefault(9)),
        };
        dataSet.ItemsById[item.Id] = item;
    }

    private static void ParseRecipe(string[] parts, GameDataSet dataSet) {
        var recipe = new VanillaRecipe {
            Code = parts[0],
            Id = ParseNumericCode(parts[0]),
            Name = parts.ElementAtOrDefault(2) ?? string.Empty,
            RecipeType = parts.ElementAtOrDefault(4) ?? string.Empty,
            TimeSpend = ParseInt(parts.ElementAtOrDefault(7)?.Split('(')[0]),
            Productive = ParseBool(parts.ElementAtOrDefault(9)),
            PreTechCode = NormalizeNullable(parts.ElementAtOrDefault(10)),
        };
        recipe.Inputs.AddRange(ParseAmounts(parts.ElementAtOrDefault(5)));
        recipe.Outputs.AddRange(ParseAmounts(parts.ElementAtOrDefault(6)));
        dataSet.RecipesById[recipe.Id] = recipe;
        foreach (RecipeAmount output in recipe.Outputs) {
            if (!dataSet.RecipesByOutputId.TryGetValue(output.Id, out List<VanillaRecipe> recipes)) {
                recipes = [];
                dataSet.RecipesByOutputId[output.Id] = recipes;
            }
            recipes.Add(recipe);
        }
    }

    private static void ParseTech(string[] parts, GameDataSet dataSet) {
        var tech = new VanillaTech {
            Code = parts[0],
            Id = ParseNumericCode(parts[0]),
            Name = parts.ElementAtOrDefault(1) ?? string.Empty,
            IsHiddenTech = string.Equals(parts.ElementAtOrDefault(5), "True", StringComparison.OrdinalIgnoreCase),
            PreItemCode = NormalizeNullable(parts.ElementAtOrDefault(6)),
            HashNeeded = ParseInt(parts.ElementAtOrDefault(8)),
        };
        tech.PreTechCodes.AddRange(ParseCodeList(parts.ElementAtOrDefault(3)));
        tech.ImplicitPreTechCodes.AddRange(ParseCodeList(parts.ElementAtOrDefault(4)));
        tech.CostItems.AddRange(ParseAmounts(parts.ElementAtOrDefault(7)));
        tech.UnlockTargets.AddRange(ParseUnlockTargets(parts.ElementAtOrDefault(9)));
        dataSet.TechsById[tech.Id] = tech;
    }

    private static void LoadCalcJson(string jsonPath, GameDataSet dataSet) {
        JObject root = JObject.Parse(File.ReadAllText(jsonPath));
        foreach (JObject recipeToken in root["recipes"]?.OfType<JObject>() ?? []) {
            var recipe = new CalcRecipe {
                Name = recipeToken.Value<string>("Name") ?? string.Empty,
                TimeSpend = recipeToken.Value<int?>("TimeSpend") ?? 0,
                Proliferator = recipeToken.Value<int?>("Proliferator") ?? 0,
            };

            recipe.Factories.AddRange(recipeToken["Factories"]?.Values<int>() ?? []);
            recipe.Items.AddRange(recipeToken["Items"]?.Values<int>() ?? []);
            recipe.ItemCounts.AddRange(recipeToken["ItemCounts"]?.Values<double>() ?? []);
            recipe.Results.AddRange(recipeToken["Results"]?.Values<int>() ?? []);
            recipe.ResultCounts.AddRange(recipeToken["ResultCounts"]?.Values<double>() ?? []);

            foreach (int resultId in recipe.Results.Distinct()) {
                if (!dataSet.CalcRecipesByOutputId.TryGetValue(resultId, out List<CalcRecipe> recipes)) {
                    recipes = [];
                    dataSet.CalcRecipesByOutputId[resultId] = recipes;
                }
                recipes.Add(recipe);
            }
        }

        foreach (JObject itemToken in root["items"]?.OfType<JObject>() ?? []) {
            int id = itemToken.Value<int?>("ID") ?? 0;
            if (id <= 0 || !dataSet.ItemsById.TryGetValue(id, out VanillaItem item)) {
                continue;
            }

            item.Space = itemToken.Value<double?>("Space") ?? item.Space;
            item.Speed = itemToken.Value<double?>("Speed") ?? item.Speed;
            item.WorkEnergyPerTick = itemToken.Value<long?>("WorkEnergyPerTick") ?? item.WorkEnergyPerTick;
        }
    }

    private static void LoadItemDetails(string jsonPath, GameDataSet dataSet) {
        string cleaned = StripJsonLineComments(File.ReadAllText(jsonPath));
        JArray items = JArray.Parse(cleaned);
        foreach (JObject itemToken in items.OfType<JObject>()) {
            int id = itemToken.Value<int?>("ID") ?? 0;
            if (id <= 0 || !dataSet.ItemsById.TryGetValue(id, out VanillaItem item)) {
                continue;
            }

            item.StackSize = itemToken.Value<int?>("StackSize") ?? item.StackSize;
        }
    }

    private static string StripJsonLineComments(string text) {
        var result = new List<string>();
        foreach (string line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)) {
            bool inString = false;
            int cut = -1;
            for (int i = 0; i < line.Length - 1; i++) {
                char c = line[i];
                if (c == '"' && (i == 0 || line[i - 1] != '\\')) {
                    inString = !inString;
                }
                if (!inString && c == '/' && line[i + 1] == '/') {
                    cut = i;
                    break;
                }
            }
            result.Add(cut >= 0 ? line.Substring(0, cut) : line);
        }
        return string.Join("\n", result);
    }

    private static IEnumerable<RecipeAmount> ParseAmounts(string text) {
        if (string.IsNullOrWhiteSpace(text) || text == "empty" || text == "null") {
            yield break;
        }

        foreach (string segment in text.Split(new[] { " + " }, StringSplitOptions.RemoveEmptyEntries)) {
            Match match = AmountRegex.Match(segment.Trim());
            if (!match.Success) {
                continue;
            }

            yield return new RecipeAmount {
                Name = match.Groups[1].Value.Trim(),
                Code = $"{match.Groups[2].Value}{match.Groups[3].Value}",
                Id = ParseInt(match.Groups[3].Value),
                Count = ParseDouble(match.Groups[4].Value),
            };
        }
    }

    private static IEnumerable<string> ParseCodeList(string text) {
        if (string.IsNullOrWhiteSpace(text) || text == "empty" || text == "null") {
            yield break;
        }

        foreach (string segment in text.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries)) {
            int left = segment.LastIndexOf('(');
            int right = segment.LastIndexOf(')');
            if (left >= 0 && right > left) {
                yield return segment.Substring(left + 1, right - left - 1);
            }
        }
    }

    private static IEnumerable<UnlockTarget> ParseUnlockTargets(string text) {
        if (string.IsNullOrWhiteSpace(text) || text == "empty" || text == "null") {
            yield break;
        }

        foreach (string segment in text.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries)) {
            int left = segment.LastIndexOf('(');
            int right = segment.LastIndexOf(')');
            if (left < 0 || right <= left) {
                continue;
            }

            string code = segment.Substring(left + 1, right - left - 1);
            if (code.Length < 2) {
                continue;
            }

            yield return new UnlockTarget {
                Kind = code[0],
                Code = code,
                Id = ParseInt(code.Substring(1)),
                Name = segment.Substring(0, left),
            };
        }
    }

    private static string NormalizeNullable(string text) =>
        string.IsNullOrWhiteSpace(text) || text == "null" || text == "empty" ? string.Empty : text.Trim();

    private static int ParseNumericCode(string code) => ParseInt(code.Substring(1));

    private static int ParseInt(string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;

    private static double ParseDouble(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0d;

    private static bool ParseBool(string text) =>
        bool.TryParse(text, out bool value) && value;
}
