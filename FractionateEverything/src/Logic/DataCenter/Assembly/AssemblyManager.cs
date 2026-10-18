using System;
using System.Collections.Generic;
using System.Linq;
using FE.Compatibility.Nebula;

namespace FE.Logic.DataCenter.Assembly;

public enum AssemblyStatus { Draft, Missing, NoPower, Working, Full, Complete, TooComplex }

/// <summary>已扣料的一轮工艺；保存实际输入增产点，取消时只退款这份在制物。</summary>
internal sealed class AssemblyWork {
    public int RecipeId;
    public bool Root;
    public double Progress;
    public int Time;
    public (int Item, int Count, long Inc)[] Inputs = [];
    public (int Item, int Count)[] Outputs = [];
}

/// <summary>订单和草稿共用同一份实际库存预留；业务状态由库存锁保护。</summary>
internal sealed class AssemblyOrder {
    public string Id;
    public int Owner;
    public int ItemId;
    public int Count;
    public int Remaining;
    public bool Draft;
    public long Revision;
    public long LastTouched;
    public AssemblyStatus Status;
    public Dictionary<string, AssemblyChoice> Choices = [];
    public Dictionary<int, long> Reserved = [];
    public AssemblyWork Work;
    public AssemblyPlan Plan;
}

/// <summary>共享装配队列；所有预留、扣料、产出和取消均在亚空间库存锁中提交。</summary>
public static partial class AssemblyManager {
    private static readonly List<AssemblyOrder> Orders = [];
    private static readonly Dictionary<int, long> Reserved = [];
    private static readonly HashSet<string> Closed = [];
    private static bool dirty;
    public static bool HasOrders { get { lock (DataCenterInventory.centerItemCount) { return Orders.Any(o => !o.Draft); } } }

    /// <summary>调用方已经持有库存锁时读取可自由提取的件数。</summary>
    internal static long FreeCount(int itemId) => Math.Max(0, DataCenterInventory.centerItemCount[itemId] - AssemblyPlanner.Read(Reserved, itemId));

    private static void Release(AssemblyOrder order) {
        foreach (var pair in order.Reserved) { Reserved[pair.Key] = AssemblyPlanner.Read(Reserved, pair.Key) - pair.Value; }
        order.Reserved.Clear();
    }

    /// <summary>只在主机对账，先归还本订单的旧预留，再按当下真实余量重新分配。</summary>
    private static void Replan(AssemblyOrder order) {
        Release(order);
        var available = new Dictionary<int, long>();
        for (int id = 1; id < DataCenterInventory.centerItemCount.Length; id++) {
            long count = FreeCount(id);
            if (count > 0) { available.Add(id, count); }
        }
        order.Plan = AssemblyPlanner.Build(order.ItemId, order.Remaining, order.Choices, available);
        order.Reserved = order.Plan.Reserved;
        foreach (var pair in order.Reserved) { Reserved[pair.Key] = AssemblyPlanner.Read(Reserved, pair.Key) + pair.Value; }
        if (order.Plan.TooComplex) { order.Status = AssemblyStatus.TooComplex; }
        else if (order.Draft) { order.Status = AssemblyStatus.Draft; }
        dirty = true;
    }

    /// <summary>更新草稿要求递增修订；迟到的展开请求不能恢复已确认或已关闭的草稿。</summary>
    public static bool Preview(int owner, string id, long revision, int itemId, int count, Dictionary<string, AssemblyChoice> choices) {
        if (NebulaMultiplayerModAPI.IsClient || !Guid.TryParseExact(id, "N", out _) || revision < 1
            || itemId <= 0 || itemId >= 12000 || !LDB.items.Exist(itemId) || count is < 1 or > 1000000
            || owner is < 0 or > 65535 || choices == null || choices.Count > 512
            || choices.Any(pair => pair.Key == null || pair.Key.Length > 100 || !pair.Key.StartsWith("0", StringComparison.Ordinal)
                || pair.Key.Any(c => c != '.' && (c < '0' || c > '9')) || pair.Value == null || pair.Value.RecipeId < 0)) { return false; }
        lock (DataCenterInventory.centerItemCount) {
            if (Closed.Contains(id)) { return false; }
            AssemblyOrder order = Orders.FirstOrDefault(o => o.Id == id);
            if (order != null && (!order.Draft || order.Owner != owner || order.Revision >= revision)) { return false; }
            if (order == null) {
                if (Orders.Count >= 256) { return false; }
                foreach (AssemblyOrder old in Orders.Where(o => o.Owner == owner && o.Draft).ToArray()) { Release(old); Orders.Remove(old); Closed.Add(old.Id); }
                order = new() { Id = id, Owner = owner, Draft = true }; Orders.Add(order);
            }
            order.ItemId = itemId; order.Count = count; order.Remaining = count; order.Revision = revision;
            order.LastTouched = GameMain.gameTick;
            order.Choices = choices.ToDictionary(pair => pair.Key, pair => new AssemblyChoice { StockOnly = pair.Value.StockOnly, RecipeId = pair.Value.RecipeId });
            Replan(order); return true;
        }
    }

