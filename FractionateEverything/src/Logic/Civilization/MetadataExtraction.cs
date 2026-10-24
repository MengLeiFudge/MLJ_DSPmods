using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using FE.Compatibility.Nebula;
using FE.Logic.DataCenter;
using FE.Logic.Items;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

/// <summary>只读原版生产峰值，以FE自有扣账将额度提取为实体；兑换与提取共享每种矩阵的额度。</summary>
public static class MetadataExtraction {
    private static readonly Dictionary<int, long> Spent = [];
    private static readonly HashSet<string> Committed = [];
    private static readonly Dictionary<int, long> Peaks = [];
    private static ConfigEntry<double> crossWorldScale;
    private static double externalMultiplier = 1;
    private static bool dirty;
    /// <summary>峰值身份沿用原版元数据六种矩阵，不把新增食物矩阵误当成原版生产指标。</summary>
    public static readonly int[] MatrixIds = [I电磁矩阵, I能量矩阵, I结构矩阵, I信息矩阵, I引力矩阵, I宇宙矩阵];

    /// <summary>跨星图宇宙矩阵峰值的线性尺度；只影响已建成丰碑的额外加速。</summary>
    public static void Bind(ConfigFile config) => crossWorldScale = config.Bind("Monument", "CrossWorldMetadataScale", 1000000d,
        new ConfigDescription("Other star charts' universe-matrix production peak per additional monument multiplier.",
            new AcceptableValueRange<double>(1d, 1e15d)));

    /// <summary>声明由领域事务提供的真实来源，不虚构普通制造配方。</summary>
    public static void RegisterSources() {
        ItemAvailability.RegisterExternalSource(I能量碎片);
        ItemAvailability.RegisterExternalSource(IFE实体元数据);
    }

    /// <summary>以亚空间库存锁作为事务锁，导出、扣账及入库具有同一提交边界。</summary>
    public static long Available(int matrixId) {
        lock (DataCenterInventory.centerItemCount) {
            return Math.Max(0, Read(Peaks, matrixId) - Read(Spent, matrixId));
        }
    }

    public static double ExternalMultiplier {
        get { lock (DataCenterInventory.centerItemCount) { return externalMultiplier; } }
    }

