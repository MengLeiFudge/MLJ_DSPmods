using System;
using System.IO;
using FE.Compatibility.Nebula;
using FE.Logic.Civilization;
using FE.Logic.Items;

namespace FE.Logic.DataCenter;

/// <summary>管理矩阵供料与永久仓储倍率；全部解析塔共享一份供料缓存和每秒结算额度。</summary>
public static class SubspaceExpansion {
    /// <summary>每档最多预存一分钟供料，避免多塔或大堆叠无限预支扩容。</summary>
    private const int SupplyLimit = 60;
    /// <summary>有限仓储的数值保护上限，终极仓储另走库存的无限容量边界。</summary>
    private const double MaximumScale = 1000000d;
    /// <summary>开放终极仓储任务的永久容量倍率。</summary>
    public const double UltimateRequiredScale = 35d;
    private static readonly object Sync = new();
    /// <summary>蓝至白六档尚未结算的供料秒数，每件矩阵对应一秒。</summary>
    private static readonly int[] Supplies = new int[6];
    private static double capacityScale = 1d;
    private static long lastSecond = long.MinValue;
    private static bool snapshotDirty;

    /// <summary>当前永久容量倍率，初始为1，停供不会下降。</summary>
    public static double CapacityScale { get { lock (Sync) { return capacityScale; } } }

    /// <summary>是否已达到终极仓储任务要求的累计容量。</summary>
    public static bool UltimateQuestReady => CapacityScale >= UltimateRequiredScale;

    /// <summary>查询当前档矩阵是否还有扩容供料空间；客户端读取同步状态，不执行收料。</summary>
    /// <param name="itemId">矩阵物品身份。</param>
    /// <returns>通信已恢复、仍可扩容且当前档缓存有空位时为true。</returns>
    public static bool CanSupplyMatrix(int itemId) {
        int stage = GetAcceptingMatrixStage(itemId);
        if (stage < 0) { return false; }
        lock (Sync) { return GetSupplyRoomUnsafe(stage) > 0; }
    }

    /// <summary>收取本批矩阵作为全局扩容供料；多余输入由解析塔保留增产点回流。</summary>
    /// <param name="itemId">六种主线矩阵之一。</param>
    /// <param name="count">输入件数。</param>
    /// <returns>本次实际消费并转入供料缓存的件数。</returns>
    public static int SupplyMatrix(int itemId, int count) {
        if (count <= 0 || NebulaMultiplayerModAPI.IsClient) { return 0; }
        int stage = GetAcceptingMatrixStage(itemId);
        if (stage < 0) { return 0; }
        lock (Sync) {
            int accepted = Math.Min(count, GetSupplyRoomUnsafe(stage));
            if (accepted <= 0) { return 0; }
            Supplies[stage] += accepted;
            snapshotDirty = true;
            return accepted;
        }
    }

    /// <summary>在扩容锁外检查物品和玩法资格，避免通信查询反向取得探索锁。</summary>
    /// <param name="itemId">输入物品身份。</param>
    /// <returns>可接收的矩阵档位；身份无效、通信未恢复或终极仓储已完成时为-1。</returns>
    private static int GetAcceptingMatrixStage(int itemId) {
        int stage = Array.IndexOf(ItemManager.MainProgressMatrixIds, itemId);
        return stage >= 0 && CivilizationRecoveryManager.HasDataCenterCommunication
               && !GrowthTechnologyManager.UltimateCapacity ? stage : -1;
    }

    /// <summary>在持有扩容锁时读取本档可收取件数；达到倍率上限后停止收料。</summary>
    /// <param name="stage">已经通过资格检查的矩阵档位，范围0～5。</param>
    /// <returns>本档剩余供料空间。</returns>
    private static int GetSupplyRoomUnsafe(int stage) =>
        capacityScale >= MaximumScale ? 0 : SupplyLimit - Supplies[stage];

