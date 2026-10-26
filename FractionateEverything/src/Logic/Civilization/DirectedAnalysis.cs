using System.Collections.Generic;
using System.IO;
using System.Linq;
using FE.Compatibility.Nebula;
using static FE.Utils.Utils;

namespace FE.Logic.Civilization;

public static partial class ResearchManager {
    private static readonly Dictionary<int, string> AnalysisTargets = [];

    public static bool IsAnalysisTarget(int itemId, string contentId) {
        lock (Sync) { return AnalysisTargets.TryGetValue(itemId, out string target) && target == contentId; }
    }

    /// <summary>只允许指定已经取得线索且尚未完成的内容；同一样本全档共用目标。</summary>
    internal static bool SetAnalysisTarget(int itemId, string contentId) {
        if (NebulaMultiplayerModAPI.IsClient || !GrowthTechnologyManager.DirectedAnalysis) { return false; }
        lock (Sync) {
            if (string.IsNullOrEmpty(contentId)) { AnalysisTargets.Remove(itemId); }
            else {
                if (!GetCandidateIdsUnsafe(itemId).Contains(contentId)) { return false; }
                AnalysisTargets[itemId] = contentId;
            }
            snapshotDirty = true;
            return true;
        }
    }

    private static string GetAnalysisTargetUnsafe(int itemId, string[] candidates) =>
        GrowthTechnologyManager.DirectedAnalysis && AnalysisTargets.TryGetValue(itemId, out string target)
            && candidates.Contains(target) ? target : null;

    private static void ExportAnalysisTargets(BinaryWriter writer) => writer.WriteList(AnalysisTargets, (entry, pair) => {
        entry.Write(pair.Key); entry.Write(pair.Value);
    });

    private static void ImportAnalysisTargets(BinaryReader reader) => reader.ReadList(entry => {
        int item = entry.ReadInt32(); string content = entry.ReadString();
        if (item <= 0 || item >= 12000 || content.Length > 256) { throw new InvalidDataException("定向解析目标无效。"); }
        AnalysisTargets.Add(item, content);
    }, 12000);
}
