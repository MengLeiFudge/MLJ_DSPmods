using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FE.Logic.Enhancements.Stacking;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Items;
using static FE.Utils.Utils;

namespace FE.Logic.Fractionation.Fractionators;

public static partial class AdvancedFractionatorRuntime {
    /// <summary>主机尚未广播的删除记录；与实例变更一起按秒发送，不写入存档。</summary>
    private static readonly ConcurrentDictionary<(int planetId, int fractionatorId), byte> RemovedStates = [];

    /// <summary>读取独立元素边界的高级塔状态，清除上一存档的状态和同步记录。</summary>
    /// <param name="reader">当前高级塔存档块。</param>
    public static void Import(BinaryReader reader) {
        IntoOtherSave();
        ReadStates(reader, null);
    }

    /// <summary>读取已有的顺序布局高级塔数据。</summary>
    public static void ImportLegacy(BinaryReader reader) {
        IntoOtherSave();
        ReadLegacyStates(reader, null);
    }

    /// <summary>写入独立的单塔记录，单塔内部的缓存字段也分别分块。</summary>
    /// <param name="writer">存档或加入时的完整快照。</param>
    public static void Export(BinaryWriter writer) {
        var pairs = States.OrderBy(pair => pair.Key.planetId).ThenBy(pair => pair.Key.fractionatorId).ToArray();
        writer.WriteList(pairs, (item, pair) => {
            lock (pair.Value) { WriteState(item, pair.Key, pair.Value); }
        });
    }

    /// <summary>写入本秒删除记录和变化的塔；未变化实例不加锁、不序列化。</summary>
    /// <param name="writer">联机增量数据，先删除、后覆盖。</param>
    /// <returns>是否存在需要发送的内容。</returns>
    public static bool ExportChanges(BinaryWriter writer) {
        List<(int planetId, int fractionatorId)> removed = [];
        foreach (var key in RemovedStates.Keys) {
            if (RemovedStates.TryRemove(key, out _) && !States.ContainsKey(key)) { removed.Add(key); }
        }
        var changed = States.Where(pair => Volatile.Read(ref pair.Value.SnapshotDirty)).ToArray();
        if (removed.Count == 0 && changed.Length == 0) { return false; }

        writer.Write(removed.Count);
        foreach (var key in removed) {
            writer.Write(key.planetId);
            writer.Write(key.fractionatorId);
        }
        writer.WriteList(changed, (item, pair) => {
            lock (pair.Value) {
                WriteState(item, pair.Key, pair.Value);
                pair.Value.SnapshotDirty = false;
            }
        });
        return true;
    }

    /// <summary>应用主机快照，复用已有实例对象；只有全量快照才删除未出现的塔。</summary>
    /// <param name="reader">已由协议版本校验的主机数据。</param>
    /// <param name="fullSnapshot">是否为玩家加入时的完整快照。</param>
    public static void ApplySnapshot(BinaryReader reader, bool fullSnapshot) {
        if (fullSnapshot) {
            HashSet<(int planetId, int fractionatorId)> seen = [];
            ReadStates(reader, seen);
            foreach (var key in States.Keys) {
                if (!seen.Contains(key)) { States.TryRemove(key, out _); }
            }
        } else {
            int count = ReadStateCount(reader);
            for (int i = 0; i < count; i++) {
                States.TryRemove((reader.ReadInt32(), reader.ReadInt32()), out _);
            }
            ReadStates(reader, null);
        }
    }

    /// <summary>切档时清除实例及尚未广播的删除记录。</summary>
    public static void IntoOtherSave() {
        States.Clear();
        RemovedStates.Clear();
    }

    /// <summary>读取受上限约束的实例数量，拒绝异常长度。</summary>
    /// <param name="reader">存档或快照流。</param>
    /// <returns>0 到 MaximumStateCount 的实例数。</returns>
    private static int ReadStateCount(BinaryReader reader) {
        int count = reader.ReadInt32();
        if (count < 0 || count > MaximumStateCount) {
            throw new InvalidDataException($"Invalid advanced fractionator state count: {count}");
        }
        return count;
    }