    /// <summary>主机按游戏秒消费最高档供料，精确积分容量曲线，不追补离线或跳过的时间。</summary>
    /// <param name="gameTick">60刻每秒的原版游戏时钟。</param>
    public static void GameTick(long gameTick) {
        if (NebulaMultiplayerModAPI.IsClient || GrowthTechnologyManager.UltimateCapacity) { return; }
        long second = gameTick / 60L;
        lock (Sync) {
            if (second <= lastSecond) { return; }
            lastSecond = second;
            if (capacityScale >= MaximumScale) { return; }
            int stage = GetSupplyStage();
            if (stage < 0) { return; }
            Supplies[stage]--;
            // dS/dt=0.1*2^stage/(1+S/10)，t以分钟计；每秒更新其积分，不受本秒输入件数影响。
            double shifted = capacityScale + 10d;
            capacityScale = Math.Min(MaximumScale, Math.Sqrt(shifted * shifted + (1 << stage) / 30d) - 10d);
            snapshotDirty = true;
        }
    }

    /// <summary>读取供仓储页显示的同一份倍率、下一秒供料矩阵与每分钟增长倍率。</summary>
    /// <returns>矩阵为0表示缺料或已停止扩容；速率以容量倍率每分钟计。</returns>
    public static (double Scale, int MatrixId, double Rate) GetStatus() {
        lock (Sync) {
            int stage = capacityScale >= MaximumScale || GrowthTechnologyManager.UltimateCapacity ? -1 : GetSupplyStage();
            return stage < 0 ? (capacityScale, 0, 0d)
                : (capacityScale, ItemManager.MainProgressMatrixIds[stage], 0.1d * (1 << stage) / (1d + capacityScale / 10d));
        }
    }

    /// <summary>在持有Sync锁时选择最高档现存供料，低档缓存保留待用。</summary>
    /// <returns>六档索引，无供料时为-1。</returns>
    private static int GetSupplyStage() {
        for (int stage = Supplies.Length - 1; stage >= 0; stage--) {
            if (Supplies[stage] > 0) { return stage; }
        }
        return -1;
    }

    /// <summary>由库存快照调度统一取走并清除扩容状态的待同步标记。</summary>
    /// <returns>供料或倍率是否发生了变化。</returns>
    public static bool TakeSnapshotDirty() {
        lock (Sync) { bool dirty = snapshotDirty; snapshotDirty = false; return dirty; }
    }

    /// <summary>写入DataCenter的Expansion子块，存档和联机快照共用。</summary>
    /// <param name="writer">子块输出流。</param>
    public static void Export(BinaryWriter writer) {
        lock (Sync) {
            writer.Write(1);
            writer.Write(capacityScale);
            writer.Write(lastSecond);
            foreach (int supply in Supplies) { writer.Write(supply); }
        }
    }

    /// <summary>校验并导入主机或存档的扩容状态，不在导入时消费供料。</summary>
    /// <param name="reader">子块输入流。</param>
    public static void Import(BinaryReader reader) {
        if (reader.ReadInt32() != 1) { throw new InvalidDataException("亚空间扩容版本无效。"); }
        double scale = reader.ReadDouble();
        long second = reader.ReadInt64();
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale < 1d || scale > MaximumScale
            || second < 0 && second != long.MinValue || second > long.MaxValue / 60L) {
            throw new InvalidDataException("亚空间扩容状态无效。");
        }
        int[] supplies = new int[Supplies.Length];
        for (int i = 0; i < supplies.Length; i++) {
            supplies[i] = reader.ReadInt32();
            if (supplies[i] < 0 || supplies[i] > SupplyLimit) { throw new InvalidDataException("亚空间扩容供料无效。"); }
        }
        lock (Sync) {
            capacityScale = scale;
            lastSecond = second;
            Array.Copy(supplies, Supplies, supplies.Length);
            snapshotDirty = false;
        }
    }

    /// <summary>切档恢复初始容量并清空供料、结算时钟和待同步标记。</summary>
    public static void IntoOtherSave() {
        lock (Sync) {
            capacityScale = 1d;
            lastSecond = long.MinValue;
            Array.Clear(Supplies, 0, Supplies.Length);
            snapshotDirty = false;
        }
    }
}
