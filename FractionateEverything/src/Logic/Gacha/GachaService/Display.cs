using System.Collections.Generic;

namespace FE.Logic.Gacha;

/// <summary>
/// 抽取模式、卡池和奖励展示文本生成逻辑。
/// </summary>
public static partial class GachaService {
    public static string GetModeNameKey() {
        return IsSpeedrunMode ? "速通模式" : "常规模式";
    }

    public static string GetPoolNameKey(int poolId) {
        if (IsSpeedrunMode) {
            return poolId switch {
                GachaPool.PoolIdOpeningLine => "速通开线方向",
                GachaPool.PoolIdProtoLoop => "速通原胚方向",
                GachaPool.PoolIdGrowth => "速通成长规划",
                GachaPool.PoolIdFocus => "速通聚焦层",
                _ => "速通开线方向",
            };
        }

        return poolId switch {
            GachaPool.PoolIdOpeningLine => "主抽取开线偏好",
            GachaPool.PoolIdProtoLoop => "主抽取原胚偏好",
            GachaPool.PoolIdGrowth => "成长规划",
            GachaPool.PoolIdFocus => "流派聚焦",
            _ => "主抽取开线偏好",
        };
    }

    public static string GetPoolDescKey(int poolId) {
        if (IsSpeedrunMode) {
            return poolId switch {
                GachaPool.PoolIdOpeningLine => "速通开线方向说明",
                GachaPool.PoolIdProtoLoop => "速通原胚方向说明",
                GachaPool.PoolIdGrowth => "速通成长规划说明",
                GachaPool.PoolIdFocus => "速通聚焦层说明",
                _ => "速通开线方向说明",
            };
        }

        return poolId switch {
            GachaPool.PoolIdOpeningLine => "主抽取开线偏好说明",
            GachaPool.PoolIdProtoLoop => "主抽取原胚偏好说明",
            GachaPool.PoolIdGrowth => "成长规划说明",
            GachaPool.PoolIdFocus => "流派聚焦说明",
            _ => "主抽取开线偏好说明",
        };
    }

    public static GachaPool GetPool(int poolId) {
        EnsurePoolsFresh();
        return GachaPool.IsValidPoolId(poolId) ? poolsById[poolId] : null;
    }

    public static List<GachaPool> GetAllPools() {
        EnsurePoolsFresh();
        return [.. pools];
    }

    public static int GetDisplayPoolPoints() {
        return GachaManager.GetPoolPoints(GachaPool.PoolIdGrowth);
    }
}
