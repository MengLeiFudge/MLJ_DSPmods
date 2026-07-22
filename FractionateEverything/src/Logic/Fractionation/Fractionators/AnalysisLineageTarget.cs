using System.IO;
using FE.Logic.Buildings.Migration;

namespace FE.Logic.Fractionation.Fractionators;

/// <summary>
/// 读取旧版解析塔谱系目标，并迁入四塔共用的主路锁定状态。
/// </summary>
public static class AnalysisLineageTarget {
    /// <summary>
    /// 导入旧存档中的解析谱系目标；新版不再单独导出该状态块。
    /// </summary>
    public static void LineageTargetImport(BinaryReader reader) {
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++) {
            int planetId = reader.ReadInt32();
            int entityId = reader.ReadInt32();
            int itemId = LegacyProtoMigration.MapItemId(reader.ReadInt32());
            FractionatorSingleLock.TryAddLegacyLockedOutput(planetId, entityId, itemId);
        }
    }
}