    /// <summary>按秒读取当前指标；不调用会为缺失条目插入零值的原版查询方法。</summary>
    public static void Refresh() {
        if (NebulaMultiplayerModAPI.IsClient) { return; }
        lock (DataCenterInventory.centerItemCount) {
            List<IDCNT> production = GameMain.history?.propertyData?.totalProduction;
            foreach (int matrix in MatrixIds) {
                long peak = production?.Where(p => p.id == matrix).Select(p => (long)Math.Max(0, p.count)).DefaultIfEmpty().Max() ?? 0;
                if (Read(Peaks, matrix) != peak) { Peaks[matrix] = peak; dirty = true; }
            }
            long current = GameMain.data?.gameDesc?.seedKey64 ?? 0;
            double total = 0;
            // 同一星图即使留下多份记录也只取峰值；不读取余额，不改原版消费记录。
            foreach (var cluster in (DSPGame.propertySystem?.propertyDatas ?? []).Where(c => c != null && c.seedKey != current).GroupBy(c => c.seedKey)) {
                total += cluster.SelectMany(c => c.totalProduction ?? []).Where(p => p.id == I宇宙矩阵)
                    .Select(p => (double)Math.Max(0, p.count)).DefaultIfEmpty().Max();
            }
            double scale = crossWorldScale?.Value ?? 1000000d;
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0) { scale = 1000000d; }
            double multiplier = 1 + total / scale;
            if (externalMultiplier != multiplier) { externalMultiplier = multiplier; dirty = true; }
        }
    }

    /// <summary>一单位额度兑换一件指定实体；只能由主机执行。重复流水不再次扣款或发货。</summary>
    internal static int Exchange(string requestId, int matrixId, bool energyShard, int requested) {
        if (NebulaMultiplayerModAPI.IsClient || !Guid.TryParseExact(requestId, "N", out _)
            || !MatrixIds.Contains(matrixId) || requested is < 1 or > 1000) { return 0; }
        Refresh();
        int output = energyShard ? I能量碎片 : IFE实体元数据;
        long accepted;
        long newCount;
        lock (DataCenterInventory.centerItemCount) {
            if (!Committed.Add(requestId)) { return 0; }
            dirty = true;
            int count = (int)Math.Min(requested, Available(matrixId));
            if (count <= 0 || !LDB.items.Exist(output)) { return 0; }
            // 手动提取允许超存；仍以实际入库量扣账，整数边界不能吞掉额度。
            accepted = DataCenterInventory.AddStoredItem(output, count, 0, StorageSource.Manual, out newCount).Stored;
            Spent[matrixId] = checked(Read(Spent, matrixId) + accepted);
        }
        DataCenterInventory.NotifyItemAdded(output, accepted, 0, newCount, false);
        return (int)accepted;
    }

    private static long Read(Dictionary<int, long> values, int id) => values.TryGetValue(id, out long value) ? value : 0;

    public static bool TakeDirty() {
        lock (DataCenterInventory.centerItemCount) { bool result = dirty; dirty = false; return result; }
    }

    public static void Export(BinaryWriter writer) {
        lock (DataCenterInventory.centerItemCount) {
            writer.WriteBlocks(
                ("Spent", w => w.WriteList(Spent, (item, pair) => { item.Write(pair.Key); item.Write(pair.Value); })),
                ("Peaks", w => w.WriteList(Peaks, (item, pair) => { item.Write(pair.Key); item.Write(pair.Value); })),
                ("ExternalMultiplier", w => w.Write(externalMultiplier)),
                ("Committed", w => w.WriteList(Committed, (item, id) => item.Write(id)))
            );
        }
    }

    /// <summary>分别恢复额度、峰值、倍率与流水，删除其中一项不影响其余状态。</summary>
    public static void Import(BinaryReader reader) {
        lock (DataCenterInventory.centerItemCount) {
            IntoOtherSave();
            reader.ReadBlocks(
                ("Spent", r => ReadAmounts(r, Spent)),
                ("Peaks", r => ReadAmounts(r, Peaks)),
                ("ExternalMultiplier", r => {
                    double value = r.ReadDouble();
                    if (double.IsNaN(value) || double.IsInfinity(value) || value < 1d) {
                        throw new InvalidDataException("跨档元数据倍率无效。");
                    }
                    externalMultiplier = value;
                }),
                ("Committed", r => r.ReadList(item => {
                    string id = item.ReadString();
                    if (!Guid.TryParseExact(id, "N", out _) || !Committed.Add(id)) {
                        throw new InvalidDataException("FE元数据流水无效。");
                    }
                }))
            );
        }
    }

    /// <summary>按物品身份恢复单项额度，已移除的矩阵不会中断后续记录。</summary>
    private static void ReadAmounts(BinaryReader reader, Dictionary<int, long> target) {
        reader.ReadList(item => {
            int id = item.ReadInt32();
            long value = item.ReadInt64();
            if (value < 0 || target.ContainsKey(id)) { throw new InvalidDataException("FE元数据额度无效。"); }
            if (MatrixIds.Contains(id)) { target.Add(id, value); }
        }, 12000);
    }

    /// <summary>读取已有的顺序布局元数据块。</summary>
    public static void ImportLegacy(BinaryReader reader) {
        lock (DataCenterInventory.centerItemCount) {
            if (reader.ReadInt32() != 1) { throw new InvalidDataException("不支持的FE元数据格式。"); }
            Spent.Clear(); Peaks.Clear(); Committed.Clear();
            int count = reader.ReadInt32();
            if (count is < 0 or > 6) { throw new InvalidDataException("FE元数据种类数无效。"); }
            for (int i = 0; i < count; i++) {
                int id = reader.ReadInt32(); long spent = reader.ReadInt64(); long peak = reader.ReadInt64();
                if (!MatrixIds.Contains(id) || spent < 0 || peak < 0 || Spent.ContainsKey(id)) { throw new InvalidDataException("FE元数据额度无效。"); }
                Spent.Add(id, spent); Peaks.Add(id, peak);
            }
            externalMultiplier = reader.ReadDouble();
            if (double.IsNaN(externalMultiplier) || double.IsInfinity(externalMultiplier) || externalMultiplier < 1) { throw new InvalidDataException("跨档元数据倍率无效。"); }
            int requests = reader.ReadInt32();
            if (requests < 0 || requests > 1000000) { throw new InvalidDataException("FE元数据流水数无效。"); }
            for (int i = 0; i < requests; i++) {
                string request = reader.ReadString();
                if (!Guid.TryParseExact(request, "N", out _) || !Committed.Add(request)) { throw new InvalidDataException("FE元数据流水无效。"); }
            }
            dirty = false;
        }
    }

    public static void IntoOtherSave() {
        lock (DataCenterInventory.centerItemCount) { Spent.Clear(); Peaks.Clear(); Committed.Clear(); externalMultiplier = 1; dirty = false; }
    }
}
