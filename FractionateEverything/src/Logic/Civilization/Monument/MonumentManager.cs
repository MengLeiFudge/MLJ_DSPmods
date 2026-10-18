using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FE.Compatibility.Nebula;
using FE.Logic.Enhancements.VanillaRecipes;
using FE.Logic.Items;

namespace FE.Logic.Civilization.Monument;

/// <summary>全档丰碑账本；发射线程只登记抵达，主线程投影加速，锁内不回调其他业务域。</summary>
internal static class MonumentManager {
    private static readonly object Sync = new();
    private static readonly Dictionary<int, long> Construction = [];
    private static readonly SortedDictionary<long, Dictionary<(int Star, int Matrix), long>> Cohorts = [];
    private static int[] matrixIds = [];
    private static long analyzed;
    private static bool dirty;
    private static double factor = 1d;
    private static double supply;
    private static double balance;
    private static long level;

    /// <summary>永不低于1的有效加速倍数；建成前不应用跨档倍率。</summary>
    internal static double SpeedFactor => Volatile.Read(ref factor);
    /// <summary>主线程接收站结算使用的当前全档材料预算，单位件/秒。</summary>
    internal static double MaterialRate { get; private set; }
    /// <summary>固定矩阵集合，不受当前研究资格或投入历史缩减。</summary>
    internal static IReadOnlyList<int> MatrixIds => matrixIds;

    /// <summary>最终来源闭包完成后建立矩阵集合；未知模组矩阵仅按真实来源判定。</summary>
    internal static void InitializeCatalog() {
        matrixIds = LDB.items.dataArray.Where(item => item != null && (item.Type == EItemType.Matrix || item.ID == 5201)
            && ItemAvailability.IsReachable(item.ID)).Select(item => item.ID).OrderBy(id => id).ToArray();
    }

    /// <summary>抵达一枚火箭。星体只改变建设贡献，不改变矩阵种类或给普通戴森奖励。</summary>
    internal static void ReceiveRocket(int starId) {
        if (NebulaMultiplayerModAPI.IsClient) { return; }
        StarData star = GameMain.galaxy?.StarById(starId);
        if (star == null) { return; }
        int weight = star.type != EStarType.GiantStar ? 1
            : star.spectr <= ESpectrType.K ? 4 : star.spectr is ESpectrType.B or ESpectrType.O ? 2 : 1;
        lock (Sync) {
            Construction.TryGetValue(starId, out long previous);
            Construction[starId] = checked(previous + weight);
            dirty = true;
        }
    }

    /// <summary>矩阵帆抵达后按游戏秒合并到期批次，寿命不受原版太阳帆科技或沙盒延寿影响。</summary>
    internal static void ReceiveSail(int starId, int matrixId) {
        if (NebulaMultiplayerModAPI.IsClient || Array.BinarySearch(matrixIds, matrixId) < 0) { return; }
        long expires = GameMain.gameTick / 60 + 600;
        lock (Sync) {
            if (!Cohorts.TryGetValue(expires, out var batch)) { Cohorts.Add(expires, batch = []); }
            var key = (starId, matrixId);
            batch.TryGetValue(key, out long count);
            batch[key] = checked(count + 1);
            dirty = true;
        }
    }

    /// <summary>解析件数直接累积，整百升阶；批量大小和帧率不影响进度。</summary>
    internal static int Analyze(int count) {
        if (count <= 0 || NebulaMultiplayerModAPI.IsClient) { return 0; }
        lock (Sync) {
            int accepted = (int)Math.Min(count, long.MaxValue - analyzed);
            analyzed += accepted;
            dirty |= accepted > 0;
            return accepted;
        }
    }

    /// <summary>每秒主线程刷新派生效果；仅在实际整帧时间改变时重建游戏执行缓存。</summary>
    internal static void Tick() {
        double external = MetadataExtraction.ExternalMultiplier;
        lock (Sync) {
            long second = GameMain.gameTick / 60;
            if (!NebulaMultiplayerModAPI.IsClient) {
                while (Cohorts.Count > 0 && Cohorts.First().Key <= second) {
                    Cohorts.Remove(Cohorts.First().Key);
                    dirty = true;
                }
            }
            double contribution = Construction.Values.Sum(value => (double)value);
            level = (long)Math.Min(long.MaxValue / 100, Math.Floor(contribution / 100d));
            var counts = matrixIds.ToDictionary(id => id, _ => 0d);
            foreach (var batch in Cohorts) {
                if (batch.Key <= second) { continue; }
                foreach (var pair in batch.Value) {
                    if (counts.ContainsKey(pair.Key.Matrix)) { counts[pair.Key.Matrix] += pair.Value; }
                }
            }
            double total = counts.Values.Sum();
            double mean = counts.Count > 0 ? total / counts.Count : 0d;
            balance = mean <= 0 ? 0d : counts.Count == 1 ? 1d
                : Math.Max(0d, Math.Min(1d, 1d - counts.Values.Sum(value => Math.Pow(value / mean - 1d, 2d))
                    / (counts.Count * (counts.Count - 1d))));
            double demand = Construction.Values.Sum(value => 100d * counts.Count * Math.Max(1d, value / 100d));
            supply = demand > 0 ? Math.Min(1d, total / demand) : 0d;
            double full = (1d + 0.15d * level + 0.02d * (analyzed / 100)) * external;
            Volatile.Write(ref factor, level > 0 ? Math.Max(1d, 1d + balance * (full - 1d)) : 1d);
            MaterialRate = level > 0 ? 2d * level * external * supply / 60d : 0d;
        }
        VanillaRecipeManager.RefreshComputedTimes();
        if (!NebulaMultiplayerModAPI.IsClient && level > 0 && VanillaRecipeManager.AllAtOneFrame) {
            PermanentProfileStore.RecordCompletion();
        }
    }