    public static bool Confirm(int owner, string id, long revision) {
        if (NebulaMultiplayerModAPI.IsClient) { return false; }
        lock (DataCenterInventory.centerItemCount) {
            AssemblyOrder order = Orders.FirstOrDefault(o => o.Id == id && o.Owner == owner);
            if (order?.Draft != true || order.Revision != revision || order.Plan?.TooComplex != false) { return false; }
            order.Draft = false; order.Status = AssemblyStatus.NoPower; dirty = true; return true;
        }
    }

    /// <summary>取消只归还尚在加工的实际输入，已经完成并写入仓储的产物不回滚。</summary>
    public static void Cancel(int owner, string id) {
        if (NebulaMultiplayerModAPI.IsClient) { return; }
        lock (DataCenterInventory.centerItemCount) {
            AssemblyOrder order = Orders.FirstOrDefault(o => o.Id == id && (o.Owner == owner || owner == AssemblyNetwork.LocalOwner));
            if (order == null) { return; }
            if (order.Work?.Inputs.Any(input => !LDB.items.Exist(input.Item)) == true) {
                order.Status = AssemblyStatus.Missing; dirty = true; return;
            }
            if (order.Work?.Inputs.Any(input => long.MaxValue - DataCenterInventory.centerItemCount[input.Item] < input.Count) == true) {
                order.Status = AssemblyStatus.Full; dirty = true; return;
            }
            Release(order);
            if (order.Work != null) {
                foreach (var input in order.Work.Inputs) {
                    DataCenterInventory.AddStoredItem(input.Item, input.Count, input.Inc, StorageSource.Manual, out _);
                }
            }
            Orders.Remove(order); Closed.Add(order.Id); dirty = true;
        }
    }

    /// <summary>页面保活不改变修订；离线或关闭后草稿可释放，已确认订单继续保存。</summary>
    public static void Touch(int owner, string id) {
        lock (DataCenterInventory.centerItemCount) {
            AssemblyOrder order = Orders.FirstOrDefault(o => o.Id == id && o.Owner == owner && o.Draft);
            if (order != null) { order.LastTouched = GameMain.gameTick; }
        }
    }

