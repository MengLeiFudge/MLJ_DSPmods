using System;
using System.IO;
using FE.Utils;
using FE.Logic.Civilization.Technology;

namespace FE.Logic.Civilization.Analysis;

#region 解析进度存储

/// <summary>
/// 保存全局检索计数、无效保底与深层解析进度。
/// 旧 Stages / DeepAnalysisProgress 机会语义不重解释。
/// </summary>
public static class AnalysisProgressStore {
    public static int TotalRetrievalCount { get; set; }
    public static int FailureStreak { get; set; }
    public static int DeepAnalysisProgress { get; set; }

    public static void Import(BinaryReader r) {
        TotalRetrievalCount = 0;
        FailureStreak = 0;
        DeepAnalysisProgress = 0;
        r.ReadBlocks(
            // 旧阶段机会库存直接忽略。
            ("Stages", _ => { }),
            ("DeepAnalysisProgress", _ => { }),
            ("Global", br => {
                TotalRetrievalCount = Math.Max(0, br.ReadInt32());
                FailureStreak = Math.Max(0, br.ReadInt32());
                DeepAnalysisProgress = Math.Max(0, br.ReadInt32());
            })
        );
    }

    public static void Export(BinaryWriter w) {
        w.WriteBlocks(
            ("Global", bw => {
                bw.Write(TotalRetrievalCount);
                bw.Write(FailureStreak);
                bw.Write(DeepAnalysisProgress);
            })
        );
    }

    public static void IntoOtherSave() {
        TotalRetrievalCount = 0;
        FailureStreak = 0;
        DeepAnalysisProgress = 0;
    }
}

#endregion // 解析进度存储

#region 深层解析服务

/// <summary>
/// 将无候选广域检索消耗的基础残片成本累计为远古文明科技点进度。
/// </summary>
public static class DeepAnalysisService {
    /// <summary>
    /// 仅累计 base 残片成本；达到点成本后产出科技点并保留余数。
    /// </summary>
    public static bool SubmitBaseCost(int baseCost, out bool awardedPoint) {
        awardedPoint = false;
        if (baseCost <= 0) {
            return true;
        }

        long progress = (long)AnalysisProgressStore.DeepAnalysisProgress + baseCost;
        if (progress > int.MaxValue) {
            progress = int.MaxValue;
        }
        AnalysisProgressStore.DeepAnalysisProgress = (int)progress;

        int cost = GetNextPointCost();
        if (AnalysisProgressStore.DeepAnalysisProgress < cost) {
            return true;
        }

        AnalysisProgressStore.DeepAnalysisProgress -= cost;
        AncientTechTreeState.AwardPoint();
        awardedPoint = true;
        return true;
    }

    public static int GetNextPointCost() {
        int earned = Math.Max(0, AncientTechTreeState.TotalPointsEarned);
        long cost = 8000L + 4000L * (earned / 3);
        return cost > int.MaxValue ? int.MaxValue : (int)cost;
    }
}

#endregion // 深层解析服务
