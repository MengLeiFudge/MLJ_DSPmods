using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FE.Logic.Fractionation.Recipes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VanillaCurveSim;

/// <summary>读取现行游戏导出；缺字段或配方数值过时就停止，避免把旧模型当作当前结果。</summary>
internal static class FeDataLoader {
    public static FeDataSet Load(string root) {
        var data = new FeDataSet();
        string recipePath = Path.Combine(root, "gamedata", "calc json", "FractionateEverything.json");
        string valuePath = Path.Combine(root, "gamedata", "FE", "itemValue.csv");
        string itemPath = Path.Combine(root, "gamedata", "FE", "gameData.csv");
        foreach (string path in new[] { recipePath, valuePath, itemPath }) {
            using SHA256 sha = SHA256.Create();
            using Stream stream = File.OpenRead(path);
            data.SourceSha256[path.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/')] =
                BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        foreach (string line in File.ReadLines(itemPath)) {
            string[] fields = line.TrimStart('\uFEFF').Split(',');
            if (fields.Length < 5 || !fields[0].StartsWith("I", StringComparison.Ordinal)
                || !int.TryParse(fields[0].Substring(1), out int id)) { continue; }
            data.Names.Add(id, fields[2]);
            if (fields[4] == "Matrix" || id == 5201) { data.Foods.Add(id); }
        }
        foreach (string line in File.ReadLines(valuePath)) {
            string[] fields = line.TrimStart('\uFEFF').Split(',');
            if (fields.Length < 3 || !int.TryParse(fields[0], out int id)) { continue; }
            double value = double.Parse(fields[fields.Length - 1], CultureInfo.InvariantCulture);
            if (Finite(value) && value > 0 && value < float.MaxValue) { data.Values.Add(id, value); }
        }
        JObject document = JObject.Parse(File.ReadAllText(recipePath));
        foreach (JObject row in (document["recipes"] as JArray
                     ?? throw new InvalidDataException("FE导出缺少recipes数组。")).OfType<JObject>()) {
            int[] factories = row["Factories"]?.Values<int>().ToArray() ?? [];
            if (!factories.Any(id => id is 8121 or 8123 or 8124 or 8125)) { continue; }
            foreach (string field in new[] { "StableID", "RequiresFood", "SuccessRatio", "InputCountsPerRound",
                         "InputConsumeProbabilities", "Items", "Results", "ResultCounts" }) {
                if (row[field] == null) { throw new InvalidDataException($"FE配方缺少{field}，请重新导出当前游戏数据。"); }
            }
            var recipe = new FeRecipe {
                Id = row.Value<string>("StableID"), Name = row.Value<string>("Name"),
                TowerId = factories.Single(), RequiresFood = row.Value<bool>("RequiresFood"),
                SuccessProbability = row.Value<float>("SuccessRatio"),
                Inputs = row["Items"].Values<int>().ToArray(),
                InputCounts = row["InputCountsPerRound"].Values<int>().ToArray(),
                ConsumeProbabilities = row["InputConsumeProbabilities"].Values<float>().ToArray(),
                Outputs = row["Results"].Values<int>().ToArray(),
                OutputCounts = row["ResultCounts"].Values<float>().ToArray(),
            };
            ValidateRecipe(recipe, data);
            data.Recipes.Add(recipe);
        }
        if (data.Recipes.Count == 0 || data.Recipes.Select(r => r.Id).Distinct().Count() != data.Recipes.Count) {
            throw new InvalidDataException("FE导出没有现行生产配方，或StableID重复。");
        }
        // 转化目录已与FE共用纯数值规格；核对快照，禁止在过时输出上叠加新公式。
        foreach (ConversionSpec spec in ConversionSpec.CreateAll()) {
            FeRecipe recipe = data.Recipes.SingleOrDefault(r => r.Id == spec.Key);
            if (recipe == null) { continue; } // 合法来源闭包可以移除当前模组组合不可用的配方。
            if (!recipe.Inputs.SequenceEqual(spec.Inputs.Select(i => i.Item))
                || !recipe.InputCounts.SequenceEqual(spec.Inputs.Select(i => i.Count))
                || !recipe.ConsumeProbabilities.SequenceEqual(spec.Inputs.Select(i => i.Consume))
                || !recipe.Outputs.SequenceEqual(spec.Outputs.Select(o => o.Item))
                || !recipe.OutputCounts.SequenceEqual(spec.Outputs.Select(o => o.Count * (o.Recovery ? 0.5f : 1f)))) {
                throw new InvalidDataException($"{recipe.Id}与当前ConversionSpec不一致，请重新导出零特质数据。");
            }
        }
        data.Recipes.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
        return data;
    }

    public static FeSimulationOptions LoadOptions(string path, FeDataSet data) {
        FeSimulationOptions options = path == null ? new() : JsonConvert.DeserializeObject<FeSimulationOptions>(
            File.ReadAllText(path), new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
        if (options == null || options.DurationSeconds < 60 || options.DurationSeconds > 3600
            || options.DurationSeconds % 60 != 0 || options.Scenarios == null
            || options.Scenarios.Count is < 1 or > 20) {
            throw new InvalidDataException("模拟时长须为60～3600秒的整分钟，工况须为1～20项。");
        }
        if (options.Scenarios.Any(s => s == null || string.IsNullOrWhiteSpace(s.Name))
            || options.Scenarios.Select(s => s.Name).Distinct().Count() != options.Scenarios.Count) {
            throw new InvalidDataException("工况名称不能为空或重复。");
        }
        foreach (FeScenarioConfig scenario in options.Scenarios) {
            if (scenario.Stack is < 1 or > 20 || scenario.TraitLevel is < 0 or > 6
                || scenario.FoodInc is < 0 or > 10 || !data.Foods.Contains(scenario.FoodItemId)
                || !data.Values.ContainsKey(scenario.FoodItemId) || !data.Values.ContainsKey(6001)
                || !Finite(scenario.MemoryPassesPerSecond) || scenario.MemoryPassesPerSecond < 0
                || scenario.MemoryPassesPerSecond > 30d * scenario.Stack) {
                throw new InvalidDataException($"工况“{scenario.Name}”的堆叠、特质、食物或源点速率无效。");
            }
        }
        return options;
    }

    private static void ValidateRecipe(FeRecipe r, FeDataSet data) {
        if (string.IsNullOrWhiteSpace(r.Id) || string.IsNullOrWhiteSpace(r.Name)
            || r.Inputs.Length == 0 || r.Outputs.Length == 0
            || r.Inputs.Length != r.InputCounts.Length || r.Inputs.Length != r.ConsumeProbabilities.Length
            || r.Outputs.Length != r.OutputCounts.Length
            || r.Inputs.Distinct().Count() != r.Inputs.Length || r.Outputs.Distinct().Count() != r.Outputs.Length
            || r.InputCounts.Any(n => n <= 0) || r.OutputCounts.Any(n => !Finite(n) || n <= 0)
            || r.ConsumeProbabilities.Any(p => !Finite(p) || p < 0 || p > 1)
            || !r.ConsumeProbabilities.Any(p => p >= 0.9999f)
            || !Finite(r.SuccessProbability) || r.SuccessProbability <= 0 || r.SuccessProbability > 1
            || r.Inputs.Concat(r.Outputs).Any(id => !data.Values.ContainsKey(id))
            || r.RequiresFood != (r.TowerId != 8123)) {
            throw new InvalidDataException($"FE配方字段或物品估值无效：{r.Id}");
        }
        if (!r.RequiresFood && (r.Inputs.Length != 1 || r.InputCounts[0] != 1
            || r.Outputs.Length != 1 || r.Outputs[0] != r.Inputs[0] || r.OutputCounts[0] != 2f)) {
            throw new InvalidDataException($"{r.Id}不符合当前同物1→2的基础矿物配方。");
        }
        bool stale = r.RequiresFood ? Math.Abs(r.SuccessProbability - 0.08f) > 1e-6
            : Math.Abs(r.SuccessProbability - 0.05f) > 1e-6;
        stale |= r.TowerId == 8125 && r.ConsumeProbabilities.Any(p => p < 1 && Math.Abs(p - 0.05f) > 1e-6);
        stale |= r.Id.StartsWith("interaction-breed:", StringComparison.Ordinal) && r.OutputCounts[0] != 1.5f;
        if (stale) { throw new InvalidDataException($"{r.Id}不是当前零特质基础数据，请重新导出。"); }
    }

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
