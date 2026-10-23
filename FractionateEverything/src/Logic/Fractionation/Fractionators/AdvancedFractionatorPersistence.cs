using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FE.Logic.Enhancements.Stacking;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Items;

namespace FE.Logic.Fractionation.Fractionators;

public static partial class AdvancedFractionatorRuntime {
    /// <summary>主机尚未广播的删除记录；与实例变更一起按秒发送，不写入存档。</summary>
    private static readonly ConcurrentDictionary<(int planetId, int fractionatorId), byte> RemovedStates = [];

    /// <summary>从存档读取全部高级塔，清除上一存档的状态和同步记录。</summary>
    /// <param name="reader">当前高级塔存档块。</param>
    public static void Import(BinaryReader reader) {
        IntoOtherSave();
        ReadStates(reader, null);
    }

    /// <summary>写入全部实例，保持高级塔存档字段顺序；不会消费联机脏标记。</summary>
    /// <param name="writer">存档或加入时的完整快照。</param>
    public static void Export(BinaryWriter writer) {
        var pairs = States.OrderBy(pair => pair.Key.planetId).ThenBy(pair => pair.Key.fractionatorId).ToArray();
        writer.Write(-4); // 版本4保存当前消化件的剩余比例，估值变化不补满或丢失进度。
        writer.Write(pairs.Length);
        foreach (var pair in pairs) {
            lock (pair.Value) { WriteState(writer, pair.Key, pair.Value); }
        }
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
        writer.Write(-4);
        writer.Write(changed.Length);
        foreach (var pair in changed) {
            lock (pair.Value) {
                WriteState(writer, pair.Key, pair.Value);
                pair.Value.SnapshotDirty = false;
            }
        }
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

    /// <summary>原位覆盖实例字段；窗口持有的实例引用保持有效。</summary>
    /// <param name="reader">由数量和实例记录组成的数据。</param>
    /// <param name="seen">完整快照的存活集合；增量及存档读取传 null。</param>
    private static void ReadStates(BinaryReader reader, HashSet<(int planetId, int fractionatorId)> seen) {
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
                if (state.Foods.Count > 1 || SumCounts(state.Foods) > FoodCapacity
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
                double remainder = reader.ReadDouble();
                state.ProgressRemainder = double.IsNaN(remainder) || double.IsInfinity(remainder)
                    ? 0d : Math.Max(0d, Math.Min(1d, remainder));
                foreach (var pair in state.Inputs) {
                    if (!state.InputCargo.ContainsKey(pair.Key)) {
                        throw new InvalidDataException($"Missing input cargo count: {pair.Key}");
                    }
                }
            }
            if (valid) { seen?.Add(key); }
        }
    }

    /// <summary>按既有存档顺序写入单塔字段，调用方持有实例锁。</summary>
    /// <param name="writer">目标流。</param>
    /// <param name="key">星球与分馏器池身份。</param>
    /// <param name="state">被锁定的实例。</param>
    private static void WriteState(BinaryWriter writer, (int planetId, int fractionatorId) key,
        AdvancedFractionatorState state) {
        writer.Write(key.planetId);
        writer.Write(key.fractionatorId);
        writer.Write(state.SelectedRecipeId);
        writer.Write(state.DigestingItemId);
        double capacity = GetFoodEnergy(state.DigestingItemId, state.DigestingInc);
        writer.Write(capacity > 0d ? Math.Max(0d, Math.Min(1d, state.DigestingEnergy / capacity)) : 0d);
        writer.Write(state.DigestingInc);
        writer.Write(state.OutputCursor);
        WriteDictionary(writer, state.Inputs);
        WriteDictionary(writer, state.InputInc);
        WriteDictionary(writer, state.Foods);
        WriteDictionary(writer, state.FoodInc);
        WriteDictionary(writer, state.Products);
        WriteDictionary(writer, state.FlowOutputs);
        WriteDictionary(writer, state.FlowOutputInc);
        writer.Write(state.InputCargo.Count);
        foreach (var pair in state.InputCargo.OrderBy(pair => pair.Key)) {
            writer.Write(pair.Key);
            writer.Write(pair.Value);
        }
        writer.Write(state.ProcessingRate);
        writer.Write(state.ProgressRemainder);
    }
}
