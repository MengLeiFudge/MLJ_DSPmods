using System;
using System.Collections.Generic;
using System.IO;

namespace FE.Logic.DataCenter;

/// <summary>只读退役订单块；预留尚未扣款，仅退还未完成工艺的实付输入。</summary>
internal sealed class RetiredAssemblyData {
    private readonly Dictionary<int, (long Count, long Inc)> refunds = [];
    private bool read;

    internal void Read(BinaryReader reader) {
        if (read || reader.ReadInt32() != 1) { throw new InvalidDataException("退役装配数据格式无效。"); }
        read = true;
        var pending = new Dictionary<int, (long Count, long Inc)>();
        var ids = new HashSet<string>();
        int orders = ReadSize(reader, 256);
        for (int i = 0; i < orders; i++) {
            string id = reader.ReadString();
            int owner = reader.ReadInt32(), item = reader.ReadInt32(), count = reader.ReadInt32(), remaining = reader.ReadInt32();
            bool draft = reader.ReadBoolean();
            long revision = reader.ReadInt64();
            reader.ReadInt64(); // 页面保活时间已无用途。
            int status = reader.ReadInt32();
            if (!Guid.TryParseExact(id, "N", out _) || !ids.Add(id) || owner is < 0 or > 65535
                || item is <= 0 or >= 12000 || count is < 1 or > 1000000 || remaining < 1 || remaining > count
                || revision < 1 || status is < 0 or > 6) { throw new InvalidDataException("退役装配订单无效。"); }
            int choices = ReadSize(reader, 512);
            for (int j = 0; j < choices; j++) { reader.ReadString(); reader.ReadBoolean(); reader.ReadInt32(); }
            int reserved = ReadSize(reader, 12000);
            for (int j = 0; j < reserved; j++) { reader.ReadInt32(); reader.ReadInt64(); }
            if (!reader.ReadBoolean()) { continue; }
            int recipe = reader.ReadInt32();
            reader.ReadBoolean();
            double progress = reader.ReadDouble();
            int time = reader.ReadInt32();
            if (draft || recipe <= 0 || time < 1 || double.IsNaN(progress) || double.IsInfinity(progress)
                || progress < 0 || progress > time) { throw new InvalidDataException("退役装配在制状态无效。"); }
            int inputs = ReadSize(reader, 128);
            for (int j = 0; j < inputs; j++) {
                int input = reader.ReadInt32(), amount = reader.ReadInt32();
                long inc = reader.ReadInt64();
                if (input is <= 0 or >= 12000 || amount < 1 || inc < 0 || inc > amount * 10L) {
                    throw new InvalidDataException("退役装配在制输入无效。");
                }
                pending.TryGetValue(input, out var previous);
                pending[input] = (checked(previous.Count + amount), checked(previous.Inc + inc));
            }
            int outputs = ReadSize(reader, 128);
            for (int j = 0; j < outputs; j++) {
                int output = reader.ReadInt32(), amount = reader.ReadInt32();
                if (output is <= 0 or >= 12000 || amount < 1) { throw new InvalidDataException("退役装配在制产物无效。"); }
            }
            if (inputs == 0 || outputs == 0) { throw new InvalidDataException("退役装配在制物料为空。"); }
        }
        int closed = ReadSize(reader, 1000000);
        for (int i = 0; i < closed; i++) {
            string id = reader.ReadString();
            if (!Guid.TryParseExact(id, "N", out _) || ids.Contains(id)) { throw new InvalidDataException("退役装配关闭记录无效。"); }
        }
        foreach (var pair in pending) { refunds.Add(pair.Key, pair.Value); }
    }

    /// <summary>全部库存块读完后在库存锁内归还，不受玩法容量影响，也不触发获得奖励。</summary>
    internal void Restore(long[] counts, long[] incs) {
        foreach (var pair in refunds) {
            _ = checked(counts[pair.Key] + pair.Value.Count);
            _ = checked(incs[pair.Key] + pair.Value.Inc);
        }
        foreach (var pair in refunds) {
            counts[pair.Key] += pair.Value.Count;
            incs[pair.Key] += pair.Value.Inc;
        }
        refunds.Clear();
    }

    private static int ReadSize(BinaryReader reader, int maximum) {
        int count = reader.ReadInt32();
        if (count < 0 || count > maximum) { throw new InvalidDataException("退役装配集合数量无效。"); }
        return count;
    }
}