    public static void Tick() {
        if (NebulaMultiplayerModAPI.IsClient) { return; }
        var notifications = new List<(int Item, long Count)>();
        HashSet<int> connected = AssemblyNetwork.ConnectedOwners();
        lock (DataCenterInventory.centerItemCount) {
            foreach (AssemblyOrder expired in Orders.Where(o => o.Draft && (!connected.Contains(o.Owner) || GameMain.gameTick - o.LastTouched > 1800)).ToArray()) {
                Release(expired); Orders.Remove(expired); Closed.Add(expired.Id); dirty = true;
            }
            foreach (AssemblyOrder draft in Orders.Where(o => o.Draft)) { Replan(draft); }
            double budget = AssemblyNodes.DrainWork();
            int iterations = 0;
            foreach (AssemblyOrder order in Orders.Where(o => !o.Draft).ToArray()) {
                if (order.Work == null) { Replan(order); }
                while (order.Remaining > 0 && iterations++ < 4096) {
                    if (order.Work == null) {
                        if (order.Plan?.TooComplex == true) { break; }
                        if (order.Choices.TryGetValue("0", out AssemblyChoice root) && root.StockOnly) {
                            if (AssemblyPlanner.Read(order.Reserved, order.ItemId) < order.Remaining) { order.Status = AssemblyStatus.Missing; break; }
                            order.Remaining = 0; break;
                        }
                        var next = AssemblyPlanner.Next(order.ItemId, order.Choices, order.Reserved, out bool blocked);
                        if (blocked || next.Recipe == null) { order.Status = AssemblyStatus.Missing; break; }
                        if (budget <= 0) { order.Status = AssemblyStatus.NoPower; break; }
                        RecipeProto recipe = next.Recipe;
                        var inputs = recipe.Items.Select((id, i) => (Item: id, Count: recipe.ItemCounts[i]))
                            .GroupBy(input => input.Item).Select(group => (Item: group.Key, Count: group.Sum(input => input.Count))).ToArray();
                        if (inputs.Any(input => AssemblyPlanner.Read(order.Reserved, input.Item) < input.Count)) { order.Status = AssemblyStatus.Missing; break; }
                        var work = new AssemblyWork { RecipeId = recipe.ID, Root = next.Root, Time = recipe.TimeSpend };
                        var paid = new List<(int, int, long)>();
                        foreach (var input in inputs) {
                            order.Reserved[input.Item] -= input.Count; Reserved[input.Item] -= input.Count;
                            long taken = DataCenterInventory.TakeItemFromModData(input.Item, (long)input.Count, out long inc);
                            if (taken != input.Count) { throw new InvalidOperationException("装配预留与实际库存不一致。"); }
                            paid.Add((input.Item, input.Count, inc));
                        }
                        work.Inputs = paid.ToArray();
                        work.Outputs = recipe.Results.Select((id, i) => (Item: id, Count: recipe.ResultCounts[i]))
                            .GroupBy(output => output.Item).Select(group => (Item: group.Key, Count: group.Sum(output => output.Count))).ToArray();
                        order.Work = work; dirty = true;
                    }
                    AssemblyWork current = order.Work;
                    double spent = Math.Min(budget, current.Time - current.Progress);
                    current.Progress += spent; budget -= spent;
                    order.Status = spent > 0 ? AssemblyStatus.Working : AssemblyStatus.NoPower;
                    dirty |= spent > 0;
                    if (current.Progress < current.Time) { break; }
                    if (current.Outputs.Any(output => !LDB.items.Exist(output.Item) || DataCenterInventory.GetRemainingCapacity(output.Item) < output.Count)) { order.Status = AssemblyStatus.Full; break; }
                    foreach (var output in current.Outputs) {
                        DataCenterInventory.AddStoredItem(output.Item, output.Count, 0, StorageSource.Assembly, out long total);
                        notifications.Add((output.Item, total));
                        if (!current.Root) {
                            AssemblyPlanner.Add(order.Reserved, output.Item, output.Count);
                            Reserved[output.Item] = AssemblyPlanner.Read(Reserved, output.Item) + output.Count;
                        }
                    }
                    if (current.Root) { order.Remaining = Math.Max(0, order.Remaining - current.Outputs.Where(output => output.Item == order.ItemId).Sum(output => output.Count)); }
                    order.Work = null; dirty = true;
                    if (budget <= 0) { break; }
                }
                if (order.Remaining == 0) { Release(order); Orders.Remove(order); Closed.Add(order.Id); dirty = true; }
            }
            AssemblyNodes.WorkRequested = Orders.Any(o => !o.Draft && o.Status != AssemblyStatus.Full && o.Status != AssemblyStatus.Missing && o.Status != AssemblyStatus.TooComplex);
        }
        foreach (var notice in notifications) { DataCenterInventory.OnItemCountIncreased(notice.Item, notice.Count); }
    }

    public static bool TakeDirty() { lock (DataCenterInventory.centerItemCount) { bool value = dirty; dirty = false; return value; } }
    public static void IntoOtherSave() {
        lock (DataCenterInventory.centerItemCount) { Orders.Clear(); Reserved.Clear(); Closed.Clear(); dirty = false; AssemblyNodes.Reset(); }
    }
}
