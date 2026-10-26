using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FE.Compatibility.Nebula;
using FE.Logic.Enhancements.VanillaRecipes;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization.Monument;

/// <summary>永久加工进度与真实构件投影；建造只改变结晶产能，升阶才改变配方时间。</summary>
internal static partial class MonumentManager {
    private static readonly object Sync = new();
    private static int[] matrixIds = [];
    private static long processingLevel;
    private static double processingCredit;
    private static bool dirty;
    private static double factor = 1d;
    private static long effectiveSets;
    private static long structureCount;
    private static readonly Dictionary<int, double> StarRates = [];

    internal static double SpeedFactor => Volatile.Read(ref factor);
    internal static long ProcessingLevel { get { lock (Sync) { return processingLevel; } } }
    internal static double NextLevelCost { get { lock (Sync) { return LevelCost(processingLevel); } } }
    internal static double MaterialRate { get; private set; }
    internal static IReadOnlyList<int> MatrixIds => matrixIds;

    internal static void InitializeCatalog() => matrixIds = MonumentCatalog.MatrixIds.ToArray();

    /// <summary>费用按下一等级计算；浮点表示边界不改变等级递增规则。</summary>
    private static double LevelCost(long completed) {
        double exponent = Math.Log(100d) + completed * Math.Log(1.25d);
        return exponent >= Math.Log(double.MaxValue) ? double.MaxValue : Math.Ceiling(100d * Math.Pow(1.25d, completed));
    }

    internal static int Analyze(int count) {
        if (count <= 0 || NebulaMultiplayerModAPI.IsClient) { return 0; }
        lock (Sync) {
            double next = processingCredit + count;
            if (double.IsInfinity(next) || next == processingCredit || processingLevel == long.MaxValue) { return 0; }
            processingCredit = next;
            while (processingCredit >= LevelCost(processingLevel) && processingLevel < long.MaxValue) {
                processingCredit -= LevelCost(processingLevel);
                processingLevel++;
            }
            dirty = true;
            return count;
        }
    }

    internal static void Tick() {
        double external = MetadataExtraction.ExternalMultiplier;
        bool completed = GrowthTechnologyManager.MonumentCompleted;
        double previous = SpeedFactor;
        lock (Sync) {
            RefreshConstruction(external);
            double baseFactor = Math.Exp(Math.Min(230d, -processingLevel * Math.Log(0.95d)));
            Volatile.Write(ref factor, completed ? 1d + 2d * (baseFactor - 1d) : baseFactor);
        }
        if (previous != SpeedFactor) { VanillaRecipeManager.RefreshComputedTimes(); }
    }

    /// <summary>建设结构数、永久加工等级、本级进度、有效套数及每分钟结晶预算。</summary>
    internal static (long Structure, long Growth, double Progress, double Cost, long Sets, double Rate) Snapshot() {
        lock (Sync) { return (structureCount, processingLevel, processingCredit, LevelCost(processingLevel), effectiveSets, MaterialRate * 60d); }
    }

    internal static IReadOnlyDictionary<int, long> SailCounts() {
        lock (Sync) {
            var result = matrixIds.ToDictionary(id => id, _ => 0L);
            foreach (var shell in Shells.Values) {
                foreach (var pair in shell.Counts) {
                    if (result.ContainsKey(pair.Key)) { result[pair.Key] += pair.Value; }
                }
            }
            return result;
        }
    }

    internal static bool HasConstruction(int starId) { lock (Sync) { return Structures.Keys.Any(key => key.Star == starId); } }
    internal static bool TakeDirty() { lock (Sync) { bool value = dirty; dirty = false; return value; } }

    internal static void Export(BinaryWriter writer) {
        lock (Sync) {
            writer.WriteBlocks(
                ("Identities", MonumentCatalog.ExportIdentities),
                ("ProcessingLevel", w => w.Write(processingLevel)),
                ("ProcessingCredit", w => w.Write(processingCredit)),
                ("Structures", ExportStructures),
                ("Shells", ExportShells)
            );
        }
    }

    internal static void Import(BinaryReader reader) {
        IntoOtherSave();
        lock (Sync) {
            reader.ReadBlocks(
                ("Identities", MonumentCatalog.ValidateIdentities),
                ("ProcessingLevel", r => {
                    long value = r.ReadInt64();
                    if (value < 0) { throw new InvalidDataException("丰碑加工等级无效。"); }
                    processingLevel = value;
                }),
                ("ProcessingCredit", r => {
                    double value = r.ReadDouble();
                    if (double.IsNaN(value) || double.IsInfinity(value) || value < 0) { throw new InvalidDataException("丰碑加工进度无效。"); }
                    processingCredit = value;
                }),
                ("Structures", ImportStructures),
                ("Shells", ImportShells)
            );
            processingCredit = Math.Min(processingCredit, LevelCost(processingLevel));
        }
    }

    /// <summary>旧独立建设账本不转换成真实构件或永久加工奖励。</summary>
    internal static void ImportLegacy(BinaryReader reader) {
        IntoOtherSave();
        // 调用方为具名块读取器，未消费的历史负载由块边界跳过。
    }

    internal static void IntoOtherSave() {
        lock (Sync) {
            Structures.Clear(); Shells.Clear(); StarRates.Clear(); SailAvailability.Clear();
            processingLevel = structureCount = effectiveSets = 0;
            processingCredit = MaterialRate = 0d;
            Volatile.Write(ref factor, 1d); dirty = false;
        }
    }
}
