using System;
using System.Collections.Generic;
using System.IO;
using FE.Logic.Civilization.Configuration;
using FE.Utils;

namespace FE.Logic.Civilization.Analysis;

/// <summary>
/// 保存各矩阵阶段的解析数据进度、可用检索机会和深层解析进度。
/// </summary>
public static class AnalysisProgressStore {
    public sealed class StageProgress {
        public long PendingData;
        public long GeneratedOpportunities;
        public int AvailableOpportunities;
    }

    private static readonly Dictionary<string, StageProgress> stages = [];

    public static int DeepAnalysisProgress { get; set; }

    public static StageProgress GetOrCreate(string stageKey) {
        if (!stages.TryGetValue(stageKey, out StageProgress progress)) {
            progress = new StageProgress();
            stages[stageKey] = progress;
        }
        return progress;
    }

    public static void Import(BinaryReader r) {
        stages.Clear();
        DeepAnalysisProgress = 0;
        r.ReadBlocks(
            ("Stages", br => {
                int count = Math.Max(0, br.ReadInt32());
                for (int i = 0; i < count; i++) {
                    string stageKey = br.ReadString();
                    stages[stageKey] = new StageProgress {
                        PendingData = Math.Max(0L, br.ReadInt64()),
                        GeneratedOpportunities = Math.Max(0L, br.ReadInt64()),
                        AvailableOpportunities = Math.Max(0, br.ReadInt32()),
                    };
                }
            }),
            ("DeepAnalysisProgress", br => DeepAnalysisProgress = Math.Max(0, br.ReadInt32()))
        );
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("Stages", bw => {
                bw.Write(stages.Count);
                foreach (KeyValuePair<string, StageProgress> pair in stages) {
                    bw.Write(pair.Key);
                    bw.Write(pair.Value.PendingData);
                    bw.Write(pair.Value.GeneratedOpportunities);
                    bw.Write(pair.Value.AvailableOpportunities);
                }
            }),
            ("DeepAnalysisProgress", bw => bw.Write(DeepAnalysisProgress))
        );
    }

    public static void IntoOtherSave() {
        stages.Clear();
        DeepAnalysisProgress = 0;
        ProgressionProfile profile = ProgressionProfileRegistry.Current;
        if (profile == null) {
            return;
        }
        foreach (MatrixStageDefinition stage in profile.Stages) {
            GetOrCreate(stage.StageKey);
        }
    }
}