    /// <summary>先读取稳定实例身份，再在原对象锁内恢复字段；缓存间不依赖字节顺序。</summary>
    private static void ReadStates(BinaryReader reader, HashSet<(int planetId, int fractionatorId)> seen) {
        reader.ReadList(item => {
            (int planetId, int fractionatorId) key = default;
            bool restored = false;
            item.ReadBlocks(
                ("Identity", r => key = (r.ReadInt32(), r.ReadInt32())),
                ("State", r => {
                    if (key.planetId <= 0 || key.fractionatorId <= 0) { return; }
                    AdvancedFractionatorState state = States.GetOrAdd(key, _ => new AdvancedFractionatorState());
                    lock (state) {
                        state.SelectedRecipeId = state.OutputCursor = state.FlowOutputCursor = 0;
                        ResetInputFlow(state);
                        state.CirculationIntakePending = false;
                        ClearDigestion(state);
                        state.Inputs.Clear(); state.InputInc.Clear(); state.InputCargo.Clear();
                        state.Foods.Clear(); state.FoodInc.Clear(); state.Products.Clear();
                        state.FlowOutputs.Clear(); state.FlowOutputInc.Clear();
                        r.ReadBlocks(
                            ("Recipe", field => {
                                int id = field.ReadInt32();
                                state.SelectedRecipeId = RecipeManager.GetRecipe(id)?.IsAdvanced == true ? id : 0;
                            }),
                            ("Digestion", field => {
                                int id = field.ReadInt32(); double energy = field.ReadDouble();
                                int inc = Math.Max(0, Math.Min(10, field.ReadInt32()));
                                double capacity = GetFoodEnergy(id, inc);
                                if (capacity > 0d && !double.IsNaN(energy) && !double.IsInfinity(energy) && energy > 0d) {
                                    state.DigestingItemId = id; state.DigestingEnergy = Math.Min(1d, energy) * capacity;
                                    state.DigestingInc = inc; state.DigestionVersion++;
                                }
                            }),
                            ("OutputCursor", field => state.OutputCursor = Math.Max(0, field.ReadInt32())),
                            ("CirculationIntakePending", field => state.CirculationIntakePending = field.ReadBoolean()),
                            ("Inputs", field => ReadInventory(field, state.Inputs, MaximumBufferCapacity)),
                            ("InputInc", field => ReadInventory(field, state.InputInc, MaximumBufferCapacity * 10)),
                            ("Foods", field => ReadInventory(field, state.Foods, MaximumBufferCapacity)),
                            ("FoodInc", field => ReadInventory(field, state.FoodInc, MaximumBufferCapacity * 10)),
                            ("Products", field => ReadInventory(field, state.Products, MaximumBufferCapacity)),
                            ("FlowOutputs", field => ReadInventory(field, state.FlowOutputs, MaximumBufferCapacity)),
                            ("FlowOutputInc", field => ReadInventory(field, state.FlowOutputInc, MaximumBufferCapacity * 10)),
                            ("InputCargo", field => field.ReadList(cargoReader => {
                                int id = cargoReader.ReadInt32(); double cargo = cargoReader.ReadDouble();
                                if (!double.IsNaN(cargo) && !double.IsInfinity(cargo) && cargo > 0d) { state.InputCargo[id] = cargo; }
                            }, MaximumDictionaryEntryCount)),
                            ("ProcessingRate", field => {
                                double value = field.ReadDouble();
                                state.ProcessingRate = double.IsNaN(value) || double.IsInfinity(value) ? 0d
                                    : Math.Max(0d, Math.Min(MaximumBufferCapacity * 60d, value));
                            }),
                            ("InputProgress", field => ReadFlowDictionary(field, state.InputProgress, MaximumBufferCapacity)),
                            ("InputRates", field => ReadFlowDictionary(field, state.InputRates, MaximumBufferCapacity * 60d))
                        );
                        ClampIncDictionary(state.InputInc, state.Inputs);
                        RemoveNonFoodEntries(state.Foods, state.FoodInc);
                        ClampIncDictionary(state.FoodInc, state.Foods);
                        ClampIncDictionary(state.FlowOutputInc, state.FlowOutputs);
                        if (state.Foods.Count > 1 || SumCounts(state.Foods) > StackingManager.AbsoluteMaxStack
                            || state.DigestingEnergy > 0d && state.Foods.Keys.Any(id => id != state.DigestingItemId)) {
                            state.Foods.Clear(); state.FoodInc.Clear();
                            LogWarning("[Save] 高级塔备用食物不符合当前约束，已跳过该缓存。");
                        }
                        foreach (int id in state.InputCargo.Keys.ToArray()) {
                            if (!state.Inputs.ContainsKey(id)) { state.InputCargo.Remove(id); }
                        }
                        foreach (var input in state.Inputs) {
                            // 货物元数据缺失时按单件堆恢复，保留物品并避免满缓存永久停机。
                            double cargo = state.InputCargo.TryGetValue(input.Key, out double saved) ? saved : input.Value;
                            state.InputCargo[input.Key] = Math.Max(input.Value / (double)StackingManager.AbsoluteMaxStack, Math.Min(input.Value, cargo));
                        }
                        WorldviewRecipe recipe = RecipeManager.GetRecipe(state.SelectedRecipeId);
                        foreach (int id in state.InputProgress.Keys.ToArray()) {
                            if (GetCount(state.Inputs, id) <= 0) {
                                state.InputProgress.Remove(id);
                            } else {
                                state.InputProgress[id] = Math.Min(state.InputProgress[id], GetCount(state.Inputs, id));
                            }
                        }
                        foreach (int id in state.InputRates.Keys.ToArray()) {
                            if (recipe == null || !recipe.TryGetInput(id, out WorldviewRecipeInput input)
                                || input.ConsumeProbability < 0.9999f) { state.InputRates.Remove(id); }
                        }
                        restored = true;
                    }
                })
            );
            if (restored) { seen?.Add(key); }
        }, MaximumStateCount);
    }

