using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FE.Compatibility.Nebula;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

/// <summary>一次背包交付的不可重复结算结果；增产点随实际扣除件数一同离开背包。</summary>
public sealed class MemoryDeliveryReceipt {
    /// <summary>主机连接玩家身份，本地主机为0。</summary>
    public int Owner;
    /// <summary>目标地点和请求内容摘要，防止同一流水号更换请求。</summary>
    public int MarkId;
    public string Fingerprint;
    /// <summary>客户端已暂存的数量与增产点。</summary>
    public int Offered;
    public int OfferedInc;
    /// <summary>主机实际接收的数量与增产点，差额退还。</summary>
    public int Accepted;
    public int AcceptedInc;
    /// <summary>本次处理后该节点的剩余需求。</summary>
    public int Remaining;
}

/// <summary>终极任务的到地背包交付及持久流水账本；不调用亚空间取物接口。</summary>
public static partial class ExplorationManager {
    private static readonly Dictionary<string, MemoryDeliveryReceipt> DeliveryResults = new(StringComparer.Ordinal);
    private static GalaxyData deliveryGalaxy;
    private static int notifiedDeliveryMark;
    private static int notifiedDeliveryRemaining = -1;
    private static int completedLocalDeliveryMark;

    /// <summary>同流水只读取既有结果，未命中时由调用方继续校验背包。</summary>
    public static bool TryGetMemoryReceipt(string requestId, out MemoryDeliveryReceipt receipt) {
        lock (Sync) { return DeliveryResults.TryGetValue(requestId, out receipt); }
    }

    /// <summary>位置和剩余需求由主机决定，扣物与账本在同一结算锁中完成。</summary>
    /// <param name="requestId">本次唯一流水，重发必须保持。</param>
    /// <param name="owner">主机确认的玩家身份。</param>
    /// <param name="fingerprint">请求内容摘要。</param>
    /// <param name="markId">交付点身份。</param>
    /// <param name="position">主机确认的宇宙位置。</param>
    /// <param name="planetId">主机确认的星球。</param>
    /// <param name="package">本地主机真实背包，或客户端交付前的真实背包快照。</param>
    /// <param name="offered">愿意提交的件数，不得超过节点总需求。</param>
    /// <param name="offeredInc">已暂存件数的增产点，仅用于回执。</param>
    /// <param name="persistPackage">远端库存持有者的写回入口，在推进节点前完成。</param>
    public static MemoryDeliveryReceipt DeliverMemory(string requestId, int owner, string fingerprint,
        int markId, VectorLF3 position, int planetId, StorageComponent package, int offered, int offeredInc,
        Action<StorageComponent> persistPackage = null) {
        if (NebulaMultiplayerModAPI.IsClient || package == null || offered <= 0 || offered > UltimateMemoryCost
            || offeredInc < 0 || offeredInc > offered * 10 || !Guid.TryParseExact(requestId, "N", out _)
            || !Finite(position.x) || !Finite(position.y) || !Finite(position.z)) { return null; }
        lock (Sync) {
            if (DeliveryResults.TryGetValue(requestId, out MemoryDeliveryReceipt existing)) {
                return existing.Owner == owner && existing.MarkId == markId && existing.Fingerprint == fingerprint
                    ? existing : null;
            }
            if (DeliveryResults.Count >= MaximumRecords) { throw new InvalidDataException("交付流水账本已满。"); }
            ExplorationMark mark = ActiveMarks.FirstOrDefault(m => m.Id == markId && IsPaymentMark(m));
            int branch = mark?.ContentId == "exploration" ? 0 : 1;
            int remaining = mark == null ? 0 : UltimateMemoryCost - UltimateInputs[branch];
            int accepted = mark != null && IsWithinArrival(mark, position, planetId) ? Math.Min(offered, remaining) : 0;
            int itemId = IFE记忆源点;
            int inc = 0;
            if (accepted > 0) { package.TakeTailItems(ref itemId, ref accepted, out inc); }
            var receipt = new MemoryDeliveryReceipt {
                Owner = owner, MarkId = markId, Fingerprint = fingerprint, Offered = offered, OfferedInc = persistPackage == null ? inc : offeredInc,
                Accepted = accepted, AcceptedInc = inc, Remaining = remaining - accepted,
            };
            persistPackage?.Invoke(package);
            DeliveryResults.Add(requestId, receipt);
            if (mark != null && accepted > 0) {
                UltimateInputs[branch] += accepted;
                if (receipt.Remaining == 0) { Claim(mark); }
            }
            MarkDirty();
            return receipt;
        }
    }

