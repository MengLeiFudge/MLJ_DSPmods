using System.Collections.Generic;
using FE.Compatibility.Nebula;
using FE.Logic.Civilization.Protocols;
using FE.Logic.Civilization.Technology;
using FE.Logic.Fractionation.Process;

namespace FE.Logic.Civilization.Achievements;

/// <summary>
/// 低频评估类型化条件，完成后立即让固定奖励生效。
/// </summary>
public static class AchievementService {
    private static long lastEvaluationTick = long.MinValue;

    public static void Tick() {
        if (NebulaMultiplayerModAPI.IsMultiplayerActive && NebulaMultiplayerModAPI.IsClient) {
            return;
        }

        long tick = GameMain.gameTick;
        if (tick >= 0 && lastEvaluationTick >= 0 && tick - lastEvaluationTick < 60) {
            return;
        }
        lastEvaluationTick = tick;

        bool changed = false;
        foreach (AchievementDefinition definition in AchievementCatalog.All) {
            if (!AchievementState.IsCompleted(definition.AchievementKey) && IsConditionMet(definition)) {
                changed |= AchievementState.Complete(definition.AchievementKey);
            }
        }
        if (changed) {
            CivilizationRuntimeSync.Refresh();
            NebulaMultiplayerModAPI.BroadcastCivilizationState();
        }
    }

    public static IReadOnlyList<AchievementDefinition> GetCompletedDefinitions() {
        List<AchievementDefinition> result = [];
        foreach (AchievementDefinition definition in AchievementCatalog.All) {
            if (AchievementState.IsCompleted(definition.AchievementKey)) {
                result.Add(definition);
            }
        }
        return result;
    }

    public static long GetCurrentValue(AchievementDefinition definition) {
        return definition.ConditionType switch {
            AchievementConditionType.CompletedProtocols => CountCompletedProtocols(),
            AchievementConditionType.CompletedStages => ProtocolCatalog.GetCompletedStageCount(),
            AchievementConditionType.SpentTechPoints => AncientTechTreeState.TotalPointsSpent,
            AchievementConditionType.FractionationSuccesses => ProcessManager.totalFractionSuccesses,
            _ => 0,
        };
    }

    private static bool IsConditionMet(AchievementDefinition definition) =>
        GetCurrentValue(definition) >= definition.Target;

    private static int CountCompletedProtocols() {
        int count = 0;
        foreach (ProtocolDefinition definition in ProtocolCatalog.All) {
            if (ProtocolProgressStore.IsComplete(definition.RecipeKey)) {
                count++;
            }
        }
        return count;
    }

    public static void IntoOtherSave() {
        lastEvaluationTick = long.MinValue;
    }
}