    /// <summary>库存字典逐项有边界；无效物品只影响该项。</summary>
    private static void ReadInventory(BinaryReader reader, Dictionary<int, int> target, int maximum) {
        reader.ReadList(item => {
            int id = item.ReadInt32(), count = item.ReadInt32();
            if (id > 0 && id < 12000 && count > 0) { target[id] = Math.Min(maximum, count); }
        }, MaximumDictionaryEntryCount);
    }

    /// <summary>独立读取各主料进度或速率；无效项只跳过该项，不依赖其他字段的读取顺序。</summary>
    private static void ReadFlowDictionary(BinaryReader reader, Dictionary<int, double> target, double maximum) {
        reader.ReadList(item => {
            int id = item.ReadInt32();
            double value = item.ReadDouble();
            if (id > 0 && id < 12000 && !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d) {
                target[id] = Math.Min(maximum, value);
            }
        }, MaximumDictionaryEntryCount);
    }

    /// <summary>读取已有的-4顺序格式；新写入使用单塔和字段边界。</summary>
    private static void ReadLegacyStates(BinaryReader reader, HashSet<(int planetId, int fractionatorId)> seen) {
        int header = reader.ReadInt32();
        if (header != -4) {
            throw new InvalidDataException($"Invalid advanced fractionator state header: {header}");
        }
        int count = ReadStateCount(reader);
        for (int i = 0; i < count; i++) {
            var key = (planetId: reader.ReadInt32(), fractionatorId: reader.ReadInt32());
            bool valid = key.planetId > 0 && key.fractionatorId > 0;
            AdvancedFractionatorState state = valid
                ? States.GetOrAdd(key, _ => new AdvancedFractionatorState())
                : new AdvancedFractionatorState();
            lock (state) {
                ResetInputFlow(state);
                state.CirculationIntakePending = false;
                int recipeId = reader.ReadInt32();
                WorldviewRecipe recipe = RecipeManager.GetRecipe(recipeId);
                state.SelectedRecipeId = recipeId != 0 && recipe?.IsAdvanced != true ? 0 : recipeId;
                int itemId = reader.ReadInt32();
                double energy = reader.ReadDouble();
                int inc = Math.Max(0, Math.Min(10, reader.ReadInt32()));
                double capacity = GetFoodEnergy(itemId, inc);
                if (capacity > 0d && !double.IsNaN(energy) && !double.IsInfinity(energy) && energy > 0d) {
                    state.DigestingItemId = itemId;
                    state.DigestingEnergy = Math.Min(1d, energy) * capacity;
                    state.DigestingInc = inc;
                    state.DigestionVersion++;
                } else {
                    ClearDigestion(state);
                }
                state.OutputCursor = Math.Max(0, reader.ReadInt32());
                ReadDictionary(reader, state.Inputs, MaximumBufferCapacity);
                ReadDictionary(reader, state.InputInc, MaximumBufferCapacity * 10);
                ReadDictionary(reader, state.Foods, MaximumBufferCapacity);
                ReadDictionary(reader, state.FoodInc, MaximumBufferCapacity * 10);
                ReadDictionary(reader, state.Products, MaximumBufferCapacity);
                ReadDictionary(reader, state.FlowOutputs, MaximumBufferCapacity);
                ReadDictionary(reader, state.FlowOutputInc, MaximumBufferCapacity * 10);
                ClampIncDictionary(state.InputInc, state.Inputs);
                RemoveNonFoodEntries(state.Foods, state.FoodInc);
                ClampIncDictionary(state.FoodInc, state.Foods);
                if (state.Foods.Count > 1 || SumCounts(state.Foods) > StackingManager.AbsoluteMaxStack
                    || state.DigestingEnergy > 0d && state.Foods.Keys.Any(id => id != state.DigestingItemId)) {
                    throw new InvalidDataException("Invalid advanced fractionator food reserve.");
                }
                ClampIncDictionary(state.FlowOutputInc, state.FlowOutputs);
                state.InputCargo.Clear();
                int cargoCount = reader.ReadInt32();
                if (cargoCount < 0 || cargoCount > MaximumDictionaryEntryCount) {
                    throw new InvalidDataException($"Invalid input cargo count: {cargoCount}");
                }
                for (int j = 0; j < cargoCount; j++) {
                    int inputId = reader.ReadInt32();
                    double cargo = reader.ReadDouble();
                    int items = GetCount(state.Inputs, inputId);
                    if (items > 0 && !double.IsNaN(cargo) && !double.IsInfinity(cargo) && cargo > 0d) {
                        state.InputCargo[inputId] = Math.Max(items / (double)StackingManager.AbsoluteMaxStack,
                            Math.Min(items, cargo));
                    }
                }
                double rate = reader.ReadDouble();
                state.ProcessingRate = double.IsNaN(rate) || double.IsInfinity(rate) ? 0d
                    : Math.Max(0d, Math.Min(30d * StackingManager.AbsoluteMaxStack * MatrixNutrition.MaximumSpeed, rate));
                reader.ReadDouble(); // 跳过旧的整机小数进度；独立主料进度从零开始，实物库存保留。
                foreach (var pair in state.Inputs) {
                    if (!state.InputCargo.ContainsKey(pair.Key)) {
                        throw new InvalidDataException($"Missing input cargo count: {pair.Key}");
                    }
                }
            }
            if (valid) { seen?.Add(key); }
        }
    }