    /// <summary>按秒寻找当前交付节点；只有进入或差额变化时提示，不在原地重复弹窗。</summary>
    private static void ProcessAutomaticDelivery() {
        Player player = GameMain.mainPlayer;
        if (player?.package == null) { return; }
        if (!ReferenceEquals(deliveryGalaxy, GameMain.galaxy)) {
            deliveryGalaxy = GameMain.galaxy;
            notifiedDeliveryMark = completedLocalDeliveryMark = 0;
            notifiedDeliveryRemaining = -1;
        }
        if (MemoryDeliverySync.PumpPending()) { return; }
        if (!player.isAlive || player.respawning) { notifiedDeliveryMark = 0; return; }
        ExplorationMark mark;
        int remaining;
        lock (Sync) {
            mark = NearbyMarks(player.planetId).FirstOrDefault(m => IsPaymentMark(m)
                && IsWithinArrival(m, player.uPosition, player.planetId));
            remaining = mark == null ? 0 : UltimateMemoryCost - UltimateInputs[mark.ContentId == "exploration" ? 0 : 1];
        }
        if (mark == null) { notifiedDeliveryMark = 0; notifiedDeliveryRemaining = -1; return; }
        if (mark.Id == completedLocalDeliveryMark) { return; }
        int count = Math.Min(remaining, player.package.GetItemCount(IFE记忆源点));
        if (count > 0) {
            if (NebulaMultiplayerModAPI.IsClient) {
                if (MemoryDeliverySync.Request(mark.Id, count)) { return; }
            } else {
                MemoryDeliveryReceipt receipt = DeliverMemory(Guid.NewGuid().ToString("N"), 0, "local",
                    mark.Id, player.uPosition, player.planetId, player.package, count, 0);
                if (receipt != null) { remaining = receipt.Remaining; }
            }
        }
        NotifyDeliveryRemaining(mark.Id, remaining);
    }

    /// <summary>本地回执与单机交付共用差额提示，普通主机快照不清理提示去重状态。</summary>
    public static void NotifyDeliveryRemaining(int markId, int remaining) {
        if (remaining == 0) { completedLocalDeliveryMark = markId; }
        if (remaining > 0 && (notifiedDeliveryMark != markId || notifiedDeliveryRemaining != remaining)) {
            CivilizationModule.QueueNotification(string.Format("FE.Exploration.DeliveryRemaining".Translate(), remaining));
        }
        notifiedDeliveryMark = markId;
        notifiedDeliveryRemaining = remaining;
    }

    private static void ExportDeliveries(BinaryWriter writer) {
        writer.Write(DeliveryResults.Count);
        foreach (var pair in DeliveryResults) {
            MemoryDeliveryReceipt r = pair.Value;
            writer.Write(pair.Key); writer.Write(r.Owner); writer.Write(r.MarkId); writer.Write(r.Fingerprint);
            writer.Write(r.Offered); writer.Write(r.OfferedInc); writer.Write(r.Accepted); writer.Write(r.AcceptedInc); writer.Write(r.Remaining);
        }
    }

    private static void ImportDeliveries(BinaryReader reader, int version) {
        if (version < 5) { return; }
        int count = ReadCount(reader);
        for (int i = 0; i < count; i++) {
            string id = reader.ReadString();
            var receipt = new MemoryDeliveryReceipt {
                Owner = reader.ReadInt32(), MarkId = reader.ReadInt32(), Fingerprint = reader.ReadString(),
                Offered = reader.ReadInt32(), OfferedInc = reader.ReadInt32(), Accepted = reader.ReadInt32(),
                AcceptedInc = reader.ReadInt32(), Remaining = reader.ReadInt32(),
            };
            if (!Guid.TryParseExact(id, "N", out _) || DeliveryResults.ContainsKey(id) || receipt.MarkId <= 0
                || receipt.Owner is < 0 or > ushort.MaxValue || receipt.Fingerprint.Length > 64
                || receipt.Offered is <= 0 or > UltimateMemoryCost || receipt.OfferedInc < 0 || receipt.OfferedInc > receipt.Offered * 10
                || receipt.Accepted < 0 || receipt.Accepted > receipt.Offered || receipt.AcceptedInc < 0
                || receipt.AcceptedInc > receipt.Accepted * 10 || receipt.Remaining is < 0 or > UltimateMemoryCost) {
                throw new InvalidDataException("交付流水数据无效。");
            }
            DeliveryResults.Add(id, receipt);
        }
    }
}
