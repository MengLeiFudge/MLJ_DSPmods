using System.Collections.Concurrent;
using System.IO;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>
/// 矿物复制塔质能裂变点池状态与存档逻辑。
/// </summary>
public static class FissionPointPool {
    #region 质能裂变点数池

    /// <summary>
    /// 存储矿物复制塔质能裂变点数池。结构：
    /// (planetId, entityId) => fissionPointPool
    /// </summary>
    private static readonly ConcurrentDictionary<(int, int), int> fissionPointPoolDic = [];

    /// <summary>
    /// 读取矿物复制塔裂变点池实例状态。
    /// </summary>
    public static void FissionPointPoolImport(BinaryReader r) {
        fissionPointPoolDic.Clear();
        int count = r.ReadInt32();
        for (int i = 0; i < count; i++) {
            int planetId = r.ReadInt32();
            int entityId = r.ReadInt32();
            int points = r.ReadInt32();
            fissionPointPoolDic.TryAdd((planetId, entityId), points);
        }
    }

    /// <summary>
    /// 写入矿物复制塔裂变点池实例状态。
    /// </summary>
    public static void FissionPointPoolExport(BinaryWriter w) {
        w.Write(fissionPointPoolDic.Count);
        foreach (var p in fissionPointPoolDic) {
            w.Write(p.Key.Item1);
            w.Write(p.Key.Item2);
            w.Write(p.Value);
        }
    }

    /// <summary>
    /// 重置矿物复制塔裂变点池实例状态。
    /// </summary>
    public static void FissionPointPoolIntoOtherSave() {
        fissionPointPoolDic.Clear();
    }

    /// <summary>
    /// 读取指定矿物复制塔当前储存的裂变点数。
    /// </summary>
    public static int GetFissionPointPool(this FractionatorComponent fractionator, PlanetFactory factory) {
        int planetId = factory.planetId;
        int entityId = fractionator.entityId;
        return fissionPointPoolDic.TryGetValue((planetId, entityId), out int points) ? points : 0;
    }

    /// <summary>
    /// 设置指定矿物复制塔当前储存的裂变点数。
    /// </summary>
    public static void SetFissionPointPool(this FractionatorComponent fractionator, PlanetFactory factory, int points) {
        int planetId = factory.planetId;
        int entityId = fractionator.entityId;
        if (points <= 0) {
            fissionPointPoolDic.TryRemove((planetId, entityId), out _);
        } else {
            fissionPointPoolDic[(planetId, entityId)] = points;
        }
    }

    #endregion
}
