using System;
using System.IO;
using System.Linq;
using FE.Compatibility.Nebula;

namespace FE.Logic.DataCenter.Assembly;

/// <summary>页面使用的订单投影，不暴露可修改的预留或在制材料。</summary>
public sealed class AssemblyOrderView {
    public string Id { get; internal set; }
    public int Owner { get; internal set; }
    public int ItemId { get; internal set; }
    public int Count { get; internal set; }
    public int Remaining { get; internal set; }
    public bool Draft { get; internal set; }
    public long Revision { get; internal set; }
    public AssemblyStatus Status { get; internal set; }
    public double WorkProgress { get; internal set; }
    public AssemblyDemand[] Rows { get; internal set; }
}

public static partial class AssemblyManager {
    public static AssemblyOrderView[] GetOrders() {
        lock (DataCenterInventory.centerItemCount) {
            return Orders.Select(order => {
                AssemblyPlan plan = order.Plan ?? AssemblyPlanner.Build(order.ItemId, order.Remaining, order.Choices, order.Reserved);
                return new AssemblyOrderView {
                    Id = order.Id, Owner = order.Owner, ItemId = order.ItemId, Count = order.Count, Remaining = order.Remaining,
                    Draft = order.Draft, Revision = order.Revision, Status = order.Status,
                    WorkProgress = order.Work == null ? 0 : order.Work.Progress / order.Work.Time,
                    Rows = plan.Rows.Select(row => new AssemblyDemand {
                        Path = row.Path, Depth = row.Depth, ItemId = row.ItemId, Required = row.Required, Stock = row.Stock,
                        Planned = row.Planned, Missing = row.Missing, RecipeId = row.RecipeId, StockOnly = row.StockOnly, Cycle = row.Cycle,
                    }).ToArray(),
                };
            }).ToArray();
        }
    }

    public static void Export(BinaryWriter writer) {
        lock (DataCenterInventory.centerItemCount) {
            writer.Write(1); writer.Write(Orders.Count);
            foreach (AssemblyOrder order in Orders) {
                writer.Write(order.Id); writer.Write(order.Owner); writer.Write(order.ItemId); writer.Write(order.Count); writer.Write(order.Remaining);
                writer.Write(order.Draft); writer.Write(order.Revision); writer.Write(order.LastTouched); writer.Write((int)order.Status);
                writer.Write(order.Choices.Count);
                foreach (var choice in order.Choices) { writer.Write(choice.Key); writer.Write(choice.Value.StockOnly); writer.Write(choice.Value.RecipeId); }
                writer.Write(order.Reserved.Count);
                foreach (var pair in order.Reserved) { writer.Write(pair.Key); writer.Write(pair.Value); }
                writer.Write(order.Work != null);
                if (order.Work == null) { continue; }
                AssemblyWork work = order.Work;
                writer.Write(work.RecipeId); writer.Write(work.Root); writer.Write(work.Progress); writer.Write(work.Time);
                writer.Write(work.Inputs.Length);
                foreach (var input in work.Inputs) { writer.Write(input.Item); writer.Write(input.Count); writer.Write(input.Inc); }
                writer.Write(work.Outputs.Length);
                foreach (var output in work.Outputs) { writer.Write(output.Item); writer.Write(output.Count); }
            }
            writer.Write(Closed.Count);
            foreach (string id in Closed) { writer.Write(id); }
        }
    }