    /// <summary>只读界面快照，返回副本而非可变账本。</summary>
    internal static (long Level, long Growth, int Progress, double Balance, double Supply, double Rate) Snapshot() {
        lock (Sync) { return (level, analyzed / 100, (int)(analyzed % 100), balance, supply, MaterialRate * 60d); }
    }

    /// <summary>按矩阵返回当前在轨件数，零投入也保留一行。</summary>
    internal static IReadOnlyDictionary<int, long> SailCounts() {
        lock (Sync) {
            var result = matrixIds.ToDictionary(id => id, _ => 0L);
            foreach (var batch in Cohorts) {
                if (batch.Key <= GameMain.gameTick / 60) { continue; }
                foreach (var pair in batch.Value) {
                    if (result.ContainsKey(pair.Key.Matrix)) { result[pair.Key.Matrix] = checked(result[pair.Key.Matrix] + pair.Value); }
                }
            }
            return result;
        }
    }

    internal static bool HasConstruction(int starId) { lock (Sync) { return Construction.ContainsKey(starId); } }
    internal static bool TakeDirty() { lock (Sync) { bool result = dirty; dirty = false; return result; } }

    /// <summary>版本1只保存事实；倍率、需求和均衡由当前合法目录重算。</summary>
    internal static void Export(BinaryWriter writer) {
        lock (Sync) {
            writer.Write(1);
            MonumentCatalog.ExportIdentities(writer);
            writer.Write(analyzed);
            writer.Write(Construction.Count);
            foreach (var pair in Construction.OrderBy(pair => pair.Key)) { writer.Write(pair.Key); writer.Write(pair.Value); }
            writer.Write(Cohorts.Count);
            foreach (var batch in Cohorts) {
                writer.Write(batch.Key); writer.Write(batch.Value.Count);
                foreach (var pair in batch.Value.OrderBy(pair => pair.Key.Star).ThenBy(pair => pair.Key.Matrix)) {
                    writer.Write(pair.Key.Star); writer.Write(pair.Key.Matrix); writer.Write(pair.Value);
                }
            }
        }
    }

    /// <summary>完整校验后替换账本，失败不留下部分导入的状态。</summary>
    internal static void Import(BinaryReader reader) {
        if (reader.ReadInt32() != 1) { throw new InvalidDataException("不支持的丰碑格式。"); }
        MonumentCatalog.ValidateIdentities(reader);
        long restoredAnalysis = reader.ReadInt64();
        if (restoredAnalysis < 0) { throw new InvalidDataException("丰碑解析数量无效。"); }
        Dictionary<int, long> construction = [];
        int size = ReadSize(reader, 12000);
        for (int i = 0; i < size; i++) {
            int star = reader.ReadInt32(); long amount = reader.ReadInt64();
            if (star <= 0 || amount <= 0 || construction.ContainsKey(star)) { throw new InvalidDataException("丰碑建设记录无效。"); }
            construction.Add(star, amount);
        }
        SortedDictionary<long, Dictionary<(int Star, int Matrix), long>> cohorts = [];
        size = ReadSize(reader, 601);
        int entries = 0;
        for (int i = 0; i < size; i++) {
            long expiry = reader.ReadInt64();
            if (expiry < 0 || cohorts.ContainsKey(expiry)) { throw new InvalidDataException("丰碑帆到期记录无效。"); }
            Dictionary<(int Star, int Matrix), long> batch = [];
            int length = ReadSize(reader, 1000000 - entries); entries += length;
            for (int j = 0; j < length; j++) {
                int star = reader.ReadInt32(), matrix = reader.ReadInt32(); long count = reader.ReadInt64();
                if (star <= 0 || matrix <= 0 || matrix >= 12000 || count <= 0 || batch.ContainsKey((star, matrix))) {
                    throw new InvalidDataException("丰碑帆来源或数量无效。");
                }
                batch.Add((star, matrix), count);
            }
            cohorts.Add(expiry, batch);
        }
        lock (Sync) {
            Construction.Clear(); foreach (var pair in construction) { Construction.Add(pair.Key, pair.Value); }
            Cohorts.Clear(); foreach (var pair in cohorts) { Cohorts.Add(pair.Key, pair.Value); }
            analyzed = restoredAnalysis; dirty = false;
        }
    }

    private static int ReadSize(BinaryReader reader, int limit) {
        int value = reader.ReadInt32();
        if (value < 0 || value > limit) { throw new InvalidDataException("丰碑记录数量越界。"); }
        return value;
    }

    internal static void IntoOtherSave() {
        lock (Sync) {
            Construction.Clear(); Cohorts.Clear(); analyzed = level = 0;
            supply = balance = MaterialRate = 0d; Volatile.Write(ref factor, 1d); dirty = false;
        }
    }
}
