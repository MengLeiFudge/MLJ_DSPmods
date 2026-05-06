using System;
using System.Collections.Generic;
using System.Linq;
using FE.Logic.Fractionation.Recipes;
using FE.Logic.Fractionation.Growth;
using UnityEngine;
using static FE.Logic.Manager.ItemManager;
using static FE.Utils.Utils;

namespace FE.Logic.Manager;

public static partial class GachaService {
    public static string GetModeNameKey() {
        return IsSpeedrunMode ? "速通模式" : "常规模式";
    }

    public static string GetPoolNameKey(int poolId) {
        if (IsSpeedrunMode) {
            return poolId switch {
                GachaPool.PoolIdOpeningLine => "阶段箱池",
                GachaPool.PoolIdProtoLoop => "简化原胚池",
                GachaPool.PoolIdGrowth => "简化成长池",
                GachaPool.PoolIdFocus => "速通聚焦层",
                _ => "阶段箱池",
            };
        }

        return poolId switch {
            GachaPool.PoolIdOpeningLine => "开线池",
            GachaPool.PoolIdProtoLoop => "原胚闭环池",
            GachaPool.PoolIdGrowth => "成长池",
            GachaPool.PoolIdFocus => "流派聚焦",
            _ => "开线池",
        };
    }

    public static string GetPoolDescKey(int poolId) {
        if (IsSpeedrunMode) {
            return poolId switch {
                GachaPool.PoolIdOpeningLine => "阶段箱池说明",
                GachaPool.PoolIdProtoLoop => "简化原胚池说明",
                GachaPool.PoolIdGrowth => "简化成长池说明",
                GachaPool.PoolIdFocus => "速通聚焦层说明",
                _ => "阶段箱池说明",
            };
        }

        return poolId switch {
            GachaPool.PoolIdOpeningLine => "开线池说明",
            GachaPool.PoolIdProtoLoop => "原胚闭环池说明",
            GachaPool.PoolIdGrowth => "成长池说明",
            GachaPool.PoolIdFocus => "流派聚焦说明",
            _ => "开线池说明",
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