    /// <summary>按稳定身份和独立字段写入单塔，调用方持有实例锁。</summary>
    /// <param name="writer">目标流。</param>
    /// <param name="key">星球与分馏器池身份。</param>
    /// <param name="state">被锁定的实例。</param>
    private static void WriteState(BinaryWriter writer, (int planetId, int fractionatorId) key,
        AdvancedFractionatorState state) {
        writer.WriteBlocks(
            ("Identity", w => { w.Write(key.planetId); w.Write(key.fractionatorId); }),
            ("State", w => w.WriteBlocks(
                ("Recipe", field => field.Write(state.SelectedRecipeId)),
                ("Digestion", field => {
                    field.Write(state.DigestingItemId);
                    double capacity = GetFoodEnergy(state.DigestingItemId, state.DigestingInc);
                    field.Write(capacity > 0d ? Math.Max(0d, Math.Min(1d, state.DigestingEnergy / capacity)) : 0d);
                    field.Write(state.DigestingInc);
                }),
                ("OutputCursor", field => field.Write(state.OutputCursor)),
                ("CirculationIntakePending", field => field.Write(state.CirculationIntakePending)),
                ("Inputs", field => WriteDictionary(field, state.Inputs)),
                ("InputInc", field => WriteDictionary(field, state.InputInc)),
                ("Foods", field => WriteDictionary(field, state.Foods)),
                ("FoodInc", field => WriteDictionary(field, state.FoodInc)),
                ("Products", field => WriteDictionary(field, state.Products)),
                ("FlowOutputs", field => WriteDictionary(field, state.FlowOutputs)),
                ("FlowOutputInc", field => WriteDictionary(field, state.FlowOutputInc)),
                ("InputCargo", field => field.WriteList(state.InputCargo.OrderBy(pair => pair.Key).ToArray(), (item, pair) => { item.Write(pair.Key); item.Write(pair.Value); })),
                ("ProcessingRate", field => field.Write(state.ProcessingRate)),
                ("InputProgress", field => field.WriteList(state.InputProgress.OrderBy(pair => pair.Key).ToArray(),
                    (item, pair) => { item.Write(pair.Key); item.Write(pair.Value); })),
                ("InputRates", field => field.WriteList(state.InputRates.OrderBy(pair => pair.Key).ToArray(),
                    (item, pair) => { item.Write(pair.Key); item.Write(pair.Value); }))
            ))
        );
    }
}
