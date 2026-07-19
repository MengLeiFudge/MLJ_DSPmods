using FE.Logic.Civilization.Technology;

namespace FE.Logic.Civilization.Analysis;

/// <summary>
/// 将阶段基础协议完成后的检索机会转化为唯一的远古文明科技点进度。
/// </summary>
public static class DeepAnalysisService {
    public static bool SubmitOpportunity(out bool awardedPoint) {
        awardedPoint = false;
        AnalysisProgressStore.DeepAnalysisProgress++;
        int cost = GetNextPointCost();
        if (AnalysisProgressStore.DeepAnalysisProgress < cost) {
            return true;
        }

        AnalysisProgressStore.DeepAnalysisProgress -= cost;
        AncientTechTreeState.AwardPoint();
        awardedPoint = true;
        return true;
    }

    public static int GetNextPointCost() => 2 + AncientTechTreeState.TotalPointsEarned / 3;
}