    public static void Import(BinaryReader reader) {
        lock (DataCenterInventory.centerItemCount) {
            if (reader.ReadInt32() != 1) { throw new InvalidDataException("不支持的装配状态格式。"); }
            Orders.Clear(); Reserved.Clear(); Closed.Clear();
            int count = ReadSize(reader, 256);
            for (int i = 0; i < count; i++) {
                var order = new AssemblyOrder {
                    Id = reader.ReadString(), Owner = reader.ReadInt32(), ItemId = reader.ReadInt32(), Count = reader.ReadInt32(), Remaining = reader.ReadInt32(),
                    Draft = reader.ReadBoolean(), Revision = reader.ReadInt64(), LastTouched = reader.ReadInt64(), Status = (AssemblyStatus)reader.ReadInt32(),
                };
                if (!Guid.TryParseExact(order.Id, "N", out _) || order.Owner is < 0 or > 65535 || order.ItemId is <= 0 or >= 12000
                    || order.Count is < 1 or > 1000000 || order.Remaining < 1 || order.Remaining > order.Count || order.Revision < 1
                    || !Enum.IsDefined(typeof(AssemblyStatus), order.Status) || Closed.Contains(order.Id) || Orders.Any(old => old.Id == order.Id)) {
                    throw new InvalidDataException("装配订单身份或数量无效。");
                }
                int choices = ReadSize(reader, 512);
                for (int j = 0; j < choices; j++) {
                    string path = reader.ReadString(); bool stockOnly = reader.ReadBoolean(); int recipe = reader.ReadInt32();
                    if (path.Length is < 1 or > 100 || !path.StartsWith("0", StringComparison.Ordinal)
                        || path.Any(c => c != '.' && (c < '0' || c > '9')) || recipe < 0 || order.Choices.ContainsKey(path)) { throw new InvalidDataException("装配需求路径无效。"); }
                    order.Choices.Add(path, new() { StockOnly = stockOnly, RecipeId = recipe });
                }
                int reserved = ReadSize(reader, 12000);
                for (int j = 0; j < reserved; j++) {
                    int item = reader.ReadInt32(); long amount = reader.ReadInt64();
                    if (item is <= 0 or >= 12000 || amount is < 0 or > 1000000000 || order.Reserved.ContainsKey(item)) { throw new InvalidDataException("装配预留物料无效。"); }
                    order.Reserved.Add(item, amount);
                }
                if (reader.ReadBoolean()) {
                    var work = new AssemblyWork { RecipeId = reader.ReadInt32(), Root = reader.ReadBoolean(), Progress = reader.ReadDouble(), Time = reader.ReadInt32() };
                    if (order.Draft || work.RecipeId <= 0 || work.Time < 1 || double.IsNaN(work.Progress) || double.IsInfinity(work.Progress)
                        || work.Progress < 0 || work.Progress > work.Time) { throw new InvalidDataException("装配在制状态无效。"); }
                    work.Inputs = new (int, int, long)[ReadSize(reader, 128)];
                    for (int j = 0; j < work.Inputs.Length; j++) {
                        int item = reader.ReadInt32(), amount = reader.ReadInt32(); long inc = reader.ReadInt64();
                        if (item is <= 0 or >= 12000 || amount < 1 || inc < 0 || inc > amount * 10L) { throw new InvalidDataException("装配在制输入无效。"); }
                        work.Inputs[j] = (item, amount, inc);
                    }
                    work.Outputs = new (int, int)[ReadSize(reader, 128)];
                    for (int j = 0; j < work.Outputs.Length; j++) {
                        int item = reader.ReadInt32(), amount = reader.ReadInt32();
                        if (item is <= 0 or >= 12000 || amount < 1) { throw new InvalidDataException("装配在制产物无效。"); }
                        work.Outputs[j] = (item, amount);
                    }
                    if (work.Inputs.Length == 0 || work.Outputs.Length == 0) { throw new InvalidDataException("装配在制物料为空。"); }
                    order.Work = work;
                }
                // 磁盘恢复不保留已离线页面的草稿；网络快照仍投影主机拥有的实时预留。
                if (order.Draft && !NebulaMultiplayerModAPI.IsClient) { Closed.Add(order.Id); continue; }
                Orders.Add(order);
                foreach (var pair in order.Reserved) { Reserved[pair.Key] = checked(AssemblyPlanner.Read(Reserved, pair.Key) + pair.Value); }
            }
            int closed = ReadSize(reader, 1000000);
            for (int i = 0; i < closed; i++) {
                string id = reader.ReadString();
                if (!Guid.TryParseExact(id, "N", out _) || Orders.Any(order => order.Id == id)) { throw new InvalidDataException("装配关闭流水无效。"); }
                Closed.Add(id);
            }
            if (Reserved.Any(pair => pair.Value > DataCenterInventory.centerItemCount[pair.Key])) { throw new InvalidDataException("装配预留超过实际库存。"); }
            dirty = false;
        }
    }

    private static int ReadSize(BinaryReader reader, int maximum) {
        int count = reader.ReadInt32();
        if (count < 0 || count > maximum) { throw new InvalidDataException("装配集合数量无效。"); }
        return count;
    }
}
