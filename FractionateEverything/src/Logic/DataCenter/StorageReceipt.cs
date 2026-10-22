using System;

namespace FE.Logic.DataCenter;

/// <summary>实体入库来源决定容量策略；玩家操作保留实体，自动超额才能折算扩容。</summary>
internal enum StorageSource {
    Manual,
    BasicTower,
    LimitedStation,
    UnlimitedStation,
}

/// <summary>同一批实体及增产点的守恒分配；上游仅扣除入库和折算两部分。</summary>
internal readonly struct StorageReceipt {
    public long Stored { get; }
    public long Converted { get; }
    public long Rejected { get; }
    public long StoredInc { get; }
    public long ConvertedInc { get; }
    public long RejectedInc { get; }
    public long Accepted => Stored + Converted;
    public long AcceptedInc => StoredInc + ConvertedInc;

    public StorageReceipt(long offered, long inc, long stored, long converted) {
        offered = Math.Max(0L, offered);
        inc = Math.Max(0L, inc);
        Stored = stored;
        Converted = converted;
        Rejected = offered - stored - converted;
        StoredInc = offered > 0 ? (long)((decimal)stored / offered * inc) : 0L;
        long acceptedInc = offered > 0 ? (long)((decimal)(stored + converted) / offered * inc) : 0L;
        ConvertedInc = acceptedInc - StoredInc;
        RejectedInc = inc - acceptedInc;
    }
}
